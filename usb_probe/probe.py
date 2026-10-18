"""Passively read raw bytes from one glove USB serial port. Usage: probe.py <port> [seconds]"""
import binascii, os, signal, sys, time
port = sys.argv[1]; secs = float(sys.argv[2]) if len(sys.argv) > 2 else 3
signal.signal(signal.SIGALRM, lambda *_: (print(f"{port}: timed out (open/read blocked)", flush=True), os._exit(2)))
signal.alarm(int(secs) + 6)
print(f"{port}: opening…", flush=True)
fd = os.open(port, os.O_RDWR | os.O_NOCTTY | os.O_NONBLOCK)
print(f"{port}: open", flush=True)
buf = b""; t = time.time()
while time.time() - t < secs:
    try:
        chunk = os.read(fd, 4096)
        if chunk: buf += chunk
    except BlockingIOError:
        time.sleep(0.01)
os.close(fd)
name = port.split("/")[-1]
open(os.path.join(os.path.dirname(__file__), name + ".bin"), "wb").write(buf)
print(f"{port}: {len(buf)} bytes in {secs:.0f} s", flush=True)
print("  hex:", binascii.hexlify(buf[:96]).decode(), flush=True)
print("  txt:", "".join(chr(b) if 32 <= b < 127 else "." for b in buf[:160]), flush=True)
