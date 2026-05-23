# Repair REPAIR-20260523160844

Title: Phase 1 lane-dispatcher implementation (LANE_DISPATCH=1 opt-in)
Source: plans/codex-worker-inbox.md

## Repair item

## 2026-05-23T15:55:00+12:00 — Phase 1 lane-dispatcher implementation (LANE_DISPATCH=1 opt-in)

- Status: pending
- Source: Claude supervisor (via Cowork session 2026-05-23 Phase 1)
- Class: autonomy
- Priority: P1
- Related issue: none — this is dispatcher infra per `plans/lane-architecture-decisions.md`
- Evidence: `plans/codex-briefs/phase1-dispatcher.md` (the full brief), `plans/loop-registry.json` (the registry the dispatcher reads), `plans/phase1-smoke-test.md` (6-step acceptance), `plans/lane-architecture-decisions.md` §1.1/§3/§6/§7/§8
- Suggested Codex action: Implement `plans/codex-briefs/phase1-dispatcher.md` exactly. Touch only `plans/overnight-supervisor.sh` and a new `tests/supervisor/test-lane-dispatch.sh`. Add `select_next_item_by_lane()` as a pure selector that reads `plans/loop-registry.json` via jq and prints exactly one line. Wire it into the main loop ONLY behind `if [ "${LANE_DISPATCH:-0}" = "1" ]; then ... fi` so default behavior with the env var unset is byte-identical to current. Add `SUPERVISOR_SOURCING_ONLY=1` guard near script top so the test can source it without starting the loop. Add `--selector-dry-run` mode for `LANE_DISPATCH=1 plans/overnight-supervisor.sh --selector-dry-run`. Run the 6 acceptance checks from the brief locally; only open PR when all 6 PASS. PR title: "Phase 1: opt-in lane dispatcher (LANE_DISPATCH=1)". Do NOT merge — leave for human review.
- Done condition: PR opened on branch `chore/phase1-lane-dispatcher`, body contains a "Smoke test results" section with 6 PASS lines (bash -n, unit test, iteration-1 prints "selected lane: epic, item: M1-002" on real registry, LANE_DISPATCH=0 byte-equivalence vs pre-change, banned-term scan empty, diff scope = exactly the two paths). `git diff --name-only` against main shows only `plans/overnight-supervisor.sh` and `tests/supervisor/test-lane-dispatch.sh`.
- Forbidden actions: real money, npm publish, dashboard changes, secret changes, `gh pr merge` (do not auto-merge), modification of any `.ts`/`.tsx`/`.cs`/`.py`/`.prisma` file, modification of `app/**`/`components/**`/`lib/**`/`public/**`, modification of `.env*`/`globalapikey/**`, modification of CI workflow files, modification of `plans/loop-registry.json` (it is the read-only input).
- Context for retry pattern: 5 prior codex MCP dispatches from this Cowork session timed out at MCP layer with zero filesystem progress (see `plans/decisions-log.md` 2026-05-23T15:48 entry). Health-check codex calls returned cleanly in <2s, so daemon transport is alive. Pattern matches decisions-log line 23 wedge that recovered on next trigger ~15-20 min later. The shell supervisor processing this inbox item should attempt fresh-thread codex dispatch and use sed/heredoc-style atomic shell briefs if the first call stalls. See decisions-log entries 2026-05-21T13:18 → 2026-05-21T13:36 for the recovery pattern.
- Lease-model addendum (Amendment 2026-05-23-B, 2026-05-23T16:05): registry items now carry `worker_class`, `initial_lease_seconds`, `lease_renewal_interval_seconds`, `heartbeat_interval_seconds`, `max_wall_seconds`. Phase 1 dispatcher is selector-only and DOES NOT implement lease/heartbeat/wall — those land in Phase 2 (scoped) and Phase 7a (completion-bound). The selector must NOT reference the old `timeout_seconds` field name; it was renamed. See the brief's Addendum section for the full constraint.
- Escalation-ladder addendum (Amendment 2026-05-23-C, 2026-05-23T16:30): registry items also now carry `worker_runtime`, `review_class`, `min_level`, `current_level`, `level_attempts`. Phase 1 dispatcher still selector-only — it reads these fields but does NOT apply escalation logic in this PR. See plans/lane-architecture-decisions.md §14 for full ladder; see plans/level-telemetry-schema.md for the telemetry contract.
- AUTO-PROGRESSION RULE (added per user instruction 2026-05-23T16:30): After Phase 1 PR is merged, the supervisor (on its next 30-min cron tick that observes the merge) MUST automatically append a Phase 2 work item to this inbox at the top of the Pending section. The Phase 2 work item is itself a Codex task to: (1) write `plans/codex-briefs/phase2-scoped-codex-cli.md` based on §13.1 / §5.6 scoped-section / §14 ladder for L1-scoped / §14.7 telemetry write; (2) implement the brief; (3) open PR titled "Phase 2: scoped+codex-cli runtime + telemetry"; (4) leave PR unmerged for operator review. Done condition: PR opened against main and `plans/codex-worker-inbox.md` has a new "Phase 2 implementation" Pending row. Do NOT block Phase 1 done-marking on Phase 2; Phase 2 is a SEPARATE inbox item that the supervisor enqueues, not a prerequisite for Phase 1 to be considered done. The supervisor that enqueues Phase 2 should also append a one-line decisions-log entry: `<ts> | phase-2 | auto-queued | Phase 1 PR <url> merged; Phase 2 inbox item appended.`



- Status: done
- Source: shell supervisor
- Class: provider
- Priority: P2
- Related issue: M6-001
- Evidence: plans/worker-secret-diff.md
- Suggested Codex action: Retry or narrow the read-only Worker secret-name diff path without printing secret values, pushing secrets, deploying, or changing provider dashboards.
- Done condition: `plans/worker-secret-diff.md` contains a completed name-only diff, or the inbox item records a provider/DNS failure with current evidence and no user-only action hidden inside it.
- Forbidden actions: live money, npm publish, dashboard changes, secret changes, deploys, printing `.env.local` values
- Result: Retried the read-only `wrangler secret list --name replyinmyvoice-app --format json` path with Wrangler logs redirected to writable temp storage. Wrangler still failed before returning Worker metadata because `api.cloudflare.com` / `dash.cloudflare.com` DNS resolution is unavailable from this shell. Direct DNS lookup returned `ENOTFOUND` for both hosts. No secret values were printed or written, no secrets were pushed, no deploy ran, no dashboard state changed, and `.env.local` was not modified. Current evidence is recorded in `plans/worker-secret-diff.md`.
- Worker evidence: 2026-05-22T18:06:47+12:00 — merged https://github.com/ChuanQiao1128/replyinmyvoice/pull/195; Retried read-only Wrangler secret listing, recorded current Cloudflare DNS blocker evidence, and reclassified M6-002 as prerequisite-blocked.

## Repository conventions

- This is a non-user blocker repair, not a product-scope expansion.
- Keep the fix scoped to the inbox item.
- Do not use git or gh. The shell supervisor owns branch, commit, push, PR, CI, and merge.
- Do not modify .env.local, .dev.vars, provider dashboards, Stripe live money, npm publish state, or secrets.
- Write plans/task-status.json using the normal Codex implementation schema.
