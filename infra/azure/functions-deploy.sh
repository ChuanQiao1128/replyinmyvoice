#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/../.."
source infra/azure/read-env.sh

RESOURCE_GROUP="$(read_env AZURE_RESOURCE_GROUP replyinmyvoice-dev-rg)"
FUNCTION_APP_NAME="$(read_env AZURE_FUNCTION_APP_NAME replyinmyvoice-func-dev)"

rm -rf backend-dotnet/artifacts/functions backend-dotnet/artifacts/replyinmyvoice-functions.zip

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
