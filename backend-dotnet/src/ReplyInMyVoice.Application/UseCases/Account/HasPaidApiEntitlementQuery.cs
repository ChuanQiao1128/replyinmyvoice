namespace ReplyInMyVoice.Application.UseCases.Account;

public sealed record HasPaidApiEntitlementQuery(Guid UserId, DateTimeOffset Now);
