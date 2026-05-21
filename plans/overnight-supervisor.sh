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
#   nohup bash plans/overnight-supervisor.sh > /dev/null 2>&1 &
#   disown
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
STOP_SIGNAL=plans/STOP-OVERNIGHT.txt
MONEY_SIGNAL=plans/MONEY-MADE.txt

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

# ─── Pre-flight ───────────────────────────────────────────────────────────

log "=== Overnight Supervisor v2 starting ==="
log "Repo: $REPO_DIR"
log "Limits: $MAX_ISSUES issues, $MAX_HOURS hours, $CODEX_TIMEOUT_SECONDS s per codex call  (real stop: STOP-OVERNIGHT.txt | MONEY-MADE.txt | no-more-pending)"

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

if ! gh auth status >/dev/null 2>&1; then
  log "ERROR: gh CLI not authenticated"
  exit 1
fi

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
  # Returns 0 (success) if NO banned terms found in changed files only
  local files
  files=$(git diff --cached --name-only 2>/dev/null)
  if [ -z "$files" ]; then
    files=$(git diff --name-only 2>/dev/null)
  fi
  if [ -z "$files" ]; then
    return 0
  fi
  # Scope: app components public lib (matches CI guard scope)
  local match
  match=$(echo "$files" | xargs -I{} grep -liE "humanizer|bypass|undetect|detector|evade" "{}" 2>/dev/null | head -3)
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
  sed -i.bak -E "s~^(\| ${id} \|[^|]*\|[^|]*\|[^|]*\| )(pending|in_progress)( \|)~\1${new_status}\3~" "$BOARD"
  rm -f "${BOARD}.bak"
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
      update_board_status "$ID" "BLOCKED-WAITING-USER"
      append_decision "$ID | blocked | Entra cluster, daytime supervised work"
      ISSUES_BLOCKED=$((ISSUES_BLOCKED + 1))
      ISSUES_PROCESSED=$((ISSUES_PROCESSED + 1))
      sleep "$COOLDOWN_SECONDS"
      continue
      ;;
    M2-001|M2-002|M2-003|M2-004|M2-005|M2-006|M2-008|M2-009)
      log "  Skipping $ID (quality gate cluster — needs eval baseline)"
      update_board_status "$ID" "BLOCKED-WAITING-USER"
      append_decision "$ID | blocked | quality gate needs eval data"
      ISSUES_BLOCKED=$((ISSUES_BLOCKED + 1))
      ISSUES_PROCESSED=$((ISSUES_PROCESSED + 1))
      sleep "$COOLDOWN_SECONDS"
      continue
      ;;
    M3-001|M3-002|M3-003|M3-004|M3-005|M3-006|M3-007|M3-008)
      log "  Skipping $ID (V2 layout cascade — typed refactor across lib)"
      update_board_status "$ID" "BLOCKED-WAITING-USER"
      append_decision "$ID | blocked | V2 cascade needs daytime coordination"
      ISSUES_BLOCKED=$((ISSUES_BLOCKED + 1))
      ISSUES_PROCESSED=$((ISSUES_PROCESSED + 1))
      sleep "$COOLDOWN_SECONDS"
      continue
      ;;
    M8-002|M8-003|M8-004|M8-005|M8-006|M8-007|M8-008|M8-009|M8-010|M8-011|M8-012|M8-013|M8-014|M8-015|M8-016)
      log "  Skipping $ID (B2B API chain — depends on M8-001 + Azure SQL routing)"
      update_board_status "$ID" "BLOCKED-WAITING-USER"
      append_decision "$ID | blocked | M8 chain needs M8-001 merged"
      ISSUES_BLOCKED=$((ISSUES_BLOCKED + 1))
      ISSUES_PROCESSED=$((ISSUES_PROCESSED + 1))
      sleep "$COOLDOWN_SECONDS"
      continue
      ;;
  esac

  # Build current-task.md from manifest or detailed brief
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

  # Mark in_progress
  update_board_status "$ID" "in_progress"
  append_decision "$ID | started | $TITLE"

  # ─── Git: branch from main ────────────────────────────────────────────
  log "  git checkout main && git pull"
  if ! git checkout main >>"$LOG" 2>&1; then
    log "  ERROR: could not checkout main"
    update_board_status "$ID" "BLOCKED"
    append_blocker "$ID" "git-checkout-main-failed" "checkout to main failed"
    ISSUES_BLOCKED=$((ISSUES_BLOCKED + 1))
    ISSUES_PROCESSED=$((ISSUES_PROCESSED + 1))
    sleep "$COOLDOWN_SECONDS"
    continue
  fi
  git pull --ff-only >>"$LOG" 2>&1 || log "  WARN: git pull failed (probably no network or remote ahead)"

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
    ISSUES_BLOCKED=$((ISSUES_BLOCKED + 1))
    ISSUES_PROCESSED=$((ISSUES_PROCESSED + 1))
    sleep "$COOLDOWN_SECONDS"
    continue
  fi

  # ─── Codex: file edits + tests ────────────────────────────────────────
  rm -f plans/task-status.json
  run_codex_implementation "$ID"
  cdx_exit=$?

  if [ ! -f plans/task-status.json ]; then
    log "  ERROR: codex did not produce task-status.json (exit=$cdx_exit)"
    update_board_status "$ID" "BLOCKED"
    append_blocker "$ID" "codex-no-status" "codex exec did not write plans/task-status.json. Log: plans/codex-exec-${ID}.log"
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
      update_board_status "$ID" "BLOCKED-WAITING-USER"
      append_blocker "$ID" "codex-needs-human" "$SUMMARY"
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
    git checkout . >>"$LOG" 2>&1
    git clean -fd >>"$LOG" 2>&1
    git checkout main >>"$LOG" 2>&1
    git branch -D "$BRANCH" >>"$LOG" 2>&1 || true
    update_board_status "$ID" "BLOCKED"
    append_blocker "$ID" "banned-term-in-diff" "Diff contained one of: humanizer/bypass/undetect/detector/evade"
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

append_progress "Run finished. Done: $ISSUES_DONE | Blocked: $ISSUES_BLOCKED | Needs human: $ISSUES_NEEDS_HUMAN"
