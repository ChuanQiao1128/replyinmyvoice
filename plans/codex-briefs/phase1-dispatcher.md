# Codex Brief — Phase 1 Dispatcher Lane Routing

Status: **QUEUED — see plans/codex-worker-inbox.md (pickup by overnight shell supervisor)**
Authored: 2026-05-23 by Claude (supervisor mode)
Last-updated: 2026-05-23T16:08+12:00 — addendum for Amendment 2026-05-23-B (lease model)
Authority: `plans/lane-architecture-decisions.md` §1.1, §1.3, §3, §7, §8 Phase 1 smoke, §13 worker classes

---

## Addendum — Amendments 2026-05-23-B (lease) + 2026-05-23-C (escalation ladder)

Per the amendments (in `plans/lane-architecture-decisions.md`), the registry now carries 11 additional fields on every item beyond the original schema:
- From Amendment B: `worker_class`, `initial_lease_seconds`, `lease_renewal_interval_seconds`, `heartbeat_interval_seconds`, `max_wall_seconds`
- From Amendment C: `worker_runtime`, `review_class`, `min_level`, `current_level`, `level_attempts`

**Phase 1 implementation is UNAFFECTED by both.** The amendments' §13.5 and §14.5 explicitly state "Phase 1 selector only — does not invoke workers; new fields read but not acted on." The dispatcher just selects the next item; lease/heartbeat/wall-time enforcement is Phase 2 (scoped) and Phase 7a (completion-bound) work, and escalation-ladder logic is Phase 4 (repair) + Phase 5 (planner) work.

The selector implementation should:
- Read the new fields without parsing them (jq `.items[]` extraction is the same as before; no per-field logic needed)
- NOT use `timeout_seconds` — it has been renamed to `max_wall_seconds` per Amendment B
- NOT implement any lease renewal, heartbeat polling, wall-time enforcement, level auto-escalation, or telemetry writes in this PR
- NOT consult `current_level` when selecting — Phase 1 uses lane priority only (epic > evidence > repair > direct) per §7

If the selector logic ever needs a timeout-like value for its own dry-run (it shouldn't — `--selector-dry-run` exits in <100ms), use a hardcoded 5-second jq timeout via `timeout 5 jq ...`.

After Phase 1 PR is merged, the supervisor MUST auto-enqueue a Phase 2 work item per the inbox auto-progression rule (see plans/codex-worker-inbox.md Phase 1 item, AUTO-PROGRESSION RULE bullet). This is a SEPARATE inbox task, not a prerequisite for Phase 1 done-marking.

All other acceptance criteria below remain valid.

---

---

## TASK

Add a `select_next_item_by_lane()` function to `plans/overnight-supervisor.sh` that reads `plans/loop-registry.json` and applies the dispatcher filter expression from `plans/lane-architecture-decisions.md` §7. Wire it into the main loop alongside the existing `find_next_pending_issue()` (which becomes the direct-lane fallback for items not yet in the registry).

Phase 1 ships in **dry-run mode only**: the new function prints `"selected lane: <X>, item: <id>"` and exits without dispatching the item. The existing direct-only path remains active and unchanged in normal runs until the user explicitly enables lane routing.

---

## CONTEXT

- Repo root: `/Users/qc/Desktop/CloudFlare`
- Read first:
  - `/Users/qc/Desktop/CloudFlare/AGENTS.md` — project rules
  - `/Users/qc/Desktop/CloudFlare/plans/lane-architecture-decisions.md` §1.1, §1.3, §3, §6, §7, §8 (Phase 1 smoke definition)
  - `/Users/qc/Desktop/CloudFlare/plans/loop-registry.json` — the file the dispatcher will read
  - `/Users/qc/Desktop/CloudFlare/plans/overnight-supervisor.sh` — focus on the existing `find_next_pending_issue()` function (around line 352) and the main loop call (around line 1156)
  - `/Users/qc/Desktop/CloudFlare/plans/issue-classification.md` — input that produced the registry (for context only; not read at runtime)
- Project rules in effect:
  - `AGENTS.md` — Supervisor Mode, banned terms, secrets policy, deployment cutover gates
  - `plans/lane-architecture-decisions.md` §6 hard stops — banned-term grep, secret values, dashboard-only actions
  - Current sprint posture: live overnight loop is running; this change must NOT take over dispatch on first merge — it must be opt-in via a `LANE_DISPATCH=1` env flag

---

## CONSTRAINTS

- **Banned terms** (CI grep guard, scope `app/ components/ public/ lib/`): `humanizer`, `bypass`, `undetect`, `detector`, `evade`. Bash and shell code under `plans/` is not in scan scope, but do not introduce these terms in comments anywhere — `git grep` may extend scope later.
- **Secrets**: never read or log values from `.env.local`, `.dev.vars`, `globalapikey/**`. The dispatcher reads `plans/loop-registry.json` only; it does NOT load env values from secret files.
- **Deploy commands**: do not run `wrangler deploy`, `gh pr merge`, `git push`, or any state-mutating command in this change. The dispatcher function only reads files and prints to stdout/log.
- **Production safety**: until the user enables `LANE_DISPATCH=1`, the new code path must be unreachable in the live overnight loop. Default behavior identical to current.
- **No source edits outside the dispatcher script**: this brief modifies only `plans/overnight-supervisor.sh` and adds a new test under `tests/supervisor/`. Do NOT touch any `.ts`, `.tsx`, `.cs`, `.py`, Prisma schema, migration, or CI workflow file.

---

## CHANGES REQUIRED

### 1. New function in `plans/overnight-supervisor.sh`

Add `select_next_item_by_lane()` near `find_next_pending_issue()` (around line 352). The function:

1. Reads `plans/loop-registry.json` using `jq` (already a project dependency — confirm via `command -v jq`; if missing, fail fast with a clear error).
2. Computes the candidate set per lane:
   - **epic**: items where `owner_class=="strong-model"` AND `status=="pending"` AND `planner_attempts < 3` (planner_attempts defaults to 0 if not present — tracked in a future phase, not Phase 1)
   - **evidence**: items where `evidence_type` is set AND `status=="pending"` (Phase 1 ignores the auto/manual distinction — that gates verifier choice, not dispatcher eligibility)
   - **repair**: SKIP for Phase 1 — no repair-queue.json yet. Print a one-line note if any candidate appears.
   - **direct**: items where `owner_class=="loop"` AND `coupling in {low, medium}` AND `brief_state=="detailed"` AND `status=="pending"`. (Phase 1 does NOT check `requires_invariants ⊆ provided_invariants` — that arrives with the planner in Phase 5.)
3. Applies priority order per §7: `epic > evidence > repair > direct`.
4. Within the selected lane, sorts by lowest M-number (natural sort on the M\d+(\.\d+)?-\d+ pattern), then lowest item id, then oldest `added_at` if present (defaults to epoch 0).
5. Prints exactly one line to stdout: `selected lane: <lane>, item: <id>` and returns 0. If no candidate exists in any lane, prints `selected lane: none, item: -` and returns 0.

The function MUST NOT mutate any file or dispatch the item. It MUST NOT call `codex`, `git`, `gh`, `wrangler`, or any network tool.

### 2. Gated wiring in main loop

Around the existing call site at line 1156 (`NEXT=$(find_next_pending_issue)`), add an `if [ "${LANE_DISPATCH:-0}" = "1" ]; then ...` branch that calls `select_next_item_by_lane` instead and logs the output. The branch must:

- Run `select_next_item_by_lane` and capture the output.
- Log the line to `$LOG` (the existing log path used by the script).
- Continue with normal `find_next_pending_issue` execution **regardless** of what lane routing chose. Phase 1 is observation-only; the existing dispatcher still drives the loop.

### 3. New test `tests/supervisor/test-lane-dispatch.sh`

A bash test (run via `bash tests/supervisor/test-lane-dispatch.sh`) that:

1. Sources `plans/overnight-supervisor.sh` in a way that does not start the loop (the script must support `if [ "${SUPERVISOR_SOURCING_ONLY:-0}" = "1" ]; then return 0; fi` near the top, before any `main` execution — add this guard if not present).
2. Creates a temporary fixture registry under `/tmp/test-lane-dispatch-<pid>.json` with 6 hand-crafted items covering each branch:
   - One epic-lane pending item
   - One evidence-lane stripe-event pending item
   - One direct-lane pending item with `brief_state=detailed`
   - One direct-lane pending item with `brief_state=manifest-only` (should be skipped per gate)
   - One in_progress item (should be skipped)
   - One epic-lane item with `planner_attempts=5` (should be skipped per <3 gate)
3. Overrides `REGISTRY_PATH` (introduce this variable at the top of the dispatcher script) to point at the fixture.
4. Runs `select_next_item_by_lane` and asserts output is exactly `selected lane: epic, item: <fixture-id>` (epic has highest priority).
5. Removes the fixture's epic-lane item, re-runs, asserts output is `selected lane: evidence, item: <id>`.
6. Continues drain-down: evidence → direct → none, asserting each step.

The test must run in <5 seconds and exit 0 on success, non-zero on any assertion fail.

### 4. Documentation note

Add a comment block above `select_next_item_by_lane` referencing `plans/lane-architecture-decisions.md` §7 so future readers find the contract.

---

## ACCEPTANCE

A change is acceptable if ALL of the following pass:

1. `bash -n plans/overnight-supervisor.sh` exits 0 (syntax check).
2. `bash tests/supervisor/test-lane-dispatch.sh` exits 0 (the new test).
3. Running `LANE_DISPATCH=1 SUPERVISOR_SOURCING_ONLY=0 plans/overnight-supervisor.sh` against the real `plans/loop-registry.json` produces a single line `selected lane: epic, item: M1-002` (epic has priority; lowest-M-number first). The script should then continue with its existing direct path — i.e., the LANE_DISPATCH=1 branch is observation, not control.
4. Running the script with `LANE_DISPATCH` unset (or `=0`) produces output byte-identical to the pre-change baseline on the same board state. Capture both with `diff` against a fresh `git stash` of the current script.
5. `grep -RniE "humanizer|bypass|undetect|detector|evade" plans/overnight-supervisor.sh tests/supervisor/` returns nothing.
6. The diff is confined to: `plans/overnight-supervisor.sh`, `tests/supervisor/test-lane-dispatch.sh` (new). `git diff --name-only` shows exactly those two paths.

---

## DO NOT

- Touch any file outside `plans/overnight-supervisor.sh` and `tests/supervisor/test-lane-dispatch.sh`.
- Modify `find_next_pending_issue` or its call site behavior when `LANE_DISPATCH` is unset.
- Implement actual dispatch (codex call, git operation, gh call) inside `select_next_item_by_lane`. This function is a pure selector.
- Implement repair-lane logic — that arrives in Phase 4.
- Validate `requires_invariants` — that arrives with the planner in Phase 5.
- Auto-mark any registry item as done, in_progress, or blocked. Phase 1 is observation only.
- Use Python or Node to parse JSON — `jq` is the project's existing shell tool. Adding a Python script for this would introduce drift.

---

## SMOKE TEST (the gate that decides whether Phase 1 ships)

Run by Claude (supervisor) after Codex returns the diff:

```bash
cd /Users/qc/Desktop/CloudFlare
bash -n plans/overnight-supervisor.sh
bash tests/supervisor/test-lane-dispatch.sh
LANE_DISPATCH=1 plans/overnight-supervisor.sh --selector-dry-run 2>&1 | head -5
# Expected: 'selected lane: epic, item: M1-002' on the first matching line
LANE_DISPATCH=0 plans/overnight-supervisor.sh --selector-dry-run 2>&1 | head -5
# Expected: identical to current behavior on this board state
grep -RniE "humanizer|bypass|undetect|detector|evade" plans/overnight-supervisor.sh tests/supervisor/
# Expected: no matches
```

(Add a `--selector-dry-run` mode to the supervisor script that runs the selection logic and exits, without entering the main poll loop. If the script does not already have a parameterized entry point, introduce one near the top using `case "${1:-}"` parsing.)

If all four checks pass, Claude appends a "Phase 1 smoke green" entry to `plans/decisions-log.md` and reports to the user. If any check fails, the diff is returned to Codex with the specific failure for retry (up to 2 retries per the active sprint failure-handling policy).

---

## ROLLBACK

If Phase 1 smoke fails 3 times: revert the dispatcher commit (`git revert <sha>`), mark Phase 1 as `failed-implementation` in `plans/decisions-log.md`, and escalate to user with a summary of what specifically failed.

The change is non-destructive by design — even if merged with a latent bug, `LANE_DISPATCH=0` (the default) preserves current behavior.
