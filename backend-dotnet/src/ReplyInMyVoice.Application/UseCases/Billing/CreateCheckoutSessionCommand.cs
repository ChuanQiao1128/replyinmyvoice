namespace ReplyInMyVoice.Application.UseCases.Billing;

public sealed record CreateCheckoutSessionCommand(
    string ExternalAuthUserId,
    string? Email,
    string? Sku);
