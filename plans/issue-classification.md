# Issue Classification — Phase 1 Input

Status: **DRAFT-FILLED BY CLAUDE 2026-05-23T15:10+12:00 — AWAITING USER REVIEW**
Authored: 2026-05-23
Phase: Triggers Phase 1 start (per `plans/lane-architecture-decisions.md` §10 Q3 + Amendment 2026-05-23-A)
Owner: Chuan (final review); Claude (draft fill per Amendment 2026-05-23-A)

This file is the input contract for `plans/loop-registry.json` generation in Phase 1. Every non-done issue from `plans/issue-board.md` is pre-extracted below.

**State as of this version:** Claude has drafted all 60 rows using the heuristic shortcuts in §"Heuristic shortcuts" below, plus per-row judgment. Rows where Claude bundled a borderline item into an epic for v0 safety (instead of risking direct+high) are flagged in the Notes column with "borderline:" prefix — those are the most likely candidates for user revision.

**User review checklist (recommended):**
1. Skim the table for any row marked `direct+high` (rubric §"Coupling" forbids this combination). Claude has drafted zero such rows; if any appears, that is a Claude bug to fix.
2. Inspect rows flagged "borderline:" in Notes. Override Lane/Coupling per your judgment if you want the loop to try them as direct-medium instead of epic.
3. Confirm the cluster Epic tags (M1-Entra, M2-Quality, M2.5-LearningOps, M3-V2, M4-FrontendPolish, M8-API). Rename if you prefer different labels.
4. Spot-check the evidence-lane `Detail` values (evidence_type) against §5.5 of `plans/lane-architecture-decisions.md` — auto_verify matrix.
5. M8-001 status is `in_progress` (PR #173 open); registry must reflect this and not re-dispatch. Confirm.

When happy, tell Claude "registry-go" and Claude proceeds to Phase 1 implementation tasks (registry JSON + dispatcher Codex brief + smoke test). Until "registry-go" is given, no dispatcher code change runs and the live overnight loop is not affected.

---

## Re-generate the row list

If the board changes before you finish filling, regenerate the row list with:

```bash
cd /Users/qc/Desktop/CloudFlare && awk -F'|' '
  /^\| M[0-9.]+-[0-9]+ \| / {
    gsub(/^[[:space:]]+|[[:space:]]+$/, "", $2)
    gsub(/^[[:space:]]+|[[:space:]]+$/, "", $4)
    gsub(/^[[:space:]]+|[[:space:]]+$/, "", $6)
    if ($6 ~ /^done/) next
    if ($6 == "") next
    title = $4; sub(/^M[0-9.]+-[0-9]+[: ]*/, "", title)
    if (length(title) > 58) title = substr(title, 1, 55) "..."
    status = $6
    if (length(status) > 24) status = substr(status, 1, 21) "..."
    printf "| %s | %s | %s |  |  |  |  |  |\n", $2, title, status
  }
' plans/issue-board.md
```

Diff this output against the table below; reconcile manually.

---

## Rubric — how to choose each column value

### `Lane` (column 4)

```text
direct    — task is one logical change, scoped to a known set of files,
            has (or can have) a detailed brief, can plausibly complete in
            ≤900s of Codex work. Example shape: M9-004 README, M5-002
            telemetry capture, M4-006 footer.

epic      — task is logically one thing but spans 3+ files OR has
            cross-cutting concerns (auth, schema, request path).
            Cannot be completed by one Codex slice without planner
            decomposition. Example shape: M1-002 (Entra middleware),
            M3-004 (V2 layout rewrite), M8-009 (B2B state machine).

evidence  — task is gated on external action (operator click, third-party
            event, DNS propagation, real money transaction). Loop cannot
            execute the action but can verify the artifact when it appears.
            Example shape: M6-008 Stripe webhook, M7-001 first paid txn,
            M9-006 npm publish, M6-004 DNS attach.

repair    — DO NOT manually classify any row as repair. Repair items are
            created dynamically by the loop when other items fail.
```

### `Owner Class` (column 5)

```text
loop          — Codex worker (current overnight loop). Use for direct-lane
                tasks only.

strong-model  — Stronger reasoning model (Claude or extended Codex).
                Use for epic-lane planning. Worker still does the per-child
                execution after planner shards.

human-only    — Requires human judgment or external action. Use for
                evidence-lane items and any task that is fundamentally
                not safely automatable.
```

### `Coupling` (column 6)

```text
low     — one file, additive change, no cross-cutting tests required.
          Default for direct-lane items with detailed brief.
          Worker timeout: 300s.

medium  — 2-4 files, modifies existing functions, requires existing tests
          to keep passing. Most non-trivial direct-lane items.
          Worker timeout: 900s.

high    — touches 5+ files OR changes a public interface OR migrates
          schema/auth/request flow. NEVER dispatched directly to worker.
          Always epic-lane (forces planner pass).
```

### `Detail` (column 7) — meaning depends on lane

```text
For direct-lane:
  detailed       — brief in plans/briefs/<id>.md is complete
                   (owned_paths, checks, acceptance defined)
  manifest-only  — only the issue-board one-liner exists;
                   brief writer needs to produce a detailed brief
                   before this item is dispatchable
  missing        — even the manifest entry is sparse; needs human
                   to clarify intent first

For epic-lane:
  manifest-only  — default; planner will produce detailed children
  (never use detailed/missing for epic-lane)

For evidence-lane: use one of these evidence_type values:
  stripe-event   — query Stripe API for an event matching criteria
  db-row         — query Neon DB (read-only) for a row matching criteria
  http-200       — curl a URL, expect 200 + content match
  dns-record     — `dig` returns expected record (may fall back to manual)
  file-present   — file exists at expected path
  sentry-api     — Sentry API returns event (blocked: needs token)
  posthog-api    — PostHog API returns event (blocked: needs key)
  manual-only    — verifier prints checklist line; operator confirms
                   completion manually
```

### `Epic` (column 8) — only fill for children of an epic

```text
Leave blank unless the item is logically a child of a parent epic that
will be sharded by planner. Example: M1-003..M1-010 would all have
Epic=M1-Entra; M1-002 itself is the epic-lane parent (Epic= blank).

For Phase 1 backfill, you can also just put M1-Entra as Epic for ALL
of M1-002..M1-010 and let the planner re-shape later; the registry
will treat the parent record as the dispatchable epic-lane item.
```

### `Notes` (column 9)

Free text. Especially useful for:
- "Wait for M8-001 to merge first" (dependency)
- "Already half-done in PR #X, just needs polish"
- "Drop this — not actually needed for launch or post-launch"

---

## Classification table

Fill columns 4-8 (Lane, Owner, Coupling, Detail, Epic, Notes). Column 3 (Status) is informational — registry status defaults to `pending` for everything except M8-001 (already `in_progress`).

| ID | Title | Status (ref) | Lane | Owner | Coupling | Detail | Epic | Notes |
|---|---|---|---|---|---|---|---|---|
| M1-002 | Replace clerkMiddleware in middleware.ts with Entra ses... | BLOCKED-AUTONOMY | epic | strong-model | high | manifest-only | M1-Entra | planner-representative for M1-Entra (lowest id) |
| M1-003 | Migrate API route auth from Clerk to Entra | BLOCKED-AUTONOMY | epic | strong-model | high | manifest-only | M1-Entra |  |
| M1-004 | Replace sign-in page with Entra MSAL redirect flow | BLOCKED-AUTONOMY | epic | strong-model | high | manifest-only | M1-Entra |  |
| M1-005 | Replace sign-up page with Entra flow | BLOCKED-AUTONOMY | epic | strong-model | high | manifest-only | M1-Entra |  |
| M1-006 | Implement /auth/callback for Entra code exchange | BLOCKED-AUTONOMY | epic | strong-model | high | manifest-only | M1-Entra |  |
| M1-007 | Add entra_user_id to User Prisma model + migration | BLOCKED-AUTONOMY | epic | strong-model | high | manifest-only | M1-Entra | borderline: schema-only could be direct-medium but blocks all of M1; safer as epic |
| M1-008 | Update Stripe webhook handler to use entra user lookup | BLOCKED-AUTONOMY | epic | strong-model | high | manifest-only | M1-Entra | depends_on M1-007 |
| M1-009 | Add Entra token validation tests | BLOCKED-AUTONOMY | epic | strong-model | high | manifest-only | M1-Entra | borderline: test-only could be direct-medium; safer as epic until cluster planned |
| M1-010 | Add Playwright e2e for Entra sign-in → /app → rewrite →... | BLOCKED-AUTONOMY | epic | strong-model | high | manifest-only | M1-Entra | last child; depends_on all other M1-Entra children |
| M2-001 | Implement hard quality gate rules in rewrite pipeline | BLOCKED-PREREQ | epic | strong-model | high | manifest-only | M2-Quality | planner-representative for M2-Quality (lowest id) |
| M2-002 | Safe-failure response when all candidates rejected | BLOCKED-PREREQ | epic | strong-model | high | manifest-only | M2-Quality |  |
| M2-003 | Targeted repair pipeline receives diagnosis tags + facts | BLOCKED-PREREQ | epic | strong-model | high | manifest-only | M2-Quality |  |
| M2-004 | Add Priya billing/proration regression case | BLOCKED-PREREQ | epic | strong-model | high | manifest-only | M2-Quality | borderline: single test-fixture file could be direct-medium; bundled for v0 |
| M2-005 | Build 25-case evaluation harness | BLOCKED-PREREQ | epic | strong-model | high | manifest-only | M2-Quality |  |
| M2-006 | Run evaluation; gate deploy | BLOCKED-PREREQ | epic | strong-model | high | manifest-only | M2-Quality | depends_on M2-005; DeepSeek budget consumed |
| M2-008 | Add quality-gate UI for safe-failure state | BLOCKED-PREREQ | epic | strong-model | high | manifest-only | M2-Quality |  |
| M2-009 | Update Naturalness Check display for repaired candidates | BLOCKED-PREREQ | epic | strong-model | high | manifest-only | M2-Quality |  |
| M2.5-002 | Run 100-case baseline; record results to docs/learning-... | BLOCKED-AUTONOMY | evidence | human-only | low | file-present |  | verifier checks docs/learning-baseline.md exists with ~100 sample rows; DeepSeek+Sapling budget ≤NZ$20/session §6 |
| M2.5-007 | Scheduled LearningOps run (Cloudflare Cron Trigger) | BLOCKED | epic | strong-model | high | manifest-only | M2.5-LearningOps | single-item epic; cron handler + worker config |
| M3-001 | Add 5 scenarios to lib/rewrite-presets.ts | BLOCKED-AUTONOMY | epic | strong-model | high | manifest-only | M3-V2 | planner-representative for M3-V2 (lowest id) |
| M3-002 | Reduce visible tone presets to 4 | BLOCKED-AUTONOMY | epic | strong-model | high | manifest-only | M3-V2 | borderline: UI-only could be direct-medium; bundled for v0 (also smoke target for Phase 5) |
| M3-003 | Add scenario-specific prompt guardrails | BLOCKED-AUTONOMY | epic | strong-model | high | manifest-only | M3-V2 |  |
| M3-004 | Rewrite components/app/rewrite-workspace.tsx (V2 layout) | BLOCKED-AUTONOMY | epic | strong-model | high | manifest-only | M3-V2 | V2 centerpiece; the reason M3 is an epic cluster |
| M3-005 | Enforce 5000-char combined cap in lib/validation.ts | BLOCKED-AUTONOMY | epic | strong-model | high | manifest-only | M3-V2 | borderline: single-file validation could be direct-low; bundled for v0 |
| M3-006 | Add character helper copy + counter | BLOCKED-AUTONOMY | epic | strong-model | high | manifest-only | M3-V2 | borderline: UI copy + counter could be direct-low; bundled for v0 |
| M3-007 | Add scenario to API request schema | BLOCKED-AUTONOMY | epic | strong-model | high | manifest-only | M3-V2 |  |
| M3-008 | Remove or hide legacy Quick context UI | BLOCKED-AUTONOMY | epic | strong-model | high | manifest-only | M3-V2 |  |
| M4-001 | Run rewrite engine against 4 documented sample cases | BLOCKED-PROVIDER | evidence | human-only | low | file-present |  | verifier checks docs/sample-cases.md or generated artifact JSON exists; DeepSeek budget |
| M4-011 | Use web-design-engineer to redesign frontend visual system | BLOCKED-AUTONOMY (ove... | epic | strong-model | high | manifest-only | M4-FrontendPolish | planner-representative; web-design-engineer skill orchestration |
| M4-015 | Final frontend critique and responsive browser pass | BLOCKED-AUTONOMY (bro... | epic | strong-model | high | manifest-only | M4-FrontendPolish | depends_on M4-011; browser-based — sandbox limited (manual review) |
| M5-003 | Offline Rewrite Quality Analysis report script | BLOCKED | direct | loop | medium | manifest-only |  | single new script under scripts/; touches cost-log model imports — needs detailed brief writer before dispatch |
| M6-001 | Diff Cloudflare Worker prod secrets vs .env.local live | BLOCKED-PROVIDER (Clo... | evidence | human-only | low | manual-only |  | Cloudflare API blocked in sandbox; operator runs wrangler secret list and commits diff |
| M6-002 | Push missing live secrets to Worker | BLOCKED-PREREQ (await... | evidence | human-only | low | manual-only |  | wrangler secret put is operator action; depends_on M6-001 |
| M6-003 | Smoke test workers.dev preview | BLOCKED-PROVIDER | evidence | human-only | low | manual-only |  | http-200 verifier blocked in sandbox §5.5; manual-only until host runner exists |
| M6-004 | Confirm replyinmyvoice.com → Worker custom domain attach | BLOCKED-PROVIDER | evidence | human-only | low | manual-only |  | dashboard action — never automated per §6 hard stops |
| M6-005 | Smoke test replyinmyvoice.com | BLOCKED-PROVIDER (san... | evidence | human-only | low | manual-only |  | http-200 to prod blocked in sandbox; manual-only until host runner exists |
| M6-007 | Full validation suite green | BLOCKED-AUTONOMY (san... | evidence | human-only | medium | manual-only |  | Playwright suite blocked on loopback EPERM; verifier blocked until non-sandboxed runner |
| M6-008 | Verify Stripe live webhook delivery | BLOCKED-WAITING-USER ... | evidence | human-only | low | stripe-event |  | verifier queries Stripe API (STRIPE_SECRET_KEY available) for live webhook event |
| M7-001 | Real-account live test (with refund) | BLOCKED-WAITING-USER ... | evidence | human-only | low | stripe-event |  | first paid txn; verifier needs both stripe-event AND db-row; user-only action §6 |
| M7-002 | PostHog analytics minimal events | BLOCKED | evidence | human-only | low | manual-only |  | manual-only until POSTHOG_API_KEY available; promotes to posthog-api per §5.5 |
| M7-003 | Sentry error monitoring | BLOCKED-PROVIDER (npm... | evidence | human-only | low | manual-only |  | manual-only until SENTRY_DSN + AUTH_TOKEN available; promotes to sentry-api per §5.5 |
| M7-008 | Post-launch 24h + 7d KPI review | BLOCKED | evidence | human-only | medium | manual-only |  | operator reads dashboards + writes summary; depends_on M7-001 done |
| M8-001 | ApiKey + ApiKeyUsage Prisma schema | in_progress (PR open ... | direct | loop | medium | detailed |  | PR #173 open; in_progress on board; registry status=in_progress; DO NOT re-dispatch |
| M8-002 | API key generate + revoke UI at /app/api-keys | BLOCKED-PREREQ | epic | strong-model | high | manifest-only | M8-API | planner-representative for M8-API (lowest id of children); depends_on M8-001 merged |
| M8-003 | API key authentication middleware | BLOCKED-PREREQ | epic | strong-model | high | manifest-only | M8-API |  |
| M8-004 | POST /api/v1/rewrite endpoint (API-key auth) | BLOCKED-PREREQ | epic | strong-model | high | manifest-only | M8-API |  |
| M8-005 | OpenAPI 3.0 spec at /api/v1/openapi.json | BLOCKED-PREREQ | epic | strong-model | high | manifest-only | M8-API |  |
| M8-006 | Per-key rate limiting via Cloudflare KV | BLOCKED-PREREQ | epic | strong-model | high | manifest-only | M8-API |  |
| M8-007 | Per-key monthly quota enforcement | BLOCKED-PREREQ | epic | strong-model | high | manifest-only | M8-API |  |
| M8-008 | Stripe products for B2B tiers | BLOCKED-PREREQ | epic | strong-model | high | manifest-only | M8-API | requires Stripe Products via API §"Sprint posture" — but real-charge automation §6 stop applies |
| M8-009 | B2B subscription state machine | BLOCKED-PREREQ | epic | strong-model | high | manifest-only | M8-API | state-machine-modeling skill applies |
| M8-010 | B2B Stripe webhook handlers (shared endpoint) | BLOCKED-PREREQ | epic | strong-model | high | manifest-only | M8-API |  |
| M8-011 | B2B customer portal link from /app/api-keys | BLOCKED-PREREQ | epic | strong-model | high | manifest-only | M8-API |  |
| M8-012 | API docs site at /developers | BLOCKED-PREREQ | epic | strong-model | high | manifest-only | M8-API | extends existing /developers page from M9-009 |
| M8-013 | Standardized API error JSON format | BLOCKED-PREREQ | epic | strong-model | high | manifest-only | M8-API |  |
| M8-014 | Idempotency-Key header support | BLOCKED-PREREQ | epic | strong-model | high | manifest-only | M8-API |  |
| M8-015 | Webhook subscriptions for API customers | BLOCKED-PREREQ | epic | strong-model | high | manifest-only | M8-API |  |
| M8-016 | B2B onboarding email + Stripe customer creation flow | BLOCKED-PREREQ | epic | strong-model | high | manifest-only | M8-API |  |
| M9-002 | Implement MCP tools (rewrite_email, analyze_signal, lis... | BLOCKED | direct | loop | medium | manifest-only |  | package-scoped: packages/mcp-server/src/tools.ts + index; needs brief writer before dispatch; was started 2026-05-23T13:23 — check current state |
| M9-006 | Publish @replyinmyvoice/mcp-server to npm | BLOCKED-WAITING-USER ... | evidence | human-only | low | manual-only |  | npm publish is user-only per §6 hard stops; verifier could promote to file-present check against npm registry response |

Row count: 60 (matches `find_next_pending_issue` filter as of 2026-05-23).

---

## Heuristic shortcuts (use if you don't want to think about each row)

These shortcuts will get you to a workable v0 classification quickly. You can refine later when running into specific friction.

```text
M1-002 .. M1-010   →  epic (parent: M1-Entra). The first one (M1-002 or
                      whichever you pick) is the epic-lane parent;
                      the rest get Epic=M1-Entra.
                      Owner=strong-model, Coupling=high, Detail=manifest-only

M2-001 .. M2-009   →  epic (parent: M2-Quality), same shape as M1.

M2.5-002           →  evidence (evidence_type=file-present, looking for
                      docs/learning-baseline.md result rows)
M2.5-007           →  epic (parent: M2.5-LearningOps), high coupling

M3-001 .. M3-008   →  epic (parent: M3-V2). M3-002, M3-005, M3-006 might
                      arguably be direct-medium if scoped tightly, but
                      safer to bundle as epic for v0.

M4-001             →  evidence (evidence_type=file-present, the 4 sample
                      cases JSON file under docs/)
M4-011, M4-015     →  epic (parent: M4-FrontendPolish)

M5-003             →  direct (likely medium, single script file)

M6-001 .. M6-007   →  evidence. evidence_type varies:
                        M6-001/M6-002: manual-only (Cloudflare API in
                          sandbox isn't reachable; user reads diff and
                          submits artifact path)
                        M6-003/M6-004/M6-005: http-200 (verifier blocked
                          on out-of-sandbox HTTP; manual-only for v0)
                        M6-007: manual-only (Playwright suite blocked on
                          loopback EPERM)
M6-008             →  evidence (evidence_type=stripe-event)

M7-001             →  evidence (evidence_type=stripe-event + db-row)
M7-002             →  evidence (evidence_type=manual-only until
                      POSTHOG_API_KEY available, then posthog-api)
M7-003             →  evidence (same shape: manual-only → sentry-api)
M7-008             →  evidence (manual-only — KPI review is a human task)

M8-001             →  direct, in_progress, owner=loop, Coupling=medium,
                      Detail=detailed (already has PR #173). Leave alone;
                      registry status reflects in_progress.

M8-002 .. M8-016   →  epic (parent: M8-API). All blocked-prereq on M8-001
                      merging; once M8-001 done, M8-API epic becomes
                      eligible for planning.

M9-002             →  direct, medium. (MCP tools — one file,
                      package-scoped.)
M9-006             →  evidence (evidence_type=manual-only — npm publish
                      is user-only). Verifier could be file-present
                      checking npm registry response.
```

If you accept all these heuristics as-is, fill the table accordingly. They're not authoritative — your call on any row.

---

## After you fill this in

1. Save the file.
2. Tell Claude "Phase 1 trigger: classification ready".
3. Claude reviews your fills, flags any rows that look risky (e.g., direct+high, or unfilled cells), and then proceeds to Phase 1 implementation (registry generator + dispatcher lane routing).

If you want to do this in stages (e.g., classify just the launch-critical rows first), tell Claude that — Phase 1 can run on a partial classification as long as the dispatcher knows to skip unclassified rows (default = most-restrictive per §3 of decisions doc).
