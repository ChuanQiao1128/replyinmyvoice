namespace ReplyInMyVoice.Application.UseCases.Account;

public sealed record GetBillingHistoryQuery(string ExternalAuthUserId, string? Email);
