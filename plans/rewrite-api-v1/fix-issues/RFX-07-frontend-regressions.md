# RFX-07: Frontend regressions + safety (FIX-06, FIX-07, FIX-13, FIX-15, FIX-18)

**Tier:** 2 · **Owner:** Codex · **Depends on:** none
Detailed findings: `plans/rewrite-api-v1/CROSS-REVIEW.md` (#6, #7, #13, #15, #18). #6 and #7 are confirmed PROD REGRESSIONS in just-shipped features.

## Context
- **CSV export 403 (FIX-06):** the Usage/Billing "Export CSV" controls are plain `<a download href="/api/me/.../export">` (`components/developers/usage-panel.tsx` ~288-295, `billing-panel.tsx` ~249-256). A `<a download>` GET sends **no `Origin` header**, but the export routes call `requireSameOrigin` (`lib/http.ts:10-21`, `lib/security.ts`) which returns **403 in production** for no-Origin requests. So the export never works in prod.
- **Telemetry dropped (FIX-07):** `lib/api-observability.ts` is called as `void captureApiEvent({...})` in `app/api/v1/rewrite/route.ts` (~46,73), `app/api/v1/usage/route.ts`, `app/api/v1/rewrite/[id]/route.ts` — a floating promise with no `waitUntil`, so on Cloudflare Workers it is cancelled after the response and the PostHog/Sentry event is dropped.
- **Proxy error shape (FIX-13):** `app/api/v1/rewrite/route.ts` (~72) + the `[id]` and `usage` routes rethrow Azure fetch failures → clients get a framework 500 instead of `{error:{code,message}}`.
- **CSV formula injection (FIX-15):** `lib/csv-export.ts` (~11-22) quotes commas/quotes/newlines but does not neutralize cells starting with `= + - @` / tab / CR.
- **same-origin GET (FIX-18):** `app/api/me/route.ts`, `app/api/keys/route.ts`, `app/api/me/payments/route.ts` GET handlers skip `requireSameOrigin` (unlike sibling routes).

## Changes required
1. **FIX-06:** make the Export CSV buttons `fetch()` the CSV (same-origin fetch sends `Origin`, so `requireSameOrigin` passes), read the blob, and trigger a client-side download (createObjectURL + a temporary `<a>`), with loading/error states. (Keep the server route's `requireSameOrigin`.)
2. **FIX-07:** flush telemetry via the Workers waitUntil — e.g. `getCloudflareContext().ctx.waitUntil(captureApiEvent(...))` (use the OpenNext/`@opennextjs/cloudflare` context helper already used elsewhere if present) so the event actually sends; keep it non-blocking and swallow errors but emit at least a debug log.
3. **FIX-13:** in the v1 proxy routes, catch backend fetch failures and return `502` with `{error:{code:"proxy_request_failed",message:...}}` (the documented error shape).
4. **FIX-15:** in `lib/csv-export.ts`, prefix cells that begin with `= + - @`, tab, or CR with a single quote `'` before quoting.
5. **FIX-18:** apply `requireSameOrigin(request)` to the GET handlers of `/api/me`, `/api/keys`, `/api/me/payments` (consistent with siblings).

## Acceptance (machine-checkable)
- [ ] `npm run typecheck` + `npm run test` green (add/adjust route + unit tests: CSV serializer neutralizes formula cells; proxy returns `{error:...}` 502 on backend failure; same-origin enforced on the three GETs; export uses fetch+blob).
- [ ] Banned-term grep clean.

## Do NOT
- Do NOT remove `requireSameOrigin` from the export routes (fix the client instead). Do NOT expose server secrets to the client. Do NOT block the response on telemetry.
