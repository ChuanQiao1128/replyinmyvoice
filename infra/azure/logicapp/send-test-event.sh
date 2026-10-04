#!/usr/bin/env bash
# Send one sample event to ops-events via the Service Bus REST API (Send-only SAS).
# Usage: infra/azure/logicapp/send-test-event.sh [type] [detail]
set -euo pipefail

cd "$(dirname "$0")/../../.."
source infra/azure/read-env.sh

RESOURCE_GROUP="$(read_env AZURE_RESOURCE_GROUP replyinmyvoice-dev-rg)"
NS="$(read_env AZURE_SERVICE_BUS_NAMESPACE replyinmyvoice-sb-dev)"
QUEUE="ops-events"
RULE="demo-send"
TYPE="${1:-rewrite.failed}"
DETAIL="${2:-sample event from send-test-event.sh}"

az servicebus queue authorization-rule create -g "$RESOURCE_GROUP" --namespace-name "$NS" \
  --queue-name "$QUEUE" -n "$RULE" --rights Send --output none
KEY="$(az servicebus queue authorization-rule keys list -g "$RESOURCE_GROUP" --namespace-name "$NS" \
  --queue-name "$QUEUE" -n "$RULE" --query primaryKey -o tsv)"

URI="https://${NS}.servicebus.windows.net/${QUEUE}"
TOKEN="$(python3 - "$URI" "$RULE" "$KEY" <<'PY'
import base64, hashlib, hmac, sys, time, urllib.parse
uri, rule, key = sys.argv[1:]
enc = urllib.parse.quote_plus(uri)
exp = str(int(time.time()) + 300)
sig = base64.b64encode(hmac.new(key.encode(), f"{enc}\n{exp}".encode(), hashlib.sha256).digest())
print(f"SharedAccessSignature sr={enc}&sig={urllib.parse.quote_plus(sig)}&se={exp}&skn={rule}")
PY
)"

BODY="$(python3 -c 'import json,sys,uuid,datetime; print(json.dumps({"type":sys.argv[1],"jobId":str(uuid.uuid4()),"userId":"demo-user","detail":sys.argv[2],"occurredAt":datetime.datetime.now(datetime.timezone.utc).isoformat()}))' "$TYPE" "$DETAIL")"

curl -sS -o /dev/null -w "send ${TYPE}: HTTP %{http_code}\n" -X POST "${URI}/messages" \
  -H "Authorization: ${TOKEN}" -H "Content-Type: application/json" --data "$BODY"
