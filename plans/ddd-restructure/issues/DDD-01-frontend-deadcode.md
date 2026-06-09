# DDD-01: Delete frontend Slice-7 dead TS + its tests

## Context
Delete the dead TS business logic that the C# backend replaced. The Next.js app is a thin proxy;
these modules are referenced only by tests and the abandoned eval script, never by live app/API
code. Deleting them tightens the frontend/backend boundary.
Read first: `lib/` (inventory), `scripts/eval-scenarios.ts` (confirm it imports the dead modules),
`package.json` (scripts that invoke the dead eval scripts).

## Constraints
- Delete ONLY the dead modules listed below + ONLY the tests that exclusively test them.
- KEEP (still live, do NOT delete): `lib/rewrite-presets.ts`, `lib/rewrite-response.ts`,
  `lib/rewrite-failure-reasons.ts`, `lib/azure-api.ts`, `lib/entra-auth.ts`, `lib/http.ts`, and
  everything under `lib/observability/` (uncertain — leave for a later pass).
- Before deleting each target, `grep -rn` it under `app/ components/` to confirm NO live import
  (only `tests/` or `scripts/` references are allowed). If a target is imported by live code, skip
  it and note it.

## Changes required (delete; verify no live import first)
1. `lib/rewrite-pipeline/` (whole directory).
2. `lib/fact-extraction.ts`, `lib/openai.ts`, `lib/openai-compatible.ts`, `lib/rewrite-diagnosis.ts`,
   `lib/rewrite-eval-cases.ts`, `lib/rewrite-quality-gate.ts`, `lib/scenario-evaluation-regression.ts`.
3. `lib/generated/` (Prisma generated types).
4. `scripts/eval-scenarios.ts`, `scripts/check-scenario-evaluation-regression.ts`.
5. `prisma/` (empty dir).
6. Tests that exclusively test the above (e.g. `tests/unit/rewrite-pipeline*.test.ts`,
   `tests/unit/fact-extraction.test.ts`, and any other test whose only imports are now-deleted
   modules — typecheck will surface dangling imports; delete those tests).
7. `package.json` — remove npm scripts that invoke the deleted eval scripts.

## Acceptance (machine-checkable)
- `npm run typecheck` exits 0
- `npm run test` exits 0
- `npm run build` exits 0
- `! grep -rEl "rewrite-pipeline|fact-extraction|lib/openai|rewrite-diagnosis|lib/generated" app components lib`

## DO NOT
- Do NOT delete `lib/rewrite-presets.ts`, `lib/rewrite-response.ts`, `lib/rewrite-failure-reasons.ts`,
  or any `lib/observability/*`.
- Do NOT change `app/api/**` proxy logic or live infra helpers (`azure-api.ts`, `entra-auth.ts`,
  `http.ts`, `client-azure-api.ts`).
- Do NOT push, open a PR, or touch main.
