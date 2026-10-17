# Making "Let's Cook!" Produce Useful Robot-Learning Data

Research compiled 2026-10-17. Setup assumed: Rokoko Smartgloves (7 sensors/glove), a separate wrist position tracker, a free-play cooking game (Unity likely, MuJoCo possible), and an Apple M2 Mac with 16 GB of RAM.

Conventions: each non-obvious claim links its source. **[UNVERIFIED]** marks claims I could not confirm from a primary source. **[OPINION]** marks my own recommendation or inference.

---

## 0. TL;DR

- Record in the **LeRobotDataset v3** format (Parquet + MP4 + `meta/`). It is native to the Hugging Face Hub, it streams, and RoboCasa365 data already ships in it. Keep a lossless raw log next to it (MCAP or JSONL) so you can re-export later.
- Store the hand in **three layers**: (a) raw Rokoko local joint quaternions, (b) 21 MANO-ordered 3D keypoints per hand plus a 6-DoF wrist pose in a fixed world frame, (c) optional MANO parameters fitted offline. Layer (b) is what dex-retargeting, GeoRT, EgoVerse and the PI/EgoMimic-style "human as embodiment" pipelines consume.
- Render a **head-mounted egocentric RGB camera** from the game at 30 fps (plus one fixed third-person view). Every major human-to-robot effort is built around egocentric video (EgoDex, EgoMimic, EgoVerse, PI, Figure).
- Log the **game's ground-truth state**: object 6-DoF poses, object semantic states (sliced, cooked, on/off), contacts, and grasp events. Then add **language task labels, subtask segments and success labels**. This is where a game beats real-world video.
- Flag anything the game "assisted" (snap-grasps, magnetic pickups, teleporting). Assisted motion is not physically valid demonstration data.
- Run consent at the door, publish under **CC BY 4.0**, and keep the MANO-derived files in a separate non-commercial subset because of MANO's license.

---

## 1. Data formats researchers actually use

### 1.1 LeRobotDataset v3.0 (Hugging Face) — **recommended**

- **Layout.** Tabular signals (states, actions, timestamps) are stored in Apache Parquet. Camera frames are encoded to MP4 and sharded per camera. Metadata describes the schema, fps, normalization stats and episode segmentation. ([LeRobot v3 docs](https://huggingface.co/docs/lerobot/lerobot-dataset-v3))
  - `meta/info.json`: features (shapes, dtypes), fps, codebase version, path templates.
  - `meta/stats.json`: mean, std, min and max per feature.
  - `meta/tasks.*`: natural-language tasks mapped to integer IDs. The docs say `tasks.jsonl`, but some third-party docs say `tasks.parquet` **[UNVERIFIED which is current; check your installed version]**.
  - `meta/episodes/`: per-episode records (length, tasks, offsets) as chunked Parquet.
  - `data/chunk-XXX/file-XXX.parquet` and `videos/<video_key>/chunk-XXX/file-XXX.mp4`. Each file holds many episodes. ([LeRobot v3 docs](https://huggingface.co/docs/lerobot/lerobot-dataset-v3); [example info.json on the Hub](https://huggingface.co/datasets/nvti/tihado_mission_1))
- **What's new in v3.** v2.1 stored one file per episode. v3 concatenates episodes into shards, and metadata moved from JSONL to Parquet. ([porting guide](https://huggingface.co/docs/lerobot/porting_datasets_v3)) v3 ships in `lerobot >= 0.4.0` ([docs](https://huggingface.co/docs/lerobot/lerobot-dataset-v3)).
- **Naming conventions.** `observation.state`, `action` and `observation.images.<camera>`. Per-frame bookkeeping columns are `timestamp`, `frame_index`, `episode_index`, `index` and `task_index`. ([docs sample output](https://huggingface.co/docs/lerobot/lerobot-dataset-v3))
- **Writing.** Call `LeRobotDataset.create(...)`, then `add_frame(frame)` for each frame and `save_episode()` per episode. You **must** call `finalize()` before `push_to_hub()` or the Parquet files will be corrupt. ([docs, "Common issues"](https://huggingface.co/docs/lerobot/lerobot-dataset-v3))
- **Streaming.** `StreamingLeRobotDataset(repo_id)` trains straight from the Hub ([docs](https://huggingface.co/docs/lerobot/lerobot-dataset-v3)).
- **Precedent for human-hand data in this format.** There is a LeRobot-tagged Manus-glove egocentric sample dataset ([OpenGraphLabs manus-egocentric-sample](https://huggingface.co/datasets/OpenGraphLabs-Research/manus-egocentric-sample/blob/main/meta/tasks.jsonl)) and a glove-plus-LEAP-retargeted human demo dataset ([Oceanveo/human-dex](https://huggingface.co/datasets/Oceanveo/human-dex)).
- **Frame rates in practice.** RoboCasa365 in LeRobot runs at 20 fps ([community v3 conversion](https://huggingface.co/datasets/Whalswp/robocasa_pretrain_human300_v3)). EgoDex records at 30 Hz ([EgoDex repo](https://github.com/apple/ml-egodex)). SO-101 recording examples use 30 fps cameras ([docs](https://huggingface.co/docs/lerobot/lerobot-dataset-v3)).

### 1.2 RLDS / Open X-Embodiment

- RLDS is TFDS-based. A dataset is a collection of episodes, and each episode has `steps`. Each step holds `observation`, `action`, `reward`, `is_first`, `is_last` and `is_terminal`, and usually a step-level `language_instruction`. ([TFDS catalog example](https://www.tensorflow.org/datasets/catalog/io_ai_tech?hl=fa); [secondary guide](https://claru.ai/formats/rlds))
- Open X-Embodiment uses RLDS. Its code is Apache-2.0 and its "other materials" are CC BY 4.0, but each constituent dataset has its own terms. ([OXE GitHub](https://github.com/google-deepmind/open_x_embodiment); [analysis](https://particula.tech/blog/open-x-embodiment-dataset-licence-commercial-use))
- [OPINION] Treat RLDS as an export target only. It is TensorFlow-centric, and the field (PI's openpi, GR00T, SmolVLA) has largely moved to LeRobot. You can convert LeRobot to RLDS later if a collaborator asks for it.

### 1.3 HDF5 (robomimic / robosuite / RoboCasa / LIBERO)

- **robomimic layout.** `data` has attributes `total` and `env_args` (JSON with `env_name`, `env_type`, `env_kwargs`). Each `data/demo_N/` contains `states` (flattened MuJoCo state), `actions`, `rewards`, `dones`, `obs/<key>` and `next_obs/<key>`, plus the attributes `num_samples` and `model_file` (MJCF XML). A `mask/` group defines splits. Images are uint8 HWC and actions are normalized to [-1, 1]. ([robomimic dataset docs](https://robomimic.github.io/docs/datasets/overview.html))
- This format matters if you want to **replay** demos inside MuJoCo or feed MimicGen/DexMimicGen, because those need simulator states.
- EgoDex also uses HDF5 (per-frame 4×4 SE(3) transforms for every joint) paired with MP4 ([EgoDex repo](https://github.com/apple/ml-egodex)).

### 1.4 MCAP / ROS bags

- MCAP has been the default rosbag2 storage format since ROS 2 Iron (May 2023). It is self-describing (schemas are embedded), serialization-agnostic (ROS, Protobuf, JSON, FlatBuffers), indexed by time and topic, and append-only, so data survives a crash. ([Foxglove: MCAP as ROS 2 default](https://foxglove.dev/blog/mcap-as-the-ros2-default-bag-format); [MCAP intro](https://foxglove.dev/blog/introducing-the-mcap-file-format))
- [OPINION] MCAP is a good *raw capture* format for the event: write Rokoko packets, tracker packets and game-state JSON into one MCAP per session, then use a converter to produce LeRobot episodes. You can inspect sessions in Foxglove.

### 1.5 Recommendation

| Use | Format |
|---|---|
| Raw capture at event (lossless, crash-safe, all rates) | MCAP with JSON or Protobuf channels (or JSONL with monotonic timestamps if you want something simpler) |
| **Primary published dataset** | **LeRobotDataset v3, 30 fps**, egocentric + third-person MP4 |
| Sim replay / MimicGen augmentation (only if you rebuild scenes in MuJoCo) | robomimic-style HDF5 with `states` + `model_file` |
| Requested by a partner | RLDS export via converter |

---

## 2. Human hand–object datasets to learn from and align with

### 2.1 Comparison table

| Dataset | Scale | Hand representation | Cooking relevance | License |
|---|---|---|---|---|
| **EPIC-KITCHENS-100** | 100 h, 45 kitchens, 37 participants, ~90K action segments, free-form narrations ([Springer](https://link.springer.com/article/10.1007/s11263-021-01531-2); [readme](https://www.huggingface.co/datasets/awsaf49/epic_kitchens_100/blob/main/P25/readme.txt)) | No 3D hand GT (video + verb/noun labels) | Very high: the canonical verb/noun kitchen taxonomy | CC BY-NC 4.0 ([readme](https://www.huggingface.co/datasets/awsaf49/epic_kitchens_100/blob/main/P25/readme.txt)) |
| **Ego-Exo4D** | Cooking ≈ 564 h / 678 takes ([aiwiki, secondary](https://aiwiki.ai/wiki/ego_exo4d)) **[UNVERIFIED]**. 664 keysteps across 17 activities; manual 3D hand pose on 458 takes ([paper](https://arxiv.org/pdf/2311.18259)) | 3D hand keypoints (21-joint style) | Very high: keystep taxonomy for recipes | Per-partner license agreement ([Meta](https://ai.facebook.com/blog/ego-exo4d-video-learning-perception/)); terms **[UNVERIFIED]** |
| **HOI4D** | 2.4M RGB-D frames, 4000 seqs, 800 objects, 16 categories, 610 rooms ([arXiv](https://arxiv.org/abs/2203.01577)) | 3D hand pose (MANO-based per the paper) **[UNVERIFIED in search]** | Medium: category-level object pose, articulated objects | See site |
| **ARCTIC** | 2.1M frames, bimanual, articulated objects, 8 allocentric + 1 egocentric cam ([site](https://arctic.is.tue.mpg.de/)) | MANO + SMPL-X, dynamic contact | Medium: articulated tools (scissors-like) | MPI research license |
| **OakInk2** | Bimanual, multi-step "complex tasks" built from Affordance → Primitive Task → Complex Task ([arXiv](https://arxiv.org/abs/2403.19417)) | MANO | High: its hierarchy maps well to recipe → step → primitive | See oakink.net/v2 |
| **DexYCB** | 8 RGB-D cams, YCB object grasps, handover task ([CVPR'21](https://openaccess.thecvf.com/content/CVPR2021/html/Chao_DexYCB_A_Benchmark_for_Capturing_Hand_Grasping_of_Objects_CVPR_2021_paper.html)) | MANO pose + shape β | Low to medium (grasping) | See site |
| **GRAB** | 10 subjects, 51 objects, 1.62M frames, per-vertex contact ([MPI](https://ps.is.mpg.de/code/grab-a-dataset-of-whole-body-human-grasping-of-objects)) | SMPL-X body with MANO hands | Low to medium | MPI PS:License 1.0 (non-commercial) |
| **HOT3D** (Meta) | 425 recordings (Aria + Quest 3), egocentric multi-view ([arXiv](https://arxiv.org/abs/2411.19167)) | **MANO and UmeTrack** from scanned hands | Medium (kitchen objects among the 33) | Sequences CC BY-SA; **hand data CC BY-NC-SA** ([license](https://projectaria.com/datasets/hot3D/license)) |
| **TACO** | 2.5K seqs (244 pre-released), 12 allocentric + 1 egocentric view, <tool, action, object> triplets ([arXiv](https://arxiv.org/abs/2401.08399); [GitHub](https://github.com/Satorinal/TACO)) | MANO meshes | **Very high**: tool use (knives, spatulas, bowls) | See site |
| **GigaHands** (CVPR'25) | 34 h, 56 subjects, 417 objects, 14K clips, 183M frames, 84K text annotations ([arXiv](https://arxiv.org/abs/2412.04244)) | MANO meshes | High: text-annotated bimanual activities | Release status of some annotations **[UNVERIFIED]** ([project](https://ivl.cs.brown.edu/research/gigahands.html)) |
| **EgoDex** (Apple, ICLR'26) | 829 h, 90M frames, 338K demos, 194 tabletop tasks, 30 Hz 1080p, Vision Pro ([arXiv](https://arxiv.org/abs/2505.11709)) | **SE(3) 4×4 per joint** (~68 joints incl. both hands, upper body, camera), confidences; LLM task descriptions ([repo](https://github.com/apple/ml-egodex)) | High | **CC BY-NC-ND** ([repo](https://github.com/apple/ml-egodex)) |
| **EgoVerse** (RSS'26) | 1,362 h, 80K episodes, 1,965 tasks, 240 scenes, 2,087 demonstrators ([arXiv](https://arxiv.org/abs/2604.07607); [summary](https://www.labellerr.com/blog/egoverse-egocentric-robot-learning-dataset/)) | **21 keypoints/hand in camera frame + 6-DoF head pose** ([paper](https://arxiv.org/pdf/2604.07607)) | High, explicitly built for robot learning | **[UNVERIFIED]** |
| **World In Your Hands (WIYH)** | >1000 h in-the-wild, mm-level 3D motion ([arXiv](https://arxiv.org/html/2512.24310)) | 3D hand motion | Medium | **[UNVERIFIED]** |

### 2.2 Human-video-to-robot efforts (why your data matters)

- **EgoMimic** (Georgia Tech/Stanford, 2024). Project Aria egocentric video + 3D hand tracking is treated as a co-equal embodiment and co-trained with robot data. Reported gains are 34–228%, and the authors report that one extra hour of hand data is worth more than one extra hour of robot data. ([arXiv](https://arxiv.org/abs/2410.24221); [Meta blog](https://ai.meta.com/blog/egomimic-project-aria-georgia-tech-ego4d-robotics-embodied-ai/))
- **Humanoid Policy ~ Human Policy (HAT / PH2D).** About 27K VR-headset human demos versus about 1.5K robot demos. It uses a shared human/humanoid state-action space that is differentiably retargeted. ([arXiv](https://arxiv.org/abs/2503.13441); [project](https://human-as-robot.github.io/))
- **DexMV** (ECCV'22). Estimates 3D hand and object pose from video and retargets it to a simulated dexterous hand for imitation learning. ([arXiv](https://arxiv.org/abs/2108.05877))
- **DexCap** (Stanford, RSS'24). **Rokoko gloves** for fingers (EMF fingertip sensing that is robust to occlusion) plus a RealSense T265 SLAM camera on each glove for wrist pose, retargeted to a LEAP hand by fingertip IK with the pinky dropped. ([arXiv](https://arxiv.org/abs/2403.07788)) This is the closest precedent to your rig.
- **DexWild** (CMU, RSS'25). Mocap glove + palm cameras + ArUco wrist tracking. 9,290 demos across 93 environments. Co-training gave 68.5% success in unseen environments, about 4× robot-only. ([arXiv](https://arxiv.org/abs/2505.07813))
- **Physical Intelligence** (Dec 16 2025). Egocentric human video is treated as another embodiment with actions = **3D hand positions**. Co-fine-tuning π0.5 roughly **doubled** performance on scenarios that appeared only in the human data, and the benefit grows with robot pretraining scale ("emergent transfer"). ([PI research post](https://pi.website/research/human_to_robot))
- **Figure "Project Go-Big"** (Sep 2025). Figure claims Helix learned navigation from egocentric human video alone and is collecting in Brookfield homes. These are Figure's claims, not independently verified. ([Figure](https://www.figure.ai/news/project-go-big); [coverage](https://humanoidroboticstechnology.com/industry-news/figure-announces-project-go-big-internet-scale-humanoid-pretraining-and-direct-human-to-robot-transfer/))
- **Tesla Optimus** (mid-2025). Reportedly shifted from mocap suits and teleop toward video of humans with multi-camera helmet rigs. Insider-sourced. ([eWeek](https://www.eweek.com/news/tesla-optimus-robot-training/)) **[UNVERIFIED by Tesla]**

**What this tells you [OPINION].** The field's "action" for humans is converging on **wrist 6-DoF + 3D fingertip/keypoint positions in a consistent frame, synchronized with egocentric RGB**. Glove data is *better* than vision-estimated hands on occlusion; DexCap and GeoRT's authors explicitly prefer gloves ([GeoRT README](https://github.com/facebookresearch/GeoRT)). Your weakest link is the scene: game video is synthetic, so lean on its strengths (ground-truth state, labels) rather than pretending it is real footage.

### 2.3 Exporting Rokoko glove data to MANO

**What Rokoko gives you**
- **Studio Custom Streaming (JSON, optionally LZ4) over UDP.** Default port is 14043 per a community guide. You get a flat `joints` list with parent indices, and each joint has `position` + `rotation` (quaternion). Each finger has Metacarpal/Proximal/Medial/Distal/Tip joints; the thumb has no Medial. Match joints by **name**, because array order isn't guaranteed. ([Rokoko Custom Streaming](https://support.rokoko.com/hc/en-us/articles/4410416376977-Custom-Streaming); [JSON sample](https://support.rokoko.com/hc/en-us/article_attachments/50030847458577))
- **Output rate.** Smartgloves (v1) output 100 fps. **Smartgloves II output about 83.33 Hz** (IMU 250 Hz, EMF 96 kHz), with 7 sensors per glove. ([Knox Labs SG II](https://www.knoxlabs.com/products/smartgloves-ii-size-m); [Rokoko](https://rokoko.com/products/smartgloves))
- **Absolute position.** Gloves alone are inertial plus EMF relative to the glove hub. Absolute global position needs the Coil Pro, which has a 2 m (or 5 m, listings disagree) radius, is sensitive to ferrous metal, and costs about $2,995. ([Rokoko store](https://us.store.rokoko.com/products/coil-pro); [Knox Labs](https://www.knoxlabs.com/products/coil-pro)) Your separate wrist tracker fills this role. Calibrate its offset to the Rokoko hand root.
- Rokoko advertises export to BVH/FBX/CSV and ROS/MuJoCo integrations (vendor claim) ([Rokoko robotics](https://www.rokoko.com/use-cases/robotics)).

**MANO basics**
- MANO pose has 16 joints: global orient + 15 finger joints, each 3 axis-angle values. Shape β is 10-D. It outputs 778 vertices and 16 joints; the 5 fingertips are added from vertices to make the common 21-keypoint set. (Standard MANO convention; see the [MANO site](https://mano.is.tue.mpg.de/).) **[VERIFY joint order against `smplx`/`manopth`; MANO's internal order is index, middle, pinky, ring, thumb, not anatomical order]**
- **License.** MANO is non-commercial research only and may not be used in commercial products or services ([MANO license](https://mano.is.tue.mpg.de/license.html)). HOT3D likewise puts its MANO files under NC terms ([HOT3D license](https://projectaria.com/datasets/hot3D/license)).

**Conversion recipe [OPINION, standard practice]**
1. **Calibrate per player.** Record 2 s of flat-hand T-pose and a fist. Measure or ask for hand length and width (or estimate it from the Rokoko skeleton). This gives the rest-pose offset per joint and lets you fit β.
2. **Build keypoints.** Run forward kinematics on Rokoko joint rotations with a scaled skeleton → 21 keypoints in **wrist frame**, then transform to world using the wrist tracker. Use the [EgoVerse 21-keypoint convention](https://arxiv.org/pdf/2604.07607) (wrist, then 4 per finger from thumb to pinky, MediaPipe/OpenPose order).
3. **Direct rotation mapping (fast).** Express each Rokoko finger joint's local rotation relative to the calibrated flat pose, change basis into MANO's joint frames (computed once from MANO's rest-pose joint axes with `flat_hand_mean=True`), and convert to axis-angle. Rokoko's "Metacarpal" joints have no MANO counterpart: fold them into the MANO root or the first joint.
4. **Optimization fit (accurate).** Minimize ‖MANO_joints(θ, β) − keypoints‖² + λ‖θ − θ_direct‖² + temporal smoothness, using PyTorch on MPS/CPU with `smplx` or `manopth`. Each frame is tiny, so batch a whole episode.
5. **Store both.** Keep keypoints (license-clean) and MANO θ/β (NC subset), plus a per-frame fit residual for quality filtering.

---

## 3. Retargeting human hands to robot hands

### 3.1 dex-retargeting (AnyTeleop) — default choice

- **License: MIT.** `pip install dex_retargeting`. Requires Python ≥3.7,<3.13. Core deps are `numpy>=2`, `pytransform3d`, `pin` (Pinocchio), `nlopt`, `anytree`, `pyyaml` and `lxml`. The `[example]` extra adds `sapien==3.0.0b0`, `mediapipe` and `opencv`. ([GitHub](https://github.com/dexsuite/dex-retargeting); [pyproject](https://raw.githubusercontent.com/dexsuite/dex-retargeting/main/pyproject.toml))
- **Optimizers.** Vector retargeting (for teleop/video), position retargeting (for hand-object pose datasets, which matches your offline case) and DexPilot ([GitHub](https://github.com/dexsuite/dex-retargeting)).
- **Shipped teleop configs.** Ability, Allegro, Inspire, LEAP, Schunk SVH and Shadow (left and right, normal + DexPilot), plus the Panda gripper ([configs dir](https://github.com/dexsuite/dex-retargeting/tree/main/src/dex_retargeting/configs/teleop)).
- **URDFs** come from [dex-urdf](https://github.com/dexsuite/dex-urdf), and each URDF has its own license: Allegro BSD, **Shadow GPL-3.0**, SVH Apache-2.0, LEAP MIT, **Inspire CC BY-NC-SA 4.0**, Ability N/A.
- **Joint order caveat.** Map outputs **by joint name**, because URDF parsers order joints differently ([README](https://github.com/dexsuite/dex-retargeting); [issue](https://github.com/dexsuite/dex-retargeting/issues/20)).
- **Mac install.** Pinocchio (`pin`) and `nlopt` publish macOS arm64 wheels **[UNVERIFIED for every version; test with `pip install dex_retargeting` in a Python 3.11 venv]**. The core path needs no GPU. SAPIEN is only needed for the visualization examples.
- **Humanoids.** Unitree's `xr_teleoperate` (formerly `avp_teleoperate`) uses dex-retargeting as a submodule for **Unitree Dex3-1** and **Inspire** hands on G1/H1, with DexPilot as the default since a later release ([mirror of Unitree README](https://git.bibbit.duckdns.org/epilectrik/teleop/raw/commit/a347067941e8be8381480287e249d021d0e2a141/README.md)) **[verify on github.com/unitreerobotics/xr_teleoperate]**.
- **Fourier hands.** I found no Fourier configs in dex-retargeting or dex-urdf. You'd need Fourier's URDF and a custom YAML **[UNVERIFIED]**. A Rust port, `dexi-rs`, claims Fourier configs ([PyPI](https://pypi.org/project/dexi-rs/)) **[UNVERIFIED]**.

### 3.2 GeoRT (Meta FAIR, 2025)

- A neural retargeter trained **unsupervised** with geometric losses (motion fidelity, C-space coverage, flatness, pinch correspondence, self-collision). It runs at about 1 kHz with no per-frame optimization at inference. ([arXiv 2503.07541](https://arxiv.org/abs/2503.07541))
- **License: CC BY-NC 4.0.** The example env is Python 3.8. You train per hand from about 5K human keypoint samples at 0.01 s intervals that cover full finger ranges and pinches. Input is an [N,3] keypoint array in a palm frame (+Y toward thumb, +Z toward middle fingertip, origin at palm center). The ships-with example is the Allegro right hand. The authors "recommend (insist)" on glove mocap rather than MediaPipe. ([GitHub](https://github.com/facebookresearch/GeoRT))
- [OPINION] GeoRT is a good fit for your gloves. You can collect its calibration set as a 1-minute "finger stretch" mini-game at the start of each session.

### 3.3 Per-hand notes

| Hand | DoF / fingers | dex-retargeting config | URDF license | Notes |
|---|---|---|---|---|
| LEAP | 16 DoF, 4 fingers ([paper](https://arxiv.org/pdf/2410.02479)) | yes | MIT | DexCap drops the pinky ([DexCap](https://arxiv.org/abs/2403.07788)) |
| Allegro | 16 DoF, 4 fingers | yes | BSD | GeoRT example |
| Shadow | ~24 joints, 5 fingers | yes | GPL-3.0 | Closest to human |
| Inspire | 12 joints / 6 actuated, 5 fingers ([paper](https://arxiv.org/pdf/2410.02479)) | yes | CC BY-NC-SA | Used on Unitree G1/H1 |
| Ability | 5 fingers, 6 actuated | yes | N/A | |
| Unitree Dex3-1 | 3 fingers, 7 DoF **[UNVERIFIED DoF]** | via xr_teleoperate | Unitree | |
| Fourier | **[UNVERIFIED]** | no | — | Needs custom config |

[OPINION] **Don't publish retargeted joints as the primary action.** Publish human keypoints and wrist pose, and ship a script plus optional pre-computed `retargeted/<hand>` columns. Retargeting choices go stale quickly, while the human signal does not.

---

## 4. Kitchen simulation benchmarks

| Sim | Engine | Kitchen content | Apple Silicon | License |
|---|---|---|---|---|
| **RoboCasa / RoboCasa365** | MuJoCo + robosuite | 2,500 kitchens, 365 tasks (~65 atomic, ~300 composite), 3,200+ objects, 600+ h human + 1,600+ h synthetic demos (v1.0, Feb 18 2026) ([robocasa.ai](https://robocasa.ai/)) | **Yes.** Use `mjpython` for viewer scripts on Mac ([GitHub](https://github.com/robocasa/robocasa)). `MUJOCO_GL=egl` is Linux-only, so use default/CGL on Mac. A `libz` workaround is in the [Stretch docs](https://docs.hello-robot.com/0.3/stretch-mujoco/) | Code MIT, assets/datasets **CC BY 4.0** ([GitHub](https://github.com/robocasa/robocasa)) |
| BEHAVIOR-1K / OmniGibson | Isaac Sim | 1,000 activities in BDDL with object states (cooked, sliced…) ([arXiv](https://arxiv.org/abs/2403.09227)) | **No.** Ubuntu/Windows + RTX GPU only ([install](https://behavior.stanford.edu/getting_started/installation.html)) | — |
| ManiSkill3 | SAPIEN | Some kitchen scenes (RoboCasa scenes ported) **[UNVERIFIED]** | CPU sim + rendering yes; GPU sim no; needs Vulkan SDK ([macOS guide](https://maniskill.readthedocs.io/en/latest/user_guide/getting_started/macos_install.html)) | Apache-2.0 **[UNVERIFIED]** |
| LIBERO | robosuite/MuJoCo | 130 tabletop/kitchen-ish tasks | Likely (robosuite supports macOS ([docs](https://robosuite.ai/docs/installation.html))), but LIBERO pins `robosuite==1.4.0`, `numpy<1.24`, `mujoco==3.2.3` ([roboverse](https://roboverse.wiki/dataset_benchmark/integrations/libero)), so isolate it | MIT **[UNVERIFIED]** |
| Genesis | own | General | **Yes.** `gs.metal` backend for GPU sim, viewer and headless ([install docs](https://genesis-world.readthedocs.io/en/latest/user_guide/overview/installation.html)) | Apache-2.0 **[UNVERIFIED]** |

**RoboCasa conventions to align with.** Robot is PandaOmron (12-DoF). `observation.state` is 16-D. Cameras are `robot0_agentview_left`, `robot0_eye_in_hand` and `robot0_agentview_right` at 256×256. Action is 12-D in [-1, 1] (base 4 + mode 1 + EEF pos 3 + EEF rot 3 + gripper 1). ([LeRobot RoboCasa docs](https://huggingface.co/docs/lerobot/robocasa))

**Can game kitchen state map to RoboCasa tasks?** Yes, at the task/label level. Below the label level, only partly. [OPINION]

- **Atomic tasks map directly to game events.** Examples: `OpenDrawer`, `OpenCabinet`, `CloseFridge`, `TurnOnMicrowave`, `TurnOffStove`, `PickPlaceCounterToStove` ([LeRobot RoboCasa docs](https://huggingface.co/docs/lerobot/robocasa)). Name your game's fixture interactions with exactly these verbs and fixtures, and emit a `robocasa_task` label when one completes.
- **Composite categories** include baking, boiling, brewing, chopping, clearing table, defrosting, loading dishwasher, making tea, microwaving and washing dishes ([same](https://huggingface.co/docs/lerobot/robocasa)). Make these your recipe-level labels.
- **Geometry/physics replay** requires rebuilding your kitchen in MuJoCo. The MuJoCo Unity plug-in imports MJCF *into* Unity and keeps a 1:1 component mapping, so authoring the kitchen as MJCF first and importing it into Unity is the cleanest route to a scene that round-trips. Note that Unity PhysX rigidbodies are free bodies while MuJoCo requires explicit joints. ([MuJoCo Unity plug-in](https://mujoco.readthedocs.io/en/stable/unity.html)) I found no Unity→MJCF exporter **[UNVERIFIED]**.
- **Object-state vocabulary.** Use a BDDL-like predicate set (`cooked`, `sliced`, `ontop`, `inside`, `open`, `toggled_on`, `filled`), which lines up with BEHAVIOR-1K ([BDDL paper](https://arxiv.org/abs/2403.09227)) even though you can't run BEHAVIOR on the Mac.

---

## 5. What makes demos valuable

| Property | Why | How in the game |
|---|---|---|
| **Language labels** (task + subtask) | VLAs are language-conditioned; LeRobot stores tasks per episode ([docs](https://huggingface.co/docs/lerobot/lerobot-dataset-v3)); EgoDex adds LLM descriptions but warns they may be wrong ([repo](https://github.com/apple/ml-egodex)) | Templated from game events ("slice the tomato on the cutting board") + optional LLM paraphrase. Store `label_source` |
| **Subtask segmentation** | RoboCasa365 ships per-frame subtask annotations ([robocasa.ai](https://robocasa.ai/)); Ego-Exo4D keysteps ([paper](https://arxiv.org/pdf/2311.18259)); OakInk2 hierarchy ([arXiv](https://arxiv.org/abs/2403.19417)) | Game knows when "grasp knife", "slice", "transfer" begin and end: emit them |
| **Success labels** | Needed for filtering and for reward/value models | Game checks goal predicates; store `success`, `partial_progress` (0–1) |
| **Failure data** | Useful for recovery and value learning; must be *labeled*, never mixed silently | Keep failed and aborted episodes, flagged |
| **Ground-truth object state** | Removes the hardest perception problem in real video | 6-DoF pose per object, semantic states, held-by-which-hand |
| **Contacts** | ARCTIC/GRAB ship contact; hand-object contact is a key supervisory signal ([ARCTIC](https://arctic.is.tue.mpg.de/); [GRAB](https://ps.is.mpg.de/code/grab-a-dataset-of-whole-body-human-grasping-of-objects)) | Per-fingertip contact flags + contacted object ID from colliders |
| **Diversity** | Human-data gains come from scene/object/task variety ([EgoMimic](https://arxiv.org/abs/2410.24221); [DexWild](https://arxiv.org/abs/2505.07813); [PI](https://pi.website/research/human_to_robot)) | Randomize kitchen layout, textures, lighting, object instances and start states per episode; log the seed |
| **Synchronization** | Mis-sync silently ruins action labels | Single monotonic clock; timestamp every packet on arrival; measure latency with a clap/tap calibration |
| **Egocentric video** | Every listed human-to-robot effort is egocentric-video-centric | **Yes, render it** (§5.1) |
| **Quality filtering** | Crowd data is noisy; RoboCrowd found incentives affect quality ([arXiv](https://arxiv.org/abs/2411.01915)) | Automatic flags: tracking dropouts, MANO residual, assist usage, idle time, jerk |

### 5.1 Should you render egocentric RGB? **Yes.**

- Render a head-mounted camera from the player's view, **plus** one fixed "agentview" camera, and optionally a virtual wrist camera on each hand (this mirrors RoboCasa's `eye_in_hand` and DexWild's palm cameras ([DexWild](https://arxiv.org/abs/2505.07813))).
- Use 30 fps at 480×640 or 256×256 crops. Save camera intrinsics and extrinsics per frame (EgoDex does this ([repo](https://github.com/apple/ml-egodex))).
- Also render **segmentation masks and depth**. They are cheap in a game engine, rare in real data, and useful for hand masking and inpainting, which PI notes is a common alignment trick ([PI](https://pi.website/research/human_to_robot)).
- Optionally render a **"robot hand overlay" variant** that draws the retargeted robot hand instead of the human hand, to reduce the visual embodiment gap [OPINION].

### 5.2 Sim-to-real caveats for game-sourced data

1. **Kinematic or "magnetic" grasps.** If the game snaps objects to the hand, object motion is not caused by finger contact. Log `assist_active` per frame and per hand, and prefer physics-based grasping where feasible [OPINION].
2. **No haptics → unrealistic grip.** Players can't feel the object, so finger closure may over-penetrate or hover. Log penetration depth and fit residual.
3. **Visual domain gap.** Game renders differ from real cameras. Mitigate with texture and lighting randomization, and keep the signal you publish centered on *motion + state* rather than pixels.
4. **Scale and physics mismatch.** Object masses, friction and sizes must be realistic, so record the physical parameters you used.
5. **Drift.** IMU+EMF gloves drift without absolute references (hence Coil Pro) ([Knox Labs](https://www.knoxlabs.com/products/coil-pro)). Re-zero at episode start.
6. **Embodiment gap.** Human hands have more DoF than most robot hands, so retargeting loses information. That's another reason to publish human keypoints.

---

## 6. Precedents: games, crowds and data multiplication

- **RoboTurk** (Stanford 2018). Smartphone as a 6-DoF controller for a cloud-hosted sim streamed to the browser. 137.5 h of data and 2,200+ successful demos in about 22 h of usage. A 2019 follow-up collected 111 h on real arms from 54 users in one week. ([arXiv 1811.02790](https://arxiv.org/abs/1811.02790); [IROS'19](https://arxiv.org/abs/1911.04052))
- **RoboCrowd** (ICRA'25). ALOHA in a university café for two weeks: 200+ volunteers, 800+ episodes. Physical rewards gave up to 2× collection time and more engaging tasks up to 4×. Crowd pretraining improved expert-finetuned policies by up to 20%. ([arXiv 2411.01915](https://arxiv.org/abs/2411.01915))
- **RoboCade** (Dec 2025). Gamified *remote* teleop with GELLO. It argues intrinsic (game) motivation scales better than payment. ([arXiv 2512.21235](https://arxiv.org/abs/2512.21235))
- **MimicGen** (CoRL'23). Over 50K demos generated from 200 human demos across 18 tasks by object-centric segment transformation. ([arXiv 2310.17596](https://arxiv.org/abs/2310.17596))
- **DexMimicGen** (ICRA'25). 21K bimanual dexterous demos from 60 source demos, with real-to-sim-to-real deployment on a humanoid. ([arXiv 2410.24185](https://arxiv.org/abs/2410.24185))
  - [OPINION] MimicGen-style augmentation needs **object-centric subtask segmentation + object poses**, which your game can provide. That is a strong reason to log both.

**Using a crowd of hackathon/event players [OPINION]**

- **Two modes.** (1) *Free play* gives diversity. (2) *Quests* such as "make a salad in 3 minutes" give labeled, repeatable tasks. Mix them roughly 30/70 so every quest has many repeats from different people.
- **Incentives that work.** RoboCrowd's evidence favors a leaderboard, engaging challenges and small prizes ([RoboCrowd](https://arxiv.org/abs/2411.01915)). Score on *quality* (smoothness, no assists, success), not just speed.
- **Throughput.** At 5 min per player and 2 glove sets, plan for about 20 players/hour minus calibration. Calibration (T-pose, fist, GeoRT finger-stretch) should take ≤60 s and be gamified.
- **Hygiene.** Size S/M/L gloves, wipes and liners. Record glove size in metadata.
- **Operator log.** One volunteer keeps a sheet of anomalies per session_id.

---

## 7. Ethics, consent and licensing

**Why motion data is personal data**
- Head + hand motion uniquely identified 94.33% of 50,000+ VR users from 100 s of data ([Nair et al., USENIX Security'23](https://www.usenix.org/conference/usenixsecurity23/presentation/nair-identification)). Assume hand-motion traces are re-identifiable.
- GDPR Art. 4(14) defines biometric data to include *behavioural* characteristics that allow unique identification. It is special-category (Art. 9) when processed *for the purpose of* identification ([GDPR Art. 4(14) commentary](https://www.thomashelbing.com/en/wissen/dsgvo-hub/begriffe-und-definitionen/1.2.14-biometrische-daten); [ICO](https://cy.ico.org.uk/for-organisations/uk-gdpr-guidance-and-resources/lawful-basis/special-category-data/what-is-special-category-data)). Your purpose is not identification, but get explicit consent anyway [OPINION]. US states such as Illinois (BIPA) have their own biometric rules **[UNVERIFIED applicability; not legal advice]**.

**Practices [OPINION]**
- Opt-in, separate from "playing the game". Players can play without recording.
- Random `participant_id` with no names in the dataset. Keep any key mapping separately and delete it after the withdrawal window.
- Collect demographics only if useful and optional: handedness, glove size, age bracket (18+ only, or parental consent).
- Never record real webcam or microphone video of the player. Only game renders and glove/tracker data.
- Offer withdrawal until a stated date (e.g. 30 days) before public release.
- Publish a dataset card listing exactly what is captured.

**Suggested consent text (short, for a tablet at the booth)**

> **Let's Cook! — Data Recording Consent**
> If you agree, we will record your hand and finger movements (from the gloves and wrist tracker), your in-game actions, and computer-rendered video of the virtual kitchen while you play. We do **not** record camera video or audio of you, and we do not store your name with the data.
> **Why:** to build an open dataset that helps researchers teach robots to handle kitchen tasks.
> **Sharing:** the data will be published openly online (Hugging Face) under the Creative Commons Attribution 4.0 license, which allows anyone, including companies, to use it. Movement patterns can sometimes be distinctive to a person, so we cannot promise that motion data is impossible to link back to you.
> **Optional info:** handedness, glove size, and age range.
> **Your choice:** participation is voluntary. You can play without recording. You can ask us to delete your session until **[DATE]** by emailing **[CONTACT]** with your session code **[CODE]**.
> ☐ I am 18 or older and agree to have my session recorded and published as described.
> ☐ (Optional) You may contact me about future studies: ________

**Dataset license recommendation**
- **CC BY 4.0** for the main dataset. It matches DROID ([HF sample](https://huggingface.co/datasets/rerun/droid_sample/blob/main/README.md)), Open X-Embodiment materials ([OXE](https://github.com/google-deepmind/open_x_embodiment)) and RoboCasa assets/datasets ([RoboCasa](https://github.com/robocasa/robocasa)). It also maximizes uptake by both labs and companies.
- **Separate subset under CC BY-NC 4.0** for MANO-fitted parameters, because MANO is non-commercial ([MANO license](https://mano.is.tue.mpg.de/license.html)). **[UNVERIFIED legal interpretation**: whether fitted parameters are "derivative works" is unsettled. HOT3D's choice to license its MANO files NC ([license](https://projectaria.com/datasets/hot3D/license)) is the precedent.]
- Game assets inside rendered video must be licensed for redistribution. Check your asset-store licenses.
- Code and converters: MIT or Apache-2.0.
- Consider Hugging Face **gated access** (click-through terms), which many hand datasets use ([example](https://huggingface.co/datasets/Oceanveo/human-dex)), for a "do not attempt re-identification" clause.

---

## 8. Who wants this data and how to publish it

**Likely consumers**
- **Academic labs doing human→robot transfer:**
  - Georgia Tech (EgoMimic/EgoVerse) ([EgoVerse](https://arxiv.org/abs/2604.07607))
  - Stanford (DexCap, RoboCrowd) ([DexCap](https://arxiv.org/abs/2403.07788))
  - CMU (DexWild) ([DexWild](https://arxiv.org/abs/2505.07813))
  - UCSD (dex-retargeting/AnyTeleop, HAT) ([HAT](https://arxiv.org/abs/2503.13441))
  - UT Austin / NVIDIA (RoboCasa, MimicGen, DexMimicGen) ([DexMimicGen](https://arxiv.org/abs/2410.24185))
  - Meta FAIR (GeoRT) ([GeoRT](https://arxiv.org/abs/2503.07541))
  - Brown (GigaHands)
- **Companies investing in human data:**
  - Physical Intelligence ([PI](https://pi.website/research/human_to_robot))
  - Figure ([Go-Big](https://www.figure.ai/news/project-go-big))
  - Tesla ([eWeek](https://www.eweek.com/news/tesla-optimus-robot-training/))
  - Apple (EgoDex)
  - EgoVerse partners Mecka AI and Scale AI ([summary](https://www.labellerr.com/blog/egoverse-egocentric-robot-learning-dataset/))
  - Humanoid makers with dexterous hands: Unitree, Fourier
- **Hand-pose / HOI vision researchers** (MANO-based benchmarks).
- **The Hugging Face LeRobot community**, which hosts "thousands" of community datasets, mostly arm manipulation ([hyper.ai](https://hyper.ai/en/stories/4edac3a2423aaddcaceaee7fcd6370c4)). A dexterous-human cooking dataset would stand out.

**Publishing on the Hub**
1. `huggingface-cli login`, create the dataset with `LeRobotDataset.create(...)`, write episodes, `finalize()`, then `push_to_hub()` ([docs](https://huggingface.co/docs/lerobot/lerobot-dataset-v3)).
2. Tags: `LeRobot`, `robotics`, `egocentric`, `hand-tracking`, `dexterous-manipulation`, `simulation`, `cooking`. LeRobot datasets are browsable via `huggingface.co/datasets?other=LeRobot` ([LeRobot repo](https://github.com/rxdu/lerobot)).
3. Dataset card sections: capture rig, coordinate frames (draw them), fps, sync method, label sources, known artifacts (assists, drift), consent summary, license split, citation, and the converter repo link.
4. Release a 10-episode `-sample` repo first so people can inspect it in the Hub viewer.
5. Announce in the LeRobot Discord and on X/LinkedIn, tag the labs above, and open a PR or issue offering a RoboCasa-label crosswalk.

---

## 9. Recommended data pipeline

### 9.1 Architecture

```
Rokoko Studio (Custom Streaming JSON/LZ4, UDP :14043, ~83–100 Hz)
Wrist tracker SDK (pose @ native rate)              ─┐
Unity game (state, events, contacts @ physics rate)  ├─► Recorder (Python, on the M2)
Unity cameras (ego, agentview, depth/seg @ 30 fps)  ─┘    • monotonic clock, stamp-on-arrival
                                                         • writes session.mcap (raw, lossless)
                                                         • writes ego.mp4 / agent.mp4 (from Unity Recorder or AsyncGPUReadback)
                     ▼ offline (same Mac, after event)
Converter: resample to 30 fps → FK keypoints → MANO fit → (optional) dex-retargeting per hand
         → quality flags → LeRobotDataset v3 (+ NC MANO subset) → push_to_hub
```

- **Clocking.** Unity sends its state over a local socket (or the recorder runs inside Unity via C#). Use one host clock: stamp every packet with `time.monotonic_ns()` on receipt. Estimate the per-stream latency offset at session start with a "tap the counter three times" calibration: correlate wrist-velocity spikes with game contact events.
- **Resampling.** Raw streams stay at native rate in MCAP. For LeRobot, resample to **30 fps**: SLERP for quaternions, linear for positions, nearest for discrete states and events. 30 fps matches EgoDex and typical LeRobot cameras. Keep a `raw/` folder with the full-rate glove stream as Parquet for people who want 83–100 Hz.
- **Frames.** World = game kitchen frame, Z-up, meters, right-handed. Document the conversion from Unity's left-handed Y-up frame (flip one axis and rotate). Per hand, the wrist frame follows GeoRT's palm convention (+Y toward thumb, +Z toward middle fingertip, +X palm normal, origin at palm center) ([GeoRT](https://github.com/facebookresearch/GeoRT)).
- **M2 16 GB budget [OPINION].** The low-dim data is tiny: about 300 floats × 30 fps ≈ 36 KB/s. Two 480p H.264/AV1 streams take about 2–4 Mbps. Encode video in Unity or with VideoToolbox (`h264_videotoolbox`/`hevc_videotoolbox`) live, and re-encode to LeRobot's codec offline. MANO fitting runs in PyTorch on MPS or CPU in minutes per hour of data.

### 9.2 Per-frame schema (LeRobot features, 30 fps)

Units: meters, quaternions `[x,y,z,w]`, world frame unless noted. `L`/`R` = left/right hand.

| Key | dtype | shape | Description |
|---|---|---|---|
| `timestamp`, `frame_index`, `episode_index`, `index`, `task_index` | std | — | LeRobot bookkeeping |
| `observation.images.ego` | video | (480, 640, 3) | Head-mounted egocentric render |
| `observation.images.agentview` | video | (480, 640, 3) | Fixed third-person render |
| `observation.images.wrist_left` / `wrist_right` | video | (256, 256, 3) | *Optional* virtual palm cameras |
| `observation.depth.ego` | video or uint16 PNG | (480, 640) | *Optional* depth |
| `observation.seg.ego` | video | (480, 640) | *Optional* instance IDs (lossless) |
| `observation.camera.ego_pose` | float32 | (7,) | Head camera pos + quat |
| `observation.state.wrist_pose` | float32 | (2, 7) | L/R wrist pos + quat (from tracker + offset) |
| `observation.state.hand_keypoints` | float32 | (2, 21, 3) | 21 keypoints per hand, **world frame** (MediaPipe order) |
| `observation.state.hand_keypoints_local` | float32 | (2, 21, 3) | Same, in wrist frame |
| `observation.state.finger_joint_quat` | float32 | (2, 19, 4) | Raw Rokoko local finger joint quats (5 metacarpals + 14 phalanges per hand: thumb has no Medial; tips have no rotation). Names in `info.json` |
| `observation.state` | float32 | (2·(7+63),) = (140,) | Flattened `wrist_pose` + `hand_keypoints_local`, the default policy input |
| `action` | float32 | (140,) | **Next-frame** (t+1) target of the same vector as `observation.state`, the convention PI/EgoMimic-style human embodiment uses (future hand positions) ([PI](https://pi.website/research/human_to_robot)). Chunking is done at train time |
| `action.retargeted.leap_right` etc. | float32 | (16,) | *Optional* dex-retargeting output per robot hand, named by joint |
| `observation.environment_state` | float32 | (K, 7) | 6-DoF pose of the K tracked objects (fixed object list per episode in metadata) |
| `observation.object_states` | int8 | (K, S) | Per-object predicates: `open, toggled_on, sliced, cooked_level(0–3), filled, dirty, inside_id, ontop_id` |
| `observation.contacts` | int16 | (2, 6) | Per hand: object ID in contact for 5 fingertips + palm (−1 = none) |
| `observation.held_object` | int16 | (2,) | Object ID held by each hand (−1 = none) |
| `quality.assist_active` | bool | (2,) | Game snapped or magnetized a grasp this frame |
| `quality.tracking_ok` | bool | (2,) | Glove + tracker valid (no dropout or out-of-range) |
| `quality.mano_residual_mm` | float32 | (2,) | MANO fit error (filled offline) |
| `annotation.subtask_index` | int16 | (1,) | Index into episode `subtasks` (−1 = transition) |
| `annotation.event` | int16 | (1,) | Sparse event ID (grasp, release, slice, pour, toggle…), 0 = none |

Separate NC subset `…-mano` with identical indices: `mano.betas (2,10)`, `mano.global_orient (2,3)`, `mano.hand_pose (2,45)`, `mano.transl (2,3)`.

### 9.3 Per-episode schema (`meta/episodes` + sidecar `episodes_ext.jsonl`)

```json
{
  "episode_index": 412,
  "session_id": "evt2026-10-24_s037",
  "participant_id": "p_8f3a91",
  "tasks": ["make a tomato salad"],
  "task_mode": "quest",
  "recipe_id": "salad_tomato_v2",
  "robocasa_task_labels": ["OpenFridge", "PickPlaceFridgeToCounter", "chopping"],
  "subtasks": [
    {"index": 0, "label": "open the fridge", "start_frame": 0,   "end_frame": 84,  "success": true},
    {"index": 1, "label": "take the tomato out", "start_frame": 85, "end_frame": 190, "success": true},
    {"index": 2, "label": "slice the tomato with the knife", "start_frame": 191, "end_frame": 610, "success": true}
  ],
  "success": true,
  "progress": 1.0,
  "termination": "goal_reached",
  "duration_s": 41.3,
  "fps": 30,
  "label_source": {"tasks": "game_template", "paraphrases": "llm:<model>", "subtasks": "game_events"},
  "language_paraphrases": ["slice a tomato for a salad", "prepare tomato slices"],
  "scene": {"kitchen_layout": "L_shape_03", "style": "modern_white", "lighting": "evening", "seed": 918273},
  "objects": [{"id": 0, "category": "tomato", "asset": "tomato_02", "mass_kg": 0.12}, {"id": 1, "category": "knife", "asset": "chef_knife_01", "mass_kg": 0.18}],
  "participant": {"handedness": "right", "glove_size": "M", "age_bracket": "25-34", "hand_length_mm": 185, "experience": "first_time"},
  "calibration": {"rokoko_profile": "p_8f3a91_cal1", "wrist_offset_L": [0,0,0,0,0,0,1], "wrist_offset_R": [0,0,0,0,0,0,1], "latency_ms": {"glove": 22, "tracker": 9, "render": 33}},
  "hardware": {"gloves": "Rokoko Smartgloves II", "glove_rate_hz": 83.33, "wrist_tracker": "<model>", "game_build": "letscook-1.4.2+abc123", "engine": "Unity 6000.x", "physics_dt": 0.005},
  "quality": {"assist_fraction": 0.04, "tracking_dropout_fraction": 0.0, "mano_residual_mm_p95": 6.1, "idle_fraction": 0.12, "flags": [], "auto_score": 0.87, "reviewer_ok": null},
  "consent": {"version": "v1.0", "license": "CC-BY-4.0", "withdrawal_deadline": "2026-11-30"}
}
```

### 9.4 Session checklist

1. Consent tablet → `participant_id` and session code card.
2. Glove fit → Rokoko calibration → **60 s calibration mini-game** (flat hand, fist, pinch each finger, full stretches). This feeds the MANO β fit and the GeoRT training set.
3. Sync tap: tap the counter three times.
4. Quest episodes (labeled) → free-play episode → leaderboard.
5. After the event: convert → auto-flag → spot-check 10% in the Rerun/LeRobot visualizer → drop assist-heavy or dropout episodes from the "clean" split (keep them in a `noisy` split) → publish `-sample`, then the full dataset after the withdrawal deadline.

### 9.5 Open items to verify before the event

- `meta/tasks.jsonl` vs `tasks.parquet`, plus the exact `add_frame` task key, in the installed lerobot version.
- Whether `pip install dex_retargeting` resolves cleanly on macOS arm64 with Python 3.11/3.12.
- Smartgloves model (I vs II) → 100 Hz vs 83.33 Hz.
- Rokoko Custom Streaming port and compression setting.
- MANO joint order in `smplx`.
- Fourier/Unitree URDFs.
- Licenses for Ego4D/Ego-Exo4D, EgoVerse, GigaHands, HOI4D and ManiSkill if you redistribute anything derived from them.
