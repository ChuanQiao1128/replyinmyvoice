# PAY-05: Capture Stripe receipt_url on the credit grant and expose it in purchase history

**Priority:** P0 · **Owner:** Codex · **Skill:** data-module-review · **Depends on:** none

## Context
- Repo root: `/Users/qc/Desktop/CloudFlare`
- Credit grant: `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/StripeEventService.cs` (`SyncCheckoutSessionAsync` ~386) inserts a `RewriteCredit` and already captures `StripePaymentIntentId`, `StripeSku`, `StripeAmountTotal`, `StripeCurrency` (SO-020) — but NOT `receipt_url`.
- Entity: `backend-dotnet/src/ReplyInMyVoice.Domain/Entities/RewriteCredit.cs`; config in `…/Infrastructure/Data/AppDbContext.cs`.
- Purchase history: `AccountService.GetPurchaseHistoryAsync` → `AccountPayment` → `GET /api/me/payments` (`AccountHttpFunctions.cs`).
- **Gap:** customers and support/reconciliation have no receipt link. The pricing-spec lists `receipt_url` as a field to capture.

## Constraints (AGENTS.md)
- Banned terms: `humanizer|bypass|undetect|detector|evade`.
- **Migration safety:** a merge to `main` auto-runs `dotnet ef database update` on LIVE Azure SQL. The new column MUST be additive + nullable. NEVER `--force-reset`.
- Do NOT push to `main`.

## Changes required
1. Add nullable `StripeReceiptUrl` (string) to `RewriteCredit`; add an **additive, nullable** EF Core migration (do not alter/drop existing columns).
2. In `SyncCheckoutSessionAsync`, resolve the receipt URL on a paid checkout grant. The checkout session object does not carry it directly — read it from the expanded `payment_intent.latest_charge.receipt_url` (or fetch the PaymentIntent/Charge) when available; tolerate absence (leave null).
3. Expose `receiptUrl` in `AccountPayment` / `GET /api/me/payments`. (Admin per-user payments view `AdminService.GetUserDetailAsync` should surface it too if cheap.)

## Acceptance (machine-checkable)
- [ ] New migration adds a nullable `StripeReceiptUrl` column only (verify the generated migration has no destructive op).
- [ ] xUnit test: a paid checkout whose source object includes a receipt url → the granted `RewriteCredit.StripeReceiptUrl` is persisted; absence → null (no crash).
- [ ] `GET /api/me/payments` returns `receiptUrl` (test or contract assertion).
- [ ] `cd backend-dotnet && dotnet test` green.

## Do NOT
- Do NOT change grant amounts, expiry, or idempotency logic.
- Do NOT write a destructive migration; do NOT touch `main` / deploy.
