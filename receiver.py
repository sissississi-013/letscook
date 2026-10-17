"""Listen for Rokoko Studio Custom Streaming (UDP) and log every frame to disk.

Usage: python3 receiver.py [port]   (default 14043, must match Studio's receiver port)
Frames are appended to sessions/<timestamp>.jsonl, one line per packet:
  {"t": unix time, "t_mono_ns": monotonic receive time, "frame": {...}}  or  {"raw_b64": ...} if undecodable
"""
import base64
import json
import socket
import sys
import time
from pathlib import Path

try:
    import lz4.frame  # Studio's optional compression is LZ4 *frame* format; pip install lz4
except ImportError:
    lz4 = None

DEFAULT_PORT = 14043
LZ4_MAGIC = b"\x04\x22\x4d\x18"


def decode(data: bytes):
    if data[:4] == LZ4_MAGIC:
        if not lz4:
            return None
        try:
            data = lz4.frame.decompress(data)
        except RuntimeError:
            return None
    try:
        return json.loads(data.decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError):
        return None


def detect_format(frame) -> str:
    """'v4' if scene.newtons is filled, 'v3' if scene.actors is filled, else 'unknown'."""
    scene = frame.get("scene", {}) if isinstance(frame, dict) else {}
    if scene.get("newtons"):
        return "v4"
    if scene.get("actors"):
        return "v3"
    return "unknown"


def main():
    port = int(sys.argv[1]) if len(sys.argv) > 1 else DEFAULT_PORT
    out_dir = Path(__file__).parent / "sessions"
    out_dir.mkdir(exist_ok=True)
    out_path = out_dir / f"{time.strftime('%Y%m%d-%H%M%S')}.jsonl"

    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    sock.setsockopt(socket.SOL_SOCKET, socket.SO_RCVBUF, 1 << 20)
    sock.bind(("0.0.0.0", port))
    print(f"Listening on UDP {port}, logging to {out_path}", flush=True)

    frames, undecoded, last_report, first = 0, 0, time.time(), True
    with out_path.open("a") as f:
        while True:
            data, addr = sock.recvfrom(65535)
            frame = decode(data)
            record = {"t": time.time(), "t_mono_ns": time.monotonic_ns()}
            if frame is None:
                undecoded += 1
                record["raw_b64"] = base64.b64encode(data).decode()
            else:
                record["frame"] = frame
                if first:
                    first = False
                    fmt = detect_format(frame)
                    who = frame["scene"]["newtons"][0].get("name") if fmt == "v4" else ""
                    print(f"First frame from {addr[0]}: format {fmt} {who}", flush=True)
            f.write(json.dumps(record) + "\n")
            frames += 1

            now = time.time()
            if now - last_report >= 5:
                print(f"{frames / (now - last_report):.0f} fps, undecoded: {undecoded}", flush=True)
                frames, undecoded, last_report = 0, 0, now
                f.flush()


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        print("\nStopped.")
