#!/usr/bin/env bash
set -euo pipefail

# Creates or updates Reply In My Voice business-metric alert rules.
# This file is an operator-run artifact and is not executed by CI.
# Alert rules are paid Azure resources; keep the explicit guard below.
# The pack creates five scheduled-query alerts and one native metric alert.

if [[ "${AZURE_ALLOW_PAID_RESOURCES:-}" != "true" ]]; then
  echo "AZURE_ALLOW_PAID_RESOURCES must be true before creating paid alert resources." >&2
  exit 1
fi

missing=()
for name in AZURE_RESOURCE_GROUP APPINSIGHTS_RESOURCE_NAME SERVICEBUS_NAMESPACE_ID; do
  if [[ -z "${!name:-}" ]]; then
    missing+=("$name")
  fi
done

if (( ${#missing[@]} > 0 )); then
  printf 'Missing required environment variable name(s): %s\n' "${missing[*]}" >&2
  exit 1
fi

SERVICEBUS_QUEUE_NAME="${SERVICEBUS_QUEUE_NAME:-rewrite-jobs}"

az extension add --name scheduled-query --upgrade --yes >/dev/null 2>&1 || true

APPINSIGHTS_ID="$(az resource show \
  --resource-group "$AZURE_RESOURCE_GROUP" \
  --name "$APPINSIGHTS_RESOURCE_NAME" \
  --resource-type microsoft.insights/components \
  --query id \
  --output tsv)"

ALERT_LOCATION="${ALERT_LOCATION:-}"
if [[ -z "$ALERT_LOCATION" ]]; then
  ALERT_LOCATION="$(az resource show --ids "$APPINSIGHTS_ID" --query location --output tsv)"
fi

ACTION_ARGS=()
if [[ -n "${ALERT_ACTION_GROUP_ID:-}" ]]; then
  ACTION_ARGS+=(--action "$ALERT_ACTION_GROUP_ID")
fi

upsert_scheduled_query_alert() {
  local rule_name="$1"
  local query="$2"
  local threshold="$3"
  local window_size="$4"
  local frequency="$5"
  local severity="$6"
  local operator="${7:-GreaterThan}"

  local common_args=(
    --resource-group "$AZURE_RESOURCE_GROUP"
    --name "$rule_name"
    --scopes "$APPINSIGHTS_ID"
    --condition "AggregatedValue $operator $threshold"
    --condition-query "$query"
    --window-size "$window_size"
    --evaluation-frequency "$frequency"
    --severity "$severity"
    --enabled true
  )

  if az monitor scheduled-query show --resource-group "$AZURE_RESOURCE_GROUP" --name "$rule_name" >/dev/null 2>&1; then
    az monitor scheduled-query update "${common_args[@]}" "${ACTION_ARGS[@]}"
  else
    az monitor scheduled-query create \
      --location "$ALERT_LOCATION" \
      --description "Reply In My Voice business metric alert: $rule_name" \
      "${common_args[@]}" \
      "${ACTION_ARGS[@]}"
  fi
}

upsert_metric_alert() {
  local rule_name="rimv-servicebus-dlq"
  local condition="max DeadletteredMessages > 0 where EntityName includes '$SERVICEBUS_QUEUE_NAME'"
  local common_args=(
    --resource-group "$AZURE_RESOURCE_GROUP"
    --name "$rule_name"
    --scopes "$SERVICEBUS_NAMESPACE_ID"
    --condition "$condition"
    --window-size 5m
    --evaluation-frequency 1m
    --severity 1
    --enabled true
  )

  if az monitor metrics alert show --resource-group "$AZURE_RESOURCE_GROUP" --name "$rule_name" >/dev/null 2>&1; then
    az monitor metrics alert update "${common_args[@]}" "${ACTION_ARGS[@]}"
  else
    az monitor metrics alert create \
      --description "Reply In My Voice Service Bus DLQ depth alert" \
      "${common_args[@]}" \
      "${ACTION_ARGS[@]}"
  fi
}

upsert_scheduled_query_alert \
  "rimv-outbox-backlog-age" \
  'customMetrics | where name == "outbox_backlog_age_seconds" | summarize AggregatedValue = max(valueMax)' \
  120 \
  10m \
  5m \
  2

upsert_scheduled_query_alert \
  "rimv-outbox-failed" \
  'customMetrics | where name == "outbox_failed_total" | summarize AggregatedValue = sum(valueSum)' \
  0 \
  15m \
  5m \
  2

upsert_scheduled_query_alert \
  "rimv-stripe-event-failed" \
  'customMetrics | where name == "stripe_event_failed_total" | summarize AggregatedValue = sum(valueSum)' \
  0 \
  15m \
  5m \
  1

upsert_scheduled_query_alert \
  "rimv-quota-release-spike" \
  'customMetrics | where name == "quota_released_total" | summarize AggregatedValue = sum(valueSum)' \
  10 \
  30m \
  15m \
  2

upsert_scheduled_query_alert \
  "rimv-provider-circuit-open" \
  'customMetrics | where name == "provider_breaker_open_total" | summarize AggregatedValue = sum(valueSum)' \
  3 \
  15m \
  5m \
  2 \
  GreaterThanOrEqual

upsert_metric_alert
