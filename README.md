# let's cook! 🍳🧤

A free-play cooking sandbox played with Rokoko Smartgloves. Every motion you make is quietly saved as human hand-motion data for training robots to cook.

Started 2026-10-17.

## Start here
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
- `receiver.py`: logs Rokoko Custom Streaming (UDP 14043) to `sessions/*.jsonl`

## Status
- [ ] Phase 0: gloves streaming, first raw session saved
- [ ] Phase 1: hands in Unity
- [ ] Phase 2: grab anything
- [ ] Phase 3: first kitchen
