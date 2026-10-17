# Cooking sandbox: technology research

Researched 2026-10-17 for **let's cook!**: a free-play ("paidia") cooking sandbox driven by Rokoko Smartgloves, with wrist position from a separate tracker. All hand motion is logged for robot training.

**Target machine:** Apple M2, 16 GB unified memory. Unity and Houdini are installed.

**How to read this file**
- **[unverified]** means I couldn't confirm the claim from a primary source. It may be a vendor claim, a secondary source, or my own inference.
- **[estimate]** means an engineering judgment, not a measured number.
- Prices are list prices seen in October 2026 snapshots, before tax. Asset Store prices change often, so check them before buying.

---

## 0. TL;DR

- **Use Unity for the game.** Its editor runs natively on Apple Silicon. You already have it, there's an official Rokoko plugin, and it has the only mature ecosystem of physics-hand and food-physics assets that work on Metal.
- **Isaac Sim is not an option on this Mac.** It needs Ubuntu or Windows plus an RTX 4080 or better.
- **Use MuJoCo as the robotics back end.** It runs on the Mac. Recorded sessions get retargeted to robot hands and replayed there offline. The game doesn't run inside it.
- **There is no practical VR headset path on macOS.** Plan for a desktop monitor view now. Port to a Windows PC or headset later if you want immersion.
- **Realism comes from a continuous food-state model, not from simulating chemistry.** Temperature, protein denaturation, browning, moisture, and structure are numbers that drive shaders and physics parameters. Use mesh slicing for cuts, Obi particles for liquids, egg contents and granular foods, and pre-fractured Houdini assets for shells.

---

## 1. Engine choice

### 1.1 Comparison

| Option | Runs on M2 Mac? | Physics fidelity for hands + food | Hand / food asset ecosystem | Path to robot training data |
|---|---|---|---|---|
| **Unity 6** | Yes, native Apple Silicon editor since 2021.2 ([Unity blog](https://blog.unity.com/en/engine-platform/2021-2-tech-stream-release-is-now-available), [system reqs](https://docs.unity3d.com/2021.2/Documentation/Manual/system-requirements.html)) | PhysX rigid bodies, ArticulationBody (Featherstone reduced-coordinate solver, PD-like drives) ([docs](https://docs.unity.cn/ja/2020.2/ScriptReference/ArticulationBody.html)). Food via assets (Obi, Zibra). | Best: Auto Hand, Hurricane VR, XR Hands, Obi, Zibra, mesh slicers, official Rokoko plugin ([GitHub](https://github.com/Rokoko/rokoko-studio-live-unity)) | Medium. Log your own data, then export. Unity's URDF Importer exists but hasn't had a push since Oct 2023 ([ecosyste.ms](https://awesome.ecosyste.ms/projects/github.com%2FUnity-Technologies%2FURDF-Importer)). |
| **Unreal Engine 5** | Yes, universal binary since UE 5.2 ([Epic](https://www.unrealengine.com/en-US/tech-blog/unreal-engine-5-2-brings-native-support-for-apple-silicon-and-other-developments-for-macos)). No hardware ray tracing on macOS, so Lumen uses software RT ([GameFromScratch](https://gamefromscratch.com/unreal-engine-5-2-on-macos/)). | Chaos physics is good for destruction. Mac-specific Chaos/VR status **[unverified]**. | Fewer hand/food-sim plugins. Zibra has a UE version ([Fab listing](https://docs.unrealengine.com/marketplace/ja/product/zibra-liquids)). | Same as Unity |
| **Godot 4.4+** | Yes, and it gained a Metal backend in 4.4 ([release](https://godotengine.org/releases/4.4/index.html)). Jolt is built in but opt-in and experimental ([GameFromScratch](https://gamefromscratch.com/godot-4-4-gets-native-jolt-physics-support/)). | Jolt is a solid rigid-body engine. No production soft-body or fluid plugins at Obi/Zibra level. | Thin. You'd build hands, slicing, and fluids yourself (there is a [Godot slicer](https://github.com/cj-dimaggio/godot-slicer)). | Same as Unity |
| **Isaac Sim / Isaac Lab** | **No.** Requirements list only Ubuntu 22.04/24.04 and Windows 10/11 on x86_64, plus DGX Spark (aarch64, DGX OS). Minimum GPU is an RTX 4080 with 16 GB. macOS is not mentioned ([Isaac Sim 5.1 reqs](https://docs.isaacsim.omniverse.nvidia.com/5.1.0/installation/requirements.html), [6.0 reqs](https://docs.isaacsim.omniverse.nvidia.com/6.0.0/installation/requirements.html)). A community fork, "IsaacLab-mlx", swaps in a light Mac simulator and isn't Isaac Sim ([repo](https://ithub.global.ssl.fastly.net/RobotFlow-Labs/IsaacLab-mlx)). | Excellent (PhysX 5 GPU, deformables) | Robot-centric, not a game | Best-in-class, but only on a Linux/Windows RTX box or cloud |
| **MuJoCo 3.x** | Yes. The CPU engine runs on macOS. The JAX backend (MJX-JAX) lists Apple Silicon. MuJoCo Warp targets NVIDIA ([MJX docs](https://mujoco.readthedocs.io/en/stable/mjx.html), [mujoco_warp](https://github.com/google-deepmind/mujoco_warp)). There's an official Unity plug-in that needs the native dylib ([docs](https://mujoco.readthedocs.io/en/stable/unity.html)). | Excellent contact for rigid hands. Deformables through "flex" (since 3.0) ([changelog](https://mujoco.readthedocs.io/en/3.0.0/changelog.html)). No fluids or cutting. | None for games. Rendering is basic. | **Excellent.** It's the lingua franca of robot learning. RoboCasa and robosuite are built on it. |
| **Genesis** | Partly. Its compiler targets Apple Metal ([docs](https://genesis-world.readthedocs.io/en/latest/user_guide/overview/what_is_genesis.html)). I found no Metal benchmarks or maturity reports **[unverified]**. | Unified rigid + MPM (liquid, elastoplastic, granular) + FEM + SPH ([solver docs](https://genesis-world.readthedocs.io/en/latest/_sources/user_guide/physics/beyond_rigid_bodies.md.txt)). In principle this is the closest thing to "real" food physics. | None. It's Python-only with no game tooling. | Good. It's designed for robot learning. |
| **ManiSkill 3 (SAPIEN)** | CPU simulation and rendering only. "GPU simulation is currently not yet supported on MacOS." Docs say a Mac suits debugging and inference ([docs](https://maniskill.readthedocs.io/en/latest/user_guide/getting_started/macos_install.html)). | Good rigid. Limited soft. | None | Good |
| **RoboCasa** | Built on robosuite + MuJoCo ([paper](https://arxiv.org/html/2406.02523v1)), so it should run on a Mac. I didn't test this **[unverified]**. | Rigid only. Cooking is symbolic, e.g. "place pan on burner". | 120 kitchen scenes and 2,500+ objects in the original release. RoboCasa365 has 365 tasks ([LeRobot docs](https://huggingface.co/docs/lerobot/robocasa.md)). | Excellent kitchen assets and task templates. Not designed for human glove play. |

### 1.2 Verdict

Use a **hybrid: Unity for play, MuJoCo for robot data.**

- **Isaac Sim is excluded on this hardware.** If it's needed later, rent a cloud RTX box for batch replay, not for interactive play.
- **Unity is the only option** that hits all three of these on an M2: realistic-feeling physical hands, liquids and soft bodies on Metal, and an existing Rokoko live plugin.
- **Data mapping is engine-independent if you log the right things** (see §6.4). Robot learning pipelines don't consume Unity scenes. They consume these streams:
  - joint angles
  - wrist pose
  - object poses
  - contacts
  - video
  - language/task labels

  You then (a) retarget human hand joints to robot hands with [dex-retargeting](https://github.com/dexsuite/dex-retargeting) (from AnyTeleop; `pip install dex_retargeting`), and (b) optionally re-simulate in MuJoCo with RoboCasa assets to check physical feasibility. DexCap is the closest prior art: glove finger joints plus a 6-DoF wrist tracker, retargeted with IK into robot policies ([arXiv 2403.07788](https://arxiv.org/pdf/2403.07788)).
- **Genesis is the interesting wildcard** for offline, high-fidelity food physics such as dough, sauces, and cutting MPM materials. It's not a game engine. Revisit it if its Metal backend proves solid.

### 1.3 Display constraint: no VR on the Mac

- SteamVR dropped macOS in 2020 ([UploadVR](https://uploadvr.com/steamvr-drops-mac-support/)).
- Meta's hand-grab-pose recording tutorial says Link "doesn't support Mac" ([Meta docs](https://developers.meta.com/horizon/documentation/unity/unity-isdk-creating-handgrab-poses/)).

Plan a **desktop first-person view on the monitor**: fixed camera over the counter, with the player's tracked hands rendered in it.

- **Gloves only:** without a headset, the gloves give finger data but not head pose. Many glove-only sims put the camera at a fixed "chef's eye" position and let the tracked wrist position move the hands within the counter volume.
- **If you need a headset later**, a Windows PC with a Quest over Link is the cheapest route. Unity makes that port trivial.

### 1.4 Glove facts that drive architecture

- **Sensors and rate:** Smartgloves use 7 IMUs per glove and stream over Wi-Fi at up to 100 fps ([CG Channel](https://www.cgchannel.com/2020/09/rokoko-launches-smartgloves/)). IMU-only fingers drift and have no absolute wrist position. That's why you need a separate wrist tracker.
- **Coil Pro:** Rokoko sells Coil Pro (EMF) for "precise wrist tracking and less noise" ([Rokoko](https://www.rokoko.com/insights/what-finger-tracking-device-should-you-choose-in-2026)).
- **Prices:**
  - Smartgloves + Coil Pro bundle: **$4,795** in the US store, €3,495 in the EU store ([US store](https://us.store.rokoko.com/pages/finger-capture-coil-pro), [EU store](https://eu.store.rokoko.com/pages/finger-capture-coil-pro))
  - Coil Pro alone: €2,995
  - Smartgloves (gen 1): $1,095 ([shop](https://www.rokoko.com/shop/smartgloves))
- **Rokoko Studio on Mac:** officially supported on Apple Silicon only. 16 GB RAM is recommended ([support](https://support.rokoko.com/hc/en-us/articles/4418023603345)).
- **Unity plugin:** receives body, face, and finger data from Studio Live on UDP port 14043 by default. Up to 5 actors ([GitHub](https://github.com/Rokoko/rokoko-studio-live-unity)).
- **Licensing:** live streaming out of Studio needs a paid Studio plan (see `../tech-stack.md`).
- **No force sensing.** DexCap lists missing force/tactile data as a main limitation of glove capture ([arXiv](https://arxiv.org/pdf/2403.07788)). Your sim must infer grip force (see §2.3).

---

## 2. Physics-based hands in Unity

### 2.1 Options

| Solution | Price | What it is | Fit for Rokoko-driven hands | Mac |
|---|---|---|---|---|
| **Auto Hand – VR Interaction** | **$90/seat** ([Asset Store](https://assetstore.unity.com/packages/tools/physics/auto-hand-vr-interaction-165323)) | Rigidbody hand that "automatically configures to the collider's shape when grabbing" (grab solver), plus weight, force distribution, throw, and physics levers/doors. v4.0.4, Unity 6 / 2022.3 / 2019.4, all render pipelines. | Good. Its newer input system allows "individual finger control through input" ([Auto Hand docs](https://earnestrobot.notion.site/Controller-Input-6707823c47d2410b98db85a75ef66e6d)), so you can feed per-finger curl. One reviewer says the hand-tracking docs are weak. Whether it accepts full joint rotations rather than curl scalars is **[unverified]**. | Pure C#, so it should work **[unverified on Mac]** |
| **Hurricane VR** | **$69.99** ([Asset Store](https://assetstore.unity.com/packages/tools/physics/hurricane-vr-physics-interaction-toolkit-177300)) | Physics interaction toolkit, v2.9.3.c (Jul 2026). Strong physical hands for controllers. | Built around controllers. Hand-tracking support isn't stated on the listing **[unverified]**. | Pure C# **[unverified]** |
| **XR Interaction Toolkit + XR Hands** | Free (Unity packages) | XR Hands is a standard hand-joint data model with a pluggable `XRHandSubsystem`. You can **implement your own provider** ([docs](https://docs.unity3d.com/Packages/com.unity.xr.hands@1.4/manual/implement-a-provider.html)). It defines finger-shape metrics (full curl, base curl, tip curl, pinch, spread) ([manual](https://docs.unity3d.com/Packages/com.unity.xr.hands@1.10/manual/index.html)). | **Good as the data layer:** write a Rokoko→XRHands provider and every XR-Hands-compatible tool works. XRI's grab is not physics-based finger grasping. | Yes |
| **Ultraleap Unity Plugin (Physical Hands)** | Free, open source ([GitHub](https://github.com/ultraleap/UnityPlugin/releases)) | Physical Hands with grasp helpers. These modify object mass on grasp and restore it on release (v6.8.0). There's a GrabDetector with hysteresis ([docs](https://docs.ultraleap.com/xr-and-tabletop/xr/unity/plugin/features/grab-detector.html)). | Good reference code. Driving it from non-Leap data needs an adapter: Ultraleap ships its own XRHands subsystem ([docs](https://docs.ultraleap.com/xr-and-tabletop/xr/unity/xri-integration.html)), so it may accept XR Hands input **[unverified]**. | The tracking service has Apple Silicon builds ([downloads](https://www.ultraleap.com/downloads/3di)). Plugin is C#. |
| **Meta Interaction SDK** | Free | Hand-grab poses, pose scaling between hand sizes. There's a Mac-specific editor tutorial ([Meta](https://developers.meta.com/horizon/documentation/unity/unity-isdk-create-handgrab-poses-mac/)). | Built for "snap to authored pose" grabs, which is the opposite of free-form realism. Tied to Meta's runtime. Not recommended here. | Partial |
| **Custom ArticulationBody / ConfigurableJoint hand** | Free | You build it. ArticulationBody drives have stiffness/damping, acting like a PD controller ([explainer](https://simonpham.medium.com/using-articulation-bodies-in-unity-01d50771f1ab)). | **Best for robot-data fidelity.** The hand becomes an explicit kinematic chain with joint targets, the same structure as a robot hand. Known pitfall: finger mass disturbs palm tracking. Fix it by raising mass scale ([GameDev.net](https://gamedev.net/forums/topic/705383-precisely-controlling-velocity-of-rigidbodies-that-are-using-joints/)). | Yes |
| MRTK hand physics | Free | Kinematic rigidbodies on fingertip/palm joints ([MRTK docs](https://microsoft.github.io/MixedRealityToolkit-Unity/version/releases/2.5.0/Documentation/Extensions/HandPhysicsService/HandPhysicsServiceOverview.html)) | Too simple. Collisions only, no grasp. | Yes |

**Recommendation:**
1. Write a **Rokoko → XR Hands provider** (or a direct component) to get a clean, standard joint stream.
2. Drive a **custom ArticulationBody hand** whose joint targets are the glove angles and whose root follows the wrist tracker through a force/velocity controller.
3. Add a **grasp-assist layer** borrowing Ultraleap's ideas.
4. Buy Auto Hand ($90) as a reference or fallback if the custom hand stalls.

### 2.2 The two-hand pattern (tracked ghost + physical hand)

Every good physics-hand system works the same way:

1. **The tracked hand is kinematic and invisible.** It goes exactly where the glove says.
2. **The physical hand has colliders and is driven toward the tracked hand** by joint drives and a wrist PD/velocity controller. It is blocked by the world and can't pass through the counter.
3. **The rendered hand is the physical hand.** If separation exceeds a threshold, say 10 cm, show the ghost or snap back, as Owlchemy's "tomato presence" family of tricks does. Owlchemy hides the hand when holding objects and lets objects break out of the hand ([Owlchemy](https://owlchemylabs.com/tomatopresence), [Game Developer Q&A](https://www.gamedeveloper.com/design/xrdc-q-a-owlchemy-labs-andrew-eiche-and-devin-reimer-explore-vr-interactions)).

### 2.3 Detecting a grasp without force feedback

Without haptics, players curl their fingers *through* the virtual object. That's the main problem to solve, and it's also a **free force signal**: the gap between where the tracked finger wants to be and where the physical finger is blocked acts like a spring.

1. **Contact-based grasp test**, run every physics step per object:
   - Thumb (or palm) in contact, AND at least one opposing finger in contact.
   - Contact normals roughly opposed (angle > ~120°). That's a two-sided pinch or wrap.
   - The object sits inside the friction cones of the contacts. This is the classic force-closure check from grasp analysis.
2. **Curl intent with hysteresis.** Use XR Hands curl values per finger. Start the grasp above a threshold and release below a lower one. Ultraleap's example uses 0.8 to grab and 0.7 to release to avoid jitter ([Ultraleap](https://docs.ultraleap.com/xr-and-tabletop/xr/unity/plugin/features/pinch-and-grab-detection.html)).
3. **Virtual grip force:**
   - `F_grip ≈ k · Σ(penetration of tracked fingertip past physical fingertip)`, clamped to a plausible human range of about 0–50 N **[estimate]**.
   - Use it to compare against the friction needed (`μ·F_grip ≥ m·g` and the inertial load) and slip the object if it falls short. Heavy pans then need a firmer squeeze.
   - Use it to crush or squash soft foods past a threshold (tomato, egg, dough).
   - **Log it.** It's a proxy force label for robot training.
4. **Attachment.** Once a grasp is valid and stable for about 50–100 ms, attach the object with a breakable joint (FixedJoint or a configurable joint with a break force) instead of relying on friction alone. PhysX friction grasps tend to jitter. Release when curl drops, contacts break, or the joint snaps. Ultraleap's helpers also temporarily change object mass while grasped ([release notes](https://github.com/ultraleap/UnityPlugin/releases/tag/com.ultraleap.tracking/6.8.0)).
5. **Tool-specific grips.** A knife or spatula has a "handle" affordance. Once grasped, constrain its orientation relative to the palm more tightly so cutting feels controllable. Keep the free-form contact grasp for everything else.

---

## 3. Food simulation: realistic but feasible on an M2

### 3.1 Cutting and slicing

| Tool | Price | Notes |
|---|---|---|
| **EzySlice** | Free, MIT ([GitHub](https://github.com/DavidArayan/ezy-slice)) | Plane cut. Interpolates UV/normal/tangent and generates cross-section caps. **Convex meshes only.** Last push Aug 2023 ([gittrend](https://gittrend.io/repo/DavidArayan/ezy-slice)). Cuts are infinite planes, with no partial cuts ([Unity forum](https://discussions.unity.com/t/how-do-i-cut-a-mesh-partially-249069/249069)). |
| **Dynamic Mesh Cutter** | Paid ([Asset Store](https://assetstore.unity.com/packages/tools/modeling/dynamic-mesh-cutter-208384)), price **[unverified]** | Plane cut into multiple disjoint meshes. Handles skinned meshes. "Optimized for mobile and VR". |
| **Turbo Slicer 2** | Paid ([Asset Store](https://assetstore.unity.com/packages/tools/modeling/turbo-slicer-2-73236)), price **[unverified]** | Long-lived commercial slicer |
| VR Cutting | $4.99 ([Asset Store](https://assetstore.unity.com/packages/tools/physics/vr-cutting-170894)) | Last updated 2020. Not recommended. |

**Tricks that make cutting feel real:**

- **Cut only when the blade really cuts.** Trigger a slice when the blade edge moves *along* its edge direction with enough speed and downward pressure, using virtual grip force from §2.3. Don't trigger on a simple collision. A dull push should squash soft foods instead.
- **Interior material.** Fill cut caps with a separate "flesh" material and texture: tomato seeds, onion rings. Bake the interior textures in Houdini.
- **Recompute physics per piece.** After each slice, compute volume (mass = density × volume) and set a convex collider. Unity convex MeshColliders are capped at 255 triangles ([note](https://simready.artlabs.ai/compare/v-hacd-vs-coacd-vs-manual-hulls)), so use a simplified hull. CoACD has a Unity package ([GitHub](https://github.com/sarahweiii/CoACD)), but its runtime use is **[unverified]**. V-HACD tools in Unity are editor-only ([Asset Store](https://assetstore-fallback.unity.com/packages/tools/physics/collision-mesh-generator-convex-decomposition-154904)). At runtime, a single convex hull per piece is fine because food pieces are near-convex.
- **Budget pieces.** Meta's fruit-slicing tutorial warns slicing causes frame spikes and recommends pooling and limiting active pieces ([Meta](https://developers.meta.com/horizon/documentation/unity/unity-tutorial-ai-vr-polish/)).
- **Merge tiny pieces.** Below roughly 1 cm³ **[estimate]**, a piece stops being a mesh and becomes "diced/minced matter": an Obi granular particle or a pile impostor carrying the same food state.
- **Concave foods** such as a bell pepper or a hollow onion are a problem for EzySlice. Model them as nested convex shells: onion layers, or pepper walls as separate convex segments.

### 3.2 Liquids and sauces

| Tool | Price | Mac/Metal | Notes |
|---|---|---|---|
| **Obi Fluid** (Virtual Method) | **$60** (also seen at $66) ([Asset Store](https://assetstore.unity.com/packages/package/63067)). Obi Physics Suite is **$179** ([Asset Store](https://assetstore.unity.com/packages/tools/physics/obi-physics-suite-313315)). | **Burst CPU backend** runs on any platform Burst targets. Burst supports Apple Silicon ([Burst docs](https://docs.unity3d.com/Packages/com.unity.burst@1.8/manual/building-projects.html)). Obi 7's **Compute (GPU)** backend runs wherever compute shaders work ([Obi backends](https://obi.virtualmethodstudio.com/manual/7.0/backends.html)). Metal compute on M2 is **[unverified]**. | Viscosity (corotational model, buckling/coiling), "polarity" surface tension, immiscible vs miscible fluids, and **4 per-particle user-data channels that diffuse at a set miscibility rate, e.g. temperature or color** ([Obi manual](https://obi.virtualmethodstudio.com/manual/7.0/emittermaterials.html)). That's ideal for sauces that carry heat and color and mix. Same solver does granular. |
| **Zibra Liquid** | **$49.99/seat** ([Asset Store](https://assetstore-fallback.unity.com/packages/tools/physics/zibra-liquid-266451), [Gumroad](https://zibraai.gumroad.com/l/zibraliquid)) | System requirements list **macOS editor, Metal, arm64** ([Zibra reqs](https://zibra.notion.site/System-requirements-3f155f15a1dc4e239763af58c137bdfd)). Required Unity is 2021.3–2023.2, and the listing says **Unity 6 isn't fully supported**. | GPU, very fast. Multiple liquid types that mix, viscosity/surface tension presets (oil, slime). Liquid can push rigidbodies ([collider docs](https://zibra.notion.site/Collider-5252a1660e03427e9db667ce7972810f)). Less suited to per-particle cooking state than Obi. |
| Fluvio / FluvioFX | Free | VFX-Graph-based, HDRP-only. Its README says VFX Graph has "severe impact" on Metal ([GitHub](https://github.com/thinksquirrel/fluviofx/blob/db6a2187a9a3e5890a21823aee18e08dfb172969/README.md)) | Avoid |
| Custom SPH compute | Free | You write it | Not worth it vs. $60 Obi |

**Pick: Obi Fluid.** The per-particle user-data diffusion turns "sauce temperature", "how much tomato is in this sauce", and "color" into simulation state, not just looks.

**Feasibility on an M2 [estimate]:**
- Keep total active fluid at about 5–20k particles on the Burst backend.
- Convert settled, still fluid into a static "pool" mesh with averaged state. That's the classic sleep trick, and it frees particles.

### 3.3 Soft bodies, dough, wraps

- **Obi Softbody: $55** ([reviews page](https://assetstore.unity.com/packages/tools/physics/obi-softbody-130029/reviews)). It's a particle shape-matching soft body. Use it for tomatoes, dough balls, and raw meat jiggle.
  - **Plasticity:** use plastic deformation settings for dough. That's an Obi feature, but parameters are **[unverified]** here.
- **Obi Cloth** (in the suite): tortillas, nori, lettuce leaves, pastry sheets. Folding a wrap is cloth plus attach constraints.
- **True dough** (kneading, stretching, tearing, rolling) is a research-grade MPM problem. Genesis supports elastoplastic MPM ([docs](https://genesis-world.readthedocs.io/en/latest/_sources/user_guide/physics/beyond_rigid_bodies.md.txt)), and a 2026 MLS-MPM cutting simulator exists ([arXiv 2601.06451](https://arxiv.org/abs/2601.06451)). Neither runs in a game loop on an M2. Treat it as a stretch goal: fake it with Obi soft body plus a remesh when "rolled".

### 3.4 Granular (salt, rice, sugar, flour)

- **Obi granular blueprints:** particles behave like rigid spheres, with resolution, density, and size randomness ([Obi manual](https://obi.virtualmethodstudio.com/manual/7.0/emittermaterials.html)). Good for rice and lentils in the hundreds-to-low-thousands range **[estimate]**.
- **Salt, pepper, spices, flour:** don't simulate grains. Spawn a VFX particle burst from the shaker, raycast where it lands, and add *quantity* to the surface's food state (`salt_g += 0.3`), plus a decal or texture splat. This is "fake but true": the state is exact even though individual grains aren't.

### 3.5 Heating, cooking state, browning

Model physical **process variables continuously** and let shaders and behavior read them. This is the difference from Overcooked or Cooking Simulator-style timers. Players in Cooking Simulator report doneness is basically "set the correct time" ([Steam discussion](https://steamcommunity.com/app/641320/discussions/0/3106888980060217285)).

**Heat transfer, kept cheap:**
- Each food piece is a **lumped or two-node thermal model**: surface temperature plus core temperature. Large pieces like a steak or potato use a coarse 1D or voxel conduction model.
- Inputs:
  - **Contact conduction:** pan → food, by contact area × conductance × ΔT.
  - **Convection:** surrounding oil or water particles. Obi user-data temperature gives this for free.
  - **Radiation:** oven, grill.
- Pans have their own thermal mass. The burner heats the pan, and cold food dropped in lowers its temperature. That gives realistic "don't overcrowd the pan" behavior.
- **Moisture:** while surface water remains, surface temperature is pinned near 100 °C (evaporation). That's why boiled meat stays grey. Browning only accelerates once the surface exceeds ~100 °C and dries ([Alimentarium](https://www.alimentarium.org/en/knowledge/maillard-reaction)).

**Reaction progress variables:** integrate rates over time, Arrhenius-like or simple piecewise. Don't use thresholds.

| Variable | Driver | Reference numbers |
|---|---|---|
| `protein_denat` (0–1) | Core temperature over time | Egg white sets ~60–63 °C. Yolk thickens ~65 °C and sets ~70 °C. Ovalbumin ~80 °C. Time matters, not just temperature ([LiveScience](https://www.livescience.com/28575-science-of-cooking-perfect-egg.html), [Anova](https://anovaculinary.com/en-gr/pages/sous-vide-egg-guide)) |
| `maillard` (0–1) | Surface temperature with dryness gate | Fast from ~130–140 °C. Above ~180 °C, pyrolysis/charring takes over ([Alimentarium](https://www.alimentarium.org/en/knowledge/maillard-reaction), [Cooking for Geeks](https://oreilly.com/library/view/cooking-for-geeks/9781449389543/ch04.html)) |
| `caramel` (0–1) | Surface temperature on sugar fraction | Sugar caramelization thresholds **[not sourced here; look up per sugar]** |
| `char` (0–1) | Surface temperature > ~180 °C | Same Alimentarium source |
| `moisture` (kg) | Evaporation with surface temperature ≥ 100 °C | Mass and volume shrink |
| `fat_rendered`, `starch_gelatinized` | Temperature × time with water present | Gelatinization range **[unverified]** |
| `max_temp` | Running max | Same idea as OmniGibson's `MaxTemperature` state ([OmniGibson](https://behavior.stanford.edu/omnigibson/object_states.html)) |

**Rendering:**
- Each food material gets a **"cook map" texture**, RG channels for maillard/char and B for moisture sheen, in texture space or per-vertex.
- Painting into it where hot contact happens gives **sear marks exactly where the food touched the pan**.
- The albedo lerps raw → golden → brown → black.
- Protein denaturation drives opacity and whitening (egg white, fish) and stiffness: soft-body stiffness and Obi viscosity go up as it cooks.

### 3.6 Mixing and emulsions

- **Mixture state:** in a container, track a mixture as phase fractions (oil, water, solids by ingredient).
- **Mixing energy:** accumulate from the stirring/whisking tool's velocity through the contents (Obi particle velocities, or tool speed × immersion).
- **Emulsions** (mayo, vinaigrette, hollandaise): a hidden `emulsion_stability` rises with mixing energy, emulsifier present (egg yolk, mustard), and a gradual oil-addition rate. It decays over time, and with heat for hollandaise. Below a threshold, the fluid renders as split: two Obi fluids with different polarity, which are immiscible by design in Obi. Above it, one opaque fluid.
- **Homogeneity:** compute from the variance of per-particle user-data (e.g. "tomato fraction") across the container. The LLM can then say "lumpy" vs "smooth".

### 3.7 Eggs

1. **Shell:** pre-fracture in Houdini into ~10–30 shards **[estimate]**, held together by breakable joints. A sharp impact on an edge (velocity × virtual grip force) breaks a crack line. Pulling the halves apart with two hands releases the contents.
2. **Contents:** two Obi fluids. White is low-viscosity and clear. Yolk is higher-viscosity, high polarity so it stays a blob, and yellow. Both carry temperature in user data.
3. **Cooking:** as particles pass the denaturation temperature, raise viscosity, then "solidify". Convert the particle cluster into a soft-body or mesh "fried egg" with the same state. This particle→mesh conversion is the standard way to make a liquid "set".
4. **Squeeze too hard while holding:** shell breaks in the hand.

Cooking Mama's Wii egg-cracking minigame was criticized as "overly sensitive" ([GameSpot](https://www.gamespot.com/wii/action/cookingmamacookoff/review.html)). Tune the thresholds with real players.

### 3.8 What Houdini is for

- Pre-fracture eggshells, nut shells, garlic skins, and bread crusts.
- Bake interior textures and volumetric "flesh" for cut faces.
- Generate LODs and convex hulls offline.
- Make vertex-animation-texture (VAT) one-shots: sizzling bubbles, boiling surface.
- Houdini Engine for Unity on Apple Silicon has had problems: a 2022 SideFX thread reports library loading bugs ([SideFX forum](https://www.sidefx.com/forum/topic/83585/)). Current status is **[unverified]**, so export FBX/Alembic/VAT files rather than depending on live Houdini Engine.

### 3.9 Performance budget on M2 / 16 GB [estimates]

| System | Budget |
|---|---|
| Rigid bodies (pieces, tools, containers) | ≤ 300 active, sleeping when still |
| Physics step | 90–120 Hz fixed, to match the 100 Hz glove |
| Obi particles (fluid + granular + soft) | ≤ 15–20k on Burst |
| Zibra Liquid (if used) | GPU-bound; one container at a time |
| Slicing | ≤ 1 slice per frame, queued; pooled pieces |
| RAM | Rokoko Studio + Unity Editor + logger. 16 GB is tight but OK. Avoid running local text-to-3D models (§6.3). |

---

## 4. Prior art: what's real vs faked

| Title | Real | Faked / tricks | Lesson |
|---|---|---|---|
| **Cooking Simulator** (Wastelands Interactive, + VR) | Physics props. "Everything reacts to your hands". Temperature changes. Slicing, grating, fire ([TechRaptor](https://Techraptor.net/games/cooking-simulator), [MobyGames](https://www.mobygames.com/game/127921/cooking-simulator/)) | Doneness is mostly timer-based. Players say visuals can mislead, e.g. chicken that looks raw but is cooked ([Steam](https://steamcommunity.com/app/641320/discussions/0/3106888980060217285)). Recipe-checklist scoring. | Closest commercial analog. Beat it with *continuous* state and visuals that match the state. |
| **Overcooked** | Nothing physical | Discrete item states raw→chopped→cooked→burnt, each per station. Clones model "burnt" as its own item with a waste timer ([Cooperative Cuisine](https://gitlab.ub.uni-bielefeld.de/scs/cocosy/cooperative-cuisine/-/commit/889364ac96dc0840a061529b73c6c7a33a67afcc.patch), [UnderCooked](https://contra.com/community/JOViX4zc-under-cooked-shipped-multiplayer-co-op-cooking)) | A state machine is the anti-pattern for paidia. Useful only as LLM-readable *labels* derived from continuous state. |
| **Cooking Mama** | Gesture recognition | Each step is a scripted minigame (draw lines to chop, rhythm to stir). Motion controls criticized as unreliable ([Wikipedia](https://en.wikipedia.org/wiki/Cooking_Mama_(video_game)), [GameSpot](https://www.gamespot.com/wii/action/cookingmamacookoff/review.html)) | Shows what *not* to do (gesture → canned result). But its feedback ("Mama" grading) is a good model for LLM commentary. |
| **Job Simulator** (Owlchemy) | Rigid physics on almost every object, hand-first | "Tomato presence": hide the hand while holding objects. Objects break out of the hand. Eating = bring food to the mouth zone (the burrito exits the game) ([Owlchemy](https://owlchemylabs.com/tomatopresence), [Game Developer](https://gamedeveloper.com/design/the-burrito-that-wraps-up-excellent-vr-design-fundamentals)) | Generous affordances beat accurate physics for *feel*. Keep them, but log the raw tracking underneath. |
| **Cook-Out: A Sandwich Tale** (Resolution Games) | Hand-held knife chopping, grilling | Ingredients pre-segmented. Timers. Reviewers wanted more techniques: peeling, flipping, roasting ([UploadVR](https://uploadvr.com/cook-out-review/)) | Players want *more verbs*. Paidia needs a broad verb set. |
| **Surgeon Simulator** (Bossa) | Physically simulated tools/organs in Unity | Deliberately clumsy controls as the joke. Players found the humor fades into frustration ([OpenCritic](https://opencritic.com/game/10079/view), [Game Developer](https://gamedeveloper.com/design/designing-controls-for-a-living-piece-of-bread)) | Raw physics-driven hands without grasp assist feel bad. You need §2.3's assist layer. |
| **Totally Accurate Battle Simulator** (Landfall) | Active ragdolls that balance themselves | Exaggerated, unstable physics for comedy ([guide](https://earlyguides.com/totally-accurate-battle-simulator)) | Emergent physics is fun, but realism is your pillar, so tune stability up. |
| **BEHAVIOR-1K / OmniGibson** | Rigid + particle "systems" (fluids, powders) on Isaac Sim | Cooking is **transition rules**. Heat sources have `temperature` and `heating_rate`. `Cooked` and `Burnt` fire when `MaxTemperature` crosses thresholds. Recipe rules turn inputs into outputs, e.g. dough+egg+sesame in oven → bagel ([transition rules](https://behavior.stanford.edu/omnigibson/transition_rules.html), [object states](https://behavior.stanford.edu/omnigibson/object_states.html)) | Best academic reference for a *state vocabulary*: Temperature, MaxTemperature, Cooked, Burnt, Frozen, Heated, OnFire, Filled, Covered, Saturated, Contains… Its recipe rules are less free than you want. |
| **iGibson 2.0** | Predecessor of OmniGibson, with extended object states (temperature, cooked, sliced, soaked) | Same rule approach. iGibson-specific docs **[unverified]**; I only reached OmniGibson docs. | — |
| **RoboCasa** | MuJoCo rigid physics, large kitchen asset library | Cooking tasks are pick/place/turn-knob. No thermal model ([paper](https://arxiv.org/html/2406.02523v1)) | Reuse its kitchen assets and task taxonomy for the robot side |
| **VirtualHome** (Unity) | Animated agent | Activities are symbolic **programs**: sequences of (action, object) steps ([arXiv 1806.07011](https://arxiv.org/pdf/1806.07011)) | Good format for auto-segmenting your logs into "action programs" |
| **DexCap** (Stanford, RSS 2024) | Real human mocap glove + wrist SLAM → robot policies | Retargeting via fingertip IK. No force data. 40-min sessions ([arXiv](https://arxiv.org/pdf/2403.07788)) | Your data pipeline's closest blueprint |

---

## 5. A generic "state of food" data model

Goal: **any dish is possible, with no recipe list.** The world only knows matter, geometry, and processes. "Dish" is an interpretation made at the end, by an LLM.

### 5.1 Entities

```
FoodEntity            // one physical piece, particle cluster, or pooled liquid
  id, parent_ids[]    // provenance DAG: produced by cutting / mixing / setting
  species             // "tomato", "egg_white", "wheat_dough": base ingredient(s)
  composition         // mass fractions: water, protein, fat, sugar, starch, fiber, salt, acid, ...
  mass_g, volume_ml, surface_area_cm2
  form                // whole | piece | slice | dice | mince | puree | liquid | granular | gel | foam | emulsion
  structure           // integrity 0..1, crushed 0..1, cut_faces count, skin_on bool
  thermal             // T_surface, T_core, T_max_surface, T_max_core
  progress            // protein_denat, maillard, caramel, char, gelatinized, fat_rendered (each 0..1)
  moisture_loss_g
  seasoning           // {salt_g, sugar_g, pepper_g, acid_g, ...}  (added, not intrinsic)
  contents_of         // container id, if inside one
  history[]           // compact event log: {t, op:"slice", tool:"knife_01", params}
```

```
Mixture (inside a Container)
  components[]        // FoodEntity ids or fractions by species
  phases              // oil / aqueous / solid fractions
  homogeneity 0..1, emulsion_stability 0..1, viscosity
```

### 5.2 Why this model

- **Ingredient definitions are data, not code.** An `IngredientDef` holds species, composition, density, thermal conductivity, soft-body stiffness, mesh and interior texture, and denaturation/browning parameters. Adding "eggplant" means writing a JSON entry, and an LLM can draft it.
- **Processes are generic functions over properties**, not recipes:
  - `cut(entity, plane)` splits mass, volume, and state proportionally and adds cut faces.
  - `heat(entity, dt)` updates temperatures and progress.
  - `mix(container, energy)` updates homogeneity and emulsion stability.
  - `combine(a, b)` creates a Mixture or a composite (a sandwich is a stack of entities joined by contact plus an "assembled" relation).
- **Names come from state.** The world doesn't need to know it's an omelette. Labels like "cooked", "burnt", "fried egg", or "caramelized onion" can be derived on demand from the numbers, through a deterministic rule table for UI and through an LLM for open-ended naming.
- **Grounding vocabulary:** OmniGibson's state names are a good compatible label set (above). FoodOn gives standard food/process term IDs, e.g. heating processes, "has ingredient", "derives from" ([Frontiers 2023](https://www.frontiersin.org/journals/artificial-intelligence/articles/10.3389/frai.2023.1137961/pdf), [FoodOn talk](https://ease-crc.org/?p=28505)). FoodOn can't pre-coordinate every "base + process + state" combo, so represent food as base + modifiers. That's exactly this model ([CEUR 2025](https://ceur-ws.org/Vol-4293/paper22.pdf)).

### 5.3 LLM interpretation of the final dish

When the player "plates" (puts food on a plate and rings a bell, or says "done"):

1. **Serialize** the plate's entities into compact JSON: each entity's species, form, mass, thermal maxima, progress variables, seasoning, and the provenance DAG collapsed into a short process story ("tomato → sliced 6× → pan, 3 min at 190 °C surface → mixed with egg").
2. **Add 1–3 screenshots** of the plate from a fixed camera.
3. **Ask an LLM** (Claude, as in `../tech-stack.md`) for structured output: dish name, cuisine guess, a doneness and safety assessment, a taste/texture critique grounded in the numbers, and suggestions. A safety example: chicken core `T_max` below 74 °C should be flagged. That's the USDA figure; I didn't fetch a source in this pass **[unverified]**.
4. **Guardrail:** tell the model to reason only from provided state. It must not invent ingredients that aren't in the JSON.

The same JSON doubles as **language annotation for robot data**: episode-level task descriptions such as "make a tomato omelette" come for free.

---

## 6. Recommended architecture

### 6.1 Diagram

```
 Rokoko Smartgloves (100 Hz IMU fingers)      Wrist tracker (Coil Pro / other)
              \                                   /
               Rokoko Studio (Mac, paid live-stream plan)
                    |  UDP 14043 (JSON / LZ4)
        +-----------+-----------------------------+
        |                                         |
  Python receiver (already in repo)      Unity 6 (Apple Silicon, Metal)
  raw ground-truth log, .jsonl           ├─ RokokoInput → XRHands provider (+ calibration, filtering)
                                         ├─ PhysicsHand (ArticulationBody chain, PD to targets)
                                         ├─ GraspAssist (contacts + curl hysteresis + virtual force)
                                         ├─ Food systems: Slicer (EzySlice/DMC), Obi Fluid/Granular/Softbody,
                                         │    Thermal + Progress integrator, CookMap shader, Mixtures
                                         ├─ ToolFactory (LLM tool specs → parametric tools)
                                         ├─ Recorder (sim-side log, 100 Hz) ──► episodes/
                                         └─ Plating → LLM judge (Claude API)
                                                   |
                         Offline pipeline (Python, Mac)
                         ├─ convert → LeRobot v3 dataset (Parquet + MP4)
                         ├─ dex-retargeting → Allegro / LEAP / Shadow joint trajectories
                         └─ MuJoCo replay (RoboCasa assets) for physical feasibility checks
```

### 6.2 Data logging, the robotics pillar

Log **at least** the following per step, at the glove's 100 Hz:

- **Raw glove packets:** keep them, with timestamps. The repo's `receiver.py` already does this.
- **Hand state:**
  - Per hand: 15 finger joint rotations plus wrist 6-DoF pose. That maps to MANO's 15 joints + global orient, a 45-D pose ([MANO summary](https://www.emergentmind.com/topics/mano)).
  - 21 keypoint positions, which are what dex-retargeting consumes.
  - Curl values.
- **Physical hand state:** where the sim hand actually was, plus per-finger contact flags and the virtual force from §2.3.
- **Object state:** 6-DoF pose, velocity, and id of every object. Food-state deltas go in an event stream.
- **Events:** grasp start/end, slice (plane, tool), pour, add-seasoning, heat changes, tool generation.
- **Video:** 1–3 fixed cameras at reduced resolution for visuomotor policies.
- **Language:** player prompts, LLM tool requests, and the final dish JSON.

**Output format:** LeRobot v3 packs many episodes into shared Parquet (tabular) and MP4 (per-camera) files, with metadata in `meta/` ([LeRobot docs](https://huggingface.co/docs/lerobot/en/lerobot-dataset-v3)). RoboCasa datasets already appear on the Hub in this format ([example](https://huggingface.co/datasets/Whalswp/robocasa_pretrain_human300_v3)).

**Retargeting:** dex-retargeting handles human→robot hand mapping. Handle joint order by name, not index ([repo](https://github.com/dexsuite/dex-retargeting)). `dexi-rs` ships configs for Allegro, Shadow, LEAP, Inspire and others ([PyPI](https://pypi.org/project/dexi-rs/)).

**Important caveat:** a sim-only recording teaches *human intent and kinematics*, not robot-feasible forces. The value is the kinematic trajectories, contact timing, and task structure. Physical validity comes from MuJoCo replay or real-robot fine-tuning.

### 6.3 "Unlimited tools" generated at runtime

Avoid generating executable code at runtime. Roslyn runtime compilation exists for Unity, but forum consensus calls it arduous and error-prone. The commercial asset's "security check" is a vendor claim ([Unity forum](https://discussions.unity.com/t/use-of-runtime-compiled-code/860333), [Roslyn C# asset](https://assetstore.unity.com/packages/tools/integration/roslyn-c-runtime-compiler-142753)). Instead:

1. **The LLM returns a ToolSpec JSON** in a fixed schema:
   - Parts: primitives or procedural parts such as handle, blade(edge length, sharpness), bowl(radius, depth), tines(n), mesh/grid, spout, hinge.
   - Physics: dimensions, materials (density, friction, thermal conductivity).
   - **Affordances** from a fixed library: `cuts`, `scoops`, `contains`, `presses`, `grates`, `whisks`, `heats(T)`, `pierces`, `rolls`, `squeezes`, `sieves`.
2. **ToolFactory assembles it procedurally** in Unity. A compound collider is built from the primitives, so no runtime convex decomposition is needed. Each affordance attaches an existing, tested behavior component, e.g. "blade" turns on the slicer for that edge. This gives effectively unlimited tools from a finite verb library, and every tool's behavior stays physically grounded and loggable.
3. **Optional visual upgrade:** send the same description to a text-to-3D API.
   - Rough prices are **[unverified, secondary sources]** ([Sloyd comparison](https://sloyd.ai/blog/3d-ai-price-comparison)): Tripo ~$0.20–0.30 per textured model, Meshy ~$0.40–0.60.
   - Generation takes tens of seconds to ~1–2 minutes **[vendor claims]**.
   - Swap the generated mesh in as visuals only, keeping the primitive colliders.
   - Local TRELLIS.2 runs on Apple Silicon but reportedly needs **≥24 GB unified memory** and ~3.5 min per mesh on an M4 Pro ([korben.info](https://korben.info/en/trellis-mac-microsoft-image-to-3d-apple-silicon.html), [HN](https://hn.svelte.dev/item/47828896)). **That won't fit on a 16 GB M2** alongside Unity and Rokoko Studio, so use a cloud API.
4. If a request needs a verb that doesn't exist yet ("a tool that smokes food"), the LLM says which affordance is missing. That becomes your development backlog.

---

## 7. MVP slice vs stretch goals

### 7.1 MVP: "one counter, one pan"

| Area | Scope |
|---|---|
| Space | One counter with a cutting board, one induction burner (heat dial), one pan, one bowl, one plate. Fixed desktop camera. |
| Tools | Chef's knife, spatula, fork/whisk (3 tools) |
| Ingredients (5) | **Tomato** (slice, dice, squash; juice as small Obi fluid), **onion** (layered convex slicing, sweat → brown via Maillard), **egg** (crack, two-fluid contents, coagulation → fried/scrambled), **butter** (solid → melts into Obi fat fluid that conducts heat), **bread slice** (rigid with cook map → toast, char) |
| Systems | Rokoko→XRHands provider, physics hand + grasp assist, plane slicing with interior caps, thermal two-node model + progress variables + cook-map shader, Obi Fluid for egg/juice/butter, seasoning by quantity (salt shaker), plating → Claude dish judge |
| Data | Recorder → LeRobot v3 converter, one retarget demo (Allegro or LEAP) replayed in MuJoCo |
| Shows | Freedom: tomato-egg scramble, onion-butter toast, burnt toast, raw-egg tomato salad, all *without recipes*. Realism: sear marks where contact happened, egg white setting before yolk, butter browning. |
| Rough effort [estimate] | 8–12 weeks for one experienced Unity developer |

**Assets to buy:**
- Obi Fluid **$60**
- optionally Obi Softbody **$55** or the Obi Physics Suite **$179**
- optionally Auto Hand **$90**

**Total: about $60–$270.** Plus the Rokoko Studio plan for live streaming.

### 7.2 Stretch goals, roughly in order

1. Boiling water and pots (convection via Obi temperature channel), pasta/rice (granular → gelatinized), sauces and emulsions (§3.6).
2. Runtime tool generation (§6.3) with the affordance library, then text-to-3D visuals.
3. Soft-body produce and meat (Obi Softbody), wraps (Obi Cloth), plastic dough approximation.
4. Oven/grill radiation, oil frying, smoke/steam VFX, fire (Cooking Simulator players love fire).
5. A bigger kitchen built from RoboCasa assets so the same scenes exist in MuJoCo for robot replay.
6. VR on a Windows PC/Quest. The same Unity project carries over and gives head pose and true depth.
7. Offline high-fidelity re-simulation of selected episodes in Genesis (MPM dough/sauces) or Isaac Lab on a rented RTX box.
8. Haptics (vibrotactile on contact) to reduce the "fingers through object" problem and improve force-proxy data quality.

---

## 8. Open risks

- **Wrist tracking quality** decides everything. Hand drift breaks both play and data.
- **Grasp assist vs data purity.** Assists make play feel good but change what the hand "did". Log the raw tracked hand *and* the assisted physical hand, plus a flag for when assist was active.
- **Asset compatibility with Unity 6.** Zibra says Unity 6 isn't fully supported. Obi's Metal GPU backend is untested. Prototype each on the M2 in week 1 before committing.
- **Paid Rokoko Studio plan** is needed for live streaming.
- **16 GB RAM** is tight with Studio + Unity Editor + Obi + the logger. Keep heavy ML (text-to-3D, retargeting batches) off the Mac or offline.

---

## Recommendation

1. **Engine:** build the game in **Unity 6 on the M2**, with a desktop camera view. Don't use Isaac Sim: it has no macOS support and needs an RTX 4080+. Don't build the game in MuJoCo, Genesis, or ManiSkill either; they aren't game engines, and ManiSkill and Genesis GPU paths are weak or unverified on Mac. Use **MuJoCo (native on Mac) plus dex-retargeting** as the offline robotics back end, and keep the RoboCasa kitchens as the shared world for robot replay.
2. **Hands:** write a **Rokoko → XR Hands provider** and a **custom ArticulationBody physics hand** that tracks the glove through PD drives. Add a grasp-assist layer: opposed contacts, curl hysteresis, a virtual grip force from tracked-vs-physical finger penetration, and breakable attach joints. Keep Auto Hand ($90) as a fallback.
3. **Food:**
   - EzySlice-style plane slicing with interior caps and piece budgets.
   - **Obi Fluid ($60)**, whose per-particle temperature and color channels give liquids, egg contents, melted fat, and granular foods real cooking state.
   - A **continuous thermal + reaction-progress model** (denaturation, Maillard, char, moisture) driving a cook-map shader.
   - Houdini for pre-fractured shells and baked interiors, exported as files.
4. **State model:** use property-based FoodEntities with a provenance DAG and *no recipes*. Labels and dish names are derived at the end. Claude judges the plated dish from the state JSON plus a screenshot, and that same JSON labels the robot episodes.
5. **Unlimited tools:** have the LLM return a ToolSpec JSON from a fixed affordance library and assemble it procedurally with primitive colliders. Use cloud text-to-3D only for visuals; local models don't fit in 16 GB. Never compile LLM code at runtime.
6. **Ship the MVP first:** knife, board, pan, 5 ingredients (tomato, onion, egg, butter, bread). Log to **LeRobot v3** from day one and prove one retarget-and-replay in MuJoCo. Spend week 1 de-risking Obi and the slicer on Metal under Unity 6 before building anything else.
