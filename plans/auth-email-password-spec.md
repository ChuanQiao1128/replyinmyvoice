# Consumer Auth → Entra External ID Native Authentication (Email + password + Reset) Specification

> Status: PROPOSED (analysis turn — no source changed yet). Owner decision **2026-05-31**: identity
> model = **email + password + self-service password reset** (NOT passwordless), implemented via
> **Entra External ID Native Authentication** (server-side proxy), Google kept as browser redirect.
> This document **supersedes** `plans/auth-native-otp-spec.md` (which was passwordless email-OTP and
> explicitly non-goal'd password accounts).
> Branch: `feat/auth-login-register`. Testing surface: `localhost:3000` for the build/mock phase,
> then production `replyinmyvoice.com` for the live real-inbox smoke once the tenant is enabled.
> Microsoft references (re-verify during Phase 0):
> - Native auth API reference: https://learn.microsoft.com/en-us/entra/identity-platform/reference-native-authentication-api
> - Native auth concept + limitations: https://learn.microsoft.com/en-us/entra/identity-platform/concept-native-authentication
> - Self-service password reset (native): https://learn.microsoft.com/en-us/entra/identity-platform/how-to-native-authentication-prepare-app

## Context

Today the login/signup UI is **not in our app**. `app/sign-in/[[...sign-in]]/page.tsx` and
`app/sign-up/[[...sign-up]]/page.tsx` render `components/auth/google-oauth-card.tsx`, whose email
form **and** "Continue with Google" button both `GET /api/auth/login`
(`lib/entra-auth.ts: createLoginRedirectUrl`), which redirects to the **Microsoft Entra hosted user
flow**. The app never sends or validates a code itself. Owner-reported symptoms: (1) login/registration
not working, (2) email codes not arriving, (3) the hosted method-selection page is ugly and off-brand,
(4) once on `*.ciamlogin.com` there is no way back to home.

`tests/e2e/auth-gate.spec.ts` already asserts an in-app code UI ("Email code sign-in",
"Continue with email code") that was never built — the test currently fails. That test will be
**rewritten** for the email+password model below.

Native authentication lets us host a **pixel-perfect in-app email+password UI** (with email-OTP used to
verify the email at sign-up and to reset the password) while Entra stays the identity provider and still
issues the same tokens, so the existing `rimv_session` cookie, `middleware.ts`, `lib/azure-api.ts`, and
the .NET `/api/me` upsert keep working unchanged.

## Goals

1. In-app, on-brand **email + password** sign-up and sign-in (no redirect to Microsoft for the email path).
2. **Email verification at sign-up** via Entra email OTP (the code the user reported missing), triggered by our server.
3. **Self-service password reset** ("forgot password"): email → code → new password, fully in-app.
4. Beautiful, responsive `/sign-in`, `/sign-up`, `/forgot-password` with a clear **"← Back to home"** affordance, inline field/error states, password show/hide, and password-policy hints.
5. **Google** stays working via the existing browser-redirect flow, with a real, official-style Google button (native auth cannot do social).
6. **Account deletion** ("注销账号") UI wired to the existing `.NET DELETE /api/me`.
7. Mint our own `rimv_session` cookie exactly as today, so downstream (.NET, middleware, `/app`) is unchanged.
8. Local-testability on `localhost:3000` without bouncing to the production domain.

## Non-Goals

- Replacing Google/social with native auth (Microsoft does not support social via native auth).
- Removing the Entra identity backend or migrating IdP.
- Changing the .NET identity model or the Prisma `User` schema (subject stays the Entra `oid`/`sub`).
- SMS / phone MFA, passkeys, magic-links (email+password+email-OTP-verification only for v1).
- B2B api-key auth (separate mechanism, out of scope here).

## Phase 0 — Prerequisites & risks (gating, owner-actioned)

> Local `az` points at the wrong tenant (`53d7…`, not the CIAM tenant `614ea821`), so Claude **cannot**
> verify or toggle the items below. These are owner actions in the Entra portal; Claude supplies the
> checklist and verifies the result via decoded tokens / curl once enabled.

1. **App registration** (the `NEXT_PUBLIC_ENTRA_CLIENT_ID` app) →
   - Authentication → **"Allow public client flows" = Yes**.
   - **Enable native authentication = Yes**.
2. **User flow** bound to that app must enable, as identity methods: **Email with password** (sign-up
   + sign-in) and **Email one-time passcode** (used for email verification), and **self-service
   password reset**. Confirm any **required attributes** (e.g. display name) — if required, the sign-up
   form must collect them.
3. **Token compatibility** (the load-bearing unknown): a native-auth `/token` access token must carry
   the **same `aud` / `iss` / `scp`** that `lib/entra-auth.ts: validateEntrabearerToken` and the .NET
   `FunctionAuthResolver` expect. Verify by decoding a real test token's claims (no secrets printed). If
   it differs, adjust the requested `scope` (and/or the API audience config) — **do not** weaken .NET validation.
4. **Google redirect URI**: register `http://localhost:3000/auth/callback` for local Google testing (Google end-to-end is portal-config-dependent and out of the hard scope — see decision 3).
5. **Diagnose the current prod redirect failure** while here (likely API-scope consent) — native auth shares the scope dependency.

If native auth cannot be enabled, the agreed fallback is the **reskin-the-hosted-redirect** path (not built unless the owner switches to it).

### Phase-0 Results / Owner Actions (Issue #338)

- Added `scripts/verify-token-compat.mjs` as the offline token-compatibility checker. It accepts `--token <jwt>` or stdin, decodes only the JWT header and payload, and prints `alg`, `aud`, `iss`, `scp` / `scope`, and `exp`.
- The script includes `--self-test` with a documented sample token shaped from placeholder `NEXT_PUBLIC_ENTRA_*` values. It does not require portal access or live tenant values.
- Live tenant JWT decode is marked `needs_manual_verify` because local `az` is signed into the wrong tenant. Owner action after native authentication is enabled: run `node scripts/verify-token-compat.mjs --token "<jwt>"`, then compare `aud`, `iss`, and `scp` / `scope` with the existing validation helper and the .NET API validation settings.
- `docs/manual-setup.md` now carries the Entra External ID native email + local account owner checklist, including native authentication, public client flows, Email one-time passcode for verification/reset, and self-service account reset.
- `.env.example` contains no Clerk auth environment entries for this cutover. The remaining `clerkUserId` model/upsert naming landmine is unchanged and remains tracked outside Phase 0.

## Current System (unchanged pieces we reuse)

- `lib/entra-auth.ts`
  - `createLoginRedirectUrl()` — hosted-flow authorize URL (PKCE, `prompt=select_account`), `rimv_oauth` state cookie. **Kept for Google.**
  - `completeEntraCallback()` — exchanges code at `/oauth2/v2.0/token`, validates id_token, mints `rimv_session` `{sub,email,name,exp,accessToken,accessTokenExp}`. **Reused for Google.**
  - `getCurrentSession()` / `getCurrentAccessToken()` — read the session cookie. **Reused.**
  - `validateEntrabearerToken()` — validates inbound API tokens. **Reused / Phase-0 compat target.**
  - Signed-cookie helpers (`createSignedCookieValue` / `verifySignedCookieValue`, `AUTH_SESSION_secret`). **Reused for the new short-lived flow cookies.**
  - Stale `SignupFlowState` + `rimv_signup` helpers (lines ~636–680) — **repurpose/replace** for the new flow.
- `app/api/auth/login/route.ts` (Google redirect), `app/auth/callback/route.ts`, `app/api/auth/logout/route.ts`, `app/api/auth/access-token/route.ts` — **kept**; callback gains visible error surfacing.
- `middleware.ts` — protects `/app` + `/api/rewrite`; redirects unauthenticated to `/sign-in` with a `redirect_to` query param **that the sign-in page ignores** (camelCase `redirectTo` elsewhere — **bug to fix**).
- `.NET` `/api/me` (GET account summary, DELETE account), `/api/rewrite` — unchanged; upsert by Entra `oid`.
- Env present (names only): `NEXT_PUBLIC_ENTRA_AUTHORITY/CLIENT_ID/API_SCOPE`, `ENTRA_CLIENT_secret`, `AUTH_SESSION_secret`, `NEXT_PUBLIC_APP_URL`, `GOOGLE_*_FOR_ENTRA`, `AZURE_EXTERNAL_ID_AUTHORITY`.

## Proposed Architecture

**Server-side proxy**: browser → our Next.js route handlers → Entra native-auth endpoints. Native-auth
endpoints don't support browser CORS; calling them server-side avoids CORS, keeps the flow in our trust
boundary, lets us mint the httpOnly `rimv_session`, and **keeps raw passwords out of any browser-readable
store**. No MSAL browser SDK.

- Native-auth base = `getEntraAuthority().replace(/\/v2\.0$/, "")` (same derivation the token exchange uses).
- Native auth is a **public client** → use `NEXT_PUBLIC_ENTRA_CLIENT_ID` only; **never** send `ENTRA_CLIENT_secret` to native endpoints.
- `challenge_type` always includes `redirect` so Entra can punt to the hosted flow when it needs something we don't handle (MFA/social) → UI shows "Continue in browser" → existing `/api/auth/login`.

### Sign-UP (new user: email + password, with email verification)
1. `POST {base}/signup/v1.0/start` — `client_id`, `username=<email>`, `password=<password>`, `challenge_type="password oob redirect"`, `attributes` JSON if the user flow requires (e.g. displayName) → `continuation_token`.
2. `POST {base}/signup/v1.0/challenge` — `client_id`, `continuation_token`, `challenge_type="oob redirect"` → **Entra emails the code**; returns `code_length`, `challenge_target_label`, new `continuation_token`.
3. `POST {base}/signup/v1.0/continue` — `client_id`, `continuation_token`, `grant_type=oob`, `oob=<code>` → `continuation_token` (email verified).
4. `POST {base}/oauth2/v2.0/token` — `client_id`, `continuation_token`, `grant_type=continuation_token`, `scope="openid offline_access email profile <API_SCOPE>"` → tokens.

> Exact placement of `password` (at `start` vs a `continue` step) and `attributes` requirements must be
> confirmed in Phase 0 against the live user flow. **The password is only ever sent in the `start`
> request server-side; it is never written to a cookie.** If the live flow requires password on a
> later `continue` step, hold it only for the duration of the single request chain — never persist it.

### Sign-IN (existing user: email + password)
1. `POST {base}/oauth2/v2.0/initiate` — `client_id`, `username=<email>`, `challenge_type="password oob redirect"` → `continuation_token`.
2. `POST {base}/oauth2/v2.0/challenge` — `client_id`, `continuation_token`, `challenge_type="password oob redirect"` → expect `challenge_type=password` (collect password).
3. `POST {base}/oauth2/v2.0/token` — `client_id`, `continuation_token`, `grant_type=password`, `username`, `password`, `scope=...` → tokens.

> The whole initiate→challenge→token chain runs inside **one** `/api/auth/signin` POST (email + password
> both supplied up front), so no intermediate cookie is needed and the password lives only for that
> request. If `challenge` returns `oob`/`redirect` instead of `password` (OTP-only account or MFA),
> fall back to "Continue in browser" (hosted redirect).

### password RESET (self-service)
1. `POST {base}/resetpassword/v1.0/start` — `client_id`, `username=<email>`, `challenge_type="oob redirect"` → `continuation_token`.
2. `POST {base}/resetpassword/v1.0/challenge` — → **emails the code**; returns `code_length`, label, new `continuation_token`.
3. `POST {base}/resetpassword/v1.0/continue` — `grant_type=oob`, `oob=<code>` → `continuation_token`.
4. `POST {base}/resetpassword/v1.0/submit` — `continuation_token`, `new_password=<password>` → `continuation_token`.
5. `POST {base}/resetpassword/v1.0/poll_completion` — poll until `status=succeeded`.
6. On success → redirect to `/sign-in?reset=success` (user signs in with the new password). New password is sent only in the `submit` request; never persisted.

### Google (unchanged)
"Continue with Google" → existing `/api/auth/login?redirectTo=…` redirect flow → `/auth/callback`. Only the **button styling** changes this round.

### Session minting (reuse existing)
After any `/token`, validate the id_token with the existing `validateIdTokenClaims`, then mint
`rimv_session` via `createSignedCookieValue` with `{sub,email,name,exp,accessToken,accessTokenExp}` —
optionally storing `refreshToken` (+exp) for silent renewal. **No downstream changes.**

## Data Model

**No new DB table required.** Flow state lives in short-lived, signed, httpOnly cookies (same signer as `rimv_oauth`):
- `rimv_signup` `{continuationToken, email, displayName?, codeLength, channelLabel, lastSentAt, exp}` — **no password**.
- `rimv_reset` `{continuationToken, email, codeLength, channelLabel, lastSentAt, exp}` — **no password**.
- Sign-in needs no cookie (single request chain).
- Optional hardening if abuse seen: Prisma `OtpThrottle { emailHash, windowStart, attempts }` for cross-instance rate limiting (deferred; `data-module-review` if added).
- `prisma/schema.prisma User`: **unchanged**. Landmine: `clerkUserId @unique` is still required at the .NET upsert layer; native auth yields the same Entra subject so behavior is unchanged — tracked for a separate rename, not this module.

## API Contracts (new Next.js BFF routes — all `dynamic = "force-dynamic"`, no secrets in responses)

| Route | Body | Success | Failure |
|---|---|---|---|
| `POST /api/auth/signup/start` | `{email, password, displayName?}` | `{ok:true, codeLength, channelLabel}` + sets `rimv_signup` | `{ok:false, error, fallbackRedirect?}` |
| `POST /api/auth/signup/verify` | `{code}` | `302 → redirectTo` (sets `rimv_session`, clears `rimv_signup`) | `{ok:false, error, remainingAttempts?}` |
| `POST /api/auth/signup/resend` | `{}` | `{ok:true, cooldownSeconds}` | `{ok:false, error}` |
| `POST /api/auth/signin` | `{email, password, redirectTo?}` | `302 → redirectTo` (sets `rimv_session`) | `{ok:false, error, fallbackRedirect?}` |
| `POST /api/auth/reset/start` | `{email}` | `{ok:true, codeLength, channelLabel}` + sets `rimv_reset` | `{ok:false, error}` |
| `POST /api/auth/reset/verify` | `{code, newpassword}` | `{ok:true, next:"/sign-in?reset=success"}` (clears `rimv_reset`) | `{ok:false, error, remainingAttempts?}` |
| `POST /api/auth/reset/resend` | `{}` | `{ok:true, cooldownSeconds}` | `{ok:false, error}` |

Existing `/api/auth/login` (Google), `/auth/callback`, `/api/auth/logout`, `/api/auth/access-token` remain.
Account: existing `.NET GET /api/me`, `DELETE /api/me` (already proxied/reachable).

## State & Error Handling

Flow state machines (model with `state-machine-modeling`):
- Sign-up: `idle → submitting → code_sent → verifying → authenticated | error`.
- Sign-in: `idle → submitting → authenticated | error | redirect_fallback`.
- Reset: `idle → email_submitted → code_sent → verifying → resetting → done | error`.

Map Entra error codes to friendly UX (never leak raw Entra text):
- wrong code / `invalid_grant` → inline "Code is incorrect" (decrement local attempt counter).
- expired `continuation_token` → "Code expired — resend" (restart the start step).
- wrong password (sign-in) → "Email or password is incorrect".
- `user_not_found` (sign-in/reset) → "No account for this email — create one" (link to `/sign-up`, preserve email).
- `user_already_exists` (sign-up) → "Account exists — sign in" (link to `/sign-in`, preserve email).
- password policy violation → surface Entra's policy hint mapped to friendly text.
- `challenge_type=redirect` → "Continue in browser" → existing redirect flow.
- network/5xx → generic retry; log without PII beyond hashed email.
- **Surface `/auth/callback` failures**: `/sign-in?error=…` must render a visible banner (fixes current silent bounce).

Resend cooldown (30s) + max code attempts (5) → force restart. Server-side IP+email rate limit on every `start`/`resend`/`signin`.

## Security & Privacy

- Public client → `NEXT_PUBLIC_ENTRA_CLIENT_ID` only; never send `ENTRA_CLIENT_secret` to native endpoints.
- **passwords**: sent only over server-side HTTPS to Entra in the single request that consumes them; **never** written to any cookie, log, or response. Enforce a client+server minimum-length check before calling Entra.
- All native calls server-side; continuation tokens live only in httpOnly signed cookies, never exposed to JS.
- `rimv_session` stays httpOnly + `Secure` (prod) + `SameSite=Lax`, signed with `AUTH_SESSION_secret`.
- Validate id_token before trusting claims (existing signature path).
- Rate-limit start/resend/signin to prevent email-bombing and password spraying.
- **Banned-term guard**: no `humanizer | bypass | undetect | detector | evade` in any new copy, identifier, comment, or filename. Run the grep before each PR.

## UI (web-design-engineer + ui-browser-testing)

- `/sign-up`: step 1 = email + password (+ displayName if required) with policy hints & show/hide; step 2 = code entry (auto-advance, resend with countdown). "← Back to home". Inline errors. Link to `/sign-in`.
- `/sign-in`: email + password, "Forgot password?" link, "Continue with Google" (real Google button), "← Back to home", error banner (incl. `?error=` from callback), link to `/sign-up`.
- `/forgot-password`: step 1 = email; step 2 = code + new password (+ confirm). Success → `/sign-in?reset=success`.
- Real Google button: official mark + Google color/typography per brand guidelines (replace the text "G" box). Reuse existing redirect href.
- `/app/account`: account summary (`GET /api/me`), sign-out, and **delete account** (typed-confirm dialog → `DELETE /api/me` → redirect home).
- All pages: responsive (desktop+mobile), keyboard-accessible, dark-mode-consistent with `.rimv` system.

## Verification Plan

- **Unit** (vitest): native-auth request builders, error mapping, cookie sign/verify, session minting, redirect-fallback detection, password min-length guard.
- **Resilience** (`resilience-test-generation`): wrong code, expired continuation token, resend cooldown, rate-limit lockout, Entra 5xx, idempotent double-submit, wrong password, reset poll timeout.
- **Integration**: mock Entra native endpoints; assert full sign-up, sign-in, and reset sequences set/clear cookies and that `getCurrentSession()` then succeeds.
- **Browser** (Playwright + screenshots, desktop+mobile): `/sign-in`, `/sign-up`, code step, `/forgot-password`, error states, "back to home", `/app/account` delete-confirm; console/network clean. **Rewrite `tests/e2e/auth-gate.spec.ts`** to the email+password UI.
- **Live smoke** (post-portal-enable): real inbox — owner relays the code; assert end-to-end sign-up + sign-in + reset; **decode the access token** and assert `.NET /api/me` accepts it (token-compat).

## Issue Decomposition (for the delivery workflow → Codex)

Dependency order: **A** → (**B**, **C**, **D**, **F** in parallel) → **E** → **G** (Phase-0 verify is owner-gated, runs as soon as the tenant is enabled).

- **A. `lib/entra-native-auth.ts`** — typed server client: `signupStart/Challenge/Continue`, `signinpassword`, `resetStart/Challenge/Continue/Submit/Poll`, low-level `nativeAuthFetch`, typed responses, Entra-error→code mapping. Vitest unit tests with mocked fetch. *(resilience, state-machine)*
- **B. Sign-up routes** — `/api/auth/signup/{start,verify,resend}` + `rimv_signup` cookie + session minting reuse. Integration tests (mock Entra). *(state-machine)*
- **C. Sign-in route** — `/api/auth/signin` (one-shot password) + error mapping + redirect fallback. Tests.
- **D. Reset routes** — `/api/auth/reset/{start,verify,resend}` + `rimv_reset` cookie + poll handling. Tests. *(resilience)*
- **E. UI** — rebuild `/sign-up` + `/sign-in`, new `/forgot-password`, real Google button, surface `/auth/callback` errors, fix `redirect_to`/`redirectTo` mismatch, **rewrite `auth-gate.spec.ts`**. *(web-design-engineer, ui-browser-testing)* — may split into E1 (sign-up/sign-in) + E2 (forgot-password + Google button + callback errors).
- **F. Account page** — `/app/account`: summary + sign-out + delete-account confirm → `DELETE /api/me`. *(ui-browser-testing)*
- **G. Phase-0 verify + docs** — token-compat decode, portal-prerequisite checklist (owner), update `AGENTS.md` / `docs/manual-setup.md` (Clerk → Entra native email+password), remove dead Clerk env, note the `clerkUserId` landmine. *(data-module-review)*

## Open Questions (resolve in Phase 0)

1. Does the native-auth access token's `aud`/`iss`/`scp` match what `validateEntrabearerToken` + .NET expect with no change? (decode a test token)
2. Is "Enable native authentication" + "public client" toggleable via Graph/`az` with portal creds, or portal-only (owner action)? (local `az` is the wrong tenant → assume owner action)
3. Does the sign-up user flow **require attributes** (e.g. displayName)? Where does `password` go — `start` or a `continue` step?
4. Is password reset (`/resetpassword/v1.0/*`) enabled on the user flow, and does it return a sign-in-ready continuation or require a fresh sign-in?
5. Is the current prod login failure config (scope/consent) or something else?
