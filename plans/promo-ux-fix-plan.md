# Promo UX Fix Plan — workspace-first + admin entry

> Fixes the post-login UX the owner flagged on the live `/app`: the redeem-code card
> took over the whole page, and there was no admin entry point. **Frontend-only** change
> (no backend/migration/secret changes). Branch cut from CURRENT `main` (bd6b79d) to avoid
> a stale base. Delivered via the Dynamic Delivery Workflow.

- **Status:** plan; executes via dynamic-delivery-workflow daemon.
- **Branch:** `feat/promo-ux-fix` (off `main`).
- **Date:** 2026-06-03.

## Problems (from live `/app`)
1. After login, `/app` renders `<RedeemCodeCard/>` **full-page** when the user has 0 credits and hasn't redeemed (`selectAppExperience` → `"redeem"`). The product workspace should be the first thing shown.
2. Redeeming should be a **button on the rewrite workspace** ("add free rewrites"), opening the code entry — not a page that replaces the tool.
3. The signed-in user is an **admin** but the header has **no link to the admin console** (`/admin` exists, gated by `isAdminSession`, but is undiscoverable).

## Current code (verified)
- `app/app/page.tsx` — branches on `selectAppExperience(...)` (`lib/promo-app-state.ts`); `"redeem"`/`"free-paywall"`/`"paid-paywall"` each render a **full-page** card instead of the workspace.
- `components/site-header.tsx` — server component; `getCurrentSession()`; shows Pricing/Developers/Sign out/Open app. No admin link.
- `lib/admin-auth.ts` — `isAdminSession(session)` already exists (used by `app/admin/page.tsx`). Reuse it in the header — no `/api/me` change needed.
- `components/app/redeem-code-card.tsx` — full-page card (redeem POST `/api/promo/redeem` + Turnstile). Used only in `app/app/page.tsx`.
- `components/app/rewrite-workspace.tsx` + `components/app/subscription-status.tsx` — the workspace + quota/status bar; quota-aware already.

## Desired UX
- `/app` **always renders `RewriteWorkspace`** (the tool is the first thing shown), regardless of credit balance.
- The workspace status bar shows remaining credits **plus**:
  - a **"Redeem code"** button (opens the code entry as a **modal/dialog**, reusing the redeem logic + Turnstile),
  - when `remaining === 0`: an inline nudge ("You have 0 rewrites — redeem a trial code or buy a pack") with the redeem button + a `/pricing` link. The Rewrite action stays gated by the existing server quota (402) — but the page is never replaced.
- Header shows an **"Admin"** link → `/admin`, **only when `isAdminSession(session)`** is true.

## What changes (files)
| File | Change |
|---|---|
| `components/site-header.tsx` | add `import { isAdminSession } from "../lib/admin-auth"`; if `isAdminSession(session)` render `<Link href="/admin">Admin</Link>` in `nav-links` (signed-in branch). |
| `app/app/page.tsx` | stop returning full-page `redeem`/`free-paywall`/`paid-paywall`; **always render `RewriteWorkspace`**, passing promo state + an `outOfCredits`/`canRedeem` flag. |
| `components/app/rewrite-workspace.tsx` | render a **"Redeem code"** button + (when 0 remaining) an inline redeem/buy nudge; mount the redeem modal. |
| `components/app/subscription-status.tsx` | surface the "Redeem code" button next to the quota label (or pass through). |
| `components/app/redeem-code-card.tsx` | refactor so it can render inside a **modal/dialog** (controlled open/close), keeping the redeem POST + Turnstile; on success, refresh `/api/me` so new credits appear without full reload. |
| `lib/promo-app-state.ts` | repurpose `selectAppExperience` from a page-level gate into an in-workspace banner state (e.g. `needsRedeem` / `needsBuy` / `ok`), or drop the page-gate usage. Keep `labelForQuotaSource` / `trialExpiryLabel`. |
| `tests/unit/*` | update `promo-app-state` tests + `workspace-copy`/contract tests that asserted the full-page redeem/paywall experience to the new in-workspace model. |

## Out of scope / unchanged
- Backend, EF migrations, prod secrets, `/api/promo/redeem`, `/api/me` — all unchanged (frontend-only).
- The paid-quota paywall content can be preserved as the in-workspace nudge for paid users (same copy).

## Delivery
- 2 issues via the dynamic-delivery-workflow daemon (branch off `main`, integration branch `feat/promo-ux-fix`):
  1. **PROMO-FIX-01** — header Admin link (`isAdminSession` → `/admin`).
  2. **PROMO-FIX-02** — `/app` workspace-first + redeem-as-modal-button + out-of-credits nudge + `promo-app-state`/tests update.
- Gates: `npm run typecheck` + `npm run test` (contract tests updated) + banned-term grep.
- Deploy: frontend-only → push `main` triggers `cloudflare-worker.yml` build-test → `cf:deploy` (NO dotnet migration this time). Same gated auto-deploy + live smoke + rollback-on-fail as the promo wave (AGENTS.md 2026-06-02 authorization covers this follow-up to the promo feature).
