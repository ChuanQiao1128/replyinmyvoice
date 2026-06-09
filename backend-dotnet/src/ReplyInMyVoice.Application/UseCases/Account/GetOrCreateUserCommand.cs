namespace ReplyInMyVoice.Application.UseCases.Account;

public sealed record GetOrCreateUserCommand(string ExternalAuthUserId, string? Email);
