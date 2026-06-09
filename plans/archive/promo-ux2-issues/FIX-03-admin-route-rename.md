TASK: Fix /admin "Admin request failed" — the C# admin functions use the RESERVED Azure Functions route prefix `admin/`, so every /api/admin/* returns 404. Rename the prefix to `console/` and retarget the frontend proxies.

CONTEXT
- Repo: /Users/qc/Desktop/CloudFlare, branch feat/promo-ux-fix-2 (off current main). Spec: plans/promo-ux-fix-2-plan.md (read it).
- PROVEN root cause: admin functions are deployed/registered (az functionapp function list shows AdminPing/AdminStats/AdminUsersList/AdminPromoCodes*…, bindings Anonymous GET route `admin/ping` etc.) but ALL `/api/admin/*` return 404, while `/api/me`, `/api/me/payments`, `/api/health/ready`, `/api/promo/redeem` work. `/admin/host/status` → 401 (the Functions host reserves `/admin` for its management API). `admin` is a reserved route prefix in Azure Functions → those routes are shadowed/404. NOT a claims/ADMIN_EMAILS issue.

CHANGES REQUIRED
1. Backend C#: in `backend-dotnet/src/ReplyInMyVoice.Functions/` rename EVERY HttpTrigger `Route = "admin/…"` to `Route = "console/…"` (grep: `Route = "admin/`; there are ~16 in AdminHttpFunctions.cs — also grep the whole Functions project to be safe). Keep `[Function("Admin…")]` names, all handler logic, and `RequireAdminAsync` exactly as-is. Routes become e.g. `console/ping`, `console/stats`, `console/users`, `console/users/{userId}`, `console/users/{userId}/credits|suspension|refund`, `console/billing-support-requests`, `console/billing-support-requests/{requestId}/resolve`, `console/accounting/revenue.csv`, `console/promo-codes`, `console/promo-codes/{promoCodeId}` (+ /disable, /enable). (`console` is not reserved.)
2. Frontend proxy targets — change ONLY the forwarded Azure path from `/api/admin/...` to `/api/console/...`; keep the Next.js route file paths (`app/api/admin/**`) and the admin UI URLs unchanged:
   - `lib/admin-api-proxy.ts` (and every `app/api/admin/**/route.ts` that calls `forwardAdminGet/Post(request, "/api/admin/…")`) → `/api/console/…`.
   - `lib/admin-promo-proxy.ts` and `app/admin/promo-codes/page.tsx` server fetch (`${getAzureApiBaseUrl()}/api/admin/...`) → `/api/console/...`.
3. Update backend tests that assert the `admin/` routes (WebApplicationFactory/route tests) to `console/`.

ACCEPTANCE (machine-checkable)
- `grep -rE 'Route = "admin/' backend-dotnet/src` → 0 matches; admin functions now use `console/`.
- No remaining `${getAzureApiBaseUrl()}.../api/admin/` forwards in lib/app (all `/api/console/`). Frontend admin URLs (app/api/admin/*) unchanged.
- `dotnet test backend-dotnet/ReplyInMyVoice.sln` green; `npm run typecheck` + `npm run test` green; banned-term grep clean.
- (Post-deploy, manual) `curl $FUNC/api/console/ping` returns 401/403, not 404.

DO NOT
- Do not change handler logic, RequireAdminAsync, function names, or ADMIN_EMAILS. No new secrets/migrations. No banned terms. Never push/PR/merge/deploy — the driver handles git.
