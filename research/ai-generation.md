# AI generation research: on-demand tools + LLM dish naming

*Researched 2026-10-17 for "let's cook!" (glove-controlled free-play cooking, Unity on an Apple M2 16 GB Mac).*

**How to read this**
- Every non-obvious claim has a source link. Prices and speeds change month to month, so re-check before you build on them.
- **[UNVERIFIED]** means only one secondary source, or sources that disagree, or my own estimate. **[EST]** means my engineering estimate, not a measured number.
- Third-party aggregator sites (costbench, 3daistudio blog, toolradar, etc.) are used only where no official page exists, and are marked.

---

## TL;DR

1. **No text-to-3D service is "instant" with good textures.** The fastest textured generators need about 10-90 s, and most need 1-4 min ([Meshy processing times](https://docs.meshy.ai/en/api/task-lifecycle)). For "ready very soon", **retrieve first, generate second.**
2. **Local generation on a 16 GB M2 is not practical for the textured pipeline.** The TRELLIS.2 Mac port peaks at about 18 GB of RAM and takes minutes ([lilting.ch M1 Max test](https://lilting.ch/en/articles/trellis2-m1-max-hands-on)). Use cloud APIs. Stable Fast 3D is the one model that can run locally as a rough fallback.
3. **Kitchen tools fit procedural generation unusually well.** Mortars, pestles, bowls, pots, rolling pins, cups and whisks are mostly *surfaces of revolution + extrusions + handles*. An LLM can output a parametric JSON spec that Unity builds into a mesh in milliseconds, with **exact** dimensions, part labels and colliders. This is the fastest path to a *physically usable* tool.
4. **Physics and affordances: ask Claude for structured JSON, and validate it in code.** Research systems (EmbodiedGen V2, PhysX-3D, Articraft) already use VLMs/LLMs to assign scale, mass and friction to generated assets. Accuracy is "plausible", not measured truth, so clamp the numbers against a material density table.
5. **Dish naming:** send Claude a compact structured event log plus one screenshot, and use `output_config.format` JSON schema (structured outputs). Cache the static ontology prompt. Use Sonnet 5.5 for naming and Haiku 5.5 for fast parsing. Opus 5.5 is only worth it for the deeper "unlock" design.

---

# PART A: "Prompt any missing tool and it's ready to use very soon"

## 1. Text-to-3D / image-to-3D options (as of Oct 2026)

### 1.1 Comparison table

| Service | Time to usable mesh | Quality / notes | Price per model | API | Formats | Polycount control | PBR | Local on M2 16 GB? |
|---|---|---|---|---|---|---|---|---|
| **Meshy** (meshy-7.1 latest, meshy-6, meshy-t2/t2.5 Smart Topology) | Median: text preview **10 s (t2) to 90 s (meshy-6)**. Image-to-3D mesh only: **4 s (t2) to 90 s**. With textures: **2.5-4 min** ([task lifecycle](https://docs.meshy.ai/en/api/task-lifecycle)) | Strong all-rounder. Meshy-7/7.1 (Aug/Sep 2026) adds geometry detail. Smart Topology gives clean, part-separated, triangle-only game meshes ([changelog](https://docs.meshy.ai/en/api/changelog)) | Credits: text/image→3D mesh **20** (7.1/6) or **5** (6-lite/t2); textured image→3D **30**; refine texture **10** ([API pricing](https://docs.meshy.ai/api/pricing)). Dollars per credit aren't published; about **$0.01-0.02/credit** implied by plans [UNVERIFIED, [gamsgo](https://www.gamsgo.com/blog/meshy-ai-cost)] → roughly **$0.05-0.60/model** | Yes, REST, prepaid credits | GLB, plus FBX/OBJ/USDZ/3MF via `target_formats`. USDZ now declares metres ([changelog](https://docs.meshy.ai/en/api/changelog)) | `target_polycount` 100-15,000 (t2), up to 30,000 (t2.5) ([changelog](https://docs.meshy.ai/en/api/changelog)) | Yes (base, metal, rough, normal, emission) | No (cloud only) |
| **Tripo** (H-series v3.1-20260211, v3.0; P-series P2) | ~8-10 s basic mesh, minutes for HD [UNVERIFIED, [review](https://pasqualepillitteri.it/en/news/7811/tripo-ai-review-3d-model-generator)]. A doc sample task took ~90 s ([docs](https://developers.tripo3d.com/en/docs/generation-text-to-model/standard.md)) | Very good geometry. v3.0 (Sep 2025) is ~20B params [UNVERIFIED]. `generate_parts` returns segmented parts, but not together with texture ([docs](https://developers.tripo3d.com/en/docs/generation-text-to-model/standard.md)) | $0.01/credit, 2,000 free credits on first API key ([Tripo blog](https://www.tripo3d.ai/blog/announcement-tripo-openapi-platform-payment-system)). Per-task credit table not found. P2 via fal: **$1.00 untextured / $1.10 textured** ([fal](https://fal.ai/models/tripo3d/p2/text-to-3d/llms.txt)) | Yes (own platform, fal, AWS Marketplace) | GLB (default). `quad:true` forces FBX ([docs](https://developers.tripo3d.com/en/docs/generation-text-to-model/standard.md)). USDZ via convert [UNVERIFIED] | `face_limit` up to 1.5M (v3.1). `smart_low_poly` 500-20k tris. P2: 48-50k ([docs](https://developers.tripo3d.com/en/docs/generation-text-to-model/standard.md), [fal](https://fal.ai/models/tripo3d/p2/text-to-3d/llms.txt)) | Yes (base, metal, rough, normal) | No |
| **Rodin / Hyper3D** (Gen-2, Gen-2.5 May 2026) | Gen-2.5 lowest tier ~30-90 s [UNVERIFIED, [3daistudio](https://www.3daistudio.com/Models/Rodin-Gen-2-5)]. Marketing claims "1M-poly in 4 s" [UNVERIFIED, [Scenario](https://help.scenario.com/articles/3791851266-rodin-hyper3d-gen-2-5-models-the-essentials)] | Top-tier hard-surface quality, quad meshes | API needs the **Business plan $120/mo** (or $96/mo yearly), ~416 models/mo. Direct credits **$1.50 each** ([pricing](https://hyper3d.ai/pricing)). Third-party pay-per-call ~$0.80/gen [UNVERIFIED, [EmpirioLabs](https://empiriolabs.ai/models/hyper3d-gen2)] | Yes (Business+) | GLB, FBX, OBJ, USDZ, STL ([80.lv](https://80.lv/partners/hyper3d)) | Yes, quad/tri, wide range [UNVERIFIED] | Yes | No (on-prem is Enterprise only) |
| **Tencent Hunyuan3D** (open 2.0/2.1/mini/turbo; cloud 3.0/3.1) | 2.0 turbo "within 30 s" ([Reuters via Fidelity](https://fidelity.com/news/article/company-news/202503180517RTRSNEWSCOMBINED_KBN3FV0LY-OUSBS_1)). 2.1 median **139 s on RTX 4090** ([SaladCloud](https://blog.salad.com/hunyuan3d-2-1/)) | 3.0 announced globally Nov 2025, with Smart Topology (tri/quad) and multi-view input ([Tencent](https://www.tencent.com/tencent-announces-global-launch-of-hunyuan-3d-engine-to-empower-creators-with-advanced-creation-tools/)) | fal: **v3.1 Rapid $0.225** (+$0.15 PBR) ([fal](https://fal.ai/models/fal-ai/hunyuan-3d/v3.1/rapid/image-to-3d/llms.txt)). v2 multi-view turbo **$0.015** [UNVERIFIED, [costgoat](https://costgoat.com/pricing/hunyuan-3d)]. Tencent Cloud gives 20 free/day on the web app ([Tencent](https://www.tencent.com/tencent-announces-global-launch-of-hunyuan-3d-engine-to-empower-creators-with-advanced-creation-tools/)) | Yes (Tencent Cloud, fal) | GLB/OBJ | Not exposed on the fal Rapid endpoint | 2.1+: PBR ([GitHub](https://github.com/Tencent-Hunyuan/Hunyuan3D-2.1)) | **Shape only, maybe.** 2.1 needs ~10 GB VRAM for shape and ~21 GB for texture ([GitHub](https://github.com/Tencent-Hunyuan/Hunyuan3D-2.1)). An MLX shape-only port uses ~5.6 GB, ~20 s [UNVERIFIED, [eliteaiadvantage](https://eliteaiadvantage.com/blog/run-ai-3d-model-generation-locally-mac), [glama](https://glama.ai/mcp/servers/JimCline/hy3d-mcp)]. Texturing on Mac often fails (needs a CUDA rasterizer) |
| **Microsoft TRELLIS.2** (4B, MIT) | H100: **~3 s at 512³, ~17 s at 1024³, ~60 s at 1536³** ([GitHub](https://github.com/microsoft/TRELLIS.2)) | Excellent image-to-3D. Image input only (no text) | fal: **$0.25 (512) / $0.30 (1024) / $0.35 (1536)**. `decimation_target` 5k-2M verts, texture 1-4K ([fal](https://fal.ai/models/fal-ai/trellis-2/llms.txt)) | Via fal, Runware, etc. | GLB | Yes (decimation target) | Base, rough, metal, opacity ([GitHub](https://github.com/microsoft/TRELLIS.2)) | **No on 16 GB.** The community MPS port takes ~3.5 min on an M4 Pro 24 GB ([Korben](https://korben.info/en/trellis-mac-microsoft-image-to-3d-apple-silicon.html)) and peaks at **~18 GB RAM**. The author says 24 GB is the real minimum, and PBR baking takes 66-224 *minutes* ([lilting.ch](https://lilting.ch/en/articles/trellis2-m1-max-hands-on)) |
| **Stability Stable Fast 3D / SPAR3D** | SF3D **~0.5 s** on a GPU, ~1 s via API ([wallstreetcn](https://wallstreetcn.com/articles/3722523)). SPAR3D **~0.7 s** ([The Decoder](https://the-decoder.com/stability-ais-new-3d-model-aims-for-real-time-generation/)) | Lowest quality of the group, but real-time. UV-unwrapped albedo, simple shapes OK | ~$0.07-0.10 [UNVERIFIED, [3daistudio](https://www.3daistudio.com/blog/best-3d-model-generation-apis-2026)]. Free under the Community License for revenue under $1M ([The Decoder](https://the-decoder.com/stability-ais-new-3d-model-aims-for-real-time-generation/)) | Stability API | GLB ([GitHub](https://github.com/Stability-AI/stable-fast-3d)) | Remesh tri/quad with a target vertex count ([GitHub](https://github.com/Stability-AI/stable-fast-3d)) | Albedo only per 3daistudio. The README describes UV textures | **Yes, experimental.** MPS support, ~6 GB for one image, but the README suggests the *CPU* build when you have under 32 GB ([GitHub](https://github.com/Stability-AI/stable-fast-3d)). Expect several seconds to tens of seconds on an M2 [EST] |
| **Meta SAM 3D Objects** (Nov 2025, open weights) | n/a | Strong single-object reconstruction from a masked photo ([DeepLearning.AI](https://www.deeplearning.ai/the-batch/metas-sam-3-image-segmentation-suite-analyzes-and-creates-3d-bodies-and-other-objects)) | Hosted on Runware, etc. ([Runware](https://runware.ai/creators/meta)) | Third-party | GLB/PLY [UNVERIFIED] | – | – | No. Reportedly needs Linux + 32 GB NVIDIA [UNVERIFIED, [macmyths](https://macmyths.com/how-to-convert-a-photo-into-a-3d-model-using-sam-3d/)] |
| **CSM (Cube)** | "Varies" | **Acquired by Google (DeepMind), Jan 2026** ([Gunderson](https://www.gunder.com/en/news-insights/client-news/gunderson-dettmer-represents-common-sense-machines-in-acquisition-by-google), [The Decoder](https://the-decoder.com/google-deepmind-goes-on-acquisition-spree-with-three-ai-deals-in-one-week/)). The API's future is uncertain | ~$0.20 [UNVERIFIED] | REST + Python ([docs](https://docs.csm.ai)) | GLB/OBJ/USDZ [UNVERIFIED] | – | Enterprise | No. **Don't build on it.** |
| **Luma Genie** | "<10 s" at the 2024 launch ([3printr](https://www.3printr.com/text-to-3d-luma-ai-introduces-genie-for-the-creation-of-3d-models-2268154/)) | Luma's focus has moved to video. No current official Genie API docs found [UNVERIFIED] | – | Unclear | Quad mesh, standard formats (2024) | – | – | No. **Skip.** |
| **Sloyd** | Near-instant. It *assembles* parametric templates rather than diffusing a mesh ([3D Printing Industry](https://3dprintingindustry.com/news/ai-text-to-stl-tool-sloyd-2-0-launches-238788/)) | Clean, game-ready, but only within its template categories | Undisclosed. Enterprise API [UNVERIFIED] | Enterprise | OBJ/FBX/GLB [UNVERIFIED] | Yes | Yes [UNVERIFIED] | No (cloud) |
| **Kaedim** | Human-in-the-loop (artists finish the AI output). Turnaround is hours, not seconds [UNVERIFIED] | Production quality | $50-1,200/mo plans [UNVERIFIED, [costbench](https://costbench.com/software/ai-3d-generation/kaedim/price-history/)] | Enterprise | FBX/OBJ/GLB | Yes | Yes | No. **Too slow for runtime.** |
| *Newer to watch:* **Pixal3D** (Tsinghua/TencentARC, SIGGRAPH 2026, MIT, PBR) [UNVERIFIED, [promptzone](https://www.promptzone.com/staluxy/pixal3d-pixel-aligned-image-to-3d-generation-open-source-3294.md)]. **PhysX-3D / PhysXGen** (NeurIPS 2025) generates geometry *with* scale, material, affordance and kinematics ([arXiv 2507.12465](https://arxiv.org/html/2507.12465v4)). Very on-topic, but research code. | | | | | | | | |

### 1.2 Takeaways for this game
- **Fastest usable cloud path:** text → (image model) → **TRELLIS.2 @512 on fal** (~3 s of GPU time + queue/upload, ~$0.25), or **Meshy meshy-t2 Smart Topology** (10 s text preview, 5 credits, clean low-poly with separated parts, which is great for physics). Add textures later as a second pass.
- **Best quality for an "upgrade" pass:** Meshy-7.1 textured, Tripo v3.1 with PBR + `face_limit` ~20-50k, or Rodin Gen-2.5. Expect 1-4 min.
- **Prefer image-to-3D over text-to-3D.** You control the look with one fast 2D image (consistent art style, front-facing, white background). The image also gives Claude something to check before you spend 3D credits. Text-to-image costs $0.034-0.067 at 1K with Nano Banana 2.1 / Gemini 3.1 Flash Image ([Gemini pricing](https://ai.google.dev/gemini-api/docs/pricing)).
- **Don't run generation locally on a 16 GB M2.** The memory comes out of the same pool Unity and the glove stream need. The one exception is Stable Fast 3D as an offline fallback [EST].

## 2. Faster alternatives: retrieval, procedural, hybrid

### 2.1 Retrieval libraries

| Library | Size / content | License | Access | Fit |
|---|---|---|---|---|
| **Objaverse 1.0** | 800K+ models ([arXiv 2212.08051](https://arxiv.org/abs/2212.08051)), GLB. LVIS category → UID mapping via `objaverse.load_lvis_annotations()` ([docs](https://objaverse.allenai.org/docs/objaverse-1.0)) | Per-object (CC-BY, CC0, NC...). Filter `license == "by"` ([docs](https://objaverse.allenai.org/docs/objaverse-1.0)) | `pip install objaverse`, which downloads GLBs | Huge, but uneven quality and scale. **Pre-filter offline** to a kitchen subset |
| **Objaverse-XL** | 10M+ objects ([arXiv 2307.05663](https://arxiv.org/pdf/2307.05663)) | Dataset ODC-By; objects keep their own licenses | Python API | Too noisy for runtime. Mine it offline |
| **Cap3D captions** | ~1M text captions for Objaverse objects. A commercial-friendly subset exists ([HF](https://huggingface.co/datasets/tiange/Cap3D)) | ODC-By | HF dataset | **Use these captions as the text you embed for search** |
| **Sketchfab Download API** | 1M+ free CC models. glTF/GLB/USDZ only, no FBX/OBJ ([Sketchfab](https://sketchfab.com/developers/download-api)) | CC (attribution needed). Most allow commercial use | **End users must log in with OAuth** unless you make a special arrangement with Sketchfab ([Sketchfab](https://sketchfab.com/developers/download-api)) | Good for build-time curation. Awkward at runtime (OAuth inside a glove game) |
| **Poly Haven** | Small, very high quality, CC0 ([API](https://polyhaven.com/our-api)) | CC0. The live API needs attribution + a unique User-Agent | Free REST API | A few kitchen props plus great HDRIs/textures for the kitchen itself |
| **Google Scanned Objects** | **1,030** real scanned household items, 13 GB, OBJ+PNG ([Google Research](https://research.google/blog/scanned-objects-by-google-research-a-dataset-of-3d-scanned-common-household-items/)) | CC-BY 4.0 | Download. A MuJoCo-ready port with collision meshes exists ([mujoco_scanned_objects](https://github.com/kevinzakka/mujoco_scanned_objects)) | **Real-world scale** and photo-real, which is good for robot-data realism. Few actual utensils |
| **YCB** | Manipulation benchmark objects incl. kitchen items, watertight `google_16k` meshes ([ycbust docs](https://docs.rs/crate/ycbust/0.3.1), [TIB](https://service.tib.eu/ldmservice/dataset/ycb-object-and-model-set)) | Check the YCB site [UNVERIFIED] | Download | Robotics-standard objects (mug, bowl, spatula, pan...). Great for sim-to-real overlap with robot datasets |

**How to retrieve:** embed captions (Cap3D) and thumbnails with a CLIP/SigLIP-style model *offline*, and store vectors locally. At runtime, embed the player's request and take the nearest neighbor, which takes milliseconds [EST]. Holodeck did this pattern: an LLM plans the objects, assets are retrieved from 50K+ Objaverse objects by text description and dimensions ([Holodeck CVPR 2024](https://arxiv.org/html/2312.09067v2)), and Holodeck 2.0 adds CLIP scoring plus generation of missing assets ([Holodeck 2.0](https://arxiv.org/html/2508.05899v1)).

**Practical recommendation:** at build time, curate a **"pantry" of ~300-1,000 kitchen tools**. Pre-scale them, pre-decompose colliders, pre-compute the physics JSON, and ship them on disk. Retrieval then reads from local disk, so the latency is just a glTFast load.

### 2.2 Procedural / parametric generation by LLM

Most kitchen tools are a few primitive families:
- **Lathe (surface of revolution):** mortar, pestle, bowl, pot, cup, rolling pin, bottle, jar, ladle bowl, whisk handle, plate.
- **Extrude / loft:** knife blade, spatula, cutting board, grater plate, cookie cutter.
- **Composition:** handle (cylinder/box) + head + joint. Wire/tube sweep for a whisk, tongs, or a rack.

Have Claude output a **parametric spec** (a profile curve in metres, wall thickness, handle params, part labels, material). Then build the mesh in C# (lathe a 2D polyline around an axis, extrude). Building takes milliseconds [EST] and gives you:
- **exact real-world size** with no rescale guesswork,
- **semantic parts by construction** ("this part is the grinding surface"), which is the hardest thing to get from a generated mesh,
- **ideal colliders** (each part is convex or close to it, or use capsules/boxes).

Research context: LLMs writing CAD code is an active area. CadQuery program benchmarks (BenchCAD, 2026) show frontier models still struggle with fine detail and complex operations like sweeps and lofts ([BenchCAD arXiv 2605.10865](https://arxiv.org/pdf/2605.10865)). So keep the grammar **small and domain-specific** (lathe/extrude/handle) rather than free-form CAD code. Houdini is installed, which is fine for authoring templates offline, but Houdini Engine at *runtime* needs extra licensing [UNVERIFIED]. The C# approach is simpler.

### 2.3 Hybrid strategy (recommended)

```
request ─► Haiku: normalize ("mortar and pestle" → canonical tool, parts, size class)
   ├─► T0  (~0.1 s)  local pantry retrieval hit? → spawn immediately
   ├─► T1  (~2-4 s)  else Claude parametric spec → C# mesh builder → spawn "clay/stylized" version
   └─► T2  (~30-120 s, background) image → TRELLIS.2/Meshy/Tripo → decimate → CoACD → VLM physics check
                      → hot-swap the visual mesh (keep the T1 colliders/affordances if the shape matches)
                      → add it to the pantry cache so next time it's a T0 hit
```

The trick that makes the swap seamless: **physics and affordances belong to the tool definition, not to the mesh.** The T1 parametric proxy defines colliders, grip, and functional parts. The T2 mesh is visual and gets aligned/scaled to the proxy's bounding box.

## 3. Runtime import into Unity

| Option | Runtime? | Formats | Cost | Notes |
|---|---|---|---|---|
| **glTFast** (`com.unity.cloud.gltfast`, v6.x) | Yes. `GltfAsset` component or scripting API ([Unity docs](https://docs.unity3d.com/Packages/com.unity.cloud.gltfast@6.0/manual/index.html)) | glTF/GLB, plus Draco, KTX2/Basis, meshopt | Free (Unity) | Built for speed and memory efficiency. Works with URP/HDRP/Built-in. **Default choice**, since every generator above outputs GLB |
| **UnityGLTF** (Khronos) | Yes | glTF/GLB import + export | Free, open source | Choose this if you need runtime **export** (e.g. saving the player's created tools); glTFast runtime export is "coming soon" ([Unity docs](https://docs.unity3d.com/Packages/com.unity.cloud.gltfast@6.0/manual/index.html)) |
| **TriLib 2** | Yes, pure C#, no native plugins | FBX, OBJ, glTF2, STL, PLY, 3MF, ZIP | **$45/seat**, v2.5.9 (Jan 2026) ([Asset Store](https://assetstore.unity.com/packages/tools/modeling/trilib-2-model-loading-package-157548)) | Only needed for Tripo quad/FBX output or Rodin FBX. You can avoid it by always requesting GLB |

**Colliders at runtime**
- Unity convex MeshColliders are limited to **255 triangles** ([Unity manual](https://docs.unity3d.com/Manual/class-MeshCollider.html)). Dynamic Rigidbodies need convex colliders, so you need **convex decomposition** into several hulls.
- **CoACD** (MIT): collision-aware decomposition, better hull count and fidelity for the cost. `pip install coacd`, works on Apple Silicon. Its README also documents a Unity package with a `CoACD` component and a runtime `RunACD(Mesh)` API ([GitHub](https://github.com/SarahWeiii/CoACD)). Key knobs: `threshold` (default 0.05, higher = fewer parts), `max_convex_hull`, and `real_metric` to put the threshold in metres. Runtime per mesh isn't published. Expect ~1-20 s on CPU for a 10-50k-tri mesh [EST]. Run it **off the main thread or in the Python sidecar.**
- **V-HACD** (Unity-Technologies fork, Apache 2.0): Package Manager git URL, macOS binaries in the repo ([GitHub](https://github.com/Unity-Technologies/VHACD)). Whether it runs in a *player build* isn't stated [UNVERIFIED]. Tune `resolution` and `maxNumVerticesPerCH` (≤ 255 tris rule).
- **Always decimate before decomposing** (target 5-20k tris). TRELLIS.2 `decimation_target` and Meshy/Tripo polycount controls do this server-side.
- For **parametric (T1) tools**, skip decomposition: emit primitive colliders per part (capsule for a pestle, a ring of boxes or ~8-16 convex segments for a mortar's hollow bowl).
- A hollow container (mortar, pot, bowl) **must** be decomposed into wall segments. A single convex hull fills the cavity, and ingredients won't go inside. This is the #1 gotcha.

**Scaling to real-world size**
- Generators output arbitrary units. Rescale so the longest bounding-box axis equals the LLM-specified `dimensions_m` (sanity-check against a per-category size prior, e.g. mortar Ø 0.10-0.20 m).
- Meshy USDZ now declares metres ([changelog](https://docs.meshy.ai/en/api/changelog)). GLB is metres by spec, but generated content is still often normalized to a unit cube [EST].
- Also re-orient: the canonical "up" axis and the handle direction come from the VLM affordance pass (§4) or from the parametric spec.

## 4. Automatic physics + affordances

### 4.1 Research landscape

| Work | What it does | Relevance |
|---|---|---|
| **PhysObjects** (Gao et al., ICRA 2024) | 39.6K crowd-sourced + 417K automated annotations of physical concepts (mass, fragility, material, etc.) on household objects. Fine-tuning a VLM on them improves physical reasoning and robot planning ([arXiv 2309.02561](https://arxiv.org/pdf/2309.02561), [project](https://iliad.stanford.edu/pg-vlm)) | Shows VLMs need help with physical concepts. Useful as an eval set |
| **NeRF2Physics** (Zhai et al., CVPR 2024) | LLM proposes candidate materials; CLIP features on 3D points → per-point mass density, friction, hardness, zero-shot. Evaluated on ABO mass + measured friction/hardness ([arXiv 2404.04242](https://arxiv.org/html/2404.04242v1), [project](https://ajzhai.github.io/NeRF2Physics)) | **Copy the pattern:** LLM picks materials → look up density from a table → mass = density × mesh volume. Don't ask the LLM for mass directly |
| **PhysX-3D / PhysXNet** (NeurIPS 2025) | 26K objects annotated with absolute scale, material, part-level affordance rank, kinematics, function. PhysXGen generates assets with these properties ([arXiv 2507.12465](https://arxiv.org/html/2507.12465v4)) | Its schema is a good template for the tool JSON |
| **EmbodiedGen V2** (2026) | Agentic, sim-ready 3D asset engine. A VLM infers real-world scale, mass and friction from multi-view renders. 96.5% human acceptance, 98.6% collision success ([arXiv 2607.07459](https://arxiv.org/abs/2607.07459); method details per [search summary](https://arxiv.org/pdf/2607.07459)) | **Closest match to this pipeline:** render 4 views → VLM → physics JSON |
| **Articraft** (2026) | Articulated asset generation. An LLM assigns per-part mass and damping, validated in Isaac Sim ([arXiv 2605.15187](https://arxiv.org/pdf/2605.15187)) | Hinged tools: tongs, garlic press, lids |
| **GaussianProperty** | LMM + SAM assigns physical properties to 3D Gaussians ([arXiv 2412.11258](https://arxiv.org/html/2412.11258)) | Same idea, different representation |
| **PhysQuantAgent / VisPhysQuant** (2026) | VLMs are poor at raw mass estimation. Visual prompting (detection, scale cues, cross-sections) significantly improves it ([arXiv 2603.16958](https://arxiv.org/abs/2603.16958)) | **Give Claude scale cues** (a ruler or known object in the render) |
| **QuantiPhy** (Dec 2025) | 21 VLMs on quantitative physics (size, velocity) tend to rely on prior knowledge more than the input ([papers.cool](https://papers.cool/arxiv/2512.19526)) | Expect priors ("a mortar is ~15 cm"), which is fine for tools |
| **AGPIL / LMAffordance3D** (CVPR 2025), **GLOVER** | Language-guided 3D/2D affordance region grounding ([CVPR](https://openaccess.thecvf.com/content/CVPR2025/html/Zhu_Grounding_3D_Object_Affordance_with_Language_Instructions_Visual_Observations_and_CVPR_2025_paper.html), [GLOVER](https://arxiv.org/html/2411.12286v2)) | For mapping "grip here" onto mesh regions |

### 4.2 Practical recipe (works today with Claude vision)
1. Render the imported mesh from 4 canonical views (front/side/top/¾) with a **10 cm grid/ruler** and axis gizmo in frame (offscreen camera, ~512 px each ≈ 342 visual tokens each by the `⌈w/28⌉×⌈h/28⌉` rule, [Claude vision docs](https://platform.claude.com/docs/en/build-with-claude/vision)).
2. Send the views + the original request + the bounding-box dimensions + a **list of candidate part regions** (e.g. the mesh split into k clusters, or Tripo/Meshy-separated parts, each with a colored overlay and an ID). Claude is better at *choosing labeled regions* than at outputting raw coordinates, which the docs call approximate ([vision docs](https://platform.claude.com/docs/en/build-with-claude/vision)).
3. Claude returns the **tool-physics JSON** (schema in §8) through structured outputs.
4. **Validate in code:** density comes from your material table (the LLM only names the material); mass = density × mesh volume × fill factor (hollow); clamp friction to [0.05, 1.2]; check dimensions against the request; reject grip points outside the mesh.
5. Map functions to game systems: `crush` → force-based particle break for ingredients under the head; `cut` → slicing plane on the edge collider; `heat` → heat-source volume; `contain` → container volume for fluid/particle capture.

---

# PART B: LLM dish naming + progression

## 5. Describing a finished dish to Claude

### 5.1 What to send
Don't send raw glove frames (far too many tokens, and that data goes to the robotics log anyway). Send a **summarized, structured cooking log**:
- **Ingredients:** canonical id, amount (g/ml), final state (raw/chopped/minced/mashed/fried/boiled/charred/emulsified), doneness 0-1, and whether it's in the final plate.
- **Actions timeline:** compressed spans like `chop(onion, 42 cuts, 38s)`, `fry(onion+garlic, pan, 180°C, 4m10s)`, `crush(pepper, mortar, 25 strokes)`.
- **Tools used**, incl. *generated* tools (a fun hook: "you invented the cast-iron spoon-whisk").
- **Final plate:** vessel, arrangement summary, temperature, plus **one screenshot** (~1000×1000 px ≈ 1,296 visual tokens ([vision docs](https://platform.claude.com/docs/en/build-with-claude/vision)), about $0.0026 on Sonnet 5.5 at $2/MTok).
- **Player profile:** past dish names, preferred cuisines/flavors, how they cook (e.g. "always over-salts", "loves charring"), unlocked courses, player-stated preferences (culture, name, favourite foods). This is the "hella personal" part.

### 5.2 Prompting
- **System prompt (static, cached):** host persona/tone, the *game ontology* (all ingredient ids, cook states, courses, recipe template format), naming rules (e.g. "names are 2-6 words, playful, may reference the player's habits, never mock"), and 5-10 few-shot examples. Put it first with a `cache_control` breakpoint. Minimum cacheable prefix is **512 tokens** on Opus/Sonnet/Haiku 5.5, and cache reads cost **0.05×** base input on Opus/Sonnet 5.5 ([prompt caching docs](https://platform.claude.com/docs/en/build-with-claude/prompt-caching)).
- **Second breakpoint:** the player profile (changes once per dish).
- **Volatile tail:** this dish's log + screenshot. Note that *adding or removing images* invalidates the message-level cache, not the system cache ([caching docs](https://platform.claude.com/docs/en/build-with-claude/prompt-caching)), so keep images in the final user turn only.
- **Structured outputs:** `output_config: {format: {type: "json_schema", schema}}` returns schema-valid JSON. The first request with a new schema pays a grammar-compilation delay, then it's cached for 24 h. Numeric `minimum/maximum` and string length limits are **not supported** (400 error), so validate locally. Enum casing may vary, so compare case-insensitively ([structured outputs docs](https://platform.claude.com/docs/en/build-with-claude/structured-outputs)).
- **Pre-warm** at game start: send a `max_tokens: 0` request to write the cache ([caching docs](https://platform.claude.com/docs/en/build-with-claude/prompt-caching)), plus one throwaway structured call to compile the grammar.
- **Model-specific gotchas** (from the bundled Claude API reference, cached 2026-10-06): forced `tool_choice` (`any`/`tool`) returns 400 on Opus 5.5 and Sonnet 5.5, so use structured outputs instead. Opus 5.5 thinking can't be disabled; lower `effort` instead (default `medium`). Sonnet 5.5 can turn thinking off with `thinking: {type: "between_tools"}`. Haiku 5.5's default effort is `medium`; set `low` for speed. Assistant prefill returns 400 on all three. Handle `stop_reason: "refusal"` (consider the server-side `fallbacks` parameter on Opus/Sonnet).
- **Ask for creativity explicitly** and provide anti-examples ("avoid generic 'Delightful Medley' names"). Ask for `name_candidates` (3), then a chosen `name` + `why`. Separating ideation from choice gives better variety [EST].
- **Personalization guardrail:** pass a `do_not_repeat` list of the last ~50 names/words so names stay fresh.

### 5.3 Which Claude model for which job
Per [models overview](https://platform.claude.com/docs/en/about-claude/models/overview) (fetched 2026-10-17): all three have vision, 1M context, 128K max output. Comparative latency: Haiku "Fastest", Sonnet "Fast", Opus "Moderate".

| Job | Model | Settings | Why | Rough cost/call [EST] |
|---|---|---|---|---|
| Parse a spoken tool request → canonical tool + size class + search query | **claude-haiku-5-5** ($0.10/$0.50 per MTok, "fastest") | effort `low`, structured output | On the critical path of "ready very soon". Simple classification | <$0.001 |
| Parametric tool spec (T1 proxy mesh) | **claude-sonnet-5-5** ($2/$10) | effort `low`-`medium`, structured output | Needs spatial/common-sense reasoning about real dimensions | ~$0.01 |
| Tool physics/affordance check from 4 renders (T2) | **claude-sonnet-5-5** | effort `medium` | Vision + judgment. Runs in the background so latency is OK | ~$0.01-0.02 |
| Dish interpretation: name, course, description, flavour read | **claude-sonnet-5-5** | effort `medium` | Creative quality matters. Players see it right after plating | ~$0.01-0.02 |
| Unlock generation: new recipe + new course tailored to history | **claude-opus-5-5** ($4/$20) | effort `medium`, streaming, runs while the "reveal" animation plays | Highest-stakes creative + long-horizon personalization. Not latency-critical | ~$0.03-0.08 |
| Host one-liners during cooking ("ooh, bold amount of garlic") | **claude-haiku-5-5** | effort `low`, short `max_tokens` | Frequent, must be quick | <$0.001 |

Measure real latency yourself. The docs give only relative latency, and the time also depends on effort and output length ([models overview](https://platform.claude.com/docs/en/about-claude/models/overview)). My guess for short structured calls: Haiku ~0.5-1.5 s, Sonnet ~1.5-4 s, Opus ~3-10 s [EST, UNVERIFIED]. Stream the dish name so the reveal can start on the first tokens.

**Architecture note:** keep the API key out of the Unity client. Run a small local **Python sidecar** (alongside the existing `receiver.py` glove logger) that holds the Anthropic key, calls 3D APIs, runs CoACD, and serves Unity over localhost HTTP/WebSocket. The official Python SDK is `anthropic` (`client.messages.create`/`.parse`).

## 6. Optional extras

### 6.1 Dish images / cookbook pages
- Claude **cannot generate images** ([vision docs FAQ](https://platform.claude.com/docs/en/build-with-claude/vision)). Have Claude write the image prompt, then call an image model.
- Gemini image pricing (paid tier, per image): **Nano Banana 2.1: $0.034 (1K) / $0.050 (2K)**. Gemini 3.1 Flash Image: $0.067 (1K). Nano Banana Pro: $0.134. Batch is ~half. Gemini 2.5 Flash Image was shut down 2026-10-02 ([Gemini pricing](https://ai.google.dev/gemini-api/docs/pricing)). Meshy also resells image gen for 3-12 credits ([Meshy pricing](https://docs.meshy.ai/api/pricing)).
- Best idea: use **image-to-image from the actual plating screenshot** → a "cookbook illustration" style. It stays faithful to what the player made, which keeps it personal. Generate it during the reveal; it's not on the critical path.

### 6.2 Host voice (TTS)
- **ElevenLabs Flash v2.5:** ~75 ms model latency claimed. An independent streaming benchmark measured a **197 ms median to first audio** including network ([Vapi](https://humannessindex.vapi.ai/models/elevenlabs-flash-v2-5)). Price ~$0.05/1K chars per ElevenLabs API pricing as reported by [invideo](https://invideo.io/blog/ai-voice-model-pricing/) [UNVERIFIED, OpenRouter lists $20/M chars ([OpenRouter](https://openrouter.ai/elevenlabs/eleven-flash-v2.5))]. Custom voice design suits a charming host character.
- **Local/free:** macOS `AVSpeechSynthesizer` (zero cost, offline, less charming). Open-weights Kokoro-82M (Apache-2.0, small enough for an M2) ([HF](https://huggingface.co/hexgrad/Kokoro-82M)). Its M2 speed is [UNVERIFIED].
- Cache common lines ("Order up!", course names) as audio files. Only synthesize the dynamic ones.

### 6.3 Speech-to-text (hands are in gloves)
| Option | Speed | Accuracy | Notes |
|---|---|---|---|
| **Apple SpeechAnalyzer / SpeechTranscriber** (macOS 26+, on-device) | 34-min video in ~45 s vs 1:41 for Whisper large-v3 ([MacStories](https://www.macstories.net/stories/hands-on-how-apples-new-speech-apis-outpace-whisper-for-lightning-fast-transcription/)). 7.5-min file in 9 s on an M2 Pro vs 40 s Whisper ([heise](https://heise.de/-10475273)) | Whisper large-v3-turbo was more accurate in heise's test | Free, offline, streaming, ~10 languages at launch. Needs a small Swift plugin/bridge for Unity [EST]. **Best fit if you're on macOS 26** |
| **whisper.cpp** (Metal) / **whisper.unity** | M2 ~14× real time for large-v3 [UNVERIFIED, [PromptQuorum](https://www.promptquorum.com/local-llms/apple-silicon-whisper-metal-benchmark)]. Use `base.en`/`small.en` or turbo for short commands | Good, 100+ languages | **whisper.unity** wraps whisper.cpp with Metal on M1+ and installs via UPM git URL ([summary](https://www.sourcepulse.org/projects/2179014)). Maintenance may be stale (~1 yr since last push) [UNVERIFIED] |
| **NVIDIA Parakeet** (via MLX/CoreML ports) | 7.5-min file in 2 s on an M2 Pro ([heise](https://heise.de/-10475273)) | Good English | Fastest. English-focused |

**Interaction design:** use a **wake word or a glove gesture** (e.g. thumb-to-pinky pinch) as push-to-talk, so kitchen chatter doesn't spawn tools. Speech utterances are short (2-4 s), so latency after the end of speech should be ~0.2-0.8 s for any of these [EST].

---

## 7. Recommended pipeline (with expected seconds)

### 7.1 Tool request: "I need a mortar and pestle"

| Step | Where | Time [EST unless cited] | Cumulative |
|---|---|---|---|
| Push-to-talk gesture → SpeechAnalyzer final transcript | Local | 0.3-0.8 s after speech ends | **~0.6 s** |
| Haiku 5.5: canonicalize → `{tool_id_guess, query, size_class, parts}` | Cloud | 0.5-1.5 s | **~1.5 s** |
| **T0 pantry hit** (local vector search + glTFast load of a pre-processed GLB + cached colliders/physics JSON) | Local | 0.05-0.3 s | **≈ 1.5-2.5 s → usable tool in hand** |
| *Else* **T1**: Sonnet 5.5 parametric spec (lathe profile for mortar + pestle) → C# lathe/extrude builder → primitive colliders → physics from the material table | Cloud + local | 1.5-4 s + <0.05 s | **≈ 3-6 s → usable stylized tool** |
| *Background* **T2**: Nano Banana 2.1 reference image (1K) | Cloud | ~5-15 s [UNVERIFIED] | |
| → TRELLIS.2 @512 (fal) *or* Meshy meshy-t2 image→3D | Cloud | ~3 s GPU ([GitHub](https://github.com/microsoft/TRELLIS.2)) + queue/upload ≈ 10-30 s; Meshy t2 mesh-only median 4 s ([Meshy](https://docs.meshy.ai/en/api/task-lifecycle)) | |
| → decimate (server-side) → CoACD decomposition | Sidecar | 1-20 s | |
| → 4-view render → Sonnet 5.5 physics/affordance check | Local + cloud | 3-8 s | |
| → glTFast load, align to the T1 proxy bounding box, hot-swap visual (+ colliders if better) | Local | <0.5 s | **≈ 30-90 s → photoreal upgrade** |
| Cache the result into the pantry | Local | – | next request = T0 |

Cost per *new* tool ≈ $0.001 (Haiku) + $0.01 (Sonnet spec) + $0.034 (image) + $0.25 (TRELLIS.2 @512) + $0.015 (Sonnet check) ≈ **$0.30**. Pantry hits are ~free.

### 7.2 Dish finished
1. Player plates the dish and makes a "serve" gesture. The game freezes the event log and grabs a screenshot (0 s).
2. Sonnet 5.5 (cached system + profile) → dish interpretation JSON, streamed. The name appears in **~2-4 s** [EST] while a drum-roll animation plays.
3. In parallel: Opus 5.5 → unlock JSON (new recipe + course), **~5-15 s** [EST], revealed after the name. Gemini image-to-image → cookbook illustration (~5-15 s [UNVERIFIED]). TTS of the host line (~0.2 s to first audio).
4. Write everything to the player's cookbook DB. Update the profile summary (Haiku, async).

---

## 8. Suggested JSON schemas

Written as JSON Schema that is compatible with Claude structured outputs (no `minimum`/`maximum`/`minLength`; ranges go in `description` and get validated in code; `additionalProperties: false` everywhere ([docs](https://platform.claude.com/docs/en/build-with-claude/structured-outputs))).

### 8.1 Tool request normalization (Haiku)
```json
{
  "type": "object",
  "additionalProperties": false,
  "required": ["tools"],
  "properties": {
    "tools": {
      "type": "array",
      "description": "One entry per distinct physical object requested (a mortar and pestle = 2 objects in one set).",
      "items": {
        "type": "object",
        "additionalProperties": false,
        "required": ["canonical_name", "set_id", "search_query", "size_class", "primary_functions", "generation_family"],
        "properties": {
          "canonical_name": {"type": "string", "description": "e.g. 'mortar'"},
          "set_id": {"type": "string", "description": "shared id for objects used together, e.g. 'mortar_and_pestle'"},
          "search_query": {"type": "string", "description": "short caption for pantry vector search"},
          "size_class": {"type": "string", "enum": ["tiny", "hand", "two_hand", "countertop", "appliance"]},
          "primary_functions": {"type": "array", "items": {"type": "string", "enum": ["cut", "crush", "grind", "stir", "whisk", "scoop", "pour", "contain", "heat", "flip", "grate", "press", "roll", "pierce", "strain", "measure", "serve"]}},
          "generation_family": {"type": "string", "enum": ["lathe", "extrude", "composite", "articulated", "appliance", "unknown"]}
        }
      }
    }
  }
}
```

### 8.2 Tool physics + affordances (Sonnet, from spec or renders)
```json
{
  "type": "object",
  "additionalProperties": false,
  "required": ["tool_id", "display_name", "dimensions_m", "up_axis", "parts", "grips", "overall_material", "is_hollow_container", "confidence", "notes"],
  "properties": {
    "tool_id": {"type": "string"},
    "display_name": {"type": "string"},
    "dimensions_m": {
      "type": "object", "additionalProperties": false,
      "required": ["x", "y", "z"],
      "description": "Real-world bounding box in metres, y = up. Validated against size_class priors.",
      "properties": {"x": {"type": "number"}, "y": {"type": "number"}, "z": {"type": "number"}}
    },
    "up_axis": {"type": "string", "enum": ["+x", "-x", "+y", "-y", "+z", "-z"]},
    "overall_material": {"type": "string", "enum": ["granite", "marble", "ceramic", "porcelain", "cast_iron", "stainless_steel", "aluminum", "copper", "carbon_steel", "wood_hard", "wood_soft", "bamboo", "silicone", "plastic_hard", "glass", "cloth"]},
    "is_hollow_container": {"type": "boolean"},
    "parts": {
      "type": "array",
      "items": {
        "type": "object", "additionalProperties": false,
        "required": ["part_id", "region_ref", "material", "functions", "collider_hint", "wall_thickness_m", "edge_sharpness", "heat_conductivity", "max_safe_temp_c"],
        "properties": {
          "part_id": {"type": "string", "description": "e.g. 'bowl', 'head', 'handle', 'blade_edge'"},
          "region_ref": {"type": "string", "description": "ID of a labeled mesh region / parametric part supplied in the prompt"},
          "material": {"type": "string", "description": "same enum vocabulary as overall_material"},
          "functions": {"type": "array", "items": {"type": "string", "enum": ["cut", "crush", "grind", "stir", "whisk", "scoop", "pour", "contain", "heat_transfer", "flip", "grate", "press", "roll", "pierce", "strain", "grip", "none"]}},
          "collider_hint": {"type": "string", "enum": ["capsule", "box", "sphere", "convex_single", "convex_decompose", "hollow_ring_segments"]},
          "wall_thickness_m": {"type": "number", "description": "0 if solid. Used for mass = density x shell volume."},
          "edge_sharpness": {"type": "string", "enum": ["none", "dull", "serrated", "sharp", "razor"]},
          "heat_conductivity": {"type": "string", "enum": ["insulating", "low", "medium", "high"]},
          "max_safe_temp_c": {"type": "number", "description": "e.g. silicone 230, plastic 100, steel 800"}
        }
      }
    },
    "grips": {
      "type": "array",
      "description": "Where a human hand should hold it, for glove grasp snapping and robot-data labels.",
      "items": {
        "type": "object", "additionalProperties": false,
        "required": ["part_id", "grip_type", "position_m", "axis", "hand"],
        "properties": {
          "part_id": {"type": "string"},
          "grip_type": {"type": "string", "enum": ["power_cylindrical", "precision_pinch", "tripod", "hook", "palm_press", "lateral_pinch"]},
          "position_m": {"type": "array", "items": {"type": "number"}, "description": "[x,y,z] in tool-local metres"},
          "axis": {"type": "array", "items": {"type": "number"}, "description": "unit vector along which the fingers wrap"},
          "hand": {"type": "string", "enum": ["either", "dominant", "non_dominant", "both"]}
        }
      }
    },
    "confidence": {"type": "string", "enum": ["low", "medium", "high"]},
    "notes": {"type": "string", "description": "one sentence; anything odd about the mesh"}
  }
}
```
**Computed in code, not by the LLM:** `mass_kg` (material density table × volume, or shell volume if hollow), `friction_static/dynamic` and `restitution` (from a material table), colliders (from `collider_hint` + CoACD), inertia tensor (Unity computes it from the colliders).

### 8.3 Dish interpretation (Sonnet)
Input (game → sidecar → Claude, as JSON text + 1 image):
```json
{
  "dish_session_id": "s_0142",
  "duration_s": 912,
  "final_plate": {"vessel": "shallow_bowl", "temp_c": 61, "components": ["rice", "egg", "scallion", "chili_oil"]},
  "ingredients": [
    {"id": "egg", "amount_g": 110, "state": "fried", "doneness": 0.7, "in_final_plate": true},
    {"id": "garlic", "amount_g": 18, "state": "crushed_then_fried", "doneness": 0.9, "in_final_plate": true}
  ],
  "actions": [
    {"t": 12, "verb": "crush", "tool": "mortar_and_pestle#gen", "target": ["garlic", "chili"], "strokes": 31},
    {"t": 140, "verb": "fry", "tool": "wok", "target": ["garlic"], "heat_c": 190, "dur_s": 75}
  ],
  "generated_tools_used": ["mortar_and_pestle"],
  "mishaps": ["dropped one egg on floor"],
  "player_profile_ref": "cached_block_2"
}
```
Output schema:
```json
{
  "type": "object",
  "additionalProperties": false,
  "required": ["name_candidates", "name", "name_reason", "course", "cuisine_influences", "description", "flavor_profile", "technique_highlights", "personal_callback", "host_line"],
  "properties": {
    "name_candidates": {"type": "array", "items": {"type": "string"}, "description": "exactly 3, 2-6 words each"},
    "name": {"type": "string"},
    "name_reason": {"type": "string", "description": "<= 25 words, shown in cookbook"},
    "course": {"type": "string", "enum": ["amuse_bouche", "appetizer", "soup", "salad", "main", "side", "dessert", "drink", "snack", "breakfast", "sauce_condiment", "bread_baked", "experimental"]},
    "cuisine_influences": {"type": "array", "items": {"type": "string"}},
    "description": {"type": "string", "description": "2 sentences, cookbook voice"},
    "flavor_profile": {
      "type": "object", "additionalProperties": false,
      "required": ["salty", "sweet", "sour", "bitter", "umami", "spicy", "richness"],
      "description": "each 0-5 integer, validated in code",
      "properties": {"salty": {"type": "integer"}, "sweet": {"type": "integer"}, "sour": {"type": "integer"}, "bitter": {"type": "integer"}, "umami": {"type": "integer"}, "spicy": {"type": "integer"}, "richness": {"type": "integer"}}
    },
    "technique_highlights": {"type": "array", "items": {"type": "string"}},
    "personal_callback": {"type": "string", "description": "reference to this player's history or habits; empty string if none fits"},
    "host_line": {"type": "string", "description": "<= 20 words, spoken by the host via TTS"}
  }
}
```

### 8.4 Unlock generation (Opus)
```json
{
  "type": "object",
  "additionalProperties": false,
  "required": ["unlocked_course", "recipe", "why_for_you", "suggested_new_tool"],
  "properties": {
    "unlocked_course": {
      "type": "object", "additionalProperties": false,
      "required": ["course_id", "course_name", "is_new_course"],
      "properties": {"course_id": {"type": "string"}, "course_name": {"type": "string"}, "is_new_course": {"type": "boolean"}}
    },
    "recipe": {
      "type": "object", "additionalProperties": false,
      "required": ["title", "course", "difficulty", "ingredients", "loose_steps", "stretch_goal"],
      "properties": {
        "title": {"type": "string"},
        "course": {"type": "string"},
        "difficulty": {"type": "string", "enum": ["easy", "medium", "hard", "chaotic"]},
        "ingredients": {"type": "array", "items": {"type": "object", "additionalProperties": false, "required": ["id", "amount", "unit"], "properties": {"id": {"type": "string", "description": "must be a known ingredient id from the ontology"}, "amount": {"type": "number"}, "unit": {"type": "string", "enum": ["g", "ml", "pcs", "pinch", "to_taste"]}}}},
        "loose_steps": {"type": "array", "items": {"type": "string"}, "description": "suggestions, not rules (paidia): 3-7 items"},
        "stretch_goal": {"type": "string", "description": "optional playful challenge"}
      }
    },
    "why_for_you": {"type": "string", "description": "1-2 sentences tying it to the player's past dishes/style"},
    "suggested_new_tool": {"type": "string", "description": "a tool this recipe invites them to prompt for; empty string if none"}
  }
}
```
Validate that ingredient ids exist in the ontology. If not, re-ask once or drop the item.

---

## 9. Open risks / next experiments
1. **Measure** Claude latency (Haiku/Sonnet/Opus with your real prompts, effort `low`/`medium`) from this Mac. The numbers here are estimates.
2. **Benchmark** TRELLIS.2 (fal @512) vs Meshy t2 vs Tripo P2 on 20 kitchen tools: wall-clock, polycount, scale, and whether the hollow mortar comes out hollow.
3. **Time** CoACD on decimated 10k/30k-tri meshes on the M2.
4. **Prototype** the C# lathe builder + Sonnet parametric spec for 10 tools. It's probably the highest-leverage piece.
5. **Licensing:** if the pantry ships with the game, keep only CC0/CC-BY assets and generate an attribution file. Check that YCB and the generated-output terms (Meshy/Tripo/fal) allow commercial use [UNVERIFIED].
6. **Robot data:** GSO and YCB objects overlap with robotics datasets. Using them in the pantry makes the glove demonstrations more transferable ([mujoco_scanned_objects](https://github.com/kevinzakka/mujoco_scanned_objects)).
