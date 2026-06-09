namespace ReplyInMyVoice.Application.Common;

public sealed record BillingSupportRequestResultDto(
    BillingSupportRequestResultKind Kind,
    BillingSupportRequestResponseDto? Response,
    string? Detail)
{
    public static BillingSupportRequestResultDto Success(BillingSupportRequestResponseDto response) =>
        new(BillingSupportRequestResultKind.Success, response, null);

    public static BillingSupportRequestResultDto InvalidRequest(string detail) =>
        new(BillingSupportRequestResultKind.InvalidRequest, null, detail);
}

public enum BillingSupportRequestResultKind
{
    Success,
    InvalidRequest,
}
