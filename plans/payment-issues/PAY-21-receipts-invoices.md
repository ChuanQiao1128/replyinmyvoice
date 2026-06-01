# PAY-21: User-facing receipts / tax invoices (view + download)

**Priority:** P1 · **Owner:** Codex · **Skill:** ui-browser-testing + data-module-review · **Depends on:** PAY-05 (#382, receipt_url capture), nice-to-have PAY-20

## Context
- Repo root: `/Users/qc/Desktop/CloudFlare`
- After PAY-05, each `RewriteCredit` (PURCHASE) will carry `StripeReceiptUrl`. Purchase history API: `GET /api/me/payments` (`AccountService.GetPurchaseHistoryAsync` → `AccountPayment`).
- Customers + accounting need an accessible receipt per purchase; today there is no receipts UI. Prefer Stripe-hosted receipts (`receipt_url`) and Stripe invoicing over building a PDF engine.

## Constraints
See `plans/payment-module-audit.md`. Standard rules (banned terms, no secrets, never push to main, never initiate a real charge).

## Changes required
1. Surface a **Receipts / Purchase history** view in the account area (e.g. `/account` or workspace) listing purchases (date, pack, amount, currency, expiry, remaining) with a **View receipt** link to the Stripe-hosted `receiptUrl`.
2. If PAY-20 enabled tax, ensure the receipt/invoice reflects the GST line (Stripe handles this when Tax + invoicing are on — verify and document, don't hand-roll tax math).
3. Keep it a thin proxy to the existing `GET /api/me/payments` (add the route under `app/api/me/payments` proxy if not present).

## Acceptance (machine-checkable)
- [ ] Account/receipts view renders the purchase list and a working receipt link for a seeded payment (Playwright or component test).
- [ ] `GET /api/me/payments` exposes `receiptUrl` (contract test).
- [ ] `npm run build` + `npm run test` green; banned-term grep clean.

## Do NOT
- Do NOT hand-build tax calculations. Do NOT add billing logic in the frontend beyond proxying. Do NOT touch `main`.
