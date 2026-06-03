#!/usr/bin/env bash
# driver-loop.sh — crash-restart wrapper around driver.sh (runs as a nohup daemon, NOT in screen).
# Mirrors launchd KeepAlive(SuccessfulExit=false): restart on crash (non-zero), stop on clean
# exit (0 = WAVE_DONE / idle / STOP). Honors ONLY the wave-local STOP (never a global .delivery/STOP).
set -uo pipefail
CONF="${RIMV_WAVE_CONF:?driver-loop: set RIMV_WAVE_CONF}"
. "$CONF"
: "${CONTROL_DIR:?}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LOG="$CONTROL_DIR/driver.log"
STOP="$CONTROL_DIR/STOP"
DONE="$CONTROL_DIR/WAVE_DONE"
[ -n "${REPO:-}" ] && cd "$REPO" 2>/dev/null || true

echo "[$(date '+%F %T')] [outer] ${WAVE:-wave} driver-loop started (pid $$)" >> "$LOG"
while true; do
  RIMV_WAVE_CONF="$CONF" bash "$HERE/driver.sh"; rc=$?
  if [ -f "$STOP" ]; then echo "[$(date '+%F %T')] [outer] STOP -> stop" >> "$LOG"; break; fi
  if [ -f "$DONE" ]; then echo "[$(date '+%F %T')] [outer] WAVE_DONE marker -> stop" >> "$LOG"; break; fi
  if [ "$rc" -eq 0 ]; then echo "[$(date '+%F %T')] [outer] driver exit 0 (done/idle) -> stop" >> "$LOG"; break; fi
  echo "[$(date '+%F %T')] [outer] driver crashed rc=$rc -> restart in 20s" >> "$LOG"; sleep 20
done
echo "[$(date '+%F %T')] [outer] ${WAVE:-wave} driver-loop ended" >> "$LOG"
