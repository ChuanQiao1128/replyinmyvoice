namespace ReplyInMyVoice.Application.UseCases.Quota;

public sealed record ReleaseExpiredReservationsCommand(
    DateTimeOffset Now,
    int BatchSize = 500);
