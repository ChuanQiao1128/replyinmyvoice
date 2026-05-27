using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

// Shared inputs + GPTZero scorer for the U2/U3 "last-shot" AI-detection pilots. EVAL-ONLY.
// Two AI-style drafts whose every fact fits a fixed template (U3) or a terse fact-note (U2), so the
// semantic gate can be reached and GPTZero actually scores the output — unlike the production hard
// cases (005 sales-quote, 041 multi-item partial refund), which are too fact-dense for either method
// and would fail the gate before reaching GPTZero. Kept in one place so U2 and U3 score identical
// inputs (any divergence would invalidate the comparison).
internal sealed record SimpleEmailCase(
    string Id,
    string EmailType,
    string Draft,
    IReadOnlyList<string> MustKeep,
    IReadOnlyList<string> MustNotClaim);

internal static class SimplePilotCases
{
    public static readonly IReadOnlyList<SimpleEmailCase> All = new[]
    {
        new SimpleEmailCase(
            "simple-scheduling",
            "scheduling",
            "Hi Mark,\n\n"
            + "Thank you so much for sending over the invitation to the planning meeting — I really "
            + "appreciate you thinking of me and including me in this important discussion.\n\n"
            + "Unfortunately, I wanted to reach out and let you know that I won't be able to attend the "
            + "meeting scheduled for Tuesday, as I have a prior commitment that conflicts with that "
            + "particular time slot. That said, I would be more than happy to meet on Wednesday afternoon "
            + "instead, should that work for everyone involved.\n\n"
            + "Please don't hesitate to let me know if Wednesday afternoon would be convenient for you. I "
            + "look forward to hearing from you soon.\n\n"
            + "Best regards,\nDana",
            new[]
            {
                "Mark", "Tuesday", "Wednesday afternoon", "Dana",
                "cannot attend the meeting on Tuesday", "proposes meeting Wednesday afternoon instead",
                "asks the recipient to confirm",
            },
            new[]
            {
                "Do not invent a new date, time, or location",
                "Do not add any commitment, agenda, or detail that is not in the draft",
            }),
        new SimpleEmailCase(
            "simple-billing",
            "billing",
            "Hello,\n\n"
            + "I hope this email finds you well. I am writing to follow up regarding invoice INV-204, which "
            + "was issued in the amount of $1,250.00 and carried a due date of June 10.\n\n"
            + "At this time, our records indicate that we have not yet received payment for this particular "
            + "invoice. We completely understand that these things can occasionally slip through the cracks, "
            + "and we simply wanted to gently check in with you regarding the matter.\n\n"
            + "Could you kindly confirm whether this payment may have been processed under a different "
            + "reference number? We would greatly appreciate your assistance in helping us resolve this.\n\n"
            + "Warm regards",
            new[]
            {
                "INV-204", "$1,250.00", "June 10",
                "payment has not been received yet",
                "asks to confirm whether it was paid under a different reference number",
            },
            new[]
            {
                "Do not state or imply the customer is at fault or has done something wrong",
                "Do not invent a new amount, date, penalty, or late fee",
            }),
    };
}

// Hardened GPTZero scorer shared by the pilots. NEVER echoes the response body — GPTZero's error
// payload reflects the api key back (a key was leaked once that way earlier and had to be rotated).
internal static class GptzeroScorer
{
    public static async Task<(int? Ai, string? Cls, string? Err)> ScoreAsync(
        HttpClient http, string key, string text, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.gptzero.me/v2/predict/text");
            req.Headers.Add("x-api-key", key);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            req.Content = new StringContent(JsonSerializer.Serialize(new { document = text }), Encoding.UTF8, "application/json");
            using var resp = await http.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                return (null, null, $"http_{(int)resp.StatusCode}");
            }

            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("documents", out var docs) || docs.ValueKind != JsonValueKind.Array || docs.GetArrayLength() == 0)
            {
                return (null, null, "no_documents");
            }

            var d = docs[0];
            double? aiProb = null;
            if (d.TryGetProperty("class_probabilities", out var cp) && cp.ValueKind == JsonValueKind.Object
                && cp.TryGetProperty("ai", out var aiEl) && aiEl.ValueKind == JsonValueKind.Number)
            {
                aiProb = aiEl.GetDouble();
            }
            else if (d.TryGetProperty("completely_generated_prob", out var cg) && cg.ValueKind == JsonValueKind.Number)
            {
                aiProb = cg.GetDouble();
            }

            var cls = d.TryGetProperty("document_classification", out var dc) && dc.ValueKind == JsonValueKind.String ? dc.GetString() : null;
            if (aiProb is null && cls is not null)
            {
                aiProb = cls == "AI_ONLY" ? 0.99 : cls == "HUMAN_ONLY" ? 0.01 : 0.5;
            }

            return (aiProb is null ? null : (int)Math.Round(aiProb.Value * 100), cls, null);
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or OperationCanceledException)
        {
            return (null, null, e.GetType().Name);
        }
    }
}
