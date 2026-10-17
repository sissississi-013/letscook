"""Live check of the real gloves: reads what receiver.py is logging and shows each finger's curl as a bar.

Usage: .venv/bin/python glove_check.py        (needs receiver.py / start.sh running and Studio streaming to 14043)
       .venv/bin/python glove_check.py --once (print one reading and exit)
"""
import glob
import json
import math
import os
import sys
import time

FINGERS = ["thumb", "index", "middle", "ring", "little"]
PARTS = {1: ["Metacarpal", "Proximal", "Distal", "Tip"]}
MAX_BEND = {1: 125.0}


def parts(n):
    return PARTS.get(n, ["Metacarpal", "Proximal", "Medial", "Distal", "Tip"])


def curl(joints, side, n):
    """Same measure as the game (HandMath.Curl): summed bend between finger segments, 0 = straight, 1 = fist."""
    pts = []
    for p in parts(n):
        j = joints.get(f"{side}Finger{n}{p}")
        if j is None:
            return None
        pts.append((j["position"]["x"], j["position"]["y"], j["position"]["z"]))
    dirs = []
    for a, b in zip(pts, pts[1:]):
        d = [b[i] - a[i] for i in range(3)]
        L = math.sqrt(sum(x * x for x in d)) or 1.0
        dirs.append([x / L for x in d])
    bend = sum(math.degrees(math.acos(max(-1.0, min(1.0, sum(u[i] * v[i] for i in range(3)))))) for u, v in zip(dirs, dirs[1:]))
    return min(1.0, bend / MAX_BEND.get(n, 245.0))


def latest_frame(path):
    with open(path, "rb") as f:
        f.seek(0, os.SEEK_END)
        f.seek(max(0, f.tell() - 200_000))
        lines = f.read().decode("utf-8", "ignore").splitlines()
    for line in reversed(lines):
        try:
            rec = json.loads(line)
        except json.JSONDecodeError:
            continue
        if "frame" in rec:
            return rec
    return None


def render(rec):
    frame = rec["frame"]
    newtons = frame.get("scene", {}).get("newtons") or []
    if not newtons:
        return "frame has no JSON v4 'newtons'. Set the Studio receiver format to JSON v4."
    n = newtons[0]
    joints = {j["name"]: j for j in n.get("joints", [])}
    age = time.time() - rec["t"]
    out = [f"source: {n.get('name')}   last frame {age:.1f}s ago   joints: {len(joints)}"]
    if age > 2:
        out.append("⚠️  no fresh frames: is Studio's Custom Streaming activated on port 14043?")
    for side in ("Left", "Right"):
        out.append(f"\n{side} hand" + ("" if f"{side}Hand" in joints else "  (not in stream)"))
        for i, name in enumerate(FINGERS, start=1):
            c = curl(joints, side, i)
            bar = "·" * 20 if c is None else "█" * round(c * 20) + "░" * (20 - round(c * 20))
            out.append(f"  {name:<7}{bar} {'' if c is None else f'{c:.2f}'}")
    return "\n".join(out)


def main():
    once = "--once" in sys.argv
    while True:
        files = sorted(glob.glob(os.path.join(os.path.dirname(__file__), "sessions", "2*.jsonl")), key=os.path.getmtime)
        text = "no glove sessions yet. Start receiver.py (or start.sh)."
        if files:
            rec = latest_frame(files[-1])
            text = render(rec) if rec else f"{os.path.basename(files[-1])}: waiting for the first frame…"
        if once:
            print(text)
            return
        print("\033[2J\033[H" + "let's cook! glove check (Ctrl+C to quit)\n" + text, flush=True)
        time.sleep(0.2)


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        pass
