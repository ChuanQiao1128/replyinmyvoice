using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Domain.Entities;

namespace ReplyInMyVoice.Application.UseCases.Account;

public sealed class GetOrCreateUserHandler(
    IAppUserRepository appUsers,
    IUnitOfWork unitOfWork)
{
    public async Task<AppUser> HandleAsync(
        GetOrCreateUserCommand command,
        CancellationToken ct = default) =>
        await AccountUseCaseSupport.GetOrCreateUserAsync(
            appUsers,
            unitOfWork,
            command.ExternalAuthUserId,
            command.Email,
            ct);
}
