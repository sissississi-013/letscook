"""Local "chef" server: Unity posts a finished dish, Claude names it and unlocks something new.

Run:  .venv/bin/python chef_server.py          (listens on 127.0.0.1:8776)

POST /dish   {"summary": {...}, "screenshot_b64": "<png base64, optional>"}
             → {"dish_name", "course", "description", "chef_note", "unlock": {...}, "offline": bool}
GET  /cookbook → every dish so far (cookbook.json)

The API key stays here (ANTHROPIC_API_KEY from the environment); Unity never sees it.
If Claude can't be reached, a simple offline name is returned so the game never blocks.
"""
import json
import sys
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

import anthropic

ROOT = Path(__file__).parent
COOKBOOK = ROOT / "cookbook.json"
MODEL = "claude-opus-5-5"
PORT = 8776

SYSTEM = """You are the warm, witty chef-host of "let's cook!", a free-play cooking sandbox played with motion-capture gloves.
There are no recipes and no wrong answers: players cook whatever they like. Your job is to look at what they actually made
and give it meaning.

You receive a JSON summary of the plate (each piece: ingredient, how it was cut, cooking state such as "golden brown" or
"raw", temperatures), what happened while cooking (cuts, grabs, heat), the player's previous dishes, and sometimes a
screenshot of the plate.

Respond with:
- dish_name: a memorable, playful but appetizing name (max 6 words) that fits what is really on the plate. Avoid generic
  names like "Toast with Butter"; avoid puns that ignore the food.
- course: which course this would be in a meal. Use one of: amuse-bouche, breakfast, starter, soup, salad, main, side,
  dessert, snack, drink. Pick what a thoughtful chef would say given the ingredients and how they were cooked.
- description: 1–2 sentences describing the dish as if on a menu, grounded in the actual cooking states (mention burnt
  bits honestly but kindly).
- chef_note: one short, encouraging, specific remark about technique (e.g. the browning, the knife work).
- unlock: a NEW recipe idea the player can try next, personal to them: build on ingredients or techniques they used or
  keep returning to (see their history), and nudge them one step further. Give recipe_name, course, why (one sentence
  linking it to what they just did), and first_step (one concrete hand action to start it).

Be kind, specific and brief. Never claim something is on the plate that isn't in the summary."""

SCHEMA = {
    "type": "object",
    "properties": {
        "dish_name": {"type": "string"},
        "course": {"type": "string", "enum": ["amuse-bouche", "breakfast", "starter", "soup", "salad", "main", "side", "dessert", "snack", "drink"]},
        "description": {"type": "string"},
        "chef_note": {"type": "string"},
        "unlock": {
            "type": "object",
            "properties": {
                "recipe_name": {"type": "string"},
                "course": {"type": "string"},
                "why": {"type": "string"},
                "first_step": {"type": "string"},
            },
            "required": ["recipe_name", "course", "why", "first_step"],
            "additionalProperties": False,
        },
    },
    "required": ["dish_name", "course", "description", "chef_note", "unlock"],
    "additionalProperties": False,
}


def load_cookbook():
    try:
        return json.loads(COOKBOOK.read_text())
    except (FileNotFoundError, json.JSONDecodeError):
        return []


def save_dish(entry):
    book = load_cookbook()
    book.append(entry)
    COOKBOOK.write_text(json.dumps(book, indent=2))
    return len(book)


def history_for_prompt(book, limit=10):
    return [{"dish_name": d["dish"]["dish_name"], "course": d["dish"]["course"],
             "ingredients": sorted({p.get("ingredient", "?") for p in d["summary"].get("plate", [])})}
            for d in book[-limit:]]


def offline_dish(summary):
    plate = summary.get("plate", [])
    names = sorted({p.get("ingredient", "something") for p in plate}) or ["mystery"]
    state = plate[0].get("doneness", "") if plate else ""
    return {
        "dish_name": f"{state.split(',')[0].title()} {' & '.join(n.title() for n in names)}".strip(),
        "course": "snack",
        "description": "Plated with care (the chef is offline, so this name is a placeholder).",
        "chef_note": "Claude couldn't be reached; your dish is saved and can be renamed later.",
        "unlock": {"recipe_name": "Anything you like", "course": "main", "why": "Free play!", "first_step": "Pick up the knife."},
        "offline": True,
    }


def ask_claude(client, summary, screenshot_b64=None, history=None):
    content = []
    if screenshot_b64:
        content.append({"type": "image", "source": {"type": "base64", "media_type": "image/png", "data": screenshot_b64}})
    content.append({"type": "text", "text": json.dumps({"plate_summary": summary, "previous_dishes": history or []}, indent=1)})
    response = client.beta.messages.create(
        model=MODEL,
        max_tokens=4000,
        betas=["server-side-fallback-2026-07-01"],
        fallbacks="default",
        system=[{"type": "text", "text": SYSTEM, "cache_control": {"type": "ephemeral"}}],
        output_config={"effort": "low", "format": {"type": "json_schema", "schema": SCHEMA}},
        messages=[{"role": "user", "content": content}],
    )
    if response.stop_reason == "refusal":
        raise RuntimeError("Claude declined to name this dish")
    text = next(b.text for b in response.content if b.type == "text")
    dish = json.loads(text)
    dish["offline"] = False
    return dish


def name_dish(summary, screenshot_b64=None, client=None):
    book = load_cookbook()
    try:
        dish = ask_claude(client or anthropic.Anthropic(), summary, screenshot_b64, history_for_prompt(book))
    except (anthropic.APIError, RuntimeError, StopIteration, json.JSONDecodeError) as e:
        print(f"[chef] offline fallback: {type(e).__name__}: {e}", file=sys.stderr, flush=True)
        dish = offline_dish(summary)
    count = save_dish({"time": time.strftime("%Y-%m-%d %H:%M:%S"), "summary": summary, "dish": dish})
    dish["cookbook_count"] = count
    return dish


class Handler(BaseHTTPRequestHandler):
    def _send(self, code, payload):
        body = json.dumps(payload).encode()
        self.send_response(code)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def do_GET(self):
        if self.path == "/cookbook":
            return self._send(200, load_cookbook())
        if self.path == "/health":
            return self._send(200, {"ok": True})
        self._send(404, {"error": "not found"})

    def do_POST(self):
        length = int(self.headers.get("Content-Length", 0))
        try:
            req = json.loads(self.rfile.read(length) or b"{}")
        except json.JSONDecodeError:
            return self._send(400, {"error": "bad json"})
        if self.path == "/dish":
            dish = name_dish(req.get("summary", {}), req.get("screenshot_b64"))
            print(f"[chef] {dish['dish_name']} ({dish['course']})", flush=True)
            return self._send(200, dish)
        self._send(404, {"error": "not found"})

    def log_message(self, fmt, *args):
        pass


def main():
    server = ThreadingHTTPServer(("127.0.0.1", PORT), Handler)
    print(f"[chef] listening on http://127.0.0.1:{PORT} (model {MODEL})", flush=True)
    server.serve_forever()


if __name__ == "__main__":
    main()
