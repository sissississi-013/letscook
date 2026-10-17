# Research notes (2026-10-17)

## Smartgloves specs
- 7 sensors per glove, hybrid IMU + EMF sensor fusion. https://rokoko.com/products/smartgloves
- Smartgloves II: ±1° rotational accuracy, 39 DoF (45 with Coil Pro), 250 Hz IMU sampling, about 83 Hz output, up to 100 m wireless range. https://www.rokoko.com/insights/smartgloves-ii-precision-finger-hand-capture-redefined
- Price: about $1,745 to $1,995. https://store.rokoko.com/products/smartgloves-ii
- Limits: one sensor per finger, so individual knuckles are partly estimated. Very similar hand shapes and tiny tremors are unreliable.

## Robotics use
- Rokoko robotics use cases (teleoperation, datasets, Stanford project): https://rokoko.com/use-cases/robotics
- Case study, 221 demos → autonomous policy: https://www.rokoko.com/insights/collecting-robot-training-data-with-rokoko-smartgloves
- DexJoCo benchmark (gloves + Vive wrist trackers, MuJoCo): https://arxiv.org/pdf/2605.16257
- Robotic Assistant (Rokoko → Mimic hand + Franka): https://arxiv.org/pdf/2510.25713
- Teleoperation device comparison: https://arxiv.org/pdf/2511.07720

## Streaming
- Custom Streaming (JSON/UDP, format files attached): https://support.rokoko.com/hc/en-us/articles/4410416376977-Custom-Streaming
- UDP trigger messages (start/stop recording; uses port +1): https://support.rokoko.com/hc/en-us/articles/14905272191505-Rokoko-Studio-UDP-Trigger-Messages
- Rokoko device SDK (RGMP v2, separate protocol): https://sdk.rokoko.com/guides/streaming-data/

## Setup guides
- Getting Started with Smartgloves II: https://support.rokoko.com/hc/en-us/articles/46732991942289-Getting-Started-with-your-Smartgloves-II
- OptiTrack's Rokoko glove setup (good network tips): https://docs.optitrack.com/markersets/glove-device-setup/rokoko-smartglove-setup
