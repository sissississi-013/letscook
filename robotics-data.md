# Robotics data collection

## Goal
Capture human hand motion while people cook freely, in a format usable for training robot hands (imitation learning / teleoperation datasets).

## What the gloves give
- Per-finger rotations (Smartgloves II: 7 sensors per glove, IMU + EMF fusion, about ±1°, 39 DoF; 45 DoF with Coil Pro)
- Wrist rotation
- **NOT wrist position in space** without a Coil Pro, Vive trackers, or a camera solution

## What a useful demonstration needs
For each session, synchronized:
1. Hand pose per frame (finger joints + wrist pose with position)
2. Object states: which tool or food is held, and where
3. Task / intent label (the LLM's interpretation of the dish and steps is useful here)
4. Outcome (dish finished, what it became)
5. Timestamps

## Pipeline (draft)
```
Gloves → Rokoko Studio → Custom Streaming (JSON over UDP) → Python receiver
   → game / simulator (retarget human hand onto a simulated hand)
   → logger: raw glove stream + sim state + labels
   → export: LeRobot-style dataset (episodes, per-frame observations/actions)
```

## Principles
- **Always save the raw glove stream**, even before the game exists. Raw human data stays valuable whatever robot it's mapped onto later.
- Keep game physics close to robot physics (MuJoCo / Isaac) if the sim data itself should train policies. Game-engine physics data is mostly useful as human-motion + labels.
- Count only *successful* actions for any "your cooking taught the robot N grasps" scoreboard.
- Consent: tell players their motion data is recorded and what it's used for.

## Prior art
- Rokoko case study: Smartgloves II + Coil Pro, 221 demonstrations → autonomous policy
- DexJoCo (MuJoCo benchmark): Rokoko gloves for fingers + 2 Vive trackers for wrists, about $2,300 rig, GeoRT retargeting
- Robotic Assistant paper: Rokoko gloves → Mimic hand on Franka arm
See [research-notes.md](research-notes.md) for links.
