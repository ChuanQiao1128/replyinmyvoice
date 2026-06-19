using ReplyInMyVoice.Domain.Contracts;

namespace ReplyInMyVoice.Infrastructure.Queueing;

public sealed class InMemoryRewriteJobPublisher : IRewriteJobPublisher
{
    private readonly List<RewriteJob> _publishedJobs = [];

    public IReadOnlyList<RewriteJob> PublishedJobs => _publishedJobs;

    public Task PublishAsync(
        RewriteJob job,
        CancellationToken cancellationToken,
        string? correlationId = null)
    {
        _publishedJobs.Add(job);
        return Task.CompletedTask;
    }
}
