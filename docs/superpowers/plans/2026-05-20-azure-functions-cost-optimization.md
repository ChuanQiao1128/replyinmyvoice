# Azure Functions Cost Optimization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Replace the costly Windows B1 Azure App Service dev backend with a low-cost Azure Functions backend while preserving the C#/.NET, Azure SQL, Service Bus, idempotent rewrite attempt, usage reservation, outbox, and worker processing architecture.

**Architecture:** Keep the main production product on Cloudflare + Neon + Clerk. Treat Azure as the C#/.NET reliability backend and demo environment. Delete the current Windows B1 App Service resources now to stop run-rate cost, then reintroduce the Azure backend as Azure Functions: HTTP triggers for API endpoints, Service Bus trigger for rewrite jobs, and Timer trigger for outbox/expiration cleanup.

**Tech Stack:** .NET 8, Azure Functions isolated worker, Azure SQL Database Basic, EF Core SQL Server, Azure Service Bus Basic, Azure Key Vault/app settings, Application Insights, GitHub Actions, xUnit.

---

## Current Cost Problem

Current Azure dev resources:

```text
Resource group: replyinmyvoice-dev-rg
Windows App Service Plan: replyinmyvoice-plan-dev, B1 Basic
Web App: replyinmyvoice-api-dev
Azure SQL Database: replyinmyvoice-db-dev, Basic
Service Bus Namespace: replyinmyvoice-sb-dev, Basic
Key Vault: replyinmyvoice-kv-dev
Application Insights: replyinmyvoice-ai-dev
```

The App Service Plan is the cost problem. Azure Portal currently forecasts a low partial-month bill because the resources were created mid-month, but a full-month Windows B1 run-rate is much higher than the forecast.

Immediate cost stop action:

```bash
az webapp delete \
  --resource-group replyinmyvoice-dev-rg \
  --name replyinmyvoice-api-dev

az appservice plan delete \
  --resource-group replyinmyvoice-dev-rg \
  --name replyinmyvoice-plan-dev \
  --yes
```

Do not delete:

```text
replyinmyvoice-db-dev
replyinmyvoice-sb-dev
replyinmyvoice-kv-dev
replyinmyvoice-ai-dev
```

Those resources are low-cost and preserve the reliability/backend demo foundation.

## Target Azure Runtime

Status on 2026-05-21:

```text
Implemented and deployed to https://replyinmyvoice-func-dev.azurewebsites.net.
Health smoke passed.
Rewrite smoke passed: Pending -> Processing -> Succeeded.
Windows B1 App Service resources are deleted.
```

Replace:

```text
ASP.NET Core Minimal API on App Service
Continuous WebJob Worker on App Service
```

With:

```text
Azure Functions isolated worker app
HTTP trigger: POST /api/rewrite
HTTP trigger: GET /api/rewrite-attempts/{attemptId}
HTTP trigger: POST /api/stripe/checkout
HTTP trigger: POST /api/stripe/portal
HTTP trigger: POST /api/stripe/webhook
Service Bus trigger: process rewrite job messages
Timer trigger: dispatch due outbox messages
Timer trigger: release expired usage reservations
```

The function app can run on Consumption or Flex Consumption. For the first cost-optimized version, prefer Consumption unless package/runtime constraints require Flex Consumption.

## File Structure

Create:

```text
backend-dotnet/src/ReplyInMyVoice.Functions/ReplyInMyVoice.Functions.csproj
backend-dotnet/src/ReplyInMyVoice.Functions/Program.cs
backend-dotnet/src/ReplyInMyVoice.Functions/Functions/RewriteHttpFunctions.cs
backend-dotnet/src/ReplyInMyVoice.Functions/Functions/BillingHttpFunctions.cs
backend-dotnet/src/ReplyInMyVoice.Functions/Functions/StripeWebhookFunction.cs
backend-dotnet/src/ReplyInMyVoice.Functions/Functions/RewriteJobFunction.cs
backend-dotnet/src/ReplyInMyVoice.Functions/Functions/OutboxDispatcherTimerFunction.cs
backend-dotnet/src/ReplyInMyVoice.Functions/Functions/ExpiredReservationCleanupTimerFunction.cs
backend-dotnet/src/ReplyInMyVoice.Functions/Http/FunctionHttpResults.cs
backend-dotnet/src/ReplyInMyVoice.Functions/Auth/FunctionAuthResolver.cs
backend-dotnet/src/ReplyInMyVoice.Functions/local.settings.example.json
infra/azure/functions-provision.sh
infra/azure/functions-deploy.sh
```

Modify:

```text
backend-dotnet/ReplyInMyVoice.sln
backend-dotnet/src/ReplyInMyVoice.Infrastructure/ServiceCollectionExtensions.cs
backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/RewriteRequestService.cs
backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/StripeBillingService.cs
backend-dotnet/tests/ReplyInMyVoice.Tests/*
.github/workflows/dotnet-azure.yml
docs/manual-setup.md
docs/dotnet-azure-full-run-result.md
```

Delete or stop using:

```text
App Service deployment package with App_Data/jobs/continuous/RewriteWorker
Windows WebJob run.cmd packaging path
```

## Task 1: Stop The Windows App Service Cost

Status on 2026-05-20:

```text
Completed. `az webapp delete` removed replyinmyvoice-api-dev and also removed the empty replyinmyvoice-plan-dev App Service Plan.

Verified remaining resources:
- replyinmyvoice-db-dev remains.
- replyinmyvoice-sb-dev remains.
- replyinmyvoice-kv-dev remains.
- replyinmyvoice-ai-dev remains.
```

- [x] **Step 1: Confirm the Windows App Service Plan contains only the demo app**

Run:

```bash
az appservice plan list \
  --resource-group replyinmyvoice-dev-rg \
  --query '[].{name:name,sku:sku.name,tier:sku.tier,apps:numberOfSites,location:location}' \
  -o table

az webapp list \
  --resource-group replyinmyvoice-dev-rg \
  --query '[].{name:name,state:state,host:defaultHostName}' \
  -o table
```

Expected:

```text
replyinmyvoice-plan-dev has 1 app
replyinmyvoice-api-dev is the only Web App in replyinmyvoice-dev-rg
```

- [x] **Step 2: Delete the Web App**

Run:

```bash
az webapp delete \
  --resource-group replyinmyvoice-dev-rg \
  --name replyinmyvoice-api-dev
```

Expected:

```text
Command exits successfully.
```

- [x] **Step 3: Delete the empty App Service Plan**

Run:

```bash
az appservice plan delete \
  --resource-group replyinmyvoice-dev-rg \
  --name replyinmyvoice-plan-dev \
  --yes
```

Expected:

```text
Command exits successfully.
```

- [x] **Step 4: Verify the low-cost Azure resources remain**

Run:

```bash
az resource list \
  --resource-group replyinmyvoice-dev-rg \
  --query '[].{name:name,type:type,sku:sku.name}' \
  -o table
```

Expected:

```text
replyinmyvoice-db-dev remains.
replyinmyvoice-sb-dev remains.
replyinmyvoice-kv-dev remains.
replyinmyvoice-ai-dev remains.
replyinmyvoice-api-dev is absent.
replyinmyvoice-plan-dev is absent.
```

## Task 2: Add Azure Functions Project

- [x] **Step 1: Create the Functions project**

Run:

```bash
cd /Users/qc/Desktop/CloudFlare/backend-dotnet
func init src/ReplyInMyVoice.Functions --worker-runtime dotnet-isolated --target-framework net8.0
dotnet sln ReplyInMyVoice.sln add src/ReplyInMyVoice.Functions/ReplyInMyVoice.Functions.csproj
```

Expected:

```text
The solution contains ReplyInMyVoice.Functions.
```

- [x] **Step 2: Add project references and packages**

Update `backend-dotnet/src/ReplyInMyVoice.Functions/ReplyInMyVoice.Functions.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <AzureFunctionsVersion>v4</AzureFunctionsVersion>
    <OutputType>Exe</OutputType>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker" Version="1.*" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore" Version="1.*" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker.Extensions.ServiceBus" Version="5.*" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker.Extensions.Timer" Version="4.*" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker.Sdk" Version="1.*" OutputItemType="Analyzer" />
    <PackageReference Include="Microsoft.ApplicationInsights.WorkerService" Version="2.23.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../ReplyInMyVoice.Domain/ReplyInMyVoice.Domain.csproj" />
    <ProjectReference Include="../ReplyInMyVoice.Infrastructure/ReplyInMyVoice.Infrastructure.csproj" />
  </ItemGroup>
</Project>
```

- [x] **Step 3: Add dependency injection startup**

Create `backend-dotnet/src/ReplyInMyVoice.Functions/Program.cs`:

```csharp
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReplyInMyVoice.Infrastructure;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        services.AddReplyInMyVoiceInfrastructure(context.Configuration);
    })
    .Build();

host.Run();
```

- [x] **Step 4: Build the solution**

Run:

```bash
cd /Users/qc/Desktop/CloudFlare/backend-dotnet
dotnet build ReplyInMyVoice.sln
```

Expected:

```text
Build succeeded.
```

## Task 3: Move Rewrite HTTP Endpoints To Functions

- [x] **Step 1: Create auth resolver for Functions**

Create `backend-dotnet/src/ReplyInMyVoice.Functions/Auth/FunctionAuthResolver.cs`:

```csharp
using Microsoft.AspNetCore.Http;

namespace ReplyInMyVoice.Functions.Auth;

public static class FunctionAuthResolver
{
    public static string? ResolveExternalUserId(HttpRequest request, IConfiguration configuration)
    {
        var allowHeaderAuth = string.Equals(
            configuration["ALLOW_HEADER_AUTH"],
            "true",
            StringComparison.OrdinalIgnoreCase);

        if (allowHeaderAuth &&
            request.Headers.TryGetValue("X-Test-User-Id", out var testUserId) &&
            !string.IsNullOrWhiteSpace(testUserId))
        {
            return testUserId.ToString();
        }

        var subject = request.HttpContext.User.FindFirst("sub")?.Value;
        return string.IsNullOrWhiteSpace(subject) ? null : subject;
    }
}
```

- [x] **Step 2: Create HTTP result helpers**

Create `backend-dotnet/src/ReplyInMyVoice.Functions/Http/FunctionHttpResults.cs`:

```csharp
using Microsoft.AspNetCore.Http;
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

    public static IActionResult Accepted(string location, object body)
    {
        return new AcceptedResult(location, body);
    }
}
```

- [x] **Step 3: Create rewrite functions**

Create `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/RewriteHttpFunctions.cs` with two functions:

```csharp
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Domain.Entities;
using ReplyInMyVoice.Domain.Enums;
using ReplyInMyVoice.Functions.Auth;
using ReplyInMyVoice.Functions.Http;
using ReplyInMyVoice.Infrastructure.Data;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Functions.Functions;

public sealed class RewriteHttpFunctions(
    IConfiguration configuration,
    AppDbContext db,
    RewriteRequestService rewriteRequestService)
{
    [Function("CreateRewriteAttempt")]
    public async Task<IActionResult> CreateRewriteAttempt(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "rewrite")] HttpRequest request,
        CancellationToken cancellationToken)
    {
        var externalUserId = FunctionAuthResolver.ResolveExternalUserId(request, configuration);
        if (string.IsNullOrWhiteSpace(externalUserId))
        {
            return FunctionHttpResults.Problem("Authentication required", "A valid authenticated user is required.", StatusCodes.Status401Unauthorized);
        }

        var idempotencyKey = request.Headers["X-Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return FunctionHttpResults.Problem("Missing idempotency key", "X-Idempotency-Key is required for rewrite requests.", StatusCodes.Status400BadRequest);
        }

        RewriteRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<RewriteRequest>(
                request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);
        }
        catch (JsonException)
        {
            return FunctionHttpResults.Problem("Invalid rewrite request", "Request body must be valid JSON.", StatusCodes.Status400BadRequest);
        }

        if (body is null)
        {
            return FunctionHttpResults.Problem("Invalid rewrite request", "Request body is required.", StatusCodes.Status400BadRequest);
        }

        var user = await GetOrCreateUserAsync(externalUserId, cancellationToken);
        var plan = GetUsagePlan(user);
        var result = await rewriteRequestService.CreateAttemptAsync(
            user.Id,
            idempotencyKey,
            body,
            plan.PeriodKey,
            plan.QuotaLimit,
            DateTimeOffset.UtcNow,
            cancellationToken);

        if (result.Kind == ReserveRewriteResultKind.QuotaExceeded)
        {
            return FunctionHttpResults.Problem("Rewrite quota exhausted", "No rewrite quota remains for the current period.", StatusCodes.Status402PaymentRequired);
        }

        if (result.Kind == ReserveRewriteResultKind.Conflict)
        {
            return FunctionHttpResults.Problem("Idempotency key conflict", "The same idempotency key was reused with a different rewrite request.", StatusCodes.Status409Conflict);
        }

        var response = new RewriteAttemptResponse(result.AttemptId, result.Status.ToString(), result.ResultJson, result.ErrorCode);
        return result.Status == RewriteAttemptStatus.Succeeded
            ? new OkObjectResult(response)
            : FunctionHttpResults.Accepted($"/api/rewrite-attempts/{result.AttemptId}", response);
    }

    [Function("GetRewriteAttempt")]
    public async Task<IActionResult> GetRewriteAttempt(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "rewrite-attempts/{attemptId:guid}")] HttpRequest request,
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        var externalUserId = FunctionAuthResolver.ResolveExternalUserId(request, configuration);
        if (string.IsNullOrWhiteSpace(externalUserId))
        {
            return FunctionHttpResults.Problem("Authentication required", null, StatusCodes.Status401Unauthorized);
        }

        var user = await db.AppUsers.SingleOrDefaultAsync(x => x.ExternalAuthUserId == externalUserId, cancellationToken);
        if (user is null)
        {
            return new NotFoundResult();
        }

        var attempt = await db.RewriteAttempts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == attemptId && x.UserId == user.Id, cancellationToken);
        if (attempt is null)
        {
            return new NotFoundResult();
        }

        return new OkObjectResult(new RewriteAttemptResponse(attempt.Id, attempt.Status.ToString(), attempt.ResultJson, attempt.ErrorCode));
    }

    private async Task<AppUser> GetOrCreateUserAsync(string externalUserId, CancellationToken cancellationToken)
    {
        var user = await db.AppUsers.SingleOrDefaultAsync(x => x.ExternalAuthUserId == externalUserId, cancellationToken);
        if (user is not null)
        {
            return user;
        }

        user = new AppUser
        {
            Id = Guid.NewGuid(),
            ExternalAuthUserId = externalUserId,
            SubscriptionStatus = SubscriptionStatus.Inactive,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        db.AppUsers.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        return user;
    }

    private static (string PeriodKey, int QuotaLimit) GetUsagePlan(AppUser user)
    {
        var active = user.SubscriptionStatus is SubscriptionStatus.Active or SubscriptionStatus.Trialing;
        return active
            ? (DateTimeOffset.UtcNow.ToString("yyyy-MM"), 40)
            : ("free-lifetime", 3);
    }
}
```

- [x] **Step 4: Build and fix namespace/package issues**

Run:

```bash
cd /Users/qc/Desktop/CloudFlare/backend-dotnet
dotnet build ReplyInMyVoice.sln
```

Expected:

```text
Build succeeded.
```

## Task 4: Move Worker Behavior To Function Triggers

- [x] **Step 1: Add Service Bus rewrite job trigger**

Create `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/RewriteJobFunction.cs`:

```csharp
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using ReplyInMyVoice.Domain.Contracts;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Functions.Functions;

public sealed class RewriteJobFunction(
    RewriteJobProcessor processor,
    ILogger<RewriteJobFunction> logger)
{
    [Function("ProcessRewriteJob")]
    public async Task Run(
        [ServiceBusTrigger("%SERVICEBUS_QUEUE_NAME%", Connection = "ServiceBus")] string messageBody,
        CancellationToken cancellationToken)
    {
        RewriteJob? job;
        try
        {
            job = JsonSerializer.Deserialize<RewriteJob>(messageBody);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Service Bus message was not valid RewriteJob JSON.");
            throw;
        }

        if (job is null || job.AttemptId == Guid.Empty)
        {
            throw new InvalidOperationException("Service Bus message did not contain a valid attempt id.");
        }

        await processor.ProcessAsync(job, cancellationToken);
    }
}
```

- [x] **Step 2: Add outbox dispatcher timer**

Create `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/OutboxDispatcherTimerFunction.cs`:

```csharp
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Functions.Functions;

public sealed class OutboxDispatcherTimerFunction(
    OutboxDispatcherService dispatcher,
    ILogger<OutboxDispatcherTimerFunction> logger)
{
    [Function("DispatchOutboxMessages")]
    public async Task Run([TimerTrigger("*/15 * * * * *")] TimerInfo timer, CancellationToken cancellationToken)
    {
        var lockedBy = Environment.MachineName;
        var count = await dispatcher.DispatchDueAsync(DateTimeOffset.UtcNow, lockedBy, 10, cancellationToken);
        if (count > 0)
        {
            logger.LogInformation("Dispatched {Count} outbox messages.", count);
        }
    }
}
```

- [x] **Step 3: Add expired reservation cleanup timer**

Create `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/ExpiredReservationCleanupTimerFunction.cs`:

```csharp
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using ReplyInMyVoice.Infrastructure.Services;

namespace ReplyInMyVoice.Functions.Functions;

public sealed class ExpiredReservationCleanupTimerFunction(
    ExpiredReservationCleanupService cleanup,
    ILogger<ExpiredReservationCleanupTimerFunction> logger)
{
    [Function("ReleaseExpiredReservations")]
    public async Task Run([TimerTrigger("0 */5 * * * *")] TimerInfo timer, CancellationToken cancellationToken)
    {
        var count = await cleanup.ReleaseExpiredReservationsAsync(DateTimeOffset.UtcNow, cancellationToken);
        if (count > 0)
        {
            logger.LogInformation("Released {Count} expired reservations.", count);
        }
    }
}
```

- [x] **Step 4: Run worker-related tests**

Run:

```bash
cd /Users/qc/Desktop/CloudFlare/backend-dotnet
dotnet test ReplyInMyVoice.sln --filter "FullyQualifiedName~Outbox|FullyQualifiedName~RewriteJob|FullyQualifiedName~Reservation"
```

Expected:

```text
All matching tests pass.
```

## Task 5: Add Azure Functions Provision And Deploy Scripts

- [x] **Step 1: Add provisioning script**

Create `infra/azure/functions-provision.sh`:

```bash
#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/../.."
source infra/azure/read-env.sh

SUBSCRIPTION_ID="$(read_env AZURE_SUBSCRIPTION_ID)"
LOCATION="$(read_env AZURE_LOCATION australiaeast)"
RESOURCE_GROUP="$(read_env AZURE_RESOURCE_GROUP replyinmyvoice-dev-rg)"
FUNCTION_APP_NAME="$(read_env AZURE_FUNCTION_APP_NAME replyinmyvoice-func-dev)"
STORAGE_ACCOUNT_NAME="$(read_env AZURE_FUNCTION_STORAGE_ACCOUNT replyinmyvoicefuncdev)"
SQL_SERVER="$(read_env AZURE_SQL_SERVER_NAME replyinmyvoice-sql-dev)"
SQL_DATABASE="$(read_env AZURE_SQL_DATABASE_NAME replyinmyvoice-db-dev)"
KEY_VAULT="$(read_env AZURE_KEY_VAULT_NAME replyinmyvoice-kv-dev)"
SERVICE_BUS_NAMESPACE="$(read_env AZURE_SERVICE_BUS_NAMESPACE replyinmyvoice-sb-dev)"
SERVICE_BUS_QUEUE="$(read_env AZURE_SERVICE_BUS_QUEUE rewrite-jobs)"

if [[ -z "$SUBSCRIPTION_ID" ]]; then
  echo "AZURE_SUBSCRIPTION_ID is required."
  exit 1
fi

arch -arm64 az account set --subscription "$SUBSCRIPTION_ID"

arch -arm64 az group create \
  --name "$RESOURCE_GROUP" \
  --location "$LOCATION" \
  --tags project=replyinmyvoice environment=dev \
  --output none

arch -arm64 az storage account create \
  --resource-group "$RESOURCE_GROUP" \
  --name "$STORAGE_ACCOUNT_NAME" \
  --location "$LOCATION" \
  --sku Standard_LRS \
  --kind StorageV2 \
  --output none

arch -arm64 az functionapp create \
  --resource-group "$RESOURCE_GROUP" \
  --name "$FUNCTION_APP_NAME" \
  --storage-account "$STORAGE_ACCOUNT_NAME" \
  --consumption-plan-location "$LOCATION" \
  --runtime dotnet-isolated \
  --runtime-version 8 \
  --functions-version 4 \
  --output none

SQL_ADMIN_USER="$(arch -arm64 az keyvault secret show --vault-name "$KEY_VAULT" --name "sql-admin-user" --query value -o tsv)"
SQL_ADMIN_PASSWORD="$(arch -arm64 az keyvault secret show --vault-name "$KEY_VAULT" --name "sql-admin-password" --query value -o tsv)"
SQL_CONNECTION="Server=tcp:${SQL_SERVER}.database.windows.net,1433;Initial Catalog=${SQL_DATABASE};Persist Security Info=False;User ID=${SQL_ADMIN_USER};Password=${SQL_ADMIN_PASSWORD};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

SERVICE_BUS_CONNECTION="$(arch -arm64 az servicebus namespace authorization-rule keys list \
  --resource-group "$RESOURCE_GROUP" \
  --namespace-name "$SERVICE_BUS_NAMESPACE" \
  --name RootManageSharedAccessKey \
  --query primaryConnectionString \
  -o tsv)"

arch -arm64 az functionapp config appsettings set \
  --resource-group "$RESOURCE_GROUP" \
  --name "$FUNCTION_APP_NAME" \
  --settings \
    "ConnectionStrings__DefaultConnection=$SQL_CONNECTION" \
    "ServiceBus=$SERVICE_BUS_CONNECTION" \
    "SERVICEBUS_QUEUE_NAME=$SERVICE_BUS_QUEUE" \
    "ALLOW_HEADER_AUTH=$(read_env ALLOW_HEADER_AUTH false)" \
    "OPENAI_MODEL=$(read_env OPENAI_MODEL gpt-4o-mini)" \
    "OPENAI_TIMEOUT_SEC=$(read_env OPENAI_TIMEOUT_SEC 25)" \
    "STRIPE_PRICE_ID=$(read_env STRIPE_PRICE_ID)" \
    "WRITING_SIGNAL_PROVIDER=$(read_env WRITING_SIGNAL_PROVIDER sapling)" \
    "WRITING_SIGNAL_TIMEOUT_SEC=$(read_env WRITING_SIGNAL_TIMEOUT_SEC 10)" \
  --output none

for secret_name in OPENAI_API_KEY SAPLING_API_KEY STRIPE_SECRET_KEY STRIPE_WEBHOOK_SECRET CLERK_SECRET_KEY NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY CLERK_JWT_ISSUER CLERK_JWT_AUDIENCE; do
  value="$(read_env "$secret_name")"
  if [[ -n "$value" ]]; then
    arch -arm64 az functionapp config appsettings set \
      --resource-group "$RESOURCE_GROUP" \
      --name "$FUNCTION_APP_NAME" \
      --settings "$secret_name=$value" \
      --output none
  fi
done

echo "Function App ready: https://${FUNCTION_APP_NAME}.azurewebsites.net"
```

- [x] **Step 2: Add deploy script**

Create `infra/azure/functions-deploy.sh`:

```bash
#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/../.."
source infra/azure/read-env.sh

RESOURCE_GROUP="$(read_env AZURE_RESOURCE_GROUP replyinmyvoice-dev-rg)"
FUNCTION_APP_NAME="$(read_env AZURE_FUNCTION_APP_NAME replyinmyvoice-func-dev)"

dotnet publish backend-dotnet/src/ReplyInMyVoice.Functions/ReplyInMyVoice.Functions.csproj \
  --configuration Release \
  --output backend-dotnet/artifacts/functions

cd backend-dotnet/artifacts/functions
zip -qr ../replyinmyvoice-functions.zip .
cd ../../..

arch -arm64 az functionapp deployment source config-zip \
  --resource-group "$RESOURCE_GROUP" \
  --name "$FUNCTION_APP_NAME" \
  --src backend-dotnet/artifacts/replyinmyvoice-functions.zip \
  --build-remote false \
  --output none

echo "Deployed Function App: https://${FUNCTION_APP_NAME}.azurewebsites.net"
```

- [x] **Step 3: Make scripts executable**

Run:

```bash
chmod +x infra/azure/functions-provision.sh infra/azure/functions-deploy.sh
```

Expected:

```text
Both scripts are executable.
```

## Task 6: Update GitHub Actions

Status on 2026-05-21:

```text
The workflow now deploys the Function App package. GitHub Actions must have AZURE_FUNCTION_APP_NAME=replyinmyvoice-func-dev configured as a repository variable before the next main-branch run.
```

- [x] **Step 1: Replace App Service deploy with Function App deploy**

Modify `.github/workflows/dotnet-azure.yml` deploy job to publish `ReplyInMyVoice.Functions.csproj` and deploy it to `AZURE_FUNCTION_APP_NAME`.

Use this deployment command:

```bash
az functionapp deployment source config-zip \
  --resource-group "${{ vars.AZURE_RESOURCE_GROUP }}" \
  --name "${{ vars.AZURE_FUNCTION_APP_NAME }}" \
  --src backend-dotnet/artifacts/replyinmyvoice-functions.zip
```

- [x] **Step 2: Keep EF Core migration step**

Keep the existing `dotnet ef database update` step because Azure SQL is still the source of truth for:

```text
RewriteAttempt
UsageReservation
UsagePeriod
OutboxMessage
StripeEvent
Subscription
```

- [x] **Step 3: Change smoke test URL**

Use:

```bash
curl --fail "https://${{ vars.AZURE_FUNCTION_APP_NAME }}.azurewebsites.net/api/health"
```

If a health function is not yet created, add `HealthFunction.cs`:

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace ReplyInMyVoice.Functions.Functions;

public sealed class HealthFunction
{
    [Function("Health")]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequest request)
    {
        return new OkObjectResult(new { ok = true, service = "replyinmyvoice-functions" });
    }
}
```

## Task 7: Verification

- [x] **Step 1: Build**

Run:

```bash
cd /Users/qc/Desktop/CloudFlare/backend-dotnet
dotnet build ReplyInMyVoice.sln
```

Expected:

```text
Build succeeded.
```

- [x] **Step 2: Test**

Run:

```bash
cd /Users/qc/Desktop/CloudFlare/backend-dotnet
dotnet test ReplyInMyVoice.sln
```

Expected:

```text
Passed!
```

- [x] **Step 3: Provision Function App**

Run:

```bash
cd /Users/qc/Desktop/CloudFlare
infra/azure/functions-provision.sh
```

Expected:

```text
Function App ready: https://replyinmyvoice-func-dev.azurewebsites.net
```

- [x] **Step 4: Deploy Function App**

Run:

```bash
cd /Users/qc/Desktop/CloudFlare
infra/azure/functions-deploy.sh
```

Expected:

```text
Deployed Function App: https://replyinmyvoice-func-dev.azurewebsites.net
```

- [x] **Step 5: Smoke test health**

Run:

```bash
curl --fail https://replyinmyvoice-func-dev.azurewebsites.net/api/health
```

Expected:

```json
{"ok":true,"service":"replyinmyvoice-functions"}
```

- [x] **Step 6: Smoke test rewrite flow**

Run a dev-auth smoke test only if `ALLOW_HEADER_AUTH=true` in the Function App settings:

```bash
curl -i \
  -X POST https://replyinmyvoice-func-dev.azurewebsites.net/api/rewrite \
  -H 'Content-Type: application/json' \
  -H 'X-Test-User-Id: azure-function-smoke-user' \
  -H "X-Idempotency-Key: smoke-$(date +%s)" \
  --data '{
    "roughDraftReply": "Hi Alex, thanks for the update. I will review the file today and send comments tomorrow.",
    "tone": "warm"
  }'
```

Expected:

```text
HTTP 202 Accepted
Response contains attemptId
```

- [x] **Step 7: Verify queue processing**

Query the returned attempt id:

```bash
curl --fail \
  -H 'X-Test-User-Id: azure-function-smoke-user' \
  https://replyinmyvoice-func-dev.azurewebsites.net/api/rewrite-attempts/<attempt-id>
```

Expected after the Service Bus trigger runs:

```text
Status is Succeeded or Failed.
If Failed, UsageReservation is Released and UsedCount is unchanged.
```

## Cost Acceptance

After migration:

```text
Windows App Service Plan B1: deleted
Azure Functions: Consumption/Flex low-usage, expected near NZD $0 initially
Azure SQL Basic: approximately NZD $9/month
Service Bus Basic: low usage near NZD $0
Key Vault: low usage near NZD $0
Application Insights: low log volume target, expected NZD $0-$5/month
```

Target monthly Azure dev cost:

```text
NZD $10-$15/month under low usage
```

If Function App cold start is too slow for demos:

```text
Option A: keep async 202 + polling and accept slower background processing.
Option B: move only Azure demo runtime to Linux B1.
Option C: use Functions Flex Consumption with Always Ready.
```

## Self-Review

Spec coverage:

```text
Stops Windows App Service B1 cost: covered by Task 1.
Keeps Azure SQL because reliability state requires it: covered by Target Azure Runtime and Task 6.
Moves worker from WebJob to Function triggers: covered by Task 4.
Keeps outbox/Service Bus design: covered by Task 4 and Task 7.
Updates CI/CD away from App Service: covered by Task 6.
Documents expected cost reduction: covered by Cost Acceptance.
```

Placeholder scan:

```text
No TODO/TBD placeholders are intentionally left in this plan.
```

Type consistency:

```text
Function names, file paths, and service names use the existing backend-dotnet conventions.
```
