#!/bin/bash
# Start the helpers for a cooking session:
#   - glove logger (raw Rokoko stream on UDP 14043 → sessions/*.jsonl)
#   - chef server (Claude dish names + tools on http://127.0.0.1:8776)
#   - optionally fake gloves:  ./start.sh --mock
# Then open the Unity project and press Play (see HOW-TO-RUN.md). Ctrl+C stops everything.
set -euo pipefail
cd "$(dirname "$0")"
PY=.venv/bin/python
[ -x "$PY" ] || { echo "Missing .venv. Run: python3 -m venv .venv && .venv/bin/pip install lz4 pytest numpy pyarrow anthropic" >&2; exit 1; }
[ -n "${ANTHROPIC_API_KEY:-}" ] || echo "⚠️  ANTHROPIC_API_KEY not set: dishes and tools will get offline placeholder names."

pids=()
cleanup() { kill "${pids[@]}" 2>/dev/null || true; }
trap cleanup EXIT INT TERM

if lsof -nP -iUDP:14043 >/dev/null 2>&1; then
  echo "• glove logger already running on UDP 14043"
else
  "$PY" -u receiver.py 14043 & pids+=($!)
fi

if lsof -nP -iTCP:8776 -sTCP:LISTEN >/dev/null 2>&1; then
  echo "• chef server already running on :8776"
else
  "$PY" -u chef_server.py & pids+=($!)
fi

if [ "${1:-}" = "--mock" ]; then
  "$PY" mock_glove.py & pids+=($!)
fi

echo "Ready. Open LetsCook/ in Unity (6000.2.9f1), open Assets/Scenes/Kitchen.unity, press Play."
wait
