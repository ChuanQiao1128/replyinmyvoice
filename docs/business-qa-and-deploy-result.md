# Business QA And Deploy Result

Date: 2026-05-19

## Summary

This run focused on the product-level rewrite workflow after the .NET/Azure backend run:

- real scenario rewrite evaluation
- measured writing-signal reduction
- repair/fallback strategy fixes
- frontend workspace UX fixes
- GitHub Actions CI/CD setup for Cloudflare Worker deployment
- Cloudflare Worker deployment and production-domain smoke tests

## Rewrite Evaluation

Latest full scenario evaluation:

- cases evaluated: 26
- measured final scores: 24
- long cases: 10
- long customer-support cases: 5
- average AI-like signal drop: 49 points
- rewrites below 50% AI-like signal: 16/24
- final selected rewrites worse than draft: 0/24
- case pass count under strict eval: 16/26
- expected facts preserved in final outputs: 26/26

Two cases had unavailable final third-party signal during the latest run, so they were not counted as measured scores.

Command:

```bash
npm run eval:scenarios
```

Report:

```text
docs/scenario-evaluation-results.md
```

## Strategy Fixes Promoted

Implemented fixes:

- Customer-support facts-first fallback now branches by issue type instead of routing every support case to invoice/seat billing.
- Added deterministic support fallbacks for workspace-access and incident-status replies.
- Added plan-change billing fallback that preserves Starter/Team plan, proration, old plan credit, and new plan charge language without using seat-count billing text.
- Added a safety selection rule so measured candidates that worsen the draft are not selected when the original draft preserves all critical facts.
- Made candidate length completeness scenario-specific: long customer-support replies stay strict; long sales/work/blank replies can be compact when they preserve facts.
- Expanded critical-fact extraction for operational phrases such as course policy, old pilot workspace, billing report folder, weekly partner updates, two other vendors, pause the campaign, and 2pm launch check.

Strategy memory updated:

```text
docs/rewrite-strategy-memory.md
```

## Frontend UX Fixes

Implemented:

- `/app` now renders the shared site header, so signed-in users can return home through the logo/navigation.
- Paywall states also include the shared header.
- Workspace has a `New draft` action to reset the current form/result without clearing local history.
- Local rewrite history key was aligned back to the requirements value: `rimv.rewrite.history.v1`.
- Free account status now shows an `Upgrade` button that opens Stripe Checkout.

## CI/CD

Added:

```text
.github/workflows/cloudflare-worker.yml
```

Behavior:

- push / PR: Node install, Prisma generate, typecheck, unit tests, Next build, OpenNext build
- push to `main`: deploy Cloudflare Worker through `npm run cf:deploy`

GitHub Actions secrets and variables were configured for Cloudflare/Next deployment without committing secret values.

Existing Azure workflow remains:

```text
.github/workflows/dotnet-azure.yml
```

## Verification

Local verification passed:

```bash
npm run typecheck
npm run test
npm run lint
npm run build
npm run test:e2e
grep -RniE "humanizer|bypass|undetect|detector|evade" app components public lib || true
```

E2E result:

```text
4 passed
```

Unit test result:

```text
61 passed
```

Cloudflare deploy passed:

```text
https://replyinmyvoice-app.qc1128qc.workers.dev
```

Production domain smoke passed:

- `GET https://replyinmyvoice.com/` returned 200
- `GET https://replyinmyvoice.com/pricing` returned 200
- `GET https://replyinmyvoice.com/sign-in` returned 200
- `GET https://replyinmyvoice.com/sign-up` returned 200
- `GET https://replyinmyvoice.com/app` returned 307 to `/sign-in` for signed-out requests
- `GET https://replyinmyvoice.com/api/health/db` returned `{"ok":true}`
- signed-out same-origin `POST /api/rewrite` returned 401

Azure dev API smoke passed:

- `GET https://replyinmyvoice-api-dev.azurewebsites.net/health` returned 200
- Azure App Service is running
- `httpsOnly` was changed to `true`

## Deployment Notes

Cloudflare/OpenNext deploy failed once when `.env.local` was sourced into the shell because it exported `NODE_ENV` into the OpenNext build. The successful deploy passed only Cloudflare auth variables to the deploy process.

Do not run:

```bash
set -a; source .env.local; set +a; npm run cf:deploy
```

Use GitHub Actions or pass only Cloudflare auth variables.

2026-05-22 deploy-size follow-up:

- `worker.js` remains the Worker entry point because it wraps OpenNext and adds
  the scheduled LearningOps handler.
- `wrangler.jsonc` must keep `"minify": true`.
- Do not re-enable root-wide `find_additional_modules` or broad `**/*.wasm`
  rules. A dry run with those settings attached unrelated repo files, root
  `node_modules`, generated Prisma WASM, and Prisma CLI WASM artifacts, causing
  the Worker package to exceed the Cloudflare size limit.
- Prisma remains schema/migration-only for this Worker runtime; do not copy
  Prisma WASM into `.open-next` unless runtime database access is intentionally
  moved back to a verified Prisma Worker path.

## State Lifecycle Follow-up

Date: 2026-05-21

State-machine review found and fixed quota, reservation, and webhook lifecycle gaps in the .NET backend:

- Expired usage reservations now persist as `Expired`, not generic `Released`.
- Expired cleanup skips rewrite attempts that have already moved to `Processing`, preventing cleanup from releasing quota while a provider call is still in flight.
- Late provider success can no longer move an expired or released reservation into a successful charged attempt.
- ASP.NET API paid quota now matches the product rule and Azure Functions path: 40 rewrites per paid billing period.
- Stripe `customer.subscription.deleted` now maps to `Canceled`, matching the Next/Stripe state semantics instead of collapsing deleted subscriptions into generic `Inactive`.
- The unused rewrite-attempt `Released` state was removed from the .NET enum; release remains a usage-reservation state, while rewrite attempts terminate as `Succeeded`, `Failed`, or `Expired`.

Verification:

```bash
dotnet test backend-dotnet/ReplyInMyVoice.sln --no-restore
npm test -- tests/unit/quota.test.ts tests/unit/stripe-webhook-events.test.ts
```

## Remaining Risks

- Some blank-note and cover-letter cases still preserve facts but do not reduce the third-party signal enough.
- Two latest eval cases had unavailable final third-party signal; future runs should distinguish provider unavailability from rewrite quality failure.
- Authenticated `/app` manual smoke with a real Entra session still needs a human browser check because local automated tests do not have a live user access token.
- Stripe remains sandbox/test mode; no live-mode payment or real charge was performed.

## 2026-05-24 C# Rewrite Backend Migration Slice

This run moved the public rewrite path and remaining public billing/webhook BFF routes toward the all-C#/Azure target while keeping the existing low-cost Azure Functions Consumption + Azure SQL + Service Bus architecture.

Implemented:

- Added C# rewrite-engine deterministic core types for input analysis, fact ledger extraction, strategy routing, budget caps, and structure gates.
- Added a C# `FactReconstructRewriteProvider` that runs draft Sapling signal, model candidate generation, structure gate, final Sapling signal, and no-charge quality failure handling.
- Added OpenAI-compatible/DeepSeek and Sapling HTTP adapters in the .NET backend.
- Updated .NET DI so configured model + Sapling keys use the C# fact-reconstruct provider; local no-key runs keep deterministic fallback.
- Changed Cloudflare `/api/rewrite` from in-process TS rewrite execution to an Azure Functions proxy with idempotency-key generation and short attempt polling.
- Changed Cloudflare Stripe checkout and webhook routes to Azure Functions proxies.
- Changed `/app` to read Azure account/quota state instead of TS/Prisma user/quota helpers.
- Removed generated Prisma client type dependencies from remaining legacy TS helpers so the app can typecheck without regenerating Prisma client.

Verification:

```text
dotnet test backend-dotnet/ReplyInMyVoice.sln --no-restore: 60 passed
dotnet build backend-dotnet/ReplyInMyVoice.sln --no-restore: passed
npm run lint: passed
npm run typecheck: passed
npm test: 51 files / 325 tests passed
npm run build: passed
npm run cf:build: passed
restricted-term scan over app/components/public/lib/scripts/tests/backend-dotnet: no matches
dotnet publish ReplyInMyVoice.Functions Release: passed
```

Deployment:

```text
Azure Functions deploy: passed, https://replyinmyvoice-func-dev.azurewebsites.net
Azure GET /api/health: 200
Azure GET /api/health/db: 200, Azure SQL
Azure POST /api/rewrite without auth: 401
```

Cloudflare local deploy note:

```text
npm run cf:deploy built successfully but local wrangler deploy stopped because CLOUDFLARE_API_TOKEN is not present in this non-interactive shell. Use GitHub Actions main-branch deployment secrets for the Cloudflare production deploy path.
```

Limitations:

- The C# rewrite provider now owns the public runtime path, but legacy TS rewrite, learningops, and observability files remain for historical tests/admin cleanup. They should be removed in a follow-up cleanup slice after their admin/reporting replacements are confirmed.
- No live signed-in browser rewrite was run from this Codex session because no authenticated user access token was available.
- No Stripe live-mode payment or real charge was performed.

## 2026-05-23 Azure Backend Cutover QA

Cloudflare production Worker now routes public backend work to Azure Functions and Azure SQL.

Passed verification:

- `dotnet test backend-dotnet/ReplyInMyVoice.sln --no-restore`: 47 passed
- `npm run lint`: passed
- `npm run typecheck`: passed
- `npm run test`: 48 files / 313 tests passed
- `npm run cf:deploy`: deployed Worker version `19086619-cd41-48e9-bb8d-d64deaa76dd1`
- Azure SQL migration: already up to date
- Azure Functions deploy: passed

Remote smoke passed:

- `GET https://replyinmyvoice-func-dev.azurewebsites.net/api/health`: 200
- `GET https://replyinmyvoice-func-dev.azurewebsites.net/api/health/db`: 200, Azure SQL
- `GET https://replyinmyvoice-func-dev.azurewebsites.net/api/me` without auth: 401
- `POST https://replyinmyvoice-func-dev.azurewebsites.net/api/rewrite` without auth: 401
- Azure CORS preflight from `https://replyinmyvoice.com`: 204 with `authorization`, `content-type`, and `x-idempotency-key`
- `GET https://replyinmyvoice.com/`: 200
- `GET https://replyinmyvoice.com/pricing`: 200
- `GET https://replyinmyvoice.com/app` signed out: 307 to sign-in
- `GET https://replyinmyvoice.com/api/health/db`: 200, Azure SQL
- `POST https://replyinmyvoice.com/api/rewrite` signed out: 401
- `GET https://replyinmyvoice.com/api/stripe/webhook`: reports `backend:"azure-functions"`

Deployment note:

- Codex desktop preview repeatedly auto-started `next dev` / `next start`, which can race with OpenNext writes to `.next`. For local deployment from Codex, run Cloudflare build/deploy with a short watchdog that stops those preview processes during the build. Normal CI should not need this workaround.
