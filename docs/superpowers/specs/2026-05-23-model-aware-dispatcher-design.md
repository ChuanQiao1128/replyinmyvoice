# Model-Aware Dispatcher Specification

## Context

The current overnight automation uses one supervisor loop in one git worktree. That model keeps git ownership simple, but it serializes every issue and makes failures in shared files such as `plans/current-task.md`, `plans/task-status.json`, `plans/issue-board.md`, and `plans/codex-worker-inbox.md` high blast-radius.

Recent supervisor failures showed that parallel execution must not mean multiple workers sharing the same checkout. Parallel work is only safe if each worker has an isolated git worktree and a coordinator serializes shared ledger updates, PR creation, CI checks, and merges.

The user also clarified an important cost and quality rule: a strong model should receive a coherent whole task instead of having that task split across several lower-context workers. Parallelism is useful for independent domains, not for tightly coupled work.

Source-of-truth inputs:

- `AGENTS.md` project policy for skills, skill-run-log, autonomous runs, and user-only actions.
- `plans/overnight-supervisor.sh` current single-loop supervisor.
- `plans/issue-board.md` issue queue and statuses.
- `plans/codex-worker-inbox.md` repair queue.
- `docs/skill-run-log.md` evidence log.

Assumption: "strong model" means a configured high-capability planner/implementer profile such as Claude Opus-class execution when available. The dispatcher must not hardcode a vendor secret or require a model that is unavailable in the current environment.

## Goals

- Route coherent, high-coupling tasks to one strong-model owner.
- Route only independent work to multiple Codex workers in separate git worktrees.
- Preserve token efficiency by avoiding repeated context loads for one tightly coupled task.
- Prevent workers from racing on git checkout, shared task/status files, board updates, PR creation, CI waits, or merges.
- Keep all live-money, secret, dashboard, publish, and provider-dependent actions out of autonomous worker assignment unless explicitly allowed by the operator.
- Produce machine-readable run state so Claude or another coordinator can resume safely after interruption.

## Non-Goals

- Do not replace human review for live Stripe money, npm publishing, provider dashboards, production secrets, or explicit business decisions.
- Do not let worker processes merge PRs directly.
- Do not let multiple workers edit the same file family without coordinator approval.
- Do not split a single coherent architecture, billing, auth, rewrite-engine, or supervisor task just to increase concurrency.
- Do not deploy or restart the overnight loop as part of the dispatcher design.

## Current System

The single supervisor loop:

- Reads `plans/issue-board.md` and `plans/codex-worker-inbox.md`.
- Writes one shared `plans/current-task.md`.
- Expects one shared `plans/task-status.json`.
- Runs one `codex exec` at a time.
- Owns git branch creation, commits, PR creation, CI polling, merge, and board updates.

This is safe only when one loop owns the checkout. It becomes unsafe if several workers share the same worktree because every worker would compete for the same current-task/status files and the same git index.

## Proposed Architecture

Use a three-role architecture.

1. Dispatcher

   The dispatcher reads issue board and repair inbox state, classifies tasks, and decides whether to assign a whole task to a strong-model owner or split independent work into a parallel group.

2. Worker

   A worker receives exactly one assignment in its own git worktree. It may edit files, run tests, and write a worker-local status file. It must not update the central board, central inbox, or merge PRs.

3. Coordinator

   The coordinator serializes shared operations: claim recording, final review, commit, push, PR creation, CI polling, merge, issue closure, and central ledger updates.

### Assignment Modes

Strong-model single-owner mode:

- Default for tightly coupled work.
- One strong model receives the full task brief and relevant context.
- Used for architecture, supervisor, billing, auth, rewrite pipeline, quota/state machine, deployment readiness, and broad refactors.

Domain-parallel mode:

- Used when the task decomposes into independent file domains.
- Example: one worker for frontend UI, one for backend API, one for docs/tests.
- Each worker must have declared file ownership before starting.

Mechanical-parallel mode:

- Used for low-risk repeated changes with predictable contracts.
- Example: fixture expansion, independent docs cleanup, static scan follow-ups.
- Lowest priority because it can create many small PRs and review overhead.

Blocked/manual mode:

- Used for live money, provider dashboards, secrets, npm publish, production DB verification, or tasks blocked by unavailable network/provider dependencies.

## Data Model

Create dispatcher run state under:

```text
plans/dispatcher-runs/<run-id>/
```

Suggested files:

```text
run.json
assignments/<assignment-id>.json
worker-logs/<assignment-id>.log
worker-status/<assignment-id>.json
merge-queue.json
```

`run.json`:

```json
{
  "run_id": "dispatch-YYYYMMDDHHMMSS",
  "status": "planning",
  "base_branch": "main",
  "max_parallel": 2,
  "created_at": "ISO-8601",
  "updated_at": "ISO-8601"
}
```

`assignment.json`:

```json
{
  "assignment_id": "A001",
  "source_type": "issue-board",
  "source_id": "M8-002",
  "mode": "strong_single_owner",
  "owner_profile": "strong-model",
  "branch": "codex/dispatch-M8-002-api-key-ui",
  "worktree": "/absolute/path",
  "status": "queued",
  "owned_paths": ["app/app/api-keys/**", "components/**", "tests/unit/**"],
  "forbidden_paths": [".env.local", ".dev.vars", "globalapikey/**"],
  "requires_user_action": false
}
```

`worker-status.json`:

```json
{
  "assignment_id": "A001",
  "next_action": "ready_for_coordinator",
  "files_changed": [],
  "checks": {
    "lint": "pass",
    "typecheck": "pass",
    "tests": "pass"
  },
  "summary": "Concise worker result",
  "limitations": []
}
```

## API and Job Contracts

Dispatcher command:

```text
plans/dispatcher.sh plan --max-parallel 2
plans/dispatcher.sh run --run-id <run-id>
```

Coordinator command:

```text
plans/dispatcher.sh reconcile --run-id <run-id>
```

Worker prompt contract:

- Read only the assignment file and referenced source files.
- Work only inside the assigned worktree.
- Do not run `git checkout`, `git merge`, `git rebase`, `git push`, `gh pr`, or `gh pr merge`.
- Write `plans/dispatcher-runs/<run-id>/worker-status/<assignment-id>.json`.
- Do not edit `.env.local`, `.dev.vars`, secret files, provider dashboards, live money, or central ledgers unless assigned by the coordinator.

Coordinator contract:

- Check worker diff against `owned_paths`.
- Reject undeclared paths.
- Run relevant checks or verify worker evidence.
- Commit and push from the worker worktree.
- Open PRs.
- Poll CI.
- Merge only one PR at a time.
- Update `plans/issue-board.md`, `plans/codex-worker-inbox.md`, and `docs/skill-run-log.md` from the main checkout after merge.

## State and Error Handling

### State List

- `queued`: discovered but not classified.
- `classified`: mode and risk category selected.
- `assigned_single_owner`: one whole-task worker selected.
- `assigned_parallel_group`: independent worker group selected.
- `worktree_ready`: isolated checkout exists.
- `worker_running`: worker process is active.
- `status_ready`: worker wrote a valid status file.
- `review_required`: coordinator must inspect diff and status.
- `pr_open`: branch pushed and PR exists.
- `ci_waiting`: checks are pending.
- `merge_ready`: CI reached successful terminal state.
- `merged`: PR merged and central ledgers updated.
- `blocked`: assignment cannot proceed autonomously.
- `stopped`: run stopped intentionally.

### Event List

- `classify_task`
- `assign_single_owner`
- `split_parallel_group`
- `create_worktree`
- `start_worker`
- `worker_status_written`
- `worker_failed`
- `coordinator_review_passed`
- `coordinator_review_failed`
- `pr_created`
- `ci_passed`
- `ci_failed`
- `ci_timeout`
- `merge_completed`
- `manual_stop`

### Transition Table

| From | Event | To | Side effect |
| --- | --- | --- | --- |
| `queued` | `classify_task` | `classified` | Record coupling score and risk flags. |
| `classified` | `assign_single_owner` | `assigned_single_owner` | Reserve one whole task for a strong-model worker. |
| `classified` | `split_parallel_group` | `assigned_parallel_group` | Create file ownership partitions. |
| `assigned_single_owner` | `create_worktree` | `worktree_ready` | Create one branch/worktree. |
| `assigned_parallel_group` | `create_worktree` | `worktree_ready` | Create one branch/worktree per assignment. |
| `worktree_ready` | `start_worker` | `worker_running` | Launch worker with assignment prompt. |
| `worker_running` | `worker_status_written` | `status_ready` | Validate JSON schema. |
| `status_ready` | `coordinator_review_passed` | `pr_open` | Commit, push, create PR. |
| `status_ready` | `coordinator_review_failed` | `blocked` | Preserve branch and record reason. |
| `pr_open` | `ci_passed` | `merge_ready` | Queue for serial merge. |
| `pr_open` | `ci_failed` | `blocked` | Leave PR open and create repair item. |
| `pr_open` | `ci_timeout` | `blocked` | Do not merge unknown CI state. |
| `merge_ready` | `merge_completed` | `merged` | Update central ledgers on main. |
| Any nonterminal | `manual_stop` | `stopped` | Stop new assignments; let active worker finish or terminate by policy. |

### Invariants

- One source issue can have only one active assignment group.
- One file path can be owned by only one active worker unless explicitly marked shared-read-only.
- Workers must not update central board/inbox files.
- Workers must not inherit GitHub write credentials unless the assignment explicitly needs read-only GitHub context.
- Coordinator is the only role allowed to push branches, create PRs, merge PRs, or update central ledgers.
- CI pending, missing, unknown, or parse-failed states cannot transition to `merge_ready`.
- Strong-model single-owner mode is preferred whenever a task has high coupling, ambiguous requirements, broad architecture, or shared state.

### Illegal Transitions

- `ci_waiting -> merged` without `ci_passed`.
- `worker_running -> merged`.
- `classified -> assigned_parallel_group` when owned path partitions overlap.
- `assigned_parallel_group -> worker_running` for user-only, live-money, secret, or dashboard tasks.
- `blocked -> worker_running` without an explicit requeue event.

## Security and Privacy

- Do not pass provider secrets, `.env.local` contents, private keys, Stripe secrets, Clerk/Entra secrets, OpenAI keys, Sapling keys, Cloudflare tokens, or GitHub write tokens into worker prompts.
- Worker processes should run with GitHub write credentials unset.
- Assignment files may include paths and high-level requirements, but not secret values.
- Logs must not include raw private user content or credentials.
- Live-money and provider-dashboard tasks remain manual unless the operator explicitly creates a one-off authorized run.

## Rollout Plan

1. Merge supervisor hardening first.
2. Add dispatcher planning-only mode. It classifies tasks and writes `run.json` plus assignments, but launches no workers.
3. Add static contract tests for classification and state transitions.
4. Add single-worker execution using a separate worktree.
5. Add `MAX_PARALLEL=2` domain-parallel execution for independent docs/test/frontend/backend partitions.
6. Add coordinator reconcile command for commit, push, PR, CI, and merge.
7. Only after several successful dry runs, allow overnight use.

## Verification Plan

Unit/static contract tests:

- Strong-coupled tasks route to `strong_single_owner`.
- Independent frontend/backend/docs partitions route to `assigned_parallel_group`.
- Overlapping owned paths reject parallel assignment.
- User-only/live-money/secret/provider-dashboard tasks route to `blocked`.
- Worker status with undeclared paths is rejected.
- CI pending/no-checks/parse-error cannot become merge-ready.
- Duplicate dispatcher process cannot claim the same issue.
- Central board/inbox files cannot be modified by workers.

Integration smoke tests:

- Dry-run dispatcher writes assignments without launching workers.
- Single-worker run creates an isolated worktree and status file.
- Coordinator refuses to merge a PR with pending CI.
- Coordinator updates central ledgers only after merge.

Manual verification:

- Review `plans/dispatcher-runs/<run-id>/run.json`.
- Confirm worktrees are isolated.
- Confirm no worker changed `.env.local`, `.dev.vars`, or central ledgers.
- Confirm `git worktree list` shows expected worker paths.

## Open Questions

- Which concrete command should invoke the strong-model owner in this environment: Claude CLI, Codex with a strong model, or a manual handoff document?
- Should dispatcher PRs default to draft until the coordinator has two successful dry runs?
- What is the initial safe `MAX_PARALLEL`: 2 or 3?
- Should dispatcher state be kept in `plans/dispatcher-runs/` permanently, or archived after merge?
