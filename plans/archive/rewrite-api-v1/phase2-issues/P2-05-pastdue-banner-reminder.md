# P2-05: In-app PastDue banner + grace-period reminder notification

**Tier:** 2 · **Owner:** Codex · **Depends on:** P2-04

## Context
- Repo root: `/Users/qc/Desktop/CloudFlare`. Spec: `plans/rewrite-api-v1/PHASE-2-SPEC.md` §D (BILL-04c/d).
- On `invoice.payment_failed` a user is `PastDue` with `PaymentGraceEndsAt` set (kept ~7 days). Today the user gets ONE `FailedPaymentNotification` and otherwise sees nothing in-app until silently downgraded (after P2-04).
- Account summary feeds the portal: `AccountService.GetOrCreateAccountSummaryAsync` → `AzureAccountSummary` (`lib/azure-api.ts` types). Workspace: `app/app/page.tsx` + `components/app/subscription-status.tsx`. Stripe portal link already exists (Manage billing).

## Changes required
1. **Expose grace state** to the portal: ensure the account summary returns `subscriptionStatus` (PastDue) and `paymentGraceEndsAt` (add the field to the summary DTO + `lib/azure-api.ts` if missing).
2. **PastDue banner** (frontend): when status is `PastDue`, show a prominent but non-blocking banner in `/app` (and the developer dashboard) — "Payment failed — update your payment method by {graceEnd} to keep your plan" with a button to the Stripe billing portal. Hide when not PastDue.
3. **Grace reminder** (backend): enqueue a reminder notification roughly mid-grace (~day 5 of 7) if still PastDue and not yet recovered — implement as a check in the daily grace job (P2-04's timer) or a small dedicated check; dedupe so it sends once.

## Acceptance (machine-checkable)
- [ ] `npm run typecheck` green; `npm run test` green (update pinned copy tests — grep `tests/` first).
- [ ] xUnit (if backend reminder added): a PastDue user mid-grace gets exactly one reminder enqueued; a recovered/Active user gets none.

## Do NOT
- Do NOT block workspace usage during grace (PastDue keeps quota until expiry). Do NOT initiate a charge.
- Do NOT duplicate the existing one-shot failure notification — this is a SEPARATE mid-grace reminder.
