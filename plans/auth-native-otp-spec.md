# Consumer Auth → Entra External ID Native Authentication (Email OTP in-app) Specification

> ⚠️ SUPERSEDED 2026-05-31 by `plans/auth-email-password-spec.md` — the owner changed the identity
> model from passwordless email-OTP to **email + password + self-service reset**. Keep this doc for
> history; build against the email+password spec.
>
> Status: PROPOSED (analysis turn — no code changed yet). Owner decision 2026-05-24: Option C
> (Entra native authentication). Testing surface: production `replyinmyvoice.com`.
> Source-of-truth inputs: `AGENTS.md`, `docs/manual-setup.md`, `lib/entra-auth.ts`,
> `components/auth/google-oauth-card.tsx`, `app/api/auth/*`, `app/auth/callback/route.ts`,
> `middleware.ts`, `lib/azure-api.ts`, `app/api/rewrite/route.ts`, `prisma/schema.prisma`,
> `backend-dotnet/src/ReplyInMyVoice.Api/Program.cs`.
> Microsoft references (verified 2026-05-24):
> - Native auth API reference: https://learn.microsoft.com/en-us/entra/identity-platform/reference-native-authentication-api
> - Native auth concept + limitations: https://learn.microsoft.com/en-us/entra/identity-platform/concept-native-authentication

## Context

Today the consumer login/signup UI is **not in our app**. `app/sign-in` and `app/sign-up`
render `components/auth/google-oauth-card.tsx`, whose email form and "Continue with Google"
button both `GET /api/auth/login`, which redirects to the **Microsoft Entra hosted user flow**
(`lib/entra-auth.ts: createLoginRedirectUrl`). `docs/manual-setup.md` (lines 94–96) confirms the
app never sends/validates email codes itself — Entra's hosted page does. Consequences the owner
reported: (1) login/registration not working, (2) email codes not arriving, (3) the hosted
method-selection page is ugly, (4) once on Microsoft's domain there is no way back to home.

Native authentication (External ID) lets us host a **pixel-perfect in-app email-OTP UI** while
Entra remains the identity provider and still issues the same tokens, so the existing
`rimv_session` cookie, `middleware.ts`, `lib/azure-api.ts`, and the .NET `/api/me` upsert keep
working unchanged.

## Goals

1. In-app, on-brand **email one-time-passcode** sign-up AND sign-in (no redirect to Microsoft for the email path).
2. Email codes reliably delivered (sent by Entra's email OTP method, triggered by our server).
3. Beautiful, responsive `/sign-in` + `/sign-up` with a clear **"← Back to home"** affordance and inline error states.
4. Keep Google working (via the existing browser-redirect flow — native auth cannot do social).
5. Mint our own `rimv_session` cookie exactly as today, so downstream (.NET, middleware, /app) is unchanged.
6. Local-testability: the flow must work on `localhost` without bouncing to the production domain.

## Non-Goals

- Replacing Google/social with native auth (Microsoft does not support social via native auth).
- Removing the Entra identity backend or migrating to a non-Entra IdP (that was Option B; not chosen).
- Changing the .NET identity model or the Prisma `User` schema (subject stays the Entra `sub`/`oid`).
- B2B API-key auth (separate mechanism on `feat/api-keys`; out of scope here).
- Password-based accounts or SMS MFA (email OTP only for v1).

## Current System

- `lib/entra-auth.ts`
  - `createLoginRedirectUrl()` builds the hosted-flow authorize URL (PKCE, `prompt=select_account`),
    sets `rimv_oauth` state cookie.
  - `completeEntraCallback()` exchanges the code at `/oauth2/v2.0/token`, validates the id_token,
    mints the `rimv_session` cookie carrying `{sub,email,name,exp,accessToken,accessTokenExp}`.
  - `getCurrentSession()` / `getCurrentAccessToken()` read that cookie.
  - `validateEntraBearerToken()` validates inbound API tokens (issuer/audience/scope) — also used by API.
- `app/api/auth/login/route.ts` → redirect to hosted flow (currently also pre-fills `login_hint`).
- `app/auth/callback/route.ts` → calls `completeEntraCallback`, on failure redirects to `/sign-in?error=callback` (error never displayed).
- `middleware.ts` protects `/app` + `/api/rewrite`; redirects unauthenticated to `/sign-in` with a
  `redirect_to` query param **that the sign-in page ignores** (uses `redirectTo` camelCase elsewhere — bug).
- `lib/azure-api.ts` / `app/api/rewrite/route.ts` send `Bearer <getCurrentAccessToken()>` to the .NET API.
- `.NET` `backend-dotnet/src/ReplyInMyVoice.Api/Program.cs` `/api/me` + `/api/rewrite` upsert by Entra subject via `AccountService`.
- `prisma/schema.prisma` `User { clerkUserId @unique (required, legacy), entraUserId @unique?, email? }`.
- Env present (names only): `NEXT_PUBLIC_ENTRA_AUTHORITY/CLIENT_ID/API_SCOPE`, `ENTRA_CLIENT_SECRET`,
  `AUTH_SESSION_SECRET`, `NEXT_PUBLIC_APP_URL=https://replyinmyvoice.com`, `GOOGLE_*_FOR_ENTRA`, `AZURE_EXTERNAL_ID_AUTHORITY`.

## Proposed Architecture

**Server-side proxy** (browser → our Next.js route handlers → Entra native-auth endpoints).
Rationale: native-auth endpoints do not support browser CORS; calling them server-side avoids CORS,
keeps the flow inside our trust boundary, and lets us mint the httpOnly `rimv_session` cookie. No MSAL
browser SDK needed.

Native-auth base = `getEntraAuthority().replace(/\/v2\.0$/, "")` (same derivation the token exchange already uses).

### Email OTP — Sign IN sequence (existing user)
1. `POST {base}/oauth2/v2.0/initiate` — `client_id`, `username=<email>`, `challenge_type="oob redirect"` → `continuation_token`.
2. `POST {base}/oauth2/v2.0/challenge` — `client_id`, `continuation_token`, `challenge_type="oob redirect"` → Entra emails code; returns `code_length`, `challenge_target_label`, new `continuation_token`.
3. `POST {base}/oauth2/v2.0/token` — `client_id`, `continuation_token`, `grant_type=oob`, `oob=<code>`, `scope="openid offline_access email profile <API_SCOPE>"` → `{access_token, id_token, refresh_token, expires_in}`.

### Email OTP — Sign UP sequence (new user)
1. `POST {base}/signup/v1.0/start` — `client_id`, `username=<email>`, `challenge_type="oob redirect"` (+ `attributes` JSON if the user flow requires e.g. displayName) → `continuation_token`.
2. `POST {base}/signup/v1.0/challenge` — `client_id`, `continuation_token`, `challenge_type="oob redirect"` → emails code.
3. `POST {base}/signup/v1.0/continue` — `client_id`, `continuation_token`, `grant_type=oob`, `oob=<code>` → `continuation_token` (verified).
4. `POST {base}/oauth2/v2.0/token` — `client_id`, `continuation_token`, `grant_type=continuation_token`, `scope=...` → tokens.

### `redirect` fallback + Google
- Every native call sends `challenge_type` including `redirect`. If Entra returns `challenge_type=redirect`
  (e.g. account requires social/federated/MFA we don't handle), the UI shows "Continue in browser" and
  hands off to the **existing** `/api/auth/login` redirect flow. Google "Continue with Google" uses that
  same existing flow (unchanged). Enhancement (verify): deep-link straight to Google to skip the chooser.

### Session minting (reuse existing)
After `/token`, validate the id_token with the existing `validateIdTokenClaims`, then mint `rimv_session`
via existing `createSignedCookieValue` with `{sub,email,name,exp,accessToken,accessTokenExp}` — plus store
`refreshToken` (+ exp) so we can silently renew. **No downstream changes**: `getCurrentSession`,
`getCurrentAccessToken`, middleware, `/api/me` keep working.

### New modules
- `lib/entra-native-auth.ts` — pure server functions: `startEmailSignIn`, `startEmailSignUp`,
  `resendCode`, `verifyEmailCode`, plus low-level `nativeAuthFetch` + typed responses + error mapping.
- `app/api/auth/otp/start/route.ts` — body `{email, mode}`; calls initiate/start + challenge; sets short-lived
  signed `rimv_otp` cookie `{continuationToken, mode, email, channelLabel, codeLength, exp}`; returns safe metadata.
- `app/api/auth/otp/verify/route.ts` — body `{code}`; reads `rimv_otp` cookie; calls continue/token; mints `rimv_session`; clears `rimv_otp`.
- `app/api/auth/otp/resend/route.ts` — re-challenge with cooldown.
- UI: replace `components/auth/google-oauth-card.tsx` with a two-step client component (email → code) built
  with the **web-design-engineer skill** for the visual layer (`anthropic-skills:web-design-engineer`).

## Data Model

**No new DB table required.** Flow state lives in a short-lived signed httpOnly cookie `rimv_otp` (same
signing helper as `rimv_oauth`). Continuation tokens are already short-lived server-side tokens.
- Optional hardening (only if abuse observed): a Prisma `OtpThrottle { emailHash, windowStart, attempts }`
  for cross-instance rate limiting. Deferred; review via `data-module-review` if added.
- `prisma/schema.prisma User`: unchanged. (Note legacy landmine: `clerkUserId` is still `@unique` &
  required at the .NET upsert layer — already handled today by the redirect flow; native auth produces the
  same Entra subject, so no new behavior. Tracked separately for M1-007 rename.)

## API and Job Contracts

`POST /api/auth/otp/start` → `{ ok, mode, channelLabel, codeLength }` | `{ ok:false, error, fallbackRedirect? }`
`POST /api/auth/otp/verify` → `302 → redirectTo` (sets `rimv_session`) | `{ ok:false, error, remainingAttempts? }`
`POST /api/auth/otp/resend` → `{ ok, cooldownSeconds }` | `{ ok:false, error }`
All read/write the `rimv_otp` cookie; all are `dynamic = "force-dynamic"`, no secrets in responses.
Existing `/api/auth/login` (Google/redirect) and `/auth/callback` remain.

## State and Error Handling

OTP flow states: `idle → code_sent → verifying → authenticated | error`. Map Entra error codes to UX:
- `invalid_grant` / wrong code → inline "Code is incorrect" (+ decrement local attempt counter).
- expired `continuation_token` → "Code expired — resend" (restart start step).
- `user_not_found` (sign-in) → "No account for this email — create one" (link to /sign-up, preserve email).
- `user_already_exists` (sign-up) → "Account exists — sign in" (link to /sign-in, preserve email).
- `challenge_type=redirect` → "Continue in browser" → existing redirect flow.
- network/5xx → generic retry message; never leak raw Entra error text.
- **Surface `/auth/callback` failures too**: `/sign-in?error=…` must render a visible banner (fixes current silent bounce).
Resend cooldown (e.g. 30s) + max attempts (e.g. 5) then force restart. Server-side IP+email rate limit on `start`.

## Security and Privacy

- Native auth is a **public client** (no secret). Use `NEXT_PUBLIC_ENTRA_CLIENT_ID` only; do not send `ENTRA_CLIENT_SECRET` to native endpoints.
- All native calls server-side over HTTPS; continuation token never exposed to the browser (kept in httpOnly `rimv_otp`).
- `rimv_session` stays httpOnly + `Secure` (prod) + `SameSite=Lax`, signed with `AUTH_SESSION_SECRET`.
- Validate id_token (signature path already exists for inbound tokens) before trusting claims.
- Rate-limit OTP start/resend to prevent email-bombing; log failures without PII beyond hashed email.
- Banned-term guard unaffected (no humanizer/bypass/undetect/detector/evade in any new copy/identifier).

## Rollout Plan

- **Phase 0 — Verify prerequisites (gating).**
  - Confirm token compatibility: a native-auth `/token` access_token must have the **same audience/issuer/scope**
    the .NET API + `validateEntraBearerToken` expect. (Verify by decoding a test token's claims — no secrets printed.)
  - Azure portal (owner has access): App registration → Authentication → "Enable mobile and desktop flows
    (public client) = Yes" AND "Enable native authentication = Yes". Confirm Email OTP is an enabled method in
    the user flow. Attempt via Microsoft Graph/az; if portal-only, owner toggles.
  - Diagnose *why the current prod redirect login fails* (likely API-scope consent or the uncommitted scope change);
    fix that, because native auth shares the email-OTP + scope dependencies.
  - Add a `localhost` override for `NEXT_PUBLIC_APP_URL` in dev so the flow is locally testable, and register the
    localhost redirect URI for the Google fallback.
- **Phase 1 — Backend:** `lib/entra-native-auth.ts` + the 3 `otp/*` routes + `rimv_otp` cookie + session minting + refresh.
- **Phase 2 — UI:** rebuild `/sign-in` + `/sign-up` (two-step email→code), home affordance, error banners (web-design-engineer).
- **Phase 3 — Google path:** keep redirect; optional Google deep-link; surface callback errors.
- **Phase 4 — Cleanup:** update `AGENTS.md`/`docs/manual-setup.md` (Clerk→Entra-native), remove dead Clerk env, fix `redirect_to`/`redirectTo` mismatch.

## Verification Plan

- **Unit** (`dotnet-backend-testing` is .NET-only; these are TS/Vitest): native-auth request builders, error
  mapping, `rimv_otp` cookie sign/verify, session minting, redirect-fallback detection.
- **Resilience** (`resilience-test-generation`): wrong code, expired continuation token, resend cooldown,
  rate-limit lockout, Entra 5xx, idempotent double-submit of the same code.
- **Integration**: mock the Entra native endpoints; assert full sign-up and sign-in sequences set `rimv_session`
  and that `getCurrentSession()` then succeeds.
- **Browser** (`ui-browser-testing` / Playwright + screenshots): desktop+mobile of /sign-in, /sign-up, code step,
  error states, "back to home"; console/network clean. Live smoke against the configured tenant with a real inbox.
- **Token compatibility**: assert a native-auth access token is accepted by `validateEntraBearerToken` and by .NET `/api/me`.

## Open Questions

1. Does the native-auth access_token's **audience** equal `NEXT_PUBLIC_ENTRA_CLIENT_ID` and **issuer** equal `NEXT_PUBLIC_ENTRA_AUTHORITY` (so .NET accepts it with no change)? — Phase 0 verification.
2. Is "Enable native authentication" toggbeable via Graph/az with current creds, or portal-only (owner action)?
3. Does the sign-up user flow **require attributes** (e.g. displayName)? If so, collect on the signup screen.
4. Can we deep-link "Continue with Google" to skip Entra's chooser, or is one interstitial unavoidable?
5. Is the current prod login failure config (scope/consent) or the uncommitted `entra-auth.ts` scope change?
