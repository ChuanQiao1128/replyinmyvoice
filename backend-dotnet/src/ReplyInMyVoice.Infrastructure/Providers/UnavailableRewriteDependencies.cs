namespace ReplyInMyVoice.Infrastructure.Providers;

public sealed class UnavailableRewriteModelClient(string errorCode) : IRewriteModelClient
{
    public Task<RewriteModelResult> GenerateCandidateAsync(
        RewriteModelRequest request,
        CancellationToken cancellationToken) =>
        Task.FromResult(new RewriteModelResult(null, false, errorCode));
}

public sealed class UnavailableWritingSignalClient(string errorCode) : IWritingSignalClient
{
    public Task<WritingSignalResult> MeasureAsync(string text, CancellationToken cancellationToken) =>
        Task.FromResult(new WritingSignalResult(false, null, errorCode));
}
