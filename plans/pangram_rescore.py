#!/usr/bin/env python3
"""Rescore the adaptive full-100 rewrites through Pangram once each (no loop).
Compares each rewrite's Sapling score (from the eval JSON) against Pangram's
mean-per-window ai_assistance_score and document fraction_ai. Read-only except
for Pangram API calls; the API key is read from .env.local and never printed."""
import json, os, sys, statistics, concurrent.futures, subprocess

REWRITES_JSON = "docs/rewrite-eval-results/20260526-023901-csharp-rewrite-full.json"
OUT = "docs/rewrite-eval-results/pangram-rescore-20260526.json"

key = None
with open(".env.local") as f:
    for line in f:
        s = line.strip()
        if s.startswith("PANGRAM_API_KEY="):
            key = s.split("=", 1)[1].strip().strip('"').strip("'")
            break
if not key:
    print("ERROR: PANGRAM_API_KEY not found in .env.local"); sys.exit(1)

rows = json.load(open(REWRITES_JSON))["rows"]

def score(row):
    text = (row.get("RewrittenText") or "").strip()
    sap = row.get("RewriteAiLikePercent")
    if not text:
        return {"id": row["Id"], "sapling": sap, "err": "empty"}
    body = json.dumps({"text": text})
    # Route HTTPS through curl so it uses the macOS system cert store (Python urllib can't
    # verify certs here, and certifi isn't installed). Body via stdin to keep it off argv.
    try:
        p = subprocess.run(
            ["curl", "-s", "-X", "POST", "https://text.api.pangram.com/v3",
             "-H", f"x-api-key: {key}", "-H", "Content-Type: application/json",
             "--data-binary", "@-", "--max-time", "90"],
            input=body, capture_output=True, text=True)
        if p.returncode != 0 or not p.stdout.strip():
            return {"id": row["Id"], "sapling": sap, "err": f"curl_rc{p.returncode}: {p.stderr[:120]}"}
        j = json.loads(p.stdout)
        wins = [w.get("ai_assistance_score") for w in (j.get("windows") or [])
                if isinstance(w.get("ai_assistance_score"), (int, float))]
        mean_win = round(statistics.mean(wins) * 100) if wins else None
        return {"id": row["Id"], "category": row.get("Category"), "sapling": sap,
                "pangram_meanwin": mean_win,
                "pangram_fraction_ai": round((j.get("fraction_ai") or 0) * 100),
                "pangram_fraction_ai_assisted": round((j.get("fraction_ai_assisted") or 0) * 100),
                "prediction": j.get("prediction_short") or j.get("headline"), "err": None}
    except Exception as e:
        return {"id": row["Id"], "sapling": sap, "err": f"{type(e).__name__}: {e}"}

results = []
with concurrent.futures.ThreadPoolExecutor(max_workers=8) as ex:
    for i, res in enumerate(ex.map(score, rows), 1):
        results.append(res)
        if i % 10 == 0:
            print(f"...{i}/{len(rows)} scored", flush=True)

results.sort(key=lambda x: x["id"])
json.dump(results, open(OUT, "w"), indent=2)

ok = [r for r in results if r.get("pangram_meanwin") is not None]
errs = [r for r in results if r.get("err")]
mw = [r["pangram_meanwin"] for r in ok]
fa = [r["pangram_fraction_ai"] for r in ok]
sap = [r["sapling"] for r in ok if r["sapling"] is not None]

def lt(v, t): return sum(1 for x in v if x < t)
print("\n===== PANGRAM RESCORE OF OUR 100 ADAPTIVE REWRITES =====")
print(f"scored OK: {len(ok)}/{len(results)}   errors: {len(errs)}")
if errs:
    from collections import Counter
    print("error kinds:", Counter(e['err'].split(':')[0] for e in errs))
if ok:
    print(f"\nPangram mean-per-window AI%:  median={statistics.median(mw)}  mean={round(statistics.mean(mw),1)}")
    print(f"  <15: {lt(mw,15)}   <25: {lt(mw,25)}   <40: {lt(mw,40)}   <50: {lt(mw,50)}   >=90: {sum(1 for x in mw if x>=90)}")
    print(f"Pangram fraction_ai %:        median={statistics.median(fa)}  mean={round(statistics.mean(fa),1)}")
    print(f"  ==0: {sum(1 for x in fa if x==0)}   <25: {lt(fa,25)}   >=90: {sum(1 for x in fa if x>=90)}   ==100: {sum(1 for x in fa if x>=100)}")
    print(f"Sapling AI% (same rewrites):  median={statistics.median(sap)}  mean={round(statistics.mean(sap),1)}   <25: {lt(sap,25)}")
    print("\n-- 12 biggest Sapling->Pangram disagreements (mean-win) --")
    gap = sorted(ok, key=lambda r: (r["pangram_meanwin"] - (r["sapling"] or 0)), reverse=True)[:12]
    print(f"{'case':<20}{'cat':<22}{'sapling':>8}{'pangram_mw':>12}{'frac_ai':>9}")
    for r in gap:
        print(f"{r['id']:<20}{(r.get('category') or ''):<22}{r['sapling']:>7}%{r['pangram_meanwin']:>11}%{r['pangram_fraction_ai']:>8}%")
print(f"\nwrote {OUT}")
