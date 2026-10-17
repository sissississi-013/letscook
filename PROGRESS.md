# Progress

Ralph loop started 2026-10-17 14:45 PDT, deadline 18:30 PDT. Brief: [RALPH.md](RALPH.md).

## Milestones
- [x] M1 Mock gloves
- [x] M2 Unity project + glove receiver
- [x] M3 Kitchen
- [x] M4 Grab anything
- [x] M5 Cooking state
- [x] M6 Cutting
- [x] M7 Session logging for robots
- [ ] M8 Plate it → Claude names it
- [ ] M9 Ask for any tool
- [x] M10 Polish + README

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

### 15:08 M5 Cooking state ✅
- `FoodState.cs` (pure C#): surface + core temperature, surface water that pins the surface at 100 °C until it boils off (no browning on wet food), Maillard browning above 140 °C, char above 180 °C, protein set (egg 62 °C, onion softens 85 °C), butter melt (32 °C). Colour and a plain-language `Doneness` ("fully set, lightly browned") come from the state. Per-ingredient profiles for tomato, onion, egg, butter, bread.
- `Food.cs` runs the state, heats from touching `Cookware`, recolours, flattens melting butter, `Describe()` for the dish summary. `Food.TimeScale` (1 = real time).
- `Cookware.cs`: pan preheats toward the burner's target (260 °C at full power, about a 1–2 min preheat) and cools in air. `Burner`: B toggles, +/- heat level, ring glows.
- Bug found and fixed: sleeping rigidbodies stop reporting contacts, so food silently stopped cooking. Food stays awake.
- Evidence: EditMode 11/11 (raw stays raw; tomato pinned at 100 °C; bread golden at about 2.5 min, not at 30 s; burns at 260 °C for 10 min; egg sets at 160 °C; butter melts; colour darkens). PlayMode 3/3, including kitchen integration (pan > 150 °C, bread browns, butter melts, tomato on the board stays raw). `shots/m5-cooked.png`.

### 15:09 M6 Cutting ✅
- `KnifeEdge.cs`: a blade hit faster than 0.3 m/s splits food along the food axis that best matches the blade normal. Each half gets half the mass, its own copy of the cooking state, and the next cut name (whole → halves → quarters → chunks → small pieces → minced). Stops below 8 mm thickness or at 40 pieces; 0.25 s cooldown.
- `CookEvents.cs`: ordered event history (grab, release, cut) for the robot logger and the dish summary. HandGrasp now emits grab/release.
- Evidence: PlayMode 5/5, including `FastChopSplitsFoodInHalf` (1.2 m/s → 2 pieces "halves", 0.06 kg each, split across x, separate states, cut event logged) and `GentleTouchDoesNotCut` (0.1 m/s → still 1 piece). EditMode 11/11.

### 15:12 M7 Session logging for robots ✅
- `SessionLogger.cs` (in the scene) writes `sessions/unity-<time>.jsonl` at 30 fps: meta line (keypoint order, frame convention), per-frame wrists + rotation, 21 MediaPipe-order keypoints per hand, 19 raw glove joint rotations, curls, held object id, assisted flag, live-vs-mock flag, every object's pose + food state (cut, doneness, temps, browning, char, set, melted), pan temperature; event lines (grab, release, cut, ...).
- `tools/to_lerobot.py`: Unity session → LeRobot v2.1-layout dataset (parquet per episode + meta/info.json, episodes.jsonl, tasks.jsonl). Converts to right-handed Z-up (tested: rotations stay proper, up stays up). `observation.state` (140) = per hand wrist pos + quat + 21 wrist-frame keypoints; `action` = next frame; plus world keypoints, object poses, held object, `quality.assist_active`, `quality.tracking_ok`, `quality.glove_live`, food-state labels. Episodes split at `plate` events. LeRobot's own v2.1→v3 script can upgrade it.
- **Claude's call:** v2.1 layout instead of writing v3 directly (simpler, and LeRobot ships the upgrade script). Raw rotations are Rokoko world rotations, not local.
- Evidence: PlayMode 6/6 (logger writes about 30 fps, objects, doneness, cut event); pytest 15/15 (basis change, quaternion round trip, fixture conversion, untracked frames are NaN, plate splits episodes). Fixture: `tests/fixtures/sample-session.jsonl` (mock hands).
- Not yet: rendered video, fingertip contact ids, MANO fitting, retargeting.

### 15:21 M10 Polish + README ✅
- `HOW-TO-RUN.md` (helpers, Studio receivers, controls, how to cook, robot data export, dev commands, troubleshooting), `start.sh` (logger + chef server, `--mock`; skips anything already running; Ctrl+C stops what it started), README updated.
- Evidence: `./start.sh --mock` started the chef server (health OK, /tool returned the library whisk) and fake gloves, detected the logger already running, and cleaned up on stop.
- Note: that test briefly sent mock frames into the real logger's current session file (`sessions/20261017-145204.jsonl`); they're marked "Mock Newton".

### 15:34 Real app run + bugs it found ✅
- `./unity.sh app` builds `build/LetsCook.app`. `AutoPilot.cs`: `LetsCook.app/Contents/MacOS/LetsCook -autoshot <dir> -autoquit <s> [-autocook]` screenshots every 2 s; `-autocook` puts bread + butter in the pan at 30× cooking speed, plates them and serves.
- Evidence (`shots/auto/`): with `mock_glove.py` streaming to 14045 and the chef server up, the app went **LIVE** (708 packets), cooked the toast (burnt black at 30× for about 5 min of cooking time), plated it, the chef server answered (offline name because of the 401 key), and the dish card showed. Session saved to `sessions/unity-*.jsonl`.
- **Bugs found only by running the real app (all fixed):**
  1. `KitchenItem`, `Burner`, `ToolInfo` lived in files with other names, so Unity dropped them from the saved scene ("missing script") and the built app crashed ("level0 is corrupted"). The Editor's Play button would have hit the same problem. Each now has its own file; new EditMode test `EveryComponentHasItsOwnScriptFile`.
  2. **M8–M10 scene rebuilds had silently failed** (the plate was looked up before the kitchen existed), so the committed scene was stale since M7. Fixed the build order; the scene now has Plating, ToolRequester, AutoPilot.
  3. The idle mock animation (no gloves) knocked the kitchen over. Mock hands are now ghosts: no colliders and no grabbing unless a real/streamed glove frame drives them (`RokokoReceiver.DrivesPhysics`). **Claude's call.**
  4. Fingers appearing inside food launched it. `maxDepenetrationVelocity = 0.4 m/s` on kitchen bodies.
  5. A held object followed the fingertip average, so re-curling yanked it (the pan ended up on the floor). It's now locked to the wrist where it was grabbed.
  6. The built app wrote sessions inside `build/`. It now finds the repo folder (the one with chef_server.py).
  7. Emoji rendered as boxes in the game font; removed.
- Tests: EditMode 15/15, PlayMode 7/7, pytest 26/26.

### 15:39 Egg cracking ✅
- `EggCracker.cs`: a whole egg cracks on a hard impact (≥ 0.7 m/s; fingers and other food don't count) or when a knife hits it. The shell splits into two bits and the white + yolk spill onto the first solid surface below (pan, bowl, board, counter) as a new `egg (cracked)` food with its own profile that fries: white goes from glossy translucent to opaque, "fully set" at 62 °C+. Emits a `crack` event ("into the pan").
- Glove checker: `glove_check.py` (live per-finger curl bars from the logger) added earlier.
- Evidence: PlayMode 9/9 incl. `GentleSetDownDoesNotCrack` and `DroppedIntoThePanItCracksAndFries` (crack event says "into the pan"; fully set after about 200 s of cooking time). EditMode 15/15.

### 16:00 Webcam wrist tracking ✅ (needs a real camera test)
- `wrist_cam.py`: MediaPipe **pose** landmarker (body wrists, so gloves shouldn't matter; unconfirmed) on the webcam, auto-calibrates the resting pose to the game's rest anchors, maps camera motion to game coordinates (x/z flipped because the camera faces the player, y up), sends UDP to Unity 14046. Preview window, C recalibrate, Q quit. Downloads `models/pose_landmarker_lite.task` (5.6 MB, git-ignored).
- Unity `WristCamInput.cs`: glides the wrist anchors to the camera positions (smoothing 14/s, clamped to the kitchen), pauses keyboard movement while fresh, hands back to the keyboard after 0.5 s without packets. HUD shows "wrists: webcam/keyboard".
- **Claude's call:** pose wrists instead of hand landmarks (hand tracking struggles with gloves); default scale 1.2.
- Fixed two flaky tests (plate test moved an interpolated body via transform; wrist test counted frames instead of time).
- Evidence: pytest 31/31 (mapping directions, scale, calibration, packet). PlayMode 10/10 incl. `CameraPacketsGlideTheWristsAndPauseKeyboard`. MediaPipe 1.1.1 runs here (10 frames in 0.1 s on a blank image). **Not yet tested with a real camera**: needs the user to allow camera access.

### 16:02 Contacts for robot data + personal cookbook ✅
- `HandGrasp.Contacts`: the object each fingertip and the palm is touching every physics step. Logged per frame as `contacts` (object ids), exported as `observation.contacts` (2×6 int16). Fixture regenerated.
- `chef_server.py` saves each plate's screenshot to `cookbook/photos/` and rebuilds `cookbook/index.html` (one card per dish, newest first: photo, number, course, time, name, description, chef note, ingredients with doneness, what it unlocked; light/dark theme; names HTML-escaped). `cookbook/` is git-ignored (personal).
- Evidence: pytest 32/32 (incl. photo saved as PNG + script-injection escape), Unity 25/25. Rendered sample page checked visually.

### 16:04 Salt & pepper ✅
- `Shaker.cs` + two shakers on the counter. Upside down (tilt > 0.3) pours 0.15 g/s, shaking (velocity changes) adds up to 1.2 g/s; grains are particles; seasoning lands on the first food straight below the cap. `FoodState.salt/pepper` (g) + `Seasoning` text ("lightly salted, peppered"). Included in the dish summary (salt_g, pepper_g, seasoning), the robot log and `season` events.
- Evidence: PlayMode 12/12 incl. `UprightShakerDispensesNothing`, `ShakingUpsideDownOverBreadSaltsIt`. `shots/m11-kitchen.png`.

### 16:06 Recalibrate gloves from the game ✅
- `StudioCommands.cs`: **K** → Rokoko Studio Command API `calibrate` (gloves only, 3 s countdown, pose straight-arms-down) at `127.0.0.1:14053/v1/1234`; toasts Studio's error text (e.g. plan not supported) or "turn on Command API" if unreachable; emits a `calibrate` event for the robot log.
- Studio's Command API is listening on this Mac (14053); a read-only `info` call returned `null` (likely no scene open yet).
- Evidence: PlayMode 13/13 incl. `UnreachableStudioFailsGracefully`.
