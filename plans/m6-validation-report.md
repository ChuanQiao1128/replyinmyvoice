# M6 Validation Report

Updated: 2026-05-23T04:53:03+12:00

## M6-007 Full Validation Suite

Source issue: `M6-007 Full validation suite green`.

Issue command set:

```bash
npm run lint
npm run typecheck
npm run test
npm run test:e2e
npm run build
npm run cf:build
```

## Current Evidence

| Command | Status | Evidence |
| --- | --- | --- |
| `npm run lint` | pass | ESLint exited 0 at 2026-05-23T04:51+12:00. |
| `npm run typecheck` | pass | `tsc --noEmit` exited 0 at 2026-05-23T04:51+12:00. |
| `npm run test` | pass | Vitest passed with 47 test files and 281 tests at 2026-05-23T04:52+12:00. |
| `npm run test:e2e` | blocked | Playwright's web server could not start because this sandbox rejects local listen on `0.0.0.0:3000` with `EPERM`. A minimal Node HTTP server check at 2026-05-23T04:50:52+12:00 also failed to bind `127.0.0.1` with `EPERM`, confirming a runner restriction before route or browser behavior executes. |
| `npm run build` | pass | Next.js production build exited 0 and generated 11 static pages at 2026-05-23T04:52+12:00. |
| `npm run cf:build` | pass | OpenNext Cloudflare build exited 0 and saved `.open-next/worker.js` at 2026-05-23T04:53+12:00. |

## Scope Decision

The earlier `dotnet test backend-dotnet/ReplyInMyVoice.sln --nologo` socket failure is recorded as a separate environment limitation, not as the remaining M6-007 prerequisite. The M6-007 issue brief names the Node/Next validation commands above, and this repair does not touch `backend-dotnet/`; per `plans/codex-implementation-prompt.md`, .NET tests are required when an issue touches `backend-dotnet/`.

## Remaining Prerequisite

Rerun `npm run test:e2e` in a local or CI runner that permits loopback server binding. No live money action, dashboard mutation, npm publish, secret change, or `.env.local` edit is required.
