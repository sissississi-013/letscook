# let's cook! 🍳🧤

A free-play cooking sandbox played with Rokoko Smartgloves. Every motion you make is quietly saved as human hand-motion data for training robots to cook.

Started 2026-10-17.

## Start here
- [HOW-TO-RUN.md](HOW-TO-RUN.md): start the helpers, stream the gloves, play, export robot data
- [PROGRESS.md](PROGRESS.md): what was built on 2026-10-17, with test evidence
- [BUILD-PLAN.md](BUILD-PLAN.md): the recommended stack, phases, budget, risks
- [glove-setup.md](glove-setup.md): step-by-step Smartgloves setup checklist (do this first)
- [open-questions.md](open-questions.md): decisions still to make

## Context
- [vision.md](vision.md): what the game is and why, in the user's own words
- [robotics-data.md](robotics-data.md): short overview of the robot data side
- [tech-stack.md](tech-stack.md): what's installed on this Mac and what can't run on it
- [research-notes.md](research-notes.md): first-pass sources

## Deep research (2026-10-17)
- [research/rokoko-streaming.md](research/rokoko-streaming.md): Studio on Mac, pricing, JSON v3/v4 format, Command API, wrist-tracking options, Wi-Fi
- [research/cooking-sim-tech.md](research/cooking-sim-tech.md): engine, physics hands, grasping, slicing, liquids, cook-state model, MVP
- [research/ai-generation.md](research/ai-generation.md): text-to-3D, procedural tools, Unity import, AI physics, Claude naming/unlocks, voice
- [research/robot-data.md](research/robot-data.md): LeRobot format, MANO, retargeting, RoboCasa, consent, publishing

## Code
- `LetsCook/`: the Unity 6 game (scripts in `Assets/LetsCook/Scripts`)
- `start.sh`: starts the glove logger + chef server (`--mock` adds fake gloves)
- `receiver.py`: logs Rokoko Custom Streaming (UDP 14043) to `sessions/*.jsonl`
- `mock_glove.py`: fake JSON v4 glove stream
- `chef_server.py`: Claude names dishes, unlocks recipes, designs tools (port 8776)
- `tools_library.json`: instant tools
- `tools/to_lerobot.py`: session → LeRobot-style dataset
- `unity.sh`: batch-mode build / test / screenshot

## Status
- [ ] Phase 0: real gloves streaming (waiting on Rokoko Studio setup + streaming plan)
- [x] Phase 1: hands in Unity (from the glove stream, or mock hands)
- [x] Phase 2: grab anything (assisted pinch grasp)
- [x] Phase 3: first kitchen (cutting, heat, browning, plating)
- [x] Claude dish naming + unlocks (needs a valid API key), ask-for-any-tool
- [x] Robot data logging + LeRobot export
