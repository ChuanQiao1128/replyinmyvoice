---
name: dynamic-delivery-workflow
description: Run an UNATTENDED, restart/suspend-resilient multi-issue software-delivery wave where each GitHub issue is implemented by Codex (codex exec) via a detached background daemon — self-healing watchdog sentinel, event-driven notifications (no Claude polling), adaptive timeouts, canary-first validation, diff-scoped safety gates, merging into an integration branch (never main). It can also take a raw requirement/spec and decompose it into issues first (one human checkpoint) before running the wave. Use whenever the user wants to run a "dynamic workflow" / "delivery wave" / "delivery pipeline" to deliver or ship a BATCH of GitHub issues via Codex with minimal supervision — especially if it should survive session restarts/suspend, save Claude usage, or run unattended/overnight while they check in periodically — even if they don't name the skill. Distinct from the in-session `delivery-pipeline` skill: THIS one runs as a detached daemon and is for hands-off, restart-proof batches. Example triggers: "run a dynamic workflow to finish issues #378-400", "kick off an unattended codex delivery wave", "跑 dynamic workflow 用 codex 完成这些 issue".
---

# Dynamic Delivery Workflow

Drive an **unattended, restart-proof** batch of GitHub issues to PRs. Each issue is implemented by
**Codex** (`codex exec`) inside its own git worktree; a **detached OS daemon** (nohup; optional
launchd) runs the serial queue, verifies every diff against safety + test gates, opens one PR per
issue into an **integration branch (never main)**, and **pushes events** to you instead of being
polled. The daemon survives Claude-session suspend/resume and (with launchd) machine reboot.

You (Claude) are the **supervisor**: you decompose / configure, launch, and engage only on pushed
events. The point is to spend *minimal* Claude usage while a long wave runs autonomously.

## When this fires

- The user wants to "run a dynamic workflow", "delivery wave", "delivery pipeline", "ship/finish a
  batch of issues", "kick off an unattended/overnight Codex run", etc. — a BATCH (not one PR).
- Especially when they want it to **survive session restart/suspend**, **save Claude usage**, or
  **run unattended while they check in periodically**.
- Distinct from the in-session **`delivery-pipeline`** skill (which keeps Claude in the loop per
  issue). Use THIS skill for hands-off, restart-proof, daemon-backed batches. If the user clearly
  wants Claude to stay engaged per issue, prefer `delivery-pipeline` instead.
- A single-PR task does NOT need this — just code it (or one Codex call).

## Why it is built this way (read before you trust it)

This skill is the hardened v2 of a real wave that shipped 22/22 issues but needed heavy runtime
firefighting. Eight latent bugs only surfaced at RUNTIME. The scripts already embody every fix; do
not "simplify" them without reading `references/postmortem.md` — several one-liners are load-bearing
(diff-scoped banned gate, `WAVE_DONE` naming, nohup-not-screen, wave-local-STOP, `--state open` PR
check, strip-`node_modules`-before-commit, atomic lock + 90s grace). Architecture overview:
`references/design.md`. Machine-reboot survival: `references/launchd.md`.

## Two phases

- **Phase 1 — Decompose** (ONLY if you were handed a requirement, not a ready issue list):
  reason the requirement → GitHub issues + per-issue briefs + `queue.tsv` → **ONE human checkpoint**.
  Full protocol: `references/decompose.md`.
- **Phase 2 — Execute** (the existing unattended daemon): preflight → `start.sh` → event-driven,
  restart-proof. This is the supervisor protocol below (steps 1–7).

If you were handed a ready issue-list with tiers/deps/briefs already, SKIP Phase 1 — go straight to
Phase 2 step 2 ("Write `wave.conf` + `queue.tsv`").

## The supervisor protocol — Phase 2 Execute (follow on trigger)

### 1. Gather the wave config

You need: the **issue list** with per-issue **tier / deps / brief / timeout**, the **integration
base branch**, the **repo + GitHub repo**, and a **branch prefix**. 

- **Input A — ready issues:** the user hands you issue numbers + an existing integration branch →
  gather their tiers/deps and go to step 2.
- **Input B — a requirement/spec** (file path, inline, or linked doc) → **do Phase 1 first: read
  `references/decompose.md` and follow it end to end.** It produces the GitHub issues, the per-issue
  briefs under `BRIEF_DIR`, and a draft `queue.tsv`, then STOPS at the one human checkpoint. Only
  after explicit approval do you continue to step 2. (You MAY run `system-spec-synthesis` upstream
  to turn rough notes into a clean spec before decomposing — but decomposition itself is now native
  here, via `references/decompose.md`, not a hand-off to the disabled `delivery-pipeline`.)
- Decide each issue's **TIMEOUT_MIN** deliberately (adaptive — NOT one flat value): ~75 for
  backend/feature work, ~40 default, ~20 for docs-only. v1's flat 40 killed 3 big features.
- Identify **TIER-1 prereqs** (things dependents must build on) → `TIER1_MERGE=yes`; everything
  else is TIER-2 (PR only).
- **Exclude owner-only / real-charge issues from the queue entirely** (e.g. a live-purchase
  verification). They stay manual; never auto-process them.

### 2. Write `wave.conf` + `queue.tsv` in the OFF-iCloud control dir

Pick a control dir **under `$HOME`, not the iCloud-synced Desktop** (default
`~/.rimv-delivery/<wave>/`). This is a v2 fix: an iCloud-synced control dir resurrected a "dataless"
global STOP and churned sync (postmortem #3/#5). The repo `.git` may stay on the Desktop — the
scripts `git worktree add` into the off-iCloud control dir.

`wave.conf` is sourced by every script. Minimum keys (see `references/design.md` and the comments
in `queue.example.tsv`):

```bash
WAVE=payment-wave                                   # short name (notifications, logs)
REPO=/Users/qc/Desktop/CloudFlare                   # repo working tree (its .git can stay on Desktop)
GHREPO=ChuanQiao1128/replyinmyvoice                 # owner/name for gh
BASE=delivery/payment-wave                          # INTEGRATION branch — NEVER main (preflight refuses default)
BRANCH_PREFIX=delivery/payment                      # per-issue branch = <prefix>/<TAG>-<ISSUE>
CONTROL_DIR="$HOME/.rimv-delivery/payment-wave"     # OFF iCloud Desktop
QUEUE="$CONTROL_DIR/queue.tsv"
BRIEF_DIR=/Users/qc/Desktop/CloudFlare/plans/payment-issues   # where per-issue briefs live
# optional (sane defaults shown):
ATTEMPTS=3
DEFAULT_TIMEOUT_MIN=40
BANNED_TERMS='humanizer|bypass|undetect|detector|evade'   # CI grep guard (project AGENTS.md)
BANNED_PATHS='app components public lib'                   # diff-scope for the banned gate
DOTNET_DIR=backend-dotnet                                  # test root for backend diffs
FRONTEND_PATHS='app components lib'                        # typecheck+test trigger paths
# RIMV_WEBHOOK_URL=https://hooks.slack.com/...             # optional extra push channel
```

`queue.tsv` rows are TAB-separated: `ISSUE  TAG  TIER  TIER1_MERGE  DEPS  BRIEF_GLOB  TIMEOUT_MIN`.
Copy `queue.example.tsv` and edit. Dependency-first order; deps are comma-separated issue numbers
( `-` = none); a dependent defers until its deps are merged into base.

### 3. Run preflight — abort if it fails

```bash
RIMV_WAVE_CONF="$CONTROL_DIR/wave.conf" bash <skill>/scripts/preflight.sh
```

Preflight validates tooling (codex/gh/git/jq), auth (`gh auth status`, HTTPS origin, `gh auth
setup-git`), that the integration base exists and is **not** the default branch, that the queue
parses and every brief exists, and it **dry-runs the banned-term gate on an EMPTY base diff** — the
exact check that would have caught the v1 mis-scoped gate that flagged a pre-existing fixture and
would have blocked all 22 issues. If preflight fails, FIX the reported cause; do not launch.

### 4. Launch (canary-first) and confirm the daemons are up

```bash
RIMV_WAVE_CONF="$CONTROL_DIR/wave.conf" bash <skill>/scripts/start.sh
```

`start.sh` re-runs preflight, publishes the base, then launches two **nohup daemons** (not screen):
`driver-loop.sh` (crash-restart wrapper around the queue loop) and `sentinel.sh` (pidfile watchdog
that relaunches a dead/hung driver, with a 90s startup grace). Confirm both print `ALIVE` and that
`$CONTROL_DIR/driver.pid` / `sentinel.pid` exist.

**Canary-first is automatic, not a human pause.** The driver processes the FIRST issue, then
auto-checks it produced a clean PR. If that first issue fails in a way that looks **systemic**
(gate/brief/auth — i.e. it blocks), the driver **PAUSES the whole wave** (writes the wave-local STOP
+ WAVE_DONE) and pushes a `canary-failed` event — so you don't burn Codex on the other 20. If the
canary passes, it auto-continues the rest with no further intervention.

### 5. DO NOT poll on a timer — wait for events

This is the Claude-spend saver and the whole reason v2 exists. The daemon calls `notify.sh` ONLY on
a state change: **issue-passed / issue-blocked / canary-passed / canary-failed / systemic-error /
wave-done**. Each event fires a macOS desktop notification, appends a line to `$CONTROL_DIR/STATUS`,
and (if `RIMV_WEBHOOK_URL` is set) POSTs to that webhook. The `STATUS` file is durable and survives
session suspend — read it on demand to catch up.

Tell the user plainly: *"I won't poll on a timer — that wastes Claude usage. I'll engage when an
event fires (a blocked issue, a systemic error, or wave-done) or when you ask. You can watch
`$CONTROL_DIR/STATUS` yourself anytime."* Then stop and let the daemon work.

If you must check liveness once, read (don't `git`) the read-only monitors:
`$CONTROL_DIR/STATUS`, `heartbeat.txt`, `driver.log`, `sentinel.log`, `logs/issue-<n>.log`. Do NOT
run git commands in the main checkout while the driver owns worktrees.

#### Optional: arm the context-frugal watcher (zero-poll auto-wakeup)
To be auto-woken on a decision-needed event WITHOUT spending Claude turns polling, launch the
Claude-side watcher via a **backgrounded Bash** (`run_in_background: true`), NOT nohup:
`bash <skill>/scripts/claude-watch.sh "$CONTROL_DIR"`.
It watches STATUS for `issue-blocked` / `canary-failed` / `systemic-error` / `wave-done` (and the
driver+sentinel both-dead case), prints the matching STATUS tail + heartbeat, then EXITS — the
harness re-invokes Claude on that exit. That exit-wakes-Claude property is why it must be a harness
background task, not nohup. It does NOT replace `sentinel.sh` (the OS watchdog that keeps the DRIVER
alive); it is the Claude-attention layer on top. See `references/sentinel.md`.

### 6. On a `systemic-error` / `canary-failed` event: investigate, don't just resume

A systemic event means something is wrong for *every* issue (a mis-scoped gate, a bad brief
pattern, an auth/transport break, an unsatisfiable dependency). **Read the per-issue log + STATUS,
find the root cause, revise the plan or config, then re-launch** — do not blindly clear STOP and
restart into the same failure. To retry a single blocked issue after fixing it: delete its
`$CONTROL_DIR/done/<n>` marker (and the STOP file) and re-run `start.sh`.

### 7. Finalize and hand off

On `wave-done`, summarize from `$CONTROL_DIR/STATUS` + `gh pr list --base $BASE`:
- **merged** PRs (tier-1, now in the integration base) vs **open** PRs (tier-2, awaiting review),
- **blocked** issues (labeled `blocked`, with the reason) and what each needs,
- the integration branch name.

Then remind the user of the gates this skill deliberately does NOT cross:
- **Integration → main is owner-gated** — a merge to main auto-deploys prod and may run a live DB
  migration. The wave never merges to main; that cutover is the owner's call.
- Any **real-charge / owner-only** issue stayed out of the queue and is still manual.

## Stop / control

- Stop cleanly: `touch "$CONTROL_DIR/STOP"` (both daemons exit within ~30s).
- The scripts honor ONLY this wave-local STOP — never a shared global `.delivery/STOP` (postmortem
  #5). Each wave is fully isolated by its `CONTROL_DIR`.

## Safety the worker is bound to (encoded in `scripts/codex-brief.tmpl`)

Every per-issue Codex prompt carries hard constraints: banned terms (`humanizer/bypass/undetect/
detector/evade`), payment/Stripe **sandbox-only, never a real charge**, no secret values in tracked
files (runtime env validation only), **never push / open a PR / touch main**, no deploy commands,
no test-gutting (`@ts-ignore`/`eslint-disable`/loosened configs). The driver re-verifies all of
this independently — it trusts nothing the worker reports.

## Pointers

- `references/decompose.md` — Phase 1: requirement → issues + briefs + queue.tsv (one checkpoint).
- `references/sentinel.md` — the two watch layers (OS watchdog vs Claude-side watcher) + SessionStart auto-reconnect.
- `references/design.md` — architecture + the five v2 levers. Read when adapting to a new repo.
- `references/postmortem.md` — the 8 bugs + invariants. Read before editing any script.
- `references/launchd.md` — optional machine-reboot survival (must live off the TCC-blocked Desktop).
- `queue.example.tsv` — annotated sample queue.
