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

## Remaining Risks

- Some blank-note and cover-letter cases still preserve facts but do not reduce the third-party signal enough.
- Two latest eval cases had unavailable final third-party signal; future runs should distinguish provider unavailability from rewrite quality failure.
- Authenticated `/app` manual smoke with a real Clerk session still needs a human browser check because local automated tests do not have a Clerk test login session.
- Stripe remains sandbox/test mode; no live-mode payment or real charge was performed.
