namespace ReplyInMyVoice.Domain.Enums;

public enum OutboxMessageStatus
{
    Pending = 0,
    Processing = 1,
    Sent = 2,
    Failed = 3,
}
