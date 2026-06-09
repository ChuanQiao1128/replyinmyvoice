namespace ReplyInMyVoice.Application.UseCases.Admin;

public sealed record GetAdminUsersQuery(
    int Page,
    int PageSize,
    string? Search = null);
