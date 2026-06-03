#!/usr/bin/env bash
# preflight.sh — validate the wave BEFORE any daemon launches. Catches systemic problems in
# seconds instead of after burning Codex on issue #1. Exits non-zero (and notifies) on any
# failure so start.sh aborts.
#
# Sourced config: wave.conf (REPO, GHREPO, BASE, BRANCH_PREFIX, CONTROL_DIR, QUEUE, BRIEF_DIR,
#                 BANNED_TERMS, BANNED_PATHS). Pass its path via $RIMV_WAVE_CONF (start.sh does).
#
# Checks, in order:
#   1. Tooling present: codex, gh, git, jq.
#   2. Auth live: `gh auth status` ok; git origin is HTTPS (SSH:22 times out from the iCloud
#      Desktop — see postmortem bug #3); `gh auth setup-git` so pushes use the gh token.
#   3. Integration BASE branch exists (locally or on origin) and is NOT a protected/default branch.
#   4. Queue parses; every brief file referenced by the queue exists.
#   5. GATE DRY-RUN on the base tree: run the diff-scoped banned-term gate against an EMPTY diff.
#      This is THE check that would have caught the v1 tree-wide false-positive that matched a
#      pre-existing deny-list fixture and would have blocked all 22 issues. On an empty diff the
#      gate MUST pass; if it flags anything, the gate is mis-scoped — abort.
set -uo pipefail

CONF="${RIMV_WAVE_CONF:?preflight: set RIMV_WAVE_CONF to the wave.conf path}"
[ -f "$CONF" ] || { echo "preflight: wave.conf not found at $CONF"; exit 2; }
. "$CONF"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
NOTIFY="$HERE/notify.sh"
note(){ bash "$NOTIFY" "$@" 2>/dev/null || true; }
fail(){ echo "PREFLIGHT FAIL: $*" >&2; note systemic-error "preflight" "$*"; exit 1; }

: "${REPO:?}" "${GHREPO:?}" "${BASE:?}" "${BRANCH_PREFIX:?}" "${CONTROL_DIR:?}" "${QUEUE:?}" "${BRIEF_DIR:?}"
BANNED_TERMS="${BANNED_TERMS:-humanizer|bypass|undetect|detector|evade}"
BANNED_PATHS="${BANNED_PATHS:-app components public lib}"

echo "=== preflight: wave=${WAVE:-?} base=$BASE repo=$REPO ==="
cd "$REPO" || fail "cannot cd into REPO=$REPO"

# 1) tooling -----------------------------------------------------------------
for t in codex gh git jq; do command -v "$t" >/dev/null 2>&1 || fail "missing tool: $t"; done
echo "  [ok] tooling: codex gh git jq"

# 2) auth + transport --------------------------------------------------------
gh auth status >/dev/null 2>&1 || fail "gh not authenticated (run: gh auth login)"
ORIGIN_URL=$(git remote get-url origin 2>/dev/null || echo "")
case "$ORIGIN_URL" in
  git@*|ssh://*) fail "origin is SSH ($ORIGIN_URL); SSH:22 times out from the iCloud Desktop. Switch to HTTPS: git remote set-url origin https://github.com/$GHREPO.git" ;;
  https://*)     echo "  [ok] origin is HTTPS" ;;
  "")            fail "no git 'origin' remote" ;;
  *)             echo "  [warn] unrecognized origin URL: $ORIGIN_URL (continuing)" ;;
esac
gh auth setup-git >/dev/null 2>&1 || echo "  [warn] gh auth setup-git failed (push may still work if a credential helper is set)"
echo "  [ok] gh authenticated"

# 3) integration base branch exists + is not the default/protected branch ----
DEFBR=$(gh repo view "$GHREPO" --json defaultBranchRef -q .defaultBranchRef.name 2>/dev/null || echo "main")
[ "$BASE" = "$DEFBR" ] && fail "BASE=$BASE is the repo default branch ($DEFBR) — the wave must target an integration branch, NEVER main/default"
[ "$BASE" = "main" ]   && fail "BASE=main is forbidden — use an integration branch"
if git show-ref --verify --quiet "refs/heads/$BASE" \
   || git ls-remote --exit-code --heads origin "$BASE" >/dev/null 2>&1; then
  echo "  [ok] integration base '$BASE' exists (default branch is '$DEFBR')"
else
  fail "integration base '$BASE' does not exist locally or on origin. Create it first: git branch $BASE && git push -u origin $BASE"
fi

# 4) queue parses + briefs exist --------------------------------------------
[ -f "$QUEUE" ] || fail "queue not found: $QUEUE"
rows=0; missing=""
while IFS=$'\t' read -r ISSUE TAG TIER T1M DEPS GLOB TMO; do
  case "$ISSUE" in ''|\#*) continue;; esac
  rows=$((rows+1))
  [ -z "$TAG" ] || [ -z "$TIER" ] && fail "queue row for issue '$ISSUE' is malformed (need TAB-separated: ISSUE TAG TIER TIER1_MERGE DEPS BRIEF_GLOB TIMEOUT_MIN)"
  # brief existence (glob-tolerant); docs/whole-wave configs may legitimately omit a brief dir,
  # but if BRIEF_DIR is set we require the referenced file to resolve.
  if [ -n "$GLOB" ] && [ "$GLOB" != "-" ]; then
    # shellcheck disable=SC2086
    if ! ls $BRIEF_DIR/$GLOB >/dev/null 2>&1; then missing="$missing\n    #$ISSUE -> $BRIEF_DIR/$GLOB"; fi
  fi
done < "$QUEUE"
[ "$rows" -ge 1 ] || fail "queue has zero actionable rows"
[ -n "$missing" ] && fail "missing brief file(s):${missing}"
echo "  [ok] queue parses: $rows issue(s); all briefs present"

# 5) GATE DRY-RUN on the base tree (THE systemic-bug catcher) ----------------
# Create a scratch worktree at the base tip and run the EXACT diff-scoped banned-term gate the
# driver uses, against an empty diff. It MUST come back clean. If it flags a pre-existing match,
# the gate is tree-wide / mis-scoped and would block every issue — abort now, not after issue #1.
DRY="$CONTROL_DIR/_preflight_wt"
git worktree remove --force "$DRY" >/dev/null 2>&1 || true
BASEREF="$BASE"; git show-ref --verify --quiet "refs/heads/$BASE" || BASEREF="origin/$BASE"
git fetch origin "$BASE" --quiet 2>/dev/null || true
if ! git worktree add -f --detach "$DRY" "$BASEREF" >/dev/null 2>&1; then
  fail "could not create dry-run worktree at base ref $BASEREF"
fi
gate_rc=0
(
  cd "$DRY" || exit 9
  # IDENTICAL scoping to driver.sh verify_issue Gate 1: only lines ADDED vs base + new untracked
  # files, under BANNED_PATHS. On a clean base worktree this set is EMPTY -> must not match.
  # shellcheck disable=SC2086
  banned_added=$( { git diff "$BASEREF" -- $BANNED_PATHS 2>/dev/null
                    for uf in $(git ls-files --others --exclude-standard -- $BANNED_PATHS 2>/dev/null); do
                      git diff --no-index -- /dev/null "$uf" 2>/dev/null
                    done
                  } | grep -E '^\+' | grep -vE '^\+\+\+' )
  if printf '%s' "$banned_added" | grep -iqE "$BANNED_TERMS"; then
    echo "  [FAIL] banned-term gate flagged the CLEAN base tree — it is mis-scoped (would block every issue):"
    printf '%s\n' "$banned_added" | grep -inE "$BANNED_TERMS" | head -5 | sed 's/^/        /'
    exit 1
  fi
  # sanity: a whole-tree grep almost certainly DOES match pre-existing fixtures; prove the
  # diff-scoping is what saves us by reporting (not failing on) any tree-wide hits.
  # shellcheck disable=SC2086
  treehits=$(grep -RniE "$BANNED_TERMS" $BANNED_PATHS 2>/dev/null | wc -l | tr -d ' ')
  echo "  [ok] banned-term gate is diff-scoped and PASSES on the empty base diff"
  echo "       (note: a naive tree-wide grep would match $treehits pre-existing line(s) — diff-scoping is why issues won't falsely block)"
  exit 0
) || gate_rc=$?
git worktree remove --force "$DRY" >/dev/null 2>&1 || true
[ "$gate_rc" -eq 0 ] || fail "banned-term gate dry-run failed on the clean base tree (mis-scoped gate)"

echo "=== preflight PASSED ==="
note info "preflight" "passed: $rows issue(s), base=$BASE, gates dry-run clean"
exit 0
