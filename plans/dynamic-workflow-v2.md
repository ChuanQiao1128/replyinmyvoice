# Dynamic Delivery Workflow — v2 design (post-mortem + next-phase spec)

Date: 2026-06-01. Context: the payment wave (#378–400, 22 issues) delivered 22/22, 0 blocked,
in ~6h42m — but with heavy runtime firefighting. This doc captures everything that went wrong
and the concrete v2 changes. v1 driver lives in `.delivery/payment/` (kept as reference).

## 1. What went wrong this run (all of it)

**Architecture-level**
- The **Workflow tool is session-bound** — it was killed by session suspend/resume **twice**, forcing a switch to a detached nohup daemon mid-run.

**8 latent driver bugs that only surfaced at RUNTIME** (the build "looked clean"):
1. **banned-term gate false-positive (tree-wide)** — grepped all of `app/components/public/lib`, matched the pre-existing deny-list fixture `lib/rewrite-eval-cases.ts` → would have blocked ALL 22. Fix: diff-scope to lines Codex *added*.
2. **macOS case-insensitive FS collision** `done/` (markers dir) ↔ `DONE` (completion file) — completion marker couldn't be written → sentinel would infinite-restart. Fix: renamed `WAVE_DONE`.
3. **origin was SSH; github.com:22 timed out** (the repo is in the **iCloud-synced Desktop**) — all git push/fetch failing. Fix: origin → HTTPS + `gh auth setup-git`.
4. **`screen` daemon unstable under Codex load** — the driver screen died ~90s in and orphaned the work; sentinel couldn't manage it. Fix: nohup daemons + pidfiles.
5. **stale GLOBAL `.delivery/STOP`** (auth-wave leftover, iCloud-synced "dataless" file) silently halted the driver AND killed the sentinel mid-run; `rm -f` in start.sh never removed it. Fix: removed it; added a sentinel guard that nukes it if it resurfaces.
6. **commit `git add -A` committed the verify-step `node_modules` symlink** + missed untracked-only changes (would falsely "empty-branch block" add-only issues). Fix: strip symlink + `git status --porcelain` detection.
7. **startup race** — sentinel relaunched a just-started driver → multiple `driver.sh` stomping one worktree. Fix: 90s sentinel grace + atomic `mkdir` lock.
8. **stale `done/378` marker + a merged-then-reset chaos PR (#401)** caused a false skip of #378. Fix: `already_done` uses `--state open` + marker; reset integration branch clean.

**Chaos era**: while bugs 1/4/7 were live, multiple drivers ran concurrently, created a duplicate PR (#401), and merged half-baked work into the integration branch → required a full reset + origin-branch cleanup.

**Cost / friction**: ~26 sentinel polls (Claude turns) + extensive manual firefighting + wasted Codex on the doomed #378 attempts.

## 2. The three specific questions

**Q1 — Codex timeouts.** Per-attempt cap = `timeout 2400` (40 min). **3 issues hit it and were killed (exit 124): #387, #392, #395** (the big features). They still produced test-passing PRs (Codex likely finished + the CLI hung at the end, OR partial work happened to pass) — but **those 3 PRs need extra review** (possible truncation). 40 min is too tight for big features.

**Q2 — Why slow.** Serial (1 issue at a time, by design for robustness) × Codex genuinely slow per issue: ~18 min avg (range 3–40), each Codex call re-reads ~12M tokens of repo + the driver runs full `dotnet test`/`npm test`. **Zero idle time** — it's fully utilized, just serial + heavy.

**Q3 — No completion notification.** A detached daemon has **no channel to push to the Claude session**. The only link was the session-bound ScheduleWakeup sentinel — and the session was suspended when the wave finished (12:49), so nothing fired until the owner manually queried.

## 3. v2 design — goals: (a) less Claude spend, (b) Codex one-shot, (c) auto-fix/adapt, + session/restart resilience

**Root-cause cleanup (fixes several bugs at once)**
- **Move `.delivery/` control files + worktrees OUT of the iCloud-synced Desktop** (e.g. `~/rimv-delivery/`). Eliminates the resurrecting global STOP, the "dataless" file weirdness, and reduces sync interference. (origin already HTTPS.)
- **Use ONLY a wave-local STOP** (`<wave>/STOP`); never honor a shared global `.delivery/STOP`.

**(a) Cut Claude consumption — event-driven, not polling**
- Daemon emits events ONLY on state change: issue passed / issue blocked / systemic-failure circuit-break / WAVE_DONE.
- Push channel independent of the session: desktop notification (`terminal-notifier`/`osascript`) + a prominent `STATUS` file; optional Slack/webhook.
- Claude engages ~2–3 times total (kickoff + on a flagged event + at completion) instead of ~26 timed polls.

**(b) Codex one-shot quality**
- **Pre-flight gate validation**: before launch, dry-run every gate against the base tree (e.g. confirm the banned-term gate passes on an empty diff). Catches systemic gate bugs in seconds, not after issue #1.
- **Canary**: process 1 issue, PAUSE, auto-verify it produced a clean PR end-to-end, THEN release the rest. Systemic bugs surface after 1 issue, not 22.
- **Reduce Codex per-call cost**: persistent Codex session / project-context caching so it doesn't re-read the whole repo each issue (faster AND cheaper AND more consistent).
- Sharper briefs + machine-checkable acceptance so attempt-1 passes verify more often.

**(c) Error handling / adaptivity**
- **Adaptive Codex timeout**: ~60–75 min for backend/feature issues, ~20 min for docs; or detect "still emitting events" vs "truly stalled" before killing.
- **Incremental Codex commits** so a timeout doesn't discard 40 min of work.
- **Systemic-failure circuit breaker** (extend the existing tier-1 0-pass breaker): if N consecutive issues block for the same reason, PAUSE + push to the owner/Claude to revise the plan rather than burn quota.

**Session / machine-restart resilience**
- Daemon already survives session suspend/resume (proven this run).
- For **machine restart**: a `launchd` LaunchAgent (works once the scripts are off the TCC-blocked Desktop) or a login-item relauncher.

**Speed (optional)**
- **Parallel workers**: 2–3 git worktrees running Codex concurrently → wall-clock to ~1/2–1/3. Cost: higher machine load (each Codex spawns builds/tests) + more complexity; needs concurrency + quota guardrails.

## 4. Implementation checklist (next phase)
- [ ] Relocate delivery control dir off iCloud Desktop; wave-local STOP only.
- [ ] Pre-flight gate dry-run + canary-first release gate.
- [ ] Event-driven notifier (desktop + STATUS file) replacing 20-min polling.
- [ ] Adaptive per-issue Codex timeout + incremental commits.
- [ ] Persistent/cached Codex context to cut per-issue cost.
- [ ] Optional: N-way parallel workers with guardrails.
- [ ] Optional: launchd LaunchAgent for machine-restart survival.
- [ ] Carry forward v1's working pieces: diff-scoped banned gate, atomic lock, 90s grace, nohup daemons, idempotent markers, tier-1-merge-then-dependents ordering.

## 5. Honest status
v1 DELIVERED (22/22) but leaned on heavy human firefighting and over-polled Claude. v2 targets exactly that: fewer Claude turns, more reliable one-shot Codex, automatic error recovery, and uninterruptible across session/restart.
