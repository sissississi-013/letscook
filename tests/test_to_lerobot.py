import json
import sys
from pathlib import Path

import numpy as np
import pyarrow.parquet as pq

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))
import to_lerobot as tl  # noqa: E402

FIXTURE = ROOT / "tests" / "fixtures" / "sample-session.jsonl"


def test_basis_change_keeps_rotations_proper():
    # A 90° yaw about Unity's up axis (y) must become a 90° rotation about z (up) in the output frame.
    s = np.sin(np.pi / 4)
    yaw = (0.0, s, 0.0, s)
    r = tl.to_world_rot(yaw)
    assert np.isclose(np.linalg.det(r), 1.0)
    axis_angle_up = r @ np.array([1.0, 0, 0])
    assert np.allclose(np.abs(axis_angle_up), [0, 1, 0], atol=1e-9)
    assert np.allclose(r[2], [0, 0, 1], atol=1e-9)  # up stays up


def test_quaternion_round_trip():
    rng = np.random.default_rng(0)
    for _ in range(20):
        q = rng.normal(size=4)
        q /= np.linalg.norm(q)
        q2 = tl.mat_to_quat(tl.quat_to_mat(q))
        assert np.isclose(abs(np.dot(q, q2)), 1.0, atol=1e-9)


def test_unity_up_becomes_z_up():
    assert np.allclose(tl.to_world_pos([0.1, 0.9, 0.4]), [0.1, 0.4, 0.9])


def test_convert_fixture(tmp_path):
    info = tl.convert(FIXTURE, tmp_path, task="test")
    assert info["total_episodes"] == 1
    assert info["total_frames"] > 50
    table = pq.read_table(tmp_path / "data" / "chunk-000" / "episode_000000.parquet")
    assert table.num_rows == info["total_frames"]
    state = np.array(table.column("observation.state").to_pylist())
    action = np.array(table.column("action").to_pylist())
    assert state.shape[1] == 140
    assert np.allclose(action[:-1], state[1:], equal_nan=True)
    tracked = np.array(table.column("quality.tracking_ok").to_pylist())[:, 0]
    assert tracked.mean() > 0.9
    assert np.isnan(state[~tracked, :70]).all(), "untracked frames must be NaN, not zeros"
    ok = state[tracked]
    # Keypoint 0 is the wrist, so its wrist-frame position is the origin
    assert np.allclose(ok[:, 7:10], 0, atol=1e-5)
    # Wrist quaternions are unit length; fingertips sit 5–25 cm from the wrist (a fist is about 7 cm)
    assert np.allclose(np.linalg.norm(ok[:, 3:7], axis=1), 1, atol=1e-4)
    tip = np.linalg.norm(ok[:, 7 + 8 * 3: 7 + 8 * 3 + 3], axis=1)   # keypoint 8 = index tip
    assert ((tip > 0.05) & (tip < 0.25)).all()
    episodes = [json.loads(l) for l in (tmp_path / "meta" / "episodes.jsonl").read_text().splitlines()]
    assert any(e["event"] == "cut" for e in episodes[0]["events"])
    assert "tomato" in info["features"]["observation.environment_state"]["names"]


def test_plate_event_splits_episodes(tmp_path):
    lines = FIXTURE.read_text().splitlines()
    frames = [json.loads(l) for l in lines if '"type":"frame"' in l]
    mid = frames[len(frames) // 2]["t"]
    plate = json.dumps({"type": "event", "t": mid, "event": "plate", "subject": "plate", "detail": ""})
    session = tmp_path / "s.jsonl"
    session.write_text("\n".join(lines + [plate]) + "\n")
    info = tl.convert(session, tmp_path / "out")
    assert info["total_episodes"] == 2
