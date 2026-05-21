# Clerk Removal Map

**Issue**: M1-001 Inventory all Clerk usage in repo
**Date**: 2026-05-21
**Scope**: Read-only audit. No source changes performed. Subsequent issues M1-002 .. M1-014 act on this map.

## Scan methodology

```text
grep -RIn "@clerk/"                                              (no hits in src — package-level migration complete)
grep -RIn -E "(clerkMiddleware|clerkClient|currentUser|useUser|useAuth|SignedIn|SignedOut|UserButton|ClerkProvider|getAuth)"
grep -RIn -E "(CLERK_|NEXT_PUBLIC_CLERK_)"
grep -RIn "clerkUserId"
```

Generated files under `lib/generated/prisma/**` are excluded — they regenerate from `prisma/schema.prisma`. `node_modules` and `.next/` excluded.

## Summary counts

- **TypeScript/TSX source files referencing Clerk identifiers**: 9 (all via `clerkUserId` or `ADMIN_CLERK_USER_IDS` — not `@clerk/*` imports)
- **Prisma model fields**: 1 (`User.clerkUserId String @unique`)
- **Env-var references in `.env.example`**: 1 (`ADMIN_CLERK_USER_IDS=`)
- **Env-var references in `lib/env.ts`**: 0 (already removed)
- **`@clerk/*` package imports anywhere in `app/`, `components/`, `lib/`, `tests/`**: 0
- **Clerk symbols** (`clerkMiddleware`, `useUser`, `useAuth`, `SignedIn`, etc.): 0
- **`app/sign-in[[...sign-in]]/page.tsx`**: already migrated to `<GoogleOAuthCard />`
- **`middleware.ts`**: already a no-op `NextResponse.next()` (no Clerk middleware, no Entra check yet either)
- **`app/auth/callback/route.ts`**: exists (Entra callback handler is in place)
- **Test files**: 1 (`tests/unit/admin-auth.test.ts` — uses `clerkUserId` as fixture key)
- **Docs / plans referencing Clerk**: 28 files (mostly historical / rollback notes)

**Headline**: package-level Clerk removal is already done. What remains is a DB column rename (`clerkUserId` → `entraUserId`) propagated through 9 source files + 1 prisma model + 1 test, plus env/docs cleanup.

## Middleware

| File | Line | Current state | Action required |
| --- | --- | --- | --- |
| `middleware.ts` | 1–13 | No-op `NextResponse.next()` with a path matcher. No Clerk import; no Entra check either. | **M1-002**: add Entra session validation (the `auth-rewrite-flow` redirect logic that used to live in `clerkMiddleware` is currently missing — `/app` is open to unauthenticated traffic at the middleware layer; route handlers still enforce). |

## API routes

| File | Line(s) | Clerk identifier | Action required |
| --- | --- | --- | --- |
| `app/api/stripe/webhook/route.ts` | 16, 101–112 | Imports `updateStripeCustomerByClerkId`; reads `session.client_reference_id ?? session.metadata?.clerkUserId`; passes `{ clerkUserId }` to `applySubscriptionToUser` | **M1-008**: rename to `entraUserId` lookup with backward-compatibility fallback during migration window. |
| `app/api/stripe/checkout/route.ts` | 31, 41 | Reads `user.clerkUserId` from session; passes `clerkUserId` to Stripe metadata builder | **M1-008**: same — use `entraUserId` once column rename lands. |

No other `app/api/**/route.ts` references Clerk.

## Client components / pages

| File | Line(s) | Current state | Action required |
| --- | --- | --- | --- |
| `app/app/page.tsx` | 22 | Reads `user.clerkUserId` from auth context to pass to client | **M1-007/M1-008**: update once `entraUserId` becomes the canonical DB field. |
| `app/sign-in/[[...sign-in]]/page.tsx` | 1–5 | `<GoogleOAuthCard />` — Entra-only | Already migrated. Verify in **M1-004** that no Clerk surface remains. |
| `app/sign-up/[[...sign-up]]/page.tsx` | — | Presumed similar (file exists) | Confirm in **M1-005** that it renders only Entra UI. |
| `app/auth/callback/route.ts` | — | Entra code-exchange route exists | Confirm coverage in **M1-006**. |

## Server-only lib code

| File | Line(s) | Clerk identifier | Action required |
| --- | --- | --- | --- |
| `lib/users.ts` | 26, 38, 57, 62, 98–108 | `clerkUserId` column name in raw SQL (`ON CONFLICT ("clerkUserId")`); functions `findUserByClerkId`, `updateStripeCustomerByClerkId` | **M1-007**: rename column to `entraUserId` (or add new column + dual-write per backfill plan). **M1-008**: rename helper functions. |
| `lib/stripe.ts` | 6, 91, 98, 106, 113–116, 181, 185–186 | Imports `findUserByClerkId`; builds Stripe metadata key `metadata[clerkUserId]` and `subscription_data[metadata][clerkUserId]`; uses as `client_reference_id` | **M1-008**: rename in code; for Stripe metadata, decide whether to (a) keep `clerkUserId` key for historical webhook payload compatibility or (b) emit `entraUserId` as the new key and treat `clerkUserId` as legacy fallback. Recommend (b) with dual-read during migration. |
| `lib/entra-auth.ts` | 139 | `getSessionSecret` falls back to `optionalEnv("CLERK_SECRET_KEY")` if `AUTH_SESSION_SECRET` is unset | **M1-012**: remove the `CLERK_SECRET_KEY` fallback once `AUTH_SESSION_SECRET` is the documented variable. |
| `lib/admin-auth.ts` | 14–16, 22, 25, 27, 42, 45, 48 | Field name `clerkUserId` on the session/user shape; env var `ADMIN_CLERK_USER_IDS` | **M1-012**: rename env var to `ADMIN_ENTRA_USER_IDS` (or generic `ADMIN_USER_IDS`); rename field to `entraUserId`. |
| `lib/admin-visible.ts` | 5, 8, 12, 15 | Same `clerkUserId` field + `ADMIN_CLERK_USER_IDS` env | **M1-012**: parallel cleanup with `admin-auth.ts`. |

## Tests

| File | Line(s) | Use | Action required |
| --- | --- | --- | --- |
| `tests/unit/admin-auth.test.ts` | 10, 12, 17, 21, 23, 32, 34, 43, 45 | Fixtures keyed by `clerkUserId` and `adminClerkUserIds` | **M1-009 / M1-012**: rename to `entraUserId` / `adminEntraUserIds` once env + field rename lands. |

No other tests reference Clerk identifiers. Playwright e2e at `tests/e2e/` does not yet have an Entra-specific scenario (**M1-010** will add it).

## Config + env

| File | Line(s) | Reference | Action required |
| --- | --- | --- | --- |
| `.env.example` | 102 | `ADMIN_CLERK_USER_IDS=` | **M1-012**: rename. |
| `.env.local` | (private) | Contains Clerk-named entries (verified by name only — never print values) | **M1-012**: user updates locally; do not modify file from automation. |
| `lib/env.ts` | — | Clean already | No action. |
| `package.json` | — | No `@clerk/*` deps | No action — **M1-011** is effectively already done; just verify lockfile is clean. |
| `next.config.*` | — | Not searched in this audit; assume clean (no hits in TS scan) | Spot-check during **M1-011**. |

## Prisma schema

| File | Line | Reference | Action required |
| --- | --- | --- | --- |
| `prisma/schema.prisma` | 16 | `clerkUserId String @unique` on `User` | **M1-007**: add `entraUserId String? @unique`, run migration, backfill mapping, then deprecate `clerkUserId`. Document backfill in `plans/clerk-to-entra-user-backfill.md` per manifest. |

## Docs + plans

These files mention Clerk in narrative context and need wording updates once the Entra cutover lands; they do not block migration. Grouped:

**Top-of-list updates (M1-013 / M1-014):**

- `docs/manual-setup.md` (lines 7, 91, 97–142): the primary user-facing manual setup guide. Has a full Clerk section plus DNS records. Rewrite per M1-014.
- `local-env.md` (lines 28–40, 292–294): documents Clerk env vars. Strip in M1-012.
- `replyinmyvoice_requirements.md` (lines 307–312): legacy requirements doc with Clerk env vars. Mark as historical or strip.
- `.env.example`: covered above under config.
- `AGENTS.md`: mentions Clerk in historical context. Leave unless **M1-014** explicitly cleans.
- `CLAUDE.md`: mentions Clerk in re-read trigger list. Leave (historical reference is correct).

**Historical / informational only — leave as-is:**

- `docs/launch-cutover-plan.md`
- `docs/preflight-report.md`
- `docs/dotnet-azure-blocker-preflight.md`
- `docs/dotnet-azure-full-run-result.md`
- `docs/dotnet-azure-full-run-target.md`
- `docs/dotnet-azure-next-phase.md`
- `docs/business-qa-and-deploy-result.md`
- `docs/deepseek-adaptive-rewrite-attempt-ledger-strategy.md`
- `docs/learningops-runbook.md`
- `docs/next-development-brief.md`
- `docs/rewrite-email-eval-cases-100.md`
- `docs/skill-run-log.md`
- `docs/superpowers/plans/2026-05-20-admin-cost-observability.md`
- `docs/superpowers/plans/2026-05-20-azure-auth-data-migration.md`
- `docs/superpowers/plans/2026-05-20-azure-functions-cost-optimization.md`
- `docs/superpowers/plans/2026-05-20-resume-alignment-gap-closure.md`
- `docs/superpowers/plans/2026-05-21-azure-functions-sql-entra-migration.md`
- `docs/unified-fact-rewrite-long-run-target.md`
- `agent-skills/cloud-architecture-cost-review/SKILL.md`
- `agent-skills/resilience-test-generation/SKILL.md`
- `agent-skills/ui-browser-testing/SKILL.md`
- `plans/commercialization-recon.md`
- `plans/commercialization-roadmap.md`
- `plans/issue-manifest.md`
- `plans/issue-board.md`
- `plans/decisions-log.md`
- `plans/supervisor-handoff.md`
- `plans/issues/M0-001.md`

## Recommended removal order

Mapping to the M1 issue series, **adjusted for the fact that imports + UI surfaces are already migrated**:

| Issue | Scope (revised) | Status |
| --- | --- | --- |
| **M1-002** | Replace the current no-op `middleware.ts` with an Entra session check (current placeholder leaves auth enforcement entirely to route handlers). | TODO |
| **M1-003** | API route auth migration — mostly done; double-check every `app/api/**/route.ts` calls Entra session, not Clerk-style cookies. | LIKELY DONE; needs grep+test pass |
| **M1-004** | `app/sign-in/[[...sign-in]]/page.tsx` is already `<GoogleOAuthCard />`. Verify it covers all `/sign-in/...` sub-paths and there is no leftover Clerk route. | LIKELY DONE; needs verification |
| **M1-005** | `app/sign-up/[[...sign-up]]/page.tsx` — same verification. | LIKELY DONE; needs verification |
| **M1-006** | `app/auth/callback/route.ts` exists. Cover with route test. | TODO (tests) |
| **M1-007** | Add `entraUserId String? @unique` to `User`; create migration; write `plans/clerk-to-entra-user-backfill.md` (dual-write → cutover → drop). | TODO |
| **M1-008** | Migrate Stripe webhook + checkout to use `entraUserId`; rename `findUserByClerkId` and `updateStripeCustomerByClerkId` in `lib/users.ts`; rename `lib/stripe.ts` metadata helpers to dual-emit `entraUserId` while keeping `clerkUserId` legacy read. | TODO |
| **M1-009** | Add unit tests for Entra token validation in `lib/entra-auth.ts` (`tests/unit/entra-auth.test.ts`). | TODO |
| **M1-010** | Add Playwright e2e `tests/e2e/auth-rewrite-flow.spec.ts`. | TODO |
| **M1-011** | Verify no `@clerk/*` deps remain in `package.json` + lockfile (already clean per this audit). Run full validation. | LIKELY NO-OP |
| **M1-012** | Rename `ADMIN_CLERK_USER_IDS` → `ADMIN_ENTRA_USER_IDS` in `lib/admin-auth.ts`, `lib/admin-visible.ts`, `.env.example`, `tests/unit/admin-auth.test.ts`. Remove `CLERK_SECRET_KEY` fallback in `lib/entra-auth.ts` once `AUTH_SESSION_SECRET` is mandatory. | TODO |
| **M1-013** | Write `plans/clerk-dns-cleanup.md` listing the Cloudflare DNS records that become orphaned: `clerk.replyinmyvoice.com`, `accounts.replyinmyvoice.com`, `clkmail.replyinmyvoice.com`, `clk._domainkey.replyinmyvoice.com`, `clk2._domainkey.replyinmyvoice.com` (sources: `docs/manual-setup.md` lines 108–112). Include exact `wrangler` / Cloudflare API delete commands for the user to run manually post-cutover. | TODO |
| **M1-014** | Rewrite `docs/manual-setup.md` lines 7, 91, 97–142 to Entra-only; mark Clerk section as "Removed — see plans/clerk-dns-cleanup.md". Strip Clerk env section from `local-env.md` (lines 28–40, 292–294). Update `replyinmyvoice_requirements.md` lines 307–312. | TODO |

## Banned-term scan (self-check on this document)

This file uses only the literal token `Clerk` plus the column name `clerkUserId` and env var names like `ADMIN_CLERK_USER_IDS`. No banned terms (`humanizer | bypass | undetect | detector | evade`) are present.

## What this document is NOT

- Not a migration script — see M1-007's backfill plan.
- Not a DNS deletion runbook — see M1-013.
- Not a manual setup rewrite — see M1-014.
