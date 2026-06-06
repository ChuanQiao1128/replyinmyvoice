# MON-01: API observability + alert documentation (PostHog / Sentry, server-side)

**Tier:** 2 · **Owner:** Codex · **Depends on:** none

## Context
- Repo root: `/Users/qc/Desktop/CloudFlare`. Existing observability helper to mirror: `lib/payment-observability.ts` (study its shape/transport + how it reads env at runtime and no-ops when unset).
- v1 public API surface: Next routes `app/api/v1/rewrite/route.ts`, `app/api/v1/rewrite/[id]/route.ts`, `app/api/v1/usage/route.ts` (these run server-side on the Worker and forward to Azure). Backend already emits to **Application Insights** (`APPLICATIONINSIGHTS_CONNECTION_STRING` on the Functions app).
- Server-side keys exist in env (do NOT hardcode; read at runtime): `POSTHOG_API_KEY`, `SENTRY_DSN`. They are **server-side** (no `NEXT_PUBLIC_` prefix) — never expose to the client.

## Changes required
1. **New helper** `lib/api-observability.ts` (mirror `payment-observability.ts`): a function like `captureApiEvent({ endpoint, statusCode, latencyMs?, errorCode?, requestId? })` that, when `POSTHOG_API_KEY` is set, sends a PostHog server capture (`api_request` / `api_error`), and when `SENTRY_DSN` is set, reports server errors (5xx / unexpected) to Sentry. **Read both keys at runtime inside the function; if a key is unset, that transport is a silent no-op** (never throw, never block the response).
2. **Wire it into the v1 Next routes**: record an event for each v1 call's outcome — success (status, latency) and error (statusCode, errorCode parsed from the `{error:{code}}` body). Must not change response behavior or add latency on the hot path (fire-and-forget; swallow transport errors).
3. **Docs** `plans/rewrite-api-v1/monitoring.md`: the signals to alert on (API error-rate, 5xx, poll latency, `quota_exhausted`/`rate_limited` spikes, queue depth) and where each is observable (PostHog dashboards, Sentry issues, App Insights). Note that prod emission requires `POSTHOG_API_KEY` + `SENTRY_DSN` to be set on the Worker (the supervisor will set these at deploy).

## Acceptance (machine-checkable)
- [ ] Unit test `tests/unit/api-observability.test.ts`: with keys stubbed, `captureApiEvent` invokes the transport with the expected `api_error` shape; with keys unset it is a no-op and never throws.
- [ ] `npm run typecheck` + `npm run test` green; banned-term grep clean (`humanizer|bypass|undetect|detector|evade`).
- [ ] No secret VALUES in source; no `NEXT_PUBLIC_` exposure of the server keys. `monitoring.md` exists.

## Do NOT
- Do NOT add latency to or block the v1 response on telemetry (fire-and-forget, swallow errors).
- Do NOT expose `POSTHOG_API_KEY`/`SENTRY_DSN` to the browser. Do NOT add a heavy APM dependency — a minimal fetch-based capture is fine.
