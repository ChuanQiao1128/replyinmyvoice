#!/usr/bin/env bash
# notify.sh — event push for the dynamic-delivery-workflow wave.
#
# This REPLACES Claude polling. The daemon (driver/sentinel) calls it ONLY on a real state
# change — issue-passed, issue-blocked, systemic-error, wave-done, canary-failed — so the
# Claude session is woken at most a handful of times per wave instead of every 20 minutes.
#
# It does three things, each best-effort (a failing channel never aborts the wave):
#   1. macOS desktop notification via osascript (or terminal-notifier if installed).
#   2. Appends a structured line to <control>/STATUS  (the durable, always-readable channel —
#      survives session suspend; Claude/owner reads this file to catch up).
#   3. If ${RIMV_WEBHOOK_URL} is set, POSTs a small JSON to it (Slack-compatible {"text":...}).
#
# Usage:  notify.sh <EVENT> <SUBJECT> <DETAIL...>
#   EVENT   one of: issue-passed | issue-blocked | systemic-error | wave-done | canary-failed |
#                   canary-passed | wave-start | info   (free-form tolerated)
#   SUBJECT short line (e.g. "issue #387" or "preflight")
#   DETAIL  rest of args = human detail
#
# Config comes from the sourced wave.conf (CONTROL_DIR, WAVE, RIMV_WEBHOOK_URL). If wave.conf
# is not found via $RIMV_WAVE_CONF, it degrades to env vars + a STATUS file next to this call.
set -uo pipefail

# --- locate + source wave.conf (CONTROL_DIR/WAVE/RIMV_WEBHOOK_URL live there) ---------------
CONF="${RIMV_WAVE_CONF:-}"
[ -z "$CONF" ] && [ -n "${CONTROL_DIR:-}" ] && CONF="$CONTROL_DIR/wave.conf"
[ -n "$CONF" ] && [ -f "$CONF" ] && . "$CONF"

EVENT="${1:-info}"; shift || true
SUBJECT="${1:-}";   shift || true
DETAIL="$*"

WAVE="${WAVE:-wave}"
CONTROL_DIR="${CONTROL_DIR:-$PWD}"
STATUS="$CONTROL_DIR/STATUS"
TS="$(date '+%F %T')"

mkdir -p "$CONTROL_DIR" 2>/dev/null || true

# 2) durable STATUS line (the channel Claude/owner actually reads on resume) — always do this first
printf '%s | %-14s | %-16s | %s\n' "$TS" "$EVENT" "$SUBJECT" "$DETAIL" >> "$STATUS" 2>/dev/null || true

# 1) desktop notification (best-effort)
_title="rimv wave: ${WAVE}"
_msg="[$EVENT] ${SUBJECT}${DETAIL:+ — $DETAIL}"
if command -v terminal-notifier >/dev/null 2>&1; then
  terminal-notifier -title "$_title" -message "$_msg" >/dev/null 2>&1 || true
elif command -v osascript >/dev/null 2>&1; then
  # escape embedded double-quotes for the AppleScript string literal
  _t=${_title//\"/\\\"}; _m=${_msg//\"/\\\"}
  osascript -e "display notification \"$_m\" with title \"$_t\"" >/dev/null 2>&1 || true
fi

# 3) optional webhook (Slack-compatible). curl is best-effort + time-bounded so it can never hang.
if [ -n "${RIMV_WEBHOOK_URL:-}" ] && command -v curl >/dev/null 2>&1; then
  _payload=$(printf '{"text":"%s: %s"}' "${_title//\"/\\\"}" "${_msg//\"/\\\"}")
  curl -fsS -m 10 -X POST -H 'Content-Type: application/json' \
       -d "$_payload" "$RIMV_WEBHOOK_URL" >/dev/null 2>&1 || true
fi

exit 0
