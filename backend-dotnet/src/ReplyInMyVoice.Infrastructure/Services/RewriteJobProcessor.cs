using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Providers;

namespace ReplyInMyVoice.Infrastructure.Services;

public sealed class RewriteJobProcessor(
    Func<AppDbContext> dbContextFactory,
    IRewriteProvider rewriteProvider)
{
    public async Task ProcessAsync(RewriteJob job, CancellationToken cancellationToken)
    {
        var quotaService = new QuotaService(dbContextFactory);
        var now = DateTimeOffset.UtcNow;
        RewriteRequest request;

        await using (var db = dbContextFactory())
        {
            var attempt = await db.RewriteAttempts
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == job.AttemptId, cancellationToken);

            if (attempt is null ||
                attempt.Status is RewriteAttemptStatus.Succeeded or RewriteAttemptStatus.Failed or RewriteAttemptStatus.Released or RewriteAttemptStatus.Expired)
            {
                return;
            }

            if (attempt.ExpiresAt <= now)
            {
                await quotaService.ReleaseAsync(job.AttemptId, "reservation_expired", now, cancellationToken);
                return;
            }

            try
            {
                request = JsonSerializer.Deserialize<RewriteRequest>(
                    attempt.RequestJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new JsonException();
            }
            catch (JsonException)
            {
                await quotaService.ReleaseAsync(job.AttemptId, "request_json_parse_failed", now, cancellationToken);
                return;
            }
        }

        await quotaService.MarkProcessingAsync(job.AttemptId, now, cancellationToken);

        var result = await rewriteProvider.RewriteAsync(job.AttemptId, request, cancellationToken);

        if (result.Success && IsValidResultJson(result.ResultJson))
        {
            await quotaService.FinalizeSuccessAsync(job.AttemptId, result.ResultJson!, DateTimeOffset.UtcNow, cancellationToken);
            return;
        }

        await quotaService.ReleaseAsync(
            job.AttemptId,
            result.Success ? "provider_json_parse_failed" : result.ErrorCode ?? "provider_failed",
            DateTimeOffset.UtcNow,
            cancellationToken);
    }

    private static bool IsValidResultJson(string? resultJson)
    {
        if (string.IsNullOrWhiteSpace(resultJson))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(resultJson);
            var root = doc.RootElement;
            return root.TryGetProperty("rewrittenText", out var rewrittenText) &&
                rewrittenText.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(rewrittenText.GetString()) &&
                root.TryGetProperty("changeSummary", out var changeSummary) &&
                changeSummary.ValueKind == JsonValueKind.Array &&
                root.TryGetProperty("riskNotes", out var riskNotes) &&
                riskNotes.ValueKind == JsonValueKind.Array;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
