#!/usr/bin/env bash
# claude-watch.sh — context-frugal Claude-side watcher for ONE delivery wave.
#
# Sleeps cheaply (a plain shell loop — ZERO Claude context), wakes Claude EXACTLY ONCE when a
# human decision is likely needed (an actionable STATUS event, or an abnormal both-daemon death),
# prints the minimal context, and EXITS. Launch it via the Bash tool with run_in_background:true —
# a harness-managed background Bash re-invokes the Claude session when this process exits, and THAT
# exit is the cheap, zero-poll wakeup.
#
# This is NOT sentinel.sh. sentinel.sh (nohup) keeps the DRIVER alive and must survive session
# suspend. claude-watch.sh wakes CLAUDE and must be a harness background task (nohup cannot wake the
# model). Different layers, opposite launch requirements — see references/sentinel.md. This watcher
# never writes anything and never touches the driver.
#
# Usage:
#   bash claude-watch.sh <CONTROL_DIR>
#   DDW_CONTROL_DIR=<dir> bash claude-watch.sh
#   RIMV_WAVE_CONF=<dir>/wave.conf bash claude-watch.sh    # sources CONTROL_DIR from the conf
#
# Tunables (env):
#   WATCH_INTERVAL=45        seconds between scans (cheap; never polls Claude)
#   WATCH_MAX_SECS=86400     hard self-cap so an orphaned watcher cannot run forever
#   WATCH_EVENTS='issue-blocked|canary-failed|systemic-error|wave-done'

set -uo pipefail

CONTROL_DIR="${1:-${DDW_CONTROL_DIR:-}}"
if [ -z "${CONTROL_DIR}" ] && [ -n "${RIMV_WAVE_CONF:-}" ] && [ -f "${RIMV_WAVE_CONF}" ]; then
  # shellcheck disable=SC1090
  . "${RIMV_WAVE_CONF}" 2>/dev/null || true
fi
if [ -z "${CONTROL_DIR:-}" ]; then
  echo "claude-watch: no control dir (pass it as arg1, or set DDW_CONTROL_DIR / RIMV_WAVE_CONF)" >&2
  exit 2
fi

WATCH_INTERVAL="${WATCH_INTERVAL:-45}"
WATCH_MAX_SECS="${WATCH_MAX_SECS:-86400}"
WATCH_EVENTS="${WATCH_EVENTS:-issue-blocked|canary-failed|systemic-error|wave-done}"

STATUS="${CONTROL_DIR}/STATUS"
HEARTBEAT="${CONTROL_DIR}/heartbeat.txt"
WAVE="$(basename "${CONTROL_DIR}")"

# Baseline: only consider STATUS lines appended AFTER this watcher started, so a watcher relaunched
# after a wakeup does not instantly re-fire on the same historical event. Captured ONCE at startup.
base=0
[ -f "${STATUS}" ] && base="$(wc -l < "${STATUS}" 2>/dev/null | tr -d ' ')"
[ -z "${base}" ] && base=0
START_LINE=$((base + 1))

emit() { # reason
  local reason="$1"
  echo "=== claude-watch wakeup: ${WAVE} ==="
  echo "reason: ${reason}"
  echo "control: ${CONTROL_DIR}"
  echo "--- heartbeat ---"
  cat "${HEARTBEAT}" 2>/dev/null || echo "(no heartbeat)"
  echo "--- STATUS (new since watch start) ---"
  tail -n "+${START_LINE}" "${STATUS}" 2>/dev/null | tail -n 10 || true
  echo "next: read ${CONTROL_DIR}/STATUS in full + the relevant logs/issue-<n>.log; do NOT git in the main checkout while the driver owns worktrees."
}

pid_alive() { # pidfile
  local p
  p="$(cat "$1" 2>/dev/null)" || return 1
  [ -n "${p}" ] && kill -0 "${p}" 2>/dev/null
}

elapsed=0
while [ "${elapsed}" -lt "${WATCH_MAX_SECS}" ]; do
  # 1. Actionable STATUS event — field-scoped on column 2 (EVENT) so it cannot match a word inside
  #    a DETAIL URL or wave name. Only the lines appended after START_LINE are considered.
  if [ -f "${STATUS}" ]; then
    hit="$(tail -n "+${START_LINE}" "${STATUS}" 2>/dev/null \
            | awk -F'|' 'NF>=2{e=$2; gsub(/ /,"",e); print e}' \
            | grep -E "${WATCH_EVENTS}" | tail -1 || true)"
    if [ -n "${hit}" ]; then
      emit "${hit}"
      exit 0
    fi
  fi

  # 2. WAVE_DONE marker — normal completion (also lands as a wave-done STATUS line caught above).
  if [ -f "${CONTROL_DIR}/WAVE_DONE" ]; then
    emit "wave-done (marker)"
    exit 0
  fi

  # 3. Abnormal: driver AND sentinel both dead with no WAVE_DONE — the OS watchdog itself is gone,
  #    so nothing will self-heal. A driver-alone death is normal (sentinel relaunches it), so we
  #    only flag the both-dead case, and only once a wave has actually started (pidfiles existed).
  if ! pid_alive "${CONTROL_DIR}/driver.pid" && ! pid_alive "${CONTROL_DIR}/sentinel.pid" \
       && [ ! -f "${CONTROL_DIR}/WAVE_DONE" ]; then
    if [ -f "${CONTROL_DIR}/driver.pid" ] || [ -f "${CONTROL_DIR}/sentinel.pid" ]; then
      emit "driver+sentinel both dead (no WAVE_DONE) — wave stranded, needs investigation"
      exit 0
    fi
  fi

  sleep "${WATCH_INTERVAL}"
  elapsed=$((elapsed + WATCH_INTERVAL))
done

emit "watch-timeout (${WATCH_MAX_SECS}s self-cap reached)"
exit 0
