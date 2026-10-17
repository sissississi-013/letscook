import json
import math
import sys
from pathlib import Path

import lz4.frame

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import mock_glove  # noqa: E402
import receiver  # noqa: E402


def joints_by_name(frame):
    return {j["name"]: j for j in frame["scene"]["newtons"][0]["joints"]}


def test_decode_plain_json():
    frame = mock_glove.make_frame(0.0)
    assert receiver.decode(json.dumps(frame).encode()) == frame


def test_decode_lz4_frame():
    frame = mock_glove.make_frame(0.5)
    payload = lz4.frame.compress(json.dumps(frame).encode())
    assert payload[:4] == receiver.LZ4_MAGIC
    assert receiver.decode(payload) == frame


def test_decode_garbage_returns_none():
    assert receiver.decode(b"\x00\x01not json") is None
    assert receiver.decode(receiver.LZ4_MAGIC + b"broken") is None


def test_detect_format():
    assert receiver.detect_format(mock_glove.make_frame(0.0)) == "v4"
    v3 = {"scene": {"actors": [{"name": "a"}], "newtons": []}}
    assert receiver.detect_format(v3) == "v3"
    assert receiver.detect_format({"scene": {"actors": [], "newtons": []}}) == "unknown"


def test_mock_has_full_hands_with_v4_names():
    names = joints_by_name(mock_glove.make_frame(0.0))
    for side in ("Left", "Right"):
        assert f"{side}Hand" in names
        assert f"{side}Finger1Tip" in names
        assert f"{side}Finger1Medial" not in names  # thumb has no medial joint in v4
        for n in range(2, 6):
            for part in ("Metacarpal", "Proximal", "Medial", "Distal", "Tip"):
                assert f"{side}Finger{n}{part}" in names
    assert len(names) == 2 * 25


def test_parent_indices_point_to_real_joints():
    joints = mock_glove.make_frame(0.0)["scene"]["newtons"][0]["joints"]
    for j in joints:
        assert j["parent"] == -1 or 0 <= j["parent"] < len(joints)
    hand = joints[0]
    assert hand["name"] == "LeftHand" and hand["parent"] == -1


def test_curling_pulls_index_tip_back_toward_wrist():
    def reach(pose):
        n = joints_by_name(mock_glove.make_frame(0, pose))
        a, b = n["LeftHand"]["position"], n["LeftFinger2Tip"]["position"]
        return math.dist((a["x"], a["y"], a["z"]), (b["x"], b["y"], b["z"]))
    assert reach("fist") < reach("open") - 0.06


def test_quaternions_are_unit():
    for j in mock_glove.make_frame(1.2)["scene"]["newtons"][0]["joints"]:
        r = j["rotation"]
        assert math.isclose(r["x"] ** 2 + r["y"] ** 2 + r["z"] ** 2 + r["w"] ** 2, 1.0, abs_tol=1e-5)


def test_pinch_closes_thumb_and_index_on_both_hands():
    def gap(pose, side):
        n = joints_by_name(mock_glove.make_frame(0, pose))
        a, b = n[f"{side}Finger1Tip"]["position"], n[f"{side}Finger2Tip"]["position"]
        return math.dist((a["x"], a["y"], a["z"]), (b["x"], b["y"], b["z"]))
    for side in ("Left", "Right"):
        assert gap("pinch", side) < 0.015
        assert gap("open", side) > 0.06


def test_thumb_sits_on_the_inner_side():
    n = joints_by_name(mock_glove.make_frame(0, "open"))
    assert n["LeftFinger1Tip"]["position"]["x"] > n["LeftHand"]["position"]["x"]
    assert n["RightFinger1Tip"]["position"]["x"] < n["RightHand"]["position"]["x"]
