#!/usr/bin/env bash
# Compile check (and optional editor method) in Unity batchmode.
# Usage: scripts/unity-check.sh [Fully.Qualified.Method] [extra unity args...]
# Requires the Unity Editor to be CLOSED for this project.
set -uo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
VERSION="$(sed -n 's/^m_EditorVersion: //p' "$ROOT/ProjectSettings/ProjectVersion.txt")"
UNITY="${UNITY_PATH:-/Applications/Unity/Hub/Editor/$VERSION/Unity.app/Contents/MacOS/Unity}"
LOG="$ROOT/Logs/unity-check.log"
ARGS=(-batchmode -nographics -quit -projectPath "$ROOT" -logFile "$LOG")
if [[ $# -gt 0 ]]; then ARGS+=(-executeMethod "$1"); shift; fi
"$UNITY" "${ARGS[@]}" "$@"
CODE=$?
ERRORS="$(grep -E "error CS[0-9]+|Assembly has |Scripts have compiler errors|executeMethod.*(failed|not found)|^\[Template\] ERROR" "$LOG" | sort -u)"
grep -E "^\[Template\]" "$LOG" | sort -u
if [[ $CODE -ne 0 || -n "$ERRORS" ]]; then
  echo "$ERRORS"
  echo "FAILED (exit $CODE). Full log: $LOG"
  exit 1
fi
echo "OK: compiled with zero errors."
