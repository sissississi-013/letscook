# Build plan (draft, 2026-10-17)

Synthesized from the four reports in `research/`. Claude's recommendations; nothing here has been approved by the user yet. Items marked **DECIDE** need the user.

## The stack

| Layer | Choice | Why |
|---|---|---|
| Glove data | Rokoko Studio (Pro plan) → Custom Streaming, **JSON v4, uncompressed**, UDP `127.0.0.1:14043` | v4 has metacarpals + hierarchy (better for robots). Uncompressed avoids the LZ4 bugs. |
| Raw logger | `receiver.py` (Python) writes every packet with timestamps | Data is saved from day one, before the game exists |
| Game | **Unity 6 (6000.2.9f1, already installed)** with **our own C# UDP receiver** | Native on M2, best hand/food physics add-ons. Own receiver skips the Rokoko plugin's Intel-only LZ4 library and its v3-only parser. Fallback: the plugin on Unity 2022.3.62f2 (also installed) for a quick sanity check. |
| Display | Desktop monitor, fixed camera over the counter | No VR on Mac (SteamVR and Quest Link don't support macOS) |
| Hands | Custom physics hand driven by glove joint angles, adapted to Unity XR Hands format. Fallback: Auto Hand ($90) | Free-form grasping, no snap-to-pose |
| Grasping | Thumb + opposing finger contact, curl hysteresis (grab 0.8 / release 0.7), breakable attach joint; "virtual grip force" from how far the glove finger sinks past the sim finger | No force feedback, so the grasp has to be inferred |
| Cutting | EzySlice (free) with a cap on pieces | Plane cuts, convex meshes |
| Liquids / egg / rice | Obi Fluid ($60); per-particle temperature + color | Sauces can hold heat and mix |
| Cooking realism | Per-piece continuous state: surface/core temperature, moisture, protein set, browning, char, seasoning + an action history. No recipes. | This is what makes it feel real *and* free |
| Dish naming + unlocks | Claude: Sonnet 5.5 names/classifies, Opus 5.5 writes personalized unlocks during the reveal, Haiku 5.5 for quick host lines. Structured outputs, prompt caching. | See research/ai-generation.md |
| Unlimited tools | 3 tiers: library lookup (~2 s) → Claude spec + Unity procedural build from tested parts (~3–6 s) → background text-to-3D upgrade, TRELLIS.2 / Meshy (~30–90 s, ~$0.30) | Textured text-to-3D is never instant |
| Voice | Apple SpeechAnalyzer (offline) with a glove gesture as push-to-talk | Hands are in gloves |
| Robot data | Raw log → convert to **LeRobot v3** (30 fps), 21 keypoints + 6-DoF wrist per hand; render egocentric + third-person video; flag every assisted/snapped grasp frame | Matches DexCap, Physical Intelligence, EgoMimic setups |
| Robot side | dex-retargeting (MIT) → LEAP/Allegro/Inspire hands in MuJoCo; align task labels with RoboCasa365 (runs on M2) | Offline, after the game |

## Biggest blocker: where the hands are in space
The gloves only give finger and wrist *rotation*. Options that work on a Mac:

| Option | Cost | Quality | Notes |
|---|---|---|---|
| Fixed wrists ("hands resting at the counter") | $0 | Game-only | Fine for the first prototype |
| Webcam + MediaPipe | $0 | Rough, no true depth | Good enough for an MVP |
| **Coil Pro** | $2,995 | Best, built into Studio | Keep about 1 m from hands; **real steel pans nearby cause problems** |
| Apple Vision Pro + Tracking Streamer | Borrow | Good | Unconfirmed with gloves on |

**DECIDE:** which one. Recommendation: fixed wrists → webcam for MVP → Coil Pro (borrow/sponsor) for publishable robot data.

## Phases
0. **Hardware + raw data.** Install Studio, start Pro trial, pair gloves, calibrate, run `receiver.py`, save the first raw sessions. *Done when:* a 1-minute session file with finger data exists.
1. **Hands in Unity.** C# receiver, glove → hand rig, fixed wrists. *Done when:* you wiggle fingers and the virtual hand matches.
2. **Grab anything.** Physics hand + grasp rules on simple objects. *Done when:* you can pick up, move, and drop a cube and a bowl reliably.
3. **First kitchen (MVP).** Counter, knife, cutting board, pan on a burner, bowl, plate, spatula, whisk; tomato, onion, egg, butter, bread. Slicing, frying with continuous cook state, cracking eggs, plating. **Week-1 spike first:** prove Obi Fluid + EzySlice run on Metal in Unity 6.
4. **The magic.** Claude names the plated dish, picks the course, writes a personalized unlock; cookbook page.
5. **Unlimited tools.** Voice request → library / procedural tool → background generated upgrade.
6. **Robot data out.** Raw → LeRobot v3 export, video renders, retarget to a robot hand in MuJoCo, a 10-episode sample on Hugging Face.
7. **Charm.** Host character voice, cookbook images, wrist tracking upgrade.

Estimate from research: phases 1–3 are about 8–12 weeks for one experienced Unity developer. Building with Claude can go faster, but realistic physics tuning needs a lot of hands-on testing with you wearing the gloves.

## Budget
- Rokoko Studio Pro: $70/month (7-day trial first; Plus at $28/month streams but lacks CSV/triggers)
- Unity add-ons: Obi Fluid $60 (+ optional Obi bundle $179, Auto Hand $90)
- AI: about $0.30 per brand-new generated tool; Claude API per dish is cents
- Optional: Coil Pro $2,995

## Risks
- Rokoko Studio doesn't officially list macOS 26 (current builds very likely work, untested)
- Obi GPU mode on Metal untested; EzySlice unmaintained since 2023
- Glove drift during fast motion without a Coil; recalibrate every take
- Assisted grasps make data less physically valid, so they must be flagged
- Hand motion can identify people: explicit opt-in consent (draft in research/robot-data.md)

## Before writing code
- [ ] User picks: timeline, wrist-position option, budget OK
- [ ] Gloves pass phase 0
