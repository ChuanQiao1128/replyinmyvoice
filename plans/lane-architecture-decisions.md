# Lane Architecture — Phase 0 Decision Record

Status: **ACTIVE — §10 questions resolved 2026-05-23T14:50+12:00**
Authored: 2026-05-23
Resolved: 2026-05-23 (see §10 RESOLUTION RECORD)
Authority: This document is the binding contract for Phase 1–7 of the lane-based autonomous loop. Any deviation in implementation must update this file first (PR-style amendment in the same file) before code lands.
Scope: Supersedes ad-hoc dispatcher behavior in `plans/overnight-supervisor.sh`. Does not override `AGENTS.md`. If conflict, `AGENTS.md` wins and this file must be amended.

---

## 1. Architectural decisions (LOCKED)

### 1.1 Lane-based universal loop

All work items the supervisor system handles are dispatched through a single loop. The loop's first action per iteration is to classify the next pending item by **lane**, then route to a lane-specific handler. The previous `find_next_pending_issue` becomes the direct-lane handler, not the loop itself.

### 1.2 Five lanes (only the first four are inside the loop)

```text
direct    — small scoped tasks with detailed brief; Codex worker executes
epic      — large coordinated tasks; strong-model planner produces child
            tasks that flow back into direct-lane after validation
evidence  — launch gates and external-action items; loop prepares
            verification harness, watches for artifact, auto-closes when
            artifact matches expected shape
repair    — failures originating from any of the above; Codex repair
            worker fixes loop / tests / CI / plan validation issues;
            circuit-breaker on repeated root-cause
decision  — NOT inside the loop. Pure human business/judgment calls
            (pricing, legal posture, marketing positioning). Tracked
            elsewhere in the operator's personal task system.
```

### 1.3 State source: markdown-as-direct-truth + JSON-sidecar hybrid

`plans/issue-board.md` remains the truth for direct-lane status (status quo).
JSON sidecars are introduced for the richer state the new lanes need:

```text
plans/loop-registry.json              # per-item lane, classification,
                                      # owned_paths, evidence_type, etc.
plans/epics/<epic-id>/plan.md         # human-readable epic plan
plans/epics/<epic-id>/child-tasks.json  # machine-readable child list
plans/epics/<epic-id>/validation.log  # plan validation results
plans/repair-queue.json               # repair items with root-cause hash
plans/evidence-queue.json             # evidence-lane items with verifier
                                      # config and last-checked timestamp
plans/human-checklist.md              # generated, ordered by EXPECTED_EFFORT
```

The dispatcher reads JSON sidecars first to determine lane; direct-lane items continue to use the markdown board's status field after lane routing. Full JSON-as-only-truth migration is **deferred to v1** (post-launch).

### 1.4 Launch-pre vs launch-post phase split

```text
Launch-pre (must complete before live revenue):
  Phase 0  Decision record (this file)
  Phase 1  Registry + dispatcher lane routing
  Phase 2  Direct-lane hardening
  Phase 3  Evidence-lane (the launch-readiness lever)

Launch-post (deferred until first paid transaction):
  Phase 4  Repair-lane circuit breaker
  Phase 5  Epic-planner v0
  Phase 6  Integration strategy (single-pr coordinator)
  Phase 7  Multi-worktree parallel workers (only if proven need)
```

---

## 2. Lane state machines (LOCKED)

### 2.1 direct-lane

```text
pending → in_progress → review → done
                              ↘ blocked-<retryable category>
                              ↘ failed (terminal, generates repair item)
```

### 2.2 epic-lane

```text
pending → planning → planned → sharded → done
                            ↘ failed-planning (terminal, escalate-human)
```

`sharded` means: plan validation passed AND child tasks appended to direct-lane registry. The epic itself does not run code after sharding — its `done` is computed when all children done + (if `single-pr`) integration PR merged.

### 2.3 evidence-lane

```text
pending → awaiting-evidence → evidence-submitted → verifying → done
                                                            ↘ rejected
                            ↘ blocked-verifier (verifier itself
                              cannot run, e.g., out-of-sandbox HTTP)
```

`rejected` means: artifact present but does not match expected shape (e.g., Stripe event found but for wrong product). Loop alerts operator, does not auto-retry.

### 2.4 repair-lane

```text
queued → dispatched → done
                   ↘ retry (count++)
                   ↘ circuit-open (root-cause-hash ≥3 in 24h window)
```

`circuit-open` is terminal until operator manually resets. The repair item moves to `plans/repair-escalations.md` for human review.

---

## 3. Schema lock — `loop-registry.json` entry (v1)

Every dispatchable item has one entry. Default for any missing field is the **most restrictive** value (lane=evidence, owner_class=human-only, brief_state=missing, coupling=high), so unclassified items never auto-execute.

```json
{
  "id": "M1-002",
  "lane": "epic",
  "owner_class": "strong-model",
  "worker_class": "completion-bound",
  "worker_runtime": "codex-cli",
  "review_class": "none",
  "coupling": "high",
  "brief_state": "manifest-only",
  "status": "pending",
  "epic_id": null,
  "evidence_type": null,
  "owned_paths": [],
  "forbidden_paths": [],
  "depends_on": [],
  "initial_lease_seconds": 1200,
  "lease_renewal_interval_seconds": 1200,
  "heartbeat_interval_seconds": 300,
  "max_wall_seconds": 28800,
  "min_level": 1,
  "current_level": 1,
  "level_attempts": {"1": 0, "2": 0, "3": 0, "4": 0},
  "github_issue": "https://github.com/ChuanQiao1128/replyinmyvoice/issues/N",
  "added_at": "2026-05-23T00:00:00Z",
  "updated_at": "2026-05-23T00:00:00Z"
}
```

Field semantics:

- `lane`: one of `direct | epic | evidence | repair`
- `owner_class`: one of `loop | strong-model | human-only`
- `worker_class`: one of `scoped | completion-bound | coordinator-poll` (lifecycle)
  - `scoped`: single-shot worker, no mid-run state, no lease renewals (one lease only). Default for tiny direct-lane items.
  - `completion-bound`: long-running worker with file-based heartbeat + checkpoint + lease renewals (see §5.6). Default for epic-lane children and medium direct-lane.
  - `coordinator-poll`: no worker — the dispatcher itself polls the verifier per §5.5. Default for evidence-lane.
- `worker_runtime`: one of `codex-cli | claude-code-worktree | none` (implementation)
  - `codex-cli`: `codex exec` invoked from the supervisor shell. Both scoped and completion-bound classes can use this runtime. Default and cheapest.
  - `claude-code-worktree`: Claude Code launched in a dedicated git worktree. Higher cost, stronger reasoning. Used only at escalation level L4 (see §14).
  - `none`: coordinator-poll items have no runtime.
- `review_class`: one of `none | claude-checkpoint-review | human-spec` (who gates progress between checkpoints)
  - `none`: supervisor applies the §5.6 numeric rules at lease renewal and never invokes a reviewer model.
  - `claude-checkpoint-review`: at every checkpoint, supervisor passes the checkpoint JSON + diff to Claude (via Claude Agent SDK or Claude API) for a continue/abort/escalate decision in addition to §5.6 numeric rules. Used at L3.
  - `human-spec`: no checkpoint review — the gate is the human spec-doc the operator wrote upfront (`plans/specs/<id>.md`). Used at L3 when Claude's role is "wrote the spec" rather than "reads every checkpoint."
- `coupling`: one of `low | medium | high`
- `brief_state`: one of `detailed | manifest-only | missing`
- `evidence_type`: only set when lane=evidence; one of `stripe-event | db-row | http-200 | dns-record | file-present | sentry-api | posthog-api | manual-only`
- `owned_paths`: glob list; worker may modify only these
- `forbidden_paths`: glob list; worker must NOT modify these (e.g., `.env*`, `globalapikey/**`)
- `initial_lease_seconds`: how long the worker's first lease lasts before requiring renewal. Defaults inherited from §5.1' if not set.
- `lease_renewal_interval_seconds`: how long each subsequent lease lasts when the coordinator approves a renewal. Usually equal to `initial_lease_seconds`.
- `heartbeat_interval_seconds`: maximum gap between worker heartbeat signals before the coordinator considers the worker dead. See §5.6.
- `max_wall_seconds`: absolute ceiling. Even with healthy heartbeats, the worker is force-stopped at this point and the item moves to repair-lane with reason `wall-time-exceeded`. (Renamed from `timeout_seconds` per Amendment 2026-05-23-B.)
- `min_level`: the lowest escalation level this item may be attempted at. Defaults to 1 (Codex long-lease) per §14.4. Set to 4 explicitly for items that should bypass cheaper attempts (rare — usually only for known-deep-reasoning epics).
- `current_level`: the level dispatcher should pick next time it picks this item. Starts at `min_level`. Incremented by §14.6 auto-escalation rule on consecutive failures at the current level.
- `level_attempts`: per-level attempt counter. Schema `{"1": 0, "2": 0, "3": 0, "4": 0}`. Each completed attempt (success OR failure) at level N increments `level_attempts[N]`. See §14.6 for how this drives escalation.

---

## 4. Schema lock — plan v1 (`child-tasks.json`)

```json
{
  "plan_schema_version": 1,
  "epic_id": "M1-Entra",
  "integration_strategy": "single-pr",
  "epic_branch": "epic/M1-entra",
  "children": [
    {
      "child_id": "M1-Entra-001",
      "title": "Replace clerkMiddleware with Entra session check",
      "owned_paths": ["middleware.ts", "lib/auth/entra-session.ts"],
      "forbidden_paths": ["app/api/**", "components/**", ".env*"],
      "depends_on": [],
      "requires_invariants": [],
      "provides_invariants": ["auth:entra-session-validator"],
      "checks": [
        "npm run lint",
        "npm run typecheck",
        "npm run test -- middleware"
      ],
      "acceptance": [
        "middleware.ts no longer imports @clerk/nextjs",
        "request with valid Entra token reaches /app",
        "request with invalid token redirects to /sign-in"
      ],
      "timeout_seconds": 900,
      "coupling": "medium",
      "brief_state": "detailed"
    }
  ],
  "rollback_strategy": "git revert integration PR + manual Stripe DB reconciliation",
  "stop_conditions": [
    "any child fails after 2 retries",
    "integration tests fail",
    "banned-term grep match in any child diff"
  ]
}
```

`integration_strategy` is one of:

- `per-child` — each child PR targets `main` directly; epic done = all children done. Only valid for collection epics.
- `single-pr` — children target `epic_branch`; epic done = integration PR merged to main + full validation passed. Required for coordinated epics.

---

## 5. Runtime config locks

### 5.1 Per-class lease and wall-time defaults (replaces 5.1 per Amendment 2026-05-23-B)

```text
                                                 max wall   initial   heartbeat
worker_class      coupling     wall ceiling      seconds    lease     interval
─────────────────────────────────────────────────────────────────────────────
scoped            low          30 min              1800       900       (stdout proxy: 120s silence = dead)
scoped            medium       2 hours             7200      1200       (stdout proxy: 120s silence = dead)
scoped            high         NEVER (forces completion-bound via epic-lane)
completion-bound  high         8 hours            28800      1200       300s (file-based, see §5.6)
completion-bound  medium       4 hours            14400      1200       300s (file-based, see §5.6)
coordinator-poll  any          (no worker; verifier polls per §5.5)
planner-exec      —            30 min              1800       —         (planner is single-shot Codex)
```

Notes on the table:
- `scoped` workers are single-shot. Under `codex-cli` runtime, they run `codex exec` and exit; the supervisor cannot demand a mid-run file heartbeat, so liveness is detected by stdout silence (no output for 120s consecutive = worker considered hung; supervisor sends SIGTERM, captures partial diff, routes to repair-lane). Under `claude-code-worktree` runtime (rare for scoped), heartbeat is file-based per §5.6.
- `completion-bound` workers may use either `codex-cli` runtime (Phase 7a, levels L1-L3) or `claude-code-worktree` runtime (Phase 7c, level L4 only). Under `codex-cli`, the brief MUST instruct codex to write `plans/worker-state/<id>.heartbeat.json` at sub-step boundaries via heredoc — codex can do this when told to, the supervisor just cannot assume it without an explicit instruction. Under `claude-code-worktree`, the worker writes heartbeat naturally via the agent's file tool.
- Wall-time ceilings are HARD — even with healthy heartbeats, the worker is stopped at that point. `completion-bound` 8-hour ceiling is the worst-case for an entire single-pr epic; individual children should checkpoint and exit at logical sub-step boundaries to avoid stretching this.
- `coordinator-poll` is the no-worker case (evidence-lane). The dispatcher itself calls the verifier per §5.5 and there is no lease — the next dispatcher iteration handles re-poll.
- `planner-exec` retains its 30-min ceiling from the original §5.1 because the planner only writes plan files; it does not execute code.

### 5.2 Planner identity + plan validation (strengthened per Amendment 2026-05-23-C)

#### 5.2.1 Planner runtime

`codex exec` with the following invariants:
- timeout 1800 seconds
- system prompt restricts output to `plans/epics/<epic-id>/**` files only
- post-run path validation: `git diff --name-only` must return only paths under `plans/epics/<epic-id>/`; `package-lock.json` and other lockfiles are auto-restored via `git checkout` before the diff check
- max 3 attempts per epic; 3rd failure → epic marked `failed-planning` and `current_level` incremented per §14.6 (i.e., next attempt at L3 with a Claude-written spec rather than a Codex-written plan)

#### 5.2.2 Plan validation — 7 hard checks (LOCKED)

Before any child in `plans/epics/<epic-id>/child-tasks.json` is dispatched to a worker, the plan MUST pass all 7 of the following checks. The validator runs in the supervisor shell immediately after the planner writes the plan and before any child enters the registry.

```text
Check 1 — owned_paths non-overlapping across children
  For every pair of children (i, j) where i ≠ j:
    expand_globs(child_i.owned_paths) ∩ expand_globs(child_j.owned_paths) = ∅
  Rationale: two children racing on the same file is the most common
  source of bad merges in single-pr epics.

Check 2 — forbidden_paths is a superset of §6 hard-stop globs
  For every child:
    {".env*", ".dev.vars", "globalapikey/**", "wrangler.toml-deploy",
     "plans/loop-registry.json", "plans/level-telemetry.jsonl"}
    ⊆ child.forbidden_paths (as glob set)
  Rationale: hardstop globs must never be inherited-by-default; planner
  must repeat them explicitly per child.

Check 3 — every child has at least one executable acceptance check
  For every child:
    child.acceptance is non-empty
    AND at least one entry of child.acceptance is shell-executable
        (begins with one of: npm | bash | dotnet | python3 | bun | grep
         | git diff | rg | tsc | prisma | wrangler | jq)
  Rationale: "looks correct" acceptance never gates the executor;
  acceptance must be runnable.

Check 4 — no child has coupling=high
  For every child:
    child.coupling ∈ {"low", "medium"}
  Rationale: §13.4 forbids direct+high; planner-produced children are
  direct-lane after sharding. high coupling means planner did not shard
  enough — reject and re-plan.

Check 5 — total max_wall_seconds for the cluster ≤ epic's epic_wall_budget
  Σ over children child.max_wall_seconds ≤ epic.epic_wall_budget
  Default epic_wall_budget = 8 × 3600 = 28800 (the §5.1' completion-bound
  high ceiling). Override per-epic by setting epic.epic_wall_budget in
  child-tasks.json.
  Rationale: prevents the planner from producing 20 children each at
  8h wall (160h total wall would never complete in a useful window).

Check 6 — requires_invariants closure is topologically feasible
  Construct the DAG where edge (i → j) exists if child_j.requires_invariants
  has any element not in (children-with-lower-id provides_invariants
  ∪ already-satisfied initial state).
  Validate:
    - DAG has no cycles
    - Every requires_invariant appears in some earlier child's
      provides_invariants OR in the epic's initial_provided_invariants
  Rationale: if children need state in topological order that the
  planner did not actually order, executor will deadlock or skip.

Check 7 — banned-term scan over the entire plan
  grep -niE "humanizer|bypass|undetect|detector|evade" \
    plans/epics/<epic-id>/plan.md plans/epics/<epic-id>/child-tasks.json
  Must return zero matches.
  Rationale: §6 hard stop. Planner producing banned terms in plan
  text means it was prompted with or hallucinated forbidden vocabulary.
  Rejection is final, NOT a retry — it indicates the planner's input
  context was wrong; operator must inspect.
```

Validation procedure:
```text
1. Run all 7 checks.
2. If all 7 pass → write child-tasks.json's children into the registry
   with status=pending, current_level=1, parent epic.status=sharded.
3. If any check fails → write the failure list to
   plans/epics/<epic-id>/validation.log, increment planner_attempts,
   delete the rejected plan files, re-invoke planner with the failure
   list appended to its system prompt. Repeat up to 3 times.
4. 3rd validation failure → epic.status=failed-planning,
   epic.current_level++ (per §14.6); next dispatcher attempt at the
   new level (L3 = human-written spec at plans/specs/<epic-id>.md).
5. Check 7 failure on any attempt = immediate halt, no retry; operator
   reviews plans/epics/<epic-id>/validation.log.
```

### 5.3 Root-cause-hash function

```text
hash = sha256_first8(
  normalize_path(failure.file),       # strip cwd, strip line numbers
  normalize_symbol(failure.function), # bare function/test name, no decorators
  failure.error_class,                # TypeError, AssertionError, EPERM,
                                      # HTTP_502, ETIMEDOUT, etc.
  failure.lane                        # direct, epic, evidence, repair
)
```

Excluded from hash input: timestamps, line numbers, random object IDs, env-specific tokens, ANSI color codes, retry attempt count, PID, branch name.

Implemented as `scripts/compute-root-cause-hash.sh` in Phase 4 with ≥5 unit tests in `tests/repair/test-hash.sh`.

### 5.4 Repair circuit breaker

```text
window:    24 hours, sliding
threshold: 3 occurrences of same hash
action:    move repair item to plans/repair-escalations.md
           log "SUPERVISOR ALERT: repair circuit open for hash X" to overnight.log
           do not auto-retry
reset:     operator manually deletes the entry from
           plans/repair-escalations.md after addressing root cause
```

### 5.5 Evidence verifiers — automation matrix

```text
evidence_type     auto_verify  notes
stripe-event      yes          uses STRIPE_SECRET_KEY (already in .env.local)
db-row            yes          requires NEON_READONLY_URL (NEW — see §10 Q4)
file-present      yes          local fs check
dns-record        partial      `dig` may work in sandbox; fallback manual
http-200          no           sandbox blocks out-of-sandbox HTTP (M6-005)
                               → marked manual-only until host runner exists
sentry-api        no           SENTRY_AUTH_TOKEN not yet provisioned → manual
posthog-api       no           POSTHOG_API_KEY not yet provisioned → manual
manual-only       no           verifier prints checklist line; operator confirms
```

### 5.6 Heartbeat + checkpoint + lease protocol (added per Amendment 2026-05-23-B)

Scoped workers (Codex `exec`):

```text
- Heartbeat: stdout-proxy. Supervisor pipes Codex stdout to a tee'd log;
  silence ≥120s consecutive = worker considered dead.
- Checkpoint: none. Scoped is single-shot; partial progress is not preserved.
  If killed for silence or wall-time, the worktree diff is captured via
  `git diff > plans/worker-aborted/<id>-<ts>.patch` and the item routes to
  repair-lane with reason="scoped-worker-no-progress" OR "wall-time-exceeded".
- Lease: not used. `initial_lease_seconds` and `lease_renewal_interval_seconds`
  in the registry are advisory only for scoped — the wall-time ceiling is
  the only hard limit and the stdout silence threshold is the only liveness
  signal.
```

Completion-bound workers (Claude Code in worktree):

```text
- Heartbeat file: <worktree>/plans/worker-state/<issue-id>.heartbeat.json
  written every heartbeat_interval_seconds. Schema:
    {
      "issue_id": "M1-Entra-001",
      "worker_pid": 12345,
      "worker_started_at": "ISO timestamp",
      "last_heartbeat": "ISO timestamp",
      "state": "working|paused|blocked",
      "current_step": "free-text one-liner",
      "files_touched_since_checkpoint": ["abs paths"],
      "blockers": []
    }

- Checkpoint file: <worktree>/plans/worker-state/<issue-id>.checkpoint.json
  written every 20 minutes OR at logical sub-step boundaries (whichever
  comes first). Schema:
    {
      "issue_id": "M1-Entra-001",
      "worker_started_at": "...",
      "checkpoint_at": "...",
      "completed_steps": ["step description 1", "step description 2"],
      "remaining_steps": ["step description N"],
      "files_touched_total": ["abs paths"],
      "validation": {
        "lint": "pass|fail|not-run",
        "typecheck": "pass|fail|not-run",
        "tests": "pass|fail|partial|not-run"
      },
      "next_action": "continue|escalate|done"
    }

- Lease renewal: at every checkpoint, the worker calls
  `<supervisor>/renew-lease <issue-id>`. The supervisor inspects the
  checkpoint's validation block:
    - lint/typecheck both pass OR not-run AND completed_steps grew since
      last checkpoint  → grant lease renewal (extend by
      lease_renewal_interval_seconds, up to max_wall_seconds cap)
    - lint/typecheck fail OR completed_steps did not grow → deny renewal,
      route to repair-lane with reason="completion-bound-no-progress" OR
      "completion-bound-validation-regressed"
  The worker treats lease-denied as a graceful-exit signal: write a final
  checkpoint with state="paused", commit the work-in-progress to the
  worktree branch, and exit 0. The supervisor then handles the diff via
  repair-lane or strong-owner re-assignment.

- Coordinator (supervisor) responsibilities:
    - Every 60 seconds, scan plans/worker-state/*.heartbeat.json for
      stale heartbeats (mtime > 2 * heartbeat_interval_seconds).
    - Every 60 seconds, check active workers against max_wall_seconds.
    - On lease-renewal call from a worker, apply the validation criteria
      above and write the renewal decision to a sidecar file the worker
      reads on its next checkpoint.

- Worker self-judgment of "done" is NOT trusted. The worker may write
  next_action="done" in its checkpoint. The supervisor responds by:
    - Running the brief's acceptance commands (from owned_paths +
      forbidden_paths + acceptance rules in §4 plan schema)
    - If acceptance fails, sending the worker a "continue with diagnosis"
      signal (similar to lease-denial) — the worker is given one
      additional lease to fix.
    - If acceptance passes, marking the item done and proceeding to
      integration (per-child commit or single-pr integration per §4).
```

Failure routing:

```text
trigger                             → outcome
─────────────────────────────────────────────────────────────────
stdout silence 120s (scoped)         → kill + diff capture + repair-lane
max_wall_seconds exceeded (any)      → kill + diff capture + repair-lane
checkpoint missed (completion-bound) → soft probe, then escalate if 2nd
                                        consecutive miss → kill + diff + repair
lease-denied at checkpoint           → worker exits gracefully → repair-lane
                                        or strong-owner re-queue based on
                                        completed_steps progress
worker self-reports done             → supervisor runs acceptance; either
                                        marks done OR grants one more lease
                                        for diagnosis
```

---

## 6. Hard stops (LOCKED — automation never crosses)

```text
- Any phase smoke test failure                → halt phase advancement
- Phase 1 backfill classification by codex    → never; human-only (see §10 Q3)
- LAUNCH_CONFIRMED modification               → never by automation
- STRIPE_LIVE_CUTOVER_APPROVED modification   → never by automation
- STRIPE_WEBHOOK_SECRET / STRIPE_PRICE_ID     → never by automation
- Banned term grep match                      → reject entire diff
  (humanizer | bypass | undetect | detector | evade, scope: app/ components/ public/ lib/)
- DeepSeek+Sapling spend > NZ$20 per session  → halt; record in budget log
- Planner attempts > 3 per epic               → mark failed-planning, escalate
- Real Stripe charge from automation          → never (user-only, M7-001)
- npm publish from automation                 → never (user-only, M9-006)
- Cloudflare custom domain attach automation  → never (user dashboard action)
- Secret values printed/logged/committed      → never (from .env.local /
                                                 .dev.vars / globalapikey/)
```

---

## 7. Dispatcher filter expression (LOCKED)

The replacement for `find_next_pending_issue`:

```text
SELECT lowest-priority item WHERE:
  registry_entry exists
  AND lane in {direct, epic, evidence, repair}
  AND status == pending
  AND lane-specific gate passes:
    direct:   owner_class==loop
              AND coupling in {low, medium}
              AND brief_state == detailed
              AND requires_invariants ⊆ provided_invariants(done items)
    epic:     owner_class==strong-model
              AND planner_attempts < 3
    evidence: verifier_auto OR (verifier_manual AND not_yet_in_checklist)
    repair:   root_cause_hash NOT in circuit_open_set
```

Priority order: epic > evidence > repair > direct
Rationale: epic planning unblocks future direct work; evidence reduces operator latency; repair recovers loop health; direct is the routine path.

Within each lane, sort by: lowest M-number, then lowest id, then oldest `added_at`.

---

## 8. Smoke test definitions per phase (LOCKED)

Each phase advances only if its smoke test passes. Smoke tests are pinned to specific real items so a green smoke is end-to-end evidence, not a unit test.

```text
Phase 1 smoke:
  Dispatcher runs one iteration with registry populated, prints
  "selected lane: <X>, item: <id>" and exits without executing.
  Verify: lane selected matches manual expectation for that item.

Phase 2 smoke:
  One direct-lane task executes end-to-end (Codex exec → checks pass
  → PR opened → merged). Verify: worker did not modify any path outside
  owned_paths (diff inspection).

Phase 3 smoke:
  One evidence-lane item with evidence_type=file-present transitions
  pending → awaiting-evidence → done after the operator creates the
  expected file. Verifier runs in the same loop iteration that follows
  file creation. Verify: human-checklist.md regenerates with the item
  removed.

Phase 4 smoke:
  Synthetic repair item with identical root-cause-hash submitted 3
  times within 24h. Verify: 3rd submission triggers circuit-open and
  4th submission is rejected without dispatching.

Phase 5 smoke:
  M3-002 (Reduce visible tone presets to 4) used as pilot epic.
  Planner produces plan.md + child-tasks.json. Validation passes.
  Children appear in direct-lane registry. Verify: validation rejects
  a deliberately broken plan (overlapping owned_paths) in a separate
  test invocation.

Phase 6 smoke:
  M1-Entra used as pilot single-pr epic. epic/M1-entra branch exists,
  all 10 children land on it, integration PR opened against main.
  Verify: a child PR that targets main directly (mis-configured) is
  rejected by dispatcher.

Phase 7 smoke:
  Two children of different epics run in parallel worktrees, neither
  observes the other's working tree. Verify: each worker's `git status`
  returns only its own changes.
```

---

## 9. Phase schedule and effort estimate

```text
Phase  When        Wall-clock estimate  Gate
─────  ──────────  ───────────────────  ────────────────────────────
0      this session  30 min             user signs off §10
1      next session  3 hours            P0 done + backfill complete
2      same as P1    1.5 hours          P1 smoke green
3      session +1    3-4 hours          P2 smoke green
                                        ↓
                                        LAUNCH NODE
                                        ↓
4      post-launch   1.5 hours          first paid txn complete
5      post-launch   6 hours            P4 smoke green
6      post-launch   3 hours            P5 smoke green on M3-002
7      defer         —                  P6 proven on ≥1 epic
─────  ──────────  ───────────────────  ────────────────────────────
Launch-pre total:  ~8.5 hours across 3 sessions
Launch-post total: ~10.5 hours across 3-4 sessions
```

---

## 10. Resolved questions

### RESOLUTION RECORD (2026-05-23)

```text
Q1 Phase 5 timing            → RECOMMENDED  (launch-pre = Phase 0-3 only)
Q2 State storage             → RECOMMENDED  (markdown + JSON sidecar hybrid)
Q3 Phase 1 backfill owner    → OVERRIDE     (user classifies; Claude does
                                            not draft. Phase 1 unblocks when
                                            user submits plans/issue-classification.md)
Q4 NEON_READONLY_URL         → RECOMMENDED  (provision Neon read-only role,
                                            add to .env.local)
```

The original questions are preserved below for context; the chosen path on each is annotated.



### Q1. Launch-pre / launch-post split  →  **CHOSEN: Recommended**

**Recommended:** Accept the split in §1.4. Phase 0-3 done before live revenue, Phase 4-7 done after.

**Alternative considered:** Build Phase 5 (epic-planner) first so M1/M3/M8 auto-progress in parallel with waiting for user-only blockers. Rejected because none of those issues are launch-blocking; building autonomy for post-launch work before launch is optimizing throughput of work that produces no revenue.

**Override path:** If you want Phase 5 in launch-pre, change §1.4 to move Phase 5 above the LAUNCH NODE and amend §9 (adds ~6 hours, pushes launch by 1-2 sessions).

### Q2. State storage strategy  →  **CHOSEN: Recommended**

**Recommended:** Markdown-as-direct-truth + JSON-sidecar hybrid per §1.3. New lanes use JSON, direct-lane keeps current markdown. v1 migration to JSON-only deferred.

**Alternative considered:** Full JSON-as-only-truth migration in Phase 1. Rejected because it requires rewriting every script that touches state (~3 sessions of pure plumbing) before any lane behavior change is observable.

**Override path:** If you want full migration, amend §1.3 and add a "Phase 1a — JSON migration" before Phase 1, ~2 sessions.

### Q3. Phase 1 backfill ownership  →  **CHOSEN: OVERRIDE — user classifies**

Phase 1 requires every existing non-done issue (~60 items) classified into the registry with `lane`, `owner_class`, `coupling`, `brief_state`. This is judgment work.

**User decision:** User classifies directly. Claude does not draft. Phase 1 code work is blocked until user submits `plans/issue-classification.md`. Claude's role is to provide a template (`plans/issue-classification.md` with all non-done rows pre-filled and empty classification cells) and a rubric for how to choose values.

**Recommended (NOT chosen):** Claude (this assistant) drafts the full classification in a single pass (30-45 minutes of read + classify), produces `plans/issue-classification-draft.md` as a reviewable table; user reviews and overrides specific rows; the approved version is the input to Phase 1 code.

**Alternative considered:** User classifies directly (better project knowledge, but slower wall-clock). Codex auto-classifies (rejected outright — Codex lacks project context and will mis-classify high-coupling epics as loop-eligible).

**Override path:** If you prefer to classify yourself, skip the draft step; Phase 1 then waits for your classification before proceeding.

### Q4. NEON_READONLY_URL provisioning  →  **CHOSEN: (a) Recommended**

Evidence-lane `db-row` verifier needs a read-only Neon connection string distinct from the dev connection. Two paths:

**(a) CHOSEN.** Provision a Neon read-only role + connection string, add `NEON_READONLY_URL` to `.env.local`. Loop verifier uses this directly. Action: user provisions in Neon console at Phase 3 start; until then, Phase 3 evidence-lane items with evidence_type=db-row remain `blocked-verifier` and surface in human checklist as "needs NEON_READONLY_URL".

**(b)** Build an internal admin HTTP endpoint (e.g., `/api/admin/evidence/db-row?...`) that runs the query server-side using the existing prod connection, returns boolean. Verifier calls the endpoint with admin auth.

**Recommended:** (a) — simpler, no new endpoint surface, no auth complications. Adds one secret to manage.

**Override path:** Pick (b) if you prefer no additional DB credentials, OR pick "defer" if evidence-lane should ship without db-row verifiers (manual-only for those gates in v0).

---

## 11. What this document deliberately does NOT decide

The following are explicitly out of scope for Phase 0 and will be decided when their phase begins:

- The exact bash/jq commands to read/write the JSON sidecars (Phase 1 implementation detail)
- The brief writer's prompt template for converting manifest-only → detailed (Phase 2)
- The exact verifier command for each evidence_type (Phase 3, per-type)
- The planner's full system prompt (Phase 5)
- The integration PR's e2e validation command set (Phase 6)
- Worktree directory layout for parallel workers (Phase 7)

If any of these decisions turns out to constrain Phase 0 choices, this document gets amended.

---

## 12. Amendment procedure

Any change to a LOCKED section requires:
1. Edit this file with the proposed change AND a `## Amendment <date>` section at the bottom describing what changed and why.
2. Update `plans/decisions-log.md` with a one-line entry referencing this file.
3. If the change invalidates a completed phase's smoke test, the affected phase's smoke must re-run before further work in that lane.

DRAFT sections (this whole file until §10 is resolved) may be edited freely without amendment overhead.

---

## 13. Worker classes (LOCKED — added per Amendment 2026-05-23-B)

### 13.1 scoped worker

```text
Runtime         Codex `exec` single-shot
Sandbox         workspace-write
Default for     direct-lane (low and medium coupling)
Wall ceiling    see §5.1' (low=30min, medium=2h)
Liveness        stdout-proxy heartbeat (120s silence = dead)
Checkpointing   none — single-shot semantics
Lease           not used; wall ceiling is the only hard limit
On failure      diff captured to plans/worker-aborted/<id>-<ts>.patch
                item routes to repair-lane
Diff scope      enforced by owned_paths + forbidden_paths in registry
Acceptance      brief's acceptance commands run by supervisor post-exit
```

When to use: any direct-lane item whose detailed brief fits in a single
Codex pass. The dispatcher selects the item, the supervisor runs Codex
with the brief, Codex produces a diff, Codex exits, supervisor runs
acceptance, item done OR repair.

### 13.2 completion-bound strong worker

```text
Runtime         Claude Code in a dedicated git worktree
Sandbox         worktree boundary (worker cannot escape its working tree)
Default for     epic-lane children (high-coupling work), any item that
                cannot reliably complete in one scoped pass
Wall ceiling    see §5.1' (medium=4h, high=8h)
Liveness        file-based heartbeat per §5.6 (heartbeat_interval=300s)
Checkpointing   every 20 min OR sub-step boundary, written to
                plans/worker-state/<id>.checkpoint.json per §5.6
Lease           renewable per §5.6; supervisor decides at every checkpoint
On failure      lease denied → graceful exit → repair-lane OR strong-owner
                re-queue based on progress
Diff scope      enforced by worktree boundary + owned_paths + acceptance
Acceptance      brief's acceptance commands run by supervisor at lease
                renewal and at worker-self-report-done
```

When to use: any epic-lane child after planner sharding; any item whose
brief is large enough that a 2h wall would force premature truncation;
any item that needs cross-step state visible to the supervisor while
in-flight.

### 13.3 coordinator-poll (not really a worker)

```text
Runtime         Dispatcher itself, in the supervisor process
Default for     evidence-lane
Behavior        Dispatcher calls the per-evidence_type verifier per §5.5
                each iteration. No worker invoked, no lease, no heartbeat.
                Item stays pending until verifier returns "evidence-present
                and matches expected shape" — then transitions to done.
Failure         verifier returns "evidence-present but wrong shape" →
                item moves to rejected per §2.3 evidence-lane state machine
```

### 13.4 Selection rule (which tuple to use per registry item)

The tuple is `(worker_class, worker_runtime, review_class)`. The escalation
level (§14) controls which tuple is chosen at each attempt.

Default at `current_level=1` (Codex long-lease, no review):

```text
lane       coupling   → (worker_class, worker_runtime, review_class)
─────────────────────────────────────────────────────────────────────
direct     low          (scoped, codex-cli, none)
direct     medium       (scoped, codex-cli, none)
direct     high         INVALID (rubric forbids direct+high)
epic       any          (completion-bound, codex-cli, none)
evidence   any          (coordinator-poll, none, none)
repair     any          inherit from the parent item that produced
                        the repair entry, but escalate one level
                        per §14.6:
                          current_level was 1 → re-attempt at 2
                          current_level was 2 → re-attempt at 3
                          current_level was 3 → re-attempt at 4
                          current_level was 4 → mark failed, escalate
                          to human (`plans/repair-escalations.md`)
```

At higher escalation levels the tuple is overridden per §14.3:

```text
current_level  →  override
─────────────────────────────────────────────────────────────────
1              no override; use lane+coupling defaults above
2              add: planner-exec pre-pass writes child plan;
                  validated per §5.2 7-check; children run at
                  current_level=1
3              override review_class to claude-checkpoint-review
                  (for epic-lane children that are in flight);
                  OR if the item has no plan yet, override
                  review_class to human-spec and require
                  plans/specs/<id>.md to exist before dispatch
4              override worker_runtime to claude-code-worktree;
                  worker_class stays completion-bound;
                  review_class returns to none (the worker IS
                  the reviewer at this tier)
```

The "escalate one tier" rule for repair-lane prevents repair loops from
running an identical configuration against an item that already failed
once. Repair-lane items are explicitly given a stronger configuration,
not the same one that failed.

### 13.5 Phase implementation mapping (revised per Amendment C)

```text
Phase 1    selector only — does not invoke workers; reads new fields
           (worker_runtime, review_class, current_level) without
           acting. Default behavior unchanged. (QUEUED — see
           plans/codex-worker-inbox.md.)

Phase 2    scoped worker, codex-cli runtime (L1 for tiny items).
           Implements:
           - codex exec invocation from supervisor shell
           - stdout-proxy heartbeat (120s silence = dead)
           - max_wall_seconds enforcement
           - diff capture on abort
           - acceptance post-exit
           - per-attempt write to plans/level-telemetry.jsonl
           - level_attempts[current_level]++ on every completed attempt
           - auto-escalation per §14.6 on 2 consecutive failures
           No completion-bound or review work yet.

Phase 4    repair-lane reads level_attempts; routes through §14.6
           auto-escalation. New failure categories from §5.6 feed
           §5.3 root-cause-hash and §14.6 escalation rule.

Phase 5    epic-planner (planner-exec runtime, 30 min ceiling).
           Produces child-tasks.json. Children inherit worker_class
           per §13.4 and current_level=1. Plan output passes the
           §5.2 7-check validator BEFORE child-tasks.json is written.
           Validator failure → planner_attempts++, retry up to 3.

Phase 6    single-pr integration ceremony. No new worker; supervisor
           runs integration tests via coordinator-poll-like check.

Phase 7a   completion-bound worker, codex-cli runtime (L1/L2 for
           medium+ epics). Implements:
           - codex exec with file-based heartbeat (codex writes
             plans/worker-state/<id>.heartbeat.json at sub-step
             boundaries via heredoc — codex CAN do this, just must
             be told to in the brief)
           - checkpoint at every 20 min OR sub-step boundary
           - lease renewal per §5.6
           - claude-checkpoint-review path for L3 (separate brief
             for the reviewer invocation)
           - level_attempts increment + telemetry write
           Required before any epic-lane planner shards run.

Phase 7b   parallel codex-cli workers (multiple worktrees in
           parallel). Deferred — only if Phase 7a telemetry shows
           queue depth justifies it.

Phase 7c   completion-bound worker, claude-code-worktree runtime
           (L4). Adds:
           - Claude Code launched in worktree via the platform's
             agent-mode invocation
           - file-based heartbeat written by Claude Code itself
           - same lease/checkpoint protocol as 7a
           Deferred — only if L3 telemetry shows persistent failure
           on a specific class of items that warrants the cost step.
```

### 13.6 What this section deliberately does NOT decide

- The exact Claude Code invocation incantation for a completion-bound
  worker in a worktree (Phase 7c implementation detail).
- The renew-lease IPC mechanism (file? socket? `<supervisor>/renew-lease`
  is a placeholder — Phase 7a will pick one).
- The diff-capture format for aborted scoped workers (Phase 2 picks).
- The escalation routing from repair-lane "L4" failures to human review
  (Phase 4 picks — likely a new file `plans/strong-owner-queue.md`).

---

## 14. Escalation ladder (LOCKED — added per Amendment 2026-05-23-C)

### 14.1 Why this ladder exists

Claude strong-owner work is high-success but high-cost. Codex CLI work
is low-cost but variable-success. A flat policy ("always try Codex" or
"always use Claude for hard items") wastes either money or success rate.

The escalation ladder lets the dispatcher try the CHEAPEST viable
configuration first, observe whether it succeeded, and only escalate to
a more expensive configuration when telemetry shows the cheaper one
won't get there. The principle (verbatim from the user 2026-05-23):
"先用便宜的长租约 Codex 和严格 checkpoint,把 Claude 变成升级路径,
而不是默认执行者."

### 14.2 The six levels

```text
L0  defer / not north-star critical
    No worker. Item is preserved in registry but skipped by dispatcher.
    Used for items the operator has decided are out-of-scope for the
    current product run (post-launch, optional polish, etc.).

L1  Codex long-lease single owner
    (worker_class=scoped|completion-bound, worker_runtime=codex-cli,
     review_class=none)
    Codex CLI runs the item end-to-end with the §5.6 lease protocol.
    No planner pass beforehand. No reviewer. This is the DEFAULT
    starting level for every new registry item.
    Cost: 1× Codex invocation per attempt. Cheapest tier.

L2  Codex planner + Codex executor
    A planner-exec pass (§5.2) produces child-tasks.json. The 7-check
    validator runs. Children then dispatch at L1 each.
    Cost: 1 planner + N children at L1. Used when an item is large
    enough that one Codex slice cannot complete it.

L3  Claude plan review + Codex implementation
    Either: review_class=claude-checkpoint-review and Codex runs at L1
    with Claude reading each 20-min checkpoint and deciding
    continue/abort/escalate; OR review_class=human-spec and the worker
    only dispatches after plans/specs/<id>.md exists (written by Claude
    or the operator).
    Cost: 1× Codex + Claude review tokens per checkpoint. ~$2-3 per
    8h epic in Sonnet pricing.

L4  Claude strong-owner implementation
    (worker_class=completion-bound, worker_runtime=claude-code-worktree,
     review_class=none)
    Claude Code runs the item end-to-end inside a worktree. Highest
    cost, highest success rate for items that require cross-system
    reasoning or repeated correction.
    Cost: Claude tokens for the full work + heartbeats. ~$10-30 per
    epic depending on size.

L5  Human only / external artifact
    No worker. The item is escalated to the operator with a summary
    of why L1-L4 failed. The operator implements the item directly,
    OR provides an external artifact (e.g., a vendor's output, a
    legal document) that the supervisor records as the item's
    completion evidence.
```

### 14.3 Tuple override per level (cross-reference §13.4)

```text
level  worker_class           worker_runtime         review_class
─────────────────────────────────────────────────────────────────
L0     —                      none                    none
L1     scoped | completion-   codex-cli               none
       bound (from §13.4)
L2     completion-bound       codex-cli               none
       (children at L1)       (planner pre-pass)
L3a    completion-bound       codex-cli               claude-checkpoint-review
L3b    completion-bound       codex-cli               human-spec
L4     completion-bound       claude-code-worktree    none
L5     —                      none                    none
```

L3 has two variants:
- L3a (cheaper, default L3): Codex implements; Claude reads every
  checkpoint and votes continue/abort/escalate. Used when the operator
  trusts the brief but wants a model-in-the-loop sanity check during
  long runs.
- L3b (no-runtime-review): Claude wrote the spec at plans/specs/<id>.md
  before dispatch; Codex implements; no per-checkpoint review. Used
  when the operator prefers to invest in spec-writing upfront and let
  the executor run uninterrupted.

### 14.4 Initial level selection (when an item enters the registry)

```text
Item shape                                → min_level   current_level
──────────────────────────────────────────────────────────────────────
direct-lane, low coupling, brief detailed       1             1
direct-lane, low coupling, brief manifest       1             1   (Phase 2
                                                                   brief-writer
                                                                   produces detailed
                                                                   before dispatch)
direct-lane, medium coupling                    1             1
epic-lane, single-item epic                     1             1
epic-lane, ≥5 children, no spec yet             1             1   (planner runs
                                                                   at L2 implicitly
                                                                   on first dispatch
                                                                   per §14.6 rule
                                                                   below)
epic-lane explicitly marked architectural        3             3   (e.g., M1-Entra,
                                                                   M8-API if operator
                                                                   pre-classifies)
evidence-lane                                   coordinator-poll, no level
repair-lane                                     parent.current_level + 1
                                                (per §13.4 repair rule)
human-only                                      5             5
```

The default is min_level=1 / current_level=1 for everything that has a
worker. Operator may pre-set higher min_level on items where L1-L2 is
known to be wrong (e.g., a cross-cutting auth migration that needs a
human-written spec before any Codex attempt).

### 14.5 The 7-check validator drives implicit L1 → L2 transition

When an L1 attempt fails because the brief is too large for a single
Codex run (codex exits with "incomplete — needs planner"), the
supervisor does NOT just retry L1 with the same brief. Instead:
- level_attempts[1]++
- Mark the item's `auto_promoted_reason = "needs-planner"`
- Set current_level = 2
- Next dispatch invokes planner-exec on the item
- Planner output runs through the 7-check validator
- Children enter registry at current_level=1

This auto-promotion to L2 is the most common transition. It is the
mechanism by which an epic that was "registry as one item" becomes
"registry as parent + N children, parent stays at status=sharded."

### 14.6 Auto-escalation rule (LOCKED)

```text
After every attempt at current_level=N completes (success OR failure):
  level_attempts[N]++
  write one line to plans/level-telemetry.jsonl (schema in §14.7)

If failure AND level_attempts[N] >= 2:
  current_level = N + 1
  IF new current_level > 4:
    item status = "failed-all-levels"
    append to plans/strong-owner-queue.md for operator review
    (which is L5 in practice)

If success:
  item status = "done"
  current_level stays at N (the level that succeeded — useful
  retrospectively for telemetry analysis; future re-runs of this item
  in repair-lane will start at N + 1 per §13.4 repair rule)
```

Note: "2 consecutive failures" not "2 total failures." If an attempt
at L1 fails, then a different item at L1 succeeds, then this item is
retried at L1 and fails again — that's 2 consecutive failures at L1
for THIS item, escalate to L2. Per-item counter, not global.

### 14.7 Telemetry record schema

Each completed attempt appends one JSON line to
`plans/level-telemetry.jsonl`:

```json
{
  "attempt_ts": "2026-05-23T16:45:00+12:00",
  "issue_id": "M5-003",
  "epic_id": null,
  "level": 1,
  "worker_class": "scoped",
  "worker_runtime": "codex-cli",
  "review_class": "none",
  "wall_seconds": 1850,
  "outcome": "success | failure | escalated | aborted-wall",
  "failure_reason": null,
  "failure_category": null,
  "cost_usd_estimate": 0.40,
  "diff_lines_added": 142,
  "diff_lines_removed": 18,
  "files_touched": ["scripts/analyze-rewrite-quality.ts"],
  "acceptance_passed": true,
  "acceptance_failures": [],
  "level_attempts_after": {"1": 1, "2": 0, "3": 0, "4": 0}
}
```

`failure_category` is one of: scoped-worker-no-progress |
completion-bound-no-progress | wall-time-exceeded |
completion-bound-validation-regressed | acceptance-failed |
plan-validation-failed | banned-term-violation | unknown.

`cost_usd_estimate` is best-effort:
- codex-cli runtime: tokens read from codex's session metadata × the
  model's per-1k token rate (DeepSeek-Pro pricing currently)
- claude-code-worktree runtime: tokens from Claude Agent SDK session
  metadata × Sonnet/Opus rate
- Coordinator-poll: 0
- Unknown: omit the field rather than guess

The file is append-only. Never edited in place. Rotated by year if
size exceeds 10MB.

### 14.8 Operator hooks

The operator can override the ladder at any time by editing the
registry item directly:
- Force defer: set `current_level = 0` (item never picked again
  until reset)
- Skip levels: set `min_level = 4` (item starts at L4, bypasses L1-L3)
- Reset escalation: set `current_level = min_level`, clear
  `level_attempts`
- Block escalation: set `current_level = N`, status = "operator-locked"
  (dispatcher will not auto-escalate; only operator can change level)

These overrides are recorded as decisions in `plans/decisions-log.md`
with format `<ts> | <issue-id> | level-override | <reason>` so the
audit trail captures why the ladder was bypassed.

### 14.9 What this section deliberately does NOT decide

- The exact Claude API / Claude Agent SDK invocation for L3 checkpoint
  review (Phase 7a picks; likely Claude Code's `headless` mode reading
  the checkpoint JSON via stdin and writing a one-word verdict).
- The cost-rate table for `cost_usd_estimate` (Phase 2 picks; updated
  as model pricing changes).
- Pre-emptive escalation (e.g., "if last 5 attempts at L1 for this
  epic_id all failed, start the next sibling at L2"). Could improve
  efficiency but adds complexity; deferred until telemetry justifies.
- The trigger for promoting an L3a "claude-checkpoint-review"
  abort/escalate verdict into a full L4 re-dispatch (Phase 7a picks;
  candidate rule: 2 abort verdicts in one run → escalate next attempt
  to L4 immediately, do not re-try L3 once).

---

## Amendment 2026-05-23-C — Escalation ladder + axis split + hard plan validation

Authorized by: User ("Chuan") via direct architectural call on 2026-05-23T16:25+12:00, after seeing the §13 lease model and asking for a cheaper-first ladder. Verbatim user-stated principle: "先用便宜的长租约 Codex 和严格 checkpoint,把 Claude 变成升级路径,而不是默认执行者." Operational target: 24/7 self-driving toward north-star with the existing 30-minute supervisor cron as the runtime carrier.

Change scope (LOCKED sections affected):
1. §13 axis split: `worker_class` (lifecycle) was a single field that conflated lifecycle and implementation. Now split into:
   - `worker_class`: `scoped | completion-bound | coordinator-poll` (lifecycle, from Amendment B)
   - `worker_runtime`: `codex-cli | claude-code-worktree | none` (NEW — implementation)
   - `review_class`: `none | claude-checkpoint-review | human-spec` (NEW — who gates progress)
2. §5.2 planner identity: gains 7 hard validation checks the planner-produced child-tasks.json MUST pass before any child dispatches. Three rejections → epic escalates one level per §14.
3. NEW §14 Escalation ladder: L0 (defer) → L1 (Codex long-lease) → L2 (Codex planner + executor) → L3 (Claude plan review + Codex impl) → L4 (Claude strong-owner impl) → L5 (human-only). Per-item `min_level`, `current_level`, `level_attempts` fields. Auto-escalation: 2 consecutive failures at level N → current_level = N+1. Telemetry: every attempt writes one line to `plans/level-telemetry.jsonl`.

Schema migration:
- Registry items gain `worker_runtime`, `review_class`, `min_level`, `current_level`, `level_attempts`. Defaults computed from `worker_class + coupling` per §14.4.
- `plans/level-telemetry.jsonl` created empty with a schema doc at `plans/level-telemetry-schema.md`.
- `plans/codex-worker-inbox.md` Phase 1 item gains an auto-progression rule (after Phase 1 ships, supervisor enqueues Phase 2 brief writing).

Phase impact:
- Phase 1 (selector, queued): UNCHANGED. Selector reads the new fields but does not act on them.
- Phase 2 (scoped+codex-cli runtime): now includes telemetry writes per item attempt + level_attempts counter increment. Estimate stays 3h.
- Phase 4 (repair): now reads level_attempts and computes escalation; failure categories from §5.6 feed §14.6 escalation rule.
- Phase 5 (epic-planner): runs §5.2 7-check validator on its output BEFORE writing child-tasks.json. Validator failure = planner_attempts++.
- Phase 7a (completion-bound+codex-cli): becomes the L1/L2/L3 runtime carrier (codex-cli with lease/heartbeat/checkpoint per §5.6).
- Phase 7c (NEW): completion-bound+claude-code-worktree — the L4 runtime. Implemented only after L1-L3 telemetry shows L3 success rate is below an acceptable threshold for a specific class of items. Deferred until empirically needed.

24/7 operation timeline (as committed to user 2026-05-23T16:25+12:00):
- T+0 (this session): §13/§5.2/§14 docs land; registry updated.
- T+30 min: supervisor cron picks up Phase 1 inbox.
- T+1-2 h: Phase 1 dispatcher PR opened. User reviews + merges.
- T+4-6 h: Phase 2 brief auto-queued (per inbox auto-progression rule); supervisor implements scoped+codex-cli runtime; M5-003/M8-001/M9-002 begin moving.
- T+1 day: Phase 4 (repair) operational; auto-escalation triggers as failures accumulate.
- T+2-3 days: Phase 5 (epic-planner with 7-check validator) operational; M1/M3/M8 plans get produced.
- T+3-5 days: Phase 7a operational; epic children run under L1 codex-cli long-lease.
- T+1 week: closed loop. Dispatcher self-selects level per item, self-escalates on failure, self-records telemetry. North-star pursuit is autonomous.

§6 hard stops unchanged. §1.4 launch-pre/post split unchanged. §7 dispatcher filter unchanged for Phase 1.

Audit trail entry in `plans/decisions-log.md` accompanies this amendment with timestamp 2026-05-23T16:30+12:00.

---

## Amendment 2026-05-23-B — Worker model: renewable lease, not fixed timeout

Authorized by: User ("Chuan") via direct architectural call on 2026-05-23 after Phase 1 codex wedge incident. Verbatim user-stated decision: "保留 timeout,但从 fixed timeout 改成 renewable lease... worker 不再只写最终 task-status.json,而是持续写心跳和 checkpoint... worker 分两类:scoped worker 和 completion-bound strong worker."

Change scope (LOCKED sections affected):
- §3 schema gains 5 fields: `worker_class`, `initial_lease_seconds`, `lease_renewal_interval_seconds`, `heartbeat_interval_seconds`, `max_wall_seconds`. Existing `timeout_seconds` is RENAMED to `max_wall_seconds` (semantic shift: hard ceiling, not the only timeout — see §5.6).
- §5.1 timeout table replaced by §5.1' lease-and-wall table (this amendment).
- New §5.6 Heartbeat + checkpoint + lease protocol added.
- New §13 Worker classes (scoped vs completion-bound) added.
- §1.2 lane definitions unchanged. §1.4 phase split unchanged.
- §6 hard stops unchanged.
- §7 dispatcher filter unchanged for Phase 1 (selection logic doesn't care about lease — it only picks the next item). Phase 2 dispatcher (which actually invokes the worker) MUST honor the lease model.

Phase impact:
- Phase 1 (selector dry-run, currently queued in `plans/codex-worker-inbox.md`): UNAFFECTED. The selector only picks items; it does not invoke workers. Brief and smoke test stay as-is.
- Phase 2 (direct-lane hardening): Now must implement scoped-worker invocation with heartbeat polling + lease renewal + max-wall enforcement. Estimate revised from 1.5h → 3h.
- Phase 4 (repair circuit breaker): Now receives lease-expiry events as one of its inputs (failure category "lease-expired-no-progress"). No schema change beyond §5.3 hash function.
- Phase 5 (epic-planner v0): Children produced by the planner inherit `worker_class` from their parent's coupling. Single-pr epics with `worker_class=completion-bound` need worktree provisioning earlier than Phase 7. May force a partial Phase 7 (worktree only, no parallelism) into the launch-pre lane.
- Phase 7 (multi-worktree): Becomes the path for `completion-bound` strong workers (Claude Code in worktree with heartbeat file writes). Original Phase 7 was "only if proven need" — now it's "required for any large epic" but the parallelism requirement stays deferred.

Schema migration (in-place — registry generator handles both shapes):
- Old: `"timeout_seconds": 900` → New: `"max_wall_seconds": 900, "worker_class": "scoped"` (default: scoped for direct-lane, completion-bound for epic-lane, coordinator-poll for evidence-lane).
- Old: no lease fields → New: lease defaults inherited from §5.1' per worker_class. Per-item override allowed but not required.

Heartbeat-vs-Codex-reality caveat: Codex CLI does not natively support mid-run heartbeat-file writes. For `scoped` workers running under Codex, the heartbeat signal is a stdout-progress proxy (supervisor watches Codex's stdout for any line within N seconds and treats silence longer than that as "no heartbeat"). For `completion-bound` workers running under Claude Code in a worktree, heartbeat is a true file-write per §5.6 protocol. This asymmetry is intentional — the only worker class that REQUIRES file-based heartbeat is the one that runs long enough for stdout-proxy to be unreliable.

Audit trail entry in `plans/decisions-log.md` accompanies this amendment with timestamp 2026-05-23T16:05+12:00.

---

## Amendment 2026-05-23-A — Q3 downgraded from OVERRIDE to CONDITIONAL OVERRIDE

Authorized by: User ("Chuan") via direct instruction on 2026-05-23 after Phase 0 sign-off, verbatim "你来完成Phase1" (English: "you complete Phase 1").

Change:
- §10 Q3 status: **OVERRIDE** (user classifies; Claude does not draft) → **CONDITIONAL OVERRIDE** (Claude drafts the full classification; user reviews and may revise any row before Phase 1 code work — i.e., before the loop-registry.json or dispatcher changes are actually run against the live overnight loop).

Rationale (user-stated): user opted into the original recommended path (see §10 Q3 "Recommended (NOT chosen)") after seeing the per-row heuristic shortcuts in `plans/issue-classification.md`. Wall-clock cost of user-classifying-by-hand outweighed the benefit relative to "Claude drafts, user spot-checks risky rows."

Scope of this amendment:
1. Claude may fill all 60 rows of `plans/issue-classification.md` using the heuristic shortcuts in that file (M1→M1-Entra epic, M2→M2-Quality epic, M2.5-002→evidence file-present, M3→M3-V2 epic, M4-001→evidence file-present, M4-011/M4-015→M4-FrontendPolish epic, M5-003→direct medium, M6-*→evidence with verifier per §5.5, M7-*→evidence, M8-001→direct medium (in_progress), M8-002..M8-016→M8-API epic, M9-002→direct medium, M9-006→evidence manual-only).
2. Claude may generate `plans/loop-registry.json` and `plans/codex-briefs/phase1-dispatcher.md`.
3. Claude may NOT have Codex execute the dispatcher change against the live loop without an explicit user "go" instruction. Phase 1 smoke test must run first against an idle dispatcher (dry-run mode) and the user must inspect the output.
4. Q3's "user reviews and overrides specific rows" expectation is preserved: any row Claude classifies as `direct+high` (which the rubric calls out as risky), or any cell whose Notes column flags "borderline" or "could arguably be …", is a candidate for user revision before the registry is treated as authoritative.

Smoke test impact: §8 Phase 1 smoke test definition unchanged. The smoke must still print "selected lane: X, item: Y" and exit without executing — that gate is what protects against any mis-classification in the Claude-drafted registry.

Not changed by this amendment: §1–§9, §11 remain as locked. §6 hard stops remain in force (specifically the "Phase 1 backfill classification by codex" hard stop still applies — Claude is not Codex; Claude is the supervisor draft layer per CLAUDE.md, and the user explicitly re-authorized this layer for this task).

Audit trail entry in `plans/decisions-log.md` accompanies this amendment with timestamp.
