"""Retarget recorded human hands onto a robot hand (default: LEAP Hand) with dex-retargeting (MIT).

Usage: .venv-robot/bin/python tools/retarget.py datasets/letscook-001 [--robot leap_hand] [--type dexpilot]
Adds `action.retargeted.<robot>_left` / `_right` (robot joint positions, radians) to every episode parquet of a
dataset made by tools/to_lerobot.py, and records them in meta/info.json.

Needs the separate Python 3.12 env (.venv-robot: dex_retargeting + torch) and the robot URDFs in
models/dex-urdf/robots/hands (git clone https://github.com/dexsuite/dex-urdf).
The hand frame is estimated from the wrist/index/middle keypoints exactly like dex-retargeting's own MediaPipe
example, then rotated into the MANO convention the configs expect.
"""
import argparse
import json
import sys
from pathlib import Path

import numpy as np
import pyarrow as pa
import pyarrow.parquet as pq

ROOT = Path(__file__).resolve().parents[1]
URDF_DIR = ROOT / "models" / "dex-urdf" / "robots" / "hands"


def estimate_frame(kp):
    """Wrist orientation (3×3, columns = MANO-style axes) from 21 keypoints, as in dex-retargeting's example."""
    points = kp[[0, 5, 9], :]
    x_vector = points[0] - points[2]
    centred = points - points.mean(axis=0, keepdims=True)
    normal = np.linalg.svd(centred)[2][2, :]
    x = x_vector - np.dot(x_vector, normal) * normal
    x /= np.linalg.norm(x)
    z = np.cross(x, normal)
    if np.dot(z, centred[1] - centred[2]) < 0:
        normal, z = -normal, -z
    return np.stack([x, normal, z], axis=1)


def to_mano(kp, side):
    from dex_retargeting.constants import OPERATOR2MANO, HandType
    kp = kp - kp[0:1]
    return kp @ estimate_frame(kp) @ OPERATOR2MANO[HandType.right if side == "right" else HandType.left]


def build(robot, kind, side):
    from dex_retargeting.constants import HandType, RetargetingType, RobotName, get_default_config_path
    from dex_retargeting.retargeting_config import RetargetingConfig
    RetargetingConfig.set_default_urdf_dir(str(URDF_DIR))
    path = get_default_config_path(RobotName[robot], RetargetingType[kind],
                                   HandType.right if side == "right" else HandType.left)
    return RetargetingConfig.load_from_file(path).build()


def ref_value(retargeting, joint_pos):
    indices = retargeting.optimizer.target_link_human_indices
    if retargeting.optimizer.retargeting_type == "POSITION":
        return joint_pos[indices, :]
    return joint_pos[indices[1, :], :] - joint_pos[indices[0, :], :]


def retarget_episode(table, retargeters):
    kp_all = np.array(table.column("observation.state.hand_keypoints").to_pylist(), dtype=np.float64).reshape(-1, 2, 21, 3)
    out = {}
    for h, side in enumerate(("left", "right")):
        rt = retargeters[side]
        dof = len(rt.joint_names)
        q = np.full((len(kp_all), dof), np.nan, dtype=np.float32)
        last = None
        for i, kp in enumerate(kp_all[:, h]):
            if np.isnan(kp).any():
                if last is not None:
                    q[i] = last   # hold the last pose through tracking gaps
                continue
            last = rt.retarget(ref_value(rt, to_mano(kp, side))).astype(np.float32)
            q[i] = last
        out[side] = (q, list(rt.joint_names))
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("dataset")
    ap.add_argument("--robot", default="leap", help="leap, allegro, shadow, inspire, ability, svh")
    ap.add_argument("--type", default="dexpilot", choices=["dexpilot", "vector", "position"])
    args = ap.parse_args()

    from dex_retargeting.constants import RobotName
    robot = {"leap": "leap", "allegro": "allegro", "shadow": "shadow", "inspire": "inspire", "ability": "ability", "svh": "svh"}[args.robot]
    if robot not in RobotName.__members__:
        sys.exit(f"unknown robot {args.robot}; available: {list(RobotName.__members__)}")
    kind = args.type
    ds = Path(args.dataset)
    info_path = ds / "meta" / "info.json"
    info = json.loads(info_path.read_text())

    names = {}
    for ep in sorted((ds / "data" / "chunk-000").glob("episode_*.parquet")):
        retargeters = {side: build(robot, kind, side) for side in ("left", "right")}   # fresh filters per episode
        table = pq.read_table(ep)
        result = retarget_episode(table, retargeters)
        for side, (q, joint_names) in result.items():
            col = f"action.retargeted.{args.robot}_{side}"
            if col in table.column_names:
                table = table.drop([col])
            table = table.append_column(col, pa.array(list(q), pa.list_(pa.float32(), q.shape[1])))
            names[col] = joint_names
        pq.write_table(table, ep)
        print(f"{ep.name}: {table.num_rows} frames retargeted to {args.robot} ({kind})")

    for col, joint_names in names.items():
        info["features"][col] = {"dtype": "float32", "shape": [len(joint_names)], "names": joint_names,
                                 "note": f"dex-retargeting {kind} onto the {args.robot} hand (radians)"}
    info_path.write_text(json.dumps(info, indent=2))


if __name__ == "__main__":
    main()
