namespace ReplyInMyVoice.Application.Abstractions;

public interface IDbExceptionClassifier
{
    /// <summary>
    /// Returns true for retryable database concurrency races, including SQL Server
    /// error numbers 1205, 2627, 2601, and 3960 plus SQLite busy signals.
    /// </summary>
    bool IsRetryableConcurrencyRace(Exception exception);
}
