# PAY-03: Test that forged / tampered webhook signatures are rejected

**Priority:** P0 · **Owner:** Codex · **Skill:** dotnet-backend-testing · **Depends on:** none

## Context
- Repo root: `/Users/qc/Desktop/CloudFlare`
- Webhook signature verification: `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/StripeWebhookFunction.cs` uses `EventUtility.ConstructEvent` against `STRIPE_WEBHOOK_SECRET` (unsigned only allowed in `Testing` env).
- Existing tests: `backend-dotnet/tests/ReplyInMyVoice.Tests/StripeWebhookApiTests.cs` covers **missing** signature (400) and **missing secret** (500, fail-closed) and a valid signed event (accepted).
- **Gap:** the actual `ConstructEvent` rejection path — a signature that is *present but invalid* — is untested. This is the security-critical case (an attacker forging a webhook to grant themselves credits).

## Constraints (AGENTS.md)
- Banned terms: `humanizer|bypass|undetect|detector|evade`.
- Test-only change; no production code edits expected.
- Do NOT push to `main`.

## Changes required
Add xUnit tests to `StripeWebhookApiTests.cs` (Production env, `STRIPE_WEBHOOK_SECRET` configured) asserting **HTTP 400 + zero `StripeEvent` rows + zero `RewriteCredit` granted** for each:
1. **Wrong-secret signature:** body signed with a *different* secret than the server is configured with.
2. **Tampered payload:** valid `t=`/`v1=` header for an original body, but the delivered body is mutated after signing.
3. **Stale timestamp (replay):** signature computed with a timestamp outside Stripe's tolerance window.

Use Stripe.net's signing helper (or compute the HMAC-SHA256 `v1` manually) to build each malicious header. Keep the valid-signature happy-path test passing.

## Acceptance (machine-checkable)
- [ ] 3 new tests added; each asserts 400 + no event row + no credit grant.
- [ ] `cd backend-dotnet && dotnet test` green (all existing tests still pass).

## Do NOT
- Do NOT weaken or change `StripeWebhookFunction` verification to make tests pass.
- Do NOT touch `main` / deploy.
