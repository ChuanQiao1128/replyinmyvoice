# Sentinel layers — keeping the wave AND the session alive

A wave has two independent watch layers. They look similar but have **opposite launch requirements**, so do not "unify" them.

## Layer 1 — `sentinel.sh`: keep the DRIVER alive (OS watchdog)
- Launched by `start.sh` as a **nohup daemon** (not screen, not a harness task).
- Every interval (~20 min): exits cleanly on `WAVE_DONE` or the wave-local `STOP`; relaunches the driver-loop if its pidfile pid is dead and the wave isn't done/stopped; if the heartbeat is stale (driver hung), kills + relaunches the driver (the fresh driver is idempotent).
- **Why nohup:** it must survive Claude-session suspend/resume (and, with launchd, machine reboot) so the *work* keeps moving while you're away — "任务不断 / the task never stops".
- It owns DRIVER health. Nothing else may relaunch the driver.

## Layer 2 — `claude-watch.sh`: wake CLAUDE on a decision (context-frugal watcher)
- Launched by **Claude** via the Bash tool with **`run_in_background: true`** (a harness-managed background task) — never nohup, never the SessionStart hook.
- Sleeps cheaply in a shell loop (zero Claude context). Wakes Claude **exactly once** when a human decision is likely needed — an actionable STATUS event (`issue-blocked | canary-failed | systemic-error | wave-done`) or the abnormal **driver + sentinel both-dead** case — then prints a compact wakeup block and EXITS.
- **Why a harness background task:** the wakeup IS the exit. A harness-managed background Bash re-invokes the Claude session when the process exits. A `nohup` process detaches and its exit reaches nothing; a SessionStart hook runs once at startup and can't wake a mid-session model. So Layer 1 is correctly nohup (survive suspend) and Layer 2 is correctly a harness task (wake Claude) — opposite requirements.
- It is **read-only**: never writes, never touches the driver. Killing or forgetting it does NOT affect driver health (Layer 1 owns that) — it only stops Claude auto-wakeups. Conversely it must **never relaunch the driver** (that's Layer 1's job; doing both invites the multi-driver stomp the atomic lock guards against).

Arm it (optional, when you want zero-poll auto-wakeups):
```bash
bash <skill>/scripts/claude-watch.sh "$CONTROL_DIR"   # via the Bash tool, run_in_background:true
```

## Session-resume auto-reconnect (SessionStart hook)
A suspended/resumed Claude session loses its harness background tasks (Layer 2), but the wave keeps running (Layer 1 is nohup). To reconnect automatically:

- The repo registers a **SessionStart hook** (`.claude/settings.json` → `.claude/hooks/ddw-session-reconnect.sh`).
- On session start/resume it scans `~/.rimv-delivery/*/` for an ACTIVE wave (`driver.pid` alive AND no `WAVE_DONE`). If found, it injects one line of context naming the wave + its `STATUS` path.
- The hook only **detects + announces** — it never launches the watcher (a hook-spawned nohup couldn't wake the model, and a long-running hook would block startup). Claude, seeing the announcement, reads `STATUS` to catch up and re-arms `claude-watch.sh` via a background Bash.
- The hook is **fail-open**: pure filesystem scan, no `gh`/`git`/network, `exit 0` on every path, silent when no wave is active.

Net: **task never stops** (Layer 1) + **session auto-reconnects** (hook) + **Claude only wakes on decisions** (Layer 2) — all while spending minimal context.
