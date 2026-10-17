"""Convert a Unity session log (sessions/unity-*.jsonl) into a LeRobot-style dataset folder.

Usage: .venv/bin/python tools/to_lerobot.py sessions/unity-XXXX.jsonl out/letscook-dataset [--task "free play cooking"]

Layout follows LeRobot v2.1 (data/chunk-000/episode_XXXXXX.parquet + meta/info.json, episodes.jsonl, tasks.jsonl);
LeRobot's own `convert_dataset_v21_to_v30` script upgrades it to v3.

Frames: Unity is left-handed Y-up. Output world frame is right-handed Z-up (x right, y forward, z up), metres,
quaternions [x, y, z, w]. Per hand, observation.state = wrist pos (3) + wrist quat (4) + 21 keypoints in the
wrist frame (63) → 70; both hands → 140. action = observation.state at t+1.
Episodes are split at "plate" events (a served dish ends an episode); otherwise the whole session is one episode.
"""
import argparse
import json
import sys
from pathlib import Path

import numpy as np
import pyarrow as pa
import pyarrow.parquet as pq

SIDES = ("Left", "Right")
# Change of basis Unity (x right, y up, z forward) → (x right, y forward, z up): swap y and z (a reflection).
P = np.array([[1, 0, 0], [0, 0, 1], [0, 1, 0]], dtype=np.float64)


def quat_to_mat(q):
    x, y, z, w = q
    return np.array([
        [1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
        [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
        [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)],
    ])


def mat_to_quat(m):
    t = np.trace(m)
    if t > 0:
        s = np.sqrt(t + 1.0) * 2
        w, x, y, z = 0.25 * s, (m[2, 1] - m[1, 2]) / s, (m[0, 2] - m[2, 0]) / s, (m[1, 0] - m[0, 1]) / s
    elif m[0, 0] > m[1, 1] and m[0, 0] > m[2, 2]:
        s = np.sqrt(1.0 + m[0, 0] - m[1, 1] - m[2, 2]) * 2
        w, x, y, z = (m[2, 1] - m[1, 2]) / s, 0.25 * s, (m[0, 1] + m[1, 0]) / s, (m[0, 2] + m[2, 0]) / s
    elif m[1, 1] > m[2, 2]:
        s = np.sqrt(1.0 + m[1, 1] - m[0, 0] - m[2, 2]) * 2
        w, x, y, z = (m[0, 2] - m[2, 0]) / s, (m[0, 1] + m[1, 0]) / s, 0.25 * s, (m[1, 2] + m[2, 1]) / s
    else:
        s = np.sqrt(1.0 + m[2, 2] - m[0, 0] - m[1, 1]) * 2
        w, x, y, z = (m[1, 0] - m[0, 1]) / s, (m[0, 2] + m[2, 0]) / s, (m[1, 2] + m[2, 1]) / s, 0.25 * s
    q = np.array([x, y, z, w])
    return q / np.linalg.norm(q)


def to_world_pos(p):
    return P @ np.asarray(p, dtype=np.float64)


def to_world_rot(q):
    """Rotation in Unity's frame → same physical rotation expressed in the right-handed Z-up frame."""
    return P @ quat_to_mat(q) @ P


def hand_state(hand):
    """70 floats: wrist pos, wrist quat, 21 keypoints in the wrist frame. NaNs when the hand isn't tracked."""
    if not hand or not hand.get("ok"):
        return np.full(70, np.nan), np.full((21, 3), np.nan), False
    wrist = to_world_pos(hand["wrist"])
    rot = to_world_rot(hand["wrist_rot"])
    kp_world = np.array([to_world_pos(p) for p in hand["kp"]])
    kp_local = (kp_world - wrist) @ rot   # = R^T (kp - wrist) per row
    state = np.concatenate([wrist, mat_to_quat(rot), kp_local.reshape(-1)])
    return state, kp_world, True


def load(path):
    meta, frames, events = None, [], []
    with open(path) as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            rec = json.loads(line)
            kind = rec.get("type")
            if kind == "meta":
                meta = rec
            elif kind == "frame":
                frames.append(rec)
            elif kind == "event":
                events.append(rec)
    if meta is None or not frames:
        raise ValueError(f"{path}: no meta line or no frames")
    return meta, frames, events


def split_episodes(frames, events):
    cuts = sorted(e["t"] for e in events if e["event"] == "plate")
    episodes, current, i = [], [], 0
    for fr in frames:
        while i < len(cuts) and fr["t"] > cuts[i]:
            if current:
                episodes.append(current)
            current, i = [], i + 1
        current.append(fr)
    if current:
        episodes.append(current)
    return episodes


def convert(session_path, out_dir, task="free play cooking"):
    meta, frames, events = load(session_path)
    out = Path(out_dir)
    (out / "data" / "chunk-000").mkdir(parents=True, exist_ok=True)
    (out / "meta").mkdir(parents=True, exist_ok=True)
    fps = float(meta.get("fps", 30))

    object_names = {}
    for fr in frames:
        for o in fr["objects"]:
            object_names.setdefault(o["id"], o["name"])
    K = max(object_names) + 1 if object_names else 0

    episodes_meta, index = [], 0
    for ep_idx, ep in enumerate(split_episodes(frames, events)):
        t0 = ep[0]["t"]
        states, kp_world, held, assisted, tracked, env, labels = [], [], [], [], [], [], []
        for fr in ep:
            s, k, h_held, h_assist, h_ok = [], [], [], [], []
            for side in SIDES:
                hand = fr["hands"].get(side)
                st, kw, ok = hand_state(hand)
                s.append(st); k.append(kw); h_ok.append(ok)
                h_held.append(hand.get("held", -1) if ok else -1)
                h_assist.append(bool(hand.get("assisted", False)) if ok else False)
            states.append(np.concatenate(s)); kp_world.append(np.stack(k))
            held.append(h_held); assisted.append(h_assist); tracked.append(h_ok)
            pose = np.full((K, 7), np.nan)
            for o in fr["objects"]:
                pose[o["id"]] = np.concatenate([to_world_pos(o["pos"]), mat_to_quat(to_world_rot(o["rot"]))])
            env.append(pose.reshape(-1))
            labels.append(json.dumps({o["name"] + f"#{o['id']}": o["food"]["doneness"] for o in fr["objects"] if "food" in o}))

        states = np.array(states, dtype=np.float32)
        actions = np.vstack([states[1:], states[-1:]])
        n = len(ep)
        table = pa.table({
            "timestamp": pa.array([fr["t"] - t0 for fr in ep], pa.float32()),
            "frame_index": pa.array(range(n), pa.int64()),
            "episode_index": pa.array([ep_idx] * n, pa.int64()),
            "index": pa.array(range(index, index + n), pa.int64()),
            "task_index": pa.array([0] * n, pa.int64()),
            "observation.state": pa.array(list(states), pa.list_(pa.float32(), 140)),
            "action": pa.array(list(actions), pa.list_(pa.float32(), 140)),
            "observation.state.hand_keypoints": pa.array([k.reshape(-1).astype(np.float32) for k in kp_world], pa.list_(pa.float32(), 126)),
            "observation.environment_state": pa.array([e.astype(np.float32) for e in env], pa.list_(pa.float32(), K * 7)),
            "observation.held_object": pa.array(held, pa.list_(pa.int16(), 2)),
            "quality.assist_active": pa.array(assisted, pa.list_(pa.bool_(), 2)),
            "quality.tracking_ok": pa.array(tracked, pa.list_(pa.bool_(), 2)),
            "quality.glove_live": pa.array([bool(fr.get("live")) for fr in ep], pa.bool_()),
            "annotation.food_state": pa.array(labels, pa.string()),
        })
        pq.write_table(table, out / "data" / "chunk-000" / f"episode_{ep_idx:06d}.parquet")
        ep_events = [e for e in events if ep[0]["t"] <= e["t"] <= ep[-1]["t"]]
        episodes_meta.append({
            "episode_index": ep_idx,
            "tasks": [task],
            "length": n,
            "session_id": meta["session_id"],
            "duration_s": round(ep[-1]["t"] - t0, 3),
            "events": [{"t": round(e["t"] - t0, 3), "event": e["event"], "subject": e["subject"], "detail": e["detail"]} for e in ep_events],
            "quality": {
                "assist_fraction": float(np.mean(np.array(assisted))) if n else 0.0,
                "glove_live_fraction": float(np.mean([bool(fr.get("live")) for fr in ep])),
            },
        })
        index += n

    joint_names = []
    for side in SIDES:
        joint_names += [f"{side}.wrist.{a}" for a in ("x", "y", "z", "qx", "qy", "qz", "qw")]
        joint_names += [f"{side}.{kp}.{a}" for kp in meta["keypoints"] for a in ("x", "y", "z")]
    info = {
        "codebase_version": "v2.1",
        "robot_type": "human_hands_rokoko_smartgloves",
        "fps": fps,
        "total_episodes": len(episodes_meta),
        "total_frames": index,
        "total_tasks": 1,
        "chunks_size": 1000,
        "data_path": "data/chunk-{episode_chunk:03d}/episode_{episode_index:06d}.parquet",
        "coordinate_frame": "right-handed, Z-up, metres; quaternions xyzw; converted from Unity (left-handed Y-up) by swapping y/z",
        "features": {
            "observation.state": {"dtype": "float32", "shape": [140], "names": joint_names},
            "action": {"dtype": "float32", "shape": [140], "names": joint_names, "note": "observation.state at t+1"},
            "observation.state.hand_keypoints": {"dtype": "float32", "shape": [2, 21, 3], "names": ["hand", "keypoint (MediaPipe order)", "xyz"], "note": "world frame"},
            "observation.environment_state": {"dtype": "float32", "shape": [K, 7], "names": [object_names.get(i, f"obj{i}") for i in range(K)], "note": "NaN when the object doesn't exist yet (e.g. a later cut piece)"},
            "observation.held_object": {"dtype": "int16", "shape": [2], "note": "object id per hand, -1 = none"},
            "quality.assist_active": {"dtype": "bool", "shape": [2], "note": "game attached the object (assisted grasp): motion is less physically valid"},
            "quality.tracking_ok": {"dtype": "bool", "shape": [2]},
            "quality.glove_live": {"dtype": "bool", "shape": [1], "note": "false = built-in mock animation, not a human"},
            "annotation.food_state": {"dtype": "string", "shape": [1], "note": "JSON: object → doneness"},
            "timestamp": {"dtype": "float32", "shape": [1]},
            "frame_index": {"dtype": "int64", "shape": [1]},
            "episode_index": {"dtype": "int64", "shape": [1]},
            "index": {"dtype": "int64", "shape": [1]},
            "task_index": {"dtype": "int64", "shape": [1]},
        },
        "source": {"session": meta["session_id"], "unity": meta.get("unity"), "keypoint_joints": meta["keypoints"]},
    }
    (out / "meta" / "info.json").write_text(json.dumps(info, indent=2))
    with open(out / "meta" / "episodes.jsonl", "w") as f:
        for e in episodes_meta:
            f.write(json.dumps(e) + "\n")
    (out / "meta" / "tasks.jsonl").write_text(json.dumps({"task_index": 0, "task": task}) + "\n")
    return info


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("session")
    ap.add_argument("out")
    ap.add_argument("--task", default="free play cooking")
    args = ap.parse_args()
    info = convert(args.session, args.out, args.task)
    print(f"wrote {info['total_episodes']} episode(s), {info['total_frames']} frames → {args.out}")


if __name__ == "__main__":
    sys.exit(main())
