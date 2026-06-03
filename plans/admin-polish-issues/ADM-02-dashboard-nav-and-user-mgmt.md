TASK: Make the /admin dashboard commercial-grade: add a discoverable link to the Promo codes page, hide already-erased test accounts, add a per-user Delete (erase) action, and clarify status labels. (Frontend + Next proxy only; the C# DELETE endpoint is delivered by a separate backend issue.)

CONTEXT
- Repo: /Users/qc/Desktop/CloudFlare, branch feat/admin-polish (off current main). Spec: plans/admin-polish-plan.md.
- `components/admin/admin-dashboard.tsx` (client) renders the admin dashboard: stat tiles, a billing-support queue, and a **Users** table. It loads via `Promise.all([loadJson("/api/admin/stats"), loadJson("/api/admin/users?…"), loadJson("/api/admin/billing-support-requests")])`. It already has helpers `loadJson<T>(url)` (GET), `postJson<T>(url)` (POST), `formatStatus`, `formatDate`, `userMatchesSearch`, and a `filteredUsers` (currently `users.items.filter(u => userMatchesSearch(u, query))` via useMemo). The Users table columns are: User / Status / Usage / Credits / Cost / Created — there is currently **no Actions column**.
- `AdminUserListItem` (lib/admin-types.ts) has: `id`, `email`, `externalAuthUserId`, `subscriptionStatus`, `usedRewrites`, `reservedRewrites`, `creditRemaining`, `costToDateUsd`, `createdAt`.
- Erased/anonymized accounts have `externalAuthUserId` that **starts with `"erased:"`** and `subscriptionStatus` "Canceled" / null email (these are deleted test accounts cluttering the list). Free users show `subscriptionStatus` like "inactive"/"free" — that is NORMAL, not junk.
- Admin proxy: `lib/admin-api-proxy.ts` exposes `forwardAdminGet(request, path)` and `forwardAdminPost(request, path)` (they build the Azure URL via `azureAdminUrl(path, request)` and attach the Bearer token, returning `forwardAzureAdminResponse(response)`). Next routes live under `app/api/admin/**`; `app/api/admin/users/[userId]/route.ts` currently has only `GET` (→ `forwardAdminGet(request, "/api/console/users/{id}")`).
- The C# side will expose `DELETE /api/console/users/{userId}` (separate backend issue). This issue wires the Next proxy + UI to call it via `DELETE /api/admin/users/{userId}`.

CHANGES REQUIRED
1. **Promo codes nav** (`components/admin/admin-dashboard.tsx`): add a clearly visible link/card near the top of the dashboard (e.g. in or just under the stat-tiles section, or in the page header) labelled **"Promo codes"** linking to `/admin/promo-codes` (Next.js `Link`). It should read like an admin sub-section entry (short caption e.g. "Create & manage redeemable trial codes").
2. **Hide erased accounts** (`admin-dashboard.tsx`): in the `filteredUsers` derivation, ALSO exclude users whose `externalAuthUserId` starts with `"erased:"` by default. Below/above the Users table, show a subtle note when any were hidden: e.g. "N erased account(s) hidden" (`text-xs text-ink/55`). (No toggle required; a static note is fine.)
3. **Delete (erase) action** (`admin-dashboard.tsx`): add an **Actions** column to the Users table with a **Delete** button per row. On click: `window.confirm("Permanently erase this account? This anonymizes the user and removes their data. This cannot be undone.")` → if confirmed, call `DELETE /api/admin/users/{user.id}` (add a `deleteJson`/`requestJson(method)` helper mirroring `postJson` but with `method: "DELETE"`, same 401→/sign-in handling, same error parsing). On success, remove that row from state (or re-fetch the current page). Show a per-row spinner/disabled state while in flight (reuse the `queueActionId` pattern or a new `rowActionId` state). Surface errors inline (don't crash the dashboard). Do NOT render Delete for the signed-in admin's own row is not required (erased rows are already hidden); a confirm is sufficient.
4. **Status clarity** (`admin-dashboard.tsx`): add a one-line legend under the Users table heading: "Inactive / Free = no active subscription (normal). Canceled = account erased." (`text-xs text-ink/55`). Keep the existing `formatStatus` badge.
5. **Next proxy** :
   - `lib/admin-api-proxy.ts`: add `forwardAdminDelete(request, path)` mirroring `forwardAdminPost` but with `method: "DELETE"` and no body (same Bearer + `forwardAzureAdminResponse`).
   - `app/api/admin/users/[userId]/route.ts`: add an exported `DELETE(request, context)` that awaits `context.params`, then `return forwardAdminDelete(request, \`/api/console/users/${encodeURIComponent(userId)}\`)`. Keep the existing `GET` and `export const dynamic = "force-dynamic"`.

ACCEPTANCE (machine-checkable + behavioral)
- The dashboard renders a "Promo codes" link to `/admin/promo-codes`.
- `filteredUsers` excludes `externalAuthUserId.startsWith("erased:")`; a hidden-count note renders when applicable.
- The Users table has an Actions column with a Delete button that calls `DELETE /api/admin/users/{id}` behind a confirm; `lib/admin-api-proxy.ts` exports `forwardAdminDelete`; `app/api/admin/users/[userId]/route.ts` exports `DELETE` forwarding to `/api/console/users/{id}`.
- A status legend clarifying Inactive/Free vs Canceled is present.
- `npm run typecheck` clean; `npm run test` green; banned-term grep over app components public lib clean. If a unit/contract test asserts the old Users-table column count or copy, update it to match.

DO NOT
- No C# change in this issue (the DELETE endpoint is a separate backend issue; the proxy forwarding to it is correct even before it deploys). Do not change `lib/admin-types.ts` shapes, the GET proxies, or auth. No new secrets. No banned terms. Do not push/PR/merge/deploy — the driver handles git.
