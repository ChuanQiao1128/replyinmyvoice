using System.Data;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Domain.Entities;

namespace ReplyInMyVoice.Application.UseCases.StripeEvent;

public sealed class ProcessPaymentGraceRemindersHandler(
    IAppUserRepository appUsers,
    IStripeEventNotifier notifier,
    IUnitOfWork unitOfWork)
{
    private const int PaymentGraceReminderElapsedDays = 5;
    private const int PaymentGraceReminderRemainingDays = 2;

    public async Task<int> HandleAsync(
        ProcessPaymentGraceRemindersCommand command,
        CancellationToken ct = default)
    {
        if (command.BatchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(command.BatchSize), "Batch size must be greater than zero.");
        }

        var processedCount = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var postCommitActions = new List<Func<CancellationToken, Task>>();

            var batchCount = await unitOfWork.ExecuteInTransactionAsync(
                async transactionCt =>
                {
                    var candidates = await appUsers.ListPaymentGraceReminderCandidatesBatchAsync(
                        command.Now,
                        command.BatchSize,
                        transactionCt);
                    var reminderUsers = candidates
                        .Where(x => ShouldSendPaymentGraceReminder(x, command.Now))
                        .ToList();

                    foreach (var user in reminderUsers)
                    {
                        user.PaymentGraceReminderSentAt = command.Now;
                        user.UpdatedAt = command.Now;
                        user.RowVersion = Guid.NewGuid();
                        postCommitActions.Add(actionCt =>
                            notifier.EnqueuePaymentGraceReminderNotificationAsync(user, actionCt));
                    }

                    await unitOfWork.SaveChangesAsync(transactionCt);
                    return reminderUsers.Count;
                },
                IsolationLevel.Serializable,
                ct);

            if (batchCount > 0)
            {
                await RunPostCommitActionsAsync(postCommitActions, ct);
            }

            processedCount += batchCount;
            if (batchCount < command.BatchSize)
            {
                return processedCount;
            }
        }
    }

    private static bool ShouldSendPaymentGraceReminder(AppUser user, DateTimeOffset now)
    {
        if (user.PaymentGraceEndsAt is null || user.PaymentGraceEndsAt <= now)
        {
            return false;
        }

        var reminderAt = user.PaymentFailedAt is { } failedAt && failedAt < user.PaymentGraceEndsAt
            ? failedAt.AddDays(PaymentGraceReminderElapsedDays)
            : user.PaymentGraceEndsAt.Value.AddDays(-PaymentGraceReminderRemainingDays);

        return reminderAt <= now;
    }

    private static async Task RunPostCommitActionsAsync(
        IReadOnlyList<Func<CancellationToken, Task>> postCommitActions,
        CancellationToken ct)
    {
        foreach (var action in postCommitActions)
        {
            try
            {
                await action(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
            }
        }
    }
}
