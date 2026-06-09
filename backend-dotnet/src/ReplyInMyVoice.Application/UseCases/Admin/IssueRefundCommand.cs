namespace ReplyInMyVoice.Application.UseCases.Admin;

public sealed record IssueRefundCommand(
    string AdminExternalAuthUserId,
    string? AdminEmail,
    Guid TargetUserId,
    string? PaymentIntentId,
    long? Amount,
    string? Currency);
