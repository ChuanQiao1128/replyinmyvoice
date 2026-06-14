using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

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

        var problemDetails = new ProblemDetails
        {
            Title = title,
            Detail = detail,
            Status = statusCode,
        };
        problemDetails.Extensions["code"] = DefaultErrorCode(statusCode);

        return new ObjectResult(problemDetails)
        {
            StatusCode = statusCode,
        };
    }

    public static IActionResult Accepted(string location, object body) =>
        new AcceptedResult(location, body);

    public static IActionResult PayloadTooLarge(long limitBytes) =>
        Problem(
            "Request body too large",
            $"Request body must be {limitBytes} bytes or smaller.",
            StatusCodes.Status413PayloadTooLarge,
            "payload_too_large");

    public static string DefaultErrorCode(int statusCode) =>
        statusCode switch
        {
            StatusCodes.Status400BadRequest => "invalid_request",
            StatusCodes.Status401Unauthorized => "unauthorized",
            StatusCodes.Status402PaymentRequired => "payment_required",
            StatusCodes.Status403Forbidden => "forbidden",
            StatusCodes.Status404NotFound => "not_found",
            StatusCodes.Status409Conflict => "conflict",
            StatusCodes.Status413PayloadTooLarge => "payload_too_large",
            StatusCodes.Status422UnprocessableEntity => "unprocessable",
            StatusCodes.Status429TooManyRequests => "rate_limited",
            StatusCodes.Status500InternalServerError => "internal_error",
            StatusCodes.Status503ServiceUnavailable => "service_unavailable",
            _ => "error",
        };
}
