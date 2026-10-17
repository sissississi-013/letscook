# Rokoko Smartgloves → custom code on an M2 Mac: research

*Researched 2026-10-17. Target machine: Apple M2, 16 GB, macOS 26.2, Unity 2022.3.x / 6000.2 installed, Houdini installed, Homebrew Python 3.14.*

**How to read this.** Every claim has a source link. Rokoko's Zendesk help center returns HTTP 403 to normal browsers and fetchers, so those articles were read through its public JSON API (`https://support.rokoko.com/api/v2/help_center/en-us/articles/<id>.json`). The links below go to the human-readable pages. Code was read from shallow clones of the official GitHub repos. Tags:

- **[VERIFIED]**: I read it in a primary source, or tested it on this machine.
- **[UNVERIFIED]**: inference, or a secondary source. Check it before relying on it.
- **[CONFLICT]**: Rokoko's own sources disagree.

---

## TL;DR

1. **Studio runs natively on Apple Silicon.** Intel Macs are not supported. The official requirements page only lists macOS up to Ventura and was last updated in 2024, so **macOS 26 is not on the official list** (Studio 2.6.x does ship Mac builds). Sign-in needs a free Rokoko ID and an internet connection. Offline use is capped at 1 to 21 days depending on plan.
2. **Live streaming of any kind (Custom Streaming, Unity, Blender, Unreal, Houdini plugins) needs Plus ($28/mo or $240/yr) or higher.** The free Starter tier and the AI-only "Basic" tier cannot stream. **Pro ($70/mo or $600/yr)** adds UDP trigger messages, CSV export and the extra Command API commands, such as `tracker`. Every new team gets one 7-day Plus or Pro trial, and a credit card is required.
3. **Custom Streaming sends one complete JSON frame per UDP datagram, with no header.** The default port is **14043**. Optional compression is **LZ4 *frame* format**, magic bytes `04 22 4D 18`, not LZ4 block. The coordinate system is **left-handed, Y-up, Z-forward**, units are metres, quaternions are `x,y,z,w`, and all joints are in **world space**. In JSON v3 the glove joints are `leftIndexProximal`, `leftIndexMedial`, `leftIndexDistal`, `leftIndexTip` and so on. JSON v4 instead uses a flat Newton `joints[]` array with parent indices.
4. **The existing `receiver.py` in this project uses `lz4.block`. That is wrong for Studio's LZ4 option.** Use `lz4.frame.decompress`. I tested this on this machine with Python 3.14 and lz4 4.4.5: block decoding fails on frame data. See §2.6.
5. **Unity plugin:** use **release 2.1.2** (GitHub `.unitypackage`) or **2.1.3** (Asset Store, built against 2022.3.29f1). The `master` branch on GitHub ships an **x86_64-only** `lz4.bundle`, which fails in the arm64 Unity Editor on M2. The 2.1.2 package has a universal (x86_64 + arm64) bundle.
6. **The Command API is HTTP POST to `http://127.0.0.1:14053/v1/1234/recording/start`** (and `/recording/stop`, `/calibrate`, and so on). The latest plan table says record start/stop and calibrate work on **every tier, including Starter**. UDP trigger messages (XML on port 14047) need Pro.
7. **Absolute wrist position:** the native option is **Coil Pro ($2,995)**, which works with Studio on macOS. HTC Vive/SteamVR is dead on macOS (Valve dropped it in 2020). Quest Link has no Mac client. The most practical Mac-native alternative for robot-grade wrist poses is **Apple Vision Pro + Tracking Streamer (`avp_stream`)**, if you can get access to one. Webcam MediaPipe gives only wrist-relative 3D plus 2D image position, with no metric depth.
8. **There is a second data path that skips Studio: the Rokoko Device SDK.** Non-commercial licences start at $995/yr. It provides raw IMU data, 83.33 Hz glove streams with microsecond timestamps, and a hand solver that emits **OpenXR 26-joint `XrHandJointEXT` hands**. It is aimed at robotics, with ROS 2 and Isaac Lab examples. A macOS install guide exists, but there is no explicit statement about Apple Silicon.
9. **Wi-Fi:** Rokoko recommends **5 GHz** for Smartgloves on **channels 36, 40, 44 or 48**, a **dedicated router**, and **802.11ac rather than 802.11ax (Wi-Fi 6) mode**. Use IPv4 only. Smartgloves I cannot see 5 GHz channels at 52 or above.
10. **No official latency figure exists.** Rokoko explicitly refuses to publish one. Measure it yourself.

---

## 1. Rokoko Studio on macOS

### 1.1 Platform support
- **Apple Silicon only.** "Rokoko Studio is only officially supported on Apple-silicon Mac's, i.e. Mac's with a M1 chip or newer. Mac's with Intel CPU's are not supported." The minimum spec is an Apple M1 with 8 GB RAM. The listed OS versions are "Big Sur, Monterey, Ventura". The article was last updated **2024-04-11**. [VERIFIED] [Download and Minimum Requirements](https://support.rokoko.com/hc/en-us/articles/4418023603345-Download-and-Minimum-Requirements)
  - **macOS 26 is not officially listed.** [UNVERIFIED] Studio 2.6.0, 2.6.1 and 2.6.2 all ship "2.6.x.x (Mac)" builds ([2.6.0 notes](https://support.rokoko.com/hc/en-us/articles/46119594114833-Rokoko-Studio-2-6-0-Smartgloves-II-Support), [2.6.2 notes](https://support.rokoko.com/hc/en-us/articles/48834414923793-Rokoko-Studio-2-6-2-Vision-3-0-Support)), so current builds very likely run on 26.x, but nobody has confirmed it in writing. 16 GB RAM matches the "recommended" tier.
- **Mac-specific known issues** ([Known Issues](https://support.rokoko.com/hc/en-us/articles/4419598164497-Rokoko-Studio-Known-Issues)) [VERIFIED]:
  - "Some users may experience that they need to allow incoming connections every time they open Rokoko Studio, and not just on the first launch."
  - "On some Mac computers, Rokoko Studio may crash if you resize the window."
  - "USB devices will not connect on Intel-based Macs." This does not affect the M2.
  - Firmware updates on Mac can report a false failure.
- **2.6.2.5 fixed a macOS problem:** "Fixed an issue on macOS where Smartglove 1 failed to initialize over USB if a driver session was already running." [VERIFIED] [2.6.2 notes](https://support.rokoko.com/hc/en-us/articles/48834414923793-Rokoko-Studio-2-6-2-Vision-3-0-Support)
- **Smartgloves II support arrived in Studio 2.6.0.** [VERIFIED] [2.6.0 notes](https://support.rokoko.com/hc/en-us/articles/46119594114833-Rokoko-Studio-2-6-0-Smartgloves-II-Support)

### 1.2 Download
- Official page: <https://www.rokoko.com/products/studio/download>, which is linked from [Getting Started with Smartgloves II](https://support.rokoko.com/hc/en-us/articles/46732991942289-How-to-Get-Started-with-your-Smartgloves-II).
- The requirements article links directly to `https://downloads.rokoko.com/studio-beta-mac` and `.../studio-beta-windows` ([source](https://support.rokoko.com/hc/en-us/articles/4418023603345-Download-and-Minimum-Requirements)). That URL returned 403 to curl, so use it from a browser. [VERIFIED link exists; download not tested]
- **Do not install "Studio Legacy"** unless you need the Houdini plugin (see §3.5). Coil Pro firmware "will remove support for your Smartgloves to function in Rokoko Legacy." [VERIFIED] [Coil Pro Specs](https://support.rokoko.com/hc/en-us/articles/17592160373137-Coil-Pro-Specifications-and-Recommendations-For-Use)

### 1.3 Account requirements
- You need a free **Rokoko ID** ([id.rokoko.com](https://id.rokoko.com/)). Plans attach to a **Team**, and a default team is created automatically. [VERIFIED] [Create a Team](https://support.rokoko.com/hc/en-us/articles/4410409137297-How-to-create-a-Rokoko-Team-and-select-a-Subscription-Plan)
- "An internet connection is only required to sign-in to Rokoko Studio. However, offline usage is limited per our Studio plans." [VERIFIED] [Wi-Fi Requirements](https://support.rokoko.com/hc/en-us/articles/4410470835473-Wi-Fi-Requirements)
- Offline time before Studio must reconnect: Starter 1 day, Plus 7 days, Pro 21 days. [VERIFIED] [Subscriptions explained](https://support.rokoko.com/hc/en-us/articles/15712580191761-Rokoko-Studio-Subscriptions-plans-explained)
- Upgrades bought on the website don't appear in Studio "unless you restart or sign out and back in." [VERIFIED] [Known Issues](https://support.rokoko.com/hc/en-us/articles/4419598164497-Rokoko-Studio-Known-Issues)

### 1.4 Pricing tiers and what unlocks streaming

Sources: the [Subscriptions explained article](https://support.rokoko.com/hc/en-us/articles/15712580191761-Rokoko-Studio-Subscriptions-plans-explained) (updated 2026-09-13), the [pricing page](https://www.rokoko.com/pricing), and the live US store product JSON (`https://us.store.rokoko.com/products/<handle>.json`, fetched 2026-10-17).

| Plan | Price | Livestreaming (Custom Streaming + all plugins) | BVH | CSV | Command API rec start/stop + calibrate | Other Command API (tracker, info, resetactor…) | UDP Trigger Messages |
|---|---|---|---|---|---|---|---|
| Starter | Free | **No** | No | No | **Yes** | No | No |
| Basic | $12/mo, $120/yr (store) | **No** (AI tools only: Vision/Create, text-to-motion) | Yes (pricing page) | No | n/a | n/a | n/a |
| **Plus** | **$28/mo or $240/yr** | **Yes** | Yes | No | Yes | No | No |
| **Pro** | **$70/mo or $600/yr** | Yes | Yes | **Yes** | Yes | **Yes** | **Yes** |
| Enterprise | Quote | Yes | Yes | Yes | Yes | Yes | Yes |

- Custom Streaming article header: "Which plans is this article relevant for? **Plus Pro Enterprise**". [VERIFIED] [Custom Streaming](https://support.rokoko.com/hc/en-us/articles/4410416376977-Custom-Streaming)
- Unity guide: "Rokoko Studio (Plus, Pro, or Enterprise Subscription for streaming)". [VERIFIED] [Livestream to Unity](https://support.rokoko.com/hc/en-us/articles/4410471183633-Livestream-to-Unity)
- **[CONFLICT] Command API tier.** The 2023 [Command API article](https://support.rokoko.com/hc/en-us/articles/9738087323025-Rokoko-Studio-Command-API) says "Pro Enterprise". The 2026 plan table says record start/stop and calibrate are "Yes" on Starter and Plus, with other commands Pro-only. The API also has an error code `NOT_SUPPORTED_IN_SUBSCRIPTION_PLAN = 18`, so the server enforces tiers. Test this on your own plan.
- **[CONFLICT] Pricing page.** The [pricing page](https://www.rokoko.com/pricing) contradicts itself. One section shows Basic $10, Plus $20, Pro $50 per month (annual billing). Another shows Basic $20 and Plus $50 per month (yearly), with Pro as "Contact us". The store and the help-center table agree on Plus $28/mo or $240/yr and Pro $70/mo or $600/yr.
- Multi-year prepaid licences in the US store: Plus 2 to 5 years at $480 to $1,200, Pro 2 to 5 years at $1,200 to $3,000. [VERIFIED] [US store](https://us.store.rokoko.com/products.json)

### 1.5 Trial, education and hackathon licensing
- **7-day free trial of Plus or Pro**, once per account. To claim it: log in at id.rokoko.com, select "Create new team", select "Choose Starter", then pick "Free Plus Trial" or "Free Pro Trial". "it is mandatory to fill out the billing information". You must cancel before day 7 or it auto-renews monthly. [VERIFIED] [Free trial article](https://support.rokoko.com/hc/en-us/articles/4410424273169-How-can-I-access-the-free-7-day-trial-of-Studio-Plus-or-Pro)
  - **Tip:** take the **Pro** trial for the hackathon or a sprint. It covers Custom Streaming, CSV export, UDP triggers and the full Command API.
- **Education bundles** are in the US store, all built on Studio Pro seats [VERIFIED] [store](https://us.store.rokoko.com/products.json):
  - Group bundle: 10 seats, $995/yr or $1,990 for 2 years.
  - Class bundle: 25 seats, $3,990 for 2 years.
  - Faculty bundle: 50 seats, $2,995/yr.
- The pricing site says "Get discounts when buying at volume or for education," which points to a quote request ([pricing](https://www.rokoko.com/pricing)). **No individual student discount or hackathon licence was found.** [UNVERIFIED that none exists] Ask sales@rokoko.com or their [Discord](https://discord.gg/PacSkJmrm4).
- Hardware prices on 2026-10-17 [VERIFIED] [US store](https://us.store.rokoko.com/products/smartgloves-ii.json):
  - Smartgloves II: **$1,995**.
  - Original Smartgloves: **$695** (marked down from $1,795).
  - Coil Pro: **$2,995**.
  - Glove-ready Smartsuit Pro II electronics: $1,895.

---

## 2. Custom Streaming: the exact data format

### 2.1 Transport [VERIFIED]

| Item | Value | Source |
|---|---|---|
| Protocol | UDP. "Protocols available: UDP" | [Custom Streaming](https://support.rokoko.com/hc/en-us/articles/4410416376977-Custom-Streaming) |
| Default port | **14043** ("14043 is by default") | [Unity README](https://github.com/Rokoko/rokoko-studio-live-unity/blob/master/README.md), `UDPReceiver.cs` |
| Framing | "one complete frame per UDP datagram, with no length prefix or header of any kind" | [Custom Streaming](https://support.rokoko.com/hc/en-us/articles/4410416376977-Custom-Streaming), note 4 |
| Encoding | "The JSON itself is ASCII encoded" | same |
| Compression | Formats offered: "JSON v3", "JSON v3 (LZ4)", "JSON v4", "JSON v4 (LZ4)". "An LZ4 payload starts with the LZ4 frame magic 04 22 4D 18." | same |
| Glove → Studio port (not your concern) | 14041 ("Receiver Port … default value is 14041") | [WiFi Tab](https://support.rokoko.com/hc/en-us/articles/16092697949073-Mastering-the-WiFi-Tab) |

**Datagram size.** The official receivers allocate 65,000 bytes (Unity `bufferSize = 65000`), 81,920 bytes (Blender `recvfrom(81920)`) and 32,768 bytes (Unreal `PacketSize{ 32768 }`). The Python example uses `recvfrom(40000)` ([liveStreamListenerExample.py](https://github.com/Rokoko/studio-command-api-examples/blob/master/PythonExamples/liveStreamListenerExample.py)). Uncompressed full-body JSON with gloves and face is large, so **use a 65,535-byte buffer and enable LZ4**. A frame larger than about 64 KB cannot fit in one IPv4 UDP datagram. On loopback that is harmless, but on a LAN it can cause IP fragmentation. [UNVERIFIED: exact frame sizes]

### 2.2 LZ4: frame, not block [VERIFIED]
- **Unreal plugin** ([VirtualProductionSource.cpp](https://github.com/Rokoko/rokoko-studio-live-unreal-engine/blob/main/Plugins/Smartsuit/Source/Smartsuit/Private/VirtualProductionSource.cpp)) uses the LZ4 *frame* API:
  ```cpp
  #include "lz4frame.h"
  ...
  decompressResult = LZ4F_decompress(g_dCtx, UncompressedData.GetData(), &dstSize, PacketData.GetData(), &srcSize, nullptr);
  // "Each datagram is expected to be one complete, self-contained LZ4 frame."
  ```
- **Blender plugin** ([core/live_data_manager.py](https://github.com/Rokoko/rokoko-studio-live-blender/blob/master/core/live_data_manager.py)):
  ```python
  from lz4 import frame
  ...
  self.data = frame.decompress(self.data)
  ...
  self.data = json.loads(self.data)
  ```
- **Unity plugin** calls a native `lz4` bundle through `Unity_LZ4_uncompressSize` and `Unity_LZ4_decompress` ([LZ4Wrapper.cs](https://github.com/Rokoko/rokoko-studio-live-unity/blob/master/Assets/Rokoko/Scripts/Core/LZ4Wrapper.cs)). The uncompressed size comes from the frame header, so there is **no separate size prefix** to parse.
- The format is self-describing: if `data[:4] == b"\x04\x22\x4d\x18"` it is compressed, otherwise it is plain JSON.

### 2.3 JSON v3 schema (the "actors" format, used by the Unity plugin)

From the [Custom Streaming article](https://support.rokoko.com/hc/en-us/articles/4410416376977-Custom-Streaming), verbatim (the comments are Rokoko's):
```
{
      version: 3,
      fps: 60,
      scene: {
            timestamp: 0,
      actors: [
      {
            name: "Dimitris", //arbitary size
            color: {r, g, b}, // bytes
            meta: {
                  hasGloves: true, //Indicates if fingers are controlled
                  hasLeftGlove: true,
                  hasRightGlove: true,
                  hasBody: true, //Indicates if body is controlled
                  hasFace: true //Indicates if face is controlled
            }
            dimensions: { totalHeight: 180, hipHeight: 100, ... }
            body: { // optional section
                  hip: {
                        position {x: 0, y: 0, z: 0} // Y-up, Z-forward
                        rotation {x: 0, y: 0, z: 0, w: 1} // quaternion
                  },
                  spine: {...}
            },
            face: { leftEyeBlink: 0, // 0-100 ... }
      }],
      props: [{ name, color, type: 0/1/2, position: {x,y,z}, rotation: {x,y,z,w} }]
    }
}
```
Notes from the code (more reliable than the doc sketch):
- `color` is parsed as an **int array** (`public int[] color;` in Unity's [JsonLiveSerializerV3.cs](https://github.com/Rokoko/rokoko-studio-live-unity/blob/master/Assets/Rokoko/Scripts/Core/JsonLiveSerializerV3.cs)), not `{r,g,b}`.
- The `version` value: Blender parses `"3,0"`-style strings (`ver_str = str(self.version).replace(".", ",")`), and the v4 docs say frames carry `"3,0"`. **Do not use `version` to tell formats apart** (see §2.4).
- `dimensions`: the v3 sketch shows centimetres (`180`, `100`), but the v4 notes say metres (`1.75`, `0.94`). **[CONFLICT]** Log a real frame and check.

**Full JSON v3 `body` key list**, copied verbatim from Unity's `BodyFrame` class ([JsonLiveSerializerV3.cs](https://github.com/Rokoko/rokoko-studio-live-unity/blob/master/Assets/Rokoko/Scripts/Core/JsonLiveSerializerV3.cs)). The same names appear in Unreal's `SuitParseBone(...)` calls in [VirtualProductionSource.cpp](https://github.com/Rokoko/rokoko-studio-live-unreal-engine/blob/main/Plugins/Smartsuit/Source/Smartsuit/Private/VirtualProductionSource.cpp).
```
hip, spine, chest, neck, head,
leftShoulder, leftUpperArm, leftLowerArm, leftHand,
rightShoulder, rightUpperArm, rightLowerArm, rightHand,
leftUpLeg, leftLeg, leftFoot, leftToe, leftToeEnd,
rightUpLeg, rightLeg, rightFoot, rightToe, rightToeEnd,

leftThumbProximal,  leftThumbMedial,  leftThumbDistal,  leftThumbTip,
leftIndexProximal,  leftIndexMedial,  leftIndexDistal,  leftIndexTip,
leftMiddleProximal, leftMiddleMedial, leftMiddleDistal, leftMiddleTip,
leftRingProximal,   leftRingMedial,   leftRingDistal,   leftRingTip,
leftLittleProximal, leftLittleMedial, leftLittleDistal, leftLittleTip,
(right* identical)
```
Each joint has this structure:
```csharp
public struct ActorJointFrame { public Vector3Frame position; public Vector4Frame rotation; }
public struct Vector4Frame { public float x; public float y; public float z; public float w; }
```
- Glove-only data: `meta.hasGloves` / `hasLeftGlove` / `hasRightGlove` are true and `hasBody` is false. The Unity plugin still runs `UpdateSkeleton` when `hasBody || hasGloves` ([Actor.cs](https://github.com/Rokoko/rokoko-studio-live-unity/blob/master/Assets/Rokoko/Scripts/Mono/Inputs/Actor.cs)). [VERIFIED]
- Finger mapping to Unity Humanoid: `Medial` maps to `Intermediate`, e.g. `case HumanBodyBones.LeftThumbIntermediate: return frame.leftThumbMedial;` ([RokokoHelper.cs, development branch](https://github.com/Rokoko/rokoko-studio-live-unity/blob/development/Assets/Rokoko/Scripts/Mono/RokokoHelper.cs)). `*Tip` joints are streamed but not mapped to Humanoid bones. [VERIFIED]
- **Thumb naming differs between formats.** v3 uses `ThumbProximal/Medial/Distal/Tip`. v4 and Newton use `Finger1Metacarpal/Proximal/Distal/Tip`, with no Medial (§2.4). The CSV exporter uses its own biomechanics scheme (§5). [VERIFIED]

### 2.4 JSON v4 schema (the "newtons" format) [VERIFIED]
Verbatim from the [Custom Streaming article](https://support.rokoko.com/hc/en-us/articles/4410416376977-Custom-Streaming):
```
{
  version: "3,0",              // see note 1 - v4 frames still report "3,0"
  fps: 60,
  scene: {
    timestamp: 0,              // seconds, floating point
    actors: [],                // always emitted; empty when streaming JSON v4
    newtons: [
      {
        name: "Newton 01",
        color: [r, g, b],      // integer array, 0-255
        meta: { hasHands: true, hasLeftHand: true, hasRightHand: true, hasBody: true, hasFace: false },
        dimensions: { totalHeight: 1.75, hipHeight: 0.94 },   // metres
        joints: [              // flat list, world space, order not guaranteed
          { name: "Hips", parent: -1, position: {x: 0, y: 0.94, z: 0}, rotation: {x: 0, y: 0, z: 0, w: 1} },
          { name: "Spine1", parent: 0, position: {...}, rotation: {...} },
          {...}
        ],
        face: { leftEyeBlink: 0, ... }   // omitted entirely when there are no blendshapes
      }
    ],
    props: [],
    characters: []
  }
}
```
Key notes, quoted from the same article:
- **Telling v3 from v4:** "a v3 frame fills scene.actors and leaves scene.newtons empty; a v4 frame fills scene.newtons and leaves scene.actors empty. Both keys are always present."
- **Coordinates:** "Left-handed, Y up, Z forward - the same convention as JSON v3. Positions are in metres. Rotations are quaternions, ordered x, y, z, w. Every joint transform is in WORLD space, even though the joint carries a parent index… Do not compose parent transforms into children - you will double-transform the pose."
- **Newton joint names** (76 bones): `Newton, Root, Hips, Spine1..Spine4, Neck, Head, HeadTip, LeftShoulder, LeftArm, LeftForeArm, LeftHand, …, LeftThigh, LeftShin, LeftFoot, LeftToe, LeftToeTip, …` plus, for each hand, `{Left|Right}FingerN{Metacarpal, Proximal, Medial, Distal, Tip}` with N=1 (thumb) to 5 (little). "The thumb (Finger1) has no Medial joint."
- "Match joints by name and build the hierarchy from the parent index. Do not hardcode array positions."
- **Why v4 matters for robotics:** v4 includes **metacarpal** joints and fingertip joints with explicit hierarchy. That makes it the better format for logging hand data you will later retarget to a robot hand.

**Converting to other frames.** These conversions are inferred from Rokoko's own code, so they are reliable.
- To Blender (right-handed, Z-up), from [animations.py](https://github.com/Rokoko/rokoko-studio-live-blender/blob/master/core/animations.py):
  ```python
  def pos_studio_to_blender(x, y, z):
      return -x, -z, y
  def rot_studio_to_blender(w, x, y, z):
      return w, x, z, -y
  ```
- To Unity: none needed. Unity is also left-handed, Y-up, Z-forward, and the plugin uses `new Quaternion(vec4.x, vec4.y, vec4.z, vec4.w)` directly ([RokokoHelper.cs](https://github.com/Rokoko/rokoko-studio-live-unity/blob/master/Assets/Rokoko/Scripts/Mono/RokokoHelper.cs)).
- To ROS (right-handed, Z-up): compose the Blender mapping with your robot base frame. Rokoko's ROS bridge uses a different source frame (OpenXR), see §2.8.

### 2.5 Other official receivers worth reading
- **Unreal (current, v1.13.6, 2026-10-06):** parses `scene.props`, `scene.actors`, `scene.newtons` and `scene.characters`. Supports Windows, Mac and Linux, and its README warns that `0.0.0.0` binds all interfaces on Mac ([README](https://github.com/Rokoko/rokoko-studio-live-unreal-engine)).
- **Godot (community):** [GodotXRRokokoTracker](https://github.com/Malcolmnixon/GodotXRRokokoTracker). Plain JSON v3 only, reading `json.scene.actors[0]` and `actor.meta.hasGloves`.
- **Python, community:** [Zhengro/rokoko_data_forwarding](https://github.com/Zhengro/rokoko_data_forwarding) (2020, ROS 1, uncompressed JSON, port 14043). It is old but shows the pattern.
- **Python, official listener:** [liveStreamListenerExample.py](https://github.com/Rokoko/studio-command-api-examples/blob/master/PythonExamples/liveStreamListenerExample.py), described as "example of reading from a custom stream target (without lz4 compression)". It reads `scene_info.get('actors')` and `theActor['body']['hip']['position']`.

### 2.6 A tested Python receiver for this Mac
Tested 2026-10-17 on this M2 with `/opt/homebrew/bin/python3.14` (3.14.0) and `lz4==4.4.5`. The `cp314 macosx_11_0_arm64` wheel exists on [PyPI](https://pypi.org/project/lz4/). I sent a synthetic LZ4-frame packet over UDP 14043. `lz4.frame.decompress` decoded it. `lz4.block.decompress` on the same packet raised `LZ4BlockError … Error code: 4`. [VERIFIED locally]
```python
# pip install lz4
import json, socket, lz4.frame
LZ4_MAGIC = b"\x04\x22\x4d\x18"

def decode_packet(data: bytes) -> dict:
    if data[:4] == LZ4_MAGIC:          # "JSON v3 (LZ4)" / "JSON v4 (LZ4)"
        data = lz4.frame.decompress(data)
    return json.loads(data)            # plain "JSON v3" / "JSON v4"

sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
sock.setsockopt(socket.SOL_SOCKET, socket.SO_RCVBUF, 4 * 1024 * 1024)
sock.bind(("127.0.0.1", 14043))        # Studio on same Mac → loopback; use "0.0.0.0" for LAN
while True:
    pkt, _ = sock.recvfrom(65535)
    f = decode_packet(pkt)
    sc = f["scene"]
    if sc.get("newtons"):              # JSON v4
        hands = {j["name"]: j for j in sc["newtons"][0]["joints"] if "Finger" in j["name"] or j["name"].endswith("Hand")}
    elif sc.get("actors"):             # JSON v3
        b = sc["actors"][0]["body"]
        idx = b["leftIndexProximal"]["rotation"]   # {x,y,z,w}, world space, left-handed Y-up
```
**This is a fix for the project's existing `receiver.py`**, which imports `lz4.block` and guesses `uncompressed_size=len(data)*20`. That path will never decode Studio's LZ4 option. Today it only works because the plain-JSON branch runs first.

Logging tip for robot data: record **both** `scene.timestamp` (Studio clock, in seconds) and a host monotonic receive time such as `time.monotonic_ns()`. The Blender plugin handles non-monotonic Studio timestamps (`if delta >= 0 … else: self.timestamp += self.timedelta_prev`), which suggests timestamps can jump backwards. [VERIFIED code; UNVERIFIED cause]

### 2.7 Coordinate and quaternion summary

| | Studio JSON v3/v4 | Unity | Device SDK OpenXR output |
|---|---|---|---|
| Handedness | left | left | right |
| Up | +Y | +Y | +Y ("glove frame rotated +90° about X") |
| Forward | +Z | +Z | bone points −Z (OpenXR) |
| Units | m (v4 explicit) | m | m |
| Quaternion order | x,y,z,w | x,y,z,w | x,y,z,w |
| Space | world | — | wrist-relative ("the wrist stays at the origin") |

Sources: [Custom Streaming](https://support.rokoko.com/hc/en-us/articles/4410416376977-Custom-Streaming), [SDK hand solving](https://sdk.rokoko.com/guides/hand-solving/), [ros2 bridge frame_convert.py](https://github.com/Rokoko/rokoko-ros2-bridge/blob/main/ros2_ws/src/rkk_hand_bridge/rkk_hand_bridge/frame_convert.py) (`# OpenXR points +Z backward along the bone, so forward is -Z.`). [VERIFIED]

### 2.8 Alternative path that skips Studio: the Rokoko Device SDK [VERIFIED unless marked]
- **What it is:** "direct, low-latency access to motion data from Rokoko hardware — without going through Rokoko Studio… Robotics and teleoperation — retarget hand and body sensors to robot models in real time." It gives raw accelerometer, gyroscope and magnetometer data, per-sensor quaternions, and 3D positions for EMF (Coil) sensors. ([Getting Started with the SDK](https://support.rokoko.com/hc/en-us/articles/48147584456081-Getting-Started-with-the-Rokoko-SDK))
- **Platforms:** "Windows, macOS, Linux x64, and Linux Arm64". Supported devices are Smartgloves I and II and Smartsuit Pro II. Gloves stream at up to 83 Hz. "USB streaming is supported on Smartgloves II only." ([sdk.rokoko.com](https://sdk.rokoko.com/))
  - The macOS install is a script from the SDK tile on id.rokoko.com that installs to `~/.local/share/rokoko-device-sdk`. Check it with `rokoko-sdk --version` ([macOS install](https://sdk.rokoko.com/guides/install/macos)). **Apple Silicon is not explicitly stated.** [UNVERIFIED]
  - **Mac risk:** the Isaac example says "The `rkk-hand-solver` binary needs glibc ≥ 2.39," which suggests at least that build is Linux-only ([Isaac example](https://sdk.rokoko.com/examples/isaac/)). The SDK also ships a "dependency-free Python" solver (`/examples/rgmp_hand_solver/`), which would run on Mac ([hand solving](https://sdk.rokoko.com/guides/hand-solving/)). [UNVERIFIED whether the native solver ships for macOS arm64]
- **Licence:** a dedicated SDK licence is required ([SDK article](https://support.rokoko.com/hc/en-us/articles/48147584456081-Getting-Started-with-the-Rokoko-SDK)). [US store](https://us.store.rokoko.com/products.json) prices: non-commercial **$995 (1 yr)**, $2,695 (3 yr), $3,745 (5 yr); commercial $2,995, $7,495, $9,995.
- **Protocols:**
  - **RCSP** is a TCP command server.
  - **RGMP v2** is binary over **TCP**. "All values are encoded in little-endian format"; the header is `uint32 msg_prefix` + `uint32 msg_len`; types are 1 Definition (JSON), 2 Data, 3 Disconnect; the data header is `device_id u32, group_id u32, timestamp_us u64`. ([RGMP spec](https://sdk.rokoko.com/reference/rgmp/))
  - Driver port **12276**. Solver port **12277**.
  - Rokoko's Python parser ([rgmp.py](https://github.com/Rokoko/rokoko-ros2-bridge/blob/main/ros2_ws/src/rkk_hand_bridge/rkk_hand_bridge/rgmp.py)):
    ```python
    def read_frame(sock) -> tuple[int, bytes]:
        msg_prefix, msg_len = struct.unpack("<II", _read_exact(sock, 8))
    _POSE_SIZE = 28  # FLOAT[7]: position + quaternion, 4 bytes each
    _DATA_HEADER_SIZE = 16  # device_id, group_id, timestamp_us
    x, y, z, qx, qy, qz, qw = struct.unpack_from("<7f", payload, offset)
    ```
- **Hand solver:** `rkk-hand-solver --emit-openxr` outputs "the 26 `XrHandJointEXT` joints of OpenXR's `XR_EXT_hand_tracking` default set", with flags `--emit local|world|both`, `--no-driver` and `--rgmp-addr 127.0.0.1:12276` ([hand solving](https://sdk.rokoko.com/guides/hand-solving/)).
- **Glove sensor layout:** "five finger sensors, an arm sensor, and a central hub". Target frames are `hub`, `thumb_finger_sensor` … `little_finger_sensor`, `arm_sensor` and `coil_pro`. The default group runs at `expected_rate_hz` 83.33. Smartgloves I lacks the magnetometer, USB streaming and the `emf_only` group. Smartgloves I has "Medium" Coil range, and II has "Long range". ([Smartgloves reference](https://sdk.rokoko.com/reference/products/smartgloves/))
- **Robotics integrations:**
  - [rokoko-ros2-bridge](https://github.com/Rokoko/rokoko-ros2-bridge): 26 joints in `XrHandJointEXT` order, a yaw-calibration service, and "Two gloves are supported, one left and one right." On macOS, ROS 2 is a source build that needs SIP disabled.
  - An Isaac Lab `RokokoGloveDevice` example that records HDF5 ([Isaac example](https://sdk.rokoko.com/examples/isaac/)).
- **Yaw calibration:** "The solver streams poses referenced to magnetic north, so until you calibrate, the hand is correct in shape but rotated by an arbitrary amount about the vertical axis." ([ros2 bridge README](https://github.com/Rokoko/rokoko-ros2-bridge))
- **Can Studio and the SDK run at the same time?** [UNVERIFIED] Probably not on the same gloves, because both act as the driver. Treat them as either/or per session.

---

## 3. The Rokoko Unity plugin

### 3.1 Install
- **Asset Store:** "Rokoko Live", free, **v2.1.3, released Jul 30 2025, built against Unity 2022.3.29f1**, compatible with Built-in, URP and HDRP ([Asset Store](https://assetstore.unity.com/packages/tools/integration/rokoko-live-98045)). [VERIFIED]
- **GitHub releases:** `RokokoStudioLive_2_1_2.unitypackage` (2025-07-23, release note "fix lz4 compression for mac os") ([releases](https://github.com/Rokoko/rokoko-studio-live-unity/releases)). Install with Assets → Import Package → Custom Package. [VERIFIED]
- **The Apple Silicon gotcha** (checked by downloading and inspecting the binaries with `lipo`):
  - `master` branch, `Assets/Rokoko/Scripts/Plugins/LZ4/lz4.bundle/Contents/MacOS/lz4`: **x86_64 only.** An arm64 Unity Editor will fail to `DllImport("lz4")`, and LZ4 streams won't decode.
  - `development` branch (commit "update lz4 macos bundle", 2025-03-26) and **release 2.1.2**: universal `x86_64 arm64`.
  - **Use 2.1.2 or the Asset Store 2.1.3, not a clone of master.** If the bundle is quarantined, you may also need `xattr -dr com.apple.quarantine` on `lz4.bundle`. [UNVERIFIED]
  - Workaround if needed: set `StudioManager.useLZ4Compression = false` (development/2.1.2 only) and stream plain "JSON v3" over loopback.
- **Unity version:** the README says "minimum officially supported version of Unity is 2018.4.x LTS". The package.json on the development branch says `"unity": "2022.3"` ([package.json](https://github.com/Rokoko/rokoko-studio-live-unity/blob/development/Assets/Rokoko/package.json)). Your installed **2022.3.62f2** is the safest match. Unity 6 (6000.2.9f1) is **not officially tested**. [UNVERIFIED for Unity 6] The plugin is plain C# plus `JsonUtility` and is likely fine.

### 3.2 Format and port
- The plugin expects **JSON v3**. The error string reads "Please ensure you are using JSON v3 as forward data format". Use Studio's "Unity" livestream target, or a Custom target set to JSON v3. ([README FAQ](https://github.com/Rokoko/rokoko-studio-live-unity))
- **It does not parse JSON v4 `newtons`.** The 2.1.2 serializer adds `scene.characters[]` with `{name, parent, position, rotation}` joints for retargeted characters, but has no `newtons` field ([development JsonLiveSerializerV3.cs](https://github.com/Rokoko/rokoko-studio-live-unity/blob/development/Assets/Rokoko/Scripts/Core/JsonLiveSerializerV3.cs)). [VERIFIED]
- `StudioManager.receivePort = 14043`, `useLZ4Compression = true` by default ([StudioManager.cs, development](https://github.com/Rokoko/rokoko-studio-live-unity/blob/development/Assets/Rokoko/Scripts/Mono/StudioManager.cs)).
- **Port conflict:** only one process can bind UDP 14043. If both a Python logger and Unity need the stream, either add a **second receiver in Studio** (Custom Streaming allows several receivers via the "plus" button, e.g. 14043 for Unity and 14044 for Python), or have Unity forward packets. [VERIFIED that multiple receivers are allowed: [Custom Streaming](https://support.rokoko.com/hc/en-us/articles/4410416376977-Custom-Streaming)]

### 3.3 How it maps glove data to a rig ([Actor.cs](https://github.com/Rokoko/rokoko-studio-live-unity/blob/master/Assets/Rokoko/Scripts/Mono/Inputs/Actor.cs)) [VERIFIED]
- Add the `Actor` component to a model whose **Rig is set to Humanoid**. "Any custom character/actor must be in a T Pose." The `Profile Name` must equal the Studio Actor Profile name. Bone mapping is either `Animator`, which uses Humanoid `HumanBodyBones`, or `Custom`, which uses the `HumanBoneMapping` component.
- Rotation modes are `Offset` (the default), `World` and `Self`. Offset mode computes:
  ```csharp
  boneTransform.rotation = this.transform.rotation * worldRotation * offsets[bone];
  // offsets = Inverse(SmartsuitTPose[bone]) * (Inverse(hips.rotation) * bone.rotation)
  ```
  Here `SmartsuitTPose` is a hard-coded table of Studio's T-pose world rotations. For example, `LeftIndexProximal = (-0.5,-0.5,0.5,-0.5)` and `LeftThumbProximal = (-0.561,-0.701,0.430,-0.092)`. **So your hand rig must be in a T-pose (palms down, fingers straight) at Awake for offsets to be correct.**
- Only **Hips** gets position. All other bones get rotation only. **The wrist's world position is not applied to a hand-only rig.** For a floating-hands cooking game you'll need your own script that reads `body.leftHand.position` (see §6). [VERIFIED]
- Known issue: "Weird finger offset when livestreaming" ([issue #20](https://github.com/Rokoko/rokoko-studio-live-unity/issues/20)). Rokoko attributed it to the model's rest pose.

### 3.4 Sample scenes (release 2.1.2, `Assets/Rokoko/Scenes/`) [VERIFIED by unpacking]
- `RokokoPluginExampleScene.unity`: default actors and props.
- `RokokoPluginExampleScene_CustomActors.unity`: custom actors and props.
- `RokokoPluginExampleScene_CustomCharacter.unity`: Studio-retargeted characters.
- `RokokoPluginExampleScene_DriftCorrection.unity`: a "Tracker" transform is sent through the Command API `tracker` command ("Auto Send Tracker Command" on StudioManager). This is the hook for feeding an external wrist position back into Studio. It needs Pro. ([Command API article](https://support.rokoko.com/hc/en-us/articles/9738087323025-Rokoko-Studio-Command-API))

### 3.5 Houdini
- The official Houdini plugin requires "Houdini 18.5, SideFX Labs, **Rokoko Studio Legacy** and a Rokoko subscription (Plus, Pro, or Enterprise…)" ([Houdini article](https://support.rokoko.com/hc/en-us/articles/4410463283345-Houdini-Download-Links-Walkthrough)). Legacy conflicts with the Coil Pro and current-Studio Smartgloves II workflow. **For Houdini, use FBX/BVH exports, or write a Python SOP/HDA receiver using the §2.6 code.** [VERIFIED requirement; recommendation is mine]

---

## 4. Command and trigger APIs: start and stop recording from code

### 4.1 HTTP Command API [VERIFIED]
Sources: [Command API article](https://support.rokoko.com/hc/en-us/articles/9738087323025-Rokoko-Studio-Command-API) and the [examples repo](https://github.com/Rokoko/studio-command-api-examples).
- Enable it in Studio under Menu → Settings → Command API. The defaults are **port 14053** and **API key `1234`**.
- URL format: `http://{ip}:{port}/{version}/{apiKey}/{command}` with an HTTP **POST** and a JSON body. Examples use both `v1` and `v2`.
- Commands: `recording/start`, `recording/stop`, `calibrate`, `resetactor`, `info`, `tracker`, `livestream` (`{"enabled": true}`), `playback`, `pose`. "for any of them to work, you need to have a scene open."
```python
import requests
BASE = "http://127.0.0.1:14053/v1/1234"
requests.post(f"{BASE}/recording/start", json={"filename": "take_042", "time": "00:00:00:00", "frame_rate": "60", "back_to_live": True})
requests.post(f"{BASE}/recording/stop",  json={"back_to_live": True})
requests.post(f"{BASE}/calibrate", json={"device_id": "", "countdown_delay": 3, "skip_suit": False, "skip_gloves": False,
                                         "use_custom_pose": False, "pose": "straight-arms-down"})  # tpose | straight-arms-down | straight-arms-forward
requests.post("http://127.0.0.1:14053/v2/1234/livestream", json={"enabled": True})   # toggle streaming from code
```
- Response format: `{description, response_code, startTime, parameters[]}`. Codes include `OK = 0`, `RECORDING_ALREADY_STARTED = 4`, `NO_LIVE_DEVICES = 14`, `NOT_SUPPORTED_IN_SUBSCRIPTION_PLAN = 18`.
- `tracker` takes `{device_id, bone_attached, position:{X,Y,Z}, rotation:{X,Y,Z,W}, timeout, is_query_only}`. It "Attaches an external tracker… to an actor, which can then determine its global position." It needs Pro. **This lets you push a webcam- or Vision-Pro-derived wrist position into Studio**, so Studio's recordings and exports contain it. Whether `bone_attached` accepts hand bones for gloves-only actors is [UNVERIFIED].
- **Tier:** see the [CONFLICT] note in §1.4.

### 4.2 UDP Trigger Messages (Pro and Enterprise) [VERIFIED]
Sources: [UDP Trigger article](https://support.rokoko.com/hc/en-us/articles/14905272191505-Rokoko-Studio-UDP-Trigger-Messages) and the [TriggerMessages examples](https://github.com/Rokoko/studio-command-api-examples/tree/master/TriggerMessages).
- Enable "Send Messages" and/or "Receive Messages" under Studio Settings → Trigger Messages. "Port: This is the port used to receive messages. To send messages, the port number +1 will be used." The examples send to **14047** and listen on **14048**.
- Message format is XML:
  ```python
  MESSAGE = '<CaptureStart><TimeCode VALUE="00:00:00:00"/><Name VALUE="NewClip"/>' \
            '<SetActiveClip VALUE="False"/><ProcessID VALUE="12345"/></CaptureStart>'
  sock.sendto(MESSAGE.encode(), ("127.0.0.1", 14047))   # CaptureStop for stop
  ```
- Studio also **broadcasts** CaptureStart and CaptureStop when you hit record in its UI. That is useful for syncing a game-side log with Studio takes.

---

## 5. Recording and export formats (the fallback data path)
Source: [Export options](https://support.rokoko.com/hc/en-us/articles/18877624744721-Rokoko-Studio-Export-options-and-file-formats) and the [plan table](https://support.rokoko.com/hc/en-us/articles/15712580191761-Rokoko-Studio-Subscriptions-plans-explained). [VERIFIED]
- **Free Starter: FBX only.** "exporting to FBX format is available on all Rokoko Studio subscriptions… Exporting in CSV, BVH format and in a 60 FPS require a paid subscription." So **Starter exports are capped below 60 fps**. The exact cap is [UNVERIFIED], likely 30.
- FBX versions 2011 to 2017, ASCII or binary. Skeleton presets are Rokoko Newton (with metacarpals), HumanIK, Mixamo, Unreal, iClone and Custom.
- **BVH:** Plus and above. Body only, no face.
- **CSV:** **Pro and Enterprise only**. "The CSV export includes Body and Finger data… follows the Joint Coordinate System (JCS) recommended by the International Society of Biomechanics… Timestamps… in milliseconds… rounded; 0, 33, 66 100. Orientation… in degrees… rotation about two body fixed axes and one floating axis… not the same [as euler angles]. Position… meters. Velocity… m/s. Acceleration… m/s²." The JCS angles are ISB anatomical angles, not quaternions, so **for robot training prefer FBX with the Newton skeleton or your own live-stream log.**
- Studio itself recommends "not exceeding 30 minutes of recordings per scene, and keeping most recordings to max. 5 minutes" ([Known Issues](https://support.rokoko.com/hc/en-us/articles/4419598164497-Rokoko-Studio-Known-Issues)).
- **Free-tier robot-data path:** record in Studio (start/stop through the Command API, allowed on Starter per the 2026 table), export FBX with the Newton skeleton, then parse it in Houdini, Blender or Python FBX SDK. The cost is a lower fps and no live data.

---

## 6. Wrist and hand position in space: options on a Mac

**The core problem.** Smartgloves are IMU-based. Without a Coil Pro, the "hand position" in the stream comes from the solved skeleton, not from measured position. Rokoko's own notes say: "When using the Smartgloves II as standalone, quickly flapping your arms will result in the arm sensor drifting during the motion and it will drift back when you stop" ([2.6.0 notes](https://support.rokoko.com/hc/en-us/articles/46119594114833-Rokoko-Studio-2-6-0-Smartgloves-II-Support)). Gloves-only, no-Coil world position is effectively a fixed or IK-driven anchor. [UNVERIFIED exact behaviour]

| Option | Works on Mac? | Cost | Data | Verdict |
|---|---|---|---|---|
| **Coil Pro** | **Yes**, through Studio (Mac-native) | $2,995 | Absolute glove (hub/arm) position in Studio's world, fused in-stream | Best integrated option. Same JSON stream, same timestamps |
| HTC Vive Tracker | **No** | — | — | SteamVR macOS support ended 2020 |
| Quest Link / Air Link | **No** Mac client | — | — | — |
| Quest standalone app streaming hand/controller poses over UDP | Yes (build an Android APK from Unity on the Mac) | Quest 3 ~$500 | Wrist pose ~60–90 Hz | DIY. Optical hand tracking over Rokoko gloves untested; strapping controllers to forearms is the fallback |
| **Apple Vision Pro + Tracking Streamer** | Yes (Python client on Mac) | AVP hardware | Wrist 4×4 + 27-joint skeleton, <100 ms wireless | Best non-Rokoko metric wrist source; robotics-proven. Unverified with gloves on |
| iPhone ARKit body tracking | Phone-side only | phone | ARBodyAnchor skeleton, rear camera, needs whole body in view | Poor fit for desk cooking |
| Webcam + MediaPipe Hands/Pose | Yes | free | 21 landmarks: image xy + relative z; world landmarks hand-centred | Good for coarse wrist placement; no metric depth |
| Rokoko Vision / Create | Web | — | Offline video-to-motion only | Not live (webcam live capture retired) |

Details and sources:

- **Coil Pro** [VERIFIED]
  - Price: "$2,995" ([product page](https://www.rokoko.com/products/coil-pro), [store](https://us.store.rokoko.com/products/coil-pro.json)).
  - How it works: "generating an Electromagnetic Field (EMF) that existing sensors in Rokoko Smartgloves can use to determine their position in space relative to the Coil Pro… The Coil Pro does not use or connect to WiFi" ([Specs](https://support.rokoko.com/hc/en-us/articles/17592160373137-Coil-Pro-Specifications-and-Recommendations-For-Use)).
  - Range: "pick up the signal… within a 5m radius… for optimal EMF accuracy… stay within a 3 meter radius." Sensors saturate closer than about **50 cm** to the Coil ([Capture Space](https://support.rokoko.com/hc/en-us/articles/21541993570193-Capture-Space-of-the-Coil-Pro)). **For a seated or standing desk game, put the Coil about 1 m or more from your hands.** Do not put it under the desk right below your hands.
  - Interference: "All metals can affect the Coil Pro signal quality. Ferrous metals in particular" ([Coil FAQ](https://support.rokoko.com/hc/en-us/articles/21598651434641-Coil-Pro-FAQs)). **Steel desks, laptop stands and real kitchen pans will warp the field.** Use a wooden table and plastic or wooden props. Studio has a signal-quality heatmap.
  - Known jitter ([Coil Known Issues](https://support.rokoko.com/hc/en-us/articles/22166939492881-Coil-Pro-Known-Issues)): "especially noticeable if you bend your hand backwards… or put two Smartgloves close together." **Two-handed cooking moves (holding a bowl and whisking) will hit this.** A hand-smoothing toggle exists.
  - Gloves-only calibration pose: "stand upright, with your arms bent at the elbow and kept at a 90 degree angle" ([Calibrating with Coil](https://support.rokoko.com/hc/en-us/articles/21982734451473-Calibrating-With-The-Coil-Pro)).
  - **[CONFLICT] Smartgloves I compatibility.** The product page says "The Coil Pro requires the Smartgloves II" ([product](https://www.rokoko.com/products/coil-pro)). The 2.6.1 notes say "Compatible with Smartglove 1" ([2.6.1](https://support.rokoko.com/hc/en-us/articles/47324364931089-Rokoko-Studio-2-6-1-with-MAJOR-Coil-Suit-Upgrade)), and the SDK reference lists Smartgloves I EMF range as "Medium range" ([SDK](https://sdk.rokoko.com/reference/products/smartgloves/)). Original gloves likely work with shorter range.
- **HTC Vive:** Valve: "SteamVR has ended OSX support so our team can focus on Windows and Linux" (2020) ([9to5Mac](https://9to5mac.com/2020/05/01/valves-steamvr-ends-support-for-macos/), [Engadget](https://www.engadget.com/valve-cancels-steamvr-support-macos-162443682.html)). Rokoko's Vive workflow is Unreal on Windows, with "Click the Windows Key…" steps ([Virtual Production guide](https://support.rokoko.com/hc/en-us/articles/18316035716113-Setting-up-Virtual-Production-in-Rokoko-Studio)). **Not viable on the M2.** [VERIFIED]
- **Quest:** there is no Meta Quest Link for macOS ([Meta forum](https://communityforums.atmeta.com/discussions/dev-general/please-add-quest-link-cable-support-on-apple-siliconmacos/1098725)). [VERIFIED as of the forum posts; no 2026 change found] A standalone Quest build that sends wrist poses over UDP to the Mac is technically straightforward. [UNVERIFIED: tracking quality with Smartgloves worn]
- **Apple Vision Pro:** ARKit `HandTrackingProvider` is **visionOS-only** ([Apple docs](https://developer.apple.com/documentation/arkit/handtrackingprovider)). [Improbable-AI/VisionProTeleop](https://github.com/Improbable-AI/VisionProTeleop) provides the App Store app "Tracking Streamer" and `pip install --upgrade avp_stream`. Data is `data.right.wrist` (4×4) plus a 27-joint skeleton. Transport is WebRTC with latency "< 100ms" wireless. It is already used for robot teleop with MuJoCo and Isaac frames. [VERIFIED README; UNVERIFIED hand tracking while wearing Rokoko gloves, since Apple's tracking is optical and gloves change hand appearance]
- **iPhone ARKit body tracking:** `ARBodyTrackingConfiguration` (iOS/iPadOS 13+) "tracks human body poses… using the rear-facing camera" ([Apple docs](https://developer.apple.com/documentation/arkit/arbodytrackingconfiguration)). It needs a person in the rear camera view at a distance, which suits full-body use rather than hands over a counter. [UNVERIFIED: wrist accuracy at desk range]
- **MediaPipe:** 21 landmarks. "world coordinates in meters with the origin at the hand's geometric center". Image-landmark z: "depth at the wrist being the origin". Supports `LIVE_STREAM` mode with `detect_async` ([Hand Landmarker Python guide](https://developers.google.com/edge/mediapipe/solutions/vision/hand_landmarker/python)). So you get **2D wrist image position and hand-local 3D, but no camera-to-hand metric depth.** Depth must be estimated, for example from palm size (needs a known hand size, which the Rokoko actor profile provides) or from a second camera. `mediapipe 1.1.1` ships a `py3-none-macosx_11_0_arm64` wheel (2026-10-14) ([PyPI](https://pypi.org/project/mediapipe/)), so it should install on Python 3.14. [UNVERIFIED runtime on 3.14] Fusion design: glove gives finger and wrist **orientation**, webcam gives wrist **position**. Do it in your own code, or push the result back to Studio through the `tracker` command (Pro).
- **Rokoko Vision:** "As of July 16, 2026, Rokoko Vision will become part of Rokoko Create… the following legacy features will be retired on August 16, 2026: Dual-cam motion capture, Online webcam capture" ([2.6.2 notes](https://support.rokoko.com/hc/en-us/articles/48834414923793-Rokoko-Studio-2-6-2-Vision-3-0-Support)). **Not a live option.** [VERIFIED]

**Recommendation for a Mac build:** **Coil Pro if the budget allows.** It is the only option that is Mac-native, inside the same Studio stream with the same clock, and needs no extra software. Without the budget, use **webcam MediaPipe** for game-feel wrist placement now. If you can borrow one, use **Vision Pro Tracking Streamer** for higher-quality robot logs.

---

## 7. Known issues and operational notes

### 7.1 Wi-Fi (2.4 vs 5 GHz) [VERIFIED]
- "It's recommended that the Rokoko Smartgloves operate in the **5GHz** band" ([Best routers for Smartgloves](https://support.rokoko.com/hc/en-us/articles/4410479492241-Best-Wi-Fi-routers-for-Smartgloves)). Troubleshooting says "Make sure you are connected to a 5GHz network" ([Gloves not appearing](https://support.rokoko.com/hc/en-us/articles/4410479456785-Smartgloves-Not-Appearing-in-Rokoko-Studio)). **This contradicts `glove-setup.md` in this project ("2.4 GHz is safest").** Update that file.
- Use channels **36, 40, 44 or 48**. "Smartgloves I cannot find 5 Ghz networks running on some channels, likely all channels at 52 or higher" ([Known Issues](https://support.rokoko.com/hc/en-us/articles/4419598164497-Rokoko-Studio-Known-Issues), [Supported channels](https://support.rokoko.com/hc/en-us/articles/4410462893457-Supported-WiFi-Frequencies-and-Channels)).
- "Devices cannot always connect to Wi-Fi routers running on 802.11ax. Changing it to 802.11ac will fix the issue." IPv4 only. "does not currently support Wi-Fi 6 or IPv6" ([Known Issues](https://support.rokoko.com/hc/en-us/articles/4419598164497-Rokoko-Studio-Known-Issues), [Wi-Fi Requirements](https://support.rokoko.com/hc/en-us/articles/4410470835473-Wi-Fi-Requirements)).
- Use a dedicated offline router, with the **computer on Ethernet** to that router ([Multi-device](https://support.rokoko.com/hc/en-us/articles/44501355655057-Managing-Multi-Device-Scenes-Maximum-Limits)). Avoid office and university networks. A phone hotspot is an accepted test ([Gloves not appearing](https://support.rokoko.com/hc/en-us/articles/4410479456785-Smartgloves-Not-Appearing-in-Rokoko-Studio)). The Smartsuit's own hotspot mode does **not** support gloves ([Wi-Fi Requirements](https://support.rokoko.com/hc/en-us/articles/4410470835473-Wi-Fi-Requirements)).
- Smartgloves II: "TCP-based WiFi streaming replaces UDP for more stable transmission with less packet loss" ([SG II product](https://www.rokoko.com/products/smartgloves-ii)). Through the SDK, SG II can also stream over **USB** (`rokoko-sdk --auto-stream-usb`). **Studio itself still requires Wi-Fi for data** ([Smartgloves FAQ](https://support.rokoko.com/hc/en-us/articles/20912004920593-Rokoko-Smartgloves-FAQs): "motion capture data… can only be transmitted to Rokoko Studio through a WiFi network"). [CONFLICT: the FAQ may predate SG II; test USB in Studio 2.6.x]
- Power: SG II needs "5V/3A output per USB A output… (10,000+ mAh recommended)" ([SG II getting started](https://support.rokoko.com/hc/en-us/articles/46732991942289-How-to-Get-Started-with-your-Smartgloves-II)). Original gloves need 2A minimum ([Multi-device](https://support.rokoko.com/hc/en-us/articles/44501355655057-Managing-Multi-Device-Scenes-Maximum-Limits)).

### 7.2 Latency and rates
- **There is no official latency number.** Rokoko says: "We don't focus on artificial benchmark numbers here… Lower perceived latency" ([SG I vs II](https://www.rokoko.com/insights/rokoko-smartgloves-i-vs-smartgloves-ii-is-it-worth-upgrading-2026-comparison)). [VERIFIED absence]
- Rates:
  - SG I: "internal framerate of 400 and an external framerate of 100" ([FAQ](https://support.rokoko.com/hc/en-us/articles/20912004920593-Rokoko-Smartgloves-FAQs)), or 166.67 Hz on the product comparison table. [CONFLICT]
  - SG II: "250 Hz internal IMU sampling rate" ([product](https://www.rokoko.com/products/smartgloves-ii)).
  - SDK glove stream: 83.33 Hz ([SDK reference](https://sdk.rokoko.com/reference/products/smartgloves/)).
  - The Studio stream carries `fps` (60 in the examples).
- **Measure it yourself:** tap the table with a finger while filming at 240 fps with a phone, with the Unity view on screen. Or log `scene.timestamp` against host receive time to see jitter.
- Accuracy: "rotational dynamic accuracy of ± 1 degree" ([FAQ](https://support.rokoko.com/hc/en-us/articles/20912004920593-Rokoko-Smartgloves-FAQs)).

### 7.3 Drift and calibration
- "It is recommended to re-calibrate before every new clip" ([SG II getting started](https://support.rokoko.com/hc/en-us/articles/46732991942289-How-to-Get-Started-with-your-Smartgloves-II)).
- Shortcuts: Ctrl+Shift+B calibrates all devices ([Multi-device](https://support.rokoko.com/hc/en-us/articles/44501355655057-Managing-Multi-Device-Scenes-Maximum-Limits)). On SG II, press and hold the glove button to calibrate (standalone mode only) ([2.6.1](https://support.rokoko.com/hc/en-us/articles/47316328335377-Rokoko-Studio-2-6-1-Coil-Pro-Stabilization-Filter)). Or call the `calibrate` Command API with `countdown_delay`.
- Calibration poses: `tpose`, `straight-arms-down`, `straight-arms-forward` ([Command API](https://support.rokoko.com/hc/en-us/articles/9738087323025-Rokoko-Studio-Command-API)). With Coil and gloves only, use elbows bent at 90°.
- Drift notes: SG II standalone arm-sensor drift during fast arm motion. "The hand movement might look a bit robotic when continuously rotating the wrist while the arm is stationary" ([2.6.0](https://support.rokoko.com/hc/en-us/articles/46119594114833-Rokoko-Studio-2-6-0-Smartgloves-II-Support)). This matters for stirring motions. The Drift Fix filter is post-processing only, for root drift ([Drift Fix](https://support.rokoko.com/hc/en-us/articles/41620261484945-Rokoko-Studio-Update-Real-time-Locomotion-Drift-Fix)).
- Magnetic environment matters for IMUs as well ([Degaussing guide](https://support.rokoko.com/hc/en-us/articles/44474445861521-Degaussing-your-space-A-guide-to-magnetic-interference) is linked from the multi-device article).
- "Starting a recording immediately after the devices are paired to an actor can lead to issues with the recorded clip" ([Known Issues](https://support.rokoko.com/hc/en-us/articles/4419598164497-Rokoko-Studio-Known-Issues)).

### 7.4 Multi-glove
- One pair is one Actor Profile. Studio handles "up to 2 or 3 Smartsuit Pro II devices and pairs of Smartgloves", with "a high risk of WiFi lag when connecting more than three actors" ([Multi-device](https://support.rokoko.com/hc/en-us/articles/44501355655057-Managing-Multi-Device-Scenes-Maximum-Limits), [Coil FAQ](https://support.rokoko.com/hc/en-us/articles/21598651434641-Coil-Pro-FAQs)).
- 2.6.2.5 "Fixed a connection issue when using more than two pairs of Smartgloves" ([2.6.2](https://support.rokoko.com/hc/en-us/articles/48834414923793-Rokoko-Studio-2-6-2-Vision-3-0-Support)).
- Set up each glove's Wi-Fi one at a time over USB. Power on and connect all gloves before any suit.
- SDK/ROS bridge: "Two gloves of the same handedness are not [supported]" ([ros2 bridge](https://github.com/Rokoko/rokoko-ros2-bridge)).

### 7.5 macOS firewall and Local Network privacy
- Rokoko says to turn the firewall off, or configure it under System Settings → Network → Firewall (Ventura and later) ([Firewall exception](https://support.rokoko.com/hc/en-us/articles/20848880788113-Creating-A-Firewall-Exception)). A better approach is to keep the firewall on and **allow incoming connections for Rokoko Studio** (gloves to Studio). Rokoko notes the Mac may re-prompt on every launch ([Known Issues](https://support.rokoko.com/hc/en-us/articles/4419598164497-Rokoko-Studio-Known-Issues)).
- **Local Network privacy** (Apple [TN3179](https://developer.apple.com/documentation/technotes/tn3179-understanding-local-network-privacy)): apps must hold the Local Network privilege to talk to LAN addresses. Grant it to **Rokoko Studio** and to the **Unity Editor or your built game** if they receive from another machine. TN3179: "macOS automatically allows local network access by… Command-line tools run from Terminal or over SSH", so the Python receiver run from Terminal is exempt. Toggle it under System Settings → Privacy & Security → Local Network. [VERIFIED]
- **If Studio and the receivers are on the same Mac, stream to `127.0.0.1`.** TN3179 defines a local network as one on a "broadcast-capable network interface. Such interfaces include Wi-Fi and Ethernet", so loopback should avoid both the Local Network prompt and LAN fragmentation. [UNVERIFIED: inferred from the definition]

---

## 8. Open questions to test on day 1
1. Does Studio 2.6.2.x launch and pair gloves on **macOS 26.2**? (Not officially listed.)
2. Plus vs Pro trial: does `recording/start` work on Starter, as the 2026 table claims?
3. In a gloves-only JSON v3 stream (no Coil), what is in `body.leftHand.position`: constant, IK-driven or drifting? Log 60 s and plot.
4. Does Unity 6000.2 import the 2.1.2 package cleanly, or should you stay on 2022.3.62f2?
5. Studio "dimensions" units: cm or m?
6. Does Smartgloves II stream to Studio over USB in 2.6.x, or Wi-Fi only?
7. Do Vision Pro or Quest optical hand tracking still work while wearing Smartgloves?

---

## Recommended setup for this project

**Phase 0: this week, about $28 (or free with the trial)**
1. Install current **Rokoko Studio (2.6.2.x, Mac)** from <https://www.rokoko.com/products/studio/download>. Create a Rokoko ID, then a new team, then start the **Pro 7-day trial** (it covers Custom Streaming, CSV, UDP triggers and the full Command API). Before day 7, either cancel or switch to **Plus ($28/mo)** if you only need streaming.
2. **Network:** a dedicated 5 GHz router on **channel 36 to 48**, **802.11ac mode** (not ax), WPA2, DHCP auto. Connect the Mac to it by **Ethernet** if possible. Set up each glove's Wi-Fi over USB one at a time. Allow Studio through the macOS firewall and Local Network privacy settings.
3. **Studio → Livestreaming → Custom Streaming**, with two receivers on **UDP, JSON v3 (LZ4), 127.0.0.1**:
   - Port **14043** for Unity.
   - Port **14044** for a Python logger. Optionally log **JSON v4 (LZ4)** on the logger port instead: it adds metacarpals, fingertips and explicit hierarchy, which is better for robot retargeting.
4. **Unity 2022.3.62f2** with **Rokoko Studio Live 2.1.2** (GitHub release `.unitypackage`, universal LZ4 bundle) or Asset Store 2.1.3. **Do not clone master** (x86_64-only LZ4). For floating hands, write a small script that reads `actor.body.{left,right}Hand.position/rotation` plus the finger joints directly from `StudioReceiver.onStudioDataReceived`, because the stock `Actor` only positions Hips.
5. **Python 3.14 logger** using `lz4.frame` (§2.6). **Fix the existing `receiver.py`** (`lz4.block` → `lz4.frame`). Write JSONL or Parquet with `scene.timestamp`, host `monotonic_ns`, the game event stream (grasp, release, object IDs) and the calibration events.
6. **Recording control from the game:** POST `http://127.0.0.1:14053/v1/1234/recording/start` and `/stop` with a take name matching your log file. On Pro, also listen for Studio's UDP `CaptureStart` and `CaptureStop` on 14048 to sync takes started from Studio's UI. Call `/calibrate` with `countdown_delay: 3` at the start of each session.
7. **Wrist position (interim):** a webcam with **MediaPipe Hand Landmarker (LIVE_STREAM)** in the Python process. Estimate depth from palm width using the actor-profile hand size. Fuse it with glove orientation in Unity: position from the camera, rotation and fingers from the glove. Treat this as **game-quality, not ground truth**, and flag it as such in the logs.

**Phase 1: if the data needs to be robot-grade**
- **Buy a Coil Pro ($2,995).** It gives absolute glove position inside the same Studio stream on macOS. Put it on a wooden stand **at least 1 m from the play area**, away from steel, using plastic or wooden kitchen props. Expect jitter when the hands come close together, and use the hands-smoothing toggle when the game needs it.
- **Alternatively, or in addition, the Rokoko Device SDK** (non-commercial $995/yr). Use it for logging sessions that need raw IMU, `timestamp_us`, USB streaming (SG II) and **OpenXR 26-joint output** that drops straight into dex-retargeting, Isaac Lab or ROS 2 pipelines. Confirm with Rokoko first that `rokoko-sdk` and `rkk-hand-solver` ship native **macOS arm64** builds. The Isaac doc's glibc note suggests the solver may be Linux-first, so fall back to the pure-Python solver or a Linux box. You will likely run the SDK **instead of** Studio in those sessions, so the Unity game would read the SDK stream rather than Studio's JSON.
- **If you can borrow an Apple Vision Pro:** `avp_stream` gives a metric wrist 4×4 plus 27 joints to Python on the Mac. That is a strong independent wrist-pose ground truth to fuse with or validate the gloves, but first test that optical tracking holds up with gloves on.

**Avoid:** Vive trackers or SteamVR, Quest Link, Rokoko Vision live webcam (retired), Studio Legacy (except for the Houdini plugin; use FBX into Houdini instead), and CSV as the primary robot format (ISB JCS Euler-like angles in degrees at 33 ms steps).
