using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Domain.Entities;

namespace ReplyInMyVoice.Application.UseCases.Admin;

public sealed class IssueRefundHandler(
    IAdminUserRepository adminUsers,
    IRewriteCreditRepository credits,
    IStripeRefundClient? stripeRefundClient,
    IUnitOfWork unitOfWork)
{
    public async Task<AdminRefundResultDto> HandleAsync(
        IssueRefundCommand command,
        CancellationToken ct = default)
    {
        var paymentIntentId = command.PaymentIntentId?.Trim();
        if (string.IsNullOrWhiteSpace(paymentIntentId))
        {
            return AdminRefundResultDto.InvalidRequest("A payment intent id is required.");
        }

        if (command.Amount is <= 0)
        {
            return AdminRefundResultDto.InvalidRequest("Refund amount must be greater than zero.");
        }

        var requestedCurrency = NormalizeCurrency(command.Currency);

        if (!await adminUsers.UserExistsAsync(command.TargetUserId, ct))
        {
            return AdminRefundResultDto.UserNotFound("No user exists for the requested id.");
        }

        var payment = await credits.GetRefundPaymentLookupAsync(
            command.TargetUserId,
            paymentIntentId,
            ct);
        if (payment is null)
        {
            return AdminRefundResultDto.PaymentNotFound("No payment exists for the requested user and payment intent.");
        }

        var amount = command.Amount ?? payment.AmountTotal;
        if (amount is not > 0)
        {
            return AdminRefundResultDto.InvalidRequest("Refund amount must be provided for payments without a stored amount.");
        }

        if (payment.AmountTotal is > 0 && amount > payment.AmountTotal.Value)
        {
            return AdminRefundResultDto.InvalidRequest("Refund amount cannot exceed the stored payment amount.");
        }

        var currency = requestedCurrency ?? NormalizeCurrency(payment.Currency);
        if (!string.IsNullOrWhiteSpace(requestedCurrency) &&
            !string.IsNullOrWhiteSpace(payment.Currency) &&
            !string.Equals(requestedCurrency, NormalizeCurrency(payment.Currency), StringComparison.OrdinalIgnoreCase))
        {
            return AdminRefundResultDto.InvalidRequest("Refund currency must match the stored payment currency.");
        }

        var existingRefund = await adminUsers.FindRefundAuditDetailsAsync(
            command.TargetUserId,
            paymentIntentId,
            amount.Value,
            ct);
        if (existingRefund is not null)
        {
            return AdminRefundResultDto.Success(new AdminRefundResponseDto(
                command.TargetUserId,
                paymentIntentId,
                amount.Value,
                existingRefund.Currency ?? currency,
                existingRefund.RefundId,
                AlreadyRefunded: true));
        }

        if (stripeRefundClient is null)
        {
            return AdminRefundResultDto.RefundUnavailable("Stripe refund services are not configured.");
        }

        var refund = await stripeRefundClient.RefundPaymentAsync(
            new StripeRefundRequest(
                paymentIntentId,
                amount.Value,
                currency,
                CreateRefundIdempotencyKey(command.TargetUserId, paymentIntentId, amount.Value),
                command.TargetUserId),
            ct);

        var details = new AdminRefundAuditDetailsDto(
            paymentIntentId,
            refund.RefundId,
            amount.Value,
            refund.Currency ?? currency,
            refund.Status);
        await adminUsers.AddAuditLogAsync(new AdminAuditLog
        {
            AdminExternalAuthUserId = command.AdminExternalAuthUserId.Trim(),
            AdminEmail = command.AdminEmail?.Trim() ?? string.Empty,
            Action = "refund",
            TargetUserId = command.TargetUserId,
            DetailsJson = AdminUseCaseSupport.SerializeAuditDetails(details),
            CreatedAt = DateTimeOffset.UtcNow,
        }, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return AdminRefundResultDto.Success(new AdminRefundResponseDto(
            command.TargetUserId,
            paymentIntentId,
            amount.Value,
            refund.Currency ?? currency,
            refund.RefundId,
            AlreadyRefunded: false));
    }

    private static string CreateRefundIdempotencyKey(
        Guid targetUserId,
        string paymentIntentId,
        long amount) =>
        $"admin-refund:{targetUserId:N}:{paymentIntentId}:{amount}";

    private static string? NormalizeCurrency(string? currency)
    {
        var normalized = currency?.Trim();
        return string.IsNullOrWhiteSpace(normalized)
            ? null
            : normalized.ToLowerInvariant();
    }
}
