# PAY-19: Notification infrastructure (transactional email + in-app) — enabler

**Priority:** P1 (enabler) · **Owner:** Codex · **Skill:** cloud-architecture-cost-review · **Depends on:** none · **Enables:** PAY-23, PAY-24, PAY-27

## Context
- Repo root: `/Users/qc/Desktop/CloudFlare`
- **There is NO email/notification infrastructure today** (grep for smtp/sendgrid/resend/postmark/IEmailSender/EmailService in `backend-dotnet/src` returns nothing). Stripe sends its own receipt + failed-payment emails, but credit-expiry and self-serve-refund flows are our own concepts Stripe cannot email about.
- Backend is C# Azure Functions; the stack is already on Azure — **Azure Communication Services Email** is the natural provider, but Codex chooses (Resend/SendGrid/ACS) and documents the decision in `plans/decisions-log.md`.
- Support address: `info@timeawake.co.nz`.

## Constraints
See `plans/payment-module-audit.md`. Standard rules: banned terms `humanizer|bypass|undetect|detector|evade`; never put provider API keys in source (env at runtime; key NAMES only in tracked files); never push to `main`; never initiate a real charge.

## Changes required
1. Define an `INotificationService` abstraction (send transactional email by template + recipient + model; extensible to in-app later).
2. Implement ONE provider behind it, configured by env (e.g. `NOTIFICATIONS_PROVIDER`, provider key name documented). Missing/disabled config → graceful no-op (log, do not throw).
3. A minimal templating mechanism (named templates + typed data), with the first templates stubbed for the consumers (failed-payment, credit-expiring, refund-request-received).
4. Register in DI; document env key NAMES in the env-wiring docs.

## Acceptance (machine-checkable)
- [ ] `INotificationService` + one provider implementation exist and are DI-registered.
- [ ] Unit test with a fake/in-memory provider asserts a send is invoked with the right template + recipient; and a missing-config path is a no-op (no throw).
- [ ] No real email sent in tests. `cd backend-dotnet && dotnet test` green.

## Do NOT
- Do NOT hardcode keys. Do NOT send real email in tests. Do NOT touch `main`.
