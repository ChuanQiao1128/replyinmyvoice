# Frontend Commercial Redesign — Master Plan

Status: **PLAN ONLY — not started, awaiting owner GO** · Date: 2026-06-10
Owner: ChuanQiao1128 (TimeAwake Ltd) · Supervisor: Claude Code · Workers: Claude Sonnet headless (Codex quota exhausted)
This file is the single source of truth. It folds in the former `REQUIREMENT.md`, `APP-SHELL.md`, and `EXECUTION-PLAN.md`.

**Locked decisions (owner, 2026-06-10):** R4 → **full App Shell, no Phase 0**. Amendments **R1, R2, R3, R5, R6 adopted** and folded into the specs below (R1 = supervisor hand-builds the shell frame FE-S1 in-session with a visual checkpoint before workers fan out; R2 = progressive-disclosure sidebar; R3 = mobile drawer; R5 = shared-pool quota label; R6 = FE-S1 solo canary). See §8 (execution model), §6.3/§6.8 (shell), §11–§12 (locked).

Goal in one line: take every page from "works" to **commercial-grade** — coherent post-login journeys, no dead ends, honest copy, professional failure states — while *evolving* (not rebuilding) the pages redesigned on 2026-06-04. The headline problem the owner named is the **signed-in experience** (§6).

---

## 1. Context & architecture reality

- Frontend: Next.js 15 App Router on Cloudflare Workers (Worker `replyinmyvoice-app`); thin UI proxying to a C#/.NET 8 Azure Functions backend + Azure SQL. Push/merge to `main` deploys prod (frontend `cf:deploy` + live EF migration).
- Auth: Entra External ID (CIAM), Google via federation, user keyed on `oid`. Clerk/Prisma are dead residue.
- Pricing (live): trial via **redeem code** (3 rewrites, `FREE_BASELINE_REWRITES=0`) · Quick NZ$2.50·10 · Value NZ$6.90·30 · Pro/API NZ$19.90/mo·90. Web + API share **one key, one quota, one balance**. Stripe LIVE; no real purchase exercised yet.
- Visual north star: **Warm Writing Desk** — off-white paper, dark readable text, soft borders, restrained warm accents; no futuristic-AI styling.
- Recently SHIPPED (do **not** rebuild, only re-frame/gap-fill): `/pricing` (PR #502), `/app` workspace internals (PR #488), `/admin/promo-codes` (PR #472). MCP shipped too — npm `@replyinmyvoiceashuman` v0.1.2 + a 406-line guide at `/developers/mcp` (#595–598).

## 2. Global constraints (every issue)

- **Banned terms** (CI grep, diff-scoped in pipeline): `humanizer | bypass | undetect | detector | evade`. No user-facing OR internal-code occurrences.
- **Secrets:** never print/commit/log values from `.env.local`, `.dev.vars`, `globalapikey/`. Validate env at runtime in the handler.
- **Positioning:** "replies that still sound like you" — natural / personal / fact-preserving. Never AI-detection claims.
- **Prod safety:** no auto-merge to `main`. Work lands on `delivery/fe-redesign`; the single `→ main` PR is human-approved. Rollback = `wrangler rollback` (frontend-only wave, no DB migrations). No `wrangler.jsonc` var changes without a preview deploy (CF config trim has 500'd prod before).
- **No live money from automation.** First real purchase/refund stay manual owner checkpoints.

## 3. Audit findings (verified in code 2026-06-10)

| # | Finding | Evidence |
|---|---------|----------|
| F1 | **Legal copy contradicts the live product**: `/terms` says "Free access includes 3 successful lifetime rewrites"; live model is trial-by-code, zero free baseline | `app/terms/page.tsx:26` |
| F2 | **Buy intent lost at the auth wall**: signed-out Buy → 401 → `/sign-in?redirectTo=/pricing`; SKU not carried; user must re-click after auth | `components/landing/buy-button.tsx` |
| F3 | **Landing CTAs ignore auth state**: hero "Start rewriting" hardcodes `/sign-up` even when signed in | `components/landing/hero.tsx:31` |
| F4 | **No `not-found.tsx` / `error.tsx` / `global-error.tsx`**; only `/admin/promo-codes` has `loading.tsx` — errors show the raw Next.js page | `app/` tree |
| F5 | **No OG image**; homepage has no page-level metadata; sitemap omits public `/developers/*` subpages | `app/sitemap.ts` |
| F6 | Design-system split-brain: landing = custom classes, workspace/dashboards = ad-hoc Tailwind; `globals.css` is 2,684 lines, no reference doc | `app/globals.css` |
| F7 | Retention wording diverges: workspace/privacy "up to 90 days" vs API docs "30 days" — both true, never explained together | `/privacy`, `/developers/data` |
| F8 | `/app/account` and `/developers/keys` lack empty/loading/error states; support form is bare | `components/account/account-panel.tsx` |
| F11 | **`/developers` buries one of its two integration paths**: 622-line landing+API-reference hybrid; MCP (shipped, 406-line guide) gets a ghost button + one sentence; the "one key/quota/plan serves both" story is never told | `app/developers/page.tsx:201,226` |
| F12 | **The signed-in product has no shell** ("UX is very bad"): `/app/account` is an **orphan** (0 inbound links); backend history API (`GET/DELETE me/rewrites*`) is **dark** (shipped, never surfaced — users see only 5 localStorage items); the API console lives under public docs (`/developers/keys`); MCP configs are generic `rmv_live_xxx`; signed-in nav is still the marketing header. The journey the product sells — 改写 → API → key → MCP — has no in-product path | `grep app/account`=0; `RewriteHttpFunctions.cs:141,218,261`; `site-header.tsx:38` |

## 4. User journeys (the rationality backbone)

- **J1 Cold → paying**: `/` → demo → `/pricing` → Buy → sign-in/up **with SKU intent preserved** → Stripe → `/app?checkout=success` → first rewrite.
- **J2 Cold → trial**: `/` → sign-up → verify → `/app` first-run checklist (honest trial-code copy) → redeem → first rewrite → nudge.
- **J3 Returning**: `/` hero recognizes session → "Open app" → workspace shows balance → buy more in-place → continue.
- **J5 In trouble**: payment fails → past-due banner → `/app/billing` history → support form → confirmation.
- **J6 In-app developer** (the journey the owner named): `/app` rewrite → sidebar "API keys" → create key (reveal offers copy-config-with-key) → "Connect" → paste into Claude Code/Cursor → first tool call → `/app/usage` reflects it. Non-Pro hits upsell states, never errors.

## 5. Per-page plan (public + funnel surfaces)

Priority: **P0** trust/correctness · **P1** funnel/coherence · **P2** polish.

### Global chrome & infrastructure
- **G1 (P0)** branded `not-found.tsx` + `error.tsx` + `global-error.tsx` with recovery CTAs · **G2 (P0)** `loading.tsx` for `/app`, `/app/account`, `/developers/keys`, `/admin` · **G3 (P0)** OG image + homepage metadata + sitemap completion · **G4 (P2)** accessible mobile menu (replace native `<details>`) · **G5 (P2)** footer link groups (Developers subpages; Account → Billing/Account).

### `/` Landing (evolve)
- **L1 (P1)** auth-aware hero + closing CTA (signed-in → /app) · **L2 (P1)** homepage metadata · **L3 (P2)** demo "preset example" caption + sign-up bridge · **L4 (P2)** trust band aligned with legal. Demo stays provider-free; `sample-cases`/`how-it-works`/`naturalness` pins update in the same PR.

### `/pricing` (gap-fill; redesigned 2026-06-04)
- **PR1 (P1)** preserve buy intent through auth (`?redirectTo&intent=buy&sku=`; auto-resume once with "Continuing your purchase…" + cancel) · **PR2 (P1)** signed-in "purchases credit this account (email)" hint · **PR3 (P2)** styled checkout-failure row + retry.

### Auth — `/sign-in`, `/sign-up`, `/forgot-password`
- **A1 (P1)** honor `redirectTo`/`intent`/`sku` on all three + the Google OAuth `state` leg, same-origin guard, allowlist `/app/*` + `/pricing` · **A2 (P1)** sign-up next-steps framing, honest trial-code copy · **A3 (P2)** human error/rate-limit/Turnstile-failure states.

### Legal — `/terms`, `/privacy` (trust blockers)
- **LG1 (P0)** rewrite billing/quota section to the trial-code model; remove "3 lifetime free" · **LG2 (P0)** bump dates, reconcile 90-day/30-day retention (F7) · **LG3 (P2)** soften "MVP" hedging into committed language. (Q4: supervisor reviews copy; owner's gate = final PR.)

### `/developers` — **restructure** to a hub + two paths (F11)
Two real shipped integration surfaces share one key/quota/plan; the IA must show that. Target:
```
/developers      HUB (short): two path cards — REST API | MCP — + shared-foundation strip
                 (one key works for both → /app/keys · shared quota & Pro/API plan · data & legal links)
/developers/api  NEW — full REST reference moved VERBATIM from today's page (ids kept: quickstart/auth/api/errors/guides/pricing)
/developers/mcp  existing guide — now equal billing
/developers/data|terms|acceptable-use  unchanged
```
- **D0 (P1)** build hub + `/developers/api`; old anchors `/developers#…` → `/developers/api#…`; update header/footer/pricing/sitemap/`developers-page.test.ts` · **D1 (P2)** shared dev subnav across the 7 public dev pages · **D4 (P2)** the 30/90-day retention sentence on the hub strip + `/developers/data`.

### `/admin`, `/admin/users/[userId]` (internal; promo-codes fresh)
- **AD1 (P2)** bring dashboard + user-detail to the promo-codes table/drawer patterns + loading/error/empty states; non-admin redirect preserved.

### Design-system consolidation (after the above)
- **DS1 (P2)** extract tokens from `globals.css` into `docs/design-system.md`; map landing classes + Tailwind to the same tokens; no visual change · **DS2 (P2)** shared Button/Card/Banner/EmptyState/Skeleton set used by shell + dashboards.

---

## 6. The signed-in App Shell (the core of this redesign — F12)

### 6.1 Why the post-login UX fails (verified)
- **S1** `/app/account` is an orphan — `grep -rln "app/account"` = 0 hits.
- **S2** the server history API (`GET me/rewrites`, `GET/DELETE me/rewrites/{id}`) is dark — shipped, never surfaced; users see only a 5-item localStorage list.
- **S3** the API console (keys/usage/billing tabs) is parked at public `/developers/keys`; signed-in nav is still the marketing header.
- **S4** every MCP host config says `rmv_live_xxx`; the key must be hand-wired from another page.
- **S5** no in-product path connects rewrite → API → key → MCP.

### 6.2 Reference pattern (what "imitate established designs" means)
Common denominator of OpenAI Platform / Anthropic Console / Resend: one persistent **app shell** after login (sidebar desktop / drawer mobile + product topbar — not the marketing header); order = **tool first**, then developer console (keys, usage), then money (billing), then identity (account); feature-gated pages show an **upgrade state, never an error**; keys are shown **once at creation**, with integration snippets offered at that moment with the fresh key inserted.

### 6.3 Target IA (with R2 progressive disclosure + R5 shared-pool pill)
```
app/app/layout.tsx  ←  NEW shell: topbar + sidebar (desktop) / drawer (mobile)

Topbar:   brand → /app · quota pill ("32 of 90 left · web + API" → /app/usage) · Docs ↗ /developers · account menu
Sidebar (CONSUMER, default — what a teacher sees):
          CREATE      Rewrite   /app          (existing workspace, internals untouched)
                      History   /app/history  (surfaces the dark me/rewrites API)
          ACCOUNT     Billing   /app/billing  (purchase history · receipts · portal · support)
                      Account   /app/account  (email · sign out · delete account)
Sidebar (+ DEVELOPERS group — shown only if tier ∈ {Pro/API} OR the user flips "Developer mode" in the account menu):
          DEVELOPERS  API keys  /app/keys     (moved from /developers/keys → 301; gated reveal §6.5)
                      Usage     /app/usage    (usage + per-source quota breakdown)
                      Connect   /app/connect  (key-aware MCP + curl)
Account menu (topbar): Developer mode [toggle] · Billing · Account · Sign out
```
**R2 rationale:** ~95% of users only rewrite; they must not face an API console. The DEVELOPERS group auto-appears for Pro/API accounts (who paid for it) and is one toggle away for everyone else. The "one key serves both" pitch still lives on `/developers` + `/pricing`. The routes `/app/keys|usage|connect` always exist and are reachable by URL; the toggle only controls sidebar *visibility*.
**R5 rationale:** web rewrites and API/MCP calls draw from one pool — the pill and `/app/usage` must say "web + API" so a Pro user spending quota via MCP doesn't think they were double-charged.

| Route | Label | Content source | Gating |
|---|---|---|---|
| `/app` | Rewrite | existing `rewrite-workspace.tsx` **unchanged inside** | signed-in |
| `/app/history` | History | NEW UI over a NEW thin proxy `app/api/me/rewrites` → backend `me/rewrites*`; localStorage demoted to "Recent on this device" | signed-in |
| `/app/keys` | API keys | `DeveloperDashboard` **keys** tab extracted; `/developers/keys` → 301 | signed-in; non-Pro → upsell |
| `/app/usage` | Usage | usage tab + `usage-bar-chart` + per-source quota from `/api/me` | signed-in; API series → upsell for non-Pro |
| `/app/connect` | Connect | NEW host configs (Claude Code/Desktop/Codex/Cursor + curl), key-aware (§6.4) | signed-in; non-Pro → upsell |
| `/app/billing` | Billing | from `account-panel.tsx`: history, receipts CSV, Stripe portal, support form | signed-in |
| `/app/account` | Account | from `account-panel.tsx`: identity, sign out, delete-account flow | signed-in |

**No backend work anywhere** — every endpoint already exists. Public docs (`/developers/*`) stay the SEO/docs side; the shell links out. Marketing `site-header` no longer renders on `/app/*`; its signed-in "API keys" link retargets `/app/keys`. Footer Account column gains Billing/Account (fixes the orphan).

### 6.4 First-run onboarding on `/app` (the owner's named journey)
Dismissible, state-driven, honest per Q1(c):
1. **Get rewrites** — redeem a team-issued trial code (contact link) or buy a pack → done when balance > 0.
2. **First rewrite** — paste a draft → done when history ≥ 1.
3. *(collapsed "For developers")* **Create an API key** → `/app/keys` · **Connect your tools** → `/app/connect` — expanded only for Pro/API.
Checklist hides once 1–2 complete or dismissed; dev steps never block the consumer path.

### 6.5 Key-aware Connect (honest "personalized config")
Keys are stored **hashed** (`ApiKeyHashing.cs`) — a full key can never be re-displayed. Therefore:
- **At key creation** (`/app/keys`): the one-time reveal modal gains "Copy MCP config / Copy curl" with the fresh key embedded — the only moment full personalization is possible (matches the reference products).
- **On `/app/connect`**: configs show the key *prefix* (`rmv_live_ab12…`) as a picker + a paste slot with client-side substitution (nothing sent anywhere) + a "create a key" deep link.
- Non-Pro: same page, sample configs + upsell ("API & MCP come with Pro/API — NZ$19.90/mo").

### 6.6 Migration & compatibility
| Concern | Handling |
|---|---|
| `/developers/keys` inbound links (header, docs, tests) | 301 → `/app/keys`; header retargeted; `developers-page.test.ts` + `site-header*.test.ts` updated |
| `account-panel.tsx` (~800 lines, mixed) | decomposed into `billing-panel.tsx` + slim `account-panel.tsx`; delete-account moved intact (e2e-covered) |
| `workspace-copy.test.ts` pins | workspace internals untouched → only page-frame assertions change |
| Old `/app/account` URL | stays valid (now = slim account) |
| `redirectTo` allowlist | include `/app/*` equivalents |
| Mobile | drawer (not a 7-item tab strip — see §11 R3); same routes |
| a11y | sidebar = `nav` + `aria-current`; axe-AA per shell page |

### 6.7 Non-goals
No rebuild of workspace internals; no backend changes; no docs moved into the app; no admin-shell change; localStorage history demoted not deleted.

### 6.8 FE-S1 build breakdown (supervisor hand-builds in-session — R1)
The keystone. Because machine ACs can't see "commercial polish" (the current pages pass tests and still feel bad), I build the frame myself with live preview + screenshots, establish the **shared component vocabulary** the workers then replicate, and lock it at an owner visual checkpoint before S2–S7 enqueue. Concretely FE-S1 delivers:

1. **`app/app/layout.tsx`** — server component: one session check (redirect → `/sign-in?redirectTo=…` if no account), fetch the shell's data once (email, tier, shared quota balance) and pass to the client chrome. Verify the marketing `site-header` is suppressed on `/app/*` (check where it renders — root layout vs per-page — and use a route group or a conditional so it doesn't double up with the topbar).
2. **`components/app/shell/`** — `AppTopbar` (brand · `QuotaPill` · Docs↗ · `AccountMenu` with Developer-mode toggle), `AppSidebar` (desktop, `nav`+`aria-current`, progressive groups per R2), `AppDrawer` (mobile, hamburger + focus trap + Esc, R3), and the **shared primitives** S2–S7 reuse: `EmptyState`, `Skeleton`, `UpsellCard`, `SectionCard`. This hand-built set *is* the design language — it seeds DS2 early so worker pages snap into a consistent frame instead of inventing their own.
3. **Developer-mode state** — tier-derived default (Pro/API → on) + a client toggle persisted (localStorage `rimv.devmode` + cookie for SSR first-paint); never gates the routes, only sidebar visibility.
4. **Placeholder routes** for `/app/history|keys|usage|connect|billing` rendering the shell + an `EmptyState`/`Skeleton` so the nav is fully walkable the moment S1 merges; S2–S7 replace each placeholder body.
5. **Wires the existing `/app` workspace** into the shell frame unchanged (move only its outer page header into the topbar; `rewrite-workspace.tsx` internals untouched).
6. **Tests + a11y**: update `site-header*`/footer pins for the `/app/*` suppression + retargeted "API keys" link; axe-AA on the shell in both consumer and developer states, desktop + mobile.

Verification I run before the checkpoint: `preview_start` → screenshot desktop+mobile, consumer vs developer sidebar, drawer open/closed → `typecheck`/`test`/`build`. Owner sees the screenshots; only after sign-off do S2–S7 enqueue (R6: S1 is a solo canary on `delivery/fe-redesign`).

---

## 7. Decisions (owner, 2026-06-10 — all open questions resolved)
- **Q1 Trial funnel → (c):** codes stay team-distributed; no auto-grant/email-capture. W1/A2/pricing copy says so honestly.
- **Q2 Account deletion → already live** (verified): backend `DeleteAccount` (DELETE `/api/me`, `AccountHttpFunctions.cs:186` + handler: Stripe cancel + erase/anonymize + idempotent) + frontend proxy + dialog + `AccountApiTests`/`AccountUseCaseTests`. → e2e-verify only, no backend work.
- **Q3 Dark mode:** deferred.
- **Q4 Legal copy:** supervisor (Claude) reviews LG1/LG2/LG3; owner's gate = final integration→main PR.
- **Delivery:** Codex quota exhausted → workers run as **Claude Sonnet headless**.

## 8. Execution model — hybrid (R1)

The shell frame and the content pages are built by **different hands on purpose**:

- **Supervisor (me, in-session):** FE-S1 only — the shell scaffold + shared primitives (§6.8), built with live preview + screenshots, locked at an owner visual checkpoint. This is the quality-defining piece machine ACs can't judge.
- **Headless Sonnet workers (delivery pipeline):** everything else — Wave A trust pages, Wave B content pages S2–S7 (which snap into the locked frame), Wave C funnel, Wave D polish. Each is a content/behavior task with checkable ACs that fills an already-good frame.

Build order: **(1)** I hand-build FE-S1 → owner sign-off → merge to `delivery/fe-redesign` as a solo canary (R6). **(2)** Launch the worker pipeline: Wave A (independent of the shell, can run in parallel with step 1) + Wave B S2–S7 (only after S1 merged). **(3)** Wave C → **(4)** Wave D. Supervisor visual review (`ui-browser-testing`) gates each wave before the next enqueues.

### 8.1 Worker mechanism — the one swap: `run_codex` → `run_claude`
Keep the `dynamic-delivery-workflow` daemon shape (detached driver + sentinel watchdog + heartbeat; worktree-per-issue; worker commits in-worktree, driver pushes + opens PR into the integration branch; verify gates distrust worker output: banned-term diff-scoped, secret/suppress scan, tests-by-diff, scope diffstat; ≤2 attempts). Swap only the worker call (`driver.sh:282`):

```sh
# run_claude WT LAST JSONL THREAD PROMPTFILE TIMEOUT_SEC
run_claude(){
  local wt="$1" last="$2" jsonl="$3" thread="$4" pfile="$5" tmo="$6"
  local args=( -p --model "${WORKER_MODEL:-sonnet}" --output-format stream-json --verbose
               --allowedTools "$WORKER_ALLOWED_TOOLS" )
  [ -n "$thread" ] && [ "$thread" != none ] && args+=( --resume "$thread" )
  ( cd "$wt" && env -u CLAUDECODE -u CLAUDE_CODE_ENTRYPOINT \
      timeout "$tmo" claude "${args[@]}" < "$pfile" ) > "$jsonl" 2>&1
  local rc=$?
  python3 "$HERE/extract-claude-result.py" "$jsonl" "$last"   # final result text → $last; session_id for resume
  return $rc
}
```
Adaptations: session_id (from the final `result` event) replaces codex thread id; `extract-claude-result.py` replaces `-o`; preflight checks `claude` + an auth smoke; `claude-brief.tmpl` adds headless-worker rules (commit in-worktree, never push/PR, never touch files outside `scope:`, terse machine-readable final summary). Containment = deny-by-default `--allowedTools` (Edit/Write/Read/Glob/Grep/TodoWrite + scoped `npm`/`npx`/`git`/read shell; **no** push/gh/web) + worktree isolation + driver gates; **never `--dangerously-skip-permissions`** (AGENTS.md). `WORKER_MODEL=sonnet` (never opus). Patch the **staged copy** in `.delivery/fe-redesign/`, not the global skill. Serial execution (Sonnet shares the owner's Claude usage window).

## 9. Issue breakdown (24 issues, 4 waves)

**Wave A — P0 trust & correctness (4):**
| ID | Scope | Acceptance |
|---|---|---|
| FE-A1 | branded 404/error/global-error (G1) | unknown route + thrown error render branded; build green |
| FE-A2 | loading skeletons: /app, /app/account, /developers/keys, /admin (G2) | render; no CLS on settle |
| FE-A3 | OG image + homepage metadata + sitemap (G3, L2) | `og:*` present; sitemap lists public routes |
| FE-A4 | /terms + /privacy correction — trial-code model, retention, dates (LG1, LG2) | no "lifetime free"; aligns with /pricing; tests updated |

**Wave B — Signed-in app shell (design §6; no backend work). FE-S1 = supervisor-built (§6.8); S2–S7 = workers into the locked frame:**
| ID | Owner | Scope | Acceptance |
|---|---|---|---|
| **FE-S1** | **supervisor (in-session)** | shell `app/app/layout.tsx` + `components/app/shell/*` (topbar · progressive sidebar R2 · mobile drawer R3 · shared primitives) · quota pill shared-pool label R5 · Developer-mode toggle · marketing header off on /app/* · header "API keys"→/app/keys · footer Account links · walkable placeholder routes | **owner visual checkpoint** (desktop+mobile, consumer vs developer); shell on all /app/*; `aria-current`; header/footer tests updated; axe pass both states; canary green (R6) |
| FE-S2 *(S1)* | worker | /app/history: proxy `app/api/me/rewrites` → backend list/detail/delete; paginated UI; localStorage demoted to "Recent on this device" | list/view/delete e2e; survives reload; workspace pins updated |
| FE-S3 *(S1)* | worker | /app/keys: extract keys tab; /developers/keys→301; loading/error/empty; non-Pro upsell; reveal "copy MCP config/curl with this key" | redirect lands; states reachable; create-key intact; upsell not error |
| FE-S4 *(S1)* | worker | /app/usage: usage tab + chart + per-source shared-pool quota from /api/me | matches API; quota pill deep-links here; "web + API" labeled |
| FE-S5 *(S1,S3)* | worker | /app/connect: key-aware host configs + curl; client-side key substitution; non-Pro upsell | snippets per host; substitution local-only; banned-term clean |
| FE-S6 *(S1)* | worker | /app/billing + slim /app/account decomposition; empty states; support form states | decomposed render; delete-account intact; states tested |
| FE-S7 *(S1)* | worker | first-run checklist on /app (get rewrites → first rewrite → collapsed dev steps), honest Q1(c) copy | fresh account sees it; hides after 1–2/dismiss; dev group Pro-only; `workspace-copy.test.ts` updated |

**Wave C — Acquisition funnel (6):**
| ID | Scope | Acceptance |
|---|---|---|
| FE-B1 | auth + Google OAuth honor redirectTo/intent/sku, same-origin guard (A1) | round-trip; open-redirect rejected |
| FE-B2 *(B1)* | BuyButton carries intent; /pricing auto-resumes checkout (PR1) | signed-out buy → auth → checkout, no second click |
| FE-B3 | landing auth-aware CTAs + pricing signed-in hint (L1, PR2) | signed-in → /app CTA |
| FE-B5 | ?checkout=success/cancelled banners on /app (W2) | param-driven; none on reload |
| FE-B6 | sign-up next-steps framing per Q1(c) (A2) | copy test |
| FE-B9 | /developers restructure: hub + /developers/api verbatim, anchors redirect, links/sitemap/tests (D0) | two equal paths; old anchors land; green |

**Wave D — Coherence & polish (7):**
| ID | Scope | Acceptance |
|---|---|---|
| FE-C1 | accessible marketing-header mobile menu + footer groups (G4, G5) | header tests; axe |
| FE-C2 | demo caption + sign-up bridge; trust band (L3, L4) | demo provider-free; pins |
| FE-C3 *(B2)* | failure-state copy: checkout retry, auth errors, workspace taxonomy incl. "not charged" (PR3, A3, W4) | error→copy unit tests; banned-clean |
| FE-C4 | delete-account journey e2e verify + polish (AC4) | delete e2e green |
| FE-C5 *(B9)* | dev subnav across 7 pages + 30/90-day sentence + legal hedge softening (D1, D4, LG3) | subnav; consistent copy |
| FE-C6 | /admin dashboard + user-detail consistency states (AD1) | render tests; non-admin redirect |
| FE-C7 | design tokens doc + shared primitives incl. shell components (DS1, DS2) | visual smoke unchanged; doc exists |

Every copy-touching issue updates its contract tests **in the same PR** (`workspace-copy`, `pricing-auth-visual-system`, `developers-page`, `site-header*`, pricing suite) — `npm run test` red blocks the prod deploy by design.

## 10. Verification
- Per PR: `typecheck` · `test` · `build` (+ `cf:build` if Worker-affecting) · diff-scoped banned grep.
- Per wave (supervisor, `ui-browser-testing`): Playwright journeys (J6 + J2/J3/J5 after Wave B; J1/J4 after Wave C) on desktop 1280 + mobile 390 + axe WCAG-AA on the customer pages incl. new shell routes; screenshots into the wave dir.
- Integration branch: full gates + repo-wide banned grep, then the single human integration→main PR. Post-merge live smoke (public pages 200, OG tags, 404 renders).

## 11. Design self-review — *can it actually be built this way?*
(Honest critique of this plan, not a defense. Recommended adjustments R1–R6.)

**What's solid.** The diagnosis is evidence-backed, not taste: the orphan page, the dark API, and the console-in-docs are facts, and the shell pattern is the established answer to "disconnected pages." Reusing the existing backend (zero migrations) keeps risk low and rollback trivial. Preserving the 2026-06-04 workspace internals avoids redoing fresh work.

**The deepest risk — machine gates ≠ commercial polish.** *The current pages already pass all their contract tests, and the owner still calls the UX "very bad."* That is the whole problem: green ACs ("renders", "axe pass", "redirect lands") are necessary but **cannot see** "looks like a real product." If FE-S1 (the shell that everything hangs off) is handed to a headless Sonnet worker judged only by machine ACs, we risk re-shipping technically-correct-but-mediocre UX — the exact failure we're fixing.
→ **R1: build the shell scaffold (FE-S1) in-session, by me, with live preview + screenshots, and pin the visual result before fanning S2–S7 out to workers.** Put a human visual checkpoint after S1. Workers are fine for the *content* pages that snap into a good frame; they're the wrong tool for *inventing* the frame.

**Consumer vs developer overload.** A teacher who just wants to rewrite an email should not face a sidebar of "API keys / Usage / Connect." The onboarding hides dev *steps*, but the **sidebar still shows all seven items to everyone** — that's clutter for ~95% of users and makes the product read as a dev tool.
→ **R2: progressive disclosure in the sidebar.** Consumers see Rewrite / History / Billing / Account. The DEVELOPERS group appears only when the user has Pro/API, or behind a one-click "Developer mode" toggle in the account menu. The "one key serves both" story still lives on `/developers` and `/pricing` for those who want it.

**Mobile.** Seven sidebar items do not become a good mobile tab strip — reference products use a drawer/hamburger. The earlier "scrollable tab strip" was mediocre.
→ **R3: mobile = drawer**, with a bottom tab bar of at most the top 3 consumer actions (Rewrite / History / Account) if we want thumb-reach.

**Big-bang vs incremental.** Seven interdependent shell issues (all blocked on FE-S1) is a large, serialized bet for autonomous workers on a live prod site, and it touches the most contract tests (header/footer/workspace/developers) — the highest-breakage cluster in the repo.
→ **R4: consider a thin Phase 0 first** — just (a) add nav links to the existing `/app/account` + (b) surface `me/rewrites` history on the current workspace. Two small issues, ~80% of the "I can't find anything" relief, shippable in days, and they de-risk the full shell by proving the history proxy and the nav model before the layout rebuild. Then the full shell becomes an *upgrade*, not a *cutover*.

**Quota semantics.** Web and API draw from **one** pool. A single quota pill is clean, but a Pro user burning quota via MCP must understand their web rewrites and tool calls share 90/mo.
→ **R5: the pill + `/app/usage` must label the shared pool explicitly** ("32 of 90 left · web + API"), or the first power user will think they were double-charged.

**Sequencing.** Because the shell clusters the riskiest test churn, it should run on its own canary branch first and get the wave's earliest supervisor review.
→ **R6: run FE-S1 as a solo canary** (the pipeline's canary-first mode), green before S2–S7 enqueue.

**Net verdict (LOCKED 2026-06-10).** The IA is right and worth doing. **R1 and R2 are load-bearing and adopted**: the shell frame (FE-S1) is hand-built by the supervisor with a human in the visual loop (§6.8), and the sidebar adapts to consumer vs developer (§6.3). R3 (mobile drawer), R5 (shared-pool quota label), R6 (S1 canary) folded in. **R4 resolved → full App Shell, no Phase 0** (owner: "直接做完整 Shell"). The risk this retires: without R1 we'd reproduce polished-but-disconnected screens that pass tests and still feel off — the exact trap that produced today's UX.

## 11b. Pricing v3 — /pricing redesign plan (owner request 2026-06-10, PLAN ONLY)

Context: /pricing was redesigned 2026-06-04 (PR #502) — structure is solid (trial card + pack tiers w/ unit price + one-time-vs-subscription split + comparison + trust + FAQ). But the 2026-06-10 console redesign changed product facts underneath it, and the funnel gaps remain. Audit of `app/pricing/page.tsx` + `pricing-{comparison,faq,trust}.tsx` (2026-06-10):

### What's now WRONG on the page (P0 — the page lies)

| # | Stale claim (3 surfaces each) | Reality since the console redesign |
|---|---|---|
| PF1 | "Private local history" (comparison row) · "Recent rewrites stay in your browser's local history and are not saved to our database" (FAQ) · "History stays in your browser" + "Private history" card (trust band) | History is **server-backed** at /app/history (cross-device view/delete, raw content retained ≤90 days then removed — per /terms + /privacy). The localStorage list was removed from the workspace. The FAQ claim also contradicted /privacy even before |
| PF2 | "Warm and Direct tone presets" (trial card) · "Warm · Direct tones" (comparison row) · "Warm · Direct" (trust card) | The workspace hardcodes `tone:"warm"`; there is **no tone picker** (known: tone-presets-not-in-product, caused a false hero bullet before) |
| PF3 | "Tone check" naming (trial card, comparison, trust) | The product UI calls it **AI Signal** (workspace meter, history detail). Naming should match what users see in-app |
| PF4 | Pro/API card: "Includes API access" only | Pro/API now means **REST API + MCP server, one key, shared web+API balance** — the console DeveloperUpsell sells exactly that story; pricing must match it |

### What's missing (funnel + coherence)

- **PG1 (P1)** Signed-in awareness: page is identical for signed-in users. Should show "Purchases credit this account (email) · current balance"; trial CTA for signed-in users should open redeem in /app (not /sign-up); an **active Pro/API subscriber** should see "You're on Pro/API — Manage billing" instead of "Go Pro/API" (also guards accidental double-subscribe — verify backend behavior).
- **PG2 (P1)** Buy-intent through auth — already planned as FE-B1+FE-B2 (signed-out Buy loses the SKU at the auth wall). Pricing v3 depends on it; not duplicated here.
- **PG3 (P2)** Trial-code honesty per Q1(c): a visitor without a code has no path. Add "Don't have a code? Start with Quick Pack, or contact us" under the trial CTA.
- **PG4 (P2)** `id="pro"` anchor on the Pro/API card so the console DeveloperUpsell ("Get Pro/API") deep-links to it; link the card to /developers for the integration story.
- **PG5 (P2)** FAQ gaps: "Can I use the API with a one-time pack?" (no — Pro/API only), "What is MCP / can I use it inside Claude or Cursor?", "Where do I get a trial code?", fix the privacy answer (PF1). Trust band: replace false items with real ones (history across devices + delete anytime; delete account & data anytime).
- **PG6 (P2)** Checkout failure UX under BuyButton (= FE-C3 slice) + checkout return banners (= FE-B5).

### How to design it better (structure)

- **Two-audience split**: "For everyday replies" (Trial + Quick/Value packs) vs **"For developers"** (Pro/API as its own developer-flavored card: API + MCP + one-key + shared-balance feature list mirroring the console DeveloperUpsell, CTA pair "Go Pro/API" + "Read the docs"). Today Pro/API is visually a third row inside the dark packs panel — it deserves the developer framing.
- **Name the free tier "Free"** (matches the Account console's Plan="Free"): comparison column "Trial" → "Free (trial code)"; keeps one mental model across pricing ↔ console.
- Keep: per-rewrite unit price, one-time-vs-subscription grouping, "Most popular" highlight, marketing design language (this is a public page — it stays in the landing system, not the shell system).

### Issue packaging (3 issues; copy contract tests `pricing-*`/`pricing-auth-visual-system` update in the same PR)

| ID | Scope | Acceptance |
|---|---|---|
| PV-1 (P0) | Truth pass: fix PF1/PF2/PF3 across trial card + comparison + trust + FAQ; rename to AI Signal; server-history copy consistent with /terms + /privacy | no "local history"/"tone presets" claims; grep-consistent with legal pages; tests updated |
| PV-2 (P1) | Developer card: PF4 + PG4 + two-audience split + comparison row "API + MCP access"; "Free" naming | Pro card lists API+MCP+one key+shared balance; `#pro` anchor lands; /developers linked |
| PV-3 (P1, dep FE-B1/B2) | Signed-in awareness PG1 + trial-CTA retarget + Pro-subscriber "Manage billing" swap; PG3 honesty line; PG5 FAQ/trust additions | session-aware render verified; double-subscribe path closed; tests updated |

Open questions for the owner: **(a)** annual billing / larger packs / team plan — out of scope unless wanted; **(b)** confirm Pro-subscriber "Go Pro/API" → "Manage billing" swap (recommended); **(c)** Focus Pack stays env-hidden?

## 12. Locked build sequence
1. **FE-S1 (supervisor, in-session):** hand-build the shell per §6.8 → `preview_start` + desktop/mobile screenshots (consumer vs developer sidebar, drawer) → **owner visual checkpoint** → merge to `delivery/fe-redesign` as a solo canary (R6).
2. **Launch worker pipeline** (`run_claude` swap, §8.1): Wave A trust (parallel-safe with step 1) + Wave B S2–S7 (after S1 merges).
3. **Wave C** funnel → **4. Wave D** polish. Supervisor `ui-browser-testing` review gates each wave.
4. **Final:** single human integration→main PR (deploys prod). Rollback `wrangler rollback`.

**Awaiting owner GO to start step 1.** Nothing below has run; no code changed yet.
