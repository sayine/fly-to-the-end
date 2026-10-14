#!/usr/bin/env bash
# Usage: scripts/unity-test.sh [EditMode|PlayMode]. Runs tests in batchmode. Requires the Unity Editor to be CLOSED for this project.
set -uo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
VERSION="$(sed -n 's/^m_EditorVersion: //p' "$ROOT/ProjectSettings/ProjectVersion.txt")"
UNITY="${UNITY_PATH:-/Applications/Unity/Hub/Editor/$VERSION/Unity.app/Contents/MacOS/Unity}"
RESULTS="$ROOT/Logs/${1:-EditMode}-results.xml"
rm -f "$RESULTS"
"$UNITY" -batchmode -nographics -projectPath "$ROOT" -runTests -testPlatform "${1:-EditMode}" \
  -testResults "$RESULTS" -logFile "$ROOT/Logs/unity-test.log"
CODE=$?
if [[ -f "$RESULTS" ]]; then
  grep -o '<test-run [^>]*' "$RESULTS" | grep -oE '(total|passed|failed|skipped)="[0-9]+"' | tr '\n' ' '; echo
  grep -B1 -A4 'result="Failed"' "$RESULTS" | grep -E 'test-case|message' | sed 's/^ */  /' | head -40
else
  echo "No results file. See Logs/unity-test.log"; grep -E "error CS" "$ROOT/Logs/unity-test.log" | sort -u
fi
exit $CODE
