# P2-03: GET /api/me/billing/history (packs + subscription invoices + refunds)

**Tier:** 2 · **Owner:** Codex · **Depends on:** P2-01

## Context
- Repo root: `/Users/qc/Desktop/CloudFlare`. Spec: `plans/rewrite-api-v1/PHASE-2-SPEC.md` §C (BILLH-02).
- One-time **pack** history already exists: `AccountService.GetPurchaseHistoryAsync` (`AccountService.cs:176-212`) over `RewriteCredit` where `Source` is a purchase. **Subscription invoices** now live in `StripeInvoice` (delivered by P2-01). Refunds reduce credits via `charge.refunded` (see `StripeEventService`).
- Entra (portal) auth: `FunctionAuthResolver`. Next pass-through pattern: `app/api/me/payments/route.ts` (forward Entra token, same-origin).

## Changes required
1. **New method** on `AccountService` that returns a **unified, date-sorted** billing history for a user, merging:
   - pack purchases (existing) → `{ type:"pack", date, description (sku/name), amount, currency, status:"paid", receiptUrl }`,
   - subscription invoices (`StripeInvoice`) → `{ type:"subscription", date, description (period), amount (AmountPaid or AmountDue), currency, status, hostedInvoiceUrl }`,
   - refunds (from refunded credits / `charge.refunded`) → `{ type:"refund", date, description, amount (negative), currency, status:"refunded" }`.
2. **Functions endpoint** `GET api/me/billing/history` (Entra-authed; `401` if unauthenticated).
3. **Next pass-through** `app/api/me/billing/history/route.ts` (same-origin, forward token).

## Acceptance (machine-checkable)
- [ ] xUnit: seeded packs + `StripeInvoice`s (+ a refund) for user A and user B → A's history is merged, newest-first, ownership-isolated (no B rows); amounts/currency correct.
- [ ] `cd backend-dotnet && dotnet test` green; `npm run typecheck` green.

## Do NOT
- Do NOT expose card data or another user's records. Do NOT drop same-origin on `/api/me/*`.
- Do NOT recompute pack history differently — reuse `GetPurchaseHistoryAsync` data.
