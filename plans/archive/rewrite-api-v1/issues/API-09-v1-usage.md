# API-09: GET /api/v1/usage (key-authed)

**Tier:** 2 · **Owner:** Codex · **Depends on:** API-02

## Context
- Repo root: `/Users/qc/Desktop/CloudFlare`
- Spec: `plans/rewrite-api-v1/SPEC.md` — §API and Job Contracts (usage).
- `AccountService.GetOrCreateAccountSummaryAsync` already computes quota/used/reserved/remaining (`AccountService.cs:74-161`) — reuse its math; do NOT recompute differently.
- `ApiKeyAuthResolver` (API-02). Add the function to `V1RewriteHttpFunctions` or `ApiKeyHttpFunctions`.

## Constraints (AGENTS.md + SPEC)
- Banned terms: `humanizer|bypass|undetect|detector|evade`. No secrets. No push/touch `main`.

## Changes required
1. `GET /api/v1/usage` (key-authed): resolve the user via `ApiKeyAuthResolver`; compute `{ scope, periodKey, quota, used, remaining, periodEnd }` from the same source as the account summary; return `200`. Missing/invalid key → `401`.
2. Next proxy route `app/api/v1/usage/route.ts` (NO same-origin; forward `Authorization`).

## Acceptance (machine-checkable)
- [ ] xUnit/integration: returned `quota`/`used`/`remaining` equal the backend `UsagePeriod` math for a seeded user; missing/invalid key → `401`.
- [ ] `cd backend-dotnet && dotnet test` green; `npm run typecheck` green.

## Do NOT
- Do NOT recompute quota differently from the website. Do NOT add same-origin to the v1 route.
