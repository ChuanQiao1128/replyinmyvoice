using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Application.Abstractions;

namespace ReplyInMyVoice.Infrastructure.Repositories;

public sealed class DbExceptionClassifier : IDbExceptionClassifier
{
    private static readonly HashSet<int> RetryableSqlServerNumbers =
    [
        1205,
        2627,
        2601,
        3960,
    ];

    public bool IsRetryableConcurrencyRace(Exception exception) =>
        IsRetryableConcurrencyRaceException(exception);

    internal static bool IsRetryableConcurrencyRaceException(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbUpdateConcurrencyException ||
                current is SqliteException { SqliteErrorCode: 5 or 6 } ||
                IsRetryableSqlServerNumber(current) ||
                current.Message.Contains("database is locked", StringComparison.OrdinalIgnoreCase) ||
                current.Message.Contains("database table is locked", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsRetryableSqlServerNumber(Exception exception)
    {
        var numberProperty = exception
            .GetType()
            .GetProperty("Number", BindingFlags.Public | BindingFlags.Instance);

        return numberProperty?.PropertyType == typeof(int) &&
            numberProperty.GetValue(exception) is int number &&
            RetryableSqlServerNumbers.Contains(number);
    }
}
