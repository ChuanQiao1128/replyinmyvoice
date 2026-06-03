TASK: Reconcile the promo-code feature branch with main so it can deploy. Work ONLY on branch `feat/promo-code-trial`. Do NOT push, do NOT merge to main, do NOT deploy, do NOT touch git remotes.

CONTEXT
- Repo: /Users/qc/Desktop/CloudFlare. Current branch: feat/promo-code-trial (clean, HEAD cc5e0ad).
- This branch holds a complete promo-code trial feature (issues #427-438) but was built on a STALE base: it is 51 commits behind origin/main. main since gained the whole payment wave (#378-400) + Google-login auth fixes (#425/#426). origin/main is already fetched locally (HEAD 50268db). The PRODUCTION DB is already at main's migration state.
- Authoritative feature spec: plans/promo-code-trial-spec.md.

GOAL
Merge origin/main into feat/promo-code-trial, resolve ALL conflicts keeping BOTH main's work AND the promo feature, regenerate the promo EF migrations on top of main's current migration chain, and make the full backend + frontend test suites green. Commit on feat/promo-code-trial.

STEPS
1. `git merge --no-ff origin/main`. Expect conflicts in ~8 files: AppDbContext.cs, AccountService.cs, AdminHttpFunctions.cs, ReplyInMyVoice.Api/Program.cs, AccountApiTests.cs, lib/rewrite-eval-cases.ts, playwright.config.ts, docs/skill-run-log.md.
2. Resolve every conflict as a UNION that preserves both sides' intent:
   - AppDbContext.cs: keep main's entities/config AND the promo entities (PromoCode, PromoCodeRedemption DbSets + their OnModelCreating config: unique Code index, unique (PromoCodeId,UserId), other indexes, CHECK constraints, FKs).
   - AccountService.cs: keep main's changes AND the promo changes (free baseline 3->0 via FREE_BASELINE_REWRITES; the `promo` summary block; PROMO -> "Trial rewrites" label; DeleteAccountAsync also anonymizing PromoCodeRedemption).
   - AdminHttpFunctions.cs, Program.cs, AccountApiTests.cs, lib/rewrite-eval-cases.ts, playwright.config.ts: union both sides (keep main's additions and the promo additions).
   - docs/skill-run-log.md: keep BOTH sets of entries.
3. EF MIGRATION FIX (critical — do this carefully):
   a. Delete the two stale promo migration files and their .Designer.cs: Migrations/20260602080020_AddPromoCodes.cs(+.Designer.cs) and Migrations/20260602091811_FreeBaselineZero.cs(+.Designer.cs).
   b. Resolve AppDbContextModelSnapshot.cs to MAIN's version (the up-to-date base); you will regenerate the promo delta on top.
   c. Ensure dotnet-ef is available (`dotnet tool install --global dotnet-ef` if needed; PATH ~/.dotnet/tools). Then regenerate the schema migration so its Designer snapshot sits on main's chain:
      `dotnet ef migrations add AddPromoCodes -p backend-dotnet/src/ReplyInMyVoice.Infrastructure -s backend-dotnet/src/ReplyInMyVoice.Api -c AppDbContext`
      This must emit CREATE TABLE for PromoCode + PromoCodeRedemption (+ indexes/CHECKs) only — the delta vs main's model.
   d. Add the data migration for the free-baseline cutover:
      `dotnet ef migrations add FreeBaselineZero -p ... -s ... -c AppDbContext`
      then hand-edit its Up() to run, via migrationBuilder.Sql(...), an UPDATE that zeros existing free quota: set UsagePeriods.QuotaLimit=0 (and UpdatedAt) WHERE PeriodKey='free:lifetime'. Provide a no-op or safe Down(). (This mirrors the original FreeBaselineZero intent; it is data-only, no schema.)
   e. Confirm the final AppDbContextModelSnapshot.cs includes BOTH main's entities AND the promo entities, and that `dotnet ef migrations list` shows main's 6 migrations THEN AddPromoCodes THEN FreeBaselineZero in order.
4. Build + test until green:
   - `dotnet test backend-dotnet/ReplyInMyVoice.sln` (fix any merge breakage; preserve both payment/auth tests AND promo tests).
   - `npm run typecheck` and `npm run test`.
5. `grep -RniE "humanizer|bypass|undetect|detector|evade" app components public lib` must be clean.
6. Commit on feat/promo-code-trial: "merge main into promo branch; resolve conflicts; regenerate promo EF migrations on main's chain". Do NOT push / PR / merge-to-main / deploy.

CONSTRAINTS
- No secret values in tracked files (runtime env only). No `migrate reset` / `--force-reset` / dropping tables. Do NOT modify Stripe live keys, STRIPE_PRICE_ID, webhook secret, LAUNCH_CONFIRMED, or DNS. Banned terms (humanizer|bypass|undetect|detector|evade) forbidden in app/components/public/lib.
- Preserve promo behavior: redemption = grant RewriteCredit{Source="PROMO"}; atomic global-cap UPDATE; idempotent (unique (codeId,userId)); IP velocity; fail-closed on missing proxy/Turnstile secret. AND preserve main's payment/auth behavior.

REPORT AT END
- list of resolved conflict files; the final ordered migration list; dotnet test result (passed/total); npm typecheck+test result; banned-term grep result; the new commit hash.
