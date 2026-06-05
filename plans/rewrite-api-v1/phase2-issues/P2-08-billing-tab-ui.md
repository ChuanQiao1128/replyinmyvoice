# P2-08: Billing dashboard tab — plan + unified billing history + receipts

**Tier:** 2 · **Owner:** Codex · **Depends on:** P2-03, P2-07

## Context
- Repo root: `/Users/qc/Desktop/CloudFlare`. Spec: `plans/rewrite-api-v1/PHASE-2-SPEC.md` §C.
- P2-07 created the developer dashboard tab shell (Keys | Usage | **Billing**) on `/developers/keys`; this issue fills the **Billing** tab.
- Data: `GET /api/me/billing/history` (from P2-03); current plan/usage from `GET /api/me` (`AzureAccountSummary`); Stripe portal link via the existing `app/api/stripe/portal/route.ts`. Existing purchase UI for reference: `components/account/account-panel.tsx` `PurchaseHistorySection`.

## Changes required
1. **Billing tab body** (`components/developers/billing-panel.tsx`):
   - Current plan + status + renewal/period end; a "Manage payment method" button → Stripe billing portal.
   - **Unified billing history** table from `/api/me/billing/history`: date, type (pack/subscription/refund), description, amount, status, and a receipt / invoice link (`receiptUrl` or `hostedInvoiceUrl`) when present.
   - Loading + empty states; mobile responsive; "Warm Writing Desk" styling; English copy.

## Acceptance (machine-checkable)
- [ ] `npm run typecheck` green; `npm run test` green (update pinned copy tests if any).
- [ ] Banned-term gate clean on `app components public lib`.
- [ ] Renders with mocked history; data fetched only via same-origin `/api/me/*`.

## Do NOT
- Do NOT re-implement the history merge on the client — consume the server endpoint.
- Do NOT expose card data or another user's records.
