using ReplyInMyVoice.Application.Abstractions;

namespace ReplyInMyVoice.Application.UseCases.CreditExpiry;

public sealed class SendCreditExpiryRemindersHandler(
    IRewriteCreditRepository credits,
    ICreditExpiryNotifier notifier)
{
    private const int BatchSize = 100;

    public async Task<int> HandleAsync(
        SendCreditExpiryRemindersCommand command,
        CancellationToken ct = default)
    {
        if (command.ReminderWindow <= TimeSpan.Zero)
        {
            return 0;
        }

        var candidates = await credits.ListExpiryReminderCandidatesAsync(
            command.Now,
            command.Now.Add(command.ReminderWindow),
            BatchSize,
            ct);

        var sentCount = 0;
        foreach (var credit in candidates)
        {
            ct.ThrowIfCancellationRequested();

            var recipientEmail = credit.User?.Email;
            if (string.IsNullOrWhiteSpace(recipientEmail) || credit.ExpiresAt is null)
            {
                continue;
            }

            var remaining = Math.Max(credit.AmountGranted - credit.AmountConsumed, 0);
            if (remaining <= 0)
            {
                continue;
            }

            var claimed = await credits.TryClaimExpiryReminderAsync(credit.Id, command.Now, ct);
            if (!claimed)
            {
                continue;
            }

            var sent = await notifier.TrySendCreditExpiringAsync(
                new CreditExpiryNotificationRequest(
                    recipientEmail,
                    remaining,
                    credit.ExpiresAt.Value.ToUniversalTime()),
                ct);
            if (!sent)
            {
                await credits.ReleaseExpiryReminderClaimAsync(credit.Id, command.Now, ct);
                continue;
            }

            sentCount++;
        }

        return sentCount;
    }
}
