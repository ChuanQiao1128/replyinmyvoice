using ReplyInMyVoice.Domain.RewriteEngine;

namespace ReplyInMyVoice.Domain.Quality;

// Source-derived ledgers a quality evaluation needs, built once per request from the draft + fact
// ledger (+ optional LLM-proposed protected-term / boundary spans). The VoiceProfile is added later
// (Voice track) — kept out of the deterministic chain for now.
public sealed record QualityContext(
    RewriteFactLedger FactLedger,
    ProtectedTermLedger ProtectedTerms,
    BoundaryLedger Boundaries)
{
    public static QualityContext Build(
        string draft,
        RewriteFactLedger factLedger,
        IReadOnlyList<string>? protectedSpans = null,
        IReadOnlyList<string>? boundarySpans = null) =>
        new(
            factLedger,
            ProtectedTermLedgerExtractor.Build(draft, factLedger, protectedSpans ?? Array.Empty<string>()),
            BoundaryLedgerExtractor.Build(draft, factLedger, boundarySpans));
}

// Aggregated verdict from the deterministic gate chain. The LLM judges (FidelityJudge, SendabilityTier)
// layer on top and can only ADD failures — they never upgrade a deterministic fail to a pass.
public sealed record QualityGateReport(
    bool Passed,
    bool FactPass,
    bool ProtectedTermPass,
    bool BoundaryPass,
    SendabilityTier SendabilityTier,
    IReadOnlyList<string> DriftedTerms,
    IReadOnlyList<string> FlippedBoundaries,
    IReadOnlyList<string> Reasons);

// Runs the deterministic fidelity gates over a candidate and aggregates them into one report:
//   RewriteFactGate (critical facts / identifiers / certainty drift)
//   · ProtectedTermGate (object/name substitution)
//   · BoundaryGate (polarity flips)
//   · SendabilityGate (garble / placeholder residue).
// Pure, no LLM, no I/O — fully unit-testable; the async LLM judges are applied separately by the
// orchestrator and combined with this report.
public static class QualityGateChain
{
    public static QualityGateReport Evaluate(string candidateText, QualityContext context)
    {
        var factResult = RewriteFactGate.Check(candidateText, context.FactLedger);
        var protectedResult = ProtectedTermGate.Check(candidateText, context.ProtectedTerms);
        var boundaryResult = BoundaryGate.Check(candidateText, context.Boundaries);
        var sendabilityResult = SendabilityGate.Check(candidateText);

        var reasons = new List<string>();
        reasons.AddRange(factResult.Reasons);
        reasons.AddRange(protectedResult.Reasons);
        reasons.AddRange(boundaryResult.Reasons);
        reasons.AddRange(sendabilityResult.Issues.Select(i => $"{i.Kind}: {i.Detail}"));

        var passed = factResult.Passed
            && protectedResult.Passed
            && boundaryResult.Passed
            && sendabilityResult.Passed;

        return new QualityGateReport(
            passed,
            factResult.Passed,
            protectedResult.Passed,
            boundaryResult.Passed,
            sendabilityResult.Tier,
            protectedResult.DriftedTerms,
            boundaryResult.FlippedBoundaries,
            reasons);
    }
}
