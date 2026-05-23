# Level Telemetry — Schema Reference

Path: `/Users/qc/Desktop/CloudFlare/plans/level-telemetry.jsonl`
Format: JSON Lines (append-only)
Authority: `plans/lane-architecture-decisions.md` §14.7

Every completed worker attempt — success, failure, escalated, or aborted — appends one JSON line to `plans/level-telemetry.jsonl`. The file is the truth-source for tuning escalation defaults in §14.4 over time. It is also read by Phase 4 repair-lane to compute root-cause hashes and by Phase 5 epic-planner to estimate likely children-success rates.

## Record schema (v1)

```json
{
  "attempt_ts": "ISO 8601 with timezone, e.g. 2026-05-23T16:45:00+12:00",
  "issue_id": "M5-003 | M1-Entra-001 | REPAIR-20260523T1645+12 | ...",
  "epic_id": "M1-Entra | null",
  "level": 1,
  "worker_class": "scoped | completion-bound | coordinator-poll",
  "worker_runtime": "codex-cli | claude-code-worktree | none",
  "review_class": "none | claude-checkpoint-review | human-spec",
  "wall_seconds": 1850,
  "outcome": "success | failure | escalated | aborted-wall",
  "failure_reason": "<one-line human-readable> | null",
  "failure_category": "scoped-worker-no-progress | completion-bound-no-progress | wall-time-exceeded | completion-bound-validation-regressed | acceptance-failed | plan-validation-failed | banned-term-violation | unknown | null",
  "cost_usd_estimate": 0.40,
  "diff_lines_added": 142,
  "diff_lines_removed": 18,
  "files_touched": ["scripts/analyze-rewrite-quality.ts"],
  "acceptance_passed": true,
  "acceptance_failures": [],
  "level_attempts_after": {"1": 1, "2": 0, "3": 0, "4": 0}
}
```

## Field notes

- `attempt_ts`: when the attempt completed (success or failure), not when it started. Reading the file in time order gives chronology of outcomes.
- `issue_id`: the registry id. For repair-lane attempts originating from a parent failure, use the repair item's own id (e.g., `REPAIR-20260523T1645+12`) and put the parent id in `failure_reason`.
- `epic_id`: null if the item is not a child of any epic. Set for planner-produced children so per-epic success rate can be aggregated.
- `level`: 1, 2, 3, or 4. (L0 and L5 never produce telemetry — they have no worker.)
- `worker_class` / `worker_runtime` / `review_class`: the tuple the dispatcher chose. Should match the registry entry's values at the time of attempt, but record at write-time in case the registry changes between dispatch and completion.
- `wall_seconds`: elapsed wall time from worker start to end (or kill).
- `outcome`:
  - `success`: acceptance passed, item marked done.
  - `failure`: worker exited but acceptance did not pass.
  - `escalated`: L3a claude-checkpoint-review or §14.6 auto-escalation triggered mid-flight.
  - `aborted-wall`: max_wall_seconds hit; worker killed.
- `failure_reason` / `failure_category`: null on success. On failure, both must be set. Categories listed exhaustively in §14.7.
- `cost_usd_estimate`: best-effort token-cost computation. Omit the field entirely (do not write 0) if it cannot be computed. Phase 2 picks the rate table.
- `diff_lines_added` / `diff_lines_removed`: from `git diff --stat` of the worker's branch against its parent. 0 on aborted-wall if nothing committed.
- `files_touched`: absolute or repo-relative paths the worker modified. Cross-checked against the item's owned_paths in acceptance.
- `acceptance_passed`: bool. True only if every entry in the brief's acceptance list executed and returned exit 0.
- `acceptance_failures`: list of strings (one per failed acceptance check) when acceptance_passed=false. Empty otherwise.
- `level_attempts_after`: the registry item's level_attempts map AFTER this attempt was recorded. Useful for replaying state if the registry is rebuilt.

## Append-only discipline

- Never edit a line in place. Corrections are appended as a new line with `outcome="correction"` (NOT in the v1 outcome enum yet — Phase 4 picks if/when needed).
- Rotation: when the file exceeds 10MB, archive to `plans/level-telemetry-<YYYY>.jsonl.gz` and start a fresh file. No automation yet — operator does this manually until volume justifies it.
- Read-only for workers. Only the supervisor writes. Workers may not append to this file directly — they emit their attempt summary to a sidecar `plans/worker-state/<id>.summary.json` and the supervisor copies the relevant fields into telemetry.

## Phase implementation mapping

- Phase 2 (scoped+codex-cli): first writer. Implements the full schema for direct-lane scoped attempts.
- Phase 4 (repair): reads `failure_category` distribution per `issue_id` to compute repair-route. Writes its own attempts (for the repair worker itself).
- Phase 5 (epic-planner): reads `level_attempts_after` aggregated by `epic_id` to estimate planner-child success rate. Writes a one-line record per planner invocation (with `worker_class="planner-exec"`).
- Phase 7a (completion-bound+codex-cli): writes per-attempt records including checkpoint-by-checkpoint cost if available.

## What this schema deliberately does NOT include

- The full checkpoint history (only the FINAL attempt summary lands in telemetry). For per-checkpoint state, see the worker-state sidecar files under `plans/worker-state/`.
- The full diff content (only stats). To inspect the diff of a past attempt, look in `plans/worker-aborted/<id>-<ts>.patch` for aborted runs, or `git show` the PR commit for completed runs.
- The model name or version (e.g., `gpt-5-codex-2026-05` or `claude-sonnet-4-7`). Phase 2 may add `worker_model` if the model changes within a runtime category and we need to compare; until then runtime is enough.
