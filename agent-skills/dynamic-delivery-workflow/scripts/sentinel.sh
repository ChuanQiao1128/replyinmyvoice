#!/usr/bin/env bash
# sentinel.sh — health watchdog for ONE wave (runs as a nohup daemon, NOT in screen).
# Every INTERVAL (default 1200s = 20 min):
#   - exit cleanly when WAVE_DONE exists, or the WAVE-LOCAL STOP is requested.
#   - if the driver-loop is DEAD (pidfile pid not alive) and not DONE/STOP -> relaunch it.
#   - if the heartbeat is STALE (> STALE secs, default 5400 = 90 min) -> the driver is hung:
#     kill driver-loop + driver + ONLY this wave's codex, then relaunch (the fresh driver is
#     idempotent). 90 min accommodates the adaptive 75-min big-feature timeout + verify.
# Pushes events via notify.sh on relaunch / hang (so Claude/owner learns the daemon self-healed).
# NO GLOBAL STOP: a sentinel that honored a shared .delivery/STOP got silently killed by a stale
# leftover (postmortem bug #5). This watches ONLY $CONTROL_DIR/STOP.
set -uo pipefail
CONF="${RIMV_WAVE_CONF:?sentinel: set RIMV_WAVE_CONF}"
. "$CONF"
: "${CONTROL_DIR:?}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
NOTIFY="$HERE/notify.sh"
SLOG="$CONTROL_DIR/sentinel.log"
HB="$CONTROL_DIR/heartbeat.txt"
STOP="$CONTROL_DIR/STOP"
DONE="$CONTROL_DIR/WAVE_DONE"
DRIVER_PID="$CONTROL_DIR/driver.pid"
INTERVAL="${SENTINEL_INTERVAL:-1200}"      # 20 min health cadence
STALE="${SENTINEL_STALE:-5400}"            # >90 min w/o heartbeat update => hung
GRACE="${SENTINEL_GRACE:-90}"              # startup grace before first relaunch (startup race)
WAVE="${WAVE:-wave}"

mkdir -p "$CONTROL_DIR"
slog(){ echo "$(date '+%F %T') | $*" >> "$SLOG"; }
note(){ bash "$NOTIFY" "$@" 2>/dev/null || true; }
[ -n "${REPO:-}" ] && cd "$REPO" 2>/dev/null || true

driver_alive(){ local p; p=$(cat "$DRIVER_PID" 2>/dev/null); [ -n "$p" ] && kill -0 "$p" 2>/dev/null; }
relaunch_driver(){
  pkill -f "$HERE/driver-loop\.sh" 2>/dev/null || true
  sleep 1
  RIMV_WAVE_CONF="$CONF" nohup bash "$HERE/driver-loop.sh" >> "$CONTROL_DIR/driver.log" 2>&1 < /dev/null &
  echo $! > "$DRIVER_PID"
}

slog "sentinel started (interval=${INTERVAL}s stale=${STALE}s grace=${GRACE}s) [pid $$]"
# initial grace: don't relaunch a driver that start.sh just launched but whose pidfile/process
# is still settling (the startup race). Honor STOP/DONE during the wait. (postmortem bug #7)
gw=0
while [ "$gw" -lt "$GRACE" ]; do
  { [ -f "$DONE" ] || [ -f "$STOP" ]; } && break
  sleep 15; gw=$((gw+15))
done

while true; do
  if [ -f "$DONE" ]; then slog "WAVE_DONE present -> sentinel exit"; break; fi
  if [ -f "$STOP" ]; then slog "STOP present -> sentinel exit"; break; fi

  alive=$(driver_alive && echo yes || echo no)
  hbline=$(head -1 "$HB" 2>/dev/null)
  hbage="n/a"; [ -f "$HB" ] && hbage=$(( $(date +%s) - $(stat -f %m "$HB") ))
  slog "driver=$alive | hb_age=${hbage}s | '${hbline}'"

  if [ "$alive" = no ]; then
    slog "driver DEAD and not DONE/STOP -> relaunch"
    relaunch_driver
    note systemic-error "$WAVE" "driver was dead — sentinel relaunched it (self-heal). Check $SLOG."
  elif [ "$hbage" != "n/a" ] && [ "$hbage" -gt "$STALE" ]; then
    slog "HANG: heartbeat stale ${hbage}s > ${STALE}s -> kill driver+codex and relaunch"
    pkill -9 -f "$HERE/driver-loop\.sh" 2>/dev/null || true
    pkill -9 -f "$HERE/driver\.sh"      2>/dev/null || true
    # kill ONLY this wave's codex (scoped to its worktree path), never unrelated codex jobs
    pkill -9 -f "exec -C $CONTROL_DIR/wt" 2>/dev/null || true
    sleep 2
    relaunch_driver
    note systemic-error "$WAVE" "driver hung (${hbage}s stale) — sentinel killed + relaunched it. Last phase: '${hbline}'. Check $SLOG."
  fi

  # interruptible sleep so STOP/DONE are noticed within ~30s
  waited=0
  while [ "$waited" -lt "$INTERVAL" ]; do
    { [ -f "$DONE" ] || [ -f "$STOP" ]; } && break
    sleep 30; waited=$((waited+30))
  done
done
slog "sentinel ended"
