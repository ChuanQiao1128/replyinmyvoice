using Microsoft.AspNetCore.Mvc;

namespace ReplyInMyVoice.Functions.Http;

public static class FunctionHttpResults
{
    public static IActionResult Problem(string title, string? detail, int statusCode)
    {
        return new ObjectResult(new ProblemDetails
        {
            Title = title,
            Detail = detail,
            Status = statusCode,
        })
        {
            StatusCode = statusCode,
        };
    }

    public static IActionResult Accepted(string location, object body) =>
        new AcceptedResult(location, body);
}
