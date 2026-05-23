# Repair REPAIR-20260523134035

Title: supervisor-skip-relax: release 5 scoped rows + remove daytime-only rationale
Source: plans/codex-worker-inbox.md

## Repair item

## 2026-05-23T13:42:00+12:00 — supervisor-skip-relax: release 5 scoped rows + remove daytime-only rationale

- Status: pending
- Source: Cowork supervisor (Claude)
- Class: autonomy
- Priority: P1
- Related issue: M1-007 (#85), M1-009 (#87), M3-001 (#???), M3-002 (#???), M3-005 (#???); supervisor policy
- Evidence: User directive on 2026-05-23 set continuous 24/7 operation as the policy — no time-of-day-based skip rationales remain valid. Audit identified 5 currently `BLOCKED-AUTONOMY` rows that are scope-isolated, additive, and safe to run unattended: M1-007 (add `entraUserId String? @unique` to Prisma User + migration), M1-009 (new `tests/unit/entra-auth.test.ts` against mock JWKS, no live Entra calls), M3-001 (additive scenarios in `lib/rewrite-presets.ts`), M3-002 (reduce tone presets to 4 in `lib/rewrite-presets.ts` with backward-compat mapping retained), M3-005 (zod cap in `lib/validation.ts`). Skip heuristics currently at `plans/overnight-supervisor.sh:1175-1186` (M1-Entra cluster, stated reason "daytime only / supervised implementation") and `:1194-1206` (M3 V2 cascade, stated reason "typed refactor across lib"). Remaining M1 rows (M1-002/003/004/005/006/008/010) still couple to the live auth path and stay BLOCKED-AUTONOMY for coupling-risk reasons, NOT time-of-day. Remaining M3 rows (M3-003/004/006/007/008) stay blocked because they require M3-001/002/005 to land first, or are the actual cascade refactor (M3-004), or depend on M3-004 (M3-006/007/008).
- Suggested Codex action: Make three coordinated edits in a single PR.
  (1) `plans/overnight-supervisor.sh:1178` — change the M1-Entra case branch from `M1-002|M1-003|M1-004|M1-005|M1-006|M1-007|M1-008|M1-009|M1-010)` to `M1-002|M1-003|M1-004|M1-005|M1-006|M1-008|M1-010)`. Replace the `log "  Skipping $ID (Entra auth migration cluster — daytime only)"` line with `log "  Skipping $ID (Entra auth cluster — couples to live auth path; release individually after per-issue brief)"`. Replace the `append_decision` text from `"...deferred to supervised implementation, not a user blocker"` to `"...auth coupling risk; release individually"`.
  (2) `plans/overnight-supervisor.sh:1196` — change the M3 case branch from `M3-001|M3-002|M3-003|M3-004|M3-005|M3-006|M3-007|M3-008)` to `M3-003|M3-004|M3-006|M3-007|M3-008)`. Replace the `log` text with `log "  Skipping $ID (V2 layout cascade — depends on M3-001/002/005 + M3-004 component rewrite)"`. Update the `append_decision` text accordingly.
  (3) `plans/issue-board.md` — flip the 5 rows (`M1-007`, `M1-009`, `M3-001`, `M3-002`, `M3-005`) from `BLOCKED-AUTONOMY` to `pending` in the rightmost state column. Use `git status` between edits so the loop's own concurrent writes don't conflict.
  (4) `plans/issues/` — create per-issue briefs for the 5 released IDs based on the source-of-truth descriptions in `plans/issue-manifest.md` (M1 section lines ~17-60, M3 section lines ~135-173). Each brief should be 10-30 lines, scoped to the single file(s) the issue touches, with banned-term reminder.
  (5) `plans/decisions-log.md` — append one line: `<ISO date> | supervisor-skip-relax | Released M1-007, M1-009, M3-001, M3-002, M3-005 to pending; removed daytime-only rationale from supervisor.sh M1 and M3 case branches; remaining M1/M3 BLOCKED-AUTONOMY rows kept with coupling-risk / cascade-prereq rationale per user 24/7 operation policy.`
- Done condition: PR merged on `main`. The 5 named rows show `pending` on `plans/issue-board.md`. `plans/overnight-supervisor.sh:1178` and `:1196` no longer reference the 5 released IDs in their case patterns and no longer contain the substring "daytime". Per-issue briefs exist at `plans/issues/M1-007.md`, `plans/issues/M1-009.md`, `plans/issues/M3-001.md`, `plans/issues/M3-002.md`, `plans/issues/M3-005.md`. `npm run lint`, `npm run typecheck`, `bash -n plans/overnight-supervisor.sh` all pass. Configured banned-term scan stays clean across `app components public lib`.
- Forbidden actions: changing any other case branch in supervisor.sh; flipping any row other than the 5 named; touching M1-002/003/004/005/006/008/010 or M3-003/004/006/007/008 on the board; modifying `.env.local`, `.dev.vars`, `globalapikey/`, `LAUNCH_CONFIRMED`, `STRIPE_LIVE_CUTOVER_APPROVED`, `STRIPE_WEBHOOK_SECRET`, `STRIPE_PRICE_ID`; force-push `main`; npm publish; live money; DNS / Cloudflare dashboard edits.

## Repository conventions

- This is a non-user blocker repair, not a product-scope expansion.
- Keep the fix scoped to the inbox item.
- Do not use git or gh. The shell supervisor owns branch, commit, push, PR, CI, and merge.
- Do not modify .env.local, .dev.vars, provider dashboards, Stripe live money, npm publish state, or secrets.
- Write plans/task-status.json using the normal Codex implementation schema.
