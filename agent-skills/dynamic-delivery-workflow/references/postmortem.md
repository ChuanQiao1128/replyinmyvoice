# dynamic-delivery-workflow — the 8 runtime bugs (and their fixes)

Read this before changing any script. Each fix below is **load-bearing** — it cost real
firefighting on the v1 payment wave (#378–400). The scripts in `../scripts/` already embody every
fix; this file exists so a future edit never silently regresses one.

The wave "looked clean" at build time and still hit all of these at RUNTIME. Defense in depth +
preflight + canary exist specifically because static checks didn't surface them.

| # | Bug (v1) | Symptom | Fix (carried into v2) |
|---|----------|---------|------------------------|
| 1 | **Banned-term gate was tree-wide** — grepped all of `app/components/public/lib` | Matched the pre-existing deny-list fixture `lib/rewrite-eval-cases.ts` → would have blocked **all 22** issues | Gate is **diff-scoped**: only lines Codex *added* vs base + new untracked files. `preflight.sh` dry-runs it on an EMPTY diff so a mis-scope is caught before launch. |
| 2 | **macOS case-insensitive FS collision** `done/` (markers dir) ↔ `DONE` (completion file) | Completion marker couldn't be written → sentinel infinite-restart | Completion marker renamed **`WAVE_DONE`**. |
| 3 | **origin was SSH; github.com:22 timed out** (repo on the iCloud Desktop) | All git push/fetch failing | origin → **HTTPS** + `gh auth setup-git`; preflight refuses an SSH origin. v2 also moves the control dir **off** the Desktop. |
| 4 | **`screen` daemon unstable under Codex load** | The driver screen died ~90s in and orphaned the work; sentinel couldn't manage it | **nohup daemons + pidfiles** (`driver.pid` / `sentinel.pid`), NOT screen. |
| 5 | **Stale GLOBAL `.delivery/STOP`** (leftover from a prior wave, iCloud "dataless" file) | Silently halted the driver AND killed the sentinel mid-run | **Wave-local STOP only** (`$CONTROL_DIR/STOP`); scripts never honor a global STOP. Control dir off iCloud so dataless files don't resurrect. |
| 6 | **commit `git add -A` grabbed the verify-step `node_modules` symlink** + missed untracked-only changes | Bloated commits; add-only issues falsely "empty-branch" blocked | **Strip the symlink before `git add -A`**; detect changes via `git status --porcelain`; the empty-branch guard uses `git rev-list --count origin/$BASE..HEAD`, not add emptiness. |
| 7 | **Startup race** — sentinel relaunched a just-started driver | Multiple `driver.sh` stomping one worktree | **90s sentinel startup grace** + **atomic `mkdir` single-instance lock** in driver.sh. |
| 8 | **Stale `done/378` marker + a merged-then-reset chaos PR (#401)** | False skip of #378 | `already_done` PR check uses **`--state open` only** (a merged-then-reset PR must not skip); idempotent `done/<n>` markers; reset the integration branch clean before a re-run. |

## The three questions v2 answers

- **Q1 Codex timeouts.** v1's flat `timeout 2400` (40 min) killed 3 big features (#387/#392/#395,
  exit 124) — those PRs needed extra review for truncation. v2 → **adaptive per-issue
  `TIMEOUT_MIN`** in the queue (~75 backend/feature, ~40 default, ~20 docs) + a brief that tells
  Codex to **commit incrementally** so a kill loses only the uncommitted delta.
- **Q2 Why slow.** Serial by design × Codex genuinely ~18 min/issue (re-reads the repo + runs full
  `dotnet test`/`npm test`). Zero idle time — fully utilized, just serial + heavy. Optional N-way
  parallel worktrees would cut wall-clock to ~1/2–1/3 at the cost of machine load + concurrency
  guardrails (future extension).
- **Q3 No completion notification.** A detached daemon has no channel into the Claude session, and
  the session-bound wakeup didn't fire because the session was suspended when the wave finished.
  v2 → **`notify.sh`**: desktop notification + durable `STATUS` file (+ optional webhook) on every
  state change. The STATUS file is the channel that survives suspend — Claude reads it on resume.

## Cost / friction in v1 (what v2 cuts)

~26 sentinel polls (Claude turns) + heavy manual firefighting + wasted Codex on the doomed #378
attempts, plus a "chaos era" where bugs 1/4/7 let multiple drivers run concurrently, create a
duplicate PR (#401), and merge half-baked work — forcing a full integration-branch reset. v2's
preflight + canary + single-instance lock + event notifications target exactly that waste.

## Invariants a future edit MUST NOT break

- Banned-term gate stays **diff-scoped** (never whole-tree).
- Completion marker stays **`WAVE_DONE`** (never `DONE`).
- Daemons stay **nohup + pidfile** (never `screen`).
- STOP stays **wave-local** (never a shared global STOP).
- `already_done` PR check stays **`--state open`**.
- Empty-branch guard stays **`rev-list --count`**, and the `node_modules` symlink is stripped
  before commit.
- PRs target the **integration branch, never main/default** (preflight refuses the default branch).
- The codex brief keeps the **hard-safety block** (banned terms, sandbox-only payments, no secret
  values, no push/PR/main, no deploy, no test-gutting).
