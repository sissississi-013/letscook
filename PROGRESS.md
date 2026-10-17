# Progress

Ralph loop started 2026-10-17 14:45 PDT, deadline 18:30 PDT. Brief: [RALPH.md](RALPH.md).

## Milestones
- [x] M1 Mock gloves
- [ ] M2 Unity project + glove receiver
- [ ] M3 Kitchen
- [ ] M4 Grab anything
- [ ] M5 Cooking state
- [ ] M6 Cutting
- [ ] M7 Session logging for robots
- [ ] M8 Plate it → Claude names it
- [ ] M9 Ask for any tool
- [ ] M10 Polish + README

## Decisions (Claude's calls, not yet confirmed by the user)
- Unity 6 (6000.2.9f1) with a custom C# receiver instead of the Rokoko plugin
- Hands drawn directly from world-space joint positions; wrists anchored above the counter, moved by keyboard until wrist tracking exists
- Two Studio receivers: 14043 (Python logger) and 14045 (Unity)

## Log

### 14:53 M1 Mock gloves ✅
- `mock_glove.py` emits JSON v4 frames (50 hand joints, palm-down hands, pose cycle open→fist→pinch→point) at about 50–60 fps to 14043 + 14045; `--pose`, `--lz4`, `--ports`, `--seconds`.
- `receiver.py` now detects the LZ4 frame magic, reports v3/v4, logs host monotonic time, raises SO_RCVBUF.
- Evidence: `pytest tests` → 9 passed. End-to-end: mock → receiver on port 15043 logged 394 frames in 8 s, 0 undecodable (plain + LZ4).
- Gotcha found: **macOS caps UDP datagrams at 9216 bytes by default**; a 10 KB frame failed with `Errno 40 Message too long` until SO_SNDBUF was raised. Real Studio frames may hit the same limit; if the logger sees nothing from Studio, try "JSON v4 (LZ4)".
- Python deps live in `.venv/` (lz4, pytest, numpy, pyarrow, anthropic).
- Real-glove logger is running on 14043 waiting for Studio.
