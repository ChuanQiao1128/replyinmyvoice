# PROMO-09 — Signup hardening: Turnstile + disposable-email block (Phase 4, TIER 2)

Wave: promo-wave · Spec: `plans/promo-code-trial-spec.md` (read §8.3, §7.3 T1/T4). Deps: - (touches the auth/signup path; sequence carefully).

## Context
Multi-account farming starts at signup. Add a Turnstile gate + reject disposable/temp-mail domains, server-side, before account creation. Auth is Entra-native email+password (see `lib/entra-native-auth.ts` and the signup API route).

## Changes required
1. Locate the server-side signup entry (the Next.js route that initiates Entra-native signup). Add **Turnstile verification** there (reuse `lib/turnstile.ts` from PROMO-07); fail → reject before creating the account.
2. Add a bundled **disposable-email-domain blocklist** (e.g. `lib/disposable-email-domains.ts` or a JSON asset from a known public list, a few thousand domains) + a checker; reject signup when the email domain is on the list, with a clear user message.
3. Add the Turnstile widget to the signup form (`NEXT_PUBLIC_TURNSTILE_SITE_KEY`; dev = test key).
4. Env-strict/fail-closed in prod (missing Turnstile secret → reject, don't bypass).

## Acceptance (machine-checkable)
- Unit tests: disposable domain → rejected; normal domain → allowed; missing/blocked Turnstile token → rejected.
- Playwright: signup form shows Turnstile; bot/blank token blocked; disposable email shows guidance; a normal signup still completes (dev test keys).
- `npm run test`/`typecheck` green; existing auth tests still pass.

## Constraints / Do NOT
- Do NOT break the existing email+password / Google flows; only ADD the gate.
- Do NOT log tokens/secrets. No banned terms. No push/PR/deploy.
