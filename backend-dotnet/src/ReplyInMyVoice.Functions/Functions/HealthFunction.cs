using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Functions.Functions;

public sealed class HealthFunction(AppDbContext db)
{
    [Function("Health")]
    public IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")]
        HttpRequest request)
    {
        return new OkObjectResult(new { ok = true, service = "replyinmyvoice-functions" });
    }

    [Function("DatabaseHealth")]
    public async Task<IActionResult> DatabaseHealth(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health/db")]
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var canConnect = await db.Database.CanConnectAsync(cancellationToken);
        return canConnect
            ? new OkObjectResult(new { ok = true, database = "azure-sql" })
            : new ObjectResult(new { ok = false, database = "azure-sql" })
            {
                StatusCode = StatusCodes.Status500InternalServerError,
            };
    }
}
