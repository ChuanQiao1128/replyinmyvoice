using Microsoft.AspNetCore.Mvc;

namespace ReplyInMyVoice.Functions.Http;

public static class FunctionHttpResults
{
    public static IActionResult Problem(
        string title,
        string? detail,
        int statusCode,
        string? errorCode = null)
    {
        if (!string.IsNullOrWhiteSpace(errorCode))
        {
            return new ObjectResult(new
            {
                error = new
                {
                    code = errorCode,
                    message = detail ?? title,
                },
            })
            {
                StatusCode = statusCode,
            };
        }

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
