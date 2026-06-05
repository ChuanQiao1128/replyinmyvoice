using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Functions.Functions;

public sealed class PaymentGraceExpiryFunction(
    StripeEventService stripeEvents,
    ILogger<PaymentGraceExpiryFunction> logger)
{
    [Function("ExpirePaymentGrace")]
    public async Task Run(
        [TimerTrigger("0 0 14 * * *")] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var reminderCount = await stripeEvents.ProcessPaymentGraceRemindersAsync(
            now,
            cancellationToken);
        var count = await stripeEvents.ProcessExpiredPaymentGraceAsync(
            now,
            cancellationToken);

        if (reminderCount > 0)
        {
            logger.LogInformation("Sent {Count} payment grace reminder notification(s).", reminderCount);
        }

        if (count > 0)
        {
            logger.LogInformation("Downgraded {Count} expired payment grace account(s).", count);
        }
    }
}
