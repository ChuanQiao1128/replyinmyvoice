using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Domain.Contracts;

namespace ReplyInMyVoice.Infrastructure.Providers;

public sealed class RewriteProviderEngineClient(IRewriteProvider provider) : IRewriteEngineClient
{
    public async Task<RewriteEngineResult> RewriteAsync(
        Guid attemptId,
        RewriteRequest request,
        CancellationToken ct = default)
    {
        using var providerCallCapture = RewriteProviderCallCapture.Begin();
        try
        {
            var result = await provider.RewriteAsync(attemptId, request, ct);
            return new RewriteEngineResult(
                result.ResultJson,
                result.Success,
                result.ErrorCode,
                Map(providerCallCapture.Calls));
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new RewriteEngineResult(
                ResultJson: null,
                Success: false,
                ErrorCode: "provider_timeout",
                Map(providerCallCapture.Calls));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new RewriteEngineResult(
                ResultJson: null,
                Success: false,
                ErrorCode: "provider_failed",
                Map(providerCallCapture.Calls));
        }
    }

    private static IReadOnlyList<RewriteEngineCallMetric> Map(
        IReadOnlyList<RewriteProviderCallMetric> calls) =>
        calls
            .Select(call => new RewriteEngineCallMetric(
                call.Provider,
                call.Role,
                call.Model,
                call.InputTokens,
                call.OutputTokens,
                call.Characters,
                call.LatencyMs,
                call.Success,
                call.ErrorCode))
            .ToArray();
}
