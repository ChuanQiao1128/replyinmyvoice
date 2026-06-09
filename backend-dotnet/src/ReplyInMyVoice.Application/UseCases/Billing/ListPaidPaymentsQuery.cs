namespace ReplyInMyVoice.Application.UseCases.Billing;

public sealed record ListPaidPaymentsQuery(
    DateTimeOffset WindowStart,
    DateTimeOffset WindowEnd);
