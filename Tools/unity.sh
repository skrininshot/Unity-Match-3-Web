#!/usr/bin/env bash
# Helper wrappers around the Unity CLI for this project.
#
#   ./Tools/unity.sh tests EditMode      run EditMode tests
#   ./Tools/unity.sh tests PlayMode      run PlayMode tests
#   ./Tools/unity.sh method Ns.Type.Fn   run an editor static method in batchmode
#   ./Tools/unity.sh compile             import + compile only
#
# Logs land in Temp/ (git-ignored). Exit code is Unity's own exit code.
set -uo pipefail

UNITY="/c/Program Files/Unity/Hub/Editor/6000.3.18f1/Editor/Unity.exe"
PROJECT="C:/Users/User/Documents/Unity Projects/Playrix-Match3-Test"
# NOTE: must not live in Temp/ — Unity wipes that directory on shutdown,
# which silently deletes the test-results XML we are about to read.
LOGDIR="$PROJECT/Artifacts/ci"
mkdir -p "$LOGDIR"

cmd="${1:-}"
shift || true

case "$cmd" in
  tests)
    platform="${1:-EditMode}"
    log="$LOGDIR/tests-$platform.log"
    results="$LOGDIR/results-$platform.xml"
    rm -f "$results"
    # -nographics is omitted for PlayMode so rendering-dependent tests (screenshots) work.
    # The screen size is pinned because CanvasScaler works off Screen.width/height, so without it
    # the UI would be laid out for whatever default the batchmode window happens to have.
    graphics=(-nographics)
    [ "$platform" = "PlayMode" ] && graphics=(-screen-width 1280 -screen-height 720 -screen-fullscreen 0)
    "$UNITY" -batchmode "${graphics[@]}" -projectPath "$PROJECT" \
      -runTests -testPlatform "$platform" -testResults "$results" \
      -logFile "$log" -accept-apiupdate
    code=$?
    echo "--- unity exit code: $code ---"
    if [ -f "$results" ]; then
      python "$PROJECT/Tools/summarize_tests.py" "$results"
    else
      echo "!! no test results produced; tail of log:"
      tail -n 60 "$log"
    fi
    exit $code
    ;;
  method)
    method="${1:?method name required}"
    log="$LOGDIR/method.log"
    "$UNITY" -batchmode -quit -nographics -projectPath "$PROJECT" \
      -executeMethod "$method" -logFile "$log" -accept-apiupdate
    code=$?
    echo "--- unity exit code: $code ---"
    grep -E "^\[TOOL\]|error CS|Exception|Error building|BuildFailed" "$log" | head -n 80
    [ $code -ne 0 ] && tail -n 60 "$log"
    exit $code
    ;;
  method-graphics)
    method="${1:?method name required}"
    log="$LOGDIR/method-graphics.log"
    "$UNITY" -batchmode -quit -projectPath "$PROJECT" \
      -executeMethod "$method" -logFile "$log" -accept-apiupdate
    code=$?
    echo "--- unity exit code: $code ---"
    grep -E "^\[TOOL\]|error CS|Exception|Error building|BuildFailed" "$log" | head -n 80
    [ $code -ne 0 ] && tail -n 60 "$log"
    exit $code
    ;;
  compile)
    log="$LOGDIR/compile.log"
    "$UNITY" -batchmode -quit -nographics -projectPath "$PROJECT" \
      -logFile "$log" -accept-apiupdate
    code=$?
    echo "--- unity exit code: $code ---"
    grep -E "error CS|warning CS|Compilation failed|Exception" "$log" | sort -u | head -n 80
    [ $code -ne 0 ] && tail -n 60 "$log"
    exit $code
    ;;
  *)
    echo "usage: $0 {tests [EditMode|PlayMode]|method <Type.Method>|method-graphics <Type.Method>|compile}"
    exit 64
    ;;
esac
