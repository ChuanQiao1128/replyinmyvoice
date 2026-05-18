# .NET Azure Blocker Preflight

Date: 2026-05-19

## Purpose

This document records the blocker preflight performed before the next long autonomous .NET/Azure run.

The goal was to check the only valid stop conditions from `docs/dotnet-azure-full-run-target.md` before the long run starts, so the autonomous run is less likely to stop while the user is away.

Do not store secret values in this document.

## Stop Conditions Checked

### Azure Permission Denied

Status: cleared for current dev resources.

Verified:

- Azure CLI is installed and authenticated.
- Subscription is enabled.
- Required resource providers are registered:
  - `Microsoft.Web`
  - `Microsoft.KeyVault`
  - `Microsoft.Sql`
  - `Microsoft.ServiceBus`
  - `Microsoft.Insights`
  - `Microsoft.OperationalInsights`
- Resource group exists.
- App Service Plan exists.
- Web App exists and is running.
- Key Vault exists and accepts non-secret write/delete.
- Azure SQL server exists.
- Azure SQL database is online.
- Service Bus namespace exists and is active.
- Service Bus queue exists and is active.
- Application Insights exists.
- Web App app settings accept non-secret write/delete.
- Full `infra/azure/provision.sh` completes successfully.
- App Service accepts a minimal static deploy through `az webapp deploy`.
- Deployed static preflight file returned HTTP 200.

Resolved during preflight:

- `infra/azure/read-env.sh` now treats empty environment values as missing when a default is provided. This prevents blank `AZURE_SERVICE_BUS_NAMESPACE` or `AZURE_SERVICE_BUS_QUEUE` entries from overriding the intended defaults.
- `Microsoft.OperationalInsights` was registered.
- `replyinmyvoice-ai-dev` Application Insights resource was created.

### GitHub Push Or Repo Settings Permission Denied

Status: cleared.

Verified:

- Git remote is configured for `git@github.com:ChuanQiao1128/replyinmyvoice.git`.
- GitHub SSH authentication succeeds.
- `git ls-remote origin --heads` succeeds.
- `git push --dry-run origin HEAD:refs/heads/codex/preflight-permission-check` succeeds.
- GitHub CLI is authenticated with repo/workflow scope.
- Repository permission is `ADMIN`.
- GitHub Actions variable write/delete succeeds.
- GitHub Actions secret write/delete succeeds.

Resolved during preflight:

- Created or configured Azure OIDC for GitHub Actions deploy:
  - Azure app registration for GitHub Actions.
  - Azure service principal.
  - Resource-group-level Contributor assignment.
  - Federated credential for `main` branch GitHub Actions.
  - GitHub Actions secrets:
    - `AZURE_CLIENT_ID`
    - `AZURE_TENANT_ID`
    - `AZURE_SUBSCRIPTION_ID`
  - GitHub Actions variables:
    - `AZURE_RESOURCE_GROUP`
    - `AZURE_APP_SERVICE_NAME`

### Required Secret Missing Or Invalid

Status: cleared for current sandbox/dev run.

Presence verified:

- Azure subscription and tenant values.
- Azure resource names.
- OpenAI API key and model name.
- Stripe sandbox secret key.
- Stripe price ID.
- Stripe webhook secret.
- Sapling API key.
- Clerk secret key.
- Clerk publishable key.

Connectivity verified:

- OpenAI model lookup succeeds for the configured model.
- Stripe sandbox price lookup succeeds and confirms NZD monthly price.
- Stripe webhook secret has the expected sandbox webhook-secret format.
- Clerk API request succeeds.
- Sapling `aidetect` request succeeds and returns numeric `score`.

### Live-Mode Payment Or Real Charge Required

Status: not a blocker for the next run.

The next run must stay in Stripe sandbox/test mode. Live-mode Stripe subscription setup, live payment, real charge, and production billing cutover are explicitly out of scope and must not stop the development run.

### Production Domain Cutover Required

Status: not a blocker for the next run.

The next run should deploy and verify the Azure dev App Service URL. It must not require production domain cutover. If production-domain changes are needed later, document them as manual launch steps.

### Secret Exposure Risk

Status: handled.

Preflight checks avoided printing:

- Azure SQL passwords.
- Service Bus connection strings.
- Stripe secret keys.
- Stripe webhook secrets.
- OpenAI API keys.
- Sapling API keys.
- Clerk secret keys.
- Cloudflare API tokens.
- Key Vault secret values.

## Remaining Non-Blocking Risks

These are not valid reasons to stop the next long run:

- Transient Azure CLI or Azure control-plane delays.
- Ordinary build, test, migration, deployment, or packaging failures.
- App Service warm-up delays.
- Provider timeout or provider temporary unavailability, as long as tests and implementation handle those failures correctly.
- Stripe live-mode not being configured.
- Production domain not being cut over.

The actual GitHub Actions OIDC login will be fully proven when a workflow run executes on `main`. The repo secrets, variables, app registration, service principal, federated credential, and resource-group role assignment are already configured, so this should be treated as ready unless the workflow itself reports a concrete failure.

## Current Preflight Result

No known blocker remains from the approved stop-condition list.

The next long autonomous run can proceed under:

```text
/Users/qc/Desktop/CloudFlare/docs/dotnet-azure-full-run-target.md
```

If a new failure occurs during the long run, treat it as a normal engineering problem unless it exactly matches one of the stop conditions in the full-run target document.
