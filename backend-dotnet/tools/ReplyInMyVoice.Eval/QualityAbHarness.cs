using System.Text;
using ReplyInMyVoice.Domain.Quality;
using ReplyInMyVoice.Domain.RewriteEngine;
using ReplyInMyVoice.Infrastructure.Providers;

// Quality A/B harness (QUALITY_AB=1). EVAL-ONLY. Runs the production engine (T0) over the corpus and
// audits each output through the full quality stack:
//   deterministic QualityGateChain (Fact · ProtectedTerm · Boundary · Sendability)
//   + LLM FidelityJudge v2 (object/term + role drift)
//   + LLM SendabilityTierJudge (agent-action / fluency).
// DeepSeek + Sapling only — NO Pangram/GPTZero (detection track is closed; not a selection signal).
//
// This is the Phase-1 readout AND, just as importantly, a FALSE-POSITIVE audit: T0 is the trusted,
// fact-faithful engine, so the new gates should pass nearly all of its outputs. Any gate that fails a
// faithful T0 rewrite is too strict and must be loosened before it can gate prod.
//
// Only the `t0` variant is wired today; MinimalEdit / VoiceEdit / Manus slot in here once their
// providers exist (same per-case audit, extra columns).
internal static class QualityAbRunner
{
    public static async Task<int> RunAsync(
        FactReconstructRewriteProvider provider,
        CountingRewriteModelClient modelCounter,
        CountingWritingSignalClient signalCounter,
        IReadOnlyList<EvalCase> cases,
        EvalConfig config,
        string apiKey,
        DateTimeOffset startedAt)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Console.Error.WriteLine("QUALITY_AB: missing model api key (DEEPSEEK_API_KEY/OPENAI_API_KEY).");
            return 2;
        }

        using var http = new HttpClient();
        using var judgeHttp = new HttpClient();
        var deepseek = new DeepSeekChatClient(http, apiKey, config.Model, config.OpenAiBaseUrl, TimeSpan.FromSeconds(60));
        var proposer = new ProtectedTermProposer(deepseek);
        var judge = new SemanticEvalJudge(judgeHttp, apiKey, config.Model, config.OpenAiBaseUrl, TimeSpan.FromSeconds(90));
        var tierJudge = new SendabilityTierJudge(deepseek);

        Console.WriteLine($"Quality A/B (variant=t0): cases={cases.Count} model={config.Model} AI-detection=OFF (offline-only)");
        var rows = new List<QualityAbRow>();

        foreach (var sample in cases)
        {
            var request = sample.ToRewriteRequest();
            var t0 = await provider.RewriteAsync(Guid.NewGuid(), request, CancellationToken.None);
            var text = RewritePayload.TryParse(t0.ResultJson)?.RewrittenText ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                rows.Add(QualityAbRow.NoOutput(sample, t0.ErrorCode));
                Console.WriteLine($"{sample.Id}: T0 no output ({t0.ErrorCode ?? "unknown"}).");
                continue;
            }

            // Build the source-derived ledgers (proposer is the only LLM call for the context).
            var factLedger = FactLedgerExtractor.Extract(request);
            var protectedSpans = await proposer.ProposeAsync(sample.InputDraft, CancellationToken.None);
            var ctx = QualityContext.Build(sample.InputDraft, factLedger, protectedSpans);

            // Deterministic chain.
            var det = QualityGateChain.Evaluate(text, ctx);

            // LLM judges.
            var sem = await judge.VerifyAsync(text, sample.MustKeep, sample.MustNotClaim, CancellationToken.None);
            var fidelityPass = sem.Error is null && sem.FactsReallyPass && sem.RealForbidden == 0 && !sem.MeaningChanged;
            var llmTier = await ((ISendabilityJudge)tierJudge).JudgeAsync(text, CancellationToken.None);

            var qualityPass = det.Passed && fidelityPass && llmTier.Tier != SendabilityTier.Unsendable;

            var row = new QualityAbRow(
                sample.Id, sample.Category, true,
                det.FactPass, det.ProtectedTermPass, det.BoundaryPass, det.SendabilityTier,
                det.DriftedTerms, det.FlippedBoundaries,
                fidelityPass, sem.RealForbidden, sem.MeaningChanged, sem.Error,
                llmTier.Tier, qualityPass);
            rows.Add(row);

            Console.WriteLine(
                $"{sample.Id}: det={(det.Passed ? "pass" : "FAIL")} "
                + $"(P={YN(det.ProtectedTermPass)} B={YN(det.BoundaryPass)} S={det.SendabilityTier}) "
                + $"fidelity={(fidelityPass ? "pass" : "FAIL")} llmTier={llmTier.Tier} "
                + $"=> quality={(qualityPass ? "PASS" : "fail")}");
        }

        var report = Render(rows, config, startedAt, deepseek.CallCount, modelCounter.CallCount, signalCounter.CallCount, judge);
        Directory.CreateDirectory(config.OutputDirectory);
        var path = Path.Combine(config.OutputDirectory, $"{startedAt:yyyyMMdd-HHmmss}-quality-ab-t0.md");
        await File.WriteAllTextAsync(path, report);
        Console.WriteLine($"\nWrote {path}");
        Console.WriteLine(Summary(rows));
        return 0;
    }

    private static string Summary(IReadOnlyList<QualityAbRow> rows)
    {
        var withOutput = rows.Where(r => r.HasOutput).ToList();
        return $"Quality A/B (t0): output={withOutput.Count}/{rows.Count}, "
            + $"detPass={withOutput.Count(r => r.DetPass)}/{withOutput.Count}, "
            + $"fidelityPass={withOutput.Count(r => r.FidelityPass)}/{withOutput.Count}, "
            + $"qualityPass={withOutput.Count(r => r.QualityPass)}/{withOutput.Count}";
    }

    private static string Render(
        IReadOnlyList<QualityAbRow> rows, EvalConfig config, DateTimeOffset startedAt,
        int deepseekCalls, int modelCalls, int saplingCalls, SemanticEvalJudge judge)
    {
        var withOutput = rows.Where(r => r.HasOutput).ToList();
        var n = withOutput.Count;
        var sb = new StringBuilder();
        sb.AppendLine("# Quality A/B — T0 baseline audit (Phase 1)");
        sb.AppendLine();
        sb.AppendLine("**Eval-only.** Production engine (T0) output audited through the deterministic QualityGateChain "
            + "(Fact · ProtectedTerm · Boundary · Sendability) + LLM FidelityJudge v2 + SendabilityTierJudge. "
            + "DeepSeek + Sapling only; **no Pangram/GPTZero** (detection track closed, not a selection signal).");
        sb.AppendLine();
        sb.AppendLine($"Started: {startedAt:O} · model: {config.Model} · judge prompt: {SemanticEvalJudge.PromptVersion}");
        sb.AppendLine();
        sb.AppendLine("## Headline (this is also a false-positive audit: T0 is the trusted faithful engine)");
        sb.AppendLine();
        sb.AppendLine($"- T0 produced output: **{n}/{rows.Count}**");
        if (n > 0)
        {
            sb.AppendLine($"- Deterministic chain pass: **{withOutput.Count(r => r.DetPass)}/{n}** "
                + $"(ProtectedTerm {withOutput.Count(r => r.ProtectedPass)}/{n}, Boundary {withOutput.Count(r => r.BoundaryPass)}/{n}, "
                + $"Sendable {withOutput.Count(r => r.DetTier != SendabilityTier.Unsendable)}/{n}, Fact {withOutput.Count(r => r.FactPass)}/{n})");
            sb.AppendLine($"- LLM FidelityJudge facts pass: **{withOutput.Count(r => r.FidelityPass)}/{n}** "
                + $"(forbidden {withOutput.Count(r => r.Forbidden > 0)}, meaning-changed {withOutput.Count(r => r.MeaningChanged)}, judge-error {withOutput.Count(r => r.JudgeError is not null)})");
            sb.AppendLine($"- LLM SendabilityTier unsendable: **{withOutput.Count(r => r.LlmTier == SendabilityTier.Unsendable)}/{n}**");
            sb.AppendLine($"- **Combined quality pass: {withOutput.Count(r => r.QualityPass)}/{n}**");
        }

        sb.AppendLine();
        sb.AppendLine("### Cases a gate flagged (inspect for false positives vs real drift)");
        sb.AppendLine();
        AppendFlagged(sb, "ProtectedTerm drift", withOutput.Where(r => !r.ProtectedPass).Select(r => $"{r.Id}: {string.Join(", ", r.DriftedTerms)}"));
        AppendFlagged(sb, "Boundary flip", withOutput.Where(r => !r.BoundaryPass).Select(r => $"{r.Id}: {string.Join(" | ", r.FlippedBoundaries)}"));
        AppendFlagged(sb, "Sendability (deterministic Unsendable)", withOutput.Where(r => r.DetTier == SendabilityTier.Unsendable).Select(r => r.Id));
        AppendFlagged(sb, "FidelityJudge drift (facts/forbidden/meaning)", withOutput.Where(r => !r.FidelityPass).Select(r => $"{r.Id}: forbid={r.Forbidden} meaning={r.MeaningChanged} err={r.JudgeError ?? "-"}"));
        AppendFlagged(sb, "LLM Sendability Unsendable", withOutput.Where(r => r.LlmTier == SendabilityTier.Unsendable).Select(r => r.Id));

        sb.AppendLine();
        sb.AppendLine("## Per-case");
        sb.AppendLine();
        sb.AppendLine("| Case | Category | T0 | Fact | Protected | Boundary | Sendable(det) | Fidelity | LlmTier | Quality |");
        sb.AppendLine("| --- | --- | :-: | :-: | :-: | :-: | :-: | :-: | :-: | :-: |");
        foreach (var r in rows)
        {
            if (!r.HasOutput)
            {
                sb.AppendLine($"| {r.Id} | {r.Category} | no-output | - | - | - | - | - | - | - |");
                continue;
            }

            sb.AppendLine($"| {r.Id} | {r.Category} | ok | {YN(r.FactPass)} | {YN(r.ProtectedPass)} | {YN(r.BoundaryPass)} | "
                + $"{r.DetTier} | {YN(r.FidelityPass)} | {r.LlmTier} | {(r.QualityPass ? "**PASS**" : "fail")} |");
        }

        sb.AppendLine();
        sb.AppendLine("## Cost");
        sb.AppendLine();
        sb.AppendLine($"DeepSeek (proposer + judges): **{deepseekCalls}** · DeepSeek (T0 rewrites): **{modelCalls}** · Sapling (engine gate): **{saplingCalls}** · AI-detection: **0**");
        sb.AppendLine();
        return sb.ToString();
    }

    private static void AppendFlagged(StringBuilder sb, string label, IEnumerable<string> items)
    {
        var list = items.ToList();
        sb.AppendLine(list.Count == 0
            ? $"- {label}: none ✓"
            : $"- {label} ({list.Count}): " + string.Join("; ", list));
    }

    private static string YN(bool value) => value ? "✓" : "✗";

    internal sealed record QualityAbRow(
        string Id, string Category, bool HasOutput,
        bool FactPass, bool ProtectedPass, bool BoundaryPass, SendabilityTier DetTier,
        IReadOnlyList<string> DriftedTerms, IReadOnlyList<string> FlippedBoundaries,
        bool FidelityPass, int Forbidden, bool MeaningChanged, string? JudgeError,
        SendabilityTier LlmTier, bool QualityPass)
    {
        public bool DetPass => FactPass && ProtectedPass && BoundaryPass && DetTier != SendabilityTier.Unsendable;

        public static QualityAbRow NoOutput(EvalCase sample, string? errorCode) =>
            new(sample.Id, sample.Category, false,
                false, false, false, SendabilityTier.Unsendable,
                Array.Empty<string>(), Array.Empty<string>(),
                false, 0, false, errorCode,
                SendabilityTier.Unsendable, false);
    }
}
