# PROMO-03 — User HTTP endpoints + /api/me promo block (Phase 1, TIER 1)

Wave: promo-wave · Spec: `plans/promo-code-trial-spec.md` (read §12, §13, §4). Deps: PROMO-02.

## Context
Expose redemption over HTTP (Azure Functions) and surface promo state to the frontend via the existing `/api/me` summary so `/app` can branch its empty state.

## Changes required
1. `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/PromoHttpFunctions.cs`:
   - `POST` `Route = "promo/redeem"` (anonymous-auth + JWT like `AccountHttpFunctions`); read body `{code, turnstileToken}`; read the trusted client IP only from the proxy-supplied header when `PROMO_PROXY_SHARED_SECRET` matches the `X-RIMV-Proxy-Secret` header (else treat IP as untrusted/null). Map `PromoService` result → HTTP: `200`; `400 invalid_request`; `422 invalid_code`; `422 code_expired`; `409 already_redeemed`; `409 code_exhausted`; `429 ip_velocity`; `500 server_config|server_error`; `401` unauthenticated.
   - Optional `GET` `Route = "promo/status"`.
   - **Enumeration-resistant:** not-found / inactive / not-yet-valid all return the SAME generic `invalid_code`.
2. `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/AccountService.cs` — extend `GetOrCreateAccountSummaryAsync` to add a `promo` block to the summary: `{ hasRedeemed, eligible, trialRemaining, trialExpiresAt }` (compute from `PromoCodeRedemption` for the user + active PROMO credits). Map credit `Source=="PROMO"` to a friendly label `"Trial rewrites"` in the `Sources[]` labels. Do NOT change `remaining` math.

## Acceptance (machine-checkable)
- WebApplicationFactory integration tests assert each status code path (401/400/403-from-proxy/422 invalid/422 expired/409 already/409 exhausted/429/200).
- `/api/me` response includes the `promo` block; a redeemed user shows `hasRedeemed=true` + `trialRemaining`/`trialExpiresAt`; PROMO source labeled "Trial rewrites".
- Unknown/inactive/not-yet-valid → identical `invalid_code` (no enumeration).
- `dotnet test` green.

## Constraints / Do NOT
- Do NOT trust a client-supplied `X-Forwarded-For`; only the proxy-secret-guarded header.
- Do NOT change quota/remaining math or consumption.
- Never log code values, tokens, secrets, or raw IPs. No banned terms. No push/PR/deploy.
