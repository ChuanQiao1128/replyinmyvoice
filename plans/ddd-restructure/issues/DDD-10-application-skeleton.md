# DDD-10: New ReplyInMyVoice.Application project skeleton

## Context
Lay the empty Application-layer project — the root all later backend DDD work builds on. Current
project graph: `Domain` (no deps) ← `Infrastructure` ← `{Api, Functions, Worker}`. The new
`Application` sits above `Domain` and will later be referenced by `Infrastructure` and the
presentation projects. This issue ONLY creates the skeleton; no logic is migrated (that is DDD-20).
Read first: `backend-dotnet/ReplyInMyVoice.sln`,
`backend-dotnet/src/ReplyInMyVoice.Domain/ReplyInMyVoice.Domain.csproj` (TFM/style reference),
`plans/ddd-restructure/REQUIREMENT.md` (§3 target architecture).

## Constraints
- Target framework `net8.0`, matching `ReplyInMyVoice.Domain`. `Nullable` enable, `ImplicitUsings`
  enable. No new NuGet packages. The only ProjectReference is `ReplyInMyVoice.Domain`.
- Create an empty skeleton + a placeholder only — migrate NO logic.
- Do not modify the `Api`, `Functions`, `Worker`, or `Infrastructure` projects.

## Changes required
1. Create `backend-dotnet/src/ReplyInMyVoice.Application/ReplyInMyVoice.Application.csproj`
   (`net8.0`, Nullable+ImplicitUsings enable, `ProjectReference` →
   `..\ReplyInMyVoice.Domain\ReplyInMyVoice.Domain.csproj`).
2. Create directories `Abstractions/`, `UseCases/`, `Common/`, each with one placeholder file so
   the project compiles and is non-empty (e.g. `Common/ApplicationAssemblyMarker.cs` — an empty
   `public static class` in namespace `ReplyInMyVoice.Application`).
3. Add the project to the solution:
   `dotnet sln backend-dotnet/ReplyInMyVoice.sln add backend-dotnet/src/ReplyInMyVoice.Application/ReplyInMyVoice.Application.csproj`.
4. Do NOT add any project's reference TO Application yet (later issues do that).

## Acceptance (machine-checkable)
- `cd backend-dotnet && dotnet build ReplyInMyVoice.sln -c Release` exits 0
- `grep -q "ReplyInMyVoice.Application" backend-dotnet/ReplyInMyVoice.sln`
- `test -f backend-dotnet/src/ReplyInMyVoice.Application/ReplyInMyVoice.Application.csproj`
- `cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release` exits 0 (full suite still green)

## DO NOT
- Do NOT migrate any service/logic into Application (DDD-20 does that).
- Do NOT modify other projects' csproj or add references to Application.
- Do NOT push, open a PR, or touch main.
