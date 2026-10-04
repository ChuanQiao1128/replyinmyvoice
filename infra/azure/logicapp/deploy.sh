#!/usr/bin/env bash
# Provision the ops-events queue + low-code Logic App demo.
# Secrets (SAS connection string, storage key) are fetched into shell variables
# and passed straight to the deployment; nothing is printed.
set -euo pipefail

cd "$(dirname "$0")/../../.."
source infra/azure/read-env.sh

RESOURCE_GROUP="$(read_env AZURE_RESOURCE_GROUP replyinmyvoice-dev-rg)"
SERVICE_BUS_NAMESPACE="$(read_env AZURE_SERVICE_BUS_NAMESPACE replyinmyvoice-sb-dev)"
STORAGE_ACCOUNT_NAME="$(read_env AZURE_FUNCTION_STORAGE_ACCOUNT replyinmyvoicefuncdev)"
QUEUE="ops-events"
CONTAINER="logicapp-inbox"
LISTEN_RULE="logicapp-listen"

if ! az servicebus queue show -g "$RESOURCE_GROUP" --namespace-name "$SERVICE_BUS_NAMESPACE" -n "$QUEUE" --output none 2>/dev/null; then
  az servicebus queue create -g "$RESOURCE_GROUP" --namespace-name "$SERVICE_BUS_NAMESPACE" -n "$QUEUE" \
    --lock-duration PT5M --max-delivery-count 5 --default-message-time-to-live P7D --output none
fi

# Queue-scoped rule: the Logic App can Listen (receive/complete) on ops-events only.
az servicebus queue authorization-rule create -g "$RESOURCE_GROUP" --namespace-name "$SERVICE_BUS_NAMESPACE" \
  --queue-name "$QUEUE" -n "$LISTEN_RULE" --rights Listen --output none

SB_CONN="$(az servicebus queue authorization-rule keys list -g "$RESOURCE_GROUP" --namespace-name "$SERVICE_BUS_NAMESPACE" \
  --queue-name "$QUEUE" -n "$LISTEN_RULE" --query primaryConnectionString -o tsv)"
# The managed connector wants a namespace-level string without EntityPath.
SB_CONN="${SB_CONN%;EntityPath=*}"

STORAGE_KEY="$(az storage account keys list -g "$RESOURCE_GROUP" -n "$STORAGE_ACCOUNT_NAME" --query '[0].value' -o tsv)"
az storage container create --account-name "$STORAGE_ACCOUNT_NAME" --account-key "$STORAGE_KEY" -n "$CONTAINER" --output none

az deployment group create -g "$RESOURCE_GROUP" -n logicapp-ops-events \
  --template-file infra/azure/logicapp/ops-events.bicep \
  --parameters queueName="$QUEUE" \
    storageAccountName="$STORAGE_ACCOUNT_NAME" archiveContainer="$CONTAINER" \
    serviceBusListenConnectionString="$SB_CONN" storageAccountKey="$STORAGE_KEY" \
  --query properties.outputs -o json
