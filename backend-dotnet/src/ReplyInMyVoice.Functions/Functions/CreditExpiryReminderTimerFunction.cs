using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Functions.Functions;

public sealed class CreditExpiryReminderTimerFunction(
    CreditExpiryReminderService reminders,
    IConfiguration configuration,
    ILogger<CreditExpiryReminderTimerFunction> logger)
{
    private const string ReminderWindowDaysSetting = "CREDIT_EXPIRY_REMINDER_WINDOW_DAYS";

    [Function("SendCreditExpiryReminders")]
    public async Task Run(
        [TimerTrigger("0 0 9 * * *")] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        var count = await reminders.RunOnceAsync(
            DateTimeOffset.UtcNow,
            TimeSpan.FromDays(ResolveReminderWindowDays()),
            cancellationToken);

        if (count > 0)
        {
            logger.LogInformation("Sent {Count} credit expiry reminder notifications.", count);
        }
    }

    private int ResolveReminderWindowDays()
    {
        var rawValue = configuration[ReminderWindowDaysSetting];
        if (int.TryParse(rawValue, out var parsed) && parsed > 0)
        {
            return parsed;
        }

        return CreditExpiryReminderService.DefaultReminderWindowDays;
    }
}
