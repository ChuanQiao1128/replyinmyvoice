#!/usr/bin/env bash
# driver.sh — generalized serial, restart-surviving delivery driver for ONE wave.
#
# Config is sourced from wave.conf (path in $RIMV_WAVE_CONF). NOTHING is hardcoded to a wave.
# Integration branch = $BASE (NEVER main — preflight refuses the default branch).
#
# Model: ONE git worktree per issue under $WT/issue-<n>, cut off the latest $BASE tip. Codex
# (codex exec) writes + commits the code IN that worktree; this driver does push + `gh pr create
# --base $BASE`. TIER-1 prereqs (TIER1_MERGE=yes) are also fast-merged into $BASE via the
# _integration worktree so dependents build on them.
#
# Per issue: worktree -> codex exec (<=ATTEMPTS, resume w/ feedback) -> verify gates -> push + PR
#            -> (tier1) base-merge. Heartbeat after every step; per-issue log. Events pushed via
#            notify.sh on issue-passed / issue-blocked / canary outcome / wave-done.
# Idempotent: skips any issue with an existing OPEN PR into base (merged-then-reset must NOT skip),
#             or a done/<n> marker.
# STOP-aware: exits 0 if the WAVE-LOCAL STOP exists ($CONTROL_DIR/STOP). NEVER honors a global STOP.
# CANARY: the FIRST queue issue is a canary — if it BLOCKS, the failure is treated as systemic
#         (PAUSE the wave + notify) rather than burning Codex on the rest.
# On clean finish writes WAVE_DONE (NOT "DONE" — case-insensitive FS collides with done/ markers).
set -uo pipefail

CONF="${RIMV_WAVE_CONF:?driver: set RIMV_WAVE_CONF to the wave.conf path}"
. "$CONF"
: "${REPO:?}" "${GHREPO:?}" "${BASE:?}" "${BRANCH_PREFIX:?}" "${CONTROL_DIR:?}" "${QUEUE:?}" "${BRIEF_DIR:?}"

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
NOTIFY="$HERE/notify.sh"
TMPL="${CODEX_BRIEF_TMPL:-$HERE/codex-brief.tmpl}"
WT="$CONTROL_DIR/wt"
INTEG_WT="$WT/_integration"
HB="$CONTROL_DIR/heartbeat.txt"
LOGDIR="$CONTROL_DIR/logs"
DONEDIR="$CONTROL_DIR/done"
DRVLOG="$CONTROL_DIR/driver.log"
STOP="$CONTROL_DIR/STOP"                 # wave-local STOP ONLY (never a global .delivery/STOP)
WAVE="${WAVE:-wave}"
ATTEMPTS="${ATTEMPTS:-3}"
DEFAULT_TIMEOUT_MIN="${DEFAULT_TIMEOUT_MIN:-40}"
BANNED_TERMS="${BANNED_TERMS:-humanizer|bypass|undetect|detector|evade}"
BANNED_PATHS="${BANNED_PATHS:-app components public lib}"
DOTNET_DIR="${DOTNET_DIR:-backend-dotnet}"
FRONTEND_PATHS="${FRONTEND_PATHS:-app components lib}"

mkdir -p "$LOGDIR" "$DONEDIR" "$WT"
cd "$REPO" || exit 3

log(){  echo "[$(date '+%F %T')] $*" >> "$DRVLOG"; }
beat(){ echo "$(date +%s) | $(date '+%F %T') | issue=${1:-} | phase=${2:-}" > "$HB"; }
stopped(){ [ -f "$STOP" ]; }
note(){ bash "$NOTIFY" "$@" 2>/dev/null || true; }
ilog(){ echo "[$(date '+%F %T')] $*" >> "$ILOG"; }   # per-issue log (ILOG set in process_issue)

command -v jq    >/dev/null || { log "FATAL: jq missing";    beat "" "fatal-no-jq";    note systemic-error "driver" "jq missing"; exit 3; }
command -v gh    >/dev/null || { log "FATAL: gh missing";    beat "" "fatal-no-gh";    note systemic-error "driver" "gh missing"; exit 3; }
command -v codex >/dev/null || { log "FATAL: codex missing"; beat "" "fatal-no-codex"; note systemic-error "driver" "codex missing"; exit 3; }
[ -f "$QUEUE" ]   || { log "FATAL: no queue"; beat "" "fatal-no-queue"; note systemic-error "driver" "queue missing"; exit 3; }

# --- ensure the _integration worktree exists (created lazily; survives across cycles) -------
ensure_integ(){
  [ -d "$INTEG_WT" ] && return 0
  local baseref="$BASE"; git show-ref --verify --quiet "refs/heads/$BASE" || baseref="origin/$BASE"
  git fetch origin "$BASE" --quiet 2>>"$DRVLOG" || true
  git worktree add -f "$INTEG_WT" "$baseref" >>"$DRVLOG" 2>&1 \
    || { log "FATAL: cannot create _integration worktree"; note systemic-error "driver" "integration worktree add failed"; return 1; }
}

# --- single-instance guard (mkdir is atomic) — prevents the startup race where start.sh and the
# --- sentinel both launch a driver and multiple driver.sh stomp the same worktree. -----------
LOCKD="$CONTROL_DIR/driver.lock.d"
if ! mkdir "$LOCKD" 2>/dev/null; then
  oldpid=$(cat "$LOCKD/pid" 2>/dev/null || echo "")
  if [ -n "$oldpid" ] && kill -0 "$oldpid" 2>/dev/null; then
    log "another driver.sh (pid $oldpid) holds the lock -> exit 0 (idle)"; exit 0
  fi
  log "stale driver lock (pid='${oldpid:-?}') -> taking over"
  rm -rf "$LOCKD"; mkdir "$LOCKD" 2>/dev/null || { log "lock race lost -> exit 0"; exit 0; }
fi
echo $$ > "$LOCKD/pid"
trap 'rm -rf "$LOCKD"' EXIT

ensure_integ || exit 3
log "=== $WAVE driver cycle start (base=$BASE attempts=$ATTEMPTS) ==="
beat "" "cycle-start"

# ---------------------------------------------------------------------------
sync_base(){
  ( cd "$INTEG_WT" \
      && git fetch origin "$BASE" --quiet 2>>"$DRVLOG" \
      && git checkout -q "$BASE" 2>>"$DRVLOG" \
      && git reset --hard "origin/$BASE" --quiet 2>>"$DRVLOG" \
      && git pull --ff-only origin "$BASE" --quiet 2>>"$DRVLOG" ) \
    || log "WARN: sync_base had a non-zero step (continuing)"
}

# already-handled? OPEN PR into base, or a done marker. NOTE: --state open ONLY — a merged-then-
# reset PR (the v1 #401 chaos) must NOT falsely skip a not-yet-redelivered issue.
already_done(){  # $1=issue $2=tag
  local n="$1" tag="$2"
  [ -f "$DONEDIR/$n" ] && { echo "marker"; return 0; }
  local st
  st=$(gh pr list --repo "$GHREPO" --base "$BASE" --state open \
        --search "head:$BRANCH_PREFIX/$tag-$n" --json state -q '.[0].state' 2>/dev/null)
  if [ -n "$st" ] && [ "$st" != "null" ]; then echo "pr:$st"; return 0; fi
  return 1
}

# is a dependency satisfied? (merged into base => present in origin/$BASE history) -----------
deps_satisfied(){  # $1=csv-or-"-"
  local deps="$1" d
  [ "$deps" = "-" ] && return 0
  ( cd "$INTEG_WT"; git fetch origin "$BASE" --quiet 2>/dev/null || true )
  IFS=',' read -ra arr <<< "$deps"
  for d in "${arr[@]}"; do
    [ -z "$d" ] && continue
    # local exact signal: the tier-1 dep wrote a done-marker (and its base-merge happened in that
    # same process_issue call, before any dependent runs). No GH-search ambiguity.
    [ -f "$DONEDIR/$d" ] && continue
    # exact remote signal: the dep's branch (looked up from the queue) is merged into base.
    local dtag dbranch m
    dtag=$(awk -F'\t' -v n="$d" '$1==n{print $2}' "$QUEUE" 2>/dev/null)
    if [ -n "$dtag" ]; then
      dbranch="$BRANCH_PREFIX/$dtag-$d"
      m=$(gh pr list --repo "$GHREPO" --base "$BASE" --state merged \
            --search "head:$dbranch" --json number -q 'length' 2>/dev/null)
      [ "${m:-0}" -ge 1 ] && continue
      if ( cd "$INTEG_WT" && git merge-base --is-ancestor "origin/$dbranch" "origin/$BASE" ) 2>/dev/null; then
        continue
      fi
    fi
    echo "$d"; return 1
  done
  return 0
}

# ---------------------------------------------------------------------------
# process_issue N TAG TIER TIER1_MERGE DEPS BRIEF_GLOB TIMEOUT_MIN IS_CANARY
# Returns 0 always; sets globals DEFERRED / CANARY_VERDICT for the main loop.
process_issue(){
  local N="$1" TAG="$2" TIER="$3" T1M="$4" DEPS="$5" GLOB="$6" TMO="$7" CANARY="${8:-no}"
  local BRANCH="$BRANCH_PREFIX/$TAG-$N"
  local IWT="$WT/issue-$N"
  ILOG="$LOGDIR/issue-$N.log"; export ILOG
  # resolve the brief (glob-tolerant)
  local BRIEF=""
  if [ -n "$GLOB" ] && [ "$GLOB" != "-" ]; then
    # shellcheck disable=SC2086
    BRIEF=$(ls $BRIEF_DIR/$GLOB 2>/dev/null | head -1)
  fi
  # per-attempt timeout in seconds (adaptive: queue value or default)
  local TMIN="${TMO:-}"; case "$TMIN" in ''|*[!0-9]*) TMIN="$DEFAULT_TIMEOUT_MIN";; esac
  local CODEX_TIMEOUT=$(( TMIN * 60 ))

  beat "$N" "start"
  ilog "=== issue #$N ($TAG) tier=$TIER tier1merge=$T1M deps=$DEPS timeout=${TMIN}m canary=$CANARY ==="

  # idempotency
  local why
  if why=$(already_done "$N" "$TAG"); then
    log "#$N SKIP (already handled: $why)"; ilog "SKIP already handled: $why"; beat "$N" "skip-$why"; return 0
  fi

  # dependency gate
  local missing
  if ! missing=$(deps_satisfied "$DEPS"); then
    log "#$N DEFER (dep #$missing not yet merged into $BASE)"; ilog "DEFER missing dep #$missing"
    beat "$N" "defer-dep-$missing"; DEFERRED=1; return 0
  fi

  if [ -n "$GLOB" ] && [ "$GLOB" != "-" ] && [ -z "$BRIEF" ]; then
    log "#$N BLOCKED missing brief $GLOB"; ilog "BLOCKED missing brief"; mark_blocked "$N" "missing brief $GLOB"; return 0
  fi

  # ---- fresh worktree off latest base ----
  beat "$N" "worktree"
  sync_base
  git worktree remove --force "$IWT" 2>>"$ILOG" || true
  git branch -D "$BRANCH" 2>>"$ILOG" || true
  if ! git worktree add -f -b "$BRANCH" "$IWT" "origin/$BASE" >>"$ILOG" 2>&1; then
    git worktree add -f -b "$BRANCH" "$IWT" "$BASE" >>"$ILOG" 2>&1 \
      || { log "#$N BLOCKED worktree add failed"; ilog "BLOCKED worktree add failed"; mark_blocked "$N" "worktree add failed"; return 0; }
  fi
  ilog "worktree ready at $IWT on $BRANCH"

  # ---- build the per-issue codex prompt ----
  local PROMPT="$CONTROL_DIR/prompt-$N.md"
  build_prompt "$N" "$TAG" "$BRANCH" "$BRIEF" "$DEPS" > "$PROMPT" 2>>"$ILOG"

  # ---- attempts loop ----
  local verdict=FAIL THREAD="" a RES="" FB="$CONTROL_DIR/feedback-$N.md"
  for a in $(seq 1 "$ATTEMPTS"); do
    stopped && { log "#$N STOP mid-issue"; beat "$N" "stopped"; exit 0; }
    beat "$N" "codex-attempt-$a/$ATTEMPTS"
    ilog "--- codex attempt $a/$ATTEMPTS (timeout ${TMIN}m)"
    local LAST="$LOGDIR/issue-$N.attempt-$a.last.json"
    local JSONL="$LOGDIR/issue-$N.attempt-$a.jsonl"
    run_codex "$IWT" "$LAST" "$JSONL" "$THREAD" "$( [ "$a" -eq 1 ] && echo "$PROMPT" || echo "$FB" )" "$CODEX_TIMEOUT"
    local cx=$?
    local TID; TID=$(grep -o '"thread_id":"[^"]*"' "$JSONL" 2>/dev/null | head -1 | sed 's/.*:"//;s/"$//')
    { [ -n "$TID" ] && [ "$TID" != none ]; } && THREAD="$TID"
    ilog "codex attempt $a exit=$cx thread=${THREAD:-none}"
    [ "$cx" -eq 124 ] && ilog "NOTE: codex hit the ${TMIN}m timeout (exit 124); verifying whatever it committed"

    # ---- verify ----
    beat "$N" "verify-attempt-$a"
    local VOUT; VOUT=$(verify_issue "$IWT" "$N" 2>&1)
    RES=$(printf '%s' "$VOUT" | grep '^VERIFY_RESULT' | tail -1)
    ilog "verify -> ${RES:-no-verify-line}"
    printf '%s\n' "$VOUT" >> "$ILOG"
    if printf '%s' "$RES" | grep -q 'PASS'; then verdict=PASS; break; fi

    # corrective feedback for next attempt (resume same thread)
    { echo "Your previous attempt FAILED verification: ${RES:-unknown}";
      echo "Fix ONLY the failing gate/checks. Stay strictly inside scope. Do NOT disable tests";
      echo "or add @ts-ignore/eslint-disable. Re-run the brief's acceptance commands plus the";
      echo "relevant project gates yourself before reporting. Remember: NO push, NO PR, NO main,";
      echo "banned terms forbidden ($BANNED_TERMS).";
      echo; echo "---- verifier output (tail) ----"; printf '%s\n' "$VOUT" | tail -30; } > "$FB"
  done

  if [ "$verdict" != PASS ]; then
    log "#$N BLOCKED after $ATTEMPTS attempts (last: ${RES:-none})"
    ilog "BLOCKED after $ATTEMPTS attempts: ${RES:-none}"
    mark_blocked "$N" "failed verification ${ATTEMPTS}x; last=${RES:-none}"
    [ "$CANARY" = yes ] && CANARY_VERDICT=blocked
    return 0
  fi

  # ---- PASS: ensure committed in the worktree, then push + PR (driver-side) ----
  beat "$N" "commit-push"
  ( cd "$IWT"
    [ -L node_modules ] && rm -f node_modules            # drop the verify-step symlink so it is NOT committed
    if [ -n "$(git status --porcelain --untracked-files=all)" ]; then
      git add -A
      git commit -q -m "$TAG: deliver issue #$N" -m "Closes #$N" \
        -m "Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>" 2>>"$ILOG" || true
    fi
  )
  # guard: a branch with no commits beyond base is a no-op -> block (detected via rev-list, NOT
  # via `git add -A` emptiness — add-only issues must not falsely block).
  local ahead
  ahead=$( cd "$IWT" && git rev-list --count "origin/$BASE..HEAD" 2>/dev/null || echo 0 )
  if [ "${ahead:-0}" -eq 0 ]; then
    log "#$N BLOCKED no commits beyond base (worker produced nothing)"; ilog "BLOCKED empty branch"
    mark_blocked "$N" "no commits beyond base"; [ "$CANARY" = yes ] && CANARY_VERDICT=blocked; return 0
  fi

  if ! ( cd "$IWT" && git push -fu origin "$BRANCH" ) >>"$ILOG" 2>&1; then
    log "#$N BLOCKED push failed"; ilog "BLOCKED push failed"; mark_blocked "$N" "git push failed"; [ "$CANARY" = yes ] && CANARY_VERDICT=blocked; return 0
  fi

  beat "$N" "pr-create"
  local PRURL
  PRURL=$(gh pr create --repo "$GHREPO" --base "$BASE" --head "$BRANCH" \
            --title "$TAG: deliver issue #$N" \
            --body "Automated delivery of issue #$N ($TAG, $WAVE wave) into \`$BASE\`. Closes #$N." \
            2>>"$ILOG" || gh pr view "$BRANCH" --repo "$GHREPO" --json url -q .url 2>/dev/null)
  log "#$N PR: ${PRURL:-none}"; ilog "PR: ${PRURL:-none}"
  printf '%s' "${PRURL:-pr-open}" > "$DONEDIR/$N"   # mark handled (PR exists) regardless of merge

  # ---- TIER-1: merge this branch into base so dependents build on it ----
  if [ "$T1M" = "yes" ]; then
    beat "$N" "tier1-merge"
    if tier1_merge "$N" "$TAG" "$BRANCH"; then
      log "#$N TIER-1 merged into $BASE"; ilog "TIER-1 merged into $BASE"
    else
      log "#$N TIER-1 merge FAILED — dependents may defer"; ilog "TIER-1 merge FAILED"
      note issue-blocked "issue #$N" "tier-1 base-merge failed (PR left open for a human): ${PRURL:-?}"
    fi
  fi

  gh issue comment "$N" --repo "$GHREPO" \
    --body "$WAVE driver: verification PASSED; PR ${PRURL:-opened} into \`$BASE\`." >/dev/null 2>&1 || true
  beat "$N" "done"
  note issue-passed "issue #$N" "$TAG PR ${PRURL:-opened} into $BASE"
  [ "$CANARY" = yes ] && CANARY_VERDICT=passed
  return 0
}

# ---------------------------------------------------------------------------
# run_codex WT LAST JSONL THREAD PROMPTFILE TIMEOUT_SEC  — proven flags; resume on attempt>=2.
run_codex(){
  local wt="$1" last="$2" jsonl="$3" thread="$4" pfile="$5" tmo="$6"
  # redirect tool caches inside the control dir so a workspace-write sandbox suffices
  export PNPM_HOME="$CONTROL_DIR/pnpm-store"
  export npm_config_store_dir="$CONTROL_DIR/pnpm-store"
  export npm_config_cache="$CONTROL_DIR/.cache/npm"
  export PLAYWRIGHT_BROWSERS_PATH="$CONTROL_DIR/.cache/ms-playwright"
  if [ -n "$thread" ] && [ "$thread" != none ]; then
    ( cd "$wt" && timeout "$tmo" codex exec resume "$thread" \
        -c approval_policy=never -c sandbox_workspace_write.network_access=true \
        --json -o "$last" - < "$pfile" ) > "$jsonl" 2>&1
  else
    timeout "$tmo" codex exec -C "$wt" -s workspace-write \
      -c sandbox_workspace_write.network_access=true -c approval_policy=never \
      --json -o "$last" < "$pfile" > "$jsonl" 2>&1
  fi
  return $?
}

# ---------------------------------------------------------------------------
# verify_issue WT N  — gates run INSIDE the worktree. Prints a final VERIFY_RESULT: PASS|FAIL.
#   Trust nothing the worker reported. Gate1 banned-term (DIFF-SCOPED). Gate1b secret/suppress.
#   Gate2 tests-by-diff. Gate3 scope diffstat.
verify_issue(){
  local wt="$1" N="$2"
  ( cd "$wt" || { echo "VERIFY_RESULT: FAIL gate=cd"; exit 1; }

    # Gate 1: BANNED TERMS (hard fail) — ONLY terms CODEX *added* (added lines vs base, plus new
    # untracked files) under $BANNED_PATHS. Pre-existing matches (e.g. a deny-list fixture) must
    # NOT fail the gate, or EVERY issue would falsely block. (postmortem bug #1)
    # shellcheck disable=SC2086
    banned_added=$( { git diff "origin/$BASE" -- $BANNED_PATHS 2>/dev/null
                      for uf in $(git ls-files --others --exclude-standard -- $BANNED_PATHS 2>/dev/null); do
                        git diff --no-index -- /dev/null "$uf" 2>/dev/null
                      done
                    } | grep -E '^\+' | grep -vE '^\+\+\+' )
    if printf '%s' "$banned_added" | grep -iqE "$BANNED_TERMS"; then
      echo "Gate1(banned): FAIL — codex ADDED a banned term:"
      printf '%s\n' "$banned_added" | grep -inE "$BANNED_TERMS" | head -5
      echo "VERIFY_RESULT: FAIL gate=banned-term"; exit 1
    fi
    echo "Gate1(banned): PASS (diff-scoped)"

    # what changed vs base
    local changed
    changed=$(git diff --name-only "origin/$BASE...HEAD" 2>/dev/null; git status --porcelain=v1 --untracked-files=all | sed 's/^...//')
    changed=$(printf '%s\n' "$changed" | sed '/^$/d' | sort -u)
    echo "--- changed files ---"; printf '%s\n' "$changed" | sed 's/^/    /'

    # Gate 1b: secret-value / suppression sanity on changed files
    local f
    while IFS= read -r f; do
      [ -z "$f" ] && continue; [ -f "$f" ] || continue
      if grep -Eq '(sk-[A-Za-z0-9]{16}|whsec_[A-Za-z0-9]{16}|rk_live_[A-Za-z0-9]{16}|AKIA[0-9A-Z]{12}|gh[pousr]_[A-Za-z0-9]{20}|-----BEGIN [A-Z ]*PRIVATE KEY)' "$f"; then
        echo "Gate1b(secret): FAIL in $f"; echo "VERIFY_RESULT: FAIL gate=secret-value"; exit 1; fi
      case "$f" in
        *.ts|*.tsx|*.cs)
          if git diff "origin/$BASE...HEAD" -- "$f" 2>/dev/null | grep -E '^\+' | grep -Eq '@ts-(ignore|expect-error)|eslint-disable'; then
            echo "Gate1b(suppress): FAIL added suppression in $f"; echo "VERIFY_RESULT: FAIL gate=suppress"; exit 1; fi;;
      esac
    done <<< "$changed"
    echo "Gate1b(secret/suppress): PASS"

    # Gate 2: tests appropriate to the diff
    local touch_dotnet=0 touch_front=0 touch_nontrivial=0
    while IFS= read -r f; do
      [ -z "$f" ] && continue
      case "$f" in
        "$DOTNET_DIR"/*) touch_dotnet=1; touch_nontrivial=1;;
        plans/*|docs/*|*.md) : ;;
        *)
          local hit=0 p
          for p in $FRONTEND_PATHS; do case "$f" in "$p"/*) hit=1;; esac; done
          if [ "$hit" = 1 ]; then touch_front=1; touch_nontrivial=1
          else case "$f" in public/*) touch_nontrivial=1;; *) touch_nontrivial=1;; esac; fi;;
      esac
    done <<< "$changed"

    if [ "$touch_dotnet" = 1 ]; then
      echo "Gate2(dotnet): running dotnet test in $DOTNET_DIR"
      if ! ( cd "$REPO/$DOTNET_DIR" && dotnet test --nologo ) > "$LOGDIR/issue-$N.dotnet.log" 2>&1; then
        echo "Gate2(dotnet): FAIL"; tail -n 25 "$LOGDIR/issue-$N.dotnet.log" | sed 's/^/    | /'
        echo "VERIFY_RESULT: FAIL gate=dotnet-test"; exit 1; fi
      echo "Gate2(dotnet): PASS"
    fi
    if [ "$touch_front" = 1 ]; then
      echo "Gate2(frontend): symlink node_modules + typecheck + unit test"
      [ -e node_modules ] || ln -s "$REPO/node_modules" node_modules 2>/dev/null || true
      if ! npm run typecheck > "$LOGDIR/issue-$N.tsc.log" 2>&1; then
        echo "Gate2(typecheck): FAIL"; tail -n 25 "$LOGDIR/issue-$N.tsc.log" | sed 's/^/    | /'
        echo "VERIFY_RESULT: FAIL gate=typecheck"; exit 1; fi
      if ! npm run test > "$LOGDIR/issue-$N.test.log" 2>&1; then
        echo "Gate2(test): FAIL"; tail -n 25 "$LOGDIR/issue-$N.test.log" | sed 's/^/    | /'
        echo "VERIFY_RESULT: FAIL gate=unit-test"; exit 1; fi
      echo "Gate2(frontend): PASS"
    fi
    if [ "$touch_nontrivial" = 0 ]; then
      local empty=0
      while IFS= read -r f; do [ -z "$f" ] && continue; [ -f "$f" ] && [ ! -s "$f" ] && { echo "empty file: $f"; empty=1; }; done <<< "$changed"
      [ "$empty" = 1 ] && { echo "Gate2(docs): FAIL empty file"; echo "VERIFY_RESULT: FAIL gate=docs-empty"; exit 1; }
      echo "Gate2(docs-only): PASS"
    fi

    # Gate 3: scope sanity — surface the diffstat (judgment, not auto-fail beyond above)
    echo "--- diffstat ---"; git diff --stat "origin/$BASE...HEAD" 2>/dev/null | tail -n 20 | sed 's/^/    /'
    echo "VERIFY_RESULT: PASS attempt=ok"
    exit 0
  )
}

# ---------------------------------------------------------------------------
# tier1_merge N TAG BRANCH — merge a passed tier-1 branch into base via the _integration worktree.
tier1_merge(){
  local N="$1" TAG="$2" BRANCH="$3"
  ( cd "$INTEG_WT" || exit 1
    git fetch origin "$BASE" "$BRANCH" --quiet 2>>"$ILOG" || true
    git checkout -q "$BASE" 2>>"$ILOG" || exit 1
    git reset --hard "origin/$BASE" --quiet 2>>"$ILOG" || true
    if ! git merge --no-ff "origin/$BRANCH" -m "Merge $TAG (#$N) into $BASE" >>"$ILOG" 2>&1; then
      git merge --abort 2>/dev/null || true
      exit 1
    fi
    git push origin "$BASE" >>"$ILOG" 2>&1 || exit 1
  )
}

# ---------------------------------------------------------------------------
mark_blocked(){  # $1=N $2=reason
  local N="$1" reason="$2"
  printf 'BLOCKED: %s\n' "$reason" > "$DONEDIR/$N"   # idempotency: do not retry on next cycle
  gh issue edit "$N" --repo "$GHREPO" --add-label blocked --remove-label in-progress >/dev/null 2>&1 || true
  gh issue comment "$N" --repo "$GHREPO" \
    --body "$WAVE driver auto-blocked this issue: ${reason}. A human can re-scope / re-run (delete $DONEDIR/$N to retry)." >/dev/null 2>&1 || true
  note issue-blocked "issue #$N" "$reason"
}

# ---------------------------------------------------------------------------
# build_prompt N TAG BRANCH BRIEF DEPS  — fills codex-brief.tmpl with the live issue body + brief.
# UNTRUSTED text (issue body, deps note) is inserted VERBATIM from temp files (awk prints the file
# at the placeholder line); only short controlled tokens go through gsub. Dependency notes are
# DERIVED from the queue (generalized — no hardcoded per-issue case statement like v1).
build_prompt(){
  local N="$1" TAG="$2" BRANCH="$3" BRIEF="$4" DEPS="$5"
  local body; body=$(gh issue view "$N" --repo "$GHREPO" --json body -q .body 2>/dev/null)
  [ -z "$body" ] && body="(could not fetch issue body; rely on the brief file ${BRIEF:-<none>})"
  # build a dependency note from the queue: each dep is ALREADY merged into base (deps_satisfied
  # gated this), so tell the worker to build on it.
  local depsnote
  if [ "$DEPS" = "-" ] || [ -z "$DEPS" ]; then
    depsnote="# Dependency note
(no cross-issue code dependencies)"
  else
    depsnote="# Dependency note
This issue depends on issue(s) #${DEPS//,/, #}, which are ALREADY merged into the base branch \`$BASE\` you were cut from — build on their work, do not reimplement it."
  fi
  local BODYF DEPSF
  BODYF=$(mktemp "${TMPDIR:-/tmp}/ddw-body.$N.XXXXXX")
  DEPSF=$(mktemp "${TMPDIR:-/tmp}/ddw-deps.$N.XXXXXX")
  printf '%s\n' "$body"     > "$BODYF"
  printf '%s\n' "$depsnote" > "$DEPSF"
  BRANCHV="$BRANCH" BRIEFV="${BRIEF:-(no brief file for this issue)}" TAGV="$TAG" NV="$N" \
  WAVEV="$WAVE" BASEV="$BASE" BANNEDV="$BANNED_TERMS" BODYF="$BODYF" DEPSF="$DEPSF" \
  awk '
    /^__ISSUE_BODY__$/ { while ((getline l < ENVIRON["BODYF"]) > 0) print l; close(ENVIRON["BODYF"]); next }
    /^__DEPS_NOTE__$/  { while ((getline l < ENVIRON["DEPSF"]) > 0) print l; close(ENVIRON["DEPSF"]); next }
    { line=$0
      gsub(/__BRANCH__/,     ENVIRON["BRANCHV"], line)
      gsub(/__BRIEF_PATH__/, ENVIRON["BRIEFV"],  line)
      gsub(/__TAG__/,        ENVIRON["TAGV"],    line)
      gsub(/__ISSUE__/,      ENVIRON["NV"],      line)
      gsub(/__WAVE__/,       ENVIRON["WAVEV"],   line)
      gsub(/__BASE__/,       ENVIRON["BASEV"],   line)
      gsub(/__BANNED_TERMS__/, ENVIRON["BANNEDV"], line)
      print line }
  ' "$TMPL"
  rm -f "$BODYF" "$DEPSF"
}

# ===========================================================================
# MAIN LOOP — read the queue, process each actionable issue, defer dep-blocked ones.
# Re-loop while any issue was deferred AND progress was made. Stop when nothing left actionable.
# CANARY: the FIRST actionable row (in the very first pass) is the canary. If it BLOCKS, the
# failure is treated as systemic -> STOP the wave + notify (don't burn Codex on the rest).
CANARY_DONE=0
while true; do
  stopped && { log "STOP file -> clean exit"; beat "" "stopped"; exit 0; }
  DEFERRED=0
  before=$(ls "$DONEDIR" 2>/dev/null | wc -l | tr -d ' ')

  while IFS=$'\t' read -r N TAG TIER T1M DEPS GLOB TMO; do
    case "$N" in ''|\#*) continue;; esac
    stopped && { log "STOP mid-queue"; beat "$N" "stopped"; exit 0; }
    if [ "$CANARY_DONE" -eq 0 ]; then
      # treat the first issue we actually attempt (not skip/defer) as the canary
      if already_done "$N" "$TAG" >/dev/null 2>&1; then
        process_issue "$N" "$TAG" "$TIER" "$T1M" "$DEPS" "$GLOB" "$TMO" "no"; continue
      fi
      CANARY_VERDICT=""
      note info "canary" "processing first issue #$N as canary"
      process_issue "$N" "$TAG" "$TIER" "$T1M" "$DEPS" "$GLOB" "$TMO" "yes"
      if [ "${CANARY_VERDICT:-}" = blocked ]; then
        log "=== CANARY #$N BLOCKED -> treating as SYSTEMIC; PAUSE wave ==="
        beat "$N" "canary-failed"
        note canary-failed "issue #$N" "first issue blocked — likely systemic (gate/brief/auth). Wave PAUSED; investigate before resuming. Delete $DONEDIR/$N and the STOP file to retry."
        : > "$STOP"          # wave-local pause; start.sh re-clears it on an intentional resume
        : > "$CONTROL_DIR/WAVE_DONE"   # let the outer loop stop cleanly (sentinel won't relaunch)
        exit 0
      fi
      [ "${CANARY_VERDICT:-}" = passed ] && note canary-passed "issue #$N" "canary clean -> auto-continuing the rest of the wave"
      CANARY_DONE=1
      continue
    fi
    process_issue "$N" "$TAG" "$TIER" "$T1M" "$DEPS" "$GLOB" "$TMO" "no"
  done < "$QUEUE"

  after=$(ls "$DONEDIR" 2>/dev/null | wc -l | tr -d ' ')
  if [ "$DEFERRED" = 0 ]; then
    log "=== queue complete (handled=$after) -> WAVE_DONE ==="
    beat "" "WAVE_DONE"
    : > "$CONTROL_DIR/WAVE_DONE"
    note wave-done "$WAVE" "queue complete: $after issue(s) handled (see $CONTROL_DIR/STATUS for per-issue pass/block)"
    exit 0
  fi
  if [ "$after" -le "$before" ]; then
    log "=== deferred issues remain but NO progress this pass (handled=$after) -> stop (likely blocked deps) ==="
    beat "" "stalled-deps"
    : > "$CONTROL_DIR/WAVE_DONE"
    note systemic-error "$WAVE" "deferred issues remain but no progress this pass — likely an unsatisfiable dependency. Wave stopped; review $CONTROL_DIR/STATUS + per-issue logs."
    exit 0
  fi
  log "--- pass complete; deferred issues remain, progress made -> re-loop"
done
