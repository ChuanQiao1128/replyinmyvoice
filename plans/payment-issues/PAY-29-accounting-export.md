# PAY-29: Accounting / revenue export (CSV) for bookkeeping + tax filing

**Priority:** P2 · **Owner:** Codex · **Skill:** data-module-review · **Depends on:** none

## Context
- Repo root: `/Users/qc/Desktop/CloudFlare`
- No export for bookkeeping/tax. Revenue + credit consumption live in `RewriteCredit` (Source=PURCHASE, amount/currency/sku/grantedAt) and usage in `UsagePeriod`/`UsageReservation`/`RewriteCostLog`. Admin endpoints exist (`AdminHttpFunctions.cs`, admin-gated).

## Constraints
See `plans/payment-module-audit.md`. Standard rules (banned terms, no secrets, never push to main, never initiate a real charge).

## Changes required
1. An **admin-gated** export endpoint returning CSV for a date range: one section/file for revenue (payments: date, user ref, sku, amount, currency, paymentIntent, receiptUrl) and optionally credit-consumption + cost-to-date.
2. Stream/paginate so a large range does not OOM. Caller must be admin (reuse `AdminAccess.RequireAdminAsync`).

## Acceptance (machine-checkable)
- [ ] Admin export returns well-formed CSV for a seeded date range (correct rows, escaping); non-admin → 403.
- [ ] A large dataset does not load entirely in memory (streamed/paged) — asserted by test or noted with the approach.
- [ ] `cd backend-dotnet && dotnet test` green.

## Do NOT
- Do NOT expose any secret or full card data (there is none). Do NOT touch `main`.
