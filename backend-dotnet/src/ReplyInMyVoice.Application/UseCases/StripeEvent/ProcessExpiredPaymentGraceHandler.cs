using System.Data;
using ReplyInMyVoice.Application.Abstractions;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;

namespace ReplyInMyVoice.Application.UseCases.StripeEvent;

public sealed class ProcessExpiredPaymentGraceHandler(
    IAppUserRepository appUsers,
    IOutboxMessageRepository outboxMessages,
    IStripeSubscriptionCancellationService cancellationService,
    IUnitOfWork unitOfWork)
{
    public async Task<int> HandleAsync(
        ProcessExpiredPaymentGraceCommand command,
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
                    var expiredUsers = await appUsers.ListExpiredPaymentGraceBatchAsync(
                        command.Now,
                        command.BatchSize,
                        transactionCt);

                    foreach (var user in expiredUsers)
                    {
                        EnqueueCancelStripeSubscription(postCommitActions, user);
                        user.SubscriptionStatus = SubscriptionStatus.Inactive;
                        ClearPaymentGrace(user);
                        user.UpdatedAt = command.Now;
                        user.RowVersion = Guid.NewGuid();
                        await outboxMessages.AddAsync(
                            StripeNotificationOutboxMessageFactory.Create(
                                StripeNotificationOutboxMessageTypes.SubscriptionPaused,
                                user.Id,
                                command.Now,
                                user.Id.ToString()),
                            transactionCt);
                    }

                    await unitOfWork.SaveChangesAsync(transactionCt);
                    return expiredUsers.Count;
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

    private void EnqueueCancelStripeSubscription(
        List<Func<CancellationToken, Task>> postCommitActions,
        AppUser user)
    {
        if (string.IsNullOrWhiteSpace(user.StripeSubscriptionId))
        {
            return;
        }

        var stripeSubscriptionId = user.StripeSubscriptionId.Trim();
        postCommitActions.Add(actionCt =>
            cancellationService.CancelSubscriptionAsync(stripeSubscriptionId, actionCt));
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

    private static void ClearPaymentGrace(AppUser user)
    {
        user.PaymentFailedAt = null;
        user.PaymentGraceEndsAt = null;
        user.PaymentGraceReminderSentAt = null;
    }
}
