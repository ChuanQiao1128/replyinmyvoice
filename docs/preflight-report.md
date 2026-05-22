# Autonomous Preflight Report

Date: 2026-05-17
Workspace: `/Users/qc/Desktop/CloudFlare`

## Decision

Proceed with coding and deployment preparation.

External services needed for development are reachable. Cloudflare token now passes Pages, Workers, Zone, and DNS records checks. Keep the current live holding page intact until final cutover.

## Working Directory And Git

- Current working directory: `/Users/qc/Desktop/CloudFlare`
- Git branch/status: `codex/autonomous-mvp`; setup/docs files are untracked
- GitHub remote: `origin -> git@github.com:ChuanQiao1128/replyinmyvoice.git`
- Remote branch heads: none detected earlier; repository appears reachable and likely empty

## Existing Project Structure

Detected files before app scaffolding:

```text
.gitignore
.nvmrc
AGENTS.md
docs/preflight-report.md
local-env.md
replyinmyvoice_requirements.md
```

- Framework detected: none yet; app still needs to be scaffolded in this same directory.
- Package manager detected: npm available; no `package.json` yet.
- Next.js version detected: not installed yet.
- Clerk middleware decision: target Next.js 15 should use `middleware.ts`.
- Holding page code in this workspace: no app source found here. Existing live holding page exists on Cloudflare and must remain live until cutover. Preserve its warm brand direction in the new app.

## Tool Versions

- Default shell Node: `v24.9.0`
- Default shell npm: `11.6.0`
- `bash -lc` Node: `v22.13.1`
- `bash -lc` npm: `10.9.2`
- Wrangler: `4.92.0`
- `.nvmrc`: `22`

Compatibility note: if install/build fails under Node 24, first try dependency-compatible fixes. If still blocked, recommend using Node 22 LTS or Node 20 LTS for local build consistency.

## Gitignore Secret Protection

- `.env`: protected
- `.env.local`: protected
- `.dev.vars`: protected
- `globalapikey/`: protected
- `node_modules/`: protected
- `.next/`: protected
- `.open-next/`: protected
- `.wrangler/`: protected
- `dist/`: protected

## Required Env Vars

Secret values are intentionally not printed.

```text
NEXT_PUBLIC_APP_URL=present
NODE_ENV=present
NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY=present
CLERK_SECRET_KEY=present
NEXT_PUBLIC_CLERK_SIGN_IN_URL=present
NEXT_PUBLIC_CLERK_SIGN_UP_URL=present
NEXT_PUBLIC_CLERK_AFTER_SIGN_IN_URL=present
NEXT_PUBLIC_CLERK_AFTER_SIGN_UP_URL=present
DATABASE_URL=present
DIRECT_URL=present
OPENAI_API_KEY=present
OPENAI_MODEL=present
OPENAI_TIMEOUT_SEC=present
NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY=present
STRIPE_SECRET_KEY=present
STRIPE_PRICE_ID=present
STRIPE_WEBHOOK_SECRET=present
WRITING_SIGNAL_PROVIDER=present
SAPLING_API_KEY=present
WRITING_SIGNAL_TIMEOUT_SEC=present
ALLOW_DEV_SUBSCRIPTION_BYPASS=present
CLOUDFLARE_ACCOUNT_ID=present
CLOUDFLARE_API_TOKEN=present
LAUNCH_CONFIRMED=present
EVAL_MAX_PROMPT_ITERATIONS=present
EVAL_MAX_WALLCLOCK_MINUTES=present
```

## External Service Checks

- Neon `DATABASE_URL`: ok
- Neon `DIRECT_URL`: ok
- OpenAI model availability: ok
- Clerk configuration availability: ok
- Stripe price: ok, `unit_amount=900`, `currency=nzd`, `interval=month`; display as `NZD $9/month`
- Stripe webhook availability: ok for `checkout.session.completed`, `customer.subscription.created`, `customer.subscription.updated`, `customer.subscription.deleted`
- Stripe webhook invoice events: not currently configured in dashboard; implement handlers and document adding `invoice.paid` and `invoice.payment_failed`
- Sapling `aidetect` availability: ok
- Sapling observed response keys: `score`, `sentence_scores`, `text`, `token_probs`, `tokens`
- Sapling score field: number, use 0..1 to 0..100 conversion unless implementation observes a schema change
- Cloudflare deployment target: Next.js App Router on Cloudflare Workers using OpenNext; no static export
- Worker name: `replyinmyvoice-app`
- Cloudflare API token availability: present
- Cloudflare Zone read: ok
- Cloudflare DNS records read: ok
- Cloudflare Pages API: ok, `replyinmyvoice` project found
- Cloudflare Workers Scripts API: ok
- Launch guardrail: `LAUNCH_CONFIRMED=false`, so do not modify `replyinmyvoice.com` DNS or existing Pages custom domain during autonomous development

## Blockers And Fallbacks

- Blocking issues before coding: none.
- Deployment/cutover fallback: keep the existing live holding page intact if a Cloudflare custom-domain or dashboard-only action blocks automatic cutover.
- If a dashboard-only action appears, document it in `docs/manual-setup.md` and continue all code work.
- Auth testing requirement: after implementation, automatically test registration/sign-in gates, logged-in `/app` access, and unauthenticated rewrite API rejection.
- Usage enforcement requirement: enforce quota server-side only; count exactly one successful user rewrite request after the response is ready.
- Naturalness optimization requirement: run the local sample evaluation, target average signal drop of at least 30 points and most rewritten samples below 50%; iterate prompts, strategies, and scoring if the target is weak.
- Naturalness guardrail: use 8-12 samples, at most 3 evaluation strategy rounds by default, and respect `EVAL_MAX_PROMPT_ITERATIONS` / `EVAL_MAX_WALLCLOCK_MINUTES`. If the target is not met within budget, document best measured strategy in `docs/optimization-notes.md` and continue.

## Post-Implementation Preflight Update

Date: 2026-05-18

- Current working directory: `/Users/qc/Desktop/CloudFlare`
- Current branch: `codex/autonomous-mvp`
- GitHub remote: `origin -> git@github.com:ChuanQiao1128/replyinmyvoice.git`
- Framework detected: Next.js `15.5.18`
- Deployment target: Cloudflare Workers via `@opennextjs/cloudflare`
- Worker name: `replyinmyvoice-app`
- Launch guardrail: `LAUNCH_CONFIRMED=false`; DNS and existing Pages custom domain were not modified
- Package manager: npm
- Node policy: `.nvmrc` is `22`, `package.json` requires `>=22 <23`
- Database runtime: Prisma schema/migrations are retained; Worker runtime database access uses Neon serverless SQL because the Prisma generated client WASM path failed in OpenNext workerd preview
- OpenNext build: passes
- Worker preview smoke:
  - `/`: 200
  - `/pricing`: 200
  - `/sign-in`: 200
  - `/app`: 307 auth redirect when signed out
  - unauthenticated `/api/rewrite`: 401
  - `/api/stripe/webhook` GET: 200
  - `/api/health/db`: 200
- Worker deployment:
  - URL: `https://replyinmyvoice-app.qc1128qc.workers.dev`
  - Latest deployed version ID observed: `c4b14fa9-a58d-4d4e-8597-e48baf7c5098`
  - Remote `/`: 200
  - Remote `/pricing`: 200
  - Remote `/sign-in`: 200
  - Remote `/app`: 307 auth redirect when signed out
  - Remote unauthenticated `/api/rewrite`: 401
  - Remote `/api/stripe/webhook` GET: 200
  - Remote `/api/health/db`: 200
- Banned-term scan over `app`, `components`, `public`, and `lib` source paths: clean
- Current deployment blockers: none for independent Worker deployment; final custom-domain cutover remains blocked by launch guardrail and should be manual/dashboard-confirmed

## Launch Cutover Preflight Update

Date: 2026-05-18

- Current working directory: `/Users/qc/Desktop/CloudFlare`
- Current branch: `codex/autonomous-mvp`
- GitHub remote: `origin -> git@github.com:ChuanQiao1128/replyinmyvoice.git`
- GitHub push dry run: ok
- Launch authorization: `LAUNCH_CONFIRMED=true`
- Secret handling: `.env.local` inspected only for variable names/presence; no secret values printed
- `.gitignore` protection confirmed for:
  - `.env`
  - `.env.local`
  - `.dev.vars`
  - `globalapikey/`
  - `node_modules/`
  - `.next/`
  - `.open-next/`
  - `.wrangler/`
  - `dist/`
- Cloudflare API checks:
  - Zone read: ok
  - DNS records read: ok
  - Worker script read for `replyinmyvoice-app`: ok
  - Workers custom domains API read: ok
  - Workers routes API read: 403; use custom domains API path for cutover instead of legacy Workers routes
- Stripe sandbox price verification: `unit_amount=900`, `currency=nzd`, `interval=month`
- Stripe webhook secret presence: yes
- Verification commands:
  - `npm run lint`: pass
  - `npm run typecheck`: pass
  - `npm run test`: 15 tests passed
  - `npm run test:e2e`: 2 tests passed
  - `npm run build`: pass
  - `npm run cf:build`: pass
  - banned-term scan over app source paths: clean
- Worker `workers.dev` smoke:
  - `/`: 200
  - `/pricing`: 200
  - `/sign-in`: 200
  - `/app`: 307 signed-out redirect
  - unauthenticated `/api/rewrite`: 401
  - `/api/stripe/webhook` GET: 200
  - `/api/health/db`: 200

## Launch Dashboard Configuration Check

Date: 2026-05-18

- Clerk local/public env alignment:
  - `NEXT_PUBLIC_APP_URL`: ok for `https://replyinmyvoice.com`
  - `NEXT_PUBLIC_CLERK_SIGN_IN_URL`: ok for `/sign-in`
  - `NEXT_PUBLIC_CLERK_SIGN_UP_URL`: ok for `/sign-up`
  - `NEXT_PUBLIC_CLERK_AFTER_SIGN_IN_URL`: ok for `/app`
  - `NEXT_PUBLIC_CLERK_AFTER_SIGN_UP_URL`: ok for `/app`
  - `CLERK_SECRET_KEY`: present
  - `NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY`: present
- Clerk API checks:
  - `/instance`: ok
  - `/redirect_urls`: ok, count observed as 0
  - `/domains`: ok, but `replyinmyvoice.com` was not observed
  - Clerk dashboard action remains: verify formal domain/origin and redirects before real-account testing
- Stripe sandbox API checks:
  - Formal webhook endpoint `https://replyinmyvoice.com/api/stripe/webhook`: present
  - `checkout.session.completed`: ok
  - `customer.subscription.created`: ok
  - `customer.subscription.updated`: ok
  - `customer.subscription.deleted`: ok
  - `invoice.paid`: added and verified
  - `invoice.payment_failed`: added and verified

## Formal Domain Cutover

Date: 2026-05-18

- Latest Worker deploy before cutover:
  - Worker: `replyinmyvoice-app`
  - URL: `https://replyinmyvoice-app.qc1128qc.workers.dev`
  - Version ID observed: `2a382dc2-b59d-4649-8394-3e1f8d8d5f87`
- Worker custom domain:
  - `replyinmyvoice.com`: present
  - service: `replyinmyvoice-app`
- Cloudflare Pages project:
  - `replyinmyvoice`: preserved
  - apex Pages custom domain removed to allow Worker custom domain
  - `www.replyinmyvoice.com`: still present on Pages at the time of cutover check
- Rollback record:
  - recreate apex CNAME `replyinmyvoice.com -> replyinmyvoice.pages.dev`, proxied, ttl `1`
- Formal-domain smoke:
  - `/`: 200
  - `/pricing`: 200
  - `/sign-in`: 200
  - `/app`: 307 signed-out redirect
  - unauthenticated `/api/rewrite`: 401
  - `/api/stripe/webhook` GET: 200
  - `/api/health/db`: 200

## Real Account Launch Path

Date: 2026-05-18

- Worker runtime secrets:
  - Required runtime secret names were written to Worker `replyinmyvoice-app`.
  - `NODE_ENV` was corrected to `production` for the Worker.
  - Secret values were not printed.
- Runtime provider fix:
  - `/api/rewrite` initially returned 500 on the formal domain.
  - Root cause observed in Worker tail: OpenAI SDK network path returned `Connection error`.
  - `lib/openai.ts` now uses Worker-native `fetch` for OpenAI Chat Completions with the existing JSON output contract.
  - Stripe checkout/portal/webhook subscription retrieval now use Worker-native `fetch` for Stripe API calls with timeout protection; Stripe SDK remains for webhook signature verification.
- Real signed-in account checks on `https://replyinmyvoice.com`:
  - Clerk test user creation by API: ok
  - sign-in token flow through `/sign-in?__clerk_ticket=...`: ok
  - `/app` authenticated workspace: ok
  - free rewrite 1: 200
  - free rewrite 2: 200
  - free rewrite 3: 200
  - fourth free rewrite after quota: 402
  - DB usage row: `lifetime:3`
- Stripe sandbox checks:
  - authenticated `/api/stripe/checkout`: 200, hosted Checkout URL created
  - Stripe Checkout browser page reached, but headless automation hit Stripe/hCaptcha agent verification
  - fallback backend subscription path used for launch verification:
    - Stripe test payment method attached by API: ok
    - Stripe sandbox subscription created by API: active
    - real Stripe webhook updated DB: `subscriptionStatus=active`, subscription id present
    - paid authenticated `/api/rewrite`: 200

## Naturalness Optimization

Date: 2026-05-18

- Baseline during this phase: average reduction 7 points, `0/8` rewrites below 50%.
- Final selected strategy:
  - first attempt: OpenAI plain email-thread note
  - fallback attempt: deterministic thread fallback using user-provided facts only
  - fallback runs only when the first candidate remains above 50% AI-like signal or reduces the draft by less than 30 points
- Final evaluation set:
  - samples evaluated: 8
  - average AI-like signal reduction: 89 points
  - rewrites below 50% AI-like signal: `8/8`
  - internal target met: yes
- Production request cap remains within the planned budget:
  - one draft writing-signal call
  - up to one OpenAI rewrite attempt
  - up to two rewrite writing-signal calls

## Final Verification

Date: 2026-05-18

- `npm run lint`: pass
- `npm run typecheck`: pass
- `npm run test`: 15 tests passed
- `npm run test:e2e`: 2 tests passed
- `npm run build`: pass
- `npm run cf:build`: pass
- Banned-term scan over `app`, `components`, `public`, and source `lib`: clean
- Latest deployed Worker version observed: `6b3f15b7-a6f2-4664-935e-b529ddd88ba7`
- `workers.dev` smoke:
  - `/`: 200
  - `/pricing`: 200
  - `/sign-in`: 200
  - `/app`: 307
  - `/api/stripe/webhook`: 200
  - `/api/health/db`: 200
- Formal-domain smoke:
  - `/`: 200
  - `/pricing`: 200
  - `/sign-in`: 200
  - `/app`: 307
  - `/api/stripe/webhook`: 200
  - `/api/health/db`: 200
  - same-origin unauthenticated `/api/rewrite`: 401

## M6-003 Worker Preview Smoke Attempt

Date: 2026-05-22

- Target: `https://replyinmyvoice-app.qc1128qc.workers.dev`
- Scope from `plans/issue-manifest.md` and `docs/launch-cutover-plan.md`:
  - `GET /`: expected 200
  - `GET /pricing`: expected 200
  - `GET /sign-in`: expected 200
  - `GET /app`: expected 307 signed-out redirect
  - `POST /api/rewrite`: expected 401 when signed out
  - `GET /api/stripe/webhook`: expected 200
  - `GET /api/health/db`: expected 200
- Result: blocked by this sandbox before reaching Cloudflare. Shell `curl` returned `Could not resolve host` for the Worker host, `cloudflare.com`, and `example.com`. Node `fetch` from both the shell and Node REPL returned `fetch failed` for the Worker routes.
- Route statuses observed from this environment: none.
- No deployment, Worker secret, DNS, Stripe, or `.env.local` changes were made.
- Required rerun in a networked shell:

```bash
curl -sS -o /dev/null -w "%{http_code} %{redirect_url}\n" https://replyinmyvoice-app.qc1128qc.workers.dev/
curl -sS -o /dev/null -w "%{http_code} %{redirect_url}\n" https://replyinmyvoice-app.qc1128qc.workers.dev/pricing
curl -sS -o /dev/null -w "%{http_code} %{redirect_url}\n" https://replyinmyvoice-app.qc1128qc.workers.dev/sign-in
curl -sS -o /dev/null -w "%{http_code} %{redirect_url}\n" https://replyinmyvoice-app.qc1128qc.workers.dev/app
curl -sS -X POST -o /dev/null -w "%{http_code}\n" https://replyinmyvoice-app.qc1128qc.workers.dev/api/rewrite
curl -sS -o /dev/null -w "%{http_code}\n" https://replyinmyvoice-app.qc1128qc.workers.dev/api/stripe/webhook
curl -sS -o /dev/null -w "%{http_code}\n" https://replyinmyvoice-app.qc1128qc.workers.dev/api/health/db
```

## M6-005 Formal Domain Smoke Prerequisite

Date: 2026-05-23

- Target: `https://replyinmyvoice.com`
- Scope from `plans/issue-manifest.md`:
  - `GET /`: expected 200
  - `GET /pricing`: expected 200
  - `GET /sign-in`: expected 200
  - `GET /privacy`: expected 200
  - `GET /terms`: expected 200
  - `GET /robots.txt`: expected 200
  - `GET /sitemap.xml`: expected 200
  - `GET /app`: expected 307 signed-out redirect
  - `POST /api/rewrite`: expected 401 when signed out, inherited from M6-003
  - `GET /api/stripe/webhook`: expected 200, inherited from M6-003
  - `GET /api/health/db`: expected 200, inherited from M6-003
- Result from this Codex sandbox: not verified. `node` DNS lookup returned `ENOTFOUND` for `replyinmyvoice.com` and `example.com`; `curl` returned `Could not resolve host` for both hosts before any HTTP response.
- Classification: this is a network-capable runner prerequisite, not a product code change or user-only decision. No deployment, Worker secret, DNS, Stripe, npm, dashboard, or `.env.local` changes are required for the repair.
- Required rerun in a shell that can resolve public DNS:

```bash
curl -sS -o /dev/null -w "%{http_code} %{redirect_url}\n" https://replyinmyvoice.com/
curl -sS -o /dev/null -w "%{http_code} %{redirect_url}\n" https://replyinmyvoice.com/pricing
curl -sS -o /dev/null -w "%{http_code} %{redirect_url}\n" https://replyinmyvoice.com/sign-in
curl -sS -o /dev/null -w "%{http_code} %{redirect_url}\n" https://replyinmyvoice.com/privacy
curl -sS -o /dev/null -w "%{http_code} %{redirect_url}\n" https://replyinmyvoice.com/terms
curl -sS -o /dev/null -w "%{http_code}\n" https://replyinmyvoice.com/robots.txt
curl -sS -o /dev/null -w "%{http_code}\n" https://replyinmyvoice.com/sitemap.xml
curl -sS -o /dev/null -w "%{http_code} %{redirect_url}\n" https://replyinmyvoice.com/app
curl -sS -X POST -o /dev/null -w "%{http_code}\n" https://replyinmyvoice.com/api/rewrite
curl -sS -o /dev/null -w "%{http_code}\n" https://replyinmyvoice.com/api/stripe/webhook
curl -sS -o /dev/null -w "%{http_code}\n" https://replyinmyvoice.com/api/health/db
```
