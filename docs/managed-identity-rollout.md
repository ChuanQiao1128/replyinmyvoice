# Managed Identity Rollout

## 1. Overview And Settings

This runbook switches the Azure Functions backend from key-based Azure SQL and Service Bus access to Managed Identity in staged, reversible steps. Existing connection-string settings remain valid fallbacks while the flag is off.

| Setting | Purpose | Default |
| --- | --- | --- |
| `USE_MANAGED_IDENTITY` | Enables SQL and publisher identity auth when set to `true`. | `false` |
| `ServiceBus__fullyQualifiedNamespace` | Identity-based Service Bus namespace setting used by the Functions host and publisher. | unset |
| `SERVICEBUS_FULLY_QUALIFIED_NAMESPACE` | Publisher-only fallback namespace setting. | unset |
| `AZURE_SQL_SERVER` | Azure SQL server host, for example `<sql-server>.database.windows.net`. | unset |
| `AZURE_SQL_DATABASE` | Azure SQL database name. | unset |
| `AZURE_CLIENT_ID` | Optional user-assigned identity selector honored by Azure SDK credentials. | unset |

## 2. Discovery

List account and Function App setting names only. Do not print setting values.

```bash
az account show
az functionapp config appsettings list -g <rg> -n <function-app> --query "[].name" -o tsv
```

For the dev Function App, `<function-app>` may be `replyinmyvoice-func-dev`.

## 3. Enable Identity

Assign a system identity to the Function App and capture the returned `principalId`.

```bash
az functionapp identity assign -g <rg> -n <function-app>
```

## 4. Service Bus RBAC

Grant the Function App identity send and receive access to the Service Bus namespace.

```bash
az role assignment create \
  --assignee-object-id <principalId> \
  --assignee-principal-type ServicePrincipal \
  --role "Azure Service Bus Data Sender" \
  --scope /subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.ServiceBus/namespaces/<namespace>

az role assignment create \
  --assignee-object-id <principalId> \
  --assignee-principal-type ServicePrincipal \
  --role "Azure Service Bus Data Receiver" \
  --scope /subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.ServiceBus/namespaces/<namespace>
```

Queue-scoped assignments are also valid when only `rewrite-jobs` should be accessible:

```text
/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.ServiceBus/namespaces/<namespace>/queues/rewrite-jobs
```

Allow about five minutes for RBAC propagation before flipping runtime settings.

## 5. SQL

Ensure the Azure SQL server has an Entra administrator.

```bash
az sql server ad-admin create \
  -g <rg> \
  --server-name <sql-server> \
  --display-name <admin-display-name> \
  --object-id <admin-object-id>
```

Then connect to the target database with Entra auth through the portal Query editor or `sqlcmd`, and grant the Function App identity data access:

```sql
CREATE USER [<function-app-name>] FROM EXTERNAL PROVIDER;
ALTER ROLE db_datareader ADD MEMBER [<function-app-name>];
ALTER ROLE db_datawriter ADD MEMBER [<function-app-name>];
```

Do not grant `db_ddladmin`. EF migrations keep using the existing CI SQL credentials from `.github/workflows/dotnet-azure.yml`; this rollout does not change migration execution.

## 6. Flip Publisher And SQL

After identity, roles, and SQL user setup are complete, enable identity mode while leaving existing connection-string settings in place for rollback.

```bash
az functionapp config appsettings set \
  -g <rg> \
  -n <function-app> \
  --settings \
  USE_MANAGED_IDENTITY=true \
  ServiceBus__fullyQualifiedNamespace=<namespace>.servicebus.windows.net \
  AZURE_SQL_SERVER=<sql-server>.database.windows.net \
  AZURE_SQL_DATABASE=<database>
```

The Function App restarts after app settings change. If a user-assigned identity is used, include `AZURE_CLIENT_ID=<client-id>`.

## 7. Verify

Check readiness and one end-to-end rewrite before changing the Service Bus trigger setting.

```bash
curl -fsS https://<function-app>.azurewebsites.net/api/health/ready
```

Expected readiness signals:

```text
checks.serviceBus.configured == true
checks.serviceBus.authMode == "managed_identity"
ok == true
```

Then submit one rewrite from `/app`, confirm it completes, and review Application Insights for Azure SQL or Service Bus dependency failures.

## 8. Flip The Trigger Last

The Service Bus trigger uses identity from `ServiceBus__fullyQualifiedNamespace`, but if a literal `ServiceBus` connection-string setting still exists, the host keeps using that connection string. Delete the connection-string settings only after step 7 passes.

```bash
az functionapp config appsettings delete \
  -g <rg> \
  -n <function-app> \
  --setting-names ServiceBus
```

If discovery showed any of these connection-string setting names, delete them in the same staged trigger flip:

```text
ConnectionStrings__ServiceBus
SERVICEBUS_CONNECTION_STRING
AZURE_SERVICE_BUS_CONNECTION_STRING
```

Re-run `/api/health/ready`, submit another rewrite, and confirm queued work drains.

## 9. Rollback

Turn the flag off and restore any deleted Service Bus connection-string settings.

```bash
az functionapp config appsettings set \
  -g <rg> \
  -n <function-app> \
  --settings USE_MANAGED_IDENTITY=false
```

Role assignments and the SQL user can remain in place; they are harmless while the flag is off.

## 10. Out Of Scope

`AzureWebJobsStorage` stays key-based. CI migration credentials stay unchanged. This runbook does not execute infrastructure changes, alter Stripe settings, or change DNS.
