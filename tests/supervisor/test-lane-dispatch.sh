#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)
REGISTRY_PATH="/tmp/test-lane-dispatch-$$.json"
TMP_PATH="${REGISTRY_PATH}.tmp"

cleanup() {
  rm -f "$REGISTRY_PATH" "$TMP_PATH"
}
trap cleanup EXIT

cat > "$REGISTRY_PATH" <<'JSON'
{
  "schema_version": 1,
  "items": [
    {
      "id": "M1-010",
      "lane": "epic",
      "owner_class": "strong-model",
      "status": "pending",
      "planner_attempts": 0,
      "added_at": "2026-05-23T00:03:00Z"
    },
    {
      "id": "M1-020",
      "lane": "evidence",
      "owner_class": "human-only",
      "status": "pending",
      "evidence_type": "stripe-event",
      "added_at": "2026-05-23T00:02:00Z"
    },
    {
      "id": "M1-030",
      "lane": "direct",
      "owner_class": "loop",
      "coupling": "medium",
      "brief_state": "detailed",
      "status": "pending",
      "added_at": "2026-05-23T00:01:00Z"
    },
    {
      "id": "M1-031",
      "lane": "direct",
      "owner_class": "loop",
      "coupling": "low",
      "brief_state": "manifest-only",
      "status": "pending",
      "added_at": "2026-05-23T00:00:00Z"
    },
    {
      "id": "M1-040",
      "lane": "direct",
      "owner_class": "loop",
      "coupling": "low",
      "brief_state": "detailed",
      "status": "in_progress",
      "added_at": "2026-05-22T00:00:00Z"
    },
    {
      "id": "M1-005",
      "lane": "epic",
      "owner_class": "strong-model",
      "status": "pending",
      "planner_attempts": 5,
      "added_at": "2026-05-21T00:00:00Z"
    }
  ]
}
JSON

SUPERVISOR_SOURCING_ONLY=1 source "$REPO_ROOT/plans/overnight-supervisor.sh"
REGISTRY_PATH="$REGISTRY_PATH"

assert_selection() {
  local expected=$1
  local actual
  actual=$(select_next_item_by_lane)
  if [ "$actual" != "$expected" ]; then
    printf 'expected: %s\nactual:   %s\n' "$expected" "$actual" >&2
    return 1
  fi
}

remove_item() {
  local id=$1
  jq --arg id "$id" 'del(.items[] | select(.id == $id))' "$REGISTRY_PATH" > "$TMP_PATH"
  mv "$TMP_PATH" "$REGISTRY_PATH"
}

assert_selection "selected lane: epic, item: M1-010"
remove_item "M1-010"

assert_selection "selected lane: evidence, item: M1-020"
remove_item "M1-020"

assert_selection "selected lane: direct, item: M1-030"
remove_item "M1-030"

assert_selection "selected lane: none, item: -"

printf 'lane dispatch test passed\n'
