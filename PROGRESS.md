# Progress

Ralph loop started 2026-10-17 14:45 PDT, deadline 18:30 PDT. Brief: [RALPH.md](RALPH.md).

## Milestones
- [x] M1 Mock gloves
- [x] M2 Unity project + glove receiver
- [x] M3 Kitchen
- [x] M4 Grab anything
- [x] M5 Cooking state
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

### 14:59 M2 Unity project + glove receiver ✅
- `LetsCook/` Unity 6 project (6000.2.9f1, built-in render pipeline, legacy Input Manager), + test-framework, ugui.
- `GloveFrame.cs` (JsonUtility v4 parser, joints by name with parent names, `HandMath.Curl`), `RokokoReceiver.cs` (UDP thread on 14045, falls back to a built-in mock animation when silent for 2 s, warns on LZ4), `GloveHand.cs` (spheres + capsules from joint positions, wrist pinned to an anchor).
- `SceneBuilder.cs` builds `Assets/Scenes/Kitchen.unity` from code; `SceneBuilder.Shot` renders screenshots in batch mode.
- `./unity.sh build | shot <pose> <name> | test` wraps batch mode (refuses if the project is open in the Editor).
- Evidence: EditMode 4/4 passed (parse, v3 rejection, curl per pose, lerp); PlayMode 1/1 passed (real UDP packet → receiver goes live, 50 joints, fist curl > 0.9). Screenshot `shots/m2-pinch.png` shows both hands.

### 15:01 M3 Kitchen ✅
- `Kitchen.cs` builds the counter (top at y = 0.9 m), backsplash, burner, cutting board, pan (base + 16-segment wall ring + handle), bowl, plate, knife (blade has `KnifeEdge`), spatula; ingredients tomato, onion, egg, butter, bread with `Food` + Rigidbody. Every object has a `KitchenItem` name for logging.
- Containers are rings of convex boxes so food can sit inside (convex collider limit).
- `GameHud` shows glove status + controls in play mode. Materials saved to `Assets/LetsCook/Materials`.
- Evidence: `shots/m3-kitchen.png`.

### 15:04 M4 Grab anything ✅
- `HandGrasp.cs`: kinematic fingertip + palm spheres follow the glove; grab = thumb tip + an opposing fingertip within 1.6 cm of the same non-kinematic `KitchenItem` and grip curl > 0.3; attach with a breakable `FixedJoint` (250 N) to a kinematic grip body that follows the fingers and wrist rotation; release when curl < 0.2 or the joint breaks. Collisions between hand and held object are ignored while held. `Held`/`Assisted` exposed for logging.
- **Claude's call:** grab/release curl thresholds are 0.3/0.2, not the 0.8/0.7 in the research, because a real pinch only curls the index about 0.6.
- `WristController.cs`: WASD/QE move wrists, Z/C turn, Tab switch hand, Space both.
- Fixed the mock thumb (was on the outer side and never met the index); pinch gap now under 1.5 cm on both hands (pytest 10/10).
- Evidence: PlayMode `PinchGrabsLiftsAndReleases` passes (open → no grab; pinch → grabs cube; hand rises 12 cm → cube rises > 8 cm; open → released and falls). Unity tests: EditMode 4/4, PlayMode 2/2.

### 15:16 M5 Cooking state ✅
- `FoodState.cs` (pure C#): surface + core temperature, surface water that pins the surface at 100 °C until it boils off (no browning on wet food), Maillard browning above 140 °C, char above 180 °C, protein set (egg 62 °C, onion softens 85 °C), butter melt (32 °C). Colour and a plain-language `Doneness` ("fully set, lightly browned") come from the state. Per-ingredient profiles for tomato, onion, egg, butter, bread.
- `Food.cs` runs the state, heats from touching `Cookware`, recolours, flattens melting butter, `Describe()` for the dish summary. `Food.TimeScale` (1 = real time).
- `Cookware.cs`: pan preheats toward the burner's target (260 °C at full power, about a 1–2 min preheat) and cools in air. `Burner`: B toggles, +/- heat level, ring glows.
- Bug found and fixed: sleeping rigidbodies stop reporting contacts, so food silently stopped cooking. Food stays awake.
- Evidence: EditMode 11/11 (raw stays raw; tomato pinned at 100 °C; bread golden at about 2.5 min, not at 30 s; burns at 260 °C for 10 min; egg sets at 160 °C; butter melts; colour darkens). PlayMode 3/3, including kitchen integration (pan > 150 °C, bread browns, butter melts, tomato on the board stays raw). `shots/m5-cooked.png`.
