"""Listen for Rokoko Studio Custom Streaming (UDP) and log every frame to disk.

Usage: python3 receiver.py [port]   (default 14043, must match Studio's receiver port)
Frames are appended to sessions/<timestamp>.jsonl, one line per packet.
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

PORT = int(sys.argv[1]) if len(sys.argv) > 1 else 14043


def decode(data: bytes):
    try:
        return json.loads(data.decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError):
        pass
    if lz4:
        try:
            return json.loads(lz4.frame.decompress(data))
        except Exception:
            pass
    return None


def main():
    out_dir = Path(__file__).parent / "sessions"
    out_dir.mkdir(exist_ok=True)
    out_path = out_dir / f"{time.strftime('%Y%m%d-%H%M%S')}.jsonl"

    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    sock.bind(("0.0.0.0", PORT))
    print(f"Listening on UDP {PORT}, logging to {out_path}")

    frames, undecoded, last_report = 0, 0, time.time()
    with out_path.open("a") as f:
        while True:
            data, _ = sock.recvfrom(65535)
            frame = decode(data)
            record = {"t": time.time()}
            if frame is None:
                undecoded += 1
                record["raw_b64"] = base64.b64encode(data).decode()
            else:
                record["frame"] = frame
                if frames == 0:
                    print("First frame keys:", list(frame)[:10] if isinstance(frame, dict) else type(frame))
            f.write(json.dumps(record) + "\n")
            frames += 1

            now = time.time()
            if now - last_report >= 2:
                print(f"{frames / (now - last_report):.0f} fps, undecoded: {undecoded}")
                frames, undecoded, last_report = 0, 0, now


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        print("\nStopped.")
