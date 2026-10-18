"""Webcam hands: play let's cook! with no gloves. MediaPipe tracks all 21 finger points of both hands and this
script sends them in Rokoko Studio's JSON v4 format, so the game, the logger and the robot-data tools can't tell the
difference from real Smartgloves. Hand position in the image also moves the wrists around the kitchen.

Usage: .venv/bin/python hand_cam.py [--camera 0] [--no-preview]
Hold your hands up in front of the webcam, palms facing the screen, fingers pointing up: in the game that's
palms down over the counter, fingers pointing forward. Pinch thumb and index to grab. Q quits the preview.
"""
import argparse
import json
import socket
import sys
import time
import urllib.request
from pathlib import Path

import numpy as np

ROOT = Path(__file__).parent
MODEL = ROOT / "models" / "hand_landmarker.task"
MODEL_URL = "https://storage.googleapis.com/mediapipe-models/hand_landmarker/hand_landmarker/float16/latest/hand_landmarker.task"

# MediaPipe landmark index → Rokoko JSON v4 joint suffix (thumb has no Medial).
LANDMARK_JOINTS = {
    0: "Hand",
    1: "Finger1Metacarpal", 2: "Finger1Proximal", 3: "Finger1Distal", 4: "Finger1Tip",
    5: "Finger2Proximal", 6: "Finger2Medial", 7: "Finger2Distal", 8: "Finger2Tip",
    9: "Finger3Proximal", 10: "Finger3Medial", 11: "Finger3Distal", 12: "Finger3Tip",
    13: "Finger4Proximal", 14: "Finger4Medial", 15: "Finger4Distal", 16: "Finger4Tip",
    17: "Finger5Proximal", 18: "Finger5Medial", 19: "Finger5Distal", 20: "Finger5Tip",
}
MCP = {2: 5, 3: 9, 4: 13, 5: 17}   # finger → its knuckle landmark, for the synthetic metacarpal joint
CHAINS = {1: ["Hand", "Finger1Metacarpal", "Finger1Proximal", "Finger1Distal", "Finger1Tip"]}
for f in range(2, 6):
    CHAINS[f] = ["Hand"] + [f"Finger{f}{p}" for p in ("Metacarpal", "Proximal", "Medial", "Distal", "Tip")]

# Camera (MediaPipe world: x image-right, y down, z away from camera; right-handed) → game (Unity: x right,
# y up, z forward; left-handed). The camera faces the player: image-right is the player's left (−x), fingers
# pointing up become fingers pointing forward (+z), palms facing the camera become palms facing down (+z_mp → +y).
CAM_TO_GAME = np.array([[-1, 0, 0], [0, 0, 1], [0, -1, 0]], dtype=float)


def to_game(points):
    return np.asarray(points, dtype=float) @ CAM_TO_GAME.T


def hand_joints(side, world_landmarks, wrist_pos):
    """21 MediaPipe world landmarks (metres) → list of Rokoko-style joints for one hand, anchored at wrist_pos."""
    pts = to_game(world_landmarks)
    pts = pts - pts[0] + np.asarray(wrist_pos, dtype=float)
    pos = {f"{side}{LANDMARK_JOINTS[i]}": pts[i] for i in range(21)}
    for f, mcp in MCP.items():   # MediaPipe has no metacarpal base: put it a fifth of the way to the knuckle
        pos[f"{side}Finger{f}Metacarpal"] = pts[0] + (pts[mcp] - pts[0]) * 0.2
    names = [f"{side}Hand"] + [f"{side}{n}" for f in CHAINS for n in CHAINS[f][1:]]
    index = {n: i for i, n in enumerate(names)}
    joints = []
    for f, chain in CHAINS.items():
        for parent, child in zip(chain, chain[1:]):
            joints.append((f"{side}{child}", f"{side}{parent}"))
    out = [{"name": f"{side}Hand", "parent": -1}]
    out += [{"name": c, "parent": index[p]} for c, p in joints]
    for j in out:
        x, y, z = pos[j["name"]]
        j["position"] = {"x": round(float(x), 5), "y": round(float(y), 5), "z": round(float(z), 5)}
        j["rotation"] = {"x": 0.0, "y": 0.0, "z": 0.0, "w": 1.0}
    return out


def frame(hands, t):
    """hands: {"Left"/"Right": joints list} → a JSON v4 frame dict."""
    joints = []
    for side in ("Left", "Right"):
        if side in hands:
            base = len(joints)
            for j in hands[side]:
                j = dict(j)
                if j["parent"] >= 0:
                    j["parent"] += base
                joints.append(j)
    return {"version": "3,0", "fps": 30, "scene": {"timestamp": t, "actors": [], "props": [], "characters": [],
            "newtons": [{"name": "Webcam Hands", "color": [80, 160, 255],
                         "meta": {"hasHands": True, "hasLeftHand": "Left" in hands, "hasRightHand": "Right" in hands,
                                  "hasBody": False, "hasFace": False},
                         "dimensions": {"totalHeight": 1.7, "hipHeight": 0.9}, "joints": joints}]}}


def wrist_from_image(u, v, side):
    """Normalised image position of the wrist → where it sits over the counter (game metres)."""
    x = -(u - 0.5) * 1.1                                   # unmirrored image: image-right is the player's left
    z = 0.35 + (0.75 - v) * 0.7                            # higher in the image = further over the counter
    return [float(np.clip(x, -0.7, 0.7)), 0.98, float(np.clip(z, 0.05, 0.8))]


def ensure_model():
    if not MODEL.exists():
        MODEL.parent.mkdir(exist_ok=True)
        urllib.request.urlretrieve(MODEL_URL, MODEL)
    return MODEL


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--camera", type=int, default=0)
    ap.add_argument("--ports", type=int, nargs="+", default=[14045, 14043])
    ap.add_argument("--wrist-port", type=int, default=14046)
    ap.add_argument("--no-preview", action="store_true")
    args = ap.parse_args()

    import cv2
    import mediapipe as mp
    from mediapipe.tasks import python as mp_python
    from mediapipe.tasks.python import vision

    landmarker = vision.HandLandmarker.create_from_options(vision.HandLandmarkerOptions(
        base_options=mp_python.BaseOptions(model_asset_path=str(ensure_model())),
        running_mode=vision.RunningMode.VIDEO, num_hands=2,
        min_hand_detection_confidence=0.5, min_tracking_confidence=0.5))
    cap = cv2.VideoCapture(args.camera)
    if not cap.isOpened():
        sys.exit("Can't open the webcam. Allow camera access for Terminal in System Settings → Privacy & Security → Camera.")
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    sock.setsockopt(socket.SOL_SOCKET, socket.SO_SNDBUF, 65535)
    start = time.monotonic()
    print("Webcam hands running: palms to the screen, fingers up. Q quits.", flush=True)
    while True:
        ok, img = cap.read()
        if not ok:
            continue
        t = time.monotonic() - start
        res = landmarker.detect_for_video(mp.Image(image_format=mp.ImageFormat.SRGB, data=cv2.cvtColor(img, cv2.COLOR_BGR2RGB)), int(t * 1000))
        hands, wrists = {}, {}
        for lm, wlm, handed in zip(res.hand_landmarks, res.hand_world_landmarks, res.handedness):
            # MediaPipe labels assume a mirrored selfie image; this frame is unmirrored, so the label is swapped.
            side = "Right" if handed[0].category_name == "Left" else "Left"
            wrist = wrist_from_image(lm[0].x, lm[0].y, side)
            hands[side] = hand_joints(side, [(p.x, p.y, p.z) for p in wlm], wrist)
            wrists[side] = {"ok": True, "pos": wrist}
            h, w = img.shape[:2]
            for p in lm:
                cv2.circle(img, (int(p.x * w), int(p.y * h)), 3, (0, 200, 255) if side == "Left" else (255, 120, 0), -1)
        if hands:
            payload = json.dumps(frame(hands, t)).encode()
            for port in args.ports:
                sock.sendto(payload, ("127.0.0.1", port))
            sock.sendto(json.dumps({"t": time.time(), "hands": wrists}).encode(), ("127.0.0.1", args.wrist_port))
        if not args.no_preview:
            cv2.imshow("let's cook! webcam hands (Q quits)", cv2.flip(img, 1))
            if cv2.waitKey(1) & 0xFF == ord("q"):
                break
    cap.release()


if __name__ == "__main__":
    main()
