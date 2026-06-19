namespace ReplyInMyVoice.Infrastructure.Notifications;

public sealed class RetryableNotificationException : Exception
{
    public RetryableNotificationException(string message)
        : base(message)
    {
    }

    public RetryableNotificationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
