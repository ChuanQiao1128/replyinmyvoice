# PAY-02: Wire Sentry + PostHog into production for payment/webhook failure visibility

**Priority:** P0 · **Owner:** Codex (code + config) / Owner (create monitors + supply secrets) · **Skill:** cloud-architecture-cost-review · **Depends on:** none

## Context
- Repo root: `/Users/qc/Desktop/CloudFlare`
- `SENTRY_DSN` and `POSTHOG_API_KEY` key names already exist (non-empty) in `.env.local`, but are NOT in `wrangler.jsonc` vars and are NOT initialized in the Worker or the Azure Functions runtime. `docs/observability.md` + `docs/support-runbook.md` treat Sentry (M7-003) and PostHog (M7-002) as not-yet-wired.
- **Defect:** there is no automated alerting on failed payments, webhook failures, or checkout errors — they are detected only by customer reports.
- Frontend: Next.js on Cloudflare Worker (`worker.js`, `open-next.config.ts`, `wrangler.jsonc`). Backend: C# Azure Functions (`backend-dotnet/src/ReplyInMyVoice.Functions`). Application Insights may already be partially present in the Functions host — check before adding a second APM.

## Constraints (AGENTS.md)
- Banned terms: `humanizer|bypass|undetect|detector|evade`.
- **Never** print/commit secret values. `SENTRY_DSN` / `POSTHOG_API_KEY` go into Cloudflare Worker secrets + Azure Functions app settings + GitHub Actions secrets — NOT into tracked files. `wrangler.jsonc` may reference them as vars only if they are non-secret; prefer Worker secrets for the DSN.
- Validate env at runtime in the handler that uses it, not at module import.
- Do NOT push to `main` / deploy.

## Changes required
1. **Frontend (Worker):** initialize Sentry (browser + Worker/server) and PostHog using env at runtime. Guard initialization so a missing key is a no-op (no crash) rather than a hard failure.
2. **Instrument the payment funnel** with PostHog events: `checkout_started` (BuyButton click, `components/landing/buy-button.tsx`), `checkout_redirected`, `payment_succeeded`, `payment_failed`, `webhook_failed`. Capture Sentry errors on the checkout proxy (`app/api/stripe/checkout/route.ts`) and account proxy failures.
3. **Backend (Functions):** report payment/webhook failures to Sentry (or Application Insights if already wired) from `StripeWebhookFunction` / `StripeEventService` failure paths and `BillingHttpFunctions` (checkout/portal errors). Include the correlation id from SO-R10 structured logging.
4. **Alerting rules** documented in `docs/observability.md`: alert on `webhook_failed` rate and on Functions payment-error rate; reference the UptimeRobot monitors (owner action).
5. Add `SENTRY_DSN` / `POSTHOG_API_KEY` to the env wiring docs (key names only) for Worker secrets + Functions app settings + CI secrets.

## Acceptance (machine-checkable)
- [ ] Sentry init + PostHog capture calls exist in the frontend, and the payment failure paths (`StripeWebhookFunction`/`StripeEventService` catch blocks, checkout proxy) emit an explicit error/event.
- [ ] Missing-key path is a graceful no-op (a unit/build check or a guarded init), proven by a test or by inspection noted in the PR.
- [ ] `npm run build` / typecheck pass; `dotnet build` passes.
- [ ] `docs/observability.md` lists the concrete alert rules.

## Do NOT
- Do NOT hardcode any DSN/key. Do NOT add the secret values to `wrangler.jsonc` or any tracked file.
- Do NOT push to `main` or deploy (owner wires secrets + deploys).
