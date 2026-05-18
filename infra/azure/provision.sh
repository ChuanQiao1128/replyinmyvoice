#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/../.."
source infra/azure/read-env.sh

SUBSCRIPTION_ID="$(read_env AZURE_SUBSCRIPTION_ID)"
LOCATION="$(read_env AZURE_LOCATION australiaeast)"
RESOURCE_GROUP="$(read_env AZURE_RESOURCE_GROUP replyinmyvoice-dev-rg)"
ALLOW_PAID="$(read_env AZURE_ALLOW_PAID_RESOURCES false)"
APP_SERVICE_PLAN="$(read_env AZURE_APP_SERVICE_PLAN_NAME replyinmyvoice-plan-dev)"
APP_SERVICE_NAME="$(read_env AZURE_APP_SERVICE_NAME replyinmyvoice-api-dev)"
SQL_SERVER="$(read_env AZURE_SQL_SERVER_NAME replyinmyvoice-sql-dev)"
SQL_DATABASE="$(read_env AZURE_SQL_DATABASE_NAME replyinmyvoice-db-dev)"
KEY_VAULT="$(read_env AZURE_KEY_VAULT_NAME replyinmyvoice-kv-dev)"
APP_INSIGHTS="$(read_env AZURE_APPLICATION_INSIGHTS_NAME replyinmyvoice-ai-dev)"
SERVICE_BUS_NAMESPACE="$(read_env AZURE_SERVICE_BUS_NAMESPACE replyinmyvoice-sb-dev)"
SERVICE_BUS_QUEUE="$(read_env AZURE_SERVICE_BUS_QUEUE rewrite-jobs)"

if [[ "$ALLOW_PAID" != "true" ]]; then
  echo "AZURE_ALLOW_PAID_RESOURCES is not true. Refusing to create Azure resources."
  exit 1
fi

if [[ -z "$SUBSCRIPTION_ID" ]]; then
  echo "AZURE_SUBSCRIPTION_ID is required."
  exit 1
fi

arch -arm64 az account set --subscription "$SUBSCRIPTION_ID"

echo "Creating/updating Azure resources in $RESOURCE_GROUP ($LOCATION)."
for provider in Microsoft.Web Microsoft.KeyVault Microsoft.Sql Microsoft.ServiceBus Microsoft.Insights Microsoft.OperationalInsights; do
  arch -arm64 az provider register --namespace "$provider" --output none || true
done

arch -arm64 az group create \
  --name "$RESOURCE_GROUP" \
  --location "$LOCATION" \
  --tags project=replyinmyvoice environment=dev \
  --output none

arch -arm64 az appservice plan create \
  --resource-group "$RESOURCE_GROUP" \
  --name "$APP_SERVICE_PLAN" \
  --location "$LOCATION" \
  --sku B1 \
  --output none

arch -arm64 az webapp create \
  --resource-group "$RESOURCE_GROUP" \
  --plan "$APP_SERVICE_PLAN" \
  --name "$APP_SERVICE_NAME" \
  --runtime "dotnet:8" \
  --output none

arch -arm64 az webapp config set \
  --resource-group "$RESOURCE_GROUP" \
  --name "$APP_SERVICE_NAME" \
  --always-on true \
  --output none

if ! arch -arm64 az keyvault show --resource-group "$RESOURCE_GROUP" --name "$KEY_VAULT" --output none 2>/dev/null; then
  arch -arm64 az keyvault create \
    --resource-group "$RESOURCE_GROUP" \
    --name "$KEY_VAULT" \
    --location "$LOCATION" \
    --enable-rbac-authorization false \
    --output none
fi

if ! arch -arm64 az sql server show --resource-group "$RESOURCE_GROUP" --name "$SQL_SERVER" --output none 2>/dev/null; then
  SQL_ADMIN_USER="rimvadmin"
  SQL_ADMIN_PASSWORD="$(openssl rand -base64 36 | tr '/+' 'Aa' | cut -c1-32)!1a"
  arch -arm64 az sql server create \
    --resource-group "$RESOURCE_GROUP" \
    --name "$SQL_SERVER" \
    --location "$LOCATION" \
    --admin-user "$SQL_ADMIN_USER" \
    --admin-password "$SQL_ADMIN_PASSWORD" \
    --minimal-tls-version 1.2 \
    --output none

  arch -arm64 az keyvault secret set \
    --vault-name "$KEY_VAULT" \
    --name "sql-admin-user" \
    --value "$SQL_ADMIN_USER" \
    --output none
  arch -arm64 az keyvault secret set \
    --vault-name "$KEY_VAULT" \
    --name "sql-admin-password" \
    --value "$SQL_ADMIN_PASSWORD" \
    --output none
fi

SQL_ADMIN_USER="$(arch -arm64 az keyvault secret show --vault-name "$KEY_VAULT" --name "sql-admin-user" --query value -o tsv)"
SQL_ADMIN_PASSWORD="$(arch -arm64 az keyvault secret show --vault-name "$KEY_VAULT" --name "sql-admin-password" --query value -o tsv)"

arch -arm64 az sql db create \
  --resource-group "$RESOURCE_GROUP" \
  --server "$SQL_SERVER" \
  --name "$SQL_DATABASE" \
  --service-objective Basic \
  --backup-storage-redundancy Local \
  --output none

CLIENT_IP="$(curl -s https://api.ipify.org || true)"
if [[ -n "$CLIENT_IP" ]]; then
  arch -arm64 az sql server firewall-rule create \
    --resource-group "$RESOURCE_GROUP" \
    --server "$SQL_SERVER" \
    --name AllowLocalDevMachine \
    --start-ip-address "$CLIENT_IP" \
    --end-ip-address "$CLIENT_IP" \
    --output none || true
fi

arch -arm64 az sql server firewall-rule create \
  --resource-group "$RESOURCE_GROUP" \
  --server "$SQL_SERVER" \
  --name AllowAzureServices \
  --start-ip-address 0.0.0.0 \
  --end-ip-address 0.0.0.0 \
  --output none || true

if ! arch -arm64 az servicebus namespace show --resource-group "$RESOURCE_GROUP" --name "$SERVICE_BUS_NAMESPACE" --output none 2>/dev/null; then
  arch -arm64 az servicebus namespace create \
    --resource-group "$RESOURCE_GROUP" \
    --name "$SERVICE_BUS_NAMESPACE" \
    --location "$LOCATION" \
    --sku Basic \
    --output none
fi

arch -arm64 az servicebus queue create \
  --resource-group "$RESOURCE_GROUP" \
  --namespace-name "$SERVICE_BUS_NAMESPACE" \
  --name "$SERVICE_BUS_QUEUE" \
  --max-delivery-count 5 \
  --output none

if ! arch -arm64 az monitor app-insights component show --resource-group "$RESOURCE_GROUP" --app "$APP_INSIGHTS" --output none 2>/dev/null; then
  arch -arm64 az monitor app-insights component create \
    --resource-group "$RESOURCE_GROUP" \
    --app "$APP_INSIGHTS" \
    --location "$LOCATION" \
    --application-type web \
    --output none
fi

SERVICE_BUS_CONNECTION="$(arch -arm64 az servicebus namespace authorization-rule keys list \
  --resource-group "$RESOURCE_GROUP" \
  --namespace-name "$SERVICE_BUS_NAMESPACE" \
  --name RootManageSharedAccessKey \
  --query primaryConnectionString \
  -o tsv)"

APPINSIGHTS_CONNECTION="$(arch -arm64 az monitor app-insights component show \
  --resource-group "$RESOURCE_GROUP" \
  --app "$APP_INSIGHTS" \
  --query connectionString \
  -o tsv)"

SQL_CONNECTION="Server=tcp:${SQL_SERVER}.database.windows.net,1433;Initial Catalog=${SQL_DATABASE};Persist Security Info=False;User ID=${SQL_ADMIN_USER};Password=${SQL_ADMIN_PASSWORD};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

arch -arm64 az webapp config connection-string set \
  --resource-group "$RESOURCE_GROUP" \
  --name "$APP_SERVICE_NAME" \
  --connection-string-type SQLAzure \
  --settings DefaultConnection="$SQL_CONNECTION" ServiceBus="$SERVICE_BUS_CONNECTION" \
  --output none

arch -arm64 az webapp config appsettings set \
  --resource-group "$RESOURCE_GROUP" \
  --name "$APP_SERVICE_NAME" \
  --settings \
    ASPNETCORE_ENVIRONMENT=Production \
    SERVICEBUS_QUEUE_NAME="$SERVICE_BUS_QUEUE" \
    APPLICATIONINSIGHTS_CONNECTION_STRING="$APPINSIGHTS_CONNECTION" \
    OPENAI_MODEL="$(read_env OPENAI_MODEL gpt-4o-mini)" \
    OPENAI_TIMEOUT_SEC="$(read_env OPENAI_TIMEOUT_SEC 25)" \
    NEXT_PUBLIC_APP_URL="$(read_env NEXT_PUBLIC_APP_URL "https://${APP_SERVICE_NAME}.azurewebsites.net")" \
    STRIPE_PRICE_ID="$(read_env STRIPE_PRICE_ID)" \
    WRITING_SIGNAL_PROVIDER="$(read_env WRITING_SIGNAL_PROVIDER sapling)" \
    WRITING_SIGNAL_TIMEOUT_SEC="$(read_env WRITING_SIGNAL_TIMEOUT_SEC 10)" \
    ALLOW_HEADER_AUTH="$(read_env ALLOW_HEADER_AUTH false)" \
  --output none

for secret_name in OPENAI_API_KEY SAPLING_API_KEY STRIPE_SECRET_KEY STRIPE_WEBHOOK_SECRET CLERK_SECRET_KEY NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY CLERK_JWT_ISSUER CLERK_JWT_AUDIENCE; do
  value="$(read_env "$secret_name")"
  if [[ -n "$value" ]]; then
    arch -arm64 az webapp config appsettings set \
      --resource-group "$RESOURCE_GROUP" \
      --name "$APP_SERVICE_NAME" \
      --settings "$secret_name=$value" \
      --output none
  fi
done

echo "Provisioning complete for $APP_SERVICE_NAME."
echo "API URL: https://${APP_SERVICE_NAME}.azurewebsites.net"
