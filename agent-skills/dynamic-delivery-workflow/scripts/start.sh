#!/usr/bin/env bash
# start.sh — (re)launch ONE delivery wave: driver-loop + sentinel as nohup daemons.
#
# Survives Claude-session suspend/resume (work runs detached, reparented to launchd) AND keeps the
# control dir + worktrees OFF the iCloud-synced Desktop (postmortem bugs #3/#5: SSH:22 timeouts,
# resurrecting "dataless" global STOP, sync churn). The repo .git stays wherever it is; we only
# `git worktree add` into the off-iCloud control dir.
#
# Usage:
#   RIMV_WAVE_CONF=/path/to/wave.conf bash start.sh
#     (wave.conf must define REPO GHREPO BASE BRANCH_PREFIX CONTROL_DIR QUEUE BRIEF_DIR; the rest
#      have sane defaults. CONTROL_DIR should be under $HOME, NOT under the iCloud Desktop.)
#
# Idempotent: clears the wave-local STOP/WAVE_DONE, kills prior daemons for THIS wave (by pidfile),
# runs preflight (aborts on failure), publishes the integration base, launches the two daemons.
#
# STOP cleanly with:   touch "$CONTROL_DIR/STOP"
set -uo pipefail

CONF="${RIMV_WAVE_CONF:?start: set RIMV_WAVE_CONF to your wave.conf path}"
[ -f "$CONF" ] || { echo "start: wave.conf not found at $CONF" >&2; exit 2; }
CONF="$(cd "$(dirname "$CONF")" && pwd)/$(basename "$CONF")"   # absolutize
export RIMV_WAVE_CONF="$CONF"
. "$CONF"
: "${REPO:?}" "${GHREPO:?}" "${BASE:?}" "${BRANCH_PREFIX:?}" "${CONTROL_DIR:?}" "${QUEUE:?}" "${BRIEF_DIR:?}"

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
NOTIFY="$HERE/notify.sh"
note(){ bash "$NOTIFY" "$@" 2>/dev/null || true; }

mkdir -p "$CONTROL_DIR/logs" "$CONTROL_DIR/done" "$CONTROL_DIR/wt"
chmod +x "$HERE"/*.sh 2>/dev/null || true

# warn loudly if the control dir is on the iCloud-synced Desktop (the thing v2 exists to avoid)
case "$CONTROL_DIR" in
  *"/Desktop/"*|*"/Library/Mobile Documents/"*)
    echo "WARNING: CONTROL_DIR ($CONTROL_DIR) looks iCloud-synced. v2 puts it under \$HOME (e.g. ~/.rimv-delivery/<wave>) to avoid resurrecting-STOP + sync churn. Continuing, but reconsider." >&2 ;;
esac

# wave-local STOP/DONE reset (NEVER touch a global .delivery/STOP — it isn't ours)
rm -f "$CONTROL_DIR/STOP" "$CONTROL_DIR/WAVE_DONE"
rm -rf "$CONTROL_DIR/driver.lock.d"

# --- preflight (aborts the launch on any systemic problem) ------------------
echo "=== running preflight ==="
if ! bash "$HERE/preflight.sh"; then
  echo "PREFLIGHT FAILED -> not launching. Fix the reported issue and re-run start.sh." >&2
  exit 1
fi

# publish the integration branch so per-issue PRs can target it (no-op if up to date)
( cd "$REPO" && git push -u origin "$BASE" 2>&1 | tail -2 ) || true

# stop any prior instances of THIS wave (pidfiles + stragglers matched to this control dir)
for pf in "$CONTROL_DIR/driver.pid" "$CONTROL_DIR/sentinel.pid"; do
  [ -f "$pf" ] && kill "$(cat "$pf" 2>/dev/null)" 2>/dev/null || true
done
pkill -f "$HERE/driver-loop\.sh" 2>/dev/null || true
pkill -f "$HERE/sentinel\.sh"    2>/dev/null || true
sleep 1

# --- launch as nohup daemons: survive this shell exit + Claude-session suspend (reparent to
# --- launchd). More reliable than `screen` (the codex-heavy driver's screen died ~90s in and
# --- orphaned the work — postmortem bug #4). $! is the daemon PID, tracked in pidfiles. ------
RIMV_WAVE_CONF="$CONF" nohup bash "$HERE/driver-loop.sh" >> "$CONTROL_DIR/driver.log"   2>&1 < /dev/null &
echo $! > "$CONTROL_DIR/driver.pid"
RIMV_WAVE_CONF="$CONF" nohup bash "$HERE/sentinel.sh"    >> "$CONTROL_DIR/sentinel.log" 2>&1 < /dev/null &
echo $! > "$CONTROL_DIR/sentinel.pid"
sleep 2

echo "=== ${WAVE:-wave} daemons ==="
allok=1
for nm in driver sentinel; do
  p=$(cat "$CONTROL_DIR/$nm.pid" 2>/dev/null)
  if [ -n "$p" ] && kill -0 "$p" 2>/dev/null; then echo "  $nm: pid $p ALIVE"; else echo "  $nm: NOT RUNNING (FAILED)"; allok=0; fi
done
echo
echo "Monitor (read-only — do NOT git in the main checkout while the driver owns worktrees):"
echo "  STATUS    : $CONTROL_DIR/STATUS            (event log: issue-passed / blocked / wave-done)"
echo "  heartbeat : $CONTROL_DIR/heartbeat.txt     (epoch | time | issue | phase)"
echo "  driver log: $CONTROL_DIR/driver.log"
echo "  sentinel  : $CONTROL_DIR/sentinel.log"
echo "  per-issue : $CONTROL_DIR/logs/issue-<n>.log"
echo "  queue     : $QUEUE"
echo "Stop      : touch $CONTROL_DIR/STOP"
echo
echo "Event-driven: notify.sh pushes a desktop notification + appends to STATUS (+ optional"
echo "RIMV_WEBHOOK_URL) on issue-passed / issue-blocked / canary / systemic-error / wave-done."
echo "Do NOT poll on a timer — wait for an event or read STATUS on demand. This is the Claude-spend saver."

if [ "$allok" = 1 ]; then
  note wave-start "$WAVE" "daemons up (canary-first); waiting on events. Control dir: $CONTROL_DIR"
else
  note systemic-error "$WAVE" "a daemon failed to start — check $CONTROL_DIR/driver.log + sentinel.log"
  exit 1
fi
