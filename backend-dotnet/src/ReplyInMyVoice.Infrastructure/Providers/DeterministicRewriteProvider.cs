using System.Text.Json;
using ReplyInMyVoice.Domain.Contracts;

namespace ReplyInMyVoice.Infrastructure.Providers;

public sealed class DeterministicRewriteProvider : IRewriteProvider
{
    public Task<RewriteProviderResult> RewriteAsync(Guid attemptId, RewriteRequest request, CancellationToken cancellationToken)
    {
        var tone = request.Tone == "direct" ? "direct" : "warm";
        var draft = request.RoughDraftReply.Trim();
        var rewritten = tone == "direct"
            ? $"Thanks for the context. {draft}"
            : $"Thanks for the context. I appreciate it. {draft}";
        var json = JsonSerializer.Serialize(new
        {
            rewrittenText = rewritten,
            changeSummary = new[] { "Kept the reply concise and based on the provided draft." },
            riskNotes = Array.Empty<string>(),
            attemptId,
        });
        return Task.FromResult(new RewriteProviderResult(json, true, null));
    }
}
