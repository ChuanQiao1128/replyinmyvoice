#!/usr/bin/env bash
# Run the full 100-case rewrite eval against the PRODUCTION C# engine
# (FactReconstructRewriteProvider, in-process). 10 shards x 10 cases, 2 waves of 5
# concurrent (<=5 to respect DeepSeek/Sapling rate limits), EVAL_MAX_ATTEMPTS=4 to cap
# cost. Each worker writes to its own EVAL_OUTPUT_DIR (timestamp filenames would collide
# otherwise). No resume — a crashed shard is detected by a missing worker-k JSON and can
# be re-run on its own. Needs network + .env.local (DEEPSEEK_API_KEY, SAPLING_API_KEY).
set -uo pipefail

ROOT="/Users/qc/Desktop/CloudFlare"
cd "$ROOT"
CORPUS="$ROOT/docs/rewrite-email-eval-cases-100.md"
PROJ="$ROOT/backend-dotnet/tools/ReplyInMyVoice.Eval/ReplyInMyVoice.Eval.csproj"
DLL="$ROOT/backend-dotnet/tools/ReplyInMyVoice.Eval/bin/Release/net8.0/ReplyInMyVoice.Eval.dll"
STAMP="$(date +%Y%m%d-%H%M%S)"
OUTROOT="$ROOT/docs/rewrite-eval-results/run-$STAMP"
mkdir -p "$OUTROOT"
echo "$OUTROOT" > "$ROOT/plans/overnight-eval-latest-outroot.txt"
echo "[run] start $STAMP  outroot=$OUTROOT"

dotnet build "$PROJ" -c Release --nologo -v quiet >/dev/null 2>&1 || { echo "[run] BUILD FAILED"; exit 1; }

shard_ids() {
  local k=$1 ids="" i n
  for i in $(seq 1 10); do
    n=$(printf "%03d" $(( k * 10 + i )))
    ids="${ids:+$ids,}rewrite-draft-$n"
  done
  echo "$ids"
}

run_shard() {
  local k=$1
  EVAL_MAX_ATTEMPTS="${EVAL_MAX_ATTEMPTS:-4}" \
  EVAL_CASES_PATH="$CORPUS" \
  EVAL_CASE_IDS="$(shard_ids "$k")" \
  EVAL_OUTPUT_DIR="$OUTROOT/worker-$k" \
  dotnet "$DLL" >"$OUTROOT/worker-$k.log" 2>&1
  echo "[run] shard $k exit=$?"
}

for wave in "0 1 2 3 4" "5 6 7 8 9"; do
  echo "[run] wave: $wave"
  for k in $wave; do run_shard "$k" & done
  wait
done

echo "[run] ===== per-shard summaries ====="
grep -h "^Summary:" "$OUTROOT"/worker-*.log 2>/dev/null | tee "$OUTROOT/_summaries.txt"
produced=$(ls "$OUTROOT"/worker-*/*.json 2>/dev/null | wc -l | tr -d ' ')
echo "[run] shard JSON files produced: $produced / 10"
echo "[run] DONE outroot=$OUTROOT"
