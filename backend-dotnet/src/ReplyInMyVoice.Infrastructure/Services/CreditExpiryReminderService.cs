using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Notifications;

namespace ReplyInMyVoice.Infrastructure.Services;

public sealed class CreditExpiryReminderService(
    Func<AppDbContext> dbContextFactory,
    INotificationService notifications,
    IConfiguration configuration,
    ILogger<CreditExpiryReminderService> logger)
{
    public const int DefaultReminderWindowDays = 7;
    private const int BatchSize = 100;

    public async Task<int> RunOnceAsync(
        DateTimeOffset now,
        TimeSpan reminderWindow,
        CancellationToken cancellationToken = default)
    {
        if (reminderWindow <= TimeSpan.Zero)
        {
            return 0;
        }

        await using var db = dbContextFactory();
        var credits = await LoadCandidateCreditsAsync(
            db,
            now,
            now.Add(reminderWindow),
            cancellationToken);

        var sentCount = 0;
        foreach (var credit in credits)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var recipientEmail = credit.User?.Email;
            if (string.IsNullOrWhiteSpace(recipientEmail) || credit.ExpiresAt is null)
            {
                logger.LogWarning(
                    "Skipping credit expiry reminder for credit {CreditId} because recipient email or expiry is missing.",
                    credit.Id);
                continue;
            }

            var remaining = Math.Max(credit.AmountGranted - credit.AmountConsumed, 0);
            if (remaining <= 0)
            {
                continue;
            }

            var result = await notifications.SendAsync(
                NotificationTemplates.CreditExpiring,
                new NotificationRecipient(recipientEmail),
                new CreditExpiringNotificationModel(
                    CustomerName: string.Empty,
                    SupportEmail: ResolveSupportEmail(),
                    CreditsExpiring: remaining,
                    ExpiresOnUtc: credit.ExpiresAt.Value.ToUniversalTime()),
                cancellationToken);

            if (!result.Sent)
            {
                logger.LogWarning(
                    "Credit expiry reminder for credit {CreditId} was not sent. Provider={Provider}. Reason={Reason}.",
                    credit.Id,
                    result.Provider,
                    result.Reason);
                continue;
            }

            credit.ExpiryReminderSentAt = now;
            credit.RowVersion = Guid.NewGuid();
            await db.SaveChangesAsync(cancellationToken);
            sentCount++;
        }

        return sentCount;
    }

    private async Task<List<RewriteCredit>> LoadCandidateCreditsAsync(
        AppDbContext db,
        DateTimeOffset now,
        DateTimeOffset windowEnd,
        CancellationToken cancellationToken)
    {
        var query = db.RewriteCredits
            .AsTracking()
            .Include(x => x.User)
            .Where(x =>
                x.ExpiryReminderSentAt == null &&
                x.ExpiresAt != null &&
                x.AmountGranted > x.AmountConsumed);

        if (db.Database.IsSqlite())
        {
            var candidates = await query.ToListAsync(cancellationToken);
            return candidates
                .Where(x => x.ExpiresAt > now && x.ExpiresAt <= windowEnd)
                .OrderBy(x => x.ExpiresAt)
                .ThenBy(x => x.Id)
                .Take(BatchSize)
                .ToList();
        }

        return await query
            .Where(x => x.ExpiresAt > now && x.ExpiresAt <= windowEnd)
            .OrderBy(x => x.ExpiresAt)
            .ThenBy(x => x.Id)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);
    }

    private string ResolveSupportEmail() =>
        FirstConfiguredValue(
            "NOTIFICATIONS_REPLY_TO_EMAIL",
            "NOTIFICATIONS_SUPPORT_EMAIL",
            "SUPPORT_EMAIL") ?? "info@timeawake.co.nz";

    private string? FirstConfiguredValue(params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = configuration[key];
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }
}
