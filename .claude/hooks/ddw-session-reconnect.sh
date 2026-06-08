#!/usr/bin/env bash
# ddw-session-reconnect.sh — SessionStart scanner for in-flight delivery waves.
#
# Fires on session start/resume (registered in .claude/settings.json). If an unattended
# dynamic-delivery wave is still running from a prior session, it injects ONE block of context so
# Claude can reconnect (read STATUS + re-arm claude-watch). It only SCANS + ANNOUNCES — it never
# launches the watcher (a hook-spawned nohup can't wake the model; a long hook would block startup).
#
# Fail-open by construction: pure filesystem scan, no gh/git/network, exit 0 on every path, silent
# when no wave is active.

set -u

ROOT="${HOME}/.rimv-delivery"
[ -d "${ROOT}" ] || exit 0

active=""
for d in "${ROOT}"/*/; do
  [ -d "${d}" ] || continue
  [ -f "${d}WAVE_DONE" ] && continue                       # finished wave -> skip
  pid="$(cat "${d}driver.pid" 2>/dev/null)" || pid=""
  if [ -n "${pid}" ] && kill -0 "${pid}" 2>/dev/null; then # driver alive + not done -> ACTIVE
    name="$(basename "${d}")"
    active="${active}  wave: ${name}   control: ${d%/}   STATUS: ${d}STATUS"$'\n'
  fi
done

[ -z "${active}" ] && exit 0                                # common case: nothing live -> silent

printf '%s\n' "[dynamic-delivery-workflow] An unattended delivery wave is still in flight from a prior session:"
printf '%s' "${active}"
printf '%s\n' "To reconnect: read that STATUS file to catch up, then re-arm the watcher via a BACKGROUNDED Bash:"
printf '%s\n' "  bash \"\${CLAUDE_PROJECT_DIR:-.}/agent-skills/dynamic-delivery-workflow/scripts/claude-watch.sh\" \"<CONTROL_DIR>\""
printf '%s\n' "Do NOT run git in the main checkout while the driver owns worktrees."
exit 0
