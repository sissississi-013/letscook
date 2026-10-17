import json
import sys
from pathlib import Path
from types import SimpleNamespace

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import chef_server  # noqa: E402
from test_chef_server import fake_client  # noqa: E402


def test_library_is_valid_and_survives_cleaning():
    lib = chef_server.load_library()
    assert len(lib) >= 10
    for tool in lib:
        assert chef_server.clean_tool(tool)["parts"] == [
            {k: (list(map(float, p[k])) if k in ("size", "offset", "rotation") else p[k]) for k in p} for p in tool["parts"]]


def test_library_matches_aliases_inside_sentences():
    assert chef_server.find_in_library("can I get a mortar and pestle please")["name"] == "mortar"
    assert chef_server.find_in_library("I NEED A WHISK")["name"] == "whisk"
    assert chef_server.find_in_library("a spiralizer") is None


def test_library_hit_never_calls_claude():
    client = fake_client(error=AssertionError("should not be called"))
    assert chef_server.design_tool("rolling pin", client=client)["source"] == "library"


def test_claude_tool_is_clamped():
    wild = {"name": "giant spatula", "mass_kg": 99, "abilities": ["flips", "teleports"],
            "parts": [{"role": "head", "shape": "box", "size": [3, 0.0001], "offset": [0, 0, 9], "rotation": [0, 0, 0], "material": "unobtainium"}]}
    out = chef_server.design_tool("a spatula the size of a door", client=fake_client(reply=wild))
    assert out["source"] == "claude"
    t = out["tool"]
    assert t["mass_kg"] == 5.0 and t["abilities"] == ["flips"]
    p = t["parts"][0]
    assert p["size"] == [0.6, 0.003, 0.05] and p["offset"][2] == 0.5 and p["material"] == "steel"


def test_claude_request_shape():
    client = fake_client(reply={"name": "x", "mass_kg": 0.1, "abilities": [], "parts": []})
    chef_server.design_tool("a spiralizer", client=client)
    call = client.beta.messages.calls[0]
    assert call["output_config"]["format"]["schema"] is chef_server.TOOL_SCHEMA
    assert "spiralizer" in call["messages"][0]["content"]


def test_offline_tool_still_usable():
    out = chef_server.design_tool("a spiralizer", client=fake_client(error=RuntimeError("down")))
    assert out["source"] == "offline" and out["tool"]["parts"]
