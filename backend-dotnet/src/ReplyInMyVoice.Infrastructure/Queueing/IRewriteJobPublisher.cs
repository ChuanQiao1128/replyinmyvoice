using ReplyInMyVoice.Domain.Contracts;

namespace ReplyInMyVoice.Infrastructure.Queueing;

public interface IRewriteJobPublisher
{
    Task PublishAsync(RewriteJob job, CancellationToken cancellationToken);
}
