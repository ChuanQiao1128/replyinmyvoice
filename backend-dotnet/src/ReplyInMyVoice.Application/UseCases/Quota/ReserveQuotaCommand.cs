namespace ReplyInMyVoice.Application.UseCases.Quota;

public sealed record ReserveQuotaCommand(
    Guid UserId,
    string IdempotencyKey,
    string RequestHash,
    string RequestJson,
    string PeriodKey,
    int QuotaLimit,
    DateTimeOffset Now,
    TimeSpan ReservationTtl,
    Guid? ApiKeyId = null);
