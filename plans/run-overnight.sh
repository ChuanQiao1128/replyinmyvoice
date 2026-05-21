#!/usr/bin/env bash
# Overnight orchestrator for replyinmyvoice commercialization sprint
# Usage:
#   cd /Users/qc/Desktop/CloudFlare
#   nohup bash plans/run-overnight.sh > plans/overnight.log 2>&1 &
#   disown
# Then sleep. Check plans/overnight.log and plans/issue-board.md in the morning.

set -uo pipefail
cd "$(dirname "$0")/.."

DIRECTIVE=plans/overnight-directive.md
LOG=plans/overnight.log
PROGRESS=plans/overnight-progress.md

MAX_ITERATIONS=${MAX_ITERATIONS:-25}    # max codex exec calls
MAX_HOURS=${MAX_HOURS:-7}                # max wall hours
COOLDOWN_SECONDS=${COOLDOWN_SECONDS:-20} # pause between iterations

START_TS=$(date +%s)
DEADLINE=$((START_TS + MAX_HOURS * 3600))

echo "=== Overnight run starting $(date -Iseconds) ===" | tee -a "$LOG"
echo "Limits: ${MAX_ITERATIONS} iterations OR ${MAX_HOURS} hours" | tee -a "$LOG"
echo "Directive: $DIRECTIVE" | tee -a "$LOG"

# Sanity check
if [ ! -f "$DIRECTIVE" ]; then
  echo "ERROR: directive file missing at $DIRECTIVE" | tee -a "$LOG"
  exit 1
fi
if [ ! -f plans/issue-board.md ]; then
  echo "ERROR: issue board missing — run plans/create-issues-direct.py first" | tee -a "$LOG"
  exit 1
fi

for iter in $(seq 1 "$MAX_ITERATIONS"); do
  NOW=$(date +%s)
  if [ "$NOW" -gt "$DEADLINE" ]; then
    echo "=== Reached time limit, stopping ===" | tee -a "$LOG"
    break
  fi

  PENDING=$(grep -c "| pending |" plans/issue-board.md 2>/dev/null || echo 0)
  if [ "$PENDING" -eq 0 ]; then
    echo "=== All issues done! No pending remaining. ===" | tee -a "$LOG"
    break
  fi

  echo "" | tee -a "$LOG"
  echo "─── Iteration $iter / $MAX_ITERATIONS at $(date -Iseconds)" | tee -a "$LOG"
  echo "    Pending issues: $PENDING" | tee -a "$LOG"
  echo "" | tee -a "$LOG"

  # Feed directive via stdin to codex exec
  # Override approval policy + sandbox + env inheritance via -c flags
  cat "$DIRECTIVE" | codex exec \
    -c 'approval_policy="never"' \
    -c 'sandbox_mode="workspace-write"' \
    -c 'shell_environment_policy.inherit="all"' \
    2>&1 | tee -a "$LOG"

  EXIT=${PIPESTATUS[0]}
  echo "" | tee -a "$LOG"
  echo "─── Iteration $iter exited with $EXIT at $(date -Iseconds)" | tee -a "$LOG"

  if [ "$EXIT" -ne 0 ]; then
    echo "    Non-zero exit, sleeping ${COOLDOWN_SECONDS}s before retry" | tee -a "$LOG"
  fi

  sleep "$COOLDOWN_SECONDS"
done

echo "" | tee -a "$LOG"
echo "=== Overnight run finished $(date -Iseconds) ===" | tee -a "$LOG"
echo "Total wall time: $(($(date +%s) - START_TS))s" | tee -a "$LOG"
echo "Final pending: $(grep -c '| pending |' plans/issue-board.md 2>/dev/null || echo '?')" | tee -a "$LOG"
echo "Final blocked: $(grep -cE '\| (BLOCKED|BLOCKED-WAITING-USER|BLOCKED-NETWORK) \|' plans/issue-board.md 2>/dev/null || echo '?')" | tee -a "$LOG"
echo "Final done:    $(grep -c '| done |' plans/issue-board.md 2>/dev/null || echo '?')" | tee -a "$LOG"
