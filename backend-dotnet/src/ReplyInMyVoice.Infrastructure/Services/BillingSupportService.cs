using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Notifications;

namespace ReplyInMyVoice.Infrastructure.Services;

public sealed class BillingSupportService(
    Func<AppDbContext> dbContextFactory,
    INotificationService notificationService)
{
    private const int MaxMessageLength = 2000;
    private const int MaxPaymentIntentLength = 160;
    private const string SupportEmail = "info@timeawake.co.nz";

    public async Task<BillingSupportRequestServiceResult> CreateForUserAsync(
        AppUser user,
        BillingSupportCreateRequest request,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseType(request.Type, out var requestType))
        {
            return BillingSupportRequestServiceResult.InvalidRequest(
                "Request type must be refund or billing-question.");
        }

        var message = NormalizeMessage(request.Message);
        if (message is null)
        {
            return BillingSupportRequestServiceResult.InvalidRequest(
                "Message must be between 10 and 2000 characters.");
        }

        var paymentIntentId = NormalizePaymentIntentId(request.RelatedPaymentIntentId);
        if (paymentIntentId == InvalidPaymentIntentMarker)
        {
            return BillingSupportRequestServiceResult.InvalidRequest(
                "Payment intent id must be 160 characters or less.");
        }

        await using var db = dbContextFactory();
        if (!string.IsNullOrWhiteSpace(paymentIntentId))
        {
            var ownsPayment = await db.RewriteCredits
                .AsNoTracking()
                .AnyAsync(
                    x => x.UserId == user.Id && x.StripePaymentIntentId == paymentIntentId,
                    cancellationToken);
            if (!ownsPayment)
            {
                return BillingSupportRequestServiceResult.InvalidRequest(
                    "Selected payment was not found for this account.");
            }
        }

        var entity = new BillingSupportRequest
        {
            UserId = user.Id,
            Type = requestType,
            RelatedPaymentIntentId = paymentIntentId,
            Message = message,
            Status = BillingSupportRequestStatus.Open,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.BillingSupportRequests.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        await notificationService.SendAsync(
            NotificationTemplates.BillingSupportRequestReceived,
            new NotificationRecipient(user.Email ?? string.Empty, user.Email),
            new BillingSupportRequestReceivedNotificationModel(
                CustomerName: user.Email ?? "there",
                SupportEmail: SupportEmail,
                RequestReference: $"request {entity.Id:N}"[..20]),
            cancellationToken);

        return BillingSupportRequestServiceResult.Success(ToResponse(entity));
    }

    public async Task<IReadOnlyList<BillingSupportRequestResponse>> GetForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using var db = dbContextFactory();
        var rows = await db.BillingSupportRequests
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .ToListAsync(cancellationToken);

        return rows
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Select(ToResponse)
            .ToList();
    }

    private const string InvalidPaymentIntentMarker = "\u0000";

    private static string? NormalizeMessage(string? message)
    {
        var normalized = message?.Trim();
        return normalized is { Length: >= 10 and <= MaxMessageLength }
            ? normalized
            : null;
    }

    private static string? NormalizePaymentIntentId(string? paymentIntentId)
    {
        var normalized = paymentIntentId?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized.Length <= MaxPaymentIntentLength
            ? normalized
            : InvalidPaymentIntentMarker;
    }

    private static bool TryParseType(
        string? value,
        out BillingSupportRequestType requestType)
    {
        var normalized = value?.Trim().Replace("_", "-", StringComparison.Ordinal).ToLowerInvariant();
        requestType = normalized switch
        {
            "refund" => BillingSupportRequestType.Refund,
            "billing-question" => BillingSupportRequestType.BillingQuestion,
            _ => default,
        };
        return normalized is "refund" or "billing-question";
    }

    private static BillingSupportRequestResponse ToResponse(BillingSupportRequest request) =>
        new(
            request.Id,
            request.UserId,
            FormatType(request.Type),
            request.RelatedPaymentIntentId,
            request.Message,
            FormatStatus(request.Status),
            request.CreatedAt,
            request.UpdatedAt,
            request.ResolvedAt);

    internal static string FormatType(BillingSupportRequestType type) =>
        type switch
        {
            BillingSupportRequestType.BillingQuestion => "billing-question",
            _ => "refund",
        };

    internal static string FormatStatus(BillingSupportRequestStatus status) =>
        status switch
        {
            BillingSupportRequestStatus.Resolved => "resolved",
            _ => "open",
        };
}

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
