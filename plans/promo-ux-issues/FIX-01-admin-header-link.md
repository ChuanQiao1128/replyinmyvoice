TASK: Add an Admin entry link to the site header, visible only to admins.

CONTEXT
- Repo: /Users/qc/Desktop/CloudFlare, branch feat/promo-ux-fix (off current main). Spec: plans/promo-ux-fix-plan.md.
- An admin console already exists at /admin (app/admin/page.tsx), gated server-side by isAdminSession(session) from lib/admin-auth. The header (components/site-header.tsx) has NO link to it, so admins can't reach it.
- components/site-header.tsx is an async server component that already does `const session = await getCurrentSession()` and conditionally renders signed-in vs signed-out nav links.

CHANGES REQUIRED
1. components/site-header.tsx: import { isAdminSession } from "../lib/admin-auth". In the signed-in branch of `.nav-links` (where "Sign out" / "Open app" render), additionally render `<Link href="/admin">Admin</Link>` (place it before "Sign out") ONLY when `isAdminSession(session)` is true.
   - Match the existing link styling/markup conventions in this file.

ACCEPTANCE (machine-checkable)
- A signed-in admin (email/oid in ADMIN_EMAILS) sees an "Admin" nav link pointing to /admin.
- A signed-in NON-admin does NOT see it; a signed-out visitor does NOT see it.
- `npm run typecheck` and `npm run test` pass; banned-term grep clean.
- Add/extend a unit test if the repo has site-header tests; otherwise keep it minimal.

DO NOT
- No /api/me or backend change (reuse isAdminSession). No banned terms (humanizer|bypass|undetect|detector|evade). No secrets in source. Never push / open PR / merge / deploy — the driver handles git.
