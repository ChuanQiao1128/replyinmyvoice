# FaithfulnessGate regression fixtures (eval-only)

Pins the precision/recall behavior of `FaithfulnessGate` after the 2026-05-30 prompt
reframe (fact-ledger verification + FACT test + truth-condition equivalence + materiality,
replacing the old recall-first "when in doubt FLAG it" posture that produced false positives).

The drift judgment lives in an LLM layer, so it cannot be pinned by a deterministic xUnit
test. These fixtures are a **live regression**: run them through the gate and check the
expected outcome by hand (or in review). Layer 1 (hard anchors) IS covered by xUnit in
`tests/ReplyInMyVoice.Tests/FaithfulnessGateTests.cs`.

## Cases

| candidate | vs source | expected |
|---|---|---|
| `kwame-faithful-paraphrase.txt` | `kwame-source.txt` | **PASS, 0 drifts.** All 11 hard facts preserved; the differences are faithful paraphrase (incl. boundary active→passive "I cannot apply any change" → "nothing can be changed on my side", "up for renewal" → "will be renewed"). These 4 were the FALSE POSITIVES the reframe fixes. |
| `kwame-corrupted.txt` | `kwame-source.txt` | **FAIL, flags the 3 injected real drifts:** `$360`→`$340` (money), `I cannot apply any change` → `I can apply any change` (polarity_flipped — same boundary sentence as above, but here the truth value really flips), `June 8` → `June 18` (date). Recall must stay intact. |

The boundary sentence is the key proof point: faithful passive rephrasing PASSES (paraphrase),
a real cannot→can flip is FLAGGED (recall) — precision and recall cleanly separated.

## Run

```bash
# (A) false-positive check — expect Passed=True, drifts=0
FAITHFULNESS_GATE=1 \
  FG_SOURCE="$PWD/backend-dotnet/tools/ReplyInMyVoice.Eval/fixtures/gate-regression/kwame-source.txt" \
  FG_CANDIDATE="$PWD/backend-dotnet/tools/ReplyInMyVoice.Eval/fixtures/gate-regression/kwame-faithful-paraphrase.txt" \
  dotnet run --project backend-dotnet/tools/ReplyInMyVoice.Eval -c Release

# (B) recall check — expect Passed=False, the 3 injected drifts flagged
FAITHFULNESS_GATE=1 \
  FG_SOURCE="$PWD/backend-dotnet/tools/ReplyInMyVoice.Eval/fixtures/gate-regression/kwame-source.txt" \
  FG_CANDIDATE="$PWD/backend-dotnet/tools/ReplyInMyVoice.Eval/fixtures/gate-regression/kwame-corrupted.txt" \
  dotnet run --project backend-dotnet/tools/ReplyInMyVoice.Eval -c Release
```

Needs a DeepSeek key in env (the judge LLM). Verified 2026-05-30: (A) 0 drifts, (B) 4 drifts
(the 3 injected; `$360` double-counted by Layer 1 "missing" + Layer 2 "changed").
