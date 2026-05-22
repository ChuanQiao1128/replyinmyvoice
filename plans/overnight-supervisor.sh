#!/usr/bin/env bash
# Overnight Supervisor v2 — shell-driven, codex-as-implementer pattern.
#
# Architecture:
#   - Shell does ALL git operations + gh API
#   - Codex CLI does ONLY file edits + tests, writes plans/task-status.json
#   - Claude is invoked only at morning (by user), reads logs
#
# Usage:
#   cd /Users/qc/Desktop/CloudFlare
#   screen -dmS rimv-overnight bash -lc 'cd /Users/qc/Desktop/CloudFlare && bash plans/overnight-supervisor.sh'
#
# In the Codex desktop environment, prefer screen. nohup/disown launches have
# been observed to die after the launching command exits.
#
# Emergency stop:
#   touch /Users/qc/Desktop/CloudFlare/plans/STOP-OVERNIGHT.txt
#
# Check progress:
#   tail -f plans/overnight.log
#   cat plans/issue-board.md | grep -E "done|BLOCKED" | wc -l

set -uo pipefail
cd "$(dirname "$0")/.."

REPO_DIR=$(pwd)
LOG=plans/overnight.log
PROGRESS=plans/overnight-progress.md
DECISIONS=plans/decisions-log.md
BLOCKERS=plans/blockers-log.md
BUDGET=plans/sleep-run-budget.md
BOARD=plans/issue-board.md
INBOX=plans/codex-worker-inbox.md
STOP_SIGNAL=plans/STOP-OVERNIGHT.txt
MONEY_SIGNAL=plans/MONEY-MADE.txt
ENV_FILE=.env.local

MAX_ISSUES=${MAX_ISSUES:-10000}
MAX_HOURS=${MAX_HOURS:-720}
COOLDOWN_SECONDS=${COOLDOWN_SECONDS:-15}
CODEX_TIMEOUT_SECONDS=${CODEX_TIMEOUT_SECONDS:-600}  # 10 min per codex invocation

START_TS=$(date +%s)
DEADLINE=$((START_TS + MAX_HOURS * 3600))
ISSUES_PROCESSED=0
ISSUES_DONE=0
ISSUES_BLOCKED=0
ISSUES_NEEDS_HUMAN=0
REPAIRS_DONE=0
REPAIRS_BLOCKED=0

# ─── Logging helpers ──────────────────────────────────────────────────────

log() {
  local ts
  ts=$(date -Iseconds)
  echo "[$ts] $*" | tee -a "$LOG"
}

append_progress() {
  cat >> "$PROGRESS" <<EOF

## Trigger at $(date -Iseconds)
$*
EOF
}

append_decision() {
  echo "$(date -Iseconds) | $*" >> "$DECISIONS"
}

append_blocker() {
  local id=$1
  local reason=$2
  local detail=$3
  cat >> "$BLOCKERS" <<EOF

## $(date -Iseconds) — $id — $reason

What was attempted: $detail

What user needs to do: review the branch chore/$id (if any), the log tail, and decide whether to retry, fix the brief, or close the issue.
EOF
}

append_repair_inbox_item() {
  local title=$1
  local class_name=$2
  local priority=$3
  local related=$4
  local evidence=$5
  local action=$6
  local done_condition=$7
  local forbidden=${8:-"live money, npm publish, dashboard changes, secret changes"}

  python3 - "$INBOX" "$title" "$class_name" "$priority" "$related" "$evidence" "$action" "$done_condition" "$forbidden" <<'PY'
from __future__ import annotations

import re
import sys
from datetime import datetime, timezone
from pathlib import Path

inbox = Path(sys.argv[1])
title, class_name, priority, related, evidence, action, done_condition, forbidden = sys.argv[2:10]

if inbox.exists():
    text = inbox.read_text()
else:
    text = """# Codex Worker Inbox

Purpose: machine repair queue for non-user blockers.

## Pending Items

"""

duplicate_pattern = re.compile(
    r"^## .+?\n(?:(?!^## ).)*?^- Status:\s*(?:pending|in_progress)\s*$"
    r"(?:(?!^## ).)*?^- Related issue:\s*" + re.escape(related) + r"\s*$"
    r"(?:(?!^## ).)*?^- Evidence:\s*" + re.escape(evidence) + r"\s*$",
    re.MULTILINE | re.DOTALL,
)
if duplicate_pattern.search(text):
    print("duplicate")
    raise SystemExit(0)

text = text.replace("\nNo queued Codex-worker items yet.\n", "\n")
text = text.replace("\nNo queued repair items yet.\n", "\n")
timestamp = datetime.now(timezone.utc).astimezone().isoformat(timespec="seconds")
item = f"""
## {timestamp} — {title}

- Status: pending
- Source: shell supervisor
- Class: {class_name}
- Priority: {priority}
- Related issue: {related}
- Evidence: {evidence}
- Suggested Codex action: {action}
- Done condition: {done_condition}
- Forbidden actions: {forbidden}
"""

if "## Pending Items" in text:
    text = text.rstrip() + "\n" + item
else:
    text = text.rstrip() + "\n\n## Pending Items\n" + item

inbox.write_text(text.rstrip() + "\n")
print("queued")
PY
}

load_gh_token_from_env_local() {
  # Do not source .env.local; parse only GitHub token keys so arbitrary shell
  # from the env file is never executed.
  if [ -n "${GH_TOKEN:-}" ] || [ -n "${GITHUB_TOKEN:-}" ]; then
    return 0
  fi

  if [ ! -f "$ENV_FILE" ]; then
    return 0
  fi

  local token_line token_name token_value
  token_line=$(python3 - <<'PY'
from pathlib import Path

env_file = Path(".env.local")
for raw in env_file.read_text().splitlines():
    line = raw.strip()
    if not line or line.startswith("#") or "=" not in line:
        continue
    name, _, value = line.partition("=")
    name = name.strip()
    if name.startswith("export "):
        name = name[len("export "):].strip()
    value = value.strip().strip('"').strip("'")
    if name in {"GH_TOKEN", "GITHUB_TOKEN", "GITHUB_PAT"} and value:
        print(f"{name}={value}")
        break
PY
)

  if [ -n "$token_line" ]; then
    token_name=${token_line%%=*}
    token_value=${token_line#*=}
    export GH_TOKEN="$token_value"
    log "Pre-flight: loaded GitHub token from $ENV_FILE ($token_name)"
  fi
}

# ─── Pre-flight ───────────────────────────────────────────────────────────

log "=== Overnight Supervisor v2 starting ==="
log "Repo: $REPO_DIR"
log "Limits: $MAX_ISSUES issues, $MAX_HOURS hours, $CODEX_TIMEOUT_SECONDS s per codex call  (real stop: STOP-OVERNIGHT.txt | MONEY-MADE.txt | no-more-pending)"
log "North star: docs/commercialization-north-star.md"

if [ ! -f "$BOARD" ]; then
  log "ERROR: issue board missing at $BOARD"
  exit 1
fi

if ! command -v codex >/dev/null 2>&1; then
  log "ERROR: codex CLI not in PATH"
  exit 1
fi

if ! command -v gh >/dev/null 2>&1; then
  log "ERROR: gh CLI not in PATH"
  exit 1
fi

load_gh_token_from_env_local

if ! GH_LOGIN=$(gh api user --jq .login 2>/dev/null); then
  log "ERROR: GitHub API authentication failed. Export GH_TOKEN/GITHUB_TOKEN or run gh auth login -h github.com."
  exit 1
fi

log "Pre-flight: GitHub API auth OK as $GH_LOGIN"

log "Pre-flight OK"

# Pre-flight: clear stale git index.lock (a crashed git process can leave one,
# which then breaks every subsequent checkout in the loop)
if [ -f .git/index.lock ]; then
  lock_age=$(($(date +%s) - $(stat -f %m .git/index.lock 2>/dev/null || echo 0)))
  if [ "$lock_age" -gt 60 ]; then
    log "Pre-flight: removing stale .git/index.lock (age ${lock_age}s)"
    rm -f .git/index.lock
  else
    log "Pre-flight: .git/index.lock exists (${lock_age}s) — another git may be running, not removing"
  fi
fi

# ─── Helpers ──────────────────────────────────────────────────────────────

banned_terms_grep() {
  # Returns 0 (success) if NO banned terms found in source files
  # SCOPE FIX 2026-05-22: previously scanned the FULL diff which caused
  # false positives on plans/blockers-log.md and plans/decisions-log.md
  # (which themselves contain literal banned-term mentions because they
  # log past banned-term events). Now scopes strictly to app/components/
  # public/lib per AGENTS.md.
  local files
  files=$(git diff --cached --name-only 2>/dev/null)
  if [ -z "$files" ]; then
    files=$(git diff --name-only 2>/dev/null)
  fi
  if [ -z "$files" ]; then
    return 0
  fi
  # Filter to AGENTS.md source paths only — operational logs are out of scope.
  local src_files
  src_files=$(echo "$files" | grep -E "^(app|components|public|lib)/" || true)
  if [ -z "$src_files" ]; then
    return 0
  fi
  local match
  match=$(echo "$src_files" | xargs -I{} grep -liE "humanizer|bypass|undetect|detector|evade" "{}" 2>/dev/null | head -3)
  if [ -n "$match" ]; then
    log "  banned terms found in: $match"
    return 1
  fi
  return 0
}

find_next_pending_issue() {
  # Outputs: issue_id|github_number|title
  # Pick lowest M-number, lowest id, status=pending
  awk -F'|' '
    /^\| M[0-9.]+-[0-9]+ \| / {
      gsub(/^[[:space:]]+|[[:space:]]+$/, "", $2)  # id
      gsub(/^[[:space:]]+|[[:space:]]+$/, "", $3)  # milestone
      gsub(/^[[:space:]]+|[[:space:]]+$/, "", $4)  # title
      gsub(/^[[:space:]]+|[[:space:]]+$/, "", $5)  # url
      gsub(/^[[:space:]]+|[[:space:]]+$/, "", $6)  # status
      if ($6 == "pending") {
        # Extract issue number from URL
        url = $5
        n = url
        sub(/^.*\//, "", n)
        printf "%s|%s|%s|%s\n", $2, n, $4, $3
      }
    }
  ' "$BOARD" | sort -t'|' -k1,1V | head -1
}

update_board_status() {
  local id=$1
  local new_status=$2
  # Replace status in the row matching id
  # macOS sed: -i ''
  sed -i.bak -E "s~^(\| ${id} \|[^|]*\|[^|]*\|[^|]*\| )([^|]*)( \|)~\1${new_status}\3~" "$BOARD"
  rm -f "${BOARD}.bak"
}

classify_needs_human_status() {
  local summary_lc
  summary_lc=$(printf "%s" "$1" | tr '[:upper:]' '[:lower:]')

  if printf "%s" "$summary_lc" | grep -Eq "timeout_or_network|timeout|rate[- ]?limit|429|502|503|provider|sapling|openai|deepseek|network"; then
    echo "BLOCKED-PROVIDER"
    return
  fi

  if printf "%s" "$summary_lc" | grep -Eq "gh cli not authenticated|github cli|npm_token|token|secret|credential|real money|refund|stripe live|dashboard|manual login|user approval|user decision|missing .*key|api key"; then
    echo "BLOCKED-WAITING-USER"
    return
  fi

  echo "BLOCKED-AUTONOMY"
}

write_status_template_for_codex() {
  local id=$1
  local current_task=$2
  cp "$current_task" plans/current-task.md
  rm -f plans/task-status.json
}

run_codex_implementation() {
  # $1 = issue id (for logging)
  local id=$1
  log "  Calling codex exec (timeout ${CODEX_TIMEOUT_SECONDS}s)..."

  local cdx_log
  cdx_log="plans/codex-exec-${id}.log"

  # Use codex exec with timeout. Feed the implementation prompt directive via stdin.
  # gtimeout (homebrew coreutils) or built-in timeout — try gtimeout first.
  local TIMEOUT_BIN
  if command -v gtimeout >/dev/null 2>&1; then
    TIMEOUT_BIN=gtimeout
  elif command -v timeout >/dev/null 2>&1; then
    TIMEOUT_BIN=timeout
  else
    TIMEOUT_BIN=""
  fi

  local CODEX_PROMPT
  CODEX_PROMPT="Read plans/codex-implementation-prompt.md for your protocol. Read plans/current-task.md for the specific issue. Implement it. Write plans/task-status.json. DO NOT use git or gh. DO NOT modify .env.local."

  if [ -n "$TIMEOUT_BIN" ]; then
    echo "$CODEX_PROMPT" | "$TIMEOUT_BIN" "$CODEX_TIMEOUT_SECONDS" \
      codex exec \
        -c 'approval_policy="never"' \
        -c 'sandbox_mode="workspace-write"' \
        -c 'shell_environment_policy.inherit="all"' \
        > "$cdx_log" 2>&1
  else
    echo "$CODEX_PROMPT" | codex exec \
      -c 'approval_policy="never"' \
      -c 'sandbox_mode="workspace-write"' \
      -c 'shell_environment_policy.inherit="all"' \
      > "$cdx_log" 2>&1
  fi
  local exit_code=$?

  log "  Codex exec exit: $exit_code (log: $cdx_log)"
  if [ ! -f plans/task-status.json ]; then
    log "  WARN: codex did not write plans/task-status.json"
    return 99
  fi

  return $exit_code
}

parse_status_field() {
  # Crude JSON field extractor (avoid jq dependency)
  local field=$1
  python3 -c "import json,sys; print(json.load(open('plans/task-status.json')).get('$field',''))" 2>/dev/null
}

worktree_has_changes() {
  ! git diff --quiet 2>/dev/null || \
    ! git diff --cached --quiet 2>/dev/null || \
    [ -n "$(git ls-files --others --exclude-standard 2>/dev/null)" ]
}

stash_dirty_worktree() {
  local label=$1
  if worktree_has_changes; then
    log "  Preserving dirty worktree in stash ($label)"
    git stash push -u -m "overnight-preserve-${label}-$(date +%s)" >>"$LOG" 2>&1
  fi
}

verify_status_declares_all_changes() {
  python3 - <<'PY'
from __future__ import annotations

import json
import subprocess
import sys
from pathlib import Path

status_path = Path("plans/task-status.json")
try:
    declared = set(json.loads(status_path.read_text()).get("files_changed", []))
except Exception as exc:
    print(f"could not read files_changed from {status_path}: {exc}")
    raise SystemExit(1)

if not declared:
    print("plans/task-status.json files_changed is empty")
    raise SystemExit(1)

result = subprocess.run(
    ["git", "status", "--porcelain=v1", "-uall"],
    check=True,
    text=True,
    stdout=subprocess.PIPE,
)

actual: set[str] = set()
for raw in result.stdout.splitlines():
    if not raw:
        continue
    path = raw[3:]
    if " -> " in path:
        path = path.split(" -> ", 1)[1]
    actual.add(path)

extra = sorted(actual - declared)
if extra:
    print("\n".join(extra))
    raise SystemExit(1)
PY
}

repair_meta_field() {
  local field=$1
  python3 -c "import json; print(json.load(open('plans/current-repair-meta.json')).get('$field',''))" 2>/dev/null
}

has_pending_repair_item() {
  python3 - "$INBOX" <<'PY'
from __future__ import annotations

import re
import sys
from pathlib import Path

inbox = Path(sys.argv[1])
if not inbox.exists():
    raise SystemExit(1)

text = inbox.read_text()
headers = list(re.finditer(r"^## .+$", text, re.MULTILINE))
for i, match in enumerate(headers):
    start = match.start()
    end = headers[i + 1].start() if i + 1 < len(headers) else len(text)
    block = text[start:end]
    if re.search(r"^- Status:\s*pending\s*$", block, re.MULTILINE):
        raise SystemExit(0)

raise SystemExit(1)
PY
}

prepare_next_pending_repair_task() {
  python3 - "$INBOX" <<'PY'
from __future__ import annotations

import json
import re
import sys
from datetime import datetime
from pathlib import Path

inbox = Path(sys.argv[1])
if not inbox.exists():
    raise SystemExit(1)

text = inbox.read_text()
headers = list(re.finditer(r"^## .+$", text, re.MULTILINE))
for i, match in enumerate(headers):
    start = match.start()
    end = headers[i + 1].start() if i + 1 < len(headers) else len(text)
    block = text[start:end].strip()
    if not re.search(r"^- Status:\s*pending\s*$", block, re.MULTILINE):
        continue

    header = match.group(0).strip()
    title = header.split(" — ", 1)[-1].strip("# ").strip()
    slug = re.sub(r"[^a-z0-9]+", "-", title.lower()).strip("-")[:48] or "repair"
    repair_id = f"REPAIR-{datetime.now().strftime('%Y%m%d%H%M%S')}"

    updated_block = re.sub(
        r"^- Status:\s*pending\s*$",
        "- Status: in_progress",
        block,
        count=1,
        flags=re.MULTILINE,
    )
    text = text[:start] + updated_block + "\n\n" + text[end:].lstrip()
    inbox.write_text(text.rstrip() + "\n")

    Path("plans/current-repair-meta.json").write_text(
        json.dumps(
            {
                "id": repair_id,
                "title": title,
                "header": header,
                "slug": slug,
            },
            indent=2,
        )
        + "\n"
    )

    Path("plans/current-task.md").write_text(
        f"""# Repair {repair_id}

Title: {title}
Source: plans/codex-worker-inbox.md

## Repair item

{block}

## Repository conventions

- This is a non-user blocker repair, not a product-scope expansion.
- Keep the fix scoped to the inbox item.
- Do not use git or gh. The shell supervisor owns branch, commit, push, PR, CI, and merge.
- Do not modify .env.local, .dev.vars, provider dashboards, Stripe live money, npm publish state, or secrets.
- Write plans/task-status.json using the normal Codex implementation schema.
"""
    )
    raise SystemExit(0)

raise SystemExit(1)
PY
}

update_repair_item_status() {
  local header=$1
  local status=$2
  local note=$3

  python3 - "$INBOX" "$header" "$status" "$note" <<'PY'
from __future__ import annotations

import re
import sys
from datetime import datetime, timezone
from pathlib import Path

inbox = Path(sys.argv[1])
header, status, note = sys.argv[2:5]
text = inbox.read_text()
headers = list(re.finditer(r"^## .+$", text, re.MULTILINE))

for i, match in enumerate(headers):
    if match.group(0).strip() != header.strip():
        continue
    start = match.start()
    end = headers[i + 1].start() if i + 1 < len(headers) else len(text)
    block = text[start:end].rstrip()
    block = re.sub(
        r"^- Status:\s*\S+.*$",
        f"- Status: {status}",
        block,
        count=1,
        flags=re.MULTILINE,
    )
    timestamp = datetime.now(timezone.utc).astimezone().isoformat(timespec="seconds")
    block += f"\n- Worker evidence: {timestamp} — {note}"
    text = text[:start] + block + "\n\n" + text[end:].lstrip()
    inbox.write_text(text.rstrip() + "\n")
    raise SystemExit(0)

raise SystemExit(1)
PY
}

process_repair_inbox_once() {
  if ! has_pending_repair_item; then
    return 1
  fi

  local id title header slug branch cdx_exit next_action lint tc tests summary commit_title pr_url checks state_summary

  log ""
  log "─── Repair queue item detected"

  stash_dirty_worktree "pre-repair-inbox" || {
    log "  ERROR: could not preserve dirty worktree before repair"
    append_blocker "repair-inbox" "dirty-worktree-stash-failed" "A pending repair item exists but the supervisor could not stash pre-existing work before claiming it."
    REPAIRS_BLOCKED=$((REPAIRS_BLOCKED + 1))
    ISSUES_PROCESSED=$((ISSUES_PROCESSED + 1))
    return 0
  }

  log "  git checkout main && git pull"
  if ! git checkout main >>"$LOG" 2>&1; then
    log "  ERROR: could not checkout main for repair"
    append_blocker "repair-inbox" "git-checkout-main-failed" "A pending repair item exists but the supervisor could not checkout main before claiming it."
    REPAIRS_BLOCKED=$((REPAIRS_BLOCKED + 1))
    ISSUES_PROCESSED=$((ISSUES_PROCESSED + 1))
    return 0
  fi
  git pull --ff-only >>"$LOG" 2>&1 || log "  WARN: git pull failed before repair"
  stash_dirty_worktree "post-repair-main-checkout" || {
    log "  ERROR: could not preserve dirty worktree after checkout main for repair"
    append_blocker "repair-inbox" "dirty-worktree-stash-failed" "The supervisor reached main for a repair item but found dirty files and could not preserve them."
    REPAIRS_BLOCKED=$((REPAIRS_BLOCKED + 1))
    ISSUES_PROCESSED=$((ISSUES_PROCESSED + 1))
    return 0
  }

  if ! prepare_next_pending_repair_task; then
    log "  WARN: repair item disappeared before task preparation"
    return 1
  fi

  id=$(repair_meta_field id)
  title=$(repair_meta_field title)
  header=$(repair_meta_field header)
  slug=$(repair_meta_field slug)
  branch="codex/repair-${slug}-${id}"

  log "  Repair $id: $title"
  append_decision "$id | repair-started | $title"

  if git show-ref --verify --quiet "refs/heads/$branch"; then
    log "  branch $branch exists locally — deleting for fresh repair"
    git branch -D "$branch" >>"$LOG" 2>&1 || true
  fi

  log "  git checkout -b $branch"
  if ! git checkout -b "$branch" >>"$LOG" 2>&1; then
    log "  ERROR: could not create repair branch $branch"
    update_repair_item_status "$header" "not_actionable" "could not create repair branch $branch"
    REPAIRS_BLOCKED=$((REPAIRS_BLOCKED + 1))
    ISSUES_PROCESSED=$((ISSUES_PROCESSED + 1))
    return 0
  fi

  rm -f plans/task-status.json
  run_codex_implementation "$id"
  cdx_exit=$?

  if [ ! -f plans/task-status.json ]; then
    log "  ERROR: repair codex did not produce task-status.json (exit=$cdx_exit)"
    update_repair_item_status "$header" "not_actionable" "codex-no-status during repair; log plans/codex-exec-${id}.log"
    git stash push -u -m "repair-no-status-${id}-$(date +%s)" >>"$LOG" 2>&1 || true
    git checkout main >>"$LOG" 2>&1
    git branch -D "$branch" >>"$LOG" 2>&1 || true
    REPAIRS_BLOCKED=$((REPAIRS_BLOCKED + 1))
    ISSUES_PROCESSED=$((ISSUES_PROCESSED + 1))
    return 0
  fi

  next_action=$(parse_status_field next_action)
  lint=$(parse_status_field lint)
  tc=$(parse_status_field typecheck)
  tests=$(parse_status_field tests)
  summary=$(parse_status_field summary)
  commit_title=$(parse_status_field title)
  log "  Repair Codex status: next_action=$next_action lint=$lint typecheck=$tc tests=$tests"

  case "$next_action" in
    ready_to_commit)
      ;;
    needs_human)
      log "  Repair needs human or broader engineering: $summary"
      local repair_block_status
      repair_block_status=$(classify_needs_human_status "$summary")
      if [ "$repair_block_status" = "BLOCKED-WAITING-USER" ]; then
        update_repair_item_status "$header" "waiting_user" "$summary"
      else
        update_repair_item_status "$header" "not_actionable" "$summary"
      fi
      stash_dirty_worktree "repair-needs-human-${id}" >>"$LOG" 2>&1 || true
      git checkout main >>"$LOG" 2>&1
      REPAIRS_BLOCKED=$((REPAIRS_BLOCKED + 1))
      ISSUES_PROCESSED=$((ISSUES_PROCESSED + 1))
      return 0
      ;;
    abort|*)
      log "  Repair aborted or unknown next_action: $next_action ($summary)"
      update_repair_item_status "$header" "not_actionable" "$summary"
      stash_dirty_worktree "repair-abort-${id}" >>"$LOG" 2>&1 || true
      git checkout main >>"$LOG" 2>&1
      git branch -D "$branch" >>"$LOG" 2>&1 || true
      REPAIRS_BLOCKED=$((REPAIRS_BLOCKED + 1))
      ISSUES_PROCESSED=$((ISSUES_PROCESSED + 1))
      return 0
      ;;
  esac

  if ! banned_terms_grep; then
    log "  Banned term found in repair — stashing and blocking"
    git stash push -u -m "repair-${id}-$(date +%s)" >>"$LOG" 2>&1 || true
    git checkout main >>"$LOG" 2>&1
    git branch -D "$branch" >>"$LOG" 2>&1 || true
    update_repair_item_status "$header" "not_actionable" "banned term found in repair diff; changes stashed"
    REPAIRS_BLOCKED=$((REPAIRS_BLOCKED + 1))
    ISSUES_PROCESSED=$((ISSUES_PROCESSED + 1))
    return 0
  fi

  if ! verify_status_declares_all_changes >>"$LOG" 2>&1; then
    log "  ERROR: repair changed files outside plans/task-status.json files_changed; preserving work and blocking"
    update_repair_item_status "$header" "not_actionable" "repair changed files outside declared files_changed list; changes stashed for split/review"
    stash_dirty_worktree "repair-undeclared-files-${id}" >>"$LOG" 2>&1 || true
    git checkout main >>"$LOG" 2>&1
    git branch -D "$branch" >>"$LOG" 2>&1 || true
    REPAIRS_BLOCKED=$((REPAIRS_BLOCKED + 1))
    ISSUES_PROCESSED=$((ISSUES_PROCESSED + 1))
    return 0
  fi

  log "  git add . && commit repair"
  git add -A
  if git diff --cached --quiet; then
    log "  WARN: repair produced no staged changes"
    update_repair_item_status "$header" "not_actionable" "Codex reported ready_to_commit but produced no changes"
    git checkout main >>"$LOG" 2>&1
    git branch -D "$branch" >>"$LOG" 2>&1 || true
    REPAIRS_BLOCKED=$((REPAIRS_BLOCKED + 1))
    ISSUES_PROCESSED=$((ISSUES_PROCESSED + 1))
    return 0
  fi

  if ! git commit -m "${commit_title:-repair: ${title}}

${summary}" >>"$LOG" 2>&1; then
    log "  ERROR: repair commit failed"
    update_repair_item_status "$header" "not_actionable" "repair commit failed"
    git checkout main >>"$LOG" 2>&1
    git branch -D "$branch" >>"$LOG" 2>&1 || true
    REPAIRS_BLOCKED=$((REPAIRS_BLOCKED + 1))
    ISSUES_PROCESSED=$((ISSUES_PROCESSED + 1))
    return 0
  fi

  log "  git push -u origin $branch"
  if ! git push -u origin "$branch" >>"$LOG" 2>&1; then
    log "  ERROR: repair push failed"
    update_repair_item_status "$header" "not_actionable" "push to $branch failed"
    git checkout main >>"$LOG" 2>&1
    REPAIRS_BLOCKED=$((REPAIRS_BLOCKED + 1))
    ISSUES_PROCESSED=$((ISSUES_PROCESSED + 1))
    return 0
  fi

  log "  gh pr create for repair"
  if pr_url=$(gh pr create --base main --head "$branch" \
    --title "${commit_title:-repair: ${title}}" \
    --body "Repair queue item: ${title}. ${summary}" 2>&1 | tail -1); then
    log "  Repair PR: $pr_url"
  else
    log "  ERROR: repair PR create failed: $pr_url"
    update_repair_item_status "$header" "not_actionable" "PR create failed for $branch: $pr_url"
    git checkout main >>"$LOG" 2>&1
    REPAIRS_BLOCKED=$((REPAIRS_BLOCKED + 1))
    ISSUES_PROCESSED=$((ISSUES_PROCESSED + 1))
    return 0
  fi

  log "  Waiting for CI on repair $pr_url"
  state_summary=""
  for i in $(seq 1 18); do
    sleep 10
    checks=$(gh pr checks "$pr_url" --json state,name 2>/dev/null || echo "[]")
    state_summary=$(echo "$checks" | python3 -c "import json,sys; d=json.load(sys.stdin); states=[c['state'] for c in d]; print(','.join(set(states)) or 'no-checks')" 2>/dev/null)
    log "  Repair CI status (${i}/18): $state_summary"
    if echo "$state_summary" | grep -q "FAILURE\|ERROR\|CANCELLED"; then
      break
    fi
    if echo "$state_summary" | grep -qv "PENDING\|IN_PROGRESS\|QUEUED" && [ -n "$state_summary" ] && [ "$state_summary" != "no-checks" ]; then
      break
    fi
  done

  if echo "$state_summary" | grep -qE "FAILURE|ERROR|CANCELLED"; then
    log "  Repair CI failed — leaving PR open"
    update_repair_item_status "$header" "not_actionable" "repair PR $pr_url CI failed"
    git checkout main >>"$LOG" 2>&1
    REPAIRS_BLOCKED=$((REPAIRS_BLOCKED + 1))
    ISSUES_PROCESSED=$((ISSUES_PROCESSED + 1))
    return 0
  fi

  log "  gh pr merge repair --squash --delete-branch"
  if gh pr merge "$pr_url" --squash --delete-branch >>"$LOG" 2>&1; then
    log "  Repair PR merged"
    update_repair_item_status "$header" "done" "merged $pr_url; ${summary}"
    append_decision "$id | repair-done | $summary (PR $pr_url)"
    REPAIRS_DONE=$((REPAIRS_DONE + 1))
  else
    log "  ERROR: repair PR merge failed"
    update_repair_item_status "$header" "not_actionable" "repair PR $pr_url merge failed"
    REPAIRS_BLOCKED=$((REPAIRS_BLOCKED + 1))
  fi

  git checkout main >>"$LOG" 2>&1
  ISSUES_PROCESSED=$((ISSUES_PROCESSED + 1))
  log "  Repair progress so far: $REPAIRS_DONE done, $REPAIRS_BLOCKED blocked"
  return 0
}

# ─── Main loop ────────────────────────────────────────────────────────────

while true; do
  # Stop signal check
  if [ -f "$STOP_SIGNAL" ]; then
    log "STOP signal detected at $STOP_SIGNAL — exiting cleanly"
    break
  fi

  # North-star money signal check
  if [ -f "$MONEY_SIGNAL" ]; then
    log "MONEY-MADE signal detected at $MONEY_SIGNAL — north star reached, exiting cleanly"
    break
  fi

  NOW=$(date +%s)
  if [ "$NOW" -gt "$DEADLINE" ]; then
    log "Reached $MAX_HOURS hour deadline — exiting"
    break
  fi

  if [ "$ISSUES_PROCESSED" -ge "$MAX_ISSUES" ]; then
    log "Reached $MAX_ISSUES issue cap — exiting"
    break
  fi

  # Repairs are first-class loop work. Claude writes the inbox; this shell loop
  # consumes it before choosing new product work so repair latency is one loop
  # iteration, not the separate Codex automation interval.
  if process_repair_inbox_once; then
    sleep "$COOLDOWN_SECONDS"
    continue
  fi

  # Find next pending issue
  NEXT=$(find_next_pending_issue)
  if [ -z "$NEXT" ]; then
    log "No more pending issues — exiting"
    break
  fi

  IFS='|' read -r ID GH_NUM TITLE MS <<< "$NEXT"
  log ""
  log "─── Issue $ID (#$GH_NUM, $MS): $TITLE"

  # Skip clusters the user has repeatedly deferred to daytime supervised work
  # (see plans/overnight-progress.md decisions across 2026-05-21 triggers).
  case "$ID" in
    M7-001|M9-006)
      log "  Skipping $ID (user-only: real money / npm publish)"
      update_board_status "$ID" "BLOCKED-WAITING-USER"
      append_decision "$ID | blocked | user-only milestone, skipped autonomously"
      ISSUES_BLOCKED=$((ISSUES_BLOCKED + 1))
      ISSUES_PROCESSED=$((ISSUES_PROCESSED + 1))
      sleep "$COOLDOWN_SECONDS"
      continue
      ;;
    M1-002|M1-003|M1-004|M1-005|M1-006|M1-007|M1-008|M1-009|M1-010)
      log "  Skipping $ID (Entra auth migration cluster — daytime only)"
      update_board_status "$ID" "BLOCKED-AUTONOMY"
      append_decision "$ID | blocked-autonomy | Entra cluster deferred to supervised implementation, not a user blocker"
      ISSUES_BLOCKED=$((ISSUES_BLOCKED + 1))
      ISSUES_PROCESSED=$((ISSUES_PROCESSED + 1))
      sleep "$COOLDOWN_SECONDS"
      continue
      ;;
    M2-001|M2-002|M2-003|M2-004|M2-005|M2-006|M2-008|M2-009)
      log "  Skipping $ID (quality gate cluster — needs eval baseline)"
      update_board_status "$ID" "BLOCKED-PREREQ"
      append_decision "$ID | blocked-prereq | quality gate cluster needs baseline/eval prerequisite, not a user blocker"
      ISSUES_BLOCKED=$((ISSUES_BLOCKED + 1))
      ISSUES_PROCESSED=$((ISSUES_PROCESSED + 1))
      sleep "$COOLDOWN_SECONDS"
      continue
      ;;
    M3-001|M3-002|M3-003|M3-004|M3-005|M3-006|M3-007|M3-008)
      log "  Skipping $ID (V2 layout cascade — typed refactor across lib)"
      update_board_status "$ID" "BLOCKED-AUTONOMY"
      append_decision "$ID | blocked-autonomy | V2 cascade deferred to supervised implementation, not a user blocker"
      ISSUES_BLOCKED=$((ISSUES_BLOCKED + 1))
      ISSUES_PROCESSED=$((ISSUES_PROCESSED + 1))
      sleep "$COOLDOWN_SECONDS"
      continue
      ;;
    M4-011)
      log "  Skipping $ID (frontend redesign spans multiple surfaces and exceeded the Codex timebox)"
      update_board_status "$ID" "BLOCKED-AUTONOMY"
      append_decision "$ID | blocked-autonomy | full frontend redesign exceeded the 600s Codex timebox; split via plans/frontend-redesign-followups.md before retry"
      ISSUES_BLOCKED=$((ISSUES_BLOCKED + 1))
      ISSUES_PROCESSED=$((ISSUES_PROCESSED + 1))
      sleep "$COOLDOWN_SECONDS"
      continue
      ;;
    M8-002|M8-003|M8-004|M8-005|M8-006|M8-007|M8-008|M8-009|M8-010|M8-011|M8-012|M8-013|M8-014|M8-015|M8-016)
      log "  Skipping $ID (B2B API chain — depends on M8-001 + Azure SQL routing)"
      update_board_status "$ID" "BLOCKED-PREREQ"
      append_decision "$ID | blocked-prereq | M8 chain needs M8-001 merged, not a user blocker"
      ISSUES_BLOCKED=$((ISSUES_BLOCKED + 1))
      ISSUES_PROCESSED=$((ISSUES_PROCESSED + 1))
      sleep "$COOLDOWN_SECONDS"
      continue
      ;;
  esac

  # ─── Git: branch from main ────────────────────────────────────────────
  stash_dirty_worktree "pre-issue-${ID}" || {
    log "  ERROR: could not preserve dirty worktree before issue start"
    update_board_status "$ID" "BLOCKED"
    append_blocker "$ID" "dirty-worktree-stash-failed" "Could not stash pre-existing work before starting $ID."
    append_repair_inbox_item \
      "$ID dirty-worktree-stash-failed" \
      "dirty_repo" \
      "P1" \
      "$ID" \
      "$LOG" \
      "Preserve or classify the dirty worktree state so the supervisor can start issues from a clean tree." \
      "The worktree is clean on main or all pre-existing work is safely preserved."
    ISSUES_BLOCKED=$((ISSUES_BLOCKED + 1))
    ISSUES_PROCESSED=$((ISSUES_PROCESSED + 1))
    sleep "$COOLDOWN_SECONDS"
    continue
  }

  log "  git checkout main && git pull"
  if ! git checkout main >>"$LOG" 2>&1; then
    log "  ERROR: could not checkout main"
    update_board_status "$ID" "BLOCKED"
    append_blocker "$ID" "git-checkout-main-failed" "checkout to main failed"
    append_repair_inbox_item \
      "$ID git-checkout-main-failed" \
      "dirty_repo" \
      "P1" \
      "$ID" \
      "$LOG" \
      "Diagnose the dirty repo, stale lock, or branch state that prevented checkout to main; preserve work and restore the loop to a clean executable state." \
      "The supervisor can checkout main and continue without losing user or loop work."
    ISSUES_BLOCKED=$((ISSUES_BLOCKED + 1))
    ISSUES_PROCESSED=$((ISSUES_PROCESSED + 1))
    sleep "$COOLDOWN_SECONDS"
    continue
  fi
  git pull --ff-only >>"$LOG" 2>&1 || log "  WARN: git pull failed (probably no network or remote ahead)"
  stash_dirty_worktree "post-main-checkout-${ID}" || {
    log "  ERROR: could not preserve dirty worktree after checkout main"
    update_board_status "$ID" "BLOCKED"
    append_blocker "$ID" "dirty-worktree-stash-failed" "The supervisor reached main for $ID but found dirty files and could not preserve them."
    append_repair_inbox_item \
      "$ID dirty-worktree-stash-failed" \
      "dirty_repo" \
      "P1" \
      "$ID" \
      "$LOG" \
      "Preserve or classify dirty files left after checkout main before launching Codex." \
      "The supervisor launches Codex only from a clean issue branch."
    ISSUES_BLOCKED=$((ISSUES_BLOCKED + 1))
    ISSUES_PROCESSED=$((ISSUES_PROCESSED + 1))
    sleep "$COOLDOWN_SECONDS"
    continue
  }

  BRANCH="chore/${ID}"
  # If branch already exists, delete it (clean slate)
  if git show-ref --verify --quiet "refs/heads/$BRANCH"; then
    log "  branch $BRANCH exists locally — deleting for fresh start"
    git branch -D "$BRANCH" >>"$LOG" 2>&1 || true
  fi
  log "  git checkout -b $BRANCH"
  if ! git checkout -b "$BRANCH" >>"$LOG" 2>&1; then
    log "  ERROR: could not create branch $BRANCH"
    update_board_status "$ID" "BLOCKED"
    append_blocker "$ID" "git-branch-failed" "creating branch $BRANCH failed"
    append_repair_inbox_item \
      "$ID git-branch-failed" \
      "dirty_repo" \
      "P1" \
      "$ID" \
      "$LOG" \
      "Diagnose why the supervisor could not create $BRANCH and restore branch creation for the loop." \
      "The supervisor can create a fresh issue branch from main or the issue is reclassified with concrete evidence."
    ISSUES_BLOCKED=$((ISSUES_BLOCKED + 1))
    ISSUES_PROCESSED=$((ISSUES_PROCESSED + 1))
    sleep "$COOLDOWN_SECONDS"
    continue
  fi

  # Build current-task.md from manifest or detailed brief on the issue branch.
  CURRENT_TASK=""
  if [ -f "plans/issues/${ID}.md" ]; then
    CURRENT_TASK="plans/issues/${ID}.md"
  else
    # Need to extract from manifest — fall back to a minimal task description
    log "  No detailed brief — using manifest entry"
    CURRENT_TASK=plans/current-task.md
    {
      echo "# Issue $ID"
      echo ""
      echo "Title: $TITLE"
      echo "Milestone: $MS"
      echo "GitHub: https://github.com/ChuanQiao1128/replyinmyvoice/issues/$GH_NUM"
      echo ""
      echo "## Brief"
      echo ""
      gh issue view "$GH_NUM" --json body --jq .body 2>/dev/null || echo "(could not fetch GitHub issue body)"
      echo ""
      echo "## Repository conventions"
      echo "- Tests: vitest for TypeScript, xunit for .NET"
      echo "- Lint: eslint via npm run lint"
      echo "- Types: tsc via npm run typecheck"
      echo "- Commits: conventional commits (feat:, fix:, chore:, docs:, test:)"
      echo "- See CLAUDE.md 'Active Commercialization Sprint' for sprint posture"
    } > plans/current-task.md
    CURRENT_TASK=plans/current-task.md
  fi

  # If using existing detailed brief, also copy to plans/current-task.md so codex has one path
  if [ "$CURRENT_TASK" != plans/current-task.md ]; then
    cp "$CURRENT_TASK" plans/current-task.md
  fi

  # Mark in_progress after branch creation so board/current-task edits cannot
  # leak across issue branches.
  update_board_status "$ID" "in_progress"
  append_decision "$ID | started | $TITLE"

  # ─── Codex: file edits + tests ────────────────────────────────────────
  rm -f plans/task-status.json
  run_codex_implementation "$ID"
  cdx_exit=$?

  if [ ! -f plans/task-status.json ]; then
    log "  ERROR: codex did not produce task-status.json (exit=$cdx_exit)"
    update_board_status "$ID" "BLOCKED"
    append_blocker "$ID" "codex-no-status" "codex exec did not write plans/task-status.json. Log: plans/codex-exec-${ID}.log"
    append_repair_inbox_item \
      "$ID codex-no-status" \
      "autonomy" \
      "P1" \
      "$ID" \
      "plans/codex-exec-${ID}.log" \
      "Investigate why Codex did not write plans/task-status.json for $ID; fix the loop prompt/task contract or requeue the issue with evidence." \
      "The supervisor can run the issue again and receive a valid plans/task-status.json, or the issue is reclassified with a concrete non-user blocker."
    git stash push -u -m "no-status-${ID}-$(date +%s)" >>"$LOG" 2>&1 || true
    git checkout main >>"$LOG" 2>&1
    git branch -D "$BRANCH" >>"$LOG" 2>&1 || true
    ISSUES_BLOCKED=$((ISSUES_BLOCKED + 1))
    ISSUES_PROCESSED=$((ISSUES_PROCESSED + 1))
    sleep "$COOLDOWN_SECONDS"
    continue
  fi

  NEXT_ACTION=$(parse_status_field next_action)
  LINT=$(parse_status_field lint)
  TC=$(parse_status_field typecheck)
  TESTS=$(parse_status_field tests)
  SUMMARY=$(parse_status_field summary)
  COMMIT_TITLE=$(parse_status_field title)
  log "  Codex status: next_action=$NEXT_ACTION lint=$LINT typecheck=$TC tests=$TESTS"

  case "$NEXT_ACTION" in
    ready_to_commit)
      # Continue to git path below
      ;;
    needs_human)
      log "  Codex flagged needs_human: $SUMMARY"
      BLOCK_STATUS=$(classify_needs_human_status "$SUMMARY")
      update_board_status "$ID" "$BLOCK_STATUS"
      append_blocker "$ID" "codex-needs-human:${BLOCK_STATUS}" "$SUMMARY"
      if [ "$BLOCK_STATUS" != "BLOCKED-WAITING-USER" ]; then
        append_repair_inbox_item \
          "$ID codex-needs-human:${BLOCK_STATUS}" \
          "autonomy" \
          "P1" \
          "$ID" \
          "plans/task-status.json" \
          "Resolve or narrow the non-user blocker Codex reported for $ID without changing live money, dashboards, npm publish state, or secrets." \
          "The issue can proceed autonomously again, or a scoped follow-up row/PR documents the exact engineering prerequisite."
      fi
      stash_dirty_worktree "needs-human-${ID}" >>"$LOG" 2>&1 || true
      git checkout main >>"$LOG" 2>&1
      # Keep branch for human review
      ISSUES_NEEDS_HUMAN=$((ISSUES_NEEDS_HUMAN + 1))
      ISSUES_PROCESSED=$((ISSUES_PROCESSED + 1))
      sleep "$COOLDOWN_SECONDS"
      continue
      ;;
    abort|*)
      log "  Codex aborted or unknown next_action: $NEXT_ACTION ($SUMMARY)"
      update_board_status "$ID" "BLOCKED"
      append_blocker "$ID" "codex-aborted" "$SUMMARY"
      append_repair_inbox_item \
        "$ID codex-aborted" \
        "autonomy" \
        "P1" \
        "$ID" \
        "plans/codex-exec-${ID}.log" \
        "Diagnose the Codex abort for $ID and either fix the task contract or split/reclassify the issue so the loop can continue." \
        "The supervisor can retry the issue with a clear task status, or the inbox item records why it is not actionable."
      stash_dirty_worktree "abort-${ID}" >>"$LOG" 2>&1 || true
      git checkout main >>"$LOG" 2>&1
      git branch -D "$BRANCH" >>"$LOG" 2>&1 || true
      ISSUES_BLOCKED=$((ISSUES_BLOCKED + 1))
      ISSUES_PROCESSED=$((ISSUES_PROCESSED + 1))
      sleep "$COOLDOWN_SECONDS"
      continue
      ;;
  esac

  # ─── Git: banned-term scan + add + commit + push ───────────────────────
  # Run banned-term scan against the diff
  if ! banned_terms_grep; then
    log "  Banned term found — reverting and blocking"
    # SAFE REVERT 2026-05-22: previously used `git checkout . && git clean -fd`
    # which wiped untracked files including plans/STOP-OVERNIGHT.txt.
    # Now we stash with -u so the work is preserved for forensic review.
    git stash push -u -m "revert-${ID}-$(date +%s)" >>"$LOG" 2>&1 || true
    git checkout main >>"$LOG" 2>&1
    git branch -D "$BRANCH" >>"$LOG" 2>&1 || true
    update_board_status "$ID" "BLOCKED"
    append_blocker "$ID" "banned-term-in-diff" "Diff contained one of: humanizer/bypass/undetect/detector/evade (stashed; run \\`git stash list\\` to inspect)"
    append_repair_inbox_item \
      "$ID banned-term-in-diff" \
      "product" \
      "P1" \
      "$ID" \
      "$LOG" \
      "Inspect the stashed diff and replace banned positioning with approved product language without changing user-only settings." \
      "The issue can be retried with no banned terms in scoped source paths."
    ISSUES_BLOCKED=$((ISSUES_BLOCKED + 1))
    ISSUES_PROCESSED=$((ISSUES_PROCESSED + 1))
    sleep "$COOLDOWN_SECONDS"
    continue
  fi

  if ! verify_status_declares_all_changes >>"$LOG" 2>&1; then
    log "  ERROR: changed files outside plans/task-status.json files_changed; preserving work and blocking"
    update_board_status "$ID" "BLOCKED"
    append_blocker "$ID" "undeclared-files-in-diff" "Codex reported ready_to_commit but the dirty worktree included files not declared in plans/task-status.json files_changed. Changes were stashed for split/review."
    append_repair_inbox_item \
      "$ID undeclared-files-in-diff" \
      "dirty_repo" \
      "P1" \
      "$ID" \
      "plans/task-status.json" \
      "Inspect the preserved stash, split unrelated work into scoped branches, and restore the supervisor to clean-branch operation." \
      "No PR commits files outside the Codex-declared files_changed list."
    stash_dirty_worktree "undeclared-files-${ID}" >>"$LOG" 2>&1 || true
    git checkout main >>"$LOG" 2>&1
    git branch -D "$BRANCH" >>"$LOG" 2>&1 || true
    ISSUES_BLOCKED=$((ISSUES_BLOCKED + 1))
    ISSUES_PROCESSED=$((ISSUES_PROCESSED + 1))
    sleep "$COOLDOWN_SECONDS"
    continue
  fi

  log "  git add . && commit"
  git add -A
  if git diff --cached --quiet; then
    log "  WARN: no staged changes — codex did not modify any files"
    update_board_status "$ID" "BLOCKED"
    append_blocker "$ID" "codex-no-changes" "Codex reported ready_to_commit but no file changes detected"
    append_repair_inbox_item \
      "$ID codex-no-changes" \
      "autonomy" \
      "P2" \
      "$ID" \
      "plans/task-status.json" \
      "Inspect why Codex reported ready_to_commit with no diff for $ID; fix the task brief or mark the issue done/not-actionable with evidence." \
      "The issue status reflects reality and the loop no longer spends cycles on an empty implementation."
    git checkout main >>"$LOG" 2>&1
    git branch -D "$BRANCH" >>"$LOG" 2>&1 || true
    ISSUES_BLOCKED=$((ISSUES_BLOCKED + 1))
    ISSUES_PROCESSED=$((ISSUES_PROCESSED + 1))
    sleep "$COOLDOWN_SECONDS"
    continue
  fi

  COMMIT_MSG="${COMMIT_TITLE:-${ID}: ${TITLE}}

${SUMMARY}

Closes #${GH_NUM}"
  if ! git commit -m "$COMMIT_MSG" >>"$LOG" 2>&1; then
    log "  ERROR: git commit failed"
    update_board_status "$ID" "BLOCKED"
    append_blocker "$ID" "git-commit-failed" "$COMMIT_MSG"
    append_repair_inbox_item \
      "$ID git-commit-failed" \
      "dirty_repo" \
      "P1" \
      "$ID" \
      "$LOG" \
      "Diagnose the commit failure, preserve the implementation diff, and restore a commit-ready branch state." \
      "The implementation is committed or safely requeued with the reason recorded."
    git checkout main >>"$LOG" 2>&1
    git branch -D "$BRANCH" >>"$LOG" 2>&1 || true
    ISSUES_BLOCKED=$((ISSUES_BLOCKED + 1))
    ISSUES_PROCESSED=$((ISSUES_PROCESSED + 1))
    sleep "$COOLDOWN_SECONDS"
    continue
  fi

  log "  git push -u origin $BRANCH"
  if ! git push -u origin "$BRANCH" >>"$LOG" 2>&1; then
    log "  ERROR: git push failed"
    update_board_status "$ID" "BLOCKED"
    append_blocker "$ID" "git-push-failed" "push to $BRANCH failed — check network / credentials"
    append_repair_inbox_item \
      "$ID git-push-failed" \
      "provider" \
      "P1" \
      "$ID" \
      "$LOG" \
      "Inspect the push failure and retry or reclassify it without printing credentials or changing secrets." \
      "The branch is pushed or the blocker is classified as a true user credential/network action with evidence."
    git checkout main >>"$LOG" 2>&1
    ISSUES_BLOCKED=$((ISSUES_BLOCKED + 1))
    ISSUES_PROCESSED=$((ISSUES_PROCESSED + 1))
    sleep "$COOLDOWN_SECONDS"
    continue
  fi

  # ─── GitHub: PR create + auto-merge if green ───────────────────────────
  log "  gh pr create"
  PR_URL=""
  if PR_URL=$(gh pr create --base main --head "$BRANCH" \
    --title "${COMMIT_TITLE:-${ID}: ${TITLE}}" \
    --body "Closes #${GH_NUM}. ${SUMMARY}" 2>&1 | tail -1); then
    log "  PR: $PR_URL"
  else
    log "  ERROR: gh pr create failed: $PR_URL"
    update_board_status "$ID" "BLOCKED"
    append_blocker "$ID" "gh-pr-create-failed" "$PR_URL"
    append_repair_inbox_item \
      "$ID gh-pr-create-failed" \
      "provider" \
      "P1" \
      "$ID" \
      "$LOG" \
      "Inspect the GitHub PR creation failure and restore PR creation or reclassify the blocker with evidence." \
      "A PR exists for the branch or the blocker is classified as a true user credential/network action."
    git checkout main >>"$LOG" 2>&1
    ISSUES_BLOCKED=$((ISSUES_BLOCKED + 1))
    ISSUES_PROCESSED=$((ISSUES_PROCESSED + 1))
    sleep "$COOLDOWN_SECONDS"
    continue
  fi

  # Wait for CI up to 3 min
  log "  Waiting for CI on $PR_URL"
  for i in $(seq 1 18); do
    sleep 10
    CHECKS=$(gh pr checks "$PR_URL" --json state,name 2>/dev/null || echo "[]")
    STATE_SUMMARY=$(echo "$CHECKS" | python3 -c "import json,sys; d=json.load(sys.stdin); states=[c['state'] for c in d]; print(','.join(set(states)) or 'no-checks')" 2>/dev/null)
    log "  CI status (${i}/18): $STATE_SUMMARY"
    if echo "$STATE_SUMMARY" | grep -q "FAILURE\|ERROR\|CANCELLED"; then
      break
    fi
    if echo "$STATE_SUMMARY" | grep -qv "PENDING\|IN_PROGRESS\|QUEUED" && [ -n "$STATE_SUMMARY" ] && [ "$STATE_SUMMARY" != "no-checks" ]; then
      # No pending/in_progress remaining; all done
      break
    fi
  done

  if echo "$STATE_SUMMARY" | grep -qE "FAILURE|ERROR|CANCELLED"; then
    log "  CI failed — leaving PR open for review"
    update_board_status "$ID" "BLOCKED"
    append_blocker "$ID" "ci-failed" "PR $PR_URL CI failed. Check gh pr checks $PR_URL"
    append_repair_inbox_item \
      "$ID ci-failed" \
      "ci" \
      "P1" \
      "$ID" \
      "$PR_URL" \
      "Read the PR checks, identify the CI failure, and submit a scoped repair PR or update the original PR if safe." \
      "CI is green or the failure is reclassified with concrete evidence and no user-only action hidden inside it."
    git checkout main >>"$LOG" 2>&1
    ISSUES_BLOCKED=$((ISSUES_BLOCKED + 1))
    ISSUES_PROCESSED=$((ISSUES_PROCESSED + 1))
    sleep "$COOLDOWN_SECONDS"
    continue
  fi

  # ─── Merge + close issue ───────────────────────────────────────────────
  log "  gh pr merge --squash --delete-branch"
  if gh pr merge "$PR_URL" --squash --delete-branch >>"$LOG" 2>&1; then
    log "  PR merged"
    gh issue close "$GH_NUM" --comment "Implemented in $PR_URL" >>"$LOG" 2>&1 || log "  WARN: gh issue close failed (probably already closed by PR)"
    update_board_status "$ID" "done"
    append_decision "$ID | done | $SUMMARY (PR $PR_URL)"
    ISSUES_DONE=$((ISSUES_DONE + 1))
  else
    log "  ERROR: gh pr merge failed"
    update_board_status "$ID" "BLOCKED"
    append_blocker "$ID" "gh-pr-merge-failed" "PR $PR_URL exists but merge failed (branch protection? unresolved conversations?)"
    append_repair_inbox_item \
      "$ID gh-pr-merge-failed" \
      "ci" \
      "P1" \
      "$ID" \
      "$PR_URL" \
      "Inspect why the green PR could not merge and resolve branch/update/CI state if it is not a user-only review decision." \
      "The PR is merged, updated, or clearly classified as waiting on a real user review/approval."
    ISSUES_BLOCKED=$((ISSUES_BLOCKED + 1))
  fi

  git checkout main >>"$LOG" 2>&1
  ISSUES_PROCESSED=$((ISSUES_PROCESSED + 1))

  log "  Progress so far: $ISSUES_DONE done, $ISSUES_BLOCKED blocked, $ISSUES_NEEDS_HUMAN needs-human / $ISSUES_PROCESSED processed"
  sleep "$COOLDOWN_SECONDS"
done

# ─── Final report ─────────────────────────────────────────────────────────

log ""
log "=== Overnight Supervisor v2 finished ==="
log "Wall time: $(($(date +%s) - START_TS))s"
log "Issues processed: $ISSUES_PROCESSED"
log "  done:         $ISSUES_DONE"
log "  blocked:      $ISSUES_BLOCKED"
log "  needs human:  $ISSUES_NEEDS_HUMAN"
log "Repairs:"
log "  done:         $REPAIRS_DONE"
log "  blocked:      $REPAIRS_BLOCKED"

append_progress "Run finished. Done: $ISSUES_DONE | Blocked: $ISSUES_BLOCKED | Needs human: $ISSUES_NEEDS_HUMAN | Repairs done: $REPAIRS_DONE | Repairs blocked: $REPAIRS_BLOCKED"
