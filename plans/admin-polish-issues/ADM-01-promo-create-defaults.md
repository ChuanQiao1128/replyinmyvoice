TASK: Fix the promo-code create form so a freshly created code is ACTIVE immediately (not "Pending") with a sensible long expiry, add a status legend, and add a back-to-Admin link.

CONTEXT
- Repo: /Users/qc/Desktop/CloudFlare, branch feat/admin-polish (off current main). Spec: plans/admin-polish-plan.md.
- The admin promo UI is `components/admin/promo-codes-admin.tsx` (client component), rendered by the server page `app/admin/promo-codes/page.tsx`. Status is derived by `derivePromoCodeStatus` in `lib/admin-promo-codes.ts` (returns "pending" when `now < validFrom`, "expired" when `now > validUntil`, else "active"/"exhausted"/"disabled").
- ROOT CAUSE of the owner's "Pending" code: `initialFormValues()` in `components/admin/promo-codes-admin.tsx` (~line 66) defaults `validFrom` to **now + 5 minutes** (`inFiveMinutes`), so a just-created code is future-dated → status "Pending" (NOT redeemable) for the first 5 minutes. It also defaults `validUntil` to only **+30 days** (too short for a campaign that should run to ~end of Aug 2026).
- `toLocalDateTimeInput(date)` and `addDays(date, n)` helpers already exist in that file. `statusClasses` already includes a `pending` style.

CHANGES REQUIRED (frontend only)
1. `components/admin/promo-codes-admin.tsx` → `initialFormValues()`:
   - `validFrom`: default to **now** (the moment the form loads), NOT now+5min. Remove the `inFiveMinutes` future offset so a created code is Active immediately. (Using `new Date()` is correct; do NOT add minutes. By submit time `now >= validFrom` so `derivePromoCodeStatus` returns "active".)
   - `validUntil`: default to **now + 90 days** (generous, sensible). Replace the current `addDays(inFiveMinutes, 30)`. Keep using `toLocalDateTimeInput` + `addDays`.
   - Keep all other defaults (code "", credits "3", globalCap "1000", perUserCap "1", ttlDays "90", displayCode "").
2. Add a small, always-visible **status legend** near the codes list (a short caption row), explaining each `AdminPromoStatus`:
   - Active = redeemable now · Pending = not yet active (valid-from is in the future) · Expired = past valid-until · Exhausted = global cap reached · Disabled = turned off by an admin.
   - Style it subtly (e.g. `text-xs text-ink/55`), consistent with the page. Reuse the existing `statusLabel`/`statusClasses` if helpful.
3. Add a **"← Back to Admin"** link in the promo page header → `/admin` (put it in `app/admin/promo-codes/page.tsx` header area, or at the top of the `promo-codes-admin.tsx` render). Use Next.js `Link`. Make it clearly visible above the create form / page title.

ACCEPTANCE (machine-checkable + behavioral)
- `grep -n "inFiveMinutes" components/admin/promo-codes-admin.tsx` → 0 matches (the future-offset default is gone).
- `initialFormValues()` sets `validFrom` to the current time and `validUntil` to ~90 days out; a code created with the unedited defaults derives status "active" (verify the logic against `derivePromoCodeStatus`).
- A status legend with all five statuses is rendered on the page; a back-to-Admin link to `/admin` is present.
- `npm run typecheck` clean; `npm run test` green; banned-term grep (humanizer|bypass|undetect|detector|evade) over app components public lib clean.

DO NOT
- No backend change. Do not change `derivePromoCodeStatus` semantics, the create payload contract, `validatePromoCreateForm`, or `lib/admin-promo-codes.ts` types. Do not touch `app/api/**` or any C#. No banned terms. Do not push/PR/merge/deploy — the driver handles git.
