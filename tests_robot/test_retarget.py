"""Run with the robot env: .venv-robot/bin/python -m pytest tests_robot"""
import json
import shutil
import sys
from pathlib import Path

import numpy as np
import pyarrow.parquet as pq

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))
import retarget  # noqa: E402
import to_lerobot  # noqa: E402

FIXTURE = ROOT / "tests" / "fixtures" / "sample-session.jsonl"


def test_frame_is_orthonormal_and_right_handed():
    table = pq.read_table(_dataset(Path("/tmp/lc-rt-frame")) / "data" / "chunk-000" / "episode_000000.parquet")
    kp = np.array(table.column("observation.state.hand_keypoints").to_pylist()[10]).reshape(2, 21, 3)[1]
    f = retarget.estimate_frame(kp - kp[0])
    assert np.allclose(f.T @ f, np.eye(3), atol=1e-6)
    assert np.isclose(np.linalg.det(f), 1.0)


def _dataset(tmp):
    if tmp.exists():
        shutil.rmtree(tmp)
    to_lerobot.convert(FIXTURE, tmp)
    return tmp


def test_retarget_adds_leap_joints_and_fist_bends_more_than_open(tmp_path, monkeypatch):
    ds = _dataset(tmp_path / "ds")
    monkeypatch.setattr(sys, "argv", ["retarget.py", str(ds)])
    retarget.main()

    info = json.loads((ds / "meta" / "info.json").read_text())
    col = "action.retargeted.leap_right"
    assert info["features"][col]["shape"] == [16]

    table = pq.read_table(ds / "data" / "chunk-000" / "episode_000000.parquet")
    q = np.array(table.column(col).to_pylist(), dtype=float)
    ok = ~np.isnan(q).any(axis=1)
    assert ok.mean() > 0.9

    # The mock session cycles open → fist; the hand closes when the human index finger curls.
    kp = np.array(table.column("observation.state.hand_keypoints").to_pylist()).reshape(-1, 2, 21, 3)[:, 1]
    reach = np.linalg.norm(kp[:, 8] - kp[:, 0], axis=1)        # index tip to wrist
    open_i, fist_i = np.nanargmax(np.where(ok, reach, np.nan)), np.nanargmin(np.where(ok, reach, np.nan))
    assert np.abs(q[fist_i]).sum() > np.abs(q[open_i]).sum() + 0.5, "robot joints should flex more for a fist"
