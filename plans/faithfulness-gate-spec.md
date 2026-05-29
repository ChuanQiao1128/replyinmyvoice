# Reliable Faithfulness Gate ("B") — Specification

**Date:** 2026-05-29 · **Author:** Claude Code (via `system-spec-synthesis`) · **Status:** spec for review, **eval-only**
**Decision basis:** the detection investigation closed to a genre-dependent tradeoff (memory `detection-three-axis-tradeoff.md`).
Score-lowering (rough essay translation) + **surgical post-hoc repair** *can* yield low-detection + fact-safe text on
non-dense emails — but only if the drifted spans are **found**. The current judge (the finder) is unreliable; this spec
makes it reliable so surgical repair can run without a human catching drifts.

## Context

Source-of-truth inputs (paths, not secrets):
- Owner rules / banned terms: `AGENTS.md` (the banned-terms CI guard; eval-only tooling stays
  eval-only; never log secret values).
- The judge to fix: `backend-dotnet/tools/ReplyInMyVoice.Eval/SemanticVerifier.cs` — `SemanticEvalJudge.VerifyAsync`
  (one LLM call, `temperature=0`, `response_format=json_object`, **`max_tokens=1600`** at [:89], `thinking:disabled`),
  `BuildUserPrompt` ([:132]) asks for `facts[]{fact,status,evidence_quote,reason}` + `forbidden[]` + `meaning_changed` +
  `send_ready`; `Parse` ([:140]) `JsonDocument.Parse(content)` → on throw returns `Error("judge_json_parse_failed")` ([:175]).
  `SemVerdict.FactsReallyPass` = no fact `missing`/`contradicted`.
- The pipeline that consumes it: `backend-dotnet/tools/ReplyInMyVoice.Eval/TranslationDirectPilot.cs`
  (`RunEssayLoopAsync`/`RunMaskedEssayLoopAsync`) — rough essay rewrite → Youdao back-translate → **today: human reads the
  candidate, finds drifts, surgically fixes only those, re-scores**. B replaces the human finder.
- Reusable deterministic prod pieces (per `plans/voice-fidelity-quality-track-spec.md`): `FactLedgerExtractor`,
  `RewriteFactGate`, `ProtectedTermLedger`/`ProtectedTermProposer`, `ForbiddenClaimScreen`, `Domain.Quality` gates.
- Probes/tools built 2026-05-29: `GptzeroProbe`, `YoudaoTx`, `EssayPolish`, `JudgeFile` (re-judge a text vs fact lists).

Proven failures today (the gate must catch all of these — they are the regression suite):
1. **Dev → "Dave"** (proper-name swap) — judge reported `facts=ok`.
2. **$12 → "12 yuan"** (currency/amount drift, ×2) — passed.
3. **"permission slip" → "receipts"** (object substitution) — passed.
4. **"I will add Maya" → "Maya was immediately added"** (tense + first-person→narration, conditional→completed) — passed.
5. **"delivered" → "shipped"** (verb/event substitution) — passed.
6. **"Why are you so obsessed with SSO?"** (content not in source — unsupported addition that can damage a sales relationship) — not flagged.
7. **`send_ready=true` over-claimed** on clearly rough text.
8. **`judge_json_parse_failed`** deterministically on one Celestine text across 3 retries (root cause: `max_tokens=1600`
   truncates a ~19-fact verbose response into invalid JSON; possibly compounded by candidate quotes).

## Goals

1. **Find every drift class the surgical repair needs to fix**, as exact spans: hard anchors, semantic polarity/subject/
   object, and unsupported additions.
2. **Output drifted spans + expected fix** (not just pass/fail), so an automated surgical-repair step can replace
   `CandidateSpan` → `ExpectedFix` with a minimal, score-preserving edit.
3. **Never crash on parse**; never silently pass when it cannot verify (**fail-closed**).
4. **No false-fails on genuine paraphrase** (e.g. "set" preserves "confirmed") — precision matters or the loop never accepts.
5. Catch all 8 known misses above; keep it **eval-only**.

## Non-Goals

- Lowering detection further (done) or judging **style/casualness** — owner ruled casual IS sendable if facts intact, so
  `send_ready` is **removed as a gate signal** (kept only as an optional advisory note, never blocks).
- Dense / safety-critical emails (medical-036, billing-Celestine): these hit a hard **detection** wall (score won't drop /
  boundaries can't be roughened safely) regardless of the gate — explicitly out of scope; flag, don't try to thread them.
- Cross-tool transfer to other detection classifiers (Pangram 401) — separate, must be verified before any productization.
- Wiring into the production rewrite engine. This gate is an **eval-only** drift-finder for the surgical-repair loop.

## Current System

`SemanticEvalJudge.VerifyAsync(rewrite, mustKeep[], mustNotClaim[])` makes ONE DeepSeek call (temp 0, json_object,
max_tokens 1600) asking the model to label every `mustKeep` fact `preserved|missing|contradicted|unverifiable` + every
`mustNotClaim` `violated` + `meaning_changed` + `send_ready`. Two structural weaknesses:
- **Recall**: one global pass over all facts at temp 0 misses fine substitutions (name Dev→Dave, currency, verb
  delivered→shipped). The model "reads past" small swaps because the date/entity around them matches.
- **Robustness**: verbose per-fact fields × many facts > 1600 tokens → truncated → unparseable → `judge_json_parse_failed`.
It also takes `mustKeep`/`mustNotClaim` **lists** (corpus-only); for arbitrary emails the source-of-truth is the **original
draft text**, which the gate must derive anchors/claims from itself.

## Proposed Architecture

A new `FaithfulnessGate` (eval-only, `backend-dotnet/tools/ReplyInMyVoice.Eval/`) = **two layers + aggregation**, taking the
**original source text** and the **candidate** and returning `FaithfulnessReport { Passed, Drifts[] }`.

```
FaithfulnessGate.EvaluateAsync(sourceEn, candidateEn)
 ├─ Layer 1 — DETERMINISTIC anchor check (no LLM):
 │    extract hard anchors from sourceEn (reuse FactLedgerExtractor/ProtectedTermLedger + regex):
 │      proper names, integers/decimals, money (symbol+unit+number), dates, times, IDs (R-####, INV-####, FieldTrip-…),
 │      percentages, phone numbers.
 │    for each anchor: present verbatim OR normalized-equivalent in candidate?
 │      • money is currency-aware: "$12" ≠ "12 yuan"/"12 元"/"¥12"  → CurrencyChanged
 │      • proper name must appear exactly; a near-name not in source (Dave) → HardAnchorChanged
 │      • missing → HardAnchorMissing
 │    → emits DriftSpan[] for hard facts.  (Would have caught #1,#2 alone.)
 ├─ Layer 2 — CONSTRAINED LLM semantic check (chunked, robust JSON):
 │    derive atomic CLAIMS from sourceEn (each: subject, action/relation, object, polarity, modality).
 │    chunk claims (≤ 8 per call); per chunk ask DeepSeek ONLY:
 │      for each claim — is it preserved / polarity-flipped / subject-or-role-swapped / object-substituted / missing?
 │      AND a NO-ADDITIONS sub-check: does candidate assert anything material not in source?
 │    response is small per chunk (status + short spans, NO evidence_quote/reason bloat) → fits max_tokens.
 │    → emits DriftSpan[] for polarity/subject/object/addition.  (Catches #3,#4,#5,#6.)
 └─ aggregate → FaithfulnessReport:
      Passed = (no Layer-1 drift) AND (no Layer-2 contradiction/flip/substitution/missing) AND (no material addition).
      Drifts = union, de-duped. send_ready NOT considered.
```

Layer 1 is the reliability backbone (deterministic, no model judgment) and catches the cases the LLM "reads past."
Layer 2 handles what regex can't (who-did-what, negation, object identity, additions) but is **scoped + chunked** so it is
both higher-recall (small focused asks) and parse-robust.

## Data Model

```csharp
enum DriftKind { HardAnchorMissing, HardAnchorChanged, CurrencyChanged, PolarityFlipped,
                 SubjectRoleSwapped, ObjectSubstituted, UnsupportedAddition }

// One thing the surgical-repair step must fix.
record DriftSpan(
    DriftKind Kind,
    string    SourceValue,      // truth from the source (e.g. "Dev", "$12", "delivered", "permission slip")
    string?   CandidateSpan,     // exact text in the candidate to replace (e.g. "Dave", "12 yuan"); null if pure omission
    string    ExpectedFix,       // what CandidateSpan should become (= SourceValue, or a minimal correct phrase)
    string    Why);             // short rationale (for logs/telemetry, not shown to users)

record FaithfulnessReport(
    bool Passed,
    IReadOnlyList<DriftSpan> Drifts,
    string? SendAdvisory = null, // optional, non-blocking
    string? Error = null);       // set ⇒ fail-closed (treated as NOT passed)
```

The **surgical-repair contract**: for each `DriftSpan` with non-null `CandidateSpan`, replace that span with `ExpectedFix`
(smallest edit); for omissions (`CandidateSpan==null`) insert `ExpectedFix` at the natural place. Repair touches only these
spans → preserves the low detection score (validated today: 32→29%, 1→1%).

## API and Job Contracts

```csharp
interface IFaithfulnessGate {
    Task<FaithfulnessReport> EvaluateAsync(string sourceEn, string candidateEn, CancellationToken ct);
}
```
- `sourceEn` = the original English draft (truth). `candidateEn` = the back-translated rough candidate.
- Eval CLI flag (extend `JudgeFile`/the loop): `FAITHFULNESS_GATE=1`, `FG_SOURCE=path`, `FG_CANDIDATE=path` → prints the
  report (Passed + each DriftSpan). The surgical-repair loop calls `EvaluateAsync`, applies fixes, re-scores GPTZero, and
  re-evaluates until `Passed` (bounded iterations) or gives up (fail-closed).
- **Assumption (mark):** anchors/claims are derived from `sourceEn`, not from `must_keep` lists, so the gate works on any
  email. Corpus `must_keep`/`must_not_claim` are used **only** in tests as ground truth.

## State and Error Handling

- **Fail-closed everywhere.** Layer-2 chunk parse failure → repair-parse (extract largest balanced `{...}`, strip prose) →
  one retry → if still unparseable, that chunk's claims are marked **blocked = DRIFT** (never silently preserved); the
  report `Error` is set and `Passed=false`.
- **JSON-robustness fixes (root-caused):** (a) chunk claims (≤8) so each response is small; (b) raise per-call `max_tokens`
  (≥ 4000); (c) drop verbose `evidence_quote`/`reason` from the required schema (short optional only); (d) repair-parse +
  retry; (e) fail-closed. (a)+(b)+(c) directly remove the `max_tokens=1600` truncation that caused `judge_json_parse_failed`.
- Layer 1 is deterministic; its only "error" is an anchor it cannot classify, which it reports as a drift candidate (safe).
- Timeout/network → `Error`, `Passed=false`.

## Security and Privacy

- **Eval-only**; not referenced by the production composition root. No prod behavior change.
- No secret values logged/printed. Do not log raw candidate/source to telemetry — log only DriftKinds + counts.
- **Banned-terms guard intact**: introduce no AGENTS.md banned terms in code, comments, names, or prompts.
- Layer-2 prompts must not instruct the model toward evasion; they only verify faithfulness.

## Rollout Plan

- **Phase 1 — build + regress (eval-only):** implement `FaithfulnessGate` (Layer 1 deterministic + Layer 2 chunked-robust),
  the data model, and the `FAITHFULNESS_GATE` CLI; pass the full regression suite below. No loop change yet.
- **Phase 2 — automate the finder in the loop:** wire `FaithfulnessGate` into the rough-essay surgical-repair loop as the
  drift-finder feeding an automated minimal-repair step (replace `CandidateSpan`→`ExpectedFix`), re-score GPTZero,
  re-evaluate; accept on `Passed` + low score, else bounded reroll. Run on the non-dense corpus subset; report a
  per-email table (score / faithful / iterations).
- **Phase 3 — only if Phase 2 shows a worthwhile hit rate:** verify cross-tool transfer to other detection classifiers (needs a working Pangram key)
  before any talk of productization. Dense/safety-critical genres remain out.

## Verification Plan

xUnit (`backend-dotnet/tests/…`, per `dotnet-backend-testing`: deterministic fakes for Layer 1; recorded/stubbed LLM or a
live-gated trait for Layer 2). **Regression suite = exactly today's known misses; each MUST now be caught with the right
`DriftKind`:**
- `Dev`→`Dave` ⇒ `HardAnchorChanged` (Layer 1). · `$12`→`12 yuan` ⇒ `CurrencyChanged` (Layer 1).
- `permission slip`→`receipts` ⇒ `ObjectSubstituted` (Layer 2). · `delivered`→`shipped` ⇒ `ObjectSubstituted` (Layer 2).
- `I will add Maya`→`Maya was immediately added` ⇒ `SubjectRoleSwapped`/polarity (Layer 2).
- `Why are you so obsessed with SSO?` ⇒ `UnsupportedAddition` (Layer 2).
- The Celestine ~19-fact text that crashed ⇒ **parses, no `judge_json_parse_failed`**, returns a report.
- `send_ready` over-claim ⇒ N/A by design (signal removed from the gate).
- **Precision guards (must NOT false-fail):** "set" preserves "confirmed"; "wrapped up" preserves "finished"; "refund in
  3–5 business days" does not violate "no instant refund"; benign tone additions ("thanks for your patience") are NOT
  `UnsupportedAddition`.
- **End-to-end (Phase 2):** on Jamie/006/005 candidates, the gate's `Drifts` exactly match the spans a human flagged today;
  applying `ExpectedFix` keeps GPTZero ≤ target and yields a genuinely faithful candidate.
- Banned-term grep clean; `dotnet test` green.

## Open Questions

1. **Anchor normalization tolerance** — is "$12" vs "$12.00" equal? Are spelled-out numbers ("twelve") equal to "12"?
   (Assumption: numeric value + currency must match; spelled-out equals digit.)
2. **Reuse vs reimplement** — call prod `FactLedgerExtractor`/`ProtectedTermLedger` across the project boundary, or a lean
   eval-local anchor extractor? (Assumption: reuse if the reference is clean; else lean local regex + name list.)
3. **UnsupportedAddition policy** — block vs advisory? Some additions are harmless tone; some ("obsessed with SSO?") harm.
   (Assumption: block only additions that assert a fact/obligation/opinion about the recipient or the deal; pure pleasantries pass.)
4. **Layer-2 cost** — chunking multiplies DeepSeek calls per candidate; acceptable for eval, but cap chunks/among reroll iterations.
5. **Source for real emails** — confirmed the source draft (not must_keep) is the truth at request time; corpus lists are test-only ground truth.
