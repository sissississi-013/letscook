# Ralph loop brief: let's cook!

You are building "let's cook!" autonomously. The user is setting up Rokoko Smartgloves in parallel and may not answer questions. Work in `/Users/sissi/let's cook!`.

## Every iteration
1. Run `date`. **If it is 18:30 PDT (6:30 pm) on 2026-10-17 or later:** stop building, make sure everything is committed, write a final "Handoff" section at the top of `PROGRESS.md` (what works, how to run it, what's next), then output `<promise>KITCHEN CLOSED</promise>` and nothing else.
2. Read `PROGRESS.md` (create it if missing) and `BUILD-PLAN.md`. Pick the **first unchecked milestone** below.
3. Build it, **verify it actually works** (run tests, compile Unity in batchmode, capture a batchmode screenshot and look at it with Read). Never claim something works without evidence.
4. Update `PROGRESS.md`: tick the milestone, note evidence (test output, screenshot path), problems, and decisions you made yourself (marked "Claude's call").
5. `git add -A && git commit` with a clear message ending in `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Hard rules
- **Never buy anything, start a Rokoko trial, enter payment info, or sign in to accounts.** Never send email/messages. Never publish data.
- Don't touch other folders in /Users/sissi except to read.
- Don't kill Rokoko Studio or Unity processes you didn't start. If the Unity project is open in the Editor (lockfile `LetsCook/Temp/UnityLockfile` in use), skip batchmode steps and work on Python/C# source only.
- Keep `receiver.py` working; Studio may be streaming real gloves into port 14043.
- Unity editor: `/Applications/Unity/Hub/Editor/6000.2.9f1/Unity.app/Contents/MacOS/Unity`. Use `-batchmode -quit -projectPath LetsCook -logFile <file>`; read the log for errors. Build scenes from code (a bootstrap script), not by hand-editing .unity YAML.
- ANTHROPIC_API_KEY is in the environment (from ~/.zshrc). Never print or commit it. Read it from the environment at runtime only.
- If a milestone is blocked for 2 iterations, write why in PROGRESS.md and move on.
- Keep it small and working over big and broken. Commit working states.

## Ports
- Studio (or `mock_glove.py`) sends JSON v4 to **14043** (Python logger) and **14045** (Unity). In Studio, the user adds two Custom Streaming receivers.
- JSON v4 format: research/rokoko-streaming.md §2.4. World-space joints; left-handed, Y-up, metres, quaternions x,y,z,w. Finger names `{Left|Right}Finger{1..5}{Metacarpal,Proximal,Medial,Distal,Tip}`, thumb (Finger1) has no Medial. Hand joints `LeftHand` / `RightHand`.

## Milestones (in order)
- [ ] **M1 Mock gloves.** `mock_glove.py`: emits realistic JSON v4 frames at 60 fps to 14043 and 14045 with animated hands (open → fist → pinch → point cycle; keys or `--pose` flag optional). `tests/test_receiver.py`: decode plain + LZ4-frame payloads, v3 vs v4 detection. Run receiver + mock together and confirm a session file fills up.
- [ ] **M2 Unity project + glove receiver.** Create `LetsCook/` (Unity 6 / 6000.2.9f1, URP or built-in, whichever creates cleanly in batchmode). `RokokoReceiver.cs` (UDP thread on 14045, parses v4 `newtons[0].joints`, thread-safe latest frame). `GloveHand.cs`: draws each hand as spheres + capsules from joint positions, re-anchored so the wrist sits at a configurable anchor above the counter (gloves give no reliable wrist position). EditMode test for the parser. Bootstrap scene builder via `-executeMethod`. Screenshot proves hands render from mock data.
- [ ] **M3 Kitchen.** Procedural counter, cutting board, burner + pan, bowl, plate, knife, spatula; ingredients tomato, onion, egg, butter, bread (primitives with nice materials are fine). Good camera framing and lighting. Screenshot.
- [ ] **M4 Grab anything.** Kinematic finger colliders; grasp = thumb tip + one opposing fingertip touching the same object with curl hysteresis (grab 0.8 / release 0.7) → attach joint; release drops it. Wrist anchors movable by keyboard (WASD/QE/arrows) for now. Log `assisted_grasp` flags. Play-mode test with scripted mock poses.
- [ ] **M5 Cooking state.** `Food.cs`: surface/core temperature, moisture, protein set, browning, char; burner heats pan, pan heats contacting food; color/material changes with doneness. Egg white sets at about 60–63 °C, browning from about 140 °C, char above about 180 °C. Unit tests for the state model.
- [ ] **M6 Cutting.** Knife edge contact with enough speed splits an ingredient into two pieces (simple primitive split is fine; EzySlice optional). Piece cap.
- [ ] **M7 Session logging for robots.** Unity writes `sessions/unity-<time>.jsonl`: per frame hand joints, wrist anchors, held object, assisted flag, object states; plus an events log (grab, release, cut, heat, plate). `tools/to_lerobot.py` converts a session to a LeRobot-style folder (parquet + meta json) with 21 keypoints + wrist per hand at 30 fps. Test on a mock session.
- [ ] **M8 Plate it → Claude names it.** Serve gesture or key (Enter) → summarize what's on the plate (ingredients, cuts, cook states, action history) → Claude API (`claude-sonnet-5-5`, structured JSON: dish_name, course, description, unlock{recipe, course}) via a small Python helper or C# UnityWebRequest → show on screen + append to `cookbook.json`. Stub when offline. Test with a canned session.
- [ ] **M9 Ask for any tool.** Text box (voice later) → Claude (`claude-haiku-5-5`) returns a tool spec from parts (handle, blade, bowl, tines, head; dimensions, material, abilities from a fixed list) → Unity builds it procedurally and drops it on the counter. Library of 10 prebuilt tools checked first.
- [ ] **M10 Polish + README.** `HOW-TO-RUN.md`: start Studio receivers, run logger, open Unity scene, keys, troubleshooting. Update README status.

If all milestones are done before 18:30, improve realism/fun (liquids, egg cracking, the host's voice lines, webcam wrist tracking via MediaPipe) in small committed steps.
