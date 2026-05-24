# Codex Brief — Eval Crash Resilience + Matcher Semantic Equivalence

Scope: **measurement-first.** Fix the crash bug and the too-literal eval matcher + add regression tests. **Do NOT change rewrite/repair agent behavior or the internal fact gate this round** (that is deferred item #3, pending a clean re-measure). Surfaced by the 2026-05-23 parallel 100-case run (90/100 completed; shard-5 crashed).

---

TASK: Make malformed-model-JSON non-fatal (retry → graceful quality failure) and teach the eval fact-matcher to recognize paraphrase without losing strictness on real fact loss. Add regression tests for both.

CONTEXT:
- Repo root: /Users/qc/Desktop/CloudFlare
- The working tree has many uncommitted changes; that working tree IS the code under evaluation. Make MINIMAL, targeted edits to only the files below.
- Relevant files (read first):
  - `lib/rewrite-pipeline/model.ts` — `createJsonCompletion` (~lines 540–592): `JSON.parse(rawContent)` at ~561; inner catch records `bad_json` then re-throws at ~577; retry loop ~579–592 only `continue`s on `AbortError`. Also `finalizeCandidate` (~774) and call site `rewriteWithFactReconstruct` in `lib/rewrite-pipeline/pipeline.ts` (~730).
  - `lib/rewrite-pipeline/pipeline.ts` — defines/throws `FactReconstructQualityError` (the eval already catches this as a graceful no-charge quality failure).
  - `scripts/eval-scenarios.ts` — eval matcher: `normalize` (~114–160), `factTokens` (~213), `includesEquivalentFact` (~221–369), `includesFact` (~372–388), `factCheck` (~390–398), exported test utils `__evalScenarioTestUtils` (~400–405).
  - `tests/unit/eval-scenarios-corpus.test.ts` — existing matcher tests (extend here).
- Project rules: AGENTS.md sections on the rewrite engine, the fact_reconstruct quality gate, and `docs/rewrite-learning-system.md` (esp. the success criterion at ~line 177: reusable semantic-equivalence lessons must be promoted as code-based normalization + tests, e.g. `can't guarantee`→`not promising`, `on hold`→`paused`).

CONSTRAINTS:
- Banned terms (CI grep guard, halt on match): humanizer, bypass, undetect, detector, evade — must not appear in code, comments, names, or test fixtures.
- No secrets in source; validate env vars at runtime.
- Do NOT run deploy commands (wrangler, az, stripe). Do NOT push or commit unless instructed; the human reviews the diff.
- Do NOT modify: rewrite/repair prompts, `preservesMustNotChange` / internal fact gate in `lib/rewrite-pipeline/checks.ts`, the 100-case corpus `docs/rewrite-email-eval-cases-100.md`, or the shard files in `plans/eval-shards/`.

CHANGES REQUIRED:

1. **`lib/rewrite-pipeline/model.ts` — `createJsonCompletion` crash resilience.**
   Today a model response whose JSON string values contain raw/unescaped control characters (e.g. a literal newline inside `"final_email": "..."`) makes `JSON.parse` throw `SyntaxError`, which is recorded as `bad_json` and re-thrown; the retry loop ignores it, so the whole rewrite request crashes (in production = 500). Required behavior:
   - (a) Before parsing, attempt a targeted sanitize that escapes raw control characters inside JSON string literals, then parse. (Implementer's choice of approach; must not corrupt otherwise-valid JSON.)
   - (b) If parsing still fails with `SyntaxError` (and reasonably `ZodError`), treat it as retryable: `continue` within the existing retry budget rather than throwing immediately.
   - (c) If the retry budget is exhausted and JSON is still unusable, surface it as a graceful failure that `rewriteWithFactReconstruct` converts into a `FactReconstructQualityError` (no-charge quality failure), NOT an uncaught throw.
   Keep `recordCall({ errorCode: "bad_json" })` accounting intact.

2. **`scripts/eval-scenarios.ts` — matcher semantic equivalence (measurement only).**
   The matcher is too literal for paraphrased facts in the 100-corpus, producing false negatives. Concrete failing example (case `rimv-email-048`): the rewrite preserved every fact in natural paraphrase but was scored as missing 4 facts because of wording differences:
   - "Financial aid cannot **cover** library replacement charges" ↔ output "financial aid cannot **be used for** library replacement charges"
   - "Official transcript **hold applies to balances over $100**" ↔ output "applies **a hold when a balance goes over $100**"
   - "Official transcript **requested ... by May 20**" ↔ output "**sent by the May 20 deadline**"
   - "Official transcript **requires payment or approved correction**" ↔ output "requires the balance to be cleared ... **pay $145 or have the charge corrected through an approved process**"
   Required: extend `normalize()` equivalence rules and/or `includesFact` token logic so these common paraphrases match, following the existing `normalize()` rewrite-rule style. **Strictness must be preserved**: a genuinely dropped or altered fact (wrong amount, missing name, missing date, changed policy) must STILL fail. Do not blanket-loosen.

3. **Regression tests.**
   - In `tests/unit/eval-scenarios-corpus.test.ts` (use the exported `__evalScenarioTestUtils`): add positive cases for the four `rimv-email-048` paraphrases above (must now PASS `factCheck`/`includesFact`) AND negative cases proving real fact loss still FAILS (e.g. amount changed $145→$155, dropped "North Ridge College", dropped "May 20").
   - Add a unit test for `createJsonCompletion` (locate existing model.ts tests under `tests/unit/`): a malformed-JSON model response (string value with a raw control char) must NOT throw uncaught — it must retry and/or resolve into a graceful `FactReconstructQualityError`-mappable failure.

ACCEPTANCE:
- A malformed-JSON model response no longer crashes the request; it retries within budget and, if still unusable, yields a graceful no-charge quality failure.
- The four `rimv-email-048` paraphrases PASS `factCheck`; the negative (real-loss) variants still FAIL.
- New + existing unit tests pass; `npm run lint` and typecheck pass.
- Banned-term scan clean: `grep -RniE "humanizer|bypass|undetect|detector|evade" app components public lib scripts tests`.
- Append a line to `plans/decisions-log.md`: `<ISO> | parallel-eval-100 | crash-resilience + matcher-equivalence | measurement-first; agent behavior untouched`.

DO NOT:
- Touch rewrite/repair prompts, `lib/rewrite-pipeline/checks.ts` internal gate, the corpus, or shard files.
- Make the matcher so loose that real fact loss passes.
- Commit, push, or deploy.

AFTER CODEX: supervisor (Claude Code) re-runs the full 100 (same 10-shard background pool) to obtain the true post-fix pass rate, then triages the residual real quality-failures (#3) for a separate, measured decision.
