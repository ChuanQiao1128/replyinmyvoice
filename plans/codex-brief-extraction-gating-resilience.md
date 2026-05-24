# Codex Brief — Fix Over-Aggressive Fact Extraction/Gating + Network Resilience

Scope: fix the **internal extraction + fact-gating** defects that cause false `fact_check_failed`, plus a network-retry resilience gap. **Do NOT change rewrite/repair prompts, agent voice, or candidate-generation strategy this round.** Surfaced by the 2026-05-23 clean 100-case eval (39/100; 37 `fact_check_failed` dominated by gating, not agent capability — see `plans/eval-results/combined-summary.md`).

---

TASK: Stop the internal fact gate from over-rejecting good paraphrased candidates, and make `createJsonCompletion` resilient to transient network errors.

CONTEXT:
- Repo root: /Users/qc/Desktop/CloudFlare. Working tree is dirty and IS the code under eval; make MINIMAL targeted edits.
- Diagnosis evidence: the eval logs 62 `missing_locked` flags across 100 cases, including indefensible bare-word "facts" ("Move", "Sender", "Options", "Requested", "Manager", "Suggested", "Purpose") and redundant atom+sentence duplicates ("Price was $689" + "$689" + "April 15"). A bare word like "Options" cannot be meaningfully preserved, so any rephrase is flagged missing → escalation → fallback → empty `fact_check_failed`.
- Relevant files (read first):
  - `lib/fact-extraction.ts` — `shouldKeepPersonCandidate`/`properNameStopWords` (~465–467) is too permissive; entity/anchor extraction captures sentence-initial capitalized common words as anchors.
  - `lib/rewrite-pipeline/fact-ledger.ts` — `addAnchor` (~273) pushes `fact.text` into `facts_that_must_not_change`.
  - `lib/rewrite-pipeline/checks.ts` — `preservesMustNotChange` (~336) is token-AND literal (every non-generic token must appear; >5-token facts exempt) → flags paraphrase as missing. Its `normalize()` (~37) already has some equivalence rules.
  - `lib/rewrite-pipeline/model.ts` — `createJsonCompletion` (~566–664): only `AbortError` triggers retry; network errors are thrown uncaught.
  - `scripts/eval-scenarios.ts` `normalize()` — the equivalence-rule style already applied on the eval side; mirror its approach.
- Project rules: AGENTS.md (rewrite engine, fact_reconstruct gate) and `docs/rewrite-learning-system.md` (semantic-equivalence must be code-based + tested).

CONSTRAINTS:
- Banned terms (halt on match): humanizer, bypass, undetect, detector, evade — nowhere in code/comments/tests.
- No secrets; validate env at runtime.
- **Do NOT change** rewrite/repair prompts, agent voice, candidate-generation strategy, the corpus `docs/rewrite-email-eval-cases-100.md`, or `plans/eval-shards/`.
- Do NOT commit, push, or deploy. Human reviews the diff.

CHANGES REQUIRED (priority order):

1. **Stop spurious single-word/fragment locked facts** (`lib/fact-extraction.ts`). Tighten entity/anchor extraction so sentence-initial capitalized common words (e.g. "Move", "Sender", "Options", "Requested", "Manager", "Suggested", "Purpose", "Staff", "Exact", "Next", "Signing") are NOT captured as person/entity anchors and therefore do not enter `facts_that_must_not_change`. Expand the stopword filter and/or require stronger evidence (real name shape, corroboration, not a lone sentence-initial common verb/noun). **Do not regress real names** — actual recipient/people names ("Priya Shah", "Martin Hale", "Lee Tran", "Elena Ruiz") must still be locked.

2. **Add semantic-equivalence to the internal gate** `preservesMustNotChange` (`lib/rewrite-pipeline/checks.ts`). A paraphrase that preserves the fact must not be flagged missing — mirror the equivalence approach already used in `scripts/eval-scenarios.ts` `normalize()` (e.g. "cover"↔"be used for"). **Stay strict**: real changes (wrong amount, dropped name/date, changed policy/condition) must STILL be flagged. The point is to recognize wording paraphrase, not to loosen fact checking.

3. **Network resilience in `createJsonCompletion`** (`lib/rewrite-pipeline/model.ts`). Currently only `AbortError` retries; network errors (`TypeError: fetch failed` with cause `ENOTFOUND` / `UND_ERR_CONNECT_TIMEOUT` / `ECONNRESET`) throw uncaught and crash the request (this corrupted an eval run). On network/connection errors, retry within the existing retry budget; if exhausted, surface as a graceful failure consistent with the existing `JsonCompletionQualityError` → `FactReconstructQualityError` path, NOT an uncaught throw.

4. **(Optional, only if straightforward)** Reduce redundant locked-fact decomposition so a fact and its atom aren't both separate locked entries (e.g. drop "$689" when "Price was $689" is already locked). Skip if it risks dropping a genuinely needed atom.

REGRESSION TESTS:
- `lib/fact-extraction.ts`: sentence-initial common words ("Move", "Options", "Requested", "Manager", "Sender", "Suggested") are NOT extracted as locked facts/person anchors; real names ("Priya Shah", "Martin Hale") ARE still locked.
- `preservesMustNotChange`: a fact-preserving paraphrase is NOT flagged missing; a real loss (wrong amount / dropped name / dropped date) IS still flagged.
- `createJsonCompletion`: a simulated network error (`fetch failed`) retries and resolves to a graceful failure, not an uncaught throw.

ACCEPTANCE:
- New + existing unit tests pass; `npm run lint` + typecheck pass.
- Banned-term scan clean: `grep -RniE "humanizer|bypass|undetect|detector|evade" app components public lib scripts tests`.
- Append to `plans/decisions-log.md`: `<ISO> | parallel-eval-100 | fix extraction over-capture + gate semantic-equivalence + network retry | reduce false fact_check_failed; agent voice untouched`.

DO NOT:
- Touch rewrite/repair prompts, agent voice, candidate strategy, corpus, or shard files.
- Loosen the gate so real fact loss passes.
- Commit, push, or deploy.

AFTER CODEX: supervisor re-runs the full 100 (10-shard background pool). A jump in customer-usable pass rate confirms the gating/extraction was the cause (hypothesis a); a flat result would point to a real agent-capability gap (hypothesis b) for a later round.
