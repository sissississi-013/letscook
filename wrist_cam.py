"""Webcam wrist tracking: moves the game's hands around the kitchen (the gloves only give finger + wrist rotation).

Usage: .venv/bin/python wrist_cam.py [--camera 0] [--scale 1.2] [--no-preview]
- Sit/stand facing the webcam with your hands resting where the counter would be. The first second calibrates
  that pose to the game's resting hand positions. Press C in the preview window to recalibrate, Q to quit.
- Uses MediaPipe *pose* (body) wrists rather than hand tracking, because body pose doesn't depend on seeing bare
  fingers (you're wearing gloves). Positions are MediaPipe world landmarks: metres, hip-centred.
Sends UDP JSON to Unity (127.0.0.1:14046): {"t": ..., "hands": {"Left": {"pos": [x, y, z], "ok": true}, ...}}
in game coordinates (left-handed, Y-up, metres).
"""
import argparse
import json
import socket
import sys
import time
import urllib.request
from pathlib import Path

ROOT = Path(__file__).parent
MODEL = ROOT / "models" / "pose_landmarker_lite.task"
MODEL_URL = "https://storage.googleapis.com/mediapipe-models/pose_landmarker/pose_landmarker_lite/float16/latest/pose_landmarker_lite.task"
LEFT_WRIST, RIGHT_WRIST = 15, 16           # MediaPipe pose landmark indices (the person's own left/right)
REST = {"Left": (-0.22, 1.0, 0.35), "Right": (0.22, 1.0, 0.35)}   # game wrist anchors at rest
MIN_VISIBILITY = 0.5


def to_game(world, rest_world, rest_game, scale=1.0):
    """MediaPipe world (x → image right, y down, z smaller = closer to camera) → game (x right, y up, z forward).
    The webcam faces the player, so the player's right is image left (flip x), reaching toward the camera is
    reaching forward over the counter (flip z), and up is -y."""
    dx = -(world[0] - rest_world[0])
    dy = -(world[1] - rest_world[1])
    dz = -(world[2] - rest_world[2])
    return [rest_game[0] + dx * scale, rest_game[1] + dy * scale, rest_game[2] + dz * scale]


class Calibrator:
    """Averages the first `frames` good readings per hand as the resting pose."""

    def __init__(self, frames=30):
        self.frames, self.samples, self.rest = frames, {"Left": [], "Right": []}, {}

    def reset(self):
        self.samples, self.rest = {"Left": [], "Right": []}, {}

    def add(self, side, world):
        if side in self.rest:
            return
        self.samples[side].append(world)
        if len(self.samples[side]) >= self.frames:
            s = self.samples[side]
            self.rest[side] = tuple(sum(p[i] for p in s) / len(s) for i in range(3))

    @property
    def done(self):
        return len(self.rest) == 2


def packet(hands):
    return json.dumps({"t": time.time(), "hands": hands}).encode()


def ensure_model():
    if not MODEL.exists():
        MODEL.parent.mkdir(exist_ok=True)
        print(f"downloading {MODEL.name}…", flush=True)
        urllib.request.urlretrieve(MODEL_URL, MODEL)
    return MODEL


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--camera", type=int, default=0)
    ap.add_argument("--port", type=int, default=14046)
    ap.add_argument("--scale", type=float, default=1.2, help="game metres per real metre of hand movement")
    ap.add_argument("--no-preview", action="store_true")
    args = ap.parse_args()

    import cv2
    import mediapipe as mp
    from mediapipe.tasks import python as mp_python
    from mediapipe.tasks.python import vision

    options = vision.PoseLandmarkerOptions(
        base_options=mp_python.BaseOptions(model_asset_path=str(ensure_model())),
        running_mode=vision.RunningMode.VIDEO,
        num_poses=1,
    )
    landmarker = vision.PoseLandmarker.create_from_options(options)
    cap = cv2.VideoCapture(args.camera)
    if not cap.isOpened():
        sys.exit("Can't open the webcam. Allow camera access for Terminal in System Settings → Privacy & Security → Camera.")

    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    calib = Calibrator()
    start = time.monotonic()
    print("Hold your hands still, resting over the counter, for a second to calibrate…", flush=True)
    while True:
        ok, frame = cap.read()
        if not ok:
            continue
        rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
        result = landmarker.detect_for_video(mp.Image(image_format=mp.ImageFormat.SRGB, data=rgb), int((time.monotonic() - start) * 1000))
        hands = {}
        if result.pose_world_landmarks:
            world, image = result.pose_world_landmarks[0], result.pose_landmarks[0]
            for side, idx in (("Left", LEFT_WRIST), ("Right", RIGHT_WRIST)):
                lm = world[idx]
                if (image[idx].visibility or 0) < MIN_VISIBILITY:
                    hands[side] = {"ok": False}
                    continue
                w = (lm.x, lm.y, lm.z)
                calib.add(side, w)
                if side in calib.rest:
                    hands[side] = {"ok": True, "pos": [round(v, 4) for v in to_game(w, calib.rest[side], REST[side], args.scale)]}
                h, wd = frame.shape[:2]
                cv2.circle(frame, (int(image[idx].x * wd), int(image[idx].y * h)), 10, (0, 200, 255) if side == "Left" else (255, 120, 0), -1)
        if calib.done and hands:
            sock.sendto(packet(hands), ("127.0.0.1", args.port))

        if not args.no_preview:
            view = cv2.flip(frame, 1)   # mirror only for display, so it feels like a mirror
            label = "tracking" if calib.done else "calibrating: hold still"
            cv2.putText(view, f"let's cook! wrist cam - {label} (C recalibrate, Q quit)", (12, 28), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (40, 40, 40), 2)
            cv2.imshow("wrist cam", view)
            key = cv2.waitKey(1) & 0xFF
            if key == ord("q"):
                break
            if key == ord("c"):
                calib.reset()
                print("recalibrating…", flush=True)
    cap.release()


if __name__ == "__main__":
    main()
