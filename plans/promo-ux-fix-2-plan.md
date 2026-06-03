# Promo UX Fix 2 — workspace action placement + admin 404 (reserved-route) fix

> Round-2 fixes after live review: (1) move the redeem/buy actions out of the rewrite OUTPUT
> box into the top status bar; (2) fix `/admin` returning "Admin request failed" — the C# admin
> routes use the **reserved `admin/` prefix** which Azure Functions 404s. Branch off CURRENT main.
> Delivered via the Dynamic Delivery Workflow.

- **Branch:** `feat/promo-ux-fix-2` (off `main`, currently b3188be).
- **Date:** 2026-06-03.

## Problem 1 — workspace action placement (design)
- `components/app/rewrite-workspace.tsx` (~L282-312): an "out of credits" box renders INSIDE the right output column with `Redeem code` / `Buy rewrites` / `Manage billing` buttons. Account-level actions don't belong in the output area.
- `components/app/subscription-status.tsx` (top bar): already has `Redeem code` (when `canRedeem`) + a button labelled `Upgrade` (free) / `Manage billing` (paid) that calls checkout/portal.
### Fix 1
- **Top status bar = the single home for account actions:** keep `Redeem code`; rename the free-user `Upgrade` → **`Buy rewrites`** (same `openCheckout` action). Paid users keep `Manage billing`.
- **Output area stays clean:** remove the Redeem/Buy/Manage **buttons** from the out-of-credits box in `rewrite-workspace.tsx`. Keep at most a one-line text hint ("You're out of rewrites — use Redeem code / Buy rewrites above"). The Rewrite action stays gated by the server 402.
- Files: `components/app/subscription-status.tsx`, `components/app/rewrite-workspace.tsx` (the `OutOfCredits`-style sub-component), and any test asserting the old output-box buttons.

## Problem 2 — /admin 404 ("Admin request failed")  ROOT CAUSE
- The C# admin functions ARE deployed/registered (`az functionapp function list` shows `AdminPing`, `AdminStats`, `AdminUsersList`, `AdminPromoCodes*`, …) with correct bindings (Anonymous GET, route `admin/ping` etc.). **Yet every `/api/admin/*` returns 404**, while `/api/me`, `/api/me/payments`, `/api/health/ready`, `/api/promo/redeem` all work.
- `/admin/host/status` → 401 → confirms **`/admin` is the Azure Functions host's reserved management namespace**. **`admin` is a reserved HTTP route prefix in Azure Functions**: function routes under `admin/...` are shadowed by the host and 404. This is why the admin dashboard's 3 fetches (`/api/admin/stats`, `/api/admin/users`, `/api/admin/billing-support-requests`) all fail → generic "Admin request failed".
- (Not a claims/email/ADMIN_EMAILS problem — the frontend `isAdminSession` is fine; the backend never gets reached.)
### Fix 2
- **Rename the C# admin route prefix `admin/*` → `console/*`** in the Functions project (keep `[Function("Admin…")]` names + all logic + `RequireAdminAsync`). Routes become `console/ping`, `console/stats`, `console/users`, `console/users/{userId}`, `console/users/{userId}/credits|suspension|refund`, `console/billing-support-requests`, `console/billing-support-requests/{id}/resolve`, `console/accounting/revenue.csv`, `console/promo-codes`, `console/promo-codes/{id}` (+ disable/enable), etc. (`console` is not reserved.)
- **Update the frontend proxy forward targets** to the new C# path (frontend's own `/api/admin/*` URLs stay unchanged so no other UI changes):
  - `lib/admin-api-proxy.ts` callers / `app/api/admin/**/route.ts`: forward to `/api/console/...` instead of `/api/admin/...`.
  - `lib/admin-promo-proxy.ts` + `app/admin/promo-codes/page.tsx` (server fetch): same `/api/console/...` retarget.
- Files (C#): `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/AdminHttpFunctions.cs` (+ any other file with `Route = "admin/…"`). Files (frontend): the `app/api/admin/**/route.ts` set + `lib/admin-api-proxy.ts` + `lib/admin-promo-proxy.ts` + any server component that fetches `${azure}/api/admin/...`.
- Update backend tests asserting the `admin/` routes to `console/`.

## Delivery
- 2 issues via dynamic-delivery-workflow (integration branch `feat/promo-ux-fix-2` off main):
  1. **FIX-03** — admin route prefix `admin/`→`console/` (C# routes + frontend proxy targets + tests). Touches backend → dotnet-azure deploy.
  2. **FIX-04** — workspace action placement (top-bar `Buy rewrites`; remove output-box buttons; tests). Frontend only.
- Gates: `dotnet test` + `npm run typecheck` + `npm run test` + banned-term grep.
- Deploy (gated, off-main so clean): push `main` → cloudflare-worker (frontend) + dotnet-azure (backend route rename; no migration, no new secrets) → CI build-test gates both deploys → live smoke: `/api/admin/stats` proxy authed works (admin dashboard loads), `/app` shows top-bar actions + clean output. Rollback: `wrangler rollback` (frontend); backend route rename is forward-safe.
- Verify post-deploy: `curl $FUNC/api/console/ping` → 401/403 (no longer 404).
