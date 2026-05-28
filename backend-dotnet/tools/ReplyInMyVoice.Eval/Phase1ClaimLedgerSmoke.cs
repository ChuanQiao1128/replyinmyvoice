using System.Net.Http.Headers;
using System.Text.Json;
using ReplyInMyVoice.Domain.Quality;
using ReplyInMyVoice.Domain.RewriteEngine;

namespace ReplyInMyVoice.Eval;

// Sub-Phase 1.0 smoke: runs the C# ClaimLedger extractor end-to-end against the same 10 corpus
// cases the Python v2 validator covered. Confirms the C# code path (Domain types + DeepSeek
// client + parser cleanup) produces a sensible ledger from real DeepSeek output, before we
// wire up Youdao EN→ZH + post-check + minimal repair in 1.1 / 1.2.
//
// Trigger: PHASE1_CLAIM_LEDGER_VALIDATE=1
// Requires: DEEPSEEK_API_KEY (already in .env.local)
// Outputs: per-case kept-claim count + side-by-side vs the Python v2 numbers (hard-coded
//          baseline; deviation > ±2 should be investigated).
internal static class Phase1ClaimLedgerSmoke
{
    // Python v2 reference counts (after the C#-side cleanup rules would apply, which the
    // Python script doesn't run — so C# numbers may be 0–2 lower per case due to dedupe +
    // empathy-skip). See /tmp/claim_ledger_validation_v2/.
    private static readonly Dictionary<string, int> PythonV2Counts = new()
    {
        ["rewrite-draft-001"] = 12,  // C#: expect 11 after empathy-skip on C011
        ["rewrite-draft-005"] = 8,
        ["rewrite-draft-007"] = 7,
        ["rewrite-draft-008"] = 8,
        ["rewrite-draft-013"] = 6,
        ["rewrite-draft-014"] = 10,  // C#: expect 9 after dedupe on C007==C008
        ["rewrite-draft-017"] = 6,
        ["rewrite-draft-028"] = 8,
        ["rewrite-draft-029"] = 10,
        ["rewrite-draft-041"] = 20,
    };

    private static readonly string[] CaseIds = PythonV2Counts.Keys.ToArray();

    public static async Task<int> RunAsync(IReadOnlyList<EvalCase> allCases)
    {
        var apiKey = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Console.Error.WriteLine("Phase1 smoke: DEEPSEEK_API_KEY not set.");
            return 2;
        }
        var baseUrl = Environment.GetEnvironmentVariable("OPENAI_BASE_URL") ?? "https://api.deepseek.com";
        var model = "deepseek-chat";

        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        var chat = new DeepSeekChatClient(http, apiKey, model, baseUrl, TimeSpan.FromSeconds(90));
        var extractor = new DeepSeekClaimLedgerExtractor(chat);

        var byId = allCases.ToDictionary(c => c.Id, StringComparer.Ordinal);
        Console.WriteLine($"=== Phase 1.0 ClaimLedger C# smoke — {CaseIds.Length} cases ===");
        Console.WriteLine($"{"case",-22} {"py_v2",6} {"csharp",7} {"delta",6}  {"summary",-40}");

        var failures = 0;
        var totalCsharp = 0;
        var totalPy = 0;

        foreach (var cid in CaseIds)
        {
            if (!byId.TryGetValue(cid, out var c))
            {
                Console.WriteLine($"{cid,-22} {"-",6} {"-",7} {"-",6}  draft not found in corpus");
                failures++;
                continue;
            }

            RewriteClaimLedger ledger;
            try
            {
                ledger = await extractor.ExtractAsync(c.InputDraft, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{cid,-22} {"-",6} {"-",7} {"-",6}  EXTRACT ERROR: {ex.GetType().Name}");
                failures++;
                continue;
            }

            var pyV2 = PythonV2Counts[cid];
            var csharp = ledger.Claims.Count;
            var delta = csharp - pyV2;
            totalCsharp += csharp;
            totalPy += pyV2;

            var summary = SummarizeLedger(ledger);
            var deltaStr = delta switch { 0 => "  =", > 0 => $"+{delta}", _ => delta.ToString() };
            Console.WriteLine($"{cid,-22} {pyV2,6} {csharp,7} {deltaStr,6}  {summary,-40}");
        }

        Console.WriteLine(new string('-', 80));
        Console.WriteLine($"{"TOTAL",-22} {totalPy,6} {totalCsharp,7} {(totalCsharp - totalPy >= 0 ? "+" : "") + (totalCsharp - totalPy),6}");
        Console.WriteLine();
        Console.WriteLine($"DeepSeek calls: {chat.CallCount}");

        if (failures > 0)
        {
            Console.Error.WriteLine($"Phase1 smoke: {failures} case(s) errored.");
            return 1;
        }
        return 0;
    }

    private static string SummarizeLedger(RewriteClaimLedger ledger)
    {
        if (ledger.Claims.Count == 0) return "(empty)";
        var negCount = ledger.Claims.Count(c => c.Polarity == RewriteClaimPolarity.Negative);
        var prohCount = ledger.Claims.Count(c => c.Modality == RewriteClaimModality.Prohibition);
        var condCount = ledger.Claims.Count(c => c.Condition != null);
        var timeCount = ledger.Claims.Count(c => c.TimeScope != null);
        return $"neg={negCount} proh={prohCount} cond={condCount} time={timeCount}";
    }
}
