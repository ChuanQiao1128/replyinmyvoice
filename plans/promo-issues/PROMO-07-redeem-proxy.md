# PROMO-07 — Next.js redeem proxy: Turnstile verify + IP/secret forward (Phase 4, TIER 2)

Wave: promo-wave · Spec: `plans/promo-code-trial-spec.md` (read §8.1, §8.3, §13). Deps: PROMO-03.

## Context
Browser → this proxy → C# `promo/redeem`. The proxy is where Turnstile is verified and the trusted client IP is captured + forwarded. Mirror `app/api/me/route.ts` (getAzureApiBaseUrl, getCurrentAccessToken, requireSameOrigin).

## Changes required
1. `app/api/promo/redeem/route.ts` (`POST`): same-origin check; read `{code, turnstileToken}`; **verify Turnstile** via `https://challenges.cloudflare.com/turnstile/v0/siteverify` with `TURNSTILE_SECRET_KEY` + `cf-connecting-ip` → on fail return `403 invalid_captcha`; forward to `${getAzureApiBaseUrl()}/api/promo/redeem` with `Authorization: Bearer`, `X-Client-IP: <cf-connecting-ip>`, `X-RIMV-Proxy-Secret: <PROMO_PROXY_SHARED_SECRET>`; pass through the C# status/body.
2. Optional `app/api/promo/status/route.ts` (`GET`).
3. **Env-strict, fail-closed:** in production, missing `TURNSTILE_SECRET_KEY` or `PROMO_PROXY_SHARED_SECRET` ⇒ the route returns a server-config error (do NOT skip the captcha or forward an unguarded IP). In dev, default to Cloudflare test keys (site `1x00000000000000000000AA` / secret `1x0000000000000000000000000000000AA`).
4. Add `lib/turnstile.ts` (server-side verify helper) + IP helper (reuse `lib/auth-rate-limit.ts`'s `clientIpFromRequest`).

## Acceptance (machine-checkable)
- Route unit tests: non-same-origin → rejected; missing/blocked Turnstile token → `403 invalid_captcha`; valid path forwards the three headers; prod-config missing secret → fail closed (server-config error, not bypass).
- `npm run test` + `npm run typecheck` green.

## Constraints / Do NOT
- Do NOT trust client-supplied `X-Forwarded-For`; the C# side trusts only the proxy-secret-guarded header.
- Do NOT log the Turnstile token, secret, or raw IP. Never put secret VALUES in tracked files (read from env).
- No banned terms in `app/components/lib`. No push/PR/deploy.
