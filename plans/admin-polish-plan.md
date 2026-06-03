# Admin polish — promo create defaults, discoverability, user management

> Commercial-grade fixes after the owner reviewed the live admin area. Branch off CURRENT main.
> Delivered via the Dynamic Delivery Workflow; self-reviewed post-deploy.

- **Branch:** `feat/admin-polish` (off `main`).
- **Date:** 2026-06-03.

## Confirmed diagnoses (from live + code)
- **Promo "Pending" + wrong dates** = `components/admin/promo-codes-admin.tsx` `initialFormValues()` defaults `validFrom = now + 5 minutes` (→ status Pending for the first 5 min) and `validUntil = now + 30 days` (too short; not the campaign window). A freshly created code should be **Active immediately**.
- **No discoverability**: `/admin` (`components/admin/admin-dashboard.tsx`) and the header have **no link** to `/admin/promo-codes`. The owner had to type the URL.
- **No user deletion**: admin user endpoints are GET users, GET users/{id}, POST users/{id}/credits|suspension|refund — **no delete/erase**. The user list also shows already-`erased:` test accounts (noise) and labels free users "Inactive" without explanation.

## Fixes

### A. Promo create form (frontend: promo-codes-admin.tsx, lib/admin-promo-codes.ts)
- `initialFormValues()`: `validFrom = now` (immediately Active — remove the +5 min). `validUntil = now + 90 days` (generous, sensible; admin can set the exact campaign end e.g. 2026-08-31).
- Add a small **status legend** near the codes list / stats: Active = redeemable now · Pending = not yet (valid-from in the future) · Expired = past valid-until · Exhausted = global cap reached · Disabled = turned off by admin.
- (Optional polish) a "copy code" affordance on each code row.

### B. Discoverability (frontend: admin-dashboard.tsx; site-header.tsx optional)
- Add a clear **"Promo codes" link/card** on the `/admin` dashboard → `/admin/promo-codes`.
- Add a back-to-`/admin` link on the promo-codes page header.

### C. User management (backend + frontend)
- **Backend**: add an admin endpoint `DELETE console/users/{userId}` (and the Next proxy `app/api/admin/users/[userId]` DELETE → `/api/console/users/{id}`). It erases the account via the existing `AccountService.DeleteAccountAsync` (anonymize, reuse the proven path) keyed by the AppUser id; writes `AdminAuditLog`. Guard: refuse to delete an admin (oid/email in ADMIN_EMAILS) and refuse self-delete.
- **Frontend** (`admin-dashboard.tsx`): 
  - **Hide already-erased accounts** (externalAuthUserId starts with `erased:`) from the list by default; show a small "N erased accounts hidden" note (optional toggle to show).
  - Add a per-user **Delete** action (with a confirm) → calls the DELETE endpoint → row removed on success.
  - Clarify status: keep the badge but add a one-line legend ("Inactive = free / no active subscription; Canceled = erased"). Don't imply Inactive is a problem.

## Out of scope / unchanged
- No schema/migration (DeleteAccountAsync already exists). No new secrets. The existing PATCH `console/promo-codes/{id}` (edit) can be surfaced later; not required for these asks.

## Delivery
- 3 issues via dynamic-delivery-workflow (integration branch `feat/admin-polish` off main):
  1. **ADM-01** — promo create defaults (Active-now + 90d) + status legend (frontend).
  2. **ADM-02** — `/admin` → Promo codes nav link + back link (frontend).
  3. **ADM-03** — admin delete-user endpoint + proxy + dashboard delete action + hide erased + status legend (backend + frontend).
- Gates: `dotnet test` + `npm run typecheck` + `npm run test` + banned-term grep.
- Deploy: push `main` → cloudflare-worker (frontend) + dotnet-azure (ADM-03 backend) — both gated by build-test; no migration/secrets/workflow files. ff-safe (off main).
- **Self-review post-deploy**: verify (a) a new code is Active (not Pending) with ~90d expiry, (b) `/admin` links to promo, (c) delete removes a test user + erased accounts are hidden, (d) `/api/console/users` still 200 for admin.
