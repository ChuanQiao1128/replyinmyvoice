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
        IReadOnlyList<string>? boundarySpans = null,
        IReadOnlyList<string>? loadBearingSpans = null,
        bool proposedSpansHard = false) =>
        new(
            factLedger,
            ProtectedTermLedgerExtractor.Build(draft, factLedger, protectedSpans ?? Array.Empty<string>(), loadBearingSpans, proposedSpansHard),
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

        // Boundary polarity is observed (reported in BoundaryPass / FlippedBoundaries) but does NOT gate.
        // The deterministic marker check is too false-positive-prone on real output: a faithful rewrite
        // routinely re-expresses an incidental negation ("not ours" -> "their office handles it", "if you
        // do not reply by X" -> "reply by X") without the literal marker, and the T0 quality audit found
        // zero real boundary flips — every flag was a false positive. Boundary-polarity drift is the LLM
        // FidelityJudge's job (a later, separate lever). The deterministic chain gates on the parts that
        // stay false-positive-free: facts, protected-term/object substitution, and sendability.
        var passed = factResult.Passed
            && protectedResult.Passed
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

    // Async: run the deterministic chain, then layer the semantic FidelityJudge on top. The judge can only
    // ADD failures (object/term substitution, truth-value flips a regex misses) — it never upgrades a
    // deterministic fail to a pass. A judge ERROR fails closed (Passed=false): an unavailable/garbled judge
    // is a quality failure, never a silent ship. Source text is needed because the judge compares against it.
    public static async Task<QualityGateReport> EvaluateWithFidelityAsync(
        string candidateText,
        string sourceText,
        QualityContext context,
        IFidelityJudge fidelityJudge,
        CancellationToken ct)
    {
        var deterministic = Evaluate(candidateText, context);

        var protectedTerms = context.ProtectedTerms.Terms
            .Select(t => t.Text)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var fidelity = await fidelityJudge.EvaluateAsync(sourceText, candidateText, protectedTerms, ct);

        if (fidelity.Passed && fidelity.Error is null)
        {
            return deterministic; // judge adds nothing
        }

        var reasons = new List<string>(deterministic.Reasons);
        var driftedTerms = new List<string>(deterministic.DriftedTerms);

        if (fidelity.Error is not null)
        {
            reasons.Add($"FidelityJudge unavailable ({fidelity.Error}) — failing closed.");
        }

        foreach (var d in fidelity.Drifts)
        {
            reasons.Add(
                $"FidelityJudge {d.Kind}: \"{d.SourceValue}\""
                + (d.CandidateSpan is null ? string.Empty : $" → \"{d.CandidateSpan}\"")
                + (string.IsNullOrWhiteSpace(d.Why) ? string.Empty : $" ({d.Why})"));
            if (d.Kind is FidelityDriftKind.ObjectSubstituted or FidelityDriftKind.HardAnchorChanged)
            {
                driftedTerms.Add(d.SourceValue);
            }
        }

        return deterministic with
        {
            Passed = false,
            DriftedTerms = driftedTerms.Distinct(StringComparer.Ordinal).ToList(),
            Reasons = reasons,
        };
    }
}
