#!/bin/bash
# Eval-only A/B smoke (22 cases, V0-V4): engine run + semantic re-score per variant.
# Pangram is deferred to gate survivors (don't pay to detect-score a facts-regressing variant).
# No prod change; the eval tool reads keys from .env.local. Prod settings: Sapling, target 20,
# max 10, floor 40; prod-realistic payload (draft + tone only).
cd /Users/qc/Desktop/CloudFlare || exit 1

IDS="rewrite-draft-003,rewrite-draft-002,rewrite-draft-005,rewrite-draft-028,rewrite-draft-006,rewrite-draft-041,rewrite-draft-045,rewrite-draft-061,rewrite-draft-071,rewrite-draft-074,rewrite-draft-080,rewrite-draft-042,rewrite-draft-066,rewrite-draft-049,aidc-201,aidc-202,aidc-203,aidc-204,aidc-205,aidc-206,aidc-207,aidc-208"
CASES="docs/rewrite-email-eval-cases-100.md,docs/ai-draft-cleanup-baseline-cases.md"
PROJ="backend-dotnet/tools/ReplyInMyVoice.Eval"

declare -A OUT
for V in v0 v1 v2 v3 v4; do
  echo "########## ENGINE $V ##########"
  WRITING_SIGNAL_PROVIDER=sapling EVAL_MODE=focused EVAL_VARIANT=$V \
    EVAL_CASES_PATH="$CASES" EVAL_CASE_IDS="$IDS" \
    EVAL_TARGET_AI_LIKE=20 EVAL_MAX_ATTEMPTS=10 NATURALNESS_THRESHOLD=40 \
    dotnet run --project "$PROJ" > "/tmp/ab-engine-$V.log" 2>&1
  OUT[$V]=$(grep -oE "/[^ ]+-focused-$V\.json" "/tmp/ab-engine-$V.log" | head -1)
  echo "$V engine done -> ${OUT[$V]}"
  grep -E "Summary:" "/tmp/ab-engine-$V.log"
done

echo
for V in v0 v1 v2 v3 v4; do
  echo "########## SEMANTIC $V (${OUT[$V]}) ##########"
  if [ -z "${OUT[$V]}" ]; then echo "  (no output json for $V — engine run failed; see /tmp/ab-engine-$V.log)"; continue; fi
  EVAL_SEMANTIC_RESCORE_FILES="${OUT[$V]}" EVAL_CASES_PATH="$CASES" \
    dotnet run --project "$PROJ" > "/tmp/ab-sem-$V.log" 2>&1
  grep -E "SEMANTIC \(C#\)|fact FN|fact FP|forbid|wrote " "/tmp/ab-sem-$V.log"
done

echo
echo "===== OUTPUT JSONs (for Pangram on survivors) ====="
for V in v0 v1 v2 v3 v4; do echo "$V ${OUT[$V]}"; done
echo "AB_DONE at $(date +%H:%M:%S)"
