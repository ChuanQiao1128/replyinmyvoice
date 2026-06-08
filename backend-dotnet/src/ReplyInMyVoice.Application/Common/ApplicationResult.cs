namespace ReplyInMyVoice.Application.Common;

public sealed record ApplicationResult<T>(
    ApplicationResultKind Kind,
    T? Value,
    string? ErrorCode = null)
{
    public static ApplicationResult<T> Success(T value) =>
        new(ApplicationResultKind.Success, value);

    public static ApplicationResult<T> Created(T value) =>
        new(ApplicationResultKind.Created, value);

    public static ApplicationResult<T> Existing(T value) =>
        new(ApplicationResultKind.Existing, value);

    public static ApplicationResult<T> NotFound() =>
        new(ApplicationResultKind.NotFound, default);

    public static ApplicationResult<T> QuotaExceeded(string? errorCode = null) =>
        new(ApplicationResultKind.QuotaExceeded, default, errorCode);

    public static ApplicationResult<T> Conflict(T value) =>
        new(ApplicationResultKind.Conflict, value);
}
