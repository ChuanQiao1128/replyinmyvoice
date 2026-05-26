#!/bin/bash
# Eval-only A/B ENGINE runs (V0-V4, 22-case smoke). Scoring (semantic + Pangram) is done by
# plans/ab_analyze.py (Python) — NOT here.
#
# Footgun history: an earlier version used `declare -A` (bash associative arrays), which macOS
# bash 3.2 does not support. It silently collapsed the variant->output mapping so the scoring
# phase re-scored one variant five times. This version is bash-3.2-safe (no associative arrays)
# and only runs the engine + writes a variant->output manifest for the Python analyzer to consume.
cd /Users/qc/Desktop/CloudFlare || exit 1

IDS="rewrite-draft-003,rewrite-draft-002,rewrite-draft-005,rewrite-draft-028,rewrite-draft-006,rewrite-draft-041,rewrite-draft-045,rewrite-draft-061,rewrite-draft-071,rewrite-draft-074,rewrite-draft-080,rewrite-draft-042,rewrite-draft-066,rewrite-draft-049,aidc-201,aidc-202,aidc-203,aidc-204,aidc-205,aidc-206,aidc-207,aidc-208"
CASES="docs/rewrite-email-eval-cases-100.md,docs/ai-draft-cleanup-baseline-cases.md"
PROJ="backend-dotnet/tools/ReplyInMyVoice.Eval"
MANIFEST="/tmp/ab-manifest.txt"
: > "$MANIFEST"

for V in v0 v1 v2 v3 v4; do
  echo "########## ENGINE $V ##########"
  WRITING_SIGNAL_PROVIDER=sapling EVAL_MODE=focused EVAL_VARIANT="$V" \
    EVAL_CASES_PATH="$CASES" EVAL_CASE_IDS="$IDS" \
    EVAL_TARGET_AI_LIKE=20 EVAL_MAX_ATTEMPTS=10 NATURALNESS_THRESHOLD=40 \
    dotnet run --project "$PROJ" > "/tmp/ab-engine-$V.log" 2>&1
  P=$(grep -oE "/[^ ]+-focused-$V\.json" "/tmp/ab-engine-$V.log" | head -1)
  echo "$V $P" >> "$MANIFEST"        # bash-3.2-safe: manifest file, not an associative array
  echo "$V -> $P"
  grep -E "Summary:" "/tmp/ab-engine-$V.log"
done

echo
echo "ENGINE_DONE. Manifest: $MANIFEST"
echo "Now score with: python3 plans/ab_analyze.py   (semantic gate -> Pangram on survivors -> paired delta)"
