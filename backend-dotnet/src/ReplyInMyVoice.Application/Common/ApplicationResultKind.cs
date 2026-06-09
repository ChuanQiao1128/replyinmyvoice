namespace ReplyInMyVoice.Application.Common;

public enum ApplicationResultKind
{
    Success = 0,
    Created = 1,
    Existing = 2,
    NotFound = 3,
    QuotaExceeded = 4,
    Conflict = 5,
}
