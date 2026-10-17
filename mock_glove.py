"""Fake Rokoko Studio Custom Streaming (JSON v4) so everything can be built without real gloves.

Usage:
  python3 mock_glove.py                       # 60 fps to 127.0.0.1:14043 and :14045, cycling poses
  python3 mock_glove.py --pose pinch          # hold one pose
  python3 mock_glove.py --ports 15043 --lz4   # LZ4-frame compressed, custom port

Frames follow research/rokoko-streaming.md §2.4: world-space joints, left-handed Y-up Z-forward,
metres, quaternions x,y,z,w. Hands are palm-down with fingers pointing +Z.
"""
import argparse
import json
import math
import socket
import time

FINGERS = [1, 2, 3, 4, 5]  # 1 = thumb ... 5 = little
POSES = {
    #        thumb index middle ring little   (0 = straight, 1 = fully curled)
    "open":  [0.0, 0.0, 0.0, 0.0, 0.0],
    "fist":  [1.0, 1.0, 1.0, 1.0, 1.0],
    "pinch": [0.75, 0.6, 0.15, 0.15, 0.15],
    "point": [0.8, 0.0, 1.0, 1.0, 1.0],
}
CYCLE = ["open", "fist", "open", "pinch", "open", "point"]
SECONDS_PER_POSE = 1.5

# Finger base offsets across the hand (x for a LEFT hand; mirrored for right) and segment lengths (m)
BASE_X = {2: 0.030, 3: 0.010, 4: -0.010, 5: -0.028}
SEGMENTS = {  # metacarpal, proximal, medial, distal (thumb has no medial)
    1: [0.045, 0.035, 0.030],
    2: [0.070, 0.042, 0.025, 0.020],
    3: [0.068, 0.046, 0.028, 0.021],
    4: [0.065, 0.043, 0.027, 0.020],
    5: [0.060, 0.035, 0.020, 0.018],
}
MAX_BEND = [0.0, math.radians(80), math.radians(95), math.radians(70)]  # per joint, metacarpal stays straight
WRISTS = {"Left": (-0.22, 1.0, 0.35), "Right": (0.22, 1.0, 0.35)}


def quat_axis_angle(axis, angle):
    ax, ay, az = axis
    n = math.sqrt(ax * ax + ay * ay + az * az)
    s = math.sin(angle / 2) / n
    return (ax * s, ay * s, az * s, math.cos(angle / 2))


def quat_mul(a, b):
    ax, ay, az, aw = a
    bx, by, bz, bw = b
    return (
        aw * bx + ax * bw + ay * bz - az * by,
        aw * by - ax * bz + ay * bw + az * bx,
        aw * bz + ax * by - ay * bx + az * bw,
        aw * bw - ax * bx - ay * by - az * bz,
    )


def quat_rotate(q, v):
    qv = (v[0], v[1], v[2], 0.0)
    conj = (-q[0], -q[1], -q[2], q[3])
    x, y, z, _ = quat_mul(quat_mul(q, qv), conj)
    return (x, y, z)


def joint(name, parent, pos, rot):
    p = [round(v, 5) for v in pos]
    r = [round(v, 6) for v in rot]
    return {
        "name": name,
        "parent": parent,
        "position": {"x": p[0], "y": p[1], "z": p[2]},
        "rotation": {"x": r[0], "y": r[1], "z": r[2], "w": r[3]},
    }


def hand_joints(side, curls, start_index):
    """Return joints for one hand. curls: 5 values in [0, 1], thumb first."""
    mirror = 1 if side == "Left" else -1
    wrist = WRISTS[side]
    hand_rot = (0.0, 0.0, 0.0, 1.0)
    joints = [joint(f"{side}Hand", -1, wrist, hand_rot)]
    hand_idx = start_index

    for n in FINGERS:
        curl = curls[n - 1]
        if n == 1:
            # Thumb starts at the inner side of the palm, angled forward, and curls across the palm
            base = (wrist[0] + mirror * 0.025, wrist[1] - 0.01, wrist[2] + 0.02)
            rot = quat_mul(hand_rot, quat_axis_angle((0, 1, 0), mirror * math.radians(-40)))
            bend_axis = (0.3, 0, -mirror * 1.0)
            names = ["Metacarpal", "Proximal", "Distal", "Tip"]
            bends = [0.0, math.radians(55), math.radians(70)]
        else:
            base = (wrist[0] + mirror * BASE_X[n], wrist[1], wrist[2] + 0.01)
            rot = hand_rot
            bend_axis = (1, 0, 0)
            names = ["Metacarpal", "Proximal", "Medial", "Distal", "Tip"]
            bends = MAX_BEND

        pos = base
        parent = hand_idx
        for seg, part in enumerate(names):
            if seg < len(bends):
                rot = quat_mul(rot, quat_axis_angle(bend_axis, bends[seg] * curl))
            joints.append(joint(f"{side}Finger{n}{part}", parent, pos, rot))
            parent = start_index + len(joints) - 1
            if seg < len(SEGMENTS[n]):
                d = quat_rotate(rot, (0, 0, SEGMENTS[n][seg]))
                pos = (pos[0] + d[0], pos[1] + d[1], pos[2] + d[2])
    return joints


def pose_at(t, fixed=None):
    if fixed:
        return POSES[fixed]
    phase = t / SECONDS_PER_POSE
    i = int(phase) % len(CYCLE)
    a, b = POSES[CYCLE[i]], POSES[CYCLE[(i + 1) % len(CYCLE)]]
    k = phase - int(phase)
    k = k * k * (3 - 2 * k)  # smoothstep
    return [x + (y - x) * k for x, y in zip(a, b)]


def make_frame(t, fixed_pose=None):
    curls = pose_at(t, fixed_pose)
    left = hand_joints("Left", curls, 0)
    right = hand_joints("Right", curls, len(left))
    return {
        "version": "3,0",
        "fps": 60,
        "scene": {
            "timestamp": t,
            "actors": [],
            "newtons": [{
                "name": "Mock Newton",
                "color": [255, 140, 0],
                "meta": {"hasHands": True, "hasLeftHand": True, "hasRightHand": True,
                         "hasBody": False, "hasFace": False},
                "dimensions": {"totalHeight": 1.75, "hipHeight": 0.94},
                "joints": left + right,
            }],
            "props": [],
            "characters": [],
        },
    }


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--host", default="127.0.0.1")
    ap.add_argument("--ports", type=int, nargs="+", default=[14043, 14045])
    ap.add_argument("--fps", type=float, default=60)
    ap.add_argument("--pose", choices=list(POSES))
    ap.add_argument("--lz4", action="store_true")
    ap.add_argument("--seconds", type=float, default=0, help="stop after N seconds (0 = forever)")
    args = ap.parse_args()

    if args.lz4:
        import lz4.frame

    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    sock.setsockopt(socket.SOL_SOCKET, socket.SO_SNDBUF, 65535)  # macOS caps datagrams at 9216 bytes otherwise
    start = time.time()
    print(f"Mock gloves → {args.host}:{args.ports} at {args.fps} fps")
    while True:
        t = time.time() - start
        if args.seconds and t > args.seconds:
            break
        payload = json.dumps(make_frame(t, args.pose)).encode()
        if args.lz4:
            payload = lz4.frame.compress(payload)
        for port in args.ports:
            sock.sendto(payload, (args.host, port))
        time.sleep(max(0.0, 1 / args.fps - (time.time() - start - t)))


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        pass
