#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/../.."
source infra/azure/read-env.sh

RESOURCE_GROUP="$(read_env AZURE_RESOURCE_GROUP replyinmyvoice-dev-rg)"
APP_SERVICE_NAME="$(read_env AZURE_APP_SERVICE_NAME replyinmyvoice-api-dev)"

PUBLISH_ROOT="backend-dotnet/artifacts/publish"
API_PUBLISH="$PUBLISH_ROOT/api"
WORKER_PUBLISH="$PUBLISH_ROOT/worker"
ZIP_PATH="$PUBLISH_ROOT/replyinmyvoice-api-webjob.zip"

rm -rf "$PUBLISH_ROOT"
mkdir -p "$API_PUBLISH" "$WORKER_PUBLISH"

dotnet test backend-dotnet/ReplyInMyVoice.sln
dotnet publish backend-dotnet/src/ReplyInMyVoice.Api/ReplyInMyVoice.Api.csproj -c Release -o "$API_PUBLISH"
dotnet publish backend-dotnet/src/ReplyInMyVoice.Worker/ReplyInMyVoice.Worker.csproj -c Release -o "$WORKER_PUBLISH"

WEBJOB_DIR="$API_PUBLISH/App_Data/jobs/continuous/RewriteWorker"
mkdir -p "$WEBJOB_DIR"
cp -R "$WORKER_PUBLISH"/. "$WEBJOB_DIR"/
cat > "$WEBJOB_DIR/run.cmd" <<'EOF'
dotnet ReplyInMyVoice.Worker.dll
EOF

(cd "$API_PUBLISH" && zip -qr "../$(basename "$ZIP_PATH")" .)

arch -arm64 az webapp deploy \
  --resource-group "$RESOURCE_GROUP" \
  --name "$APP_SERVICE_NAME" \
  --src-path "$ZIP_PATH" \
  --type zip \
  --async false \
  --output none

echo "Deploy complete: https://${APP_SERVICE_NAME}.azurewebsites.net/health"
