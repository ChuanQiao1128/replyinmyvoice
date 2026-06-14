using System.Data;
using System.Text.Json;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;

namespace ReplyInMyVoice.Application.UseCases.StripeReconciliation;

public sealed class ReconcileStripeHandler(
    IPaymentGrantRepository paymentGrants,
    IStripePaymentReconciliationClient stripeClient,
    IRewriteCreditRepository credits,
    IAppUserRepository appUsers,
    IAdminUserRepository adminUsers,
    IStripeReconciliationRunRepository runs,
    IOutboxMessageRepository outboxMessages,
    StripeReconciliationOptions options,
    IUnitOfWork unitOfWork)
{
    private const int MaxPersistedRowsPerList = 200;
    private const string PurchaseSource = "PURCHASE";
    private const string AlertMessageType = "StripeReconciliationAlertRequested";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<StripeReconciliationReportDto> HandleAsync(
        ReconcileStripeCommand command,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(unitOfWork);

        if (command.WindowEnd <= command.WindowStart)
        {
            throw new ArgumentException("reconciliation_window_invalid", nameof(command));
        }

        var stripePayments = NormalizePayments(await stripeClient.ListPaidPaymentIntentsAsync(
            command.WindowStart,
            command.WindowEnd,
            ct));
        var paymentIntentIds = stripePayments
            .Select(x => x.PaymentIntentId)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var grants = await paymentGrants.ListPurchaseGrantsForReconciliationAsync(
            command.WindowStart,
            command.WindowEnd,
            paymentIntentIds,
            ct);
        var report = BuildReport(
            command.WindowStart,
            command.WindowEnd,
            command.CompletedAt,
            stripePayments,
            grants);
        var autoGrantResult = await AutoGrantMissingPurchaseCreditsAsync(
            command,
            report.Discrepancies
                .Where(x => x.Kind == StripeReconciliationDiscrepancyKindDto.PaidButNoGrant)
                .ToList(),
            ct);
        var subscriptionMismatches = await BuildSubscriptionMismatchesAsync(ct);

        report = StripeReconciliationReportDto.Create(
            command.WindowStart,
            command.WindowEnd,
            command.CompletedAt,
            stripePayments.Count,
            grants.Count,
            report.Discrepancies,
            autoGrantResult.AutoGrants,
            autoGrantResult.ManualReview,
            subscriptionMismatches,
            autoGrantResult.AutoGrantSkippedCount);

        await PersistRunAndMaybeAlertAsync(command, report, ct);

        return report;
    }

    private async Task<AutoGrantPassResult> AutoGrantMissingPurchaseCreditsAsync(
        ReconcileStripeCommand command,
        IReadOnlyList<StripeReconciliationDiscrepancyDto> missingGrantRows,
        CancellationToken ct)
    {
        var autoGrants = new List<StripeReconciliationAutoGrantDto>();
        var manualReview = new List<StripeReconciliationManualReviewDto>();
        var skippedOverCap = 0;

        foreach (var row in missingGrantRows
            .Where(x => !string.IsNullOrWhiteSpace(x.PaymentIntentId))
            .OrderBy(x => x.StripePaidAt ?? DateTimeOffset.MinValue)
            .ThenBy(x => x.PaymentIntentId, StringComparer.Ordinal))
        {
            var paymentIntentId = row.PaymentIntentId!.Trim();
            if (options.AutoGrantMaxPerRun <= 0)
            {
                skippedOverCap++;
                manualReview.Add(new StripeReconciliationManualReviewDto(paymentIntentId, "over_cap"));
                continue;
            }

            if (row.StripePaidAt is { } paidAt &&
                paidAt > command.CompletedAt.AddMinutes(-options.MinPaymentAgeMinutes))
            {
                manualReview.Add(new StripeReconciliationManualReviewDto(paymentIntentId, "payment_too_recent"));
                continue;
            }

            var session = await stripeClient.FindCheckoutSessionForPaymentIntentAsync(paymentIntentId, ct);
            if (session is null)
            {
                manualReview.Add(new StripeReconciliationManualReviewDto(paymentIntentId, "no_checkout_session"));
                continue;
            }

            if (!string.Equals(Normalize(session.Mode), "payment", StringComparison.Ordinal) ||
                !string.Equals(Normalize(session.PaymentStatus), "paid", StringComparison.Ordinal))
            {
                manualReview.Add(new StripeReconciliationManualReviewDto(paymentIntentId, "not_payment_mode"));
                continue;
            }

            var rewrites = ResolveGrantedRewrites(session);
            if (rewrites is not > 0)
            {
                manualReview.Add(new StripeReconciliationManualReviewDto(paymentIntentId, "rewrites_unresolvable"));
                continue;
            }

            var user = await appUsers.FindForStripeCheckoutAsync(
                session.ExternalAuthUserId,
                session.CustomerId,
                ct);
            if (user is null)
            {
                manualReview.Add(new StripeReconciliationManualReviewDto(paymentIntentId, "user_not_found"));
                continue;
            }

            if (autoGrants.Count >= options.AutoGrantMaxPerRun)
            {
                skippedOverCap++;
                manualReview.Add(new StripeReconciliationManualReviewDto(paymentIntentId, "over_cap"));
                continue;
            }

            try
            {
                var grant = await unitOfWork.ExecuteInTransactionAsync(
                    async transactionCt =>
                    {
                        var syntheticEventId = CreateSyntheticEventId(paymentIntentId);
                        if (await credits.ExistsByStripeEventIdAsync(syntheticEventId, transactionCt))
                        {
                            return null;
                        }

                        var existingCredits = await credits.ListByStripePaymentIntentIdAsync(
                            paymentIntentId,
                            transactionCt);
                        if (existingCredits.Any(x => string.Equals(x.Source, PurchaseSource, StringComparison.Ordinal)))
                        {
                            return null;
                        }

                        var credit = new RewriteCredit
                        {
                            UserId = user.Id,
                            Source = PurchaseSource,
                            AmountGranted = rewrites.Value,
                            OriginalAmountGranted = rewrites.Value,
                            AmountConsumed = 0,
                            GrantedAt = command.CompletedAt,
                            ExpiresAt = command.CompletedAt.AddDays(90),
                            StripeEventId = syntheticEventId,
                            StripePaymentIntentId = paymentIntentId,
                            StripeSku = Normalize(session.Sku),
                            StripeAmountTotal = row.StripeAmount ?? session.AmountTotal,
                            StripeCurrency = Normalize(row.StripeCurrency) ?? Normalize(session.Currency),
                        };

                        await credits.AddAsync(credit, transactionCt);
                        await adminUsers.AddAuditLogAsync(new AdminAuditLog
                        {
                            AdminExternalAuthUserId = "system:reconciliation",
                            AdminEmail = string.Empty,
                            Action = "reconciliation_auto_grant",
                            TargetUserId = user.Id,
                            DetailsJson = JsonSerializer.Serialize(new
                            {
                                source = "reconciliation",
                                paymentIntentId,
                                creditId = credit.Id,
                                sku = credit.StripeSku,
                                rewrites = rewrites.Value,
                                windowStart = command.WindowStart,
                                windowEnd = command.WindowEnd,
                            }, JsonOptions),
                            CreatedAt = command.CompletedAt,
                        }, transactionCt);
                        await unitOfWork.SaveChangesAsync(transactionCt);

                        return new StripeReconciliationAutoGrantDto(
                            paymentIntentId,
                            credit.Id,
                            user.Id,
                            rewrites.Value,
                            credit.StripeSku);
                    },
                    IsolationLevel.Serializable,
                    ct);

                if (grant is not null)
                {
                    autoGrants.Add(grant);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                manualReview.Add(new StripeReconciliationManualReviewDto(paymentIntentId, "grant_write_failed"));
            }
        }

        return new AutoGrantPassResult(autoGrants, manualReview, skippedOverCap);
    }

    private async Task<IReadOnlyList<StripeSubscriptionDiscrepancyDto>> BuildSubscriptionMismatchesAsync(
        CancellationToken ct)
    {
        var stripeSubscriptions = (await stripeClient.ListSubscriptionsAsync(ct))
            .Where(x => !string.IsNullOrWhiteSpace(x.SubscriptionId))
            .Select(x => x with
            {
                SubscriptionId = x.SubscriptionId.Trim(),
                CustomerId = Normalize(x.CustomerId),
                Status = Normalize(x.Status),
            })
            .ToList();
        var localUsers = await paymentGrants.ListSubscriptionUsersForReconciliationAsync(ct);
        var localBySubscriptionId = localUsers
            .Where(x => !string.IsNullOrWhiteSpace(x.StripeSubscriptionId))
            .GroupBy(x => x.StripeSubscriptionId!.Trim(), StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);
        var localByCustomerId = localUsers
            .Where(x => !string.IsNullOrWhiteSpace(x.StripeCustomerId))
            .GroupBy(x => x.StripeCustomerId!.Trim(), StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);
        var stripeBySubscriptionId = stripeSubscriptions
            .GroupBy(x => x.SubscriptionId, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);
        var mismatches = new List<StripeSubscriptionDiscrepancyDto>();

        foreach (var stripeSubscription in stripeSubscriptions
            .Where(x => IsActiveSubscriptionStatus(MapStripeSubscriptionStatus(x.Status)))
            .OrderBy(x => x.SubscriptionId, StringComparer.Ordinal))
        {
            var localUser = localBySubscriptionId.GetValueOrDefault(stripeSubscription.SubscriptionId);
            if (localUser is null && stripeSubscription.CustomerId is not null)
            {
                localUser = localByCustomerId.GetValueOrDefault(stripeSubscription.CustomerId);
            }

            if (localUser is null || !IsActiveSubscriptionStatus(localUser.Status))
            {
                mismatches.Add(new StripeSubscriptionDiscrepancyDto(
                    "stripe_active_local_not",
                    stripeSubscription.SubscriptionId,
                    stripeSubscription.CustomerId,
                    localUser?.UserId,
                    stripeSubscription.Status,
                    localUser?.Status.ToString() ?? "Missing"));
            }
        }

        foreach (var localUser in localUsers
            .Where(x => IsActiveSubscriptionStatus(x.Status))
            .OrderBy(x => x.UserId))
        {
            var subscriptionId = Normalize(localUser.StripeSubscriptionId);
            StripeSubscriptionSnapshotDto? stripeSubscription = null;
            var hasStripeSubscription = subscriptionId is not null &&
                stripeBySubscriptionId.TryGetValue(subscriptionId, out stripeSubscription);
            if (!hasStripeSubscription ||
                !IsActiveSubscriptionStatus(MapStripeSubscriptionStatus(stripeSubscription?.Status)))
            {
                mismatches.Add(new StripeSubscriptionDiscrepancyDto(
                    "local_active_stripe_not",
                    subscriptionId,
                    Normalize(localUser.StripeCustomerId),
                    localUser.UserId,
                    stripeSubscription?.Status,
                    localUser.Status.ToString()));
            }
        }

        return mismatches;
    }

    private async Task PersistRunAndMaybeAlertAsync(
        ReconcileStripeCommand command,
        StripeReconciliationReportDto report,
        CancellationToken ct)
    {
        var payloadJson = JsonSerializer.Serialize(TruncateForPersistence(report), JsonOptions);

        await unitOfWork.ExecuteInTransactionAsync(
            async transactionCt =>
            {
                var run = new StripeReconciliationRun
                {
                    WindowStart = command.WindowStart,
                    WindowEnd = command.WindowEnd,
                    StartedAt = command.CompletedAt,
                    CompletedAt = command.CompletedAt,
                    StripePaymentCount = report.StripePaymentCount,
                    PurchaseGrantCount = report.PurchaseGrantCount,
                    PaidButNoGrantCount = report.PaidButNoGrantCount,
                    GrantButNoPaymentCount = report.GrantButNoPaymentCount,
                    AmountMismatchCount = report.AmountMismatchCount,
                    SubscriptionMismatchCount = report.SubscriptionMismatchCount,
                    AutoGrantedCount = report.AutoGrantedCount,
                    AutoGrantSkippedCount = report.AutoGrantSkippedCount,
                    ManualReviewCount = report.ManualReviewCount,
                    ReportJson = payloadJson,
                };
                await runs.AddAsync(run, transactionCt);

                if (report.DiscrepancyCount +
                    report.AutoGrantedCount +
                    report.SubscriptionMismatchCount +
                    report.ManualReviewCount > 0)
                {
                    await outboxMessages.AddAsync(new OutboxMessage
                    {
                        MessageType = AlertMessageType,
                        PayloadJson = payloadJson,
                        Status = OutboxMessageStatus.Pending,
                        CreatedAt = command.CompletedAt,
                        NextAttemptAt = command.CompletedAt,
                        MaxAttempts = 10,
                        CorrelationId = run.Id.ToString(),
                    }, transactionCt);
                }

                await unitOfWork.SaveChangesAsync(transactionCt);
            },
            IsolationLevel.Serializable,
            ct);
    }

    private static IReadOnlyList<StripePaidPaymentDto> NormalizePayments(
        IReadOnlyList<StripePaidPaymentDto> stripePayments) =>
        stripePayments
            .Where(x => !string.IsNullOrWhiteSpace(x.PaymentIntentId))
            .Select(x => x with
            {
                PaymentIntentId = x.PaymentIntentId.Trim(),
                Currency = NormalizeCurrency(x.Currency) ?? string.Empty,
            })
            .GroupBy(x => x.PaymentIntentId, StringComparer.Ordinal)
            .Select(x => x.OrderByDescending(row => row.PaidAt).First())
            .ToList();

    private static StripeReconciliationReportDto BuildReport(
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        DateTimeOffset completedAt,
        IReadOnlyList<StripePaidPaymentDto> stripePayments,
        IReadOnlyList<PaymentGrantSnapshot> grants)
    {
        var paymentByIntent = stripePayments.ToDictionary(x => x.PaymentIntentId, StringComparer.Ordinal);
        var grantsByIntent = grants
            .Where(x => !string.IsNullOrWhiteSpace(x.PaymentIntentId))
            .GroupBy(x => x.PaymentIntentId!.Trim(), StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.Ordinal);
        var discrepancies = new List<StripeReconciliationDiscrepancyDto>();

        foreach (var payment in stripePayments.OrderBy(x => x.PaidAt).ThenBy(x => x.PaymentIntentId))
        {
            if (!grantsByIntent.TryGetValue(payment.PaymentIntentId, out var matchingGrants) ||
                matchingGrants.Count == 0)
            {
                discrepancies.Add(new StripeReconciliationDiscrepancyDto(
                    StripeReconciliationDiscrepancyKindDto.PaidButNoGrant,
                    payment.PaymentIntentId,
                    CreditId: null,
                    StripeAmount: payment.AmountReceived,
                    LedgerAmount: null,
                    StripeCurrency: payment.Currency,
                    LedgerCurrency: null,
                    StripePaidAt: payment.PaidAt,
                    LedgerGrantedAt: null));
                continue;
            }

            var grant = matchingGrants.OrderBy(x => x.GrantedAt).First();
            if (grant.AmountTotal != payment.AmountReceived ||
                !string.Equals(
                    NormalizeCurrency(grant.Currency),
                    NormalizeCurrency(payment.Currency),
                    StringComparison.Ordinal))
            {
                discrepancies.Add(new StripeReconciliationDiscrepancyDto(
                    StripeReconciliationDiscrepancyKindDto.AmountMismatch,
                    payment.PaymentIntentId,
                    grant.CreditId,
                    payment.AmountReceived,
                    grant.AmountTotal,
                    payment.Currency,
                    grant.Currency,
                    payment.PaidAt,
                    grant.GrantedAt));
            }
        }

        foreach (var grant in grants
            .Where(x => x.GrantedAt >= windowStart && x.GrantedAt < windowEnd)
            .OrderBy(x => x.GrantedAt)
            .ThenBy(x => x.CreditId))
        {
            var paymentIntentId = grant.PaymentIntentId?.Trim();
            if (!string.IsNullOrWhiteSpace(paymentIntentId) &&
                paymentByIntent.ContainsKey(paymentIntentId))
            {
                continue;
            }

            discrepancies.Add(new StripeReconciliationDiscrepancyDto(
                StripeReconciliationDiscrepancyKindDto.GrantButNoPayment,
                string.IsNullOrWhiteSpace(paymentIntentId) ? null : paymentIntentId,
                grant.CreditId,
                StripeAmount: null,
                LedgerAmount: grant.AmountTotal,
                StripeCurrency: null,
                LedgerCurrency: grant.Currency,
                StripePaidAt: null,
                LedgerGrantedAt: grant.GrantedAt));
        }

        return StripeReconciliationReportDto.Create(
            windowStart,
            windowEnd,
            completedAt,
            stripePayments.Count,
            grants.Count,
            discrepancies);
    }

    private static string? NormalizeCurrency(string? currency)
    {
        var normalized = currency?.Trim();
        return string.IsNullOrWhiteSpace(normalized)
            ? null
            : normalized.ToLowerInvariant();
    }

    private static string? Normalize(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized)
            ? null
            : normalized;
    }

    private static string CreateSyntheticEventId(string paymentIntentId) =>
        $"reconciliation:{paymentIntentId}";

    private static int? ResolveGrantedRewrites(StripeCheckoutSessionSnapshotDto session)
    {
        if (session.GrantedRewrites is > 0)
        {
            return session.GrantedRewrites;
        }

        return Normalize(session.Sku) switch
        {
            "quick_pack" => 10,
            "value_pack" => 30,
            _ => null,
        };
    }

    private static SubscriptionStatus MapStripeSubscriptionStatus(string? status)
    {
        return status?.Trim().ToLowerInvariant() switch
        {
            "active" => SubscriptionStatus.Active,
            "trialing" => SubscriptionStatus.Trialing,
            "past_due" => SubscriptionStatus.PastDue,
            "canceled" => SubscriptionStatus.Inactive,
            "unpaid" or
            "incomplete" or
            "incomplete_expired" or
            "paused" => SubscriptionStatus.Inactive,
            _ => SubscriptionStatus.Inactive,
        };
    }

    private static bool IsActiveSubscriptionStatus(SubscriptionStatus status) =>
        status is SubscriptionStatus.Active or SubscriptionStatus.Trialing or SubscriptionStatus.PastDue;

    private static StripeReconciliationReportDto TruncateForPersistence(
        StripeReconciliationReportDto report) =>
        report with
        {
            Discrepancies = report.Discrepancies.Take(MaxPersistedRowsPerList).ToList(),
            AutoGrants = report.AutoGrants.Take(MaxPersistedRowsPerList).ToList(),
            ManualReview = report.ManualReview.Take(MaxPersistedRowsPerList).ToList(),
            SubscriptionMismatches = report.SubscriptionMismatches.Take(MaxPersistedRowsPerList).ToList(),
        };

    private sealed record AutoGrantPassResult(
        IReadOnlyList<StripeReconciliationAutoGrantDto> AutoGrants,
        IReadOnlyList<StripeReconciliationManualReviewDto> ManualReview,
        int AutoGrantSkippedCount);
}
