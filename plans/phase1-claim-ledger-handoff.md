# Phase 1 ClaimLedger — Codex Handoff Brief

**Branch:** `exp/ai-draft-cleanup-ab` (experimental — do NOT merge to `main`; owner OK to delete branch if it doesn't pan out)
**Status @ 2026-05-28:** Sub-Phase 1.0 complete (Domain types + LLM extractor + C# smoke). Sub-Phase 1.1 next.
**Why this exists:** Claude credits exhausted mid-build; Codex picks up from this commit.

---

## TL;DR

We're building an EN→ZH safe-intermediate pipeline (Kimi's Phase-1 design) as an experimental fidelity layer. The detection-chasing track is CLOSED (see `MEMORY.md` → `stop-chasing-ai-detection`); this is a quality / fidelity track. The whole point of Phase 1 is to validate that we can take an English draft, push it through a Chinese intermediate, and recover an English output that preserves every atomic claim — measured against a structured `ClaimLedger`, NOT against any AI-detection score.

Phase 1 sub-phases:

- **1.0 ✅ DONE** — extract structured atomic claims from the EN draft via DeepSeek with a frozen prompt, parse into typed `RewriteClaim` records, expose via `IClaimLedgerExtractor`.
- **1.1 ⏳ NEXT** — Youdao EN→ZH translate; verify the ZH output preserves both the regex `RewriteFactLedger` and the new `RewriteClaimLedger`; produce a structured drift report.
- **1.2 ⏳ LATER** — for ZH drifts found in 1.1, do a minimal Chinese repair (DeepSeek, in-Chinese prompt, surgical edits only); re-check; emit final ZH "safe intermediate".

NO production code is touched. Everything lives in the eval tool or in additive Domain extensions. Domain extensions are backward-compatible (all defaults; existing 271 + 29 = 300 tests pass).

---

## What 1.0 shipped

| File | Purpose |
|---|---|
| `backend-dotnet/src/ReplyInMyVoice.Domain/RewriteEngine/RewriteEngineCore.cs` | Extended `RewriteFact` with `PreserveMode` + `Normalized` (both optional, default-preserving). Added `RewriteClaim`, `RewriteClaimLedger`, `RewriteClaimModality`, `RewriteClaimPolarity`, `RewriteFactPreserveMode`. |
| `backend-dotnet/src/ReplyInMyVoice.Domain/Quality/ClaimLedgerExtractor.cs` | `IClaimLedgerExtractor` interface + `ClaimLedgerJsonParser` static helper. **The frozen `claim-ledger-v1` prompt is a `const string` here** — treat as immutable; re-validate via `plans/claim-ledger-validate-v2.py` before any edit. |
| `backend-dotnet/tools/ReplyInMyVoice.Eval/DeepSeekClaimLedgerExtractor.cs` | Interface impl. Reuses the `DeepSeekChatClient` internal class from `TranslationPilotV2.cs`. |
| `backend-dotnet/tools/ReplyInMyVoice.Eval/Phase1ClaimLedgerSmoke.cs` | End-to-end smoke. Triggered by env flag. |
| `backend-dotnet/tools/ReplyInMyVoice.Eval/Program.cs` | Added `PHASE1_CLAIM_LEDGER_VALIDATE=1` dispatcher. |
| `backend-dotnet/tests/ReplyInMyVoice.Tests/ClaimLedgerJsonParserTests.cs` | 29 unit tests pinning the 3 cleanup rules + parser behaviors. |
| `plans/claim-ledger-validate-v2.py` | Offline Python validator (DeepSeek only, ~$0.01 to run). Source of truth for the prompt text — keep in sync with the C# const. |

### Verification — what should pass right now

```bash
cd /Users/qc/Desktop/CloudFlare

# 1. Full test suite (300 tests, all green):
dotnet test backend-dotnet/tests/ReplyInMyVoice.Tests/ReplyInMyVoice.Tests.csproj --nologo

# 2. C# end-to-end smoke (needs DEEPSEEK_API_KEY from .env.local; ~$0.005 + 10 DeepSeek calls):
PHASE1_CLAIM_LEDGER_VALIDATE=1 dotnet run --project backend-dotnet/tools/ReplyInMyVoice.Eval/ReplyInMyVoice.Eval.csproj
# Expected: 10/10 cases, per-case ledger size matches Python v2 ±1 (dedupe rule fires on 014).
# TOTAL 95 → 94 (one dedupe). DeepSeek calls: 10.

# 3. Python offline re-validator (only needed if you edit the prompt const):
python3 plans/claim-ledger-validate-v2.py
# Expected: 10 cases, JSON saved to /tmp/claim_ledger_validation_v2/{case}.json
```

### Validation evidence the prompt actually works

10-case eval on the corpus (`docs/rewrite-email-eval-cases-100.md`, cases 001/005/007/008/013/014/017/028/029/041) produced **95 claims, 0 hallucinated facts, 0 source_span violations (whitespace-normalized), 0 missed critical anchors**. Full per-case readout: see prior chat / `/tmp/claim_ledger_validation_v2/`. Re-runnable via the Python script above.

### Decisions locked (do NOT relitigate)

1. **Prompt frozen as `claim-ledger-v1`** — text lives at `ClaimLedgerJsonParser.SystemPrompt`. Any edit requires re-running `plans/claim-ledger-validate-v2.py` on the same 10 cases AND confirming the new version maintains: 0 hallucinations, all critical anchors preserved, no new modality-classification drift.
2. **Option A: extend `RewriteFact` in Domain directly** (vs. adding a parallel `ProtectedFact` type). Owner-locked 2026-05-28. The prod engine uses `RewriteFact` already; extending it keeps one type.
3. **Cleanup behaviors handled in the C# parser, not the prompt** — dedupe by `source_span`, drop paraphrased spans that fail a whitespace-normalized substring check, soft-skip empathy-shape openings ("I know this is…", "I understand…"). Pinned by `ClaimLedgerJsonParserTests`.
4. **NO placeholders / sentinels in Phase 1.** Kimi's Phase 1 advice explicitly: skip the masking approach, do EN→ZH then post-check + repair against structured ledgers. Sentinel-mask approach is what failed in the GPTZero loop (see `GptzeroLoopPilot.cs` history).
5. **NO GPTZero in Phase 1.** Success criterion = fact + claim preservation through the round-trip. AI-detection score is irrelevant here.
6. **`GptzeroLoopPilot.cs` is reference material, do NOT modify.** New pilots = new files (`StageOneEnToZhSafePilot.cs` etc.).

---

## What 1.1 needs to build

### Goal

Take a clean EN draft → run it through Youdao EN→ZH → check whether the ZH output preserves every entry in both `RewriteFactLedger` (regex anchors) and `RewriteClaimLedger` (structured claims) → produce a structured drift report.

This is **measurement only**. No repair yet. The output tells us:

1. Which `RewriteFact` entries survived (verbatim or normalized) and which dropped.
2. Which `RewriteClaim` entries survived (subject + action + object + modality + polarity + time + condition all present, OR translated equivalents present) and which drifted.
3. The drift pattern — does Youdao consistently drop amounts? does it flip modality? does it merge atomic claims?

If 1.1 shows Youdao destroys most claims, 1.2 is dead and we stop. If 1.1 shows ~70%+ survive, 1.2's job is small and tractable.

### Where things live

- **Youdao EN→ZH HTTP call:** see `backend-dotnet/tools/ReplyInMyVoice.Eval/TranslationPilotV2.cs` — there's a `YoudaoClient` (or similar; grep for `youdao.com` or `youdao_app_key`). Reuse it; do not rebuild. Keys are `YOUDAO_APP_KEY` + `YOUDAO_APP_SECRET` in `.env.local`.
- **`RewriteFactLedger` extraction:** already exists in `Domain/RewriteEngine/RewriteEngineCore.cs` — `RewriteFactLedgerExtractor.Extract(RewriteRequest)`. The `RewriteRequest` is a simple wrapper around the draft; see how `GptzeroLoopPilot.cs` builds it for an existing example.
- **`RewriteClaimLedger` extraction:** `DeepSeekClaimLedgerExtractor.ExtractAsync(draft, ct)` — already wired.
- **Add the new pilot file:** `backend-dotnet/tools/ReplyInMyVoice.Eval/StageOneEnToZhSafePilot.cs` (do NOT touch `GptzeroLoopPilot.cs`).
- **Add Program.cs dispatcher:** `STAGE1_EN_TO_ZH_PILOT=1`.

### Suggested 1.1 API shape

```csharp
internal static class StageOneEnToZhSafePilot
{
    public static async Task<int> RunAsync(IReadOnlyList<EvalCase> cases) { /* ... */ }
}

internal sealed record ZhPostCheckReport(
    string CaseId,
    string OriginalEn,
    string TranslatedZh,
    IReadOnlyList<RewriteFact> FactsSurvived,
    IReadOnlyList<RewriteFact> FactsDrifted,    // present in EN ledger, missing in ZH
    IReadOnlyList<RewriteClaim> ClaimsSurvived,
    IReadOnlyList<RewriteClaim> ClaimsDrifted,   // present in EN ledger, semantically broken in ZH
    double FactSurvivalPct,
    double ClaimSurvivalPct);
```

### "Did this fact survive in ZH?" check — implementation hints

- **Exact identifiers / amounts / dates** (PreserveMode = Exact): substring check on the ZH text. "$1,250.00" should still be "$1,250.00" or "1,250.00 美元" or "1250 美元" — for now, use substring on the digits. If digits dropped, fact drifted.
- **Persons / business nouns** (PreserveMode = Exact or Semantic): substring check on a translated equivalent. Phase 1.1 can be naive — just check if the digits / proper nouns survive verbatim. Translated-equivalent matching is a 1.2 problem.
- **Claims**: for each `RewriteClaim`, ask DeepSeek "does sentence X in ZH text Y still assert subject=S, action=A, object=O, modality=M, polarity=P, time=T, condition=C?" → returns yes/no/partial. This is one DeepSeek call per claim per case; budget ~$0.10 per 10-case run.

That last bullet is the expensive part — consider batching all claims for one case in a single call to keep cost down.

### Where to land the result

- `docs/rewrite-eval-results/{timestamp}-stage1-en-zh-pilot.md` — one row per case, with survival % and drift list.
- Update `MEMORY.md` if the per-case survival pattern is surprising (likely worth a new memory note).

---

## What 1.2 needs to build (preview)

Take the `ZhPostCheckReport` from 1.1. For each `ClaimsDrifted` entry: ask DeepSeek (in Chinese, prompt also in Chinese) to MINIMALLY edit the ZH text so the specific drifted claim is restored — change as few characters as possible, keep all other words verbatim. Re-check. Two repair iterations max.

Output: ZH "safe intermediate" — a Chinese paragraph that preserves all claims. This is the artifact the next phase (`Phase 2: ZH→EN with style restoration`) would translate back to English.

---

## Hard rules Codex must NOT violate

- **No prod code edits.** Production engine is `FactReconstructRewriteProvider.cs`, `RewriteEngineCore.cs` (the methods, not the records I added), `RewriteRequestService.cs`, `Functions/RewriteHttp*`. Phase 1 is eval-tool + Domain-type-additions only.
- **No banned terms.** `humanizer | bypass | undetect | detector | evade`. Run `grep -RniE "humanizer|bypass|undetect|detector|evade" backend-dotnet/src backend-dotnet/tools` before committing.
- **No real Stripe / Azure dashboard / DNS changes.** Branch is experimental; only local eval + git push allowed.
- **Don't unfreeze the prompt without re-validating.** If you tune `ClaimLedgerJsonParser.SystemPrompt`, re-run `plans/claim-ledger-validate-v2.py` on the same 10 cases and confirm 0 regressions before committing.
- **Don't merge this branch to `main`.** Owner instruction. Branch may be deleted if Phase 1 doesn't pan out.

---

## Context links (in-repo only — Codex starts cold)

- **Kimi's Phase 1 design rationale**: was in chat, not in repo. Key points reproduced here: skip placeholders / sentinels; do EN→ZH then post-check + repair; structured ClaimLedger; per-claim judge not per-paragraph; reject (don't fallback to) any transform whose protection broke.
- **AI-detection track close-out** (why we're NOT chasing detection scores): `plans/translation-roundtrip-pilot.md` — the 10-round investigation that proved low-detection ⟺ broken-text coupling. Pangram has ±50 noise on identical text; GPTZero whack-a-mole confirmed; cross-tool non-transfer of detection wins.
- **Voice + Fidelity quality spec** (the broader pivot this Phase 1 sits inside): `plans/voice-fidelity-quality-track-spec.md`.
- **Existing eval-tool pilots** (reference patterns to mimic for `StageOneEnToZhSafePilot.cs`): `backend-dotnet/tools/ReplyInMyVoice.Eval/TranslationPilotV2.cs` (Youdao client + DeepSeekChatClient), `TranslationPilotV6.cs`, `R10DualChannelPilot.cs`. `GptzeroLoopPilot.cs` is reference only — do NOT modify.
- **Domain Quality gates already shipped** (Phase-0-ish prior work on this branch — Codex may need them for the post-check): `backend-dotnet/src/ReplyInMyVoice.Domain/Quality/` — `ProtectedTermGate`, `BoundaryGate`, `SendabilityGate`, `QualityGateChain`, `LoadBearingPhraseExtractor`, `VoiceProfileExtractor`, `AiTellStripper`. All 271 pre-existing tests pass.
