namespace ReplyInMyVoice.Application.UseCases.CreditExpiry;

public sealed record SendCreditExpiryRemindersCommand(
    DateTimeOffset Now,
    TimeSpan ReminderWindow);
