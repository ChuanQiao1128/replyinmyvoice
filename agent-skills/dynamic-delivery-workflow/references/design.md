# dynamic-delivery-workflow — v2 architecture (condensed)

Read this when you need to understand *why* the pieces are shaped the way they are, or when
adapting the scripts to a new repo / wave shape. The runtime protocol lives in `SKILL.md`; the
specific bugs these choices defend against live in `postmortem.md`.

## What this is

A supervised but **UNATTENDED** multi-issue delivery wave. Each GitHub issue is implemented by
**Codex** (`codex exec`), driven by a **detached OS daemon** (nohup; optional launchd) that
**survives Claude-session suspend/resume** and machine restart. The daemon verifies every diff
against safety + test gates, opens one PR per issue into an **integration branch (never main)**,
and pushes **events** to Claude/owner instead of being polled.

Origin: the payment wave (#378–400, 22 issues) shipped 22/22 in ~6h42m but needed heavy runtime
firefighting and ~26 Claude polls. v2's goals: **less Claude spend, more reliable one-shot Codex,
automatic recovery, uninterruptible across session/restart.**

## Component map

```
start.sh        # set up off-iCloud control dir, run preflight, launch the two daemons (nohup), print status
  └─ preflight.sh   # validate BEFORE launch; abort+notify on failure (incl. gate dry-run on empty diff)
  └─ driver-loop.sh # crash-restart wrapper (KeepAlive-style) — nohup daemon #1
        └─ driver.sh    # the serial queue loop: per issue -> worktree -> codex -> verify -> push+PR -> (tier1) base-merge
              └─ codex-brief.tmpl  # per-issue prompt (issue body + brief + hard safety)
              └─ notify.sh         # event push (called on issue-passed/blocked/canary/systemic/wave-done)
  └─ sentinel.sh    # pidfile watchdog (relaunch dead/hung driver) — nohup daemon #2
        └─ notify.sh
wave.conf       # ALL per-wave parameters (sourced by every script); written by Claude into the control dir
queue.tsv       # the ordered, dependency-first issue list
```

## Execution model

- **Serial, one issue at a time** — chosen for robustness (a wave-wide reset is cheap; concurrent
  drivers stomping one worktree is not). Optional N-way parallel workers are a future extension
  (see postmortem "speed").
- **One git worktree per issue** under `$CONTROL_DIR/wt/issue-<n>`, cut fresh off the latest `$BASE`
  tip. Codex writes + commits *in that worktree*; the **driver** (not Codex) does push + `gh pr
  create --base $BASE`. This keeps push/PR/merge authority with the supervisor, so Codex can never
  push or touch main even if it tries.
- **Tiering**: TIER-1 prereqs (`TIER1_MERGE=yes`) are fast-merged into `$BASE` via a dedicated
  `_integration` worktree *before* any dependent runs, so dependents build on them. TIER-2 issues
  only get a PR. Dependents whose deps aren't in base yet **defer**, and the loop re-runs while
  there's deferred-but-progressing work.

## The five v2 levers (vs v1)

1. **Off-iCloud control dir.** Default `~/.rimv-delivery/<wave>/`. Eliminates the resurrecting
   global STOP, the "dataless" file weirdness, and sync interference. The repo `.git` stays put;
   only the worktrees + control files move.
2. **Wave-local STOP only.** `$CONTROL_DIR/STOP`. The scripts NEVER honor a shared global STOP.
3. **Preflight + canary.** Preflight dry-runs every gate on the base tree (esp. the banned-term
   gate on an EMPTY diff) so a mis-scoped gate is caught in seconds. The canary processes ONLY the
   first issue, then auto-checks it produced a clean PR; a systemic-looking failure PAUSES the wave
   instead of burning Codex on all the rest. (Both are automatic — not a human pause.)
4. **Event-driven notify** replaces polling. The daemon pushes a desktop notification + a `STATUS`
   line (+ optional webhook) ONLY on state change: issue-passed / issue-blocked / canary outcome /
   systemic-error / wave-done. Claude engages ~2–3 times total instead of ~26 timed polls.
5. **Adaptive per-issue timeout.** Each queue row carries `TIMEOUT_MIN` (~75 backend/feature, ~40
   default, ~20 docs). v1's flat 40-min cap killed 3 big features (exit 124). Codex is told to
   commit incrementally so a timeout never discards a whole run.

## Verify gates (run by the driver, inside the worktree — trust nothing Codex reported)

1. **Banned-term** (hard) — DIFF-SCOPED: only lines Codex *added* vs base + new untracked files,
   under `$BANNED_PATHS`. Pre-existing matches must not fail it.
2. **Secret-value / suppression** — reject committed secret-shaped strings and added
   `@ts-ignore`/`eslint-disable` on changed `.ts/.tsx/.cs`.
3. **Tests-by-diff** — backend touched → `dotnet test` in `$DOTNET_DIR`; frontend touched →
   symlink `node_modules` then `npm run typecheck` + `npm run test`; docs-only → non-empty check.
4. **Scope diffstat** — surfaced for judgment (not an auto-fail beyond the above).

≤3 attempts per issue; on fail the driver resumes the same Codex thread with corrective feedback;
on the 3rd fail the issue is marked `blocked` (label + comment + idempotent `done/<n>` marker) and
the wave moves on.

## Finalization

When the queue is clear (no deferrals) the driver writes `WAVE_DONE` (NOT `DONE` — a
case-insensitive FS collides with the `done/` markers dir) and pushes a `wave-done` event. Result:
merged PRs (tier-1) + open PRs (tier-2) into `$BASE`; blocked issues are labeled. **Integration →
main is owner-gated** (a merge to main auto-deploys prod and may run a live DB migration). Any
real-charge / owner-only issue is intentionally excluded from the queue and never auto-processed.
