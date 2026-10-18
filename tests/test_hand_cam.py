import math
import sys
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import hand_cam as hc  # noqa: E402
import receiver  # noqa: E402
import json  # noqa: E402

# A right hand held up to the camera, palm facing it, fingers up (MediaPipe world coords: y down, z away).
UP = -1.0


def fake_hand(curl=0.0):
    pts = np.zeros((21, 3))
    xs = {1: 0.04, 2: 0.025, 3: 0.0, 4: -0.02, 5: -0.04}
    for f in range(1, 6):
        base = 1 + (f - 1) * 4
        for k in range(4):
            y = UP * (0.03 + 0.03 * (k + 1)) * (1 - curl * 0.6 * (k / 3))
            z = curl * 0.02 * k
            pts[base + k] = (xs[f], y, z)
    return pts


def test_mapping_is_a_handedness_flip():
    assert np.isclose(np.linalg.det(hc.CAM_TO_GAME), -1.0)


def test_fingers_up_to_camera_become_fingers_forward():
    g = hc.to_game([[0, -0.1, 0]])[0]          # 10 cm up in the image
    assert np.allclose(g, [0, 0, 0.1])
    g = hc.to_game([[0, 0, -0.05]])[0]          # 5 cm towards the camera (palm side)
    assert np.allclose(g, [0, -0.05, 0])        # → down onto the counter


def test_frame_has_rokoko_v4_names_and_parses():
    joints = hc.hand_joints("Right", fake_hand(), [0.2, 0.98, 0.35])
    names = {j["name"] for j in joints}
    assert len(joints) == 25
    assert {"RightHand", "RightFinger1Tip", "RightFinger2Metacarpal", "RightFinger5Tip"} <= names
    assert "RightFinger1Medial" not in names
    f = hc.frame({"Right": joints}, 1.0)
    assert receiver.detect_format(f) == "v4"
    assert receiver.decode(json.dumps(f).encode()) == f
    wrist = next(j for j in joints if j["name"] == "RightHand")["position"]
    assert math.isclose(wrist["x"], 0.2) and math.isclose(wrist["y"], 0.98)


def test_parents_point_inside_their_hand_when_both_hands_present():
    both = hc.frame({"Left": hc.hand_joints("Left", fake_hand(), [-0.2, 0.98, 0.35]),
                     "Right": hc.hand_joints("Right", fake_hand(), [0.2, 0.98, 0.35])}, 0.0)
    joints = both["scene"]["newtons"][0]["joints"]
    for j in joints:
        if j["parent"] >= 0:
            assert joints[j["parent"]]["name"][:4] == j["name"][:4]   # Left→Left, Righ→Righ


def test_wrist_from_image():
    centre = hc.wrist_from_image(0.5, 0.75, "Left")
    assert np.allclose(centre, [0.0, 0.98, 0.35])
    assert hc.wrist_from_image(0.2, 0.75, "Right")[0] > 0     # image-left is the player's right
    assert hc.wrist_from_image(0.5, 0.4, "Right")[2] > 0.35    # higher in the image = further over the counter
