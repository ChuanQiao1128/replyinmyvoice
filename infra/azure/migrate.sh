#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/../.."
source infra/azure/read-env.sh

SUBSCRIPTION_ID="$(read_env AZURE_SUBSCRIPTION_ID)"
RESOURCE_GROUP="$(read_env AZURE_RESOURCE_GROUP replyinmyvoice-dev-rg)"
SQL_SERVER="$(read_env AZURE_SQL_SERVER_NAME replyinmyvoice-sql-dev)"
SQL_DATABASE="$(read_env AZURE_SQL_DATABASE_NAME replyinmyvoice-db-dev)"
KEY_VAULT="$(read_env AZURE_KEY_VAULT_NAME replyinmyvoice-kv-dev)"

if [[ -z "$SUBSCRIPTION_ID" ]]; then
  echo "AZURE_SUBSCRIPTION_ID is required."
  exit 1
fi

arch -arm64 az account set --subscription "$SUBSCRIPTION_ID"

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

SQL_ADMIN_USER="$(arch -arm64 az keyvault secret show --vault-name "$KEY_VAULT" --name "sql-admin-user" --query value -o tsv)"
SQL_ADMIN_PASSWORD="$(arch -arm64 az keyvault secret show --vault-name "$KEY_VAULT" --name "sql-admin-password" --query value -o tsv)"
SQL_CONNECTION="Server=tcp:${SQL_SERVER}.database.windows.net,1433;Initial Catalog=${SQL_DATABASE};Persist Security Info=False;User ID=${SQL_ADMIN_USER};Password=${SQL_ADMIN_PASSWORD};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

ConnectionStrings__DefaultConnection="$SQL_CONNECTION" \
  dotnet ef database update \
    --project backend-dotnet/src/ReplyInMyVoice.Infrastructure/ReplyInMyVoice.Infrastructure.csproj \
    --startup-project backend-dotnet/src/ReplyInMyVoice.Api/ReplyInMyVoice.Api.csproj \
    --context AppDbContext

echo "Azure SQL migrations applied to $SQL_DATABASE."
