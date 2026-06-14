using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Infrastructure.Providers;

namespace ReplyInMyVoice.Tests;

internal sealed class FakeRewriteProvider(RewriteProviderResult result) : IRewriteProvider
{
    public int CallCount { get; private set; }
    public RewriteRequest? SeenRequest { get; private set; }

    public Task<RewriteProviderResult> RewriteAsync(
        Guid attemptId,
        RewriteRequest request,
        CancellationToken cancellationToken)
    {
        CallCount += 1;
        SeenRequest = request;
        return Task.FromResult(result);
    }
}

internal sealed class NoopOutboxFastPathDispatcher : IOutboxFastPathDispatcher
{
    public Task TryDispatchAsync(Guid outboxMessageId, CancellationToken ct = default) =>
        Task.CompletedTask;
}
