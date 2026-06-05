# P2-01: StripeInvoice entity + EF migration + populate from invoice.* webhooks

**Tier:** 1 (prereq, merged into base) · **Owner:** Codex · **Depends on:** none

## Context
- Repo root: `/Users/qc/Desktop/CloudFlare`. Spec: `plans/rewrite-api-v1/PHASE-2-SPEC.md` §C (BILLH-01/03).
- Stripe webhook dispatch lives in `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/StripeEventService.cs` (event-type switch around `:388-434`; handlers `SyncInvoicePaymentFailedAsync`, `SyncInvoicePaymentSucceededAsync`). Idempotency is via the `StripeEvents` table (already present) — reuse it; do not add a second dedupe.
- EF context: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Data/AppDbContext.cs`. Entity style to mirror: `RewriteCredit.cs`, `StripeEvent.cs` (`backend-dotnet/src/ReplyInMyVoice.Domain/Entities/`).
- Stripe SDK (Stripe.net) types are already used in this service — reuse the same `Invoice` object access pattern.

## Changes required
1. **New entity** `StripeInvoice` (`Domain/Entities/StripeInvoice.cs`):
   `Id` (string PK = Stripe invoice id), `UserId` (Guid FK to AppUser), `SubscriptionId` (string?), `Status` (string: draft/open/paid/void/uncollectible), `AmountDue` (long, minor units), `AmountPaid` (long), `Currency` (string), `PeriodStart` (DateTimeOffset?), `PeriodEnd` (DateTimeOffset?), `AttemptCount` (int), `NextPaymentAttempt` (DateTimeOffset?), `HostedInvoiceUrl` (string?), `InvoicePdf` (string?), `CreatedAt`, `UpdatedAt`, `RowVersion` (Guid).
2. **Map** in `AppDbContext`: PK = `Id` (string); index `(UserId, CreatedAt)`. Generate EF migration `AddStripeInvoice` in the Infrastructure project (`dotnet ef migrations add AddStripeInvoice`).
3. **Populate** in `StripeEventService`: on `invoice.paid`, `invoice.payment_succeeded`, and `invoice.payment_failed` (add `invoice.paid`/`invoice.finalized` to the dispatch switch if absent), **idempotently upsert** a `StripeInvoice` from the Stripe invoice object (match the local user by Stripe customer id, same lookup the other handlers use). Re-processing the same event id must not duplicate or corrupt the row (upsert by invoice `Id`, update Status/AmountPaid/AttemptCount/NextPaymentAttempt/UpdatedAt).

## Acceptance (machine-checkable)
- [ ] xUnit in `backend-dotnet/tests/ReplyInMyVoice.Tests/`: a sample `invoice.paid` event upserts a `StripeInvoice` with correct fields for the matched user; replaying the same event id leaves exactly one row; a subsequent `invoice.payment_failed` updates Status→ and bumps AttemptCount on the same row.
- [ ] `cd backend-dotnet && dotnet test` green; `dotnet build` green (migration compiles).

## Do NOT
- Do NOT change live price IDs, `*_WEBHOOK_SECRET`, or any `*_LIVE_*` / `LAUNCH_CONFIRMED` wiring.
- Do NOT alter the existing credit-grant (`checkout.session.completed`) or quota logic.
- Do NOT initiate any Stripe API write/charge — this issue only persists incoming invoice data.
