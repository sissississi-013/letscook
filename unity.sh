#!/bin/bash
# Batch-mode helpers for the LetsCook Unity project.
#   ./unity.sh build              rebuild Assets/Scenes/Kitchen.unity from code
#   ./unity.sh shot <pose> <name> [yes] render (yes = stage cooked food) shots/<name>.png with a mock pose (open|fist|pinch|point)
#   ./unity.sh test               run EditMode tests → logs/tests.xml
set -euo pipefail
cd "$(dirname "$0")"
UNITY=/Applications/Unity/Hub/Editor/6000.2.9f1/Unity.app/Contents/MacOS/Unity
PROJECT="$PWD/LetsCook"
mkdir -p logs

if [ -e "$PROJECT/Temp/UnityLockfile" ] && pgrep -f "projectPath.*LetsCook" >/dev/null 2>&1; then
  echo "LetsCook is open in the Unity Editor; close it to use batch mode." >&2
  exit 2
fi

check() {
  if grep -E "error CS|Scripts have compiler errors|executeMethod.*(failed|could not)" "$1" >/dev/null; then
    grep -E "error CS|compiler errors|Exception" "$1" | head -20 >&2
    exit 1
  fi
}

case "${1:-}" in
  build)
    "$UNITY" -batchmode -quit -projectPath "$PROJECT" -executeMethod LetsCook.EditorTools.SceneBuilder.Build -logFile logs/build.log || { check logs/build.log; tail -20 logs/build.log; exit 1; }
    check logs/build.log; grep "\[let's cook\]" logs/build.log ;;
  shot)
    "$UNITY" -batchmode -quit -projectPath "$PROJECT" -executeMethod LetsCook.EditorTools.SceneBuilder.Shot -shotPose "${2:-pinch}" -shotName "${3:-shot}" -shotCook "${4:-no}" -shotTools "${5:-no}" -logFile logs/shot.log || { check logs/shot.log; tail -20 logs/shot.log; exit 1; }
    check logs/shot.log; grep "\[let's cook\]" logs/shot.log ;;
  test)
    rm -f logs/tests-*.xml
    for mode in EditMode PlayMode; do
      "$UNITY" -batchmode -projectPath "$PROJECT" -runTests -testPlatform $mode -testResults "$PWD/logs/tests-$mode.xml" -logFile logs/test-$mode.log || true
      check logs/test-$mode.log
    done
    python3 - <<'EOF'
import glob, xml.etree.ElementTree as ET
bad = 0
for path in sorted(glob.glob("logs/tests-*.xml")):
  r = ET.parse(path).getroot()
  print(f"{path}: {r.get('total')} total, {r.get('passed')} passed, {r.get('failed')} failed")
  bad += int(r.get("failed")) + (1 if r.get("total") == "0" else 0)
  for c in r.iter("test-case"):
    if c.get("result") != "Passed":
      msg = c.find("failure/message")
      print("FAIL", c.get("fullname"), (msg.text or "").strip()[:300] if msg is not None else "")
raise SystemExit(1 if bad else 0)
EOF
    ;;
  *) echo "usage: $0 build | shot <pose> <name> | test" >&2; exit 64 ;;
esac
