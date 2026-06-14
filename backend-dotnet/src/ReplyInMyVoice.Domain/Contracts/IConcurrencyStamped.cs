namespace ReplyInMyVoice.Domain.Contracts;

public interface IConcurrencyStamped
{
    Guid RowVersion { get; set; }
}
