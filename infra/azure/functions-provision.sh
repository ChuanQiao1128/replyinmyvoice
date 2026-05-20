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
APP_INSIGHTS="$(read_env AZURE_APPLICATION_INSIGHTS_NAME replyinmyvoice-ai-dev)"
SERVICE_BUS_NAMESPACE="$(read_env AZURE_SERVICE_BUS_NAMESPACE replyinmyvoice-sb-dev)"
SERVICE_BUS_QUEUE="$(read_env AZURE_SERVICE_BUS_QUEUE rewrite-jobs)"

if [[ -z "$SUBSCRIPTION_ID" ]]; then
  echo "AZURE_SUBSCRIPTION_ID is required."
  exit 1
fi

arch -arm64 az account set --subscription "$SUBSCRIPTION_ID"

for provider in Microsoft.Storage Microsoft.Web Microsoft.KeyVault Microsoft.Sql Microsoft.ServiceBus Microsoft.Insights Microsoft.OperationalInsights; do
  arch -arm64 az provider register --namespace "$provider" --output none || true
done

arch -arm64 az group create \
  --name "$RESOURCE_GROUP" \
  --location "$LOCATION" \
  --tags project=replyinmyvoice environment=dev \
  --output none

if ! arch -arm64 az storage account show \
  --resource-group "$RESOURCE_GROUP" \
  --name "$STORAGE_ACCOUNT_NAME" \
  --output none 2>/dev/null; then
  arch -arm64 az storage account create \
    --resource-group "$RESOURCE_GROUP" \
    --name "$STORAGE_ACCOUNT_NAME" \
    --location "$LOCATION" \
    --sku Standard_LRS \
    --kind StorageV2 \
    --output none
fi

if ! arch -arm64 az functionapp show \
  --resource-group "$RESOURCE_GROUP" \
  --name "$FUNCTION_APP_NAME" \
  --output none 2>/dev/null; then
  arch -arm64 az functionapp create \
    --resource-group "$RESOURCE_GROUP" \
    --name "$FUNCTION_APP_NAME" \
    --storage-account "$STORAGE_ACCOUNT_NAME" \
    --consumption-plan-location "$LOCATION" \
    --runtime dotnet-isolated \
    --runtime-version 8 \
    --functions-version 4 \
    --os-type Linux \
    --output none
fi

SQL_ADMIN_USER="$(arch -arm64 az keyvault secret show --vault-name "$KEY_VAULT" --name "sql-admin-user" --query value -o tsv)"
SQL_ADMIN_PASSWORD="$(arch -arm64 az keyvault secret show --vault-name "$KEY_VAULT" --name "sql-admin-password" --query value -o tsv)"
SQL_CONNECTION="Server=tcp:${SQL_SERVER}.database.windows.net,1433;Initial Catalog=${SQL_DATABASE};Persist Security Info=False;User ID=${SQL_ADMIN_USER};Password=${SQL_ADMIN_PASSWORD};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

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
  -o tsv 2>/dev/null || true)"

arch -arm64 az functionapp config appsettings set \
  --resource-group "$RESOURCE_GROUP" \
  --name "$FUNCTION_APP_NAME" \
  --settings \
    "ConnectionStrings__DefaultConnection=$SQL_CONNECTION" \
    "ServiceBus=$SERVICE_BUS_CONNECTION" \
    "SERVICEBUS_QUEUE_NAME=$SERVICE_BUS_QUEUE" \
    "ALLOW_HEADER_AUTH=$(read_env ALLOW_HEADER_AUTH true)" \
    "OPENAI_MODEL=$(read_env OPENAI_MODEL gpt-4o-mini)" \
    "OPENAI_TIMEOUT_SEC=$(read_env OPENAI_TIMEOUT_SEC 25)" \
    "NEXT_PUBLIC_APP_URL=$(read_env NEXT_PUBLIC_APP_URL "https://${FUNCTION_APP_NAME}.azurewebsites.net")" \
    "STRIPE_PRICE_ID=$(read_env STRIPE_PRICE_ID)" \
    "WRITING_SIGNAL_PROVIDER=$(read_env WRITING_SIGNAL_PROVIDER sapling)" \
    "WRITING_SIGNAL_TIMEOUT_SEC=$(read_env WRITING_SIGNAL_TIMEOUT_SEC 10)" \
  --output none

if [[ -n "$APPINSIGHTS_CONNECTION" ]]; then
  arch -arm64 az functionapp config appsettings set \
    --resource-group "$RESOURCE_GROUP" \
    --name "$FUNCTION_APP_NAME" \
    --settings "APPLICATIONINSIGHTS_CONNECTION_STRING=$APPINSIGHTS_CONNECTION" \
    --output none
fi

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
