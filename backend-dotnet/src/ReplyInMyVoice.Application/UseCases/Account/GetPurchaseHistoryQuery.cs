namespace ReplyInMyVoice.Application.UseCases.Account;

public sealed record GetPurchaseHistoryQuery(string ExternalAuthUserId, string? Email);
