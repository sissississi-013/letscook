import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import wrist_cam as wc  # noqa: E402

REST_W = (0.2, 0.1, -0.3)
REST_G = (0.22, 1.0, 0.35)


def test_rest_pose_maps_to_rest_anchor():
    assert wc.to_game(REST_W, REST_W, REST_G) == list(REST_G)


def test_directions():
    # Player moves their right hand to their right → image left (x decreases) → game +x
    assert wc.to_game((0.1, 0.1, -0.3), REST_W, REST_G)[0] > REST_G[0]
    # Hand goes up → MediaPipe y decreases → game y increases
    assert wc.to_game((0.2, 0.0, -0.3), REST_W, REST_G)[1] > REST_G[1]
    # Reach toward the camera (over the counter) → z decreases → game z increases
    assert wc.to_game((0.2, 0.1, -0.5), REST_W, REST_G)[2] > REST_G[2]


def test_scale():
    moved = wc.to_game((0.2, 0.0, -0.3), REST_W, REST_G, scale=2.0)
    assert abs(moved[1] - (1.0 + 0.2)) < 1e-9


def test_calibrator_averages_then_locks():
    c = wc.Calibrator(frames=3)
    for p in [(0, 0, 0), (0.3, 0, 0), (0.6, 0, 0)]:
        c.add("Left", p)
    assert not c.done
    for p in [(1, 1, 1)] * 3:
        c.add("Right", p)
    assert c.done and abs(c.rest["Left"][0] - 0.3) < 1e-9
    c.add("Left", (9, 9, 9))
    assert abs(c.rest["Left"][0] - 0.3) < 1e-9
    c.reset()
    assert not c.done


def test_packet_shape():
    msg = json.loads(wc.packet({"Left": {"ok": True, "pos": [0, 1, 0.3]}, "Right": {"ok": False}}))
    assert msg["hands"]["Left"]["pos"] == [0, 1, 0.3]
    assert msg["hands"]["Right"]["ok"] is False
