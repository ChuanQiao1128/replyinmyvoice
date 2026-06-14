# HARD-12: CI migration guard: expand/contract discipline

> Priority: P2 · Adversarial verdict: **PASS** · Migration: No
> Part of the production-hardening wave. Read `../SPEC.md` first for the engine-swap-boundary principle and the frozen external contract.

## Summary

Add a diff-scoped CI migration-discipline guard to dotnet-azure.yml: a new step in the sqlserver-migration job (before "Apply migrations to SQL Server") collects migration .cs files ADDED in the PR/push diff and feeds them to a new small C# console tool, backend-dotnet/tools/ReplyInMyVoice.MigrationGuard, which scans only the Up() region for destructive operations (DropTable, DropColumn, RenameColumn, RenameTable, and AlterColumn that changes CLR/store type, narrows maxLength, or tightens nullability) and fails CI unless the migration file carries an explicit "// MIGRATION-RISK-ACCEPTED: <reason>" comment. Ships with xUnit tests (including a test proving zero findings on all 25 existing migrations) and docs/migration-discipline.md describing the expand/contract workflow. No schema change, no engine/boundary contact, no frontend contact.

## Files to create / modify

- CREATE /Users/qc/Desktop/CloudFlare/backend-dotnet/tools/ReplyInMyVoice.MigrationGuard/ReplyInMyVoice.MigrationGuard.csproj — net8.0 console exe, no package refs, no project refs (mirror tools/ReplyInMyVoice.Eval/ReplyInMyVoice.Eval.csproj properties: OutputType Exe, TargetFramework net8.0, ImplicitUsings+Nullable enable)
- CREATE /Users/qc/Desktop/CloudFlare/backend-dotnet/tools/ReplyInMyVoice.MigrationGuard/MigrationDisciplineScanner.cs — all scan logic as a pure static class (unit-testable, no IO)
- CREATE /Users/qc/Desktop/CloudFlare/backend-dotnet/tools/ReplyInMyVoice.MigrationGuard/Program.cs — thin CLI: reads file paths from args, skips *.Designer.cs and AppDbContextModelSnapshot.cs, prints findings + GitHub ::error annotations, exit codes 0/1/2
- MODIFY /Users/qc/Desktop/CloudFlare/backend-dotnet/ReplyInMyVoice.sln — add ReplyInMyVoice.MigrationGuard under the existing 'tools' solution folder (GUID {22F11C5A-384B-4C5B-A191-BE80688B2895}) via `dotnet sln add ... --solution-folder tools` so build-test compiles it
- MODIFY /Users/qc/Desktop/CloudFlare/backend-dotnet/tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj — add <ProjectReference Include="..\..\tools\ReplyInMyVoice.MigrationGuard\ReplyInMyVoice.MigrationGuard.csproj" /> next to the existing Eval reference (line 31)
- CREATE /Users/qc/Desktop/CloudFlare/backend-dotnet/tests/ReplyInMyVoice.Tests/MigrationDisciplineScannerTests.cs — xUnit + FluentAssertions tests with inline raw-string fixtures (do NOT add fixture .cs files under the test tree; they would be compiled)
- MODIFY /Users/qc/Desktop/CloudFlare/.github/workflows/dotnet-azure.yml — sqlserver-migration job: add fetch-depth: 0 to its checkout (line 53) and insert the 'Migration discipline guard (expand/contract)' step after setup-dotnet, before 'Restore'/'Apply migrations to SQL Server'
- CREATE /Users/qc/Desktop/CloudFlare/docs/migration-discipline.md — expand/contract doc + marker semantics + local run instructions

## Implementation approach

1. Create the tool project `backend-dotnet/tools/ReplyInMyVoice.MigrationGuard/ReplyInMyVoice.MigrationGuard.csproj`: `<Project Sdk="Microsoft.NET.Sdk">` with OutputType=Exe, TargetFramework=net8.0, ImplicitUsings=enable, Nullable=enable, RootNamespace `ReplyInMyVoice.MigrationGuard`, zero PackageReference/ProjectReference (copy the property block of /Users/qc/Desktop/CloudFlare/backend-dotnet/tools/ReplyInMyVoice.Eval/ReplyInMyVoice.Eval.csproj).

2. Implement `MigrationDisciplineScanner.cs` (namespace ReplyInMyVoice.MigrationGuard), public API:
   - `public sealed record MigrationFinding(string Operation, int LineNumber, string Reason);`
   - `public sealed record MigrationScanResult(string FilePath, IReadOnlyList<MigrationFinding> Findings, bool HasRiskMarker, string? MarkerReason) { public bool IsViolation => Findings.Count > 0 && !HasRiskMarker; }`
   - `public static MigrationScanResult ScanFile(string filePath, string content)`.
   Algorithm:
   a. RISK MARKER (scan raw content, whole file): multiline regex `^\s*//\s*MIGRATION-RISK-ACCEPTED:\s*(?<reason>\S.*)$`. HasRiskMarker=true only when the reason group is non-empty after Trim(); capture MarkerReason. A bare `// MIGRATION-RISK-ACCEPTED:` with no reason does NOT count (test #14).
   b. UP REGION: find `protected\s+override\s+void\s+Up\s*\(` via Regex; region = from end of that match to the start of `protected\s+override\s+void\s+Down\s*\(` (first occurrence after Up) or end of file. If no Up signature exists, scan the WHOLE file (conservative) — EF scaffolds always order Up before Down, verified across all 25 existing migrations.
   c. Within the Up region, strip line comments first (per line, text from the first `//` that is not inside a double-quoted string literal) so commented-out code never flags, then match destructive invocations:
      - Always destructive: regex `migrationBuilder\s*\.\s*(DropTable|DropColumn|RenameColumn|RenameTable)\s*\(` → Finding(op, lineNumber, "destructive operation in Up()").
      - Conditionally destructive: regex `migrationBuilder\s*\.\s*AlterColumn\s*(?:<(?<clr>[^>]+)>)?\s*\(` — NOTE the optional generic argument; a plain `AlterColumn\(` regex misses every real scaffold (`AlterColumn<string>(`). For each match, extract the full argument block with a balanced-parenthesis walker that is string-literal aware (state machine tracking inString + `\\` escape so `typeof(string)` nested parens and `"nvarchar(max)"` parens inside literals are handled — test #16). Parse named args from the block with regexes: `oldClrType:\s*typeof\((?<t>[^)]+)\)`, `(^|[\s(,])type:\s*"(?<v>[^"]*)"`, `oldType:\s*"(?<v>[^"]*)"`, `(^|[\s(,])maxLength:\s*(?<n>\d+)`, `oldMaxLength:\s*(?<n>\d+)`, `(^|[\s(,])nullable:\s*(?<b>true|false)`, `oldNullable:\s*(?<b>true|false)`. AlterColumn is DESTRUCTIVE iff ANY of:
        (i) generic CLR arg and oldClrType both present and trimmed-ordinal unequal → "CLR type change";
        (ii) `type:` and `oldType:` both present and trimmed OrdinalIgnoreCase unequal → "store type change"; if exactly ONE of the pair is present → "store type indeterminate" (conservative; never occurs in the 25 existing files — both always carry the pair);
        (iii) maxLength and oldMaxLength both present and new < old → "max length narrowed";
        (iv) `nullable: false` AND `oldNullable: true` → "nullability tightened (NOT NULL on existing nullable column)".
        Otherwise clean — this exact rule set yields ZERO findings on the two real Up() AlterColumns (20260520010531:14-20 and 20260530142637:14-20: identical type/oldType, nullable loosening), which is what makes the all-existing-migrations test pass.
   d. Wording rule: messages use "destructive"/"flagged"/"guard"/"scanner" vocabulary only. The five banned substrings (per CLAUDE.md) must not appear in any source, message, or doc.

3. Implement `Program.cs`: args = file paths. For each arg: silently skip paths ending `.Designer.cs` or whose filename is `AppDbContextModelSnapshot.cs`; missing file → print `::error::migration guard: file not found {path}` and exit 2. Scan the rest. For each violating file print one line per finding `"{path}({line}): {Operation}: {Reason}"` plus a GitHub annotation `::error file={path},line={line}::Migration discipline: {Operation} — {Reason}. Add '// MIGRATION-RISK-ACCEPTED: <reason>' after review, see docs/migration-discipline.md`. Files with findings AND a valid marker print a `::notice` with the marker reason and pass. Exit 1 if any IsViolation, else 0. No args → print "migration guard: no migration files supplied" and exit 0.

4. Add the project to the solution: `dotnet sln /Users/qc/Desktop/CloudFlare/backend-dotnet/ReplyInMyVoice.sln add backend-dotnet/tools/ReplyInMyVoice.MigrationGuard/ReplyInMyVoice.MigrationGuard.csproj --solution-folder tools` (run from repo root; verify the project lands under solution-folder GUID {22F11C5A-384B-4C5B-A191-BE80688B2895} like ReplyInMyVoice.Eval).

5. Add `<ProjectReference Include="..\\..\\tools\\ReplyInMyVoice.MigrationGuard\\ReplyInMyVoice.MigrationGuard.csproj" />` to /Users/qc/Desktop/CloudFlare/backend-dotnet/tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj (ItemGroup at lines 26-31).

6. Write `MigrationDisciplineScannerTests.cs` (tests listed in the tests field). Fixtures are inline C# raw string literals (\"\"\"...\"\"\") modeled on real scaffolds — never separate .cs files inside the test tree. The all-existing-migrations test locates the migrations directory deterministically: walk parent directories upward from AppContext.BaseDirectory until a directory containing `ReplyInMyVoice.sln` is found, then append `src/ReplyInMyVoice.Infrastructure/Migrations`; enumerate `*.cs` excluding `*.Designer.cs` and `AppDbContextModelSnapshot.cs` (expect exactly 25 — assert count >= 25 so future additive migrations don't break it); assert every ScanFile result has Findings.Count == 0. Pure in-memory tests — serial-safe under the assembly-wide parallelization-off setting.

7. Modify /Users/qc/Desktop/CloudFlare/.github/workflows/dotnet-azure.yml, `sqlserver-migration` job only:
   a. Line 53 checkout gains `with: fetch-depth: 0` (full history needed for merge-base; actions/checkout fetch-depth 0 fetches all branches).
   b. Insert a new step named `Migration discipline guard (expand/contract)` immediately after the setup-dotnet step (line 54-56) and before `Restore` — i.e. structurally before the 'Apply migrations to SQL Server' gate. Step env: `GITHUB_EVENT_BEFORE: ${{ github.event.before }}`. Step run (bash, set -euo pipefail):
      - Resolve diff base: if `$GITHUB_EVENT_NAME` == pull_request → `git fetch origin "$GITHUB_BASE_REF" --quiet || true; BASE="$(git merge-base "origin/$GITHUB_BASE_REF" HEAD)"`; elif `$GITHUB_EVENT_BEFORE` is non-empty, not 40 zeros, and `git cat-file -e "$GITHUB_EVENT_BEFORE^{commit}"` → `BASE="$GITHUB_EVENT_BEFORE"`; else (new-branch push / force-push with unreachable before) `git fetch origin main --quiet || true; BASE="$(git merge-base origin/main HEAD || true)"`. If BASE empty → echo skip notice, exit 0.
      - Collect ONLY newly added migration sources: `git diff --name-only --diff-filter=A "$BASE"..HEAD -- backend-dotnet/src/ReplyInMyVoice.Infrastructure/Migrations/ | grep -E '\\.cs$' | grep -vE '\\.Designer\\.cs$' | grep -v 'AppDbContextModelSnapshot\\.cs' || true` into an array via `mapfile -t` (ubuntu bash >= 4; this script is CI-only — local users run the dotnet tool directly per the doc).
      - If array empty → echo "No newly added migrations in this diff." and exit 0.
      - Else `dotnet run --project backend-dotnet/tools/ReplyInMyVoice.MigrationGuard/ReplyInMyVoice.MigrationGuard.csproj --configuration Release -- "${NEW_MIGRATIONS[@]}"` (tool has no project refs so this restore/build is fast). Non-zero exit fails the job, which deploy `needs:`, so a destructive unmarked migration can never reach `dotnet ef database update`.
   c. Touch nothing else in the workflow: build-test, deploy, health/version gates stay byte-identical.

8. Write /Users/qc/Desktop/CloudFlare/docs/migration-discipline.md covering: why this exists (merging to main auto-applies EF migrations to live Azure SQL via the deploy job, so destructive DDL is irreversible); the expand→migrate→contract pattern (expand = additive-only PR: new nullable columns/tables/indexes + dual-read/write code; migrate = backfill; contract = a LATER separate PR that drops/renames the old shape once no deployed code reads it, carrying the marker); exactly what the guard flags (the Up()-only op list and the four AlterColumn conditions above) and what it deliberately ignores (Down() bodies — rollback is inherently destructive; *.Designer.cs and the model snapshot; DropIndex — index drops are performance-scoped, not data-destructive; existing/modified migrations — diff-scoped to ADDED files only); the marker contract: a single line `// MIGRATION-RISK-ACCEPTED: <non-empty reason>` anywhere in the migration file, reason mandatory, reviewer must verify data is unreferenced/backed up; local usage: `dotnet run --project backend-dotnet/tools/ReplyInMyVoice.MigrationGuard -- <migration .cs files>`.

9. Verify locally before PR: `dotnet build backend-dotnet/ReplyInMyVoice.sln --configuration Release`; `dotnet test backend-dotnet/ReplyInMyVoice.sln --configuration Release`; run the tool over ALL existing migrations and assert exit 0; run the CLAUDE.md banned-term grep.

## Acceptance criteria (machine-checkable)

- `dotnet build /Users/qc/Desktop/CloudFlare/backend-dotnet/ReplyInMyVoice.sln --configuration Release` succeeds and compiles ReplyInMyVoice.MigrationGuard (project present in the sln under the tools folder: `grep -c MigrationGuard backend-dotnet/ReplyInMyVoice.sln` >= 2)
- `dotnet test /Users/qc/Desktop/CloudFlare/backend-dotnet/ReplyInMyVoice.sln --configuration Release` passes with all MigrationDisciplineScannerTests green and zero failures in the pre-existing suite (AdminRouteMetadataTests and InfrastructureServiceCollectionTests untouched and green)
- Zero false positives proven empirically: `dotnet run --project backend-dotnet/tools/ReplyInMyVoice.MigrationGuard --configuration Release -- backend-dotnet/src/ReplyInMyVoice.Infrastructure/Migrations/*.cs` exits 0 against every checked-in migration (Designer/snapshot files skipped by the tool itself)
- True-positive CLI check: writing a temp file whose Up() contains `migrationBuilder.DropColumn(` and running the tool on it exits 1 and prints a `::error file=` annotation; appending the line `// MIGRATION-RISK-ACCEPTED: contract phase, column unused since vX` to the same file makes the tool exit 0
- Running the tool with no arguments exits 0
- Workflow wiring: in .github/workflows/dotnet-azure.yml the sqlserver-migration job checkout has `fetch-depth: 0`, a step named 'Migration discipline guard (expand/contract)' exists and appears earlier in the job's steps list than 'Apply migrations to SQL Server', and the step uses `git diff --name-only --diff-filter=A` scoped to backend-dotnet/src/ReplyInMyVoice.Infrastructure/Migrations/ (verify by grep); build-test and deploy job bodies are unchanged (git diff shows no hunks in those jobs)
- On the PR for this change itself, the dotnet-azure workflow's sqlserver-migration job runs the new guard step and it passes with 'No newly added migrations in this diff.' (the PR adds no migration files)
- Banned-term gate clean: `grep -RniE "humanizer|bypass|undetect|det.ctor|evade" backend-dotnet/tools/ReplyInMyVoice.MigrationGuard backend-dotnet/tests/ReplyInMyVoice.Tests/MigrationDisciplineScannerTests.cs docs/migration-discipline.md .github/workflows/dotnet-azure.yml` returns no matches
- No EF migration added: `git diff --name-only` contains no files under backend-dotnet/src/ReplyInMyVoice.Infrastructure/Migrations/
- docs/migration-discipline.md exists and contains the literal marker string `MIGRATION-RISK-ACCEPTED`, the words 'expand' and 'contract', and the local `dotnet run --project backend-dotnet/tools/ReplyInMyVoice.MigrationGuard` invocation

## Tests

- MigrationDisciplineScannerTests.Scan_AllCheckedInMigrations_HaveNoDestructiveFindings — enumerates the real backend-dotnet/src/ReplyInMyVoice.Infrastructure/Migrations/*.cs (excluding Designer/snapshot, asserts >= 25 files found via the walk-up-to-sln locator) and asserts every file scans with Findings.Count == 0 (the literal zero-false-positive guarantee)
- Scan_DropTableInUp_IsFlagged — inline fixture with DropTable in Up() → one finding, IsViolation true
- Scan_DropColumnInUp_IsFlagged — same for DropColumn
- Scan_RenameColumnInUp_IsFlagged — same for RenameColumn
- Scan_RenameTableInUp_IsFlagged — same for RenameTable
- Scan_DropOperationsInDownOnly_AreClean — canonical scaffold shape (CreateTable in Up, DropTable/DropColumn in Down) → zero findings
- Scan_AlterColumnLooseningNullability_IsClean — fixture copied from 20260530142637 Up() (same type/oldType, nullable: true, no oldNullable) → zero findings
- Scan_AlterColumnClrTypeChange_IsFlagged — AlterColumn<int> with oldClrType: typeof(string) → finding 'CLR type change'
- Scan_AlterColumnStoreTypeChange_IsFlagged — type: "nvarchar(100)" vs oldType: "nvarchar(max)" → finding 'store type change'
- Scan_AlterColumnMaxLengthNarrowed_IsFlagged — maxLength: 64 with oldMaxLength: 256 → finding 'max length narrowed'
- Scan_AlterColumnNullabilityTightened_IsFlagged — nullable: false with oldNullable: true → finding 'nullability tightened'
- Scan_AlterColumnMissingOldTypePair_IsFlagged — type: present without oldType: → conservative finding
- Scan_StringLiteralParensInsideAlterColumn_AreParsedCorrectly — args containing "nvarchar(max)" and typeof(string) parse to the correct argument block and a loosening change stays clean
- Scan_DestructiveOpWithRiskMarker_IsAccepted — DropColumn in Up + '// MIGRATION-RISK-ACCEPTED: contract phase of HARD-12' → Findings non-empty, HasRiskMarker true, MarkerReason captured, IsViolation false
- Scan_RiskMarkerWithoutReason_IsNotAccepted — bare '// MIGRATION-RISK-ACCEPTED:' line → IsViolation stays true
- Scan_CommentedOutDropColumn_IsClean — '// migrationBuilder.DropColumn(...)' line in Up() → zero findings
- Scan_FileWithoutUpMethod_IsScannedWhole — content with DropTable but no Up()/Down() signatures → flagged (conservative whole-file scan)

## Risks

- Diff-base resolution on push events: github.event.before is unreliable after force-pushes/rebases on codex/** branches; the design falls back to merge-base with origin/main and, if unresolvable, skips with exit 0 — a pathological history could therefore skip the guard on a push (it still runs on the PR toward main, which is the gate that matters for deploy).
- The guard scans only files ADDED in the diff (per mandate). Editing an EXISTING migration in place — itself an anti-pattern since prod has applied it — slips through; flagged under notes as follow-up.
- Raw SQL escape hatch: destructive DDL inside migrationBuilder.Sql("ALTER TABLE ... DROP ...") is not scanned (out of mandate scope); documented in migration-discipline.md as a review responsibility.
- AlterColumn 'safety' heuristics are textual, not semantic: a widening that EF expresses with differing type strings (nvarchar(50)→nvarchar(200), int→bigint) is conservatively flagged and needs the marker — intentional (size-of-data rewrites/locks are themselves risky on live SQL), but it adds one marker line of friction to genuinely safe widenings.
- The all-existing-migrations xUnit test locates the repo via walk-up-to-ReplyInMyVoice.sln from AppContext.BaseDirectory; if test execution ever runs from outside the repo checkout (not the case in CI or local dotnet test) it would fail to locate the directory — the test should fail loudly with a clear message rather than silently pass on an empty enumeration (assert file count >= 25).
- mapfile in the CI step requires bash >= 4: fine on ubuntu-latest, but the inline script is not runnable on macOS default bash 3.2 — docs point local users at the dotnet tool directly instead.
- dotnet run in the sqlserver-migration job adds ~30-60s (tool restore+build); acceptable since the job already waits ~60s+ for SQL Server startup.
- fetch-depth: 0 on the sqlserver-migration checkout increases clone time slightly on this small repo; no other jobs are touched.
- Unknown (not runtime-verified): whether github.event.before is populated for the workflow's `codex/**` push triggers in this repo's actual usage — the cat-file existence guard makes the worst case a fallback to merge-base, never a hard failure.

## Design notes

Verified ground truth used by this design: all Drop*/Rename* calls in the 25 existing migrations live exclusively in Down() (awk scan over Up regions found zero), and the only two Up() AlterColumns (20260520010531_AddOutboxAndStripeEventStatus.cs:14, 20260530142637_AddContentRetentionAndConsent.cs:14) are pure loosenings with identical type/oldType — so the heuristic rule set achieves literal zero findings on the existing corpus, and the regex MUST allow the generic form AlterColumn<T>( (a plain 'AlterColumn(' pattern matches nothing real). Engine-swap safety (constraint 5): this item touches only CI, a standalone tool, tests, and docs — no contact with IRewriteEngineClient, providers, DI, or any runtime code path. Adjacent problems intentionally left out of scope: (1) modifications to already-applied migration files are not flagged (worth a follow-up M-status check with a stricter rule: any edit to an existing migration is an error); (2) destructive raw SQL via migrationBuilder.Sql is unscanned; (3) the deploy job's prod `dotnet ef database update` has no independent guard — it relies on `needs: sqlserver-migration`, which is correct today but would silently lose protection if someone removed that needs edge; (4) DropIndex is deliberately allowed (performance-scoped), document if contested. Naming discipline: all new code/docs use 'guard/scanner/flagged/destructive' vocabulary — none of the five CI-banned substrings.

## Reviewer notes (non-blocking, for context)

NON-BLOCKING:
1) Repo already has a SUPERIOR in-repo pattern the design ignores: FreeBaselineMigrationTests.cs inspects migrations via reflection (typeof(AppDbContext).Assembly.GetTypes) + invokes Up/Down on a real MigrationBuilder and reads structured MigrationOperation objects (DropColumnOperation, AlterColumnOperation with OldColumn/IsNullable/MaxLength/ColumnType). That is far more robust than textual regex and better honors constraint 7 ("follow existing repo patterns"). The regex approach still meets the stated acceptance (0 findings on 25 files), so this is a quality note, not a blocker — but for the CLI Program.cs the diff-scoped use-case needs file paths (reflection can't see "added in this PR"), so the regex tool is justified for the CI step; consider reusing the reflection pattern for the all-existing-migrations test to make it engine/EF-version-proof.
2) Acceptance grep writes `det.ctor` (a regex wildcard, not the literal banned word) — harmless. Also note the AUTHORITATIVE CI banned-term grep scans only `app components public lib`, so files under backend-dotnet/tools/ are outside its scope anyway; the design's self-check is stricter than CI, which is fine.
3) Comment-stripper is "double-quote aware" but migrations use C# raw-string literals (""" ... """) for SQL; today no `//` appears inside those blocks so there is no effect, but a future migration with `//` inside a raw SQL string could be mis-stripped. Low risk; worth a guard test if hardened later.
4) Risk #2 (in-place edits to already-applied migrations slip through) and raw-SQL DropColumn via migrationBuilder.Sql (two such Sql() calls already exist in Up()) are correctly documented as out-of-scope follow-ups.
