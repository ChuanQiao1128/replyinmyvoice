# Site Overhaul — Requirement & Acceptance Spec

Status: **DRAFT for owner sign-off** · Branch: `feat/site-overhaul` · Date: 2026-05-30
Owner: ChuanQiao1128 (TimeAwake Ltd) · Supervisor: Claude Code · Worker: Codex (MCP) via `delivery-pipeline`

This document is the **requirement source** that the `delivery-pipeline` skill decomposes into GitHub issues.
Each issue carries its own scope + machine-checkable acceptance criteria; this file is the contract behind them.

---

## 0. Architecture reality (current prod)

- **Frontend:** Next.js 15 App Router on Cloudflare Workers/OpenNext — Worker `replyinmyvoice-app`. Thin UI; proxies to backend.
- **Backend:** C#/.NET 8 **Azure Functions** (`replyinmyvoice-func-dev`) + **Azure SQL** via EF Core. Auth validation, quota, billing, rewrite engine.
- **Auth:** Microsoft Entra External ID (CIAM); Google via Entra↔Google federation; user keyed on the `oid` claim. Clerk is removed (only dead residue remains).
- **Payments:** Stripe **LIVE** mode; credit-pack model (Quick 10 / Value 30 / Pro·API 90; hidden Focus pack). **Never exercised with a real purchase.**
- **Deploy:** push/merge to `main` ⇒ Cloudflare `cf:deploy` (frontend) **and** `dotnet ef database update` on **live Azure SQL**. Treat `main` as production.

`AGENTS.md` predates this stack in places (it still describes Clerk + Prisma/Neon). Where `AGENTS.md` conflicts with the above, the above wins for implementation; product rules (quota semantics, banned terms, secrets, positioning) in `AGENTS.md` still apply.

## 1. Global constraints (apply to every issue)

- **Banned terms** (CI grep over `app components public lib`, and by policy also internal names): `humanizer | bypass | undetect | detector | evade`. No user-facing OR internal-code occurrences. Run `grep -RniE "humanizer|bypass|undetect|detector|evade" app components public lib || true` before completion.
- **Secrets:** never print/commit/log values from `.env.local`, `.dev.vars`, `globalapikey/`. Validate env at runtime in the handler, not at import.
- **Positioning:** "replies that still sound like you" — natural / personal / fact-preserving. Never AI-detection-related claims.
- **No live money from automation:** automation must never initiate a real Stripe **charge**. Refund *issuance* is owner-confirmed only (see WS-2 / WS-4). The first real purchase and first real refund are **manual owner checkpoints**.
- **Prod safety:** no PR merges to `main` automatically. Work lands on an integration branch; the single `→ main` PR is human-approved (it deploys prod + migrates live SQL).

### Per-PR quality gates (delivery-pipeline enforces on every PR)
- Frontend: `npm run typecheck` · `npm run test` · `npm run build` (and `npm run cf:build` for Worker-affecting changes) · banned-term grep clean.
- Backend: `dotnet build` · `dotnet test` (xUnit) for the touched projects.
- Scope: only files in the issue's declared `scope:` list may change.

## 2. Locked product decisions (owner, 2026-05-30)

1. **History = server-side + controls.** Surface the already-persisted rewrites as a managed "My rewrites" list (view / delete, cross-device); add retention TTL + consent; reconcile Privacy/Terms copy with reality.
2. **Refunds = clawback + in-app refund.** Webhook handles refund/dispute → revoke credits + reconcile ledger; owner can issue refunds from the admin backoffice (confirmed + audit-logged).
3. **Admin = dashboard + actions.** View users / usage / payments / credits / costs AND act: issue refunds, grant/adjust credits, suspend users. Customer rewrite-content view OFF by default (privacy-gated, separate opt-in).
4. **Redesign = evolve every page.** Consolidate one design system; every page gets a polish/consistency/a11y pass via the web-design skill; new pages (account/history, admin, 404/error) built to it; refine — not discard — the landing.
5. **Per-call cost tracking (required).** Every customer rewrite call records its computed cost — each provider call's model + input/output tokens + estimated cost (from configured rates) — attributed per user + attempt, aggregated per attempt. Surfaced in the admin per-customer cost view. `SO-014` (delete-account) and `SO-045` (cost) are both **kept** (no longer optional).

---

## 3. Workstreams (scope · goal · acceptance)

### WS-0 — Foundation cleanup (no behavior change)
**Goal:** remove dead code/config and close a security gap so later work is clean.
- **SO-001** Remove dead Clerk/Prisma residue in active code+config (`ADMIN_CLERK_USER_IDS`, stale Clerk strings, references to the dead generated Prisma client where unused). **Do NOT delete vars from `wrangler.jsonc` in this issue** — trimming Worker vars has 500'd prod static pages before despite green local gates; leave the harmless unused var, or change it only with a preview-deploy verification.
  *AC:* `grep -ri clerk app lib components` = 0 (migration history excepted); `npm run build` + `npm run typecheck` green; no `wrangler.jsonc` var deletions.
- **SO-002** Harden the `ALLOW_HEADER_AUTH` identity-override path so it is impossible in production.
  *Scope:* `backend-dotnet/src/ReplyInMyVoice.Functions/Auth/FunctionAuthResolver.cs`, `backend-dotnet/src/ReplyInMyVoice.Api/Program.cs`.
  *AC:* new xUnit test — when environment is Production, header identity is ignored even if `ALLOW_HEADER_AUTH=true`; existing tests pass.
- **SO-003** Strip dead Clerk JWT code from the legacy `ReplyInMyVoice.Api` host (keep it as the EF migrations startup project, Entra-only).
  *AC:* `grep -ri clerk backend-dotnet/src/ReplyInMyVoice.Api` = 0; `dotnet build` + `dotnet test` green.

### WS-1 — Auth & account (注册登录)
**Goal:** the login surface tells the truth, dead auth code is gone, and users have a real account surface.
- **SO-010** Align sign-in/sign-up copy with the actual Entra OAuth-redirect flow (remove the "email code / one-time code" implication that points at the unused native path).
  *Scope:* `components/auth/google-oauth-card.tsx`, `app/sign-in/**`, `app/sign-up/**`.
  *AC:* copy grep shows no "one-time code"/"email code" claim; Playwright `sign-in → /app` smoke exits 0; contract tests updated.
- **SO-011** Remove the unwired native email+password subsystem (`lib/entra-native-auth.ts`, `app/api/auth/password`, `app/api/auth/signup/*`) — live flow is OAuth-redirect-only.
  *AC:* routes return 404; `grep -r entra-native-auth app lib` = 0; build green. (If password login is wanted later, it's a separate build.)
- **SO-012** Account surface (email, sign-out, "Manage billing", delete-account button). **This is UI → built in Wave 2** to the design system; its backend (delete-account) is SO-014, and `/api/me` already returns email.
- **SO-013** Enforce same-origin on `/api/stripe/checkout`, `/api/stripe/portal`, and review `/api/auth/access-token` exposure.
  *AC:* route test — cross-origin POST rejected; same-origin allowed.
- **SO-014** Delete-account / erasure **backend** flow: cancel any Stripe subscription, erase or anonymize the `AppUser` + children (attempts, periods, reservations, credits), invalidate the session; idempotent endpoint. **Kept (not optional).**
  *Scope:* backend account/erasure endpoint + service; `AppUser` cascade. *(Confirm UI is a Wave-2 task; pairs with the retention/consent posture.)*
  *AC:* test — delete cancels the sub, erases/anonymizes the user + children, is idempotent; the account can no longer authenticate as live.

### WS-2 — Billing, refund & receipts (付款退款)
**Goal:** refunds and chargebacks reconcile automatically, the owner can issue refunds, customer/admin can see what was bought.
> **Design note (verified from code):** `RewriteCredit` stores only `StripeEventId` (the checkout-session event) — **no PaymentIntent/Charge/SKU/amount** — so a `charge.refunded` event can't be mapped back to a grant today. One data enrichment unblocks refund clawback **and** purchase history **and** the admin payments view. WS-2 therefore starts with that enrichment.
- **SO-020** Enrich the credit grant: capture `StripePaymentIntentId` (+ charge id, sku, amount, currency, receipt URL when available) on `RewriteCredit` at grant time in `SyncCheckoutSessionAsync`; add a unique (filtered, non-null) constraint on `StripeEventId`; migration.
  *Scope:* `backend-dotnet/.../Entities/RewriteCredit.cs`, `.../Services/StripeEventService.cs`, `.../Data/AppDbContext.cs`, migration.
  *AC:* xUnit — a paid checkout writes the new fields; a duplicate-event grant is rejected by the unique index; migration applies on SQLite + SqlServer.
- **SO-021** Refund + dispute handler: webhook handles `charge.refunded` and `charge.dispute.created/closed` → look up the grant by PaymentIntent → revoke **unconsumed** credits (clamp so `AmountGranted ≥ AmountConsumed`); idempotent by event id. Depends on SO-020.
  *Scope:* `.../Services/StripeEventService.cs` (+ `RefundService`).
  *AC:* xUnit — refund of a paid pack revokes remaining credits; already-consumed not driven below 0; dispute path; replay is a no-op.
- **SO-022** Runtime assertion: in non-Testing environments the Stripe webhook must refuse unsigned requests when `STRIPE_WEBHOOK_SECRET` is absent (no silent unsigned-accept in prod).
  *AC:* test — Production env + missing secret ⇒ webhook refuses (no credit grant).
- **SO-023** Purchase-history **API** (derive from the enriched grants), caller-scoped. *(In-app receipts UI + pack-buyer portal access is a Wave-2 UI task.)* Depends on SO-020.
  *Scope:* backend payments-list endpoint.
  *AC:* `GET /api/me/payments` returns only the caller's purchases (amount/date/sku/expiry); a cross-user request ⇒ empty.

### WS-3 — Customer history & usage (历史记录 + 次数)
**Goal:** customers manage their own rewrite history server-side, see their usage by source, and the privacy story is honest.
- **SO-030** Backend "My rewrites": list (paginated, caller-scoped), detail, soft-delete. Reuse `RewriteAttempt`; add `Title?`/`DeletedAt` as needed.
  *Scope:* `backend-dotnet/.../Functions/RewriteHttpFunctions.cs` (+ service), entity/migration.
  *AC:* `GET /api/me/rewrites` returns only the caller's rows (cross-user request ⇒ empty/403); soft-delete hides a row; pagination test.
- **SO-031** Retention: TTL job that scrubs raw `RequestJson`/`ResultJson` after a configured window; consent flag captured at first use.
  *Scope:* new timer function, entity field, settings.
  *AC:* test — attempt older than TTL has raw content nulled while the row/metadata remains; consent persisted.
- **SO-032** "My rewrites" UI — server-backed history (replaces/augments the 5-item localStorage list), with view + delete.
  *Scope:* `components/app/rewrite-workspace.tsx` (+ new component).
  *AC:* Playwright — list shows server rewrites, view opens one, delete removes it; works after reload (server-backed).
- **SO-033** Reconcile Privacy/Terms + workspace copy with actual retention; add the consent line.
  *Scope:* `app/privacy/**`, `app/terms/**`, workspace copy.
  *AC:* copy grep — no "not saved to the database" claim about rewrite content; contract tests updated.
- **SO-034** Per-source usage breakdown (free vs each pack) in `/api/me` + UI (the `quotaSources` the UI currently hardcodes to `[]`).
  *AC:* `/api/me` returns sources array; UI renders the breakdown; test.

### WS-4 — Admin backoffice (管理员后台)
**Goal:** an owner-only backoffice to see and run the business.
- **SO-040** Admin gate: compare the caller's `oid`/email against `ADMIN_EMAILS` in the backend; reusable `RequireAdmin`.
  *AC:* test — non-admin `oid` ⇒ 403 on every `/admin*` endpoint; admin ⇒ 200.
- **SO-041** Admin read endpoints: users list, user detail (usage/credits/payments/subscription **+ cost-to-date** from SO-045), aggregate stats.
  *AC:* endpoint tests; admin-only; pagination on the users list; user detail returns per-user cost total.
- **SO-042** Admin action — **issue refund** via Stripe API (owner-confirmed, audit-logged); the refund then flows through SO-021 to revoke credits. Depends on SO-040, SO-021.
  *Scope:* backend admin function + audit-log entity.
  *AC:* test — refund call hits Stripe (faked) + writes an audit row; idempotent.
- **SO-047** Admin action — **grant / adjust credits** (manual `RewriteCredit` with `Source="ADMIN"`, audit-logged). Depends on SO-040.
  *AC:* test — adjust changes the user's balance; audit row written.
- **SO-048** **User suspension** — add `SuspendedAt`/`IsSuspended` to `AppUser` (+ migration), **enforce in the rewrite request path** (suspended ⇒ rejected), and an admin toggle. Depends on SO-040.
  *Scope:* `.../Entities/AppUser.cs`, `QuotaService`/`RewriteRequestService`, admin endpoint, migration.
  *AC:* test — a suspended user's rewrite is rejected; unsuspend restores access.
- **SO-045** **Per-call cost tracking (required).** Instrument the rewrite pipeline so every attempt records each provider call (`RewriteProviderCall`: model, input/output tokens, latency, est. cost from configured rates) and an aggregate (`RewriteCostLog`: total cost, scenario, signal %s), attributed to user + attempt. Provider rates are config (no secrets). This powers per-customer cost in admin (SO-041).
  *Scope:* provider wrapper(s) (capture `usage` from DeepSeek/OpenAI-compatible + Sapling), `RewriteJobProcessor`/`RewriteRequestService`, the two entities, migration if fields change.
  *AC:* xUnit — a completed rewrite writes one `RewriteCostLog` row + one `RewriteProviderCall` per provider call, with non-zero tokens and a computed cost, linked to the attempt's `UserId`.
- **SO-044** `/admin` UI — dashboard + user detail + actions, built to the design system; non-admin redirected. **UI → Wave 2.**
  *Scope:* `app/admin/**`, `components/admin/**`.
  *AC:* Playwright — non-admin redirected; admin sees users + can trigger an action (stubbed in test).

### WS-5 — Design system & page redesign (evolve every page)
**Goal:** one coherent, accessible design system applied to every page; the thin/missing pages brought up to par.
- **SO-050** Consolidate the design system — extract tokens + primitives from the 1,900-line `globals.css` into a documented system; write a short design-system reference. Use the **web-design-engineer** skill.
  *AC:* `npm run build` green; landing visual smoke unchanged (no regression); design-system doc exists.
- **SO-051** Redesign the **app workspace** to the system (bring it up to landing-level polish).
  *AC:* contract tests updated; Playwright + axe (WCAG AA) pass on `/app`.
- **SO-052** Build **account/history** and **admin** pages to the system (depends on WS-3/WS-4).
  *AC:* render + axe pass.
- **SO-053** Add **404** (`not-found.tsx`) and **error** (`error.tsx`) pages.
  *AC:* unknown route renders branded 404; thrown error renders branded boundary.
- **SO-054** Polish/consistency + a11y pass on landing / pricing / developers / privacy / terms / auth (evolve, not rebrand).
  *AC:* axe AA per page; contract tests pass; visual smoke.
- **SO-055** (optional) Dark mode. *(Defer unless wanted.)*

### WS-6 — End-to-end verification & launch gates
**Goal:** prove the whole site works before the human `→ main` merge.
- **SO-060** Playwright e2e per flow: auth (sign-in→app→sign-out), checkout→quota (test mode), refund→clawback (test), history (list/view/delete), admin (gated views + an action).
  *AC:* suite exits 0 in CI.
- **SO-061** Full gate run doc + script: banned-term scan, typecheck, `npm run test`, `dotnet test`, `npm run build`, `cf:build`.
  *AC:* all green; documented.
- **SO-062** Owner runbook for the **manual** checkpoints: one real purchase + one real refund on live Stripe (owner-run, not automated).
  *AC:* runbook exists; checkpoints marked owner-only.

---

## 4. Sequencing & waves

**Principle (revised 2026-05-30 after a Wave-1 reasonableness review):** Wave 1 is **backend + data-model + API + security + pure-text** only — *no new rendered pages*. Every new UI surface is built once in Wave 2, after the design system exists, so nothing is styled twice. Backend correctness is proven by xUnit/HTTP tests; the owner doesn't need a screen to verify a Wave-1 issue. Where an issue spans backend+UI (SO-012 account, SO-023 receipts, SO-032 history, SO-034 usage breakdown), the backend/API ships in Wave 1 and the rendered UI ships in Wave 2. Per-issue verification commands are scoped to the layer touched (`dotnet test` for backend issues, `npm` gates for frontend).

**Wave 1 — backend correctness, security & honesty (no new UI; ~19 issues):**
```
WS-0  SO-001  SO-002  SO-003                          (no deps)
WS-1  SO-010  SO-011  SO-013  SO-014                  (after WS-0)
WS-2  SO-020 → SO-021 ;  SO-022 ;  SO-023(API)        (SO-021/023 after SO-020)
WS-3  SO-030 ;  SO-031 ;  SO-033 ;  SO-034(backend)   (after WS-0)
WS-4  SO-040 → SO-041 ;  SO-042 ;  SO-047 ;  SO-048 ;  SO-045
                                                      (SO-041 after SO-045+SO-020; SO-042 after SO-021+SO-040)
```

**Wave 2 — design system + all UI + verification:**
```
SO-050 (design system FIRST)
  → SO-051 workspace · SO-012 account UI · SO-032 history UI · SO-034 usage-breakdown UI
    · SO-023 receipts UI + pack-buyer portal · SO-044 admin UI
  → SO-053 404/error · SO-054 polish+a11y (landing/pricing/developers/legal/auth) · SO-055 dark mode(optional)
  → WS-6: SO-060 e2e · SO-061 gate run · SO-062 owner runbook
```

Each wave has its own `delivery-pipeline` checkpoint — you approve the issue list before any Codex work runs.

## 5. Out of scope (this overhaul)
- Building the B2B API (`/api/v1/*`, API keys) — `/developers` copy will be reconciled to not over-promise, but the API itself is a separate effort.
- Full deletion of the dead Prisma/Neon generated client (large; tracked separately as Slice-7).
- Rewrite-engine quality changes (separate track).
- Any DNS / domain / `LAUNCH_CONFIRMED` changes.

## 6. Manual owner checkpoints (cannot be automated)
- First real Stripe purchase (validates the live checkout→webhook→credit grant path).
- First real refund (validates the clawback path end-to-end).
- The final `delivery/integration → main` PR review + merge (deploys prod + migrates live SQL).
