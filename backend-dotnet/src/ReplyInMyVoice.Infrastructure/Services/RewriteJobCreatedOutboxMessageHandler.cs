using System.Diagnostics;
using System.Text.Json;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Infrastructure.Queueing;

namespace ReplyInMyVoice.Infrastructure.Services;

public sealed class RewriteJobCreatedOutboxMessageHandler(IRewriteJobPublisher jobPublisher) : IOutboxMessageHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public string MessageType => "RewriteJobCreated";

    public async Task HandleAsync(OutboxMessage message, CancellationToken ct = default)
    {
        var payload = JsonSerializer.Deserialize<RewriteJobCreatedPayload>(
            message.PayloadJson,
            JsonOptions);
        if (payload is null || payload.AttemptId == Guid.Empty)
        {
            throw new JsonException("Outbox payload did not contain a valid attempt id.");
        }

        await jobPublisher.PublishAsync(
            new RewriteJob(payload.AttemptId, message.CorrelationId, Activity.Current?.Id),
            ct);
    }

    private sealed record RewriteJobCreatedPayload(Guid AttemptId);
}
