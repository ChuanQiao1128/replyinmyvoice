# Delivery Pipeline — Wave 1 Handoff (site-overhaul)

> Self-contained handoff so a **fresh Claude Code session** can resume cold.
> Last updated by the supervisor after #253–#257 merged. Models locked: **codex = 5.5 (writes code), claude = 4.8 (orchestration + judgment)**.

## Resume in 30 seconds

1. Same machine, repo `/Users/qc/Desktop/CloudFlare`: `git checkout delivery/integration && git pull`
2. `rm .delivery/PAUSE`  (the previous session paused its /loop driver; remove to allow driving)
3. Read this file + `.delivery/plan.json` (status of all 22 issues).
4. Settle the **4 open decisions** (below), then run a **dynamic Workflow** to drive the rest.

Do **not** touch `main`. Do **not** change any issue's scope/AC. One PR per issue into `delivery/integration`; the final `integration → main` PR is **human-only**.

## Where we are

- Integration branch: **`delivery/integration`** (origin synced). Base was `feat/site-overhaul` (NOT main).
- **5 / 15 wave-1 done, merged into integration, issues CLOSED:**

| Issue | SO | PR | What |
|---|---|---|---|
| #253 | SO-001 | #276 | Remove dead Clerk env residue (CI workflow) |
| #254 | SO-002 | #277 | Harden ALLOW_HEADER_AUTH (no header-auth in prod) |
| #255 | SO-003 | #278 | Strip dead Clerk JWT from legacy Api host |
| #256 | SO-010 | #279 | Fix sign-in/up copy to match Entra OAuth |
| #257 | SO-011 | #280 | Remove unwired native email/OTP subsystem |

- `main` untouched. (Any local-main vs origin-main divergence is pre-existing, unrelated to this work.)

## Pending (17 issues, all `ready` in plan.json)

- **Remaining wave-1 (no deps):** 258, 259, 260, 261, 262, 263, 265, 266, 267, 270
- **wave-2:** 264 (dep 263) · 268 (dep 260) · 269 (dep 260) · 271 (dep 266,260,267)
- **wave-3:** 272, 273, 274 (each dep 266, 271, 270)

## Runtime state lives in `.delivery/` — gitignored, LOCAL to this machine

A fresh session on **this machine** has it; a fresh clone elsewhere does NOT (would need reconstruction from GitHub issues + this doc).

- `plan.json` — manifest: base, deps, status, pr per issue. Source of truth for the queue.
- `issue-map.txt`, `issues/SO-*.md` — decomposed issue bodies (scope, ACs with verify commands, non-goals, deps).
- `gen.py` — materializes, per issue N: `scope/issue-N.txt`, `checks/issue-N.txt` (LABEL::cmd lines), `prompts/issue-N.md` (Codex worker prompt). Run: `python3 .delivery/gen.py <N>`.
- `reports/`, `logs/` — verify signatures + bulk command output (never read whole into context).
- Skill scripts: `~/.claude/skills/delivery-pipeline/scripts/{dp-delegate,dp-verify,dp-state}.sh`.

### Two fixes already baked into `gen.py` — keep them

1. **banned-term check is DIFF-SCOPED.** It fails only if the issue's *own change* introduces `humanizer|bypass|undetect|detector|evade`. A repo-wide scan (the literal AGENTS.md command) false-fails **every** issue because the base branch already contains those substrings in the dead eval fixture `lib/rewrite-eval-cases.ts`. CI does NOT run the banned scan, so the base is not red.
2. **Gate 4 "secret" is a known FALSE-POSITIVE generator.** `dp-verify.sh` Gate 4 hard-fails if any *changed file* contains the words `secret|api_key|password|bearer`. This trips on `.env.example` var names, GitHub Actions `${{ secrets.* }}`, the `Bearer` scheme, JWT method/var names, and config-key names like `STRIPE_WEBHOOK_SECRET`. **The verify step must apply judgment:** override to PASS **only** if every match is a name/reference/placeholder with **no literal credential VALUE**. NEVER override the other Gate 4 reasons (`ts-suppress`, `eslint-disable`, `test-removed`, `test-deleted`, `disabled-test`) — those are real gaming signals.

## Execution mechanism — dynamic Workflow (decided)

**Why:** a raw background `codex exec` (Bash run_in_background) is a session child and was **killed on a session restart**. The `/loop` short-session mode helped, but the real win is moving orchestration **off-context** into a Workflow script: loops/branches/intermediate results live in script variables; this session only ever sees the final report. Codex's bulk output stays in each worker's discarded context.

**Hard constraints of the Workflow tool:**
- The script can only `agent()/parallel()/pipeline()/log()/phase()` — it **cannot** touch shell/FS directly. **`codex exec` must run inside a worker agent.**
- Concurrency ≤ 16 simultaneous agents; ≤ 1000 agents total per run.
- **Resume is same-session only.** Mitigation: make the script **idempotent** — phase 0 reads `plan.json` + `gh` labels and processes only `status != verified` issues, so re-running after any kill skips finished work. This is the durable fix for the session-kill problem.

**Proposed shape:**
- **Phase 0 — idempotent state (1 agent):** `gh issue list` + read `plan.json` → list of not-yet-verified issues with deps satisfied.
- **Phase 1 — parallel fan-out (≤ N, `isolation:'worktree'`):** per issue a thin worker → in its own worktree: verify-first (skip codex if tree already passes) → else `dp-delegate.sh` (codex 5.5) → `dp-verify.sh` (4 gates + Gate 4 FP rule) → commit to its branch → return one line `{issue, PASS/FAIL, branch, note}`.
- **Phase 2 — SERIAL merge (dep order, a merge agent):** merge each verified branch into `delivery/integration` one at a time, re-build/test after each, push. Conflicts/migrations handled here.
- **Phase 3 — optional adversarial cross-check:** independent agents re-review PASS diffs for "fake green".

## Why NOT naive 16-way parallel (parallel vs serial partition)

- **4 migration issues — 260, 263, 267, 270** — each edits the shared EF `ModelSnapshot` + needs ordered migration history → **MUST serialize** among themselves (rebase each on the latest integration, regenerate migration).
- **Shared files / deps:** 266 builds the admin scaffolding (`AdminHttpFunctions`/`AdminAccess`) that 271/272/273/274 need; 265 (`/api/me`) may overlap wave-2 269 → can't co-batch.
- **Safe parallel set P1** (no migration, no deps, *likely* non-overlapping): **258, 259, 261, 262, 265, 266** — confirm with a scope-overlap matrix before running; move any file-overlapping pair into the serial lane.
- **Serial migration lane S1:** 260 → 263 → 267 → 270.
- Then wave-2 once deps merged, then wave-3.

## Per-issue worker recipe (proven on 5/15)

1. `python3 .delivery/gen.py N` ; `git checkout -B delivery/N-<slug>` off integration ; `rm -f .delivery/reports/issue-N.failing.log` ; label `in-progress`.
2. Worker (in a worktree if parallel): verify-first → else `dp-delegate.sh --issue N --slug <slug> --prompt .delivery/prompts/issue-N.md` (runs `codex exec`, edits tree, **does not commit/push**) → `dp-verify.sh --issue N --scope … --checks …` → apply Gate 4 FP rule → return verdict.
3. On PASS (supervisor/merge-agent does git): commit `Closes #N` + push + `gh pr create --base delivery/integration` + `gh pr merge --squash --delete-branch` + `git checkout delivery/integration && git pull --ff-only` + `dp-state.sh set --issue N --status verified --pr URL` + `gh issue edit N --add-label verified --remove-label in-progress` + `gh issue close N`.
4. On FAIL: recover (re-delegate with the failing tail) until `streak>=5` or `attempt>=10` → label `blocked`, comment reason, skip.

## 4 open decisions (need owner)

1. **Run scope:** finish remaining wave-1 (10) then stop, or the whole remaining queue (17)?  *(supervisor rec: wave-1 first, prove the workflow, then extend.)*
2. **Parallelism:** P1 concurrency = 6 + migrations serial (rec), or conservative 3–4 to shrink conflict blast radius?
3. **Audit trail:** keep one PR per issue into integration (rec), or squash straight into integration with a single summary table?
4. **The 5 merged stay as-is** — assumed yes.

## Commands cheat-sheet

```bash
# queue status / next ready
bash ~/.claude/skills/delivery-pipeline/scripts/dp-state.sh --repo . status
bash ~/.claude/skills/delivery-pipeline/scripts/dp-state.sh --repo . next
# materialize a worker's scope/checks/prompt
python3 .delivery/gen.py <N>
# delegate (codex) / verify (4 gates) — run INSIDE a worker agent, not the script
bash ~/.claude/skills/delivery-pipeline/scripts/dp-delegate.sh --repo /Users/qc/Desktop/CloudFlare --issue <N> --slug <slug> --attempt 1 --prompt /Users/qc/Desktop/CloudFlare/.delivery/prompts/issue-<N>.md
bash ~/.claude/skills/delivery-pipeline/scripts/dp-verify.sh   --repo /Users/qc/Desktop/CloudFlare --issue <N> --scope /Users/qc/Desktop/CloudFlare/.delivery/scope/issue-<N>.txt --checks /Users/qc/Desktop/CloudFlare/.delivery/checks/issue-<N>.txt
```
