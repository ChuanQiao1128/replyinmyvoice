using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Domain.Contracts;

namespace ReplyInMyVoice.Infrastructure.Providers;

/// <summary>
/// Boundary adapter from the current <see cref="IRewriteProvider"/> implementation to the frozen
/// <see cref="IRewriteEngineClient"/> port. This adapter owns the call-capture scope used by the
/// provider path; direct engine implementations must populate ProviderCalls themselves.
/// </summary>
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
                ErrorCode: RewriteEngineErrorCodes.ProviderTimeout,
                Map(providerCallCapture.Calls));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new RewriteEngineResult(
                ResultJson: null,
                Success: false,
                ErrorCode: RewriteEngineErrorCodes.ProviderFailed,
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
