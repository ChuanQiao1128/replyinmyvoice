using System.Text.Json;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Domain.Entities;
using AppStripeReconciliationAlerter = ReplyInMyVoice.Application.Abstractions.IStripeReconciliationAlerter;

namespace ReplyInMyVoice.Infrastructure.Services;

public sealed class StripeReconciliationAlertOutboxMessageHandler(
    AppStripeReconciliationAlerter alerter)
    : ReplyInMyVoice.Application.Abstractions.IOutboxMessageHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public string MessageType => "StripeReconciliationAlertRequested";

    public async Task HandleAsync(OutboxMessage message, CancellationToken ct = default)
    {
        var report = JsonSerializer.Deserialize<StripeReconciliationReportDto>(
            message.PayloadJson,
            JsonOptions);
        if (report is null)
        {
            throw new JsonException("Outbox payload did not contain a reconciliation report.");
        }

        await alerter.AlertAsync(report, ct);
    }
}
