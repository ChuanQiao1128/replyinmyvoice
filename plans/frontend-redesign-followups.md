# Frontend Redesign Followups

Date: 2026-05-22
Source issue: M4-011
Evidence: `plans/codex-exec-M4-011.log`

## Root Cause

M4-011 asked one Codex invocation to redesign landing, pricing, auth, shared navigation, footer, and the `/app` workspace while also using design skills, writing a design brief, running validations, and checking browser layouts. The supervisor timebox is 600 seconds. The run started implementation and was killed around the timeout before `plans/task-status.json` could be written.

## Context

The shell supervisor owns issue selection, branch work, validation, and PR handling. Codex is expected to make scoped edits and always write `plans/task-status.json`. M4-011 broke that contract because the task was larger than one unattended invocation.

## Goals

- Keep M4-011 as an umbrella frontend redesign goal.
- Prevent the supervisor from retrying it as one broad task.
- Split redesign work into smaller follow-ups that fit one supervisor timebox.
- Preserve required design and browser-verification skills for each browser-visible follow-up.

## Non-Goals

- Do not perform the frontend redesign in this repair.
- Do not change auth, billing, rewrite, quota, API, telemetry, webhook, provider, or secret behavior.
- Do not change live infrastructure, Stripe, Cloudflare dashboards, or npm publication state.

## Current System

- `plans/overnight-supervisor.sh` runs one Codex task with `CODEX_TIMEOUT_SECONDS=600`.
- `plans/codex-implementation-prompt.md` tells Codex to write `plans/task-status.json`.
- `plans/issues/M4-011.md` described a multi-route redesign and verification pass.
- `plans/codex-exec-M4-011.log` ends during implementation output, before status creation.

## Proposed Architecture

Treat M4-011 as a blocked umbrella issue and run only smaller issue-board entries for the actual redesign. The implementation prompt now requires a timebox preflight before edits. The supervisor also has an explicit M4-011 guard so a future accidental `pending` state is reclassified instead of relaunched.

## Data Model

No database or application data model changes are required. The operational state lives in repository files:

- `plans/issue-board.md`
- `plans/issues/M4-011.md`
- `plans/frontend-redesign-followups.md`
- `plans/task-status.json`

## API and Job Contracts

- Codex must write `plans/task-status.json` for every implementation invocation.
- If a task is too broad for the timebox, Codex must write a `needs_human` status before source edits.
- The supervisor must not relaunch M4-011 as one broad unattended job.
- Smaller frontend follow-ups keep the same lint, typecheck, test, and browser-verification expectations.

## State Model

- `pending`: issue is safe for the shell supervisor to run.
- `in_progress`: supervisor has selected the issue and launched Codex.
- `BLOCKED-AUTONOMY`: issue is engineering-actionable but too broad or coupled for the unattended loop.
- `ready_to_commit`: Codex finished scoped work, validations, and status.
- `needs_human`: Codex found the issue cannot safely finish inside the current contract.

## Events

- `select_pending_issue`: supervisor chooses the next pending issue.
- `codex_timeout`: the Codex process exits without `plans/task-status.json`.
- `scope_reclassified`: repair confirms the task must be split before retry.
- `scoped_followup_created`: a smaller task is written and can be queued.

## Transition Table

| From | Event | To | Side effect |
| --- | --- | --- | --- |
| `pending` | `select_pending_issue` | `in_progress` | Supervisor copies the task brief and starts Codex. |
| `in_progress` | `codex_timeout` | `BLOCKED-AUTONOMY` | Supervisor queues a repair item with the Codex log path. |
| `BLOCKED-AUTONOMY` | `scope_reclassified` | `BLOCKED-AUTONOMY` | Keep M4-011 blocked as a broad umbrella. |
| `BLOCKED-AUTONOMY` | `scoped_followup_created` | `pending` follow-up | Run only the smaller follow-up issue, not the umbrella task. |

## Invariants

- The supervisor should not relaunch M4-011 as one broad unattended issue.
- A Codex implementation run must write `plans/task-status.json` before exiting or before declaring work incomplete.
- Follow-up issues should each fit one Codex timebox including tests and status output.
- Browser-visible follow-ups must keep the `web-design-engineer` and `ui-browser-testing` requirements.

## Illegal Transitions

- `BLOCKED-AUTONOMY` M4-011 directly back to `pending` without a scoped issue split.
- `in_progress` to branch cleanup without recording either `plans/task-status.json` or a repair inbox item.
- `ready_to_commit` without lint, typecheck, tests, and a clean scoped banned-term scan.

## Persistence Implications

The issue-board status is the persisted routing control. M4-011 remains `BLOCKED-AUTONOMY` until a human or follow-up planning task creates smaller issue-board entries. The follow-up document is safe to commit because it contains no secrets or private user content.

## Security and Privacy

This repair does not read or write `.env.local`, `.dev.vars`, provider credentials, dashboard state, or customer message content. Follow-up redesign tasks must continue to preserve legal/product copy accuracy and avoid fabricated claims.

## Rollout Plan

1. Commit this repair so the supervisor prompt and M4-011 guard are in place.
2. Add or queue the scoped follow-up issues below.
3. Run each follow-up independently through the normal supervisor loop.
4. Run the final critique/browser pass after the smaller implementation PRs merge.

## Scoped Follow-up Issues

1. `M4-011a`: Landing page, shared header, and footer visual refresh only. Produce/update `plans/frontend-redesign-design-brief.md`, keep product copy accurate, and verify `/` desktop/mobile.
2. `M4-011b`: Pricing and auth page visual alignment only. Reuse tokens from `M4-011a`, preserve auth and billing behavior, and verify `/pricing`, `/sign-in`, and `/sign-up` if visible.
3. `M4-011c`: `/app` workspace shell polish only. Preserve rewrite, quota, billing, API, telemetry, and webhook behavior. Verify signed-out behavior plus any local preview path available.
4. `M4-011d`: Final critique and responsive browser pass. Record five-dimension scores, fix highest-impact layout defects, and document any blocked browser checks.

## Verification Plan

- `npm run lint`
- `npm run typecheck`
- `npm run test`
- Focused browser or Playwright checks for only the routes touched by each follow-up.
- Confirm each follow-up writes `plans/task-status.json` before the supervisor timeout.

## Test Checklist

- Supervisor test covers M4-011 blocking before task handoff.
- Prompt test covers timebox preflight and early `needs_human` status.
- Shell syntax check covers the new case branch.
- Full lint, typecheck, and Vitest suite must pass before `ready_to_commit`.

## Open Questions

- Whether the shell issue creator should add M4-011a through M4-011d as formal GitHub issues, or whether the owner wants to replace M4-011 with a single supervised daytime redesign pass.
