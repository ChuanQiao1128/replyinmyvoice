namespace ReplyInMyVoice.Application.UseCases.Account;

public sealed record GetAccountSummaryQuery(string ExternalAuthUserId, string? Email);
