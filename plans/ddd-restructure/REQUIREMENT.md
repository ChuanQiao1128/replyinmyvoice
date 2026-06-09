# REQUIREMENT — Backend DDD restructure + frontend/backend separation cleanup

**Status:** Wave 1 ready for unattended delivery (dynamic-delivery-workflow)
**Author:** Claude (supervisor), 2026-06-09
**Integration branch:** `delivery/ddd-restructure` (NEVER main)
**Owner authorization:** full autonomous run, decompose-then-run, no per-issue review (2026-06-09).

---

## 1. Goal

Two owner goals, translated into concrete engineering work:

1. **"前后端分离" (frontend/backend separation)** — the Next.js app is ALREADY a thin proxy
   layer (all 51 `app/api/**` routes forward to the Azure C# backend via
   `NEXT_PUBLIC_AZURE_API_BASE_URL`; Prisma/Clerk/Neon already removed from `package.json`).
   The remaining work is **deleting the Slice-7 dead TS business logic** still sitting in
   `lib/` and tightening the boundary so the frontend holds only proxy/auth/observability code.

2. **"后端采用 DDD" (backend Domain-Driven Design)** — the backend is already layered and the
   Domain project is already pure (zero infra deps; entities + RewriteEngine + Quality gates).
   What is missing for full classic DDD:
   - **No Application layer** — use-case orchestration is scattered across `Api/Program.cs`
     (1559 lines inline), `Functions/*` (22 function classes touching `AppDbContext` directly),
     and `Infrastructure/Services/*` (24 services mixing orchestration with I/O).
   - **No repository abstraction** — `AppDbContext` is injected/used directly everywhere.
   - **Duplicated dual HTTP entry points** — `Api` (ASP.NET Core) and `Functions` (Azure
     Functions) both reimplement every endpoint.

3. **"整理项目结构" (tidy the project)** — remove dead code, archive stale one-off plan dirs,
   and record the target architecture in an ADR.

## 2. Current state (verified 2026-06-09)

- **Frontend:** thin proxy confirmed. Backend base URL via `lib/azure-api.ts::getAzureApiBaseUrl()`
  → `replyinmyvoice-func-dev.azurewebsites.net`. Dead TS still present: `lib/rewrite-pipeline/`
  (16 files), `lib/fact-extraction.ts`, `lib/openai.ts`, `lib/openai-compatible.ts`,
  `lib/rewrite-diagnosis.ts`, `lib/rewrite-eval-cases.ts`, `lib/rewrite-quality-gate.ts`,
  `lib/scenario-evaluation-regression.ts`, `lib/generated/` (Prisma), `scripts/eval-scenarios.ts`,
  `scripts/check-scenario-evaluation-regression.ts`, empty `prisma/`. Only referenced by tests +
  the dead eval script. KEEP (still live): `lib/rewrite-presets.ts`, `lib/rewrite-response.ts`,
  `lib/rewrite-failure-reasons.ts`.
- **Backend project graph:** `Domain` (no deps) ← `Infrastructure` ← `{Api, Functions, Worker}`.
  `Domain` is pure (no NuGet packages). Production HTTP entry = **Azure Functions** (deployed by
  `.github/workflows/dotnet-azure.yml`, gated `if: github.ref == 'refs/heads/main'`); `Api` is NOT
  deployed to production.
- **Sizes:** `Infrastructure/Services` 9.7k LOC (incl. `StripeEventService` 1388, `AdminService`
  1406, `PromoAdminService` 722, `AccountService` 663, `QuotaService` 624, `RewriteJobProcessor`
  583, `PromoService` 554, `StripeBillingService` 494). `Functions` 4.5k. `Api/Program.cs` 1559.
  Tests 19.8k (67 files).

## 3. Target architecture (full classic DDD)

```
Presentation:   ReplyInMyVoice.Functions  (single production HTTP/timer entry — thin shells)
                ReplyInMyVoice.Api         (local/integration-test host — thin shells, same logic)
                ReplyInMyVoice.Worker      (background host — thin shells)
                     │ depends on
                     ▼
Application:    ReplyInMyVoice.Application  (NEW)
                  - UseCases/  (command + query handlers; CQRS-lite)
                  - Abstractions/  (repository interfaces + IUnitOfWork)
                  - Common/  (result types, DTOs)
                     │ depends on
                     ▼
Domain:         ReplyInMyVoice.Domain  (UNCHANGED — entities, value objects, enums,
                  domain services: RewriteEngine, Quality gates)
                     ▲ implemented by
Infrastructure: ReplyInMyVoice.Infrastructure  (depends on Application + Domain)
                  - Data/  (AppDbContext — the UoW substrate)
                  - Repositories/  (repository + UnitOfWork IMPLEMENTATIONS)  ← NEW
                  - Providers/ Queueing/ Notifications/ Stripe (unchanged)
```

Dependency rule: `Domain ← Application ← Infrastructure`; Presentation depends on Application and
wires Infrastructure only at the composition root (DI).

## 4. Key decisions

- **D1 — Single production entry = Azure Functions.** Do NOT change the deploy target. `Api` is
  kept as a local/integration-test host, thinned to call the same Application handlers, so the
  duplicated logic is eliminated (logic lives once, in Application) without breaking the
  `WebApplicationFactory` integration tests or the deploy pipeline.
- **D2 — Repository interfaces in `Application/Abstractions`**, implementations in
  `Infrastructure/Repositories`, backed by `AppDbContext`. `IUnitOfWork` wraps
  `AppDbContext.SaveChangesAsync`. Interfaces are added **incrementally per bounded context**
  (not all 25 aggregates at once) — only what each migrated use-case needs.
- **D3 — Strangler migration (add-then-replace).** For each bounded context: (a) add the
  Application handler using repositories; (b) LEAVE the old `Infrastructure/Services` service in
  place so the build never breaks; (c) switch the entry point (Functions/Api) to call the new
  handler; (d) a later cleanup issue removes the now-dead old service. **Every issue leaves the
  tree green and is independently mergeable into the integration branch.**
- **D4 — No database schema change, no new EF migration.** This is a pure code-layering
  reorganization. (Keeps the eventual main cutover from running a live Azure SQL migration.)
- **D5 — Tests move/adjust with the code; the full `dotnet test` and `npm run test` suites stay
  green on every PR.** Never delete a test to make a diff pass (only update or relocate it).

## 5. Inherited safety constraints (every issue)

- Merge only into `delivery/ddd-restructure`. **Never push to / merge / touch `main`.** No deploy
  commands.
- Banned terms (CI grep guard) never added under `app components public lib`:
  `humanizer|bypass|undetect|detector|evade`.
- Payments stay sandbox-only — never a real charge. No secret values in tracked files (runtime
  env validation only). Do not modify `LAUNCH_CONFIRMED`, Stripe live keys/price/webhook secret,
  or DNS.
- No test-gutting (`@ts-ignore`, `eslint-disable`, loosened tsconfig/csproj).

## 6. Phasing

Full classic DDD over a ~16k-LOC backend is a **~40-issue, multi-day** effort. It is delivered as
**rolling waves**, each a coherent stage, with the supervisor advancing on the `wave-done` event:

- **Wave 1 (this document + queue):** lay the Application + repository foundation, prove the
  migration pattern end-to-end on ONE real bounded context (Rewrite create/get), plus frontend
  dead-code deletion and the ADR. 7 issues. ← **starting now, unattended.**
- **Wave 2+ (decomposed after Wave 1 proves the pattern):** migrate the remaining contexts
  (Account, Quota, Promo, Billing, Stripe, ApiKey, Admin, BillingSupport, Webhook/Outbox, the
  rewrite job processor), thin out all Functions/Api entry points, thin the Worker, then the
  cleanup pass that deletes the strangled old services. Each context follows the playbook
  (`docs/ddd-migration-playbook.md`) written in Wave 1 (DDD-30).

Project tidy of TRACKED stale plan dirs and UNTRACKED screenshot/dup residue is handled by the
supervisor directly (lower risk than handing path-judgement to Codex) and reported at the end.

## 7. Wave 1 issue breakdown

| TAG | Title | Tier | Deps | Timeout | Scope (paths) |
|---|---|---|---|---|---|
| DDD-10 | New `ReplyInMyVoice.Application` project skeleton | 1 | — | 40 | `backend-dotnet/ReplyInMyVoice.sln`, `backend-dotnet/src/ReplyInMyVoice.Application/**` (new) |
| DDD-11 | Application: repository interfaces + `IUnitOfWork` (rewrite + core aggregates) | 1 | DDD-10 | 60 | `backend-dotnet/src/ReplyInMyVoice.Application/Abstractions/**` |
| DDD-12 | Infrastructure: repository + UoW implementations + DI wiring | 1 | DDD-11 | 75 | `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Repositories/**`, `ServiceCollectionExtensions.cs`, `ReplyInMyVoice.Infrastructure.csproj` |
| DDD-20 | Pattern migration: Rewrite create/get use-cases → Application handlers (strangler) | 1 | DDD-12 | 75 | `backend-dotnet/src/ReplyInMyVoice.Application/UseCases/Rewrite/**`, `ServiceCollectionExtensions.cs`, test project |
| DDD-21 | Functions: rewrite create/get entry shelled onto the new handlers | 2 | DDD-20 | 75 | `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/RewriteHttpFunctions.cs` |
| DDD-30 | ADR-0002 (DDD layering) + migration playbook | 2 | DDD-12 | 20 | `docs/architecture-decision-record-0002-ddd-layering.md`, `docs/ddd-migration-playbook.md` |
| DDD-01 | Delete frontend Slice-7 dead TS + its tests | 2 | — | 40 | `lib/**`, `scripts/**`, `prisma/`, `tests/unit/**` (delete-only per brief list) |

**Canary = DDD-10** (first row): it exercises the .NET build + `.sln` edit + `dotnet test` gate —
the riskiest systemic path. If it fails systemically the wave auto-pauses before burning Codex on
the rest.

**Queue order (dependency-first):** DDD-10, DDD-11, DDD-12, DDD-20, DDD-21, DDD-30, DDD-01.

## 8. Gates on every PR (enforced by the daemon, independent of what Codex reports)

- Backend diff → `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release` green.
- Frontend diff → `npm run typecheck` + `npm run test` green.
- Diff-scoped banned-term gate over `app components public lib` prints nothing.
- Secret/suppression scan: no secret values, no `@ts-ignore`/`eslint-disable`/loosened configs.

## 9. Excluded — owner-only (never queued)

- Merging `delivery/ddd-restructure` → `main` (auto-deploys prod; owner's call).
- Any live charge, DNS change, secret provisioning, or `LAUNCH_CONFIRMED`/`*_LIVE_*` flag change.
