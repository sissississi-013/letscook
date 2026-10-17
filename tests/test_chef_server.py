import json
import sys
from pathlib import Path
from types import SimpleNamespace

import anthropic
import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import chef_server  # noqa: E402

SUMMARY = {"plate": [{"ingredient": "bread", "cut": "whole", "doneness": "golden brown"},
                     {"ingredient": "butter", "cut": "whole", "doneness": "melted"}],
           "events": {"cuts": 0}}


class FakeMessages:
    def __init__(self, reply=None, error=None):
        self.reply, self.error, self.calls = reply, error, []

    def create(self, **kwargs):
        self.calls.append(kwargs)
        if self.error:
            raise self.error
        return SimpleNamespace(stop_reason="end_turn", content=[SimpleNamespace(type="text", text=json.dumps(self.reply))])


def fake_client(**kw):
    return SimpleNamespace(beta=SimpleNamespace(messages=FakeMessages(**kw)))


DISH = {"dish_name": "Sunrise Butter Toast", "course": "breakfast", "description": "d", "chef_note": "n",
        "unlock": {"recipe_name": "Eggy Bread", "course": "breakfast", "why": "w", "first_step": "crack an egg"}}


@pytest.fixture(autouse=True)
def temp_cookbook(tmp_path, monkeypatch):
    monkeypatch.setattr(chef_server, "COOKBOOK", tmp_path / "cookbook.json")
    monkeypatch.setattr(chef_server, "COOKBOOK_PAGE", tmp_path / "cookbook" / "index.html")


def test_names_dish_and_saves_to_cookbook():
    client = fake_client(reply=DISH)
    dish = chef_server.name_dish(SUMMARY, client=client)
    assert dish["dish_name"] == "Sunrise Butter Toast" and dish["offline"] is False
    assert dish["cookbook_count"] == 1
    book = chef_server.load_cookbook()
    assert book[0]["summary"] == SUMMARY

    call = client.beta.messages.calls[0]
    assert call["model"] == "claude-opus-5-5"
    assert call["output_config"]["format"]["type"] == "json_schema"
    assert call["system"][0]["cache_control"] == {"type": "ephemeral"}
    assert "plate_summary" in call["messages"][0]["content"][-1]["text"]


def test_history_is_sent_for_personal_unlocks():
    chef_server.name_dish(SUMMARY, client=fake_client(reply=DISH))
    client = fake_client(reply=DISH)
    chef_server.name_dish(SUMMARY, client=client)
    sent = json.loads(client.beta.messages.calls[0]["messages"][0]["content"][-1]["text"])
    assert sent["previous_dishes"][0]["dish_name"] == "Sunrise Butter Toast"
    assert sent["previous_dishes"][0]["ingredients"] == ["bread", "butter"]


def test_screenshot_goes_first_as_image():
    client = fake_client(reply=DISH)
    chef_server.name_dish(SUMMARY, screenshot_b64="aGk=", client=client)
    content = client.beta.messages.calls[0]["messages"][0]["content"]
    assert content[0]["type"] == "image" and content[1]["type"] == "text"


def test_offline_fallback_when_api_fails():
    err = RuntimeError("down")
    dish = chef_server.name_dish(SUMMARY, client=fake_client(error=err))
    assert dish["offline"] is True
    assert "Bread" in dish["dish_name"] and "Butter" in dish["dish_name"]
    assert chef_server.load_cookbook()[0]["dish"]["offline"] is True


def test_schema_requires_every_field():
    assert set(chef_server.SCHEMA["required"]) == set(chef_server.SCHEMA["properties"])
    assert chef_server.SCHEMA["additionalProperties"] is False


PNG_1PX = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=="


def test_cookbook_page_has_photo_and_escapes_names(tmp_path):
    evil = dict(DISH, dish_name="<script>alert(1)</script> Toast")
    chef_server.name_dish(SUMMARY, screenshot_b64=PNG_1PX, client=fake_client(reply=evil))
    page = (tmp_path / "cookbook" / "index.html").read_text()
    assert "&lt;script&gt;" in page and "<script>" not in page
    assert 'src="photos/dish-001.png"' in page
    assert (tmp_path / "cookbook" / "photos" / "dish-001.png").read_bytes()[:4] == b"\x89PNG"
    assert "Unlocked:" in page and "Eggy Bread" in page
