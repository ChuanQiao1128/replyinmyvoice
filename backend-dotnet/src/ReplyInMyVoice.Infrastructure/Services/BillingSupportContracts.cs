namespace ReplyInMyVoice.Infrastructure.Services;

// Surviving contracts relocated out of the deleted BillingSupportService (Wave-4 cleanup tail).
// The old service class was dead (its use-cases moved to Application/UseCases/BillingSupport),
// but these records/enum are still consumed by the Application handlers + AccountHttpFunctions,
// so they live on here with identical names/namespace.

public sealed record BillingSupportCreateRequest(
    string? Type,
    string? RelatedPaymentIntentId,
    string? Message);

public sealed record BillingSupportRequestResponse(
    Guid Id,
    Guid UserId,
    string Type,
    string? RelatedPaymentIntentId,
    string Message,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ResolvedAt);

public sealed record BillingSupportRequestServiceResult(
    BillingSupportRequestResultKind Kind,
    BillingSupportRequestResponse? Response,
    string? Detail)
{
    public static BillingSupportRequestServiceResult Success(BillingSupportRequestResponse response) =>
        new(BillingSupportRequestResultKind.Success, response, null);

    public static BillingSupportRequestServiceResult InvalidRequest(string detail) =>
        new(BillingSupportRequestResultKind.InvalidRequest, null, detail);
}

public enum BillingSupportRequestResultKind
{
    Success,
    InvalidRequest,
}
