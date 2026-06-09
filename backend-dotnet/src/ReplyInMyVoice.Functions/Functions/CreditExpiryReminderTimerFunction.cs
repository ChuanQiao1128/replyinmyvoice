using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ReplyInMyVoice.Application.UseCases.CreditExpiry;

namespace ReplyInMyVoice.Functions.Functions;

public sealed class CreditExpiryReminderTimerFunction(
    SendCreditExpiryRemindersHandler reminders,
    IConfiguration configuration,
    ILogger<CreditExpiryReminderTimerFunction> logger)
{
    private const int DefaultReminderWindowDays = 7;
    private const string ReminderWindowDaysSetting = "CREDIT_EXPIRY_REMINDER_WINDOW_DAYS";

    [Function("SendCreditExpiryReminders")]
    public async Task Run(
        [TimerTrigger("0 0 9 * * *")] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        var count = await reminders.HandleAsync(
            new SendCreditExpiryRemindersCommand(
                DateTimeOffset.UtcNow,
                TimeSpan.FromDays(ResolveReminderWindowDays())),
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

        return DefaultReminderWindowDays;
    }
}
