# Voice + Fidelity Quality Track — Specification

**Date:** 2026-05-27 · **Author:** Claude Code (via `system-spec-synthesis`) · **Status:** spec for review, eval-only until approved
**Decision basis:** owner pivot (2026-05-27) away from the AI-detection track after the 10-round investigation
(`plans/translation-roundtrip-pilot.md`): a low Pangram reading and fact-safe/send-ready quality are coupled opposites,
and Pangram is too noisy (±50 on identical text) to optimize against. **Pangram is hereby demoted to offline observation
only — never an optimization target or a gate.**

## Context

Source-of-truth inputs (paths, not secrets):
- Product positioning + rules: `AGENTS.md` (fact_reconstruct prod route; Sapling naturalness gate; banned terms
  `humanizer|bypass|undetect|detector|evade`; "not a detector-bypass/humanizer product"; no-charge on quality failure).
- Prod rewrite engine (C#): `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Providers/FactReconstructRewriteProvider.cs`
  (`RewriteAsync(Guid, RewriteRequest, CancellationToken)`), `backend-dotnet/src/ReplyInMyVoice.Domain/RewriteEngine/RewriteEngineCore.cs`
  (`FactLedgerExtractor`, `RewriteFact`/`RewriteFactLedger`/`RewriteFactCategory`, `RewriteStrategy`, `RewriteStructureGate`,
  `RewriteFactGate`, `RewriteStrategyRouter`).
- Request contract: `backend-dotnet/src/ReplyInMyVoice.Domain/Contracts/RewriteRequest.cs`. Prod input is **draft-only**
  (`{ RoughDraftReply, Tone="warm" }`) per the corpus contract and memory.
- Eval harness + corpus: `backend-dotnet/tools/ReplyInMyVoice.Eval/` (`Program.cs`, `EvalHarness.cs`,
  `SemanticVerifier.cs`), `docs/rewrite-email-eval-cases-100.md`.
- **Eval-only byproducts to promote** (built during the investigation, currently in `backend-dotnet/tools/ReplyInMyVoice.Eval/`):
  `ProtectedTermProposer` (TranslationPilotV2.cs), `FactDriftRepairer` + `R5PatchApplier` (TranslationPilotV4.cs),
  `SendabilityTierJudge` (TranslationPilotV3.cs), `ProfessionalInternationalEnglishJudge` + `ControlledInternationalGenerator`
  (R8LayeredPilot.cs), `UnderstandabilityJudge` (TranslationPilotV4.cs), `SemanticEvalJudge` (SemanticVerifier.cs),
  `DeepSeekChatClient` (TranslationPilotV2.cs), `IExternalRewriteProvider`/`ManusRewriteProvider` (ExternalRewritePilot.cs).

Known defect (first-class requirement): the current semantic judge **misses object/term substitution** — in the eval it
passed `seat credit → letter of credit`, `planter → flowerpot`, `saucer → tea tray`. Fidelity hardening must make those FAIL.

## Goals

1. **Fidelity**: no business-object / identifier / amount / date / status drift (ProtectedTermLedger + hardened FidelityJudge).
2. **Boundary safety**: cannot / may / not yet / no-refund / no-medical-advice / policy limits never soften or flip (BoundaryGate).
3. **Sendability**: output is genuinely send-ready, not merely fact-token-passing (SendabilityGate).
4. **Voice**: output reads like the specific user (VoiceProfile from their history) — the product's actual promise.
5. **Minimal-edit option**: preserve the draft's human surface where acceptable, instead of always reconstructing from facts.
6. **Measured quality A/B**: compare T0 / Manus / MinimalEdit / VoiceEdit on fidelity + sendability + human preference.

## Non-Goals

- Lowering or optimizing any AI-detection score (Pangram). Pangram = offline observation only.
- Any humanizer / detector-evasion / "undetectable" capability or copy. **Banned-terms CI guard stays intact and enforced.**
- Changing the draft-only production input contract or the quota/no-charge rules in this spec.
- Building the voice-sample collection UI (flagged Open Question; this spec covers the backend contract it will call).

## Current System

`FactReconstructRewriteProvider.RewriteAsync` runs: writing-signal measure (Sapling) → `RewriteInputAnalyzer` →
`FactLedgerExtractor` → budget → `RewriteStrategyRouter.ChooseInitial` → adaptive loop {generate candidate via
`IRewriteModelClient` (DeepSeek) → `RewriteStructureGate` → `RewriteFactGate` → naturalness threshold → sentence-feedback
retry} → success JSON or quality-failure/no-charge. Strategies in `RewriteStrategy` include `FactsFirstReconstruct`
(current default for medium drafts), `MinimalPolish`, `SupportPolicyOptionsRewrite`, etc. Fact checks today are
deterministic (`RewriteFactGate`) + the eval-only `SemanticEvalJudge`; **neither reliably catches object/term substitution.**

## Proposed Architecture

Add a Domain library `ReplyInMyVoice.Domain.Quality` (and Infrastructure judges) by **promoting the eval byproducts to
production-grade, tested components**, then wire them into the existing gate chain and add two new strategies.

```
RewriteAsync
 ├─ extract: FactLedger (existing) + BoundaryLedger (new) + ProtectedTermLedger (new) + VoiceProfile (new, optional)
 ├─ route strategy (RewriteStrategyRouter, extended):
 │     • VoiceEdit       (if VoiceProfile present)        → MinimalHumanEditProvider(voice=on)
 │     • MinimalHumanEdit (low/medium structural risk)    → MinimalHumanEditProvider(voice=off)
 │     • FactsFirstReconstruct (messy/high-structure)     → existing path (T0)
 ├─ generate candidate (provider)
 ├─ QUALITY GATE CHAIN (all hard):
 │     FactGate(existing) · ProtectedTermGate(new) · BoundaryGate(new) · ForbiddenClaimScreen ·
 │     FidelityJudge(hardened semantic, object/term-drift aware) · SendabilityGate(new)
 ├─ on gate fail → Rewrite Quality Strategist escalation (existing pattern) → bounded retry
 ├─ success | quality-failure/no-charge (existing rule)
 └─ [offline only] Pangram observation logged to telemetry, NOT a gate, NOT in selection
```

Components and ownership:
- **ProtectedTermLedgerExtractor** (Domain): deterministic anchors from `FactLedgerExtractor` (Amount/Identifier/Date/Person/Count)
  ∪ `ProtectedTermProposer` (DeepSeek span proposer, every span validated as an exact substring of the draft). Owner: rewrite engine.
- **BoundaryLedgerExtractor** (Domain): rules from `FactLedgerExtractor` NegativeConstraint/Condition + a DeepSeek boundary
  augmenter (rules-first, LLM may only ADD candidates, never override). Owner: rewrite engine.
- **ProtectedTermGate / BoundaryGate** (Domain): each protected term must appear verbatim OR be confirmed
  semantically-identical (NOT object-substituted) by the FidelityJudge; each boundary's polarity must hold.
- **FidelityJudge** (Infrastructure, promote+harden `SemanticEvalJudge`): add an explicit object/term-substitution check
  driven by the ProtectedTermLedger ("for each protected term, is it present or replaced by a DIFFERENT object/term? a
  different object = contradiction"). Must fail the 3 known misses.
- **SendabilityGate** (Infrastructure, promote `SendabilityTierJudge`/`ProfessionalInternationalEnglishJudge`): tier
  sendable / minor / unsendable; flags agent-action errors, garble, broken sign-off. Hard for email.
- **MinimalHumanEditProvider** (Infrastructure, new): patch-list edit over the ORIGINAL draft (reuse the
  `FactDriftRepairer`+`R5PatchApplier` patch engine, repurposed for QUALITY edits: clarity/tone/sendability), preserving the
  draft's wording/order where acceptable; enforces a source-overlap floor; NOT facts-first reconstruct. Voice-aware when a
  VoiceProfile is supplied.
- **VoiceProfileExtractor + store** (Infrastructure + EF Core): deterministic stats (opening/closing pattern, median
  sentence length, contraction rate, politeness markers) + a bounded LLM summary of common/avoided expressions, from
  user-supplied history samples. Persisted per user (Azure SQL).
- **Quality A/B harness** (eval tool): variants {T0, Manus, MinimalEdit, VoiceEdit}; metrics below; Pangram offline-only.
- **Manus** (`IExternalRewriteProvider`): kept as a quality-engine option (round-6: quality-competitive), eval-only first.

## Data Model

New EF Core entities (Azure SQL; migration required — run `data-module-review` before applying):
```csharp
// Per-user voice profile (opt-in). Derived data; source samples retained per privacy policy (Open Q).
class VoiceProfile {
  string UserId;            // = Entra oid (auth key)
  string? OpeningStyle;     // template, e.g. "Hi {name},"
  string? ClosingStyle;     // e.g. "Best," | "Thanks," | "" (none)
  int? MedianSentenceWords;
  string PolitenessLevel;   // low | medium | high
  string CommonPhrasesJson; // serialized IReadOnlyList<string>
  string AvoidedPhrasesJson;
  int SampleCount;
  DateTimeOffset UpdatedAt;
}
```
Domain value objects (no persistence):
```csharp
enum ProtectedTermKind { BusinessObject, Identifier, Amount, DateTime, Contact, ProperName, StatusPhrase, ActionPhrase }
record ProtectedTerm(string Text, ProtectedTermKind Kind, string Source, bool ExactRequired);
record ProtectedTermLedger(IReadOnlyList<ProtectedTerm> Terms);

enum BoundaryKind { NegativeConstraint, Modality, NoPromise, PolicyLimit, NoAdvice, RefundLimit, Status }
record Boundary(string Text, BoundaryKind Kind, string Polarity); // negative | uncertain | conditional
record BoundaryLedger(IReadOnlyList<Boundary> Items);

record FidelityReport(
  bool FactPass, bool BoundaryPass, bool ProtectedTermPass, int Forbidden, bool MeaningChanged,
  string SendabilityTier, IReadOnlyList<string> DriftedTerms, IReadOnlyList<string> MissingOrContradicted);
```

## API and Job Contracts

- `RewriteRequest` (extend, backward-compatible): add optional `string? VoiceProfileUserId` (resolve profile server-side;
  never pass raw samples in the request). No change to the draft-only contract.
- Gate interface (Domain): `interface IQualityGate { GateResult Evaluate(RewriteCandidate c, QualityContext ctx); }` where
  `QualityContext` carries FactLedger/BoundaryLedger/ProtectedTermLedger/VoiceProfile. Semantic gates wrap an
  `IFidelityJudge` (async).
- `interface IRewriteProvider { Task<RewriteProviderResult> RewriteAsync(...); }` — existing
  `FactReconstructRewriteProvider` unchanged; add `MinimalHumanEditProvider`.
- VoiceProfile build (internal service): `Task<VoiceProfile> BuildAsync(string userId, IReadOnlyList<string> samples, ct)`.
- Quality A/B (eval CLI, new flag `QUALITY_AB=1`, `EVAL_VARIANTS="t0,manus,minimaledit,voiceedit"`): emits per-variant
  fact-pass / boundary-pass / protected-pass / sendability-tier / preference-slot, + Pangram in an `offline_observation`
  column clearly marked non-selecting.

## State and Error Handling

- Gate-fail → existing `Rewrite Quality Strategist` escalation (diagnose → choose next allowed strategy → bounded retry,
  Budget Manager caps). Unrecoverable → quality-failure / **no usage charge** (existing AGENTS.md rule).
- VoiceProfile absent or `SampleCount` below a floor → VoiceEdit degrades to MinimalHumanEdit; MinimalHumanEdit failing
  source-overlap or sendability → fall back to FactsFirstReconstruct (T0). T0 remains the always-available safety net.
- FidelityJudge unavailable → fail closed to deterministic gates + return quality-failure (do not silently ship unverified).

## Security and Privacy

- **Voice samples are private user writing.** Store only what's needed; consent + retention policy required (Open Q).
  Do NOT silently train/modify production prompts from private user content (AGENTS.md learning rule) — VoiceProfile is
  per-user derived data used at request time, not a global prompt change.
- Never log raw draft/sample text or rewrite output to telemetry; log only metrics + the offline Pangram number.
- Sending user content to Manus (external) requires disclosure/consent; Manus stays eval-only until that's decided.
- **Banned-terms guard stays enforced**: `grep -RniE "humanizer|bypass|undetect|detector|evade" app components public lib`
  must remain clean; no new code/comments/copy may introduce them. No "undetectable"/"bypass" product copy.

## Rollout Plan

- **Phase 1 — eval-only, no prod change:** promote byproducts into `ReplyInMyVoice.Domain.Quality` + Infrastructure judges
  with xUnit tests; build Quality A/B harness; **harden FidelityJudge** and add the 3 known-miss regression tests; run A/B on
  the 100-case corpus + a small voice-sample fixture. Deliver a readout.
- **Phase 2 — gates behind a default-off flag:** wire ProtectedTermGate / BoundaryGate / SendabilityGate into the engine
  gate chain as optional levers (same pattern as the eval `EVAL_VARIANT` switches); validate on the corpus; then enable in prod.
- **Phase 3 — minimal-edit + voice:** add `MinimalHumanEditProvider`; add VoiceProfile extraction + EF migration
  (`data-module-review` first) + `VoiceEdit` route; voice-sample intake UI is a separate workstream.
- **Phase 4 — prod cutover:** route by risk + voice availability; gated by Quality A/B + human review (do not ship while any
  corpus case regresses on fidelity). Pangram remains offline observation throughout.

## Verification Plan

- **xUnit** (`backend-dotnet/tests/`, per `dotnet-backend-testing`: xUnit + FluentAssertions + WebApplicationFactory + EF
  SQLite + deterministic fakes):
  - FidelityJudge regression: `seat credit→letter of credit`, `planter→flowerpot`, `saucer→tea tray` MUST fail ProtectedTermGate/FidelityJudge.
  - BoundaryGate: polarity flips (cannot→can, may→will, "no refund"→refund) MUST fail.
  - SendabilityGate: agent-action error ("I am unable to get a full refund"), garble, broken sign-off MUST fail.
  - MinimalHumanEditProvider: source-overlap floor respected; facts/protected terms preserved; deterministic provider fake.
  - VoiceProfileExtractor: opening/closing/sentence-length/politeness extracted from fixture samples; absent-profile fallback.
- **Quality A/B** on the 100-case corpus + voice fixtures: per-variant fact/boundary/protected/sendability pass counts +
  human preference rating; Pangram reported offline-only. Acceptance: see below.
- **Banned-term grep** clean; build + `npm run test` (frontend contract tests) + `dotnet test` green before any deploy.

Acceptance criteria (Phase 1 gate to proceed):
- FidelityJudge catches ≥ the 3 known misses + 0 regressions on existing corpus fact-pass.
- ProtectedTermGate + BoundaryGate + SendabilityGate: ≥ baseline fact-safety on the 100-case corpus, with the new
  object/term-drift cases now caught.
- Quality A/B produces a clean per-variant table (fidelity + sendability + preference) with Pangram clearly non-selecting.

## Open Questions (need product decision — not assumed here)

1. **Voice-sample intake**: how/where do users provide history (UI flow), how many samples, and consent/retention policy?
   (Assumption used for spec: opt-in paste of 2–3 past emails; profile stored per user; samples retention TBD.)
2. **Mode exposure**: is Quality/Minimal/Voice user-selectable, or auto-routed? (Assumption: auto-routed by risk +
   voice-profile availability; not user-facing initially.)
3. **"User preference" metric**: human rating panel vs a proxy? (Assumption: human rating in eval; no automated proxy claimed.)
4. **Manus in prod**: cost/latency/async UX acceptable? (round-6: ~45s/case, paid.) — eval-only until decided;
   run `cloud-architecture-cost-review` before any paid prod dependency.
5. **VoiceProfile schema** location/migration on Azure SQL — needs `data-module-review` + a migration before Phase 3.
