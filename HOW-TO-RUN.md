# How to run let's cook!

## 1. Start the helpers
```bash
cd ~/"let's cook!"
./start.sh          # glove logger + chef server
./start.sh --mock   # same, plus fake gloves (no hardware needed)
```
- **Glove logger** saves the raw Rokoko stream from UDP 14043 to `sessions/<time>.jsonl`.
- **Chef server** (`http://127.0.0.1:8776`) names dishes and designs tools with Claude. It needs a valid `ANTHROPIC_API_KEY`; without one you get offline placeholder names (the game still works).

## 2. Stream the real gloves (Rokoko Studio)
Full checklist: [glove-setup.md](glove-setup.md). In short:
1. Pair both gloves, put them on, **calibrate** (Ctrl+Shift+B).
2. Livestreaming → **Custom Streaming** → add two receivers, both `127.0.0.1`, UDP, **JSON v4**, no LZ4:
   - port **14043** (logger)
   - port **14045** (Unity)
3. Enable both and click **Activate** (needs the Plus/Pro plan or trial).

**Check the gloves before playing:** `.venv/bin/python glove_check.py` shows a live bar per finger (0 = straight, 1 = fist). Make a fist, open your hand, point: the bars should follow.

If no glove stream arrives, you play with the mouse: hold the left button to pinch with the right hand, the right button for the left hand.

## 2a. No gloves? Use your webcam as the gloves
```bash
.venv/bin/python hand_cam.py
```
MediaPipe tracks all 21 points of both hands and sends them in the gloves' own format (to the game, the logger and the wrist anchors), so everything works as if you were wearing Smartgloves. Hold your hands up to the webcam, **palms facing the screen, fingers up**: in the game that's palms down over the counter. Move your hands around the image to move them around the kitchen; **pinch thumb and index** to grab. macOS will ask to allow camera access for Terminal.

## 2b. (Optional) Move your hands with the webcam
```bash
.venv/bin/python wrist_cam.py
```
- macOS will ask to let Terminal use the camera: allow it.
- Face the webcam with your hands resting where the counter is, and hold still for a second (calibration). After that, moving your arms moves the hands in the kitchen. **C** recalibrates, **Q** quits.
- It tracks your body's wrists (MediaPipe pose), which should work with gloves on (untested so far). If movements feel too small, try `--scale 1.6`.
- While the camera is sending, the keyboard movement keys pause; the HUD says "wrists: webcam".

## 3. Play
1. Open Unity Hub → **Add** → `~/let's cook!/LetsCook` → open with **6000.2.9f1**.
2. Open `Assets/Scenes/Kitchen.unity` and press **Play**.
3. The game first asks whether to record: **Y** saves hand motion + videos for robot research, **N** just plays. (The separate `receiver.py` glove logger records raw glove data whenever it runs, so stop it for players who said no.)

| Key | What it does |
|---|---|
| Left / right mouse button (no gloves) | hold to close the right / left hand (pinch-grab); release to let go |
| W A S D | move hands across the counter (when the wrist cam isn't running) |
| Q / E | hands down / up |
| Z / C | turn hands |
| Tab / Space | move one hand / both hands |
| B, + / − | burner on/off, heat level |
| T | ask for **any tool**: type it (or press **Fn twice** for macOS dictation and say it), Enter |
| Enter | **serve** what's on the plate → Claude names it and unlocks a recipe |
| Esc | close the dish card |
| R | fresh kitchen (new ingredients, tools back in place); also ends the robot-data episode |
| K | recalibrate the gloves through Rokoko Studio (turn on Studio → Settings → Command API; hold the calibration pose for the 3 s countdown) |

Your **cookbook**: every served dish is saved with a photo. Open it with `open cookbook/index.html`.

How to cook:
- **Grab:** pinch the thumb and a finger around something (or close your hand on it). Open your fingers to let go.
- **Cut:** chop food fast with the knife (or any tool with a blade).
- **Cook:** put food in the pan on the lit burner. The pan takes about a minute to heat up, like real cast iron. Food dries, browns, sets, melts and burns based on temperature.
- **Oil the pan:** tip the oil bottle (back right) past sideways over the pan. Oiled pans brown food faster and more evenly; above about 230 °C the oil smokes, so turn the heat down with −.
- **Season:** pick up the salt or pepper shaker (back right) and turn it upside down over food; shake it for more.
- **Crack an egg:** knock it on the pan rim or counter (or hit it with the knife). It spills into whatever is below and fries.
- **Serve:** move food onto the white plate and press Enter.

## 4. Robot data
Every Play session writes `sessions/unity-<time>.jsonl` (30 fps: hands, objects, food state, events). Convert it to a LeRobot-style dataset:
```bash
.venv/bin/python tools/to_lerobot.py sessions/unity-XXXX.jsonl datasets/letscook-001
```
Episodes are split each time you serve a plate or reset the kitchen. Frames where the game held an object for you are flagged (`quality.assist_active`), and so are frames driven by mock hands (`quality.glove_live = false`).

Retarget the hands onto a robot hand (LEAP by default; also allegro, shadow, inspire, ability, svh):
```bash
uv venv -p 3.12 .venv-robot && VIRTUAL_ENV=.venv-robot uv pip install dex_retargeting torch numpy pyarrow
git clone --depth 1 --filter=blob:none --sparse https://github.com/dexsuite/dex-urdf models/dex-urdf && git -C models/dex-urdf sparse-checkout set robots/hands/leap_hand
.venv-robot/bin/python tools/retarget.py datasets/letscook-001
```
This adds `action.retargeted.leap_left` / `leap_right` (16 joint angles each) to every episode.

## Developer commands
```bash
./unity.sh build                    # rebuild the Kitchen scene from code
./unity.sh test                     # Unity EditMode + PlayMode tests
./unity.sh shot pinch name [yes]    # screenshot to shots/name.png (yes = cooked food)
.venv/bin/python -m pytest tests    # Python tests
```
`unity.sh` needs the project closed in the Unity Editor (batch mode can't open a project that's already open).

## Troubleshooting
- **Real hands point the wrong way** (toward you, sideways or upside down): Studio's "forward" is the direction you faced when calibrating. Face the screen and recalibrate (K or Ctrl+Shift+B in Studio), or hold **Z / C** to turn the hands until fingers point into the kitchen.
- **Fingers look bent when your hand is flat:** recalibrate; the gloves drift over time.
- **Status says "mock hands" while Studio streams:** check that the port 14045 receiver is enabled, set to JSON v4 without LZ4, and that macOS allowed Studio on the local network.
- **Logger shows nothing:** macOS limits UDP packets to 9 KB by default and full-body frames can be bigger. Try the "JSON v4 (LZ4)" format for the 14043 receiver only (the logger can decode it; Unity can't).
- **Dish names say "offline":** the chef server isn't running, or the API key is invalid (the key in `~/.zshrc` returned 401 on 2026-10-17).
- **Port 8765 is busy:** another app uses it, which is why the chef server uses 8776.
