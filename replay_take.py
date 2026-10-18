"""Replay a recorded glove session into the game as if the gloves were live.

Usage: .venv/bin/python replay_take.py sessions/take-<name>.jsonl [--loop] [--ports 14045]
Works with takes imported from Rokoko Studio FBX exports (Unity menu: Let's Cook → Import Rokoko FBX Take) and with
raw sessions logged by receiver.py. Frames are sent at their recorded timing.
"""
import argparse
import json
import socket
import time


def load(path):
    frames = []
    with open(path) as f:
        for line in f:
            rec = json.loads(line)
            if "frame" in rec:
                frames.append((rec["t"], json.dumps(rec["frame"]).encode()))
    if not frames:
        raise SystemExit(f"{path}: no frames")
    t0 = frames[0][0]
    return [(t - t0, payload) for t, payload in frames]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("session")
    ap.add_argument("--ports", type=int, nargs="+", default=[14045])
    ap.add_argument("--loop", action="store_true")
    args = ap.parse_args()
    frames = load(args.session)
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    sock.setsockopt(socket.SOL_SOCKET, socket.SO_SNDBUF, 65535)
    print(f"replaying {len(frames)} frames ({frames[-1][0]:.1f} s) → {args.ports}", flush=True)
    while True:
        start = time.monotonic()
        for t, payload in frames:
            delay = t - (time.monotonic() - start)
            if delay > 0:
                time.sleep(delay)
            for port in args.ports:
                sock.sendto(payload, ("127.0.0.1", port))
        if not args.loop:
            break


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        pass
