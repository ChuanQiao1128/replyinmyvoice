# PAY-07: /admin UI — users list, user detail, refund / credit-adjust / suspend (SO-044)

**Priority:** P1 · **Owner:** Codex · **Skill:** ui-browser-testing · **Depends on:** none (backend complete)

## Context
- Repo root: `/Users/qc/Desktop/CloudFlare`
- The admin **backend is complete** but there is **no UI** (SO-044 deferred to Wave-2, never issued). Endpoints (all admin-gated via `AdminAccess.RequireAdminAsync`, `ADMIN_EMAILS`):
  - `GET admin/users` (paginated), `GET admin/users/{id}` (detail: subscription + credit ledger + payments + cost-to-date), `GET admin/stats`
  - `POST admin/users/{id}/credits` (grant/adjust), `POST admin/users/{id}/suspension`, `POST admin/users/{id}/refund`
  - Source: `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/AdminHttpFunctions.cs`, `AdminService.cs`.
- Frontend proxy pattern to follow: `app/api/stripe/checkout/route.ts` (same-origin + Entra bearer → Azure). Auth helpers: `getCurrentAccessToken`, `requireSameOrigin`.
- **Why this is in the launch set:** the owner needs a UI to issue the test refund in PAY-11 — today refunds require hand-crafting an authenticated API call.

## Constraints (AGENTS.md)
- Banned terms: `humanizer|bypass|undetect|detector|evade` — none in routes/labels/UI copy.
- Same-origin + bearer on every admin proxy route; non-admin must get 403 / redirect.
- No secrets in source; validate at runtime.
- Do NOT push to `main`.

## Changes required
1. Add `app/admin/` pages (App Router): users list (paginated, search), user detail (subscription status, credit ledger, payments incl. receiptUrl if PAY-05 landed, cost-to-date), and aggregate stats.
2. Add same-origin + bearer proxy routes under `app/api/admin/*` forwarding to the Azure admin endpoints (mirror `app/api/stripe/checkout/route.ts`).
3. Actions with confirm dialogs: **issue refund** (amount + reason), **grant/adjust credits** (amount + reason), **suspend / unsuspend**. Show the resulting `AdminAuditLog` outcome.
4. Admin gate on the page (non-admin → redirect or 403 view). Reuse the existing admin-email gate semantics.

## Acceptance (machine-checkable)
- [ ] `/admin` renders for an admin user and lists users; non-admin is denied (test or Playwright assertion).
- [ ] Refund / credit-adjust / suspend actions call the real admin API and reflect success/failure.
- [ ] Playwright smoke (`tests/e2e/admin.spec.ts`): admin can open a user detail; non-admin is blocked.
- [ ] `npm run build` + `npm run test` green; banned-term grep clean.

## Do NOT
- Do NOT add new billing logic in the frontend — proxy to the existing C# endpoints only.
- Do NOT initiate real charges/refunds in tests (mock the admin API or use test mode).
- Do NOT touch `main` / deploy.
