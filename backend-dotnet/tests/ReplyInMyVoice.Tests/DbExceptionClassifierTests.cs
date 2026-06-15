using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Infrastructure.Repositories;

namespace ReplyInMyVoice.Tests;

public sealed class DbExceptionClassifierTests
{
    private readonly DbExceptionClassifier _classifier = new();

    [Theory]
    [InlineData(1205)]
    [InlineData(2627)]
    [InlineData(2601)]
    [InlineData(3960)]
    public void IsRetryableConcurrencyRace_returns_true_for_retryable_sql_numbers(int number)
    {
        var exception = new NumberedDbException(number);

        var result = _classifier.IsRetryableConcurrencyRace(exception);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsRetryableConcurrencyRace_returns_false_for_non_retryable_sql_number()
    {
        var exception = new NumberedDbException(547);

        var result = _classifier.IsRetryableConcurrencyRace(exception);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsRetryableConcurrencyRace_returns_true_for_db_update_concurrency_exception()
    {
        var result = _classifier.IsRetryableConcurrencyRace(new DbUpdateConcurrencyException());

        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    public void IsRetryableConcurrencyRace_returns_true_for_sqlite_busy_error_codes(int sqliteErrorCode)
    {
        var exception = new SqliteException("SQLite busy", sqliteErrorCode);

        var result = _classifier.IsRetryableConcurrencyRace(exception);

        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("database is locked")]
    [InlineData("database table is locked")]
    public void IsRetryableConcurrencyRace_returns_true_for_sqlite_busy_message_fallback(string message)
    {
        var exception = new InvalidOperationException(message);

        var result = _classifier.IsRetryableConcurrencyRace(exception);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsRetryableConcurrencyRace_returns_false_for_plain_exception()
    {
        var result = _classifier.IsRetryableConcurrencyRace(new InvalidOperationException("plain failure"));

        result.Should().BeFalse();
    }

    [Fact]
    public void IsRetryableConcurrencyRace_walks_inner_exception_chain_for_sql_number()
    {
        var exception = new DbUpdateException(
            "Save failed.",
            new InvalidOperationException(
                "Nested provider failure.",
                new NumberedDbException(2601)));

        var result = _classifier.IsRetryableConcurrencyRace(exception);

        result.Should().BeTrue();
    }

    private sealed class NumberedDbException(int number) : Exception("Provider database failure.")
    {
        public int Number { get; } = number;
    }
}
