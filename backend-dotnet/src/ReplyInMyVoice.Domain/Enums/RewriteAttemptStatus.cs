namespace ReplyInMyVoice.Domain.Enums;

public enum RewriteAttemptStatus
{
    Pending = 0,
    Processing = 1,
    Succeeded = 2,
    Failed = 3,
    Released = 4,
    Expired = 5,
}
