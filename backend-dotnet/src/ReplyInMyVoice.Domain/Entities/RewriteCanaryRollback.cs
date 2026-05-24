namespace ReplyInMyVoice.Domain.Entities;

public sealed class RewriteCanaryRollback
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string CanaryStrategyVersion { get; set; }
    public required string ControlStrategyVersion { get; set; }
    public required string Scenario { get; set; }
    public string State { get; set; } = "open";
    public int WindowRewrites { get; set; }
    public double RollingAverageSignalDrop { get; set; }
    public double BaselineAverageSignalDrop { get; set; }
    public double RegressionPoints { get; set; }
    public DateTimeOffset? WindowStartedAt { get; set; }
    public DateTimeOffset? WindowEndedAt { get; set; }
    public string AdminEmailStatus { get; set; } = "pending";
    public DateTimeOffset? AdminEmailSentAt { get; set; }
    public string GithubIssueStatus { get; set; } = "pending";
    public string? GithubIssueUrl { get; set; }
    public DateTimeOffset? GithubIssueOpenedAt { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset LastCheckedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ResolvedAt { get; set; }
    public Guid RowVersion { get; set; } = Guid.NewGuid();
}
