#!/usr/bin/env python3
"""Offline AI-detection snapshot (Pangram) on the 22 baseline cases.

One score per text (draft + final output), no feedback, no best-of-N, no per-email target —
distribution only. Answers the release-level question: did the rewrite actually LOWER
AI-detection risk vs the draft, and what's the baseline output distribution? Reads
PANGRAM_API_KEY from .env.local; HTTPS via curl (system cert store). Read-only + writes a report."""
import json, re, subprocess, statistics, sys

JSONS = [
    "docs/rewrite-eval-results/20260526-170606-csharp-rewrite-focused.json",
    "docs/rewrite-eval-results/20260526-170733-csharp-rewrite-focused.json",
    "docs/rewrite-eval-results/20260526-171031-csharp-rewrite-focused.json",
]
CASE_FILES = ["docs/rewrite-email-eval-cases-100.md", "docs/ai-draft-cleanup-baseline-cases.md"]
OUT = "docs/rewrite-eval-results/ai-draft-baseline-pangram-snapshot.md"

def env(name, d=None):
    try:
        for line in open(".env.local"):
            s = line.strip()
            if s.startswith(name + "="):
                return s.split("=", 1)[1].strip().strip('"').strip("'")
    except FileNotFoundError:
        pass
    return d

KEY = env("PANGRAM_API_KEY")
if not KEY:
    sys.exit("ERROR: PANGRAM_API_KEY not in .env.local")

POLICY = re.compile(r"\b(refund|credit|charge|invoice|subscription|seat|transfer|enrollment|registration|eligible|eligibility|availability|policy|cancel|cancellation|confirm|confirmation|approval|approve|no change|without confirmation)\b", re.I)
LISTQ = re.compile(r'(?m)^\s*(?:\d+[.)]|[-*•]|>|")')
MESSY = re.compile(r"\b(forwarded message|from:|sent:|subject:|wrote:|on .+ wrote)\b", re.I)
WORD = re.compile(r"[\w'\-]+", re.U)
def wc(t): return len(WORD.findall(t or ""))
def strategy(d):
    if MESSY.search(d): return "MessyThreadCleanup"
    if LISTQ.search(d): return "QuoteListSafe"
    if POLICY.search(d): return "SupportPolicyOptions"
    n = wc(d)
    return "MinimalPolish" if n <= 35 else ("FullStructureRewrite" if n >= 120 else "FactsFirstReconstruct")

def section(block, name):
    m = re.search(rf"#### {name}\s*\n(.*?)(?:\n#### |\Z)", block, re.S)
    return m.group(1).strip() if m else ""
drafts = {}
for f in CASE_FILES:
    try: md = open(f, encoding="utf-8").read()
    except FileNotFoundError: continue
    for b in re.split(r"(?m)^### Case ", md)[1:]:
        mid = re.search(r"^- id:\s*(\S+)", b, re.M)
        if mid: drafts[mid.group(1).strip()] = section(b, "input_draft")

def pangram(text):
    if not (text or "").strip(): return None
    p = subprocess.run(
        ["curl", "-s", "-X", "POST", "https://text.api.pangram.com/v3", "-H", f"x-api-key: {KEY}",
         "-H", "Content-Type: application/json", "--data-binary", "@-", "--max-time", "90"],
        input=json.dumps({"text": text}), capture_output=True, text=True)
    if p.returncode != 0 or not p.stdout.strip(): return None
    try:
        j = json.loads(p.stdout)
        wins = [w.get("ai_assistance_score") for w in (j.get("windows") or []) if isinstance(w.get("ai_assistance_score"), (int, float))]
        return round(statistics.mean(wins) * 100) if wins else round((j.get("fraction_ai") or 0) * 100)
    except Exception:
        return None

rows = []
for jf in JSONS:
    rows += json.load(open(jf))["rows"]

recs = []
for i, r in enumerate(rows, 1):
    cid = r["Id"]; draft = drafts.get(cid, ""); out = r.get("RewrittenText") or ""
    recs.append({"id": cid, "strategy": strategy(draft), "in_wc": wc(draft), "out_wc": wc(out),
                 "draft_pg": pangram(draft), "out_pg": pangram(out)})
    print(f"  {i}/{len(rows)} {cid} scored", flush=True)

def pct(vals, p):
    v = sorted(x for x in vals if x is not None)
    if not v: return None
    k = (len(v) - 1) * p
    f = int(k); return v[f] if f + 1 >= len(v) else round(v[f] + (v[f+1]-v[f])*(k-f))
def dist(vals):
    v = [x for x in vals if x is not None]
    if not v: return "n/a"
    return f"median={pct(v,.5)} p75={pct(v,.75)} p90={pct(v,.9)} >=90={sum(x>=90 for x in v)}/{len(v)} >=50={sum(x>=50 for x in v)}/{len(v)} <25={sum(x<25 for x in v)}/{len(v)}"

out_pg = [x["out_pg"] for x in recs]
draft_pg = [x["draft_pg"] for x in recs]
def avg(v): v=[x for x in v if x is not None]; return round(statistics.mean(v),1) if v else None

lines = ["# AI Draft Cleanup baseline — Pangram snapshot (offline, 1 score/text)", "",
         "Release-level AI-detection-risk baseline of the CURRENT engine. One Pangram score per text; no feedback, no best-of-N, no per-email target.", "",
         f"## Output distribution (the baseline to beat)\n{dist(out_pg)}",
         f"\n## Draft distribution (for reference)\n{dist(draft_pg)}",
         f"\n## Did the rewrite lower detection risk? mean draft={avg(draft_pg)} -> mean output={avg(out_pg)} | outputs lower than their draft: {sum(1 for x in recs if x['draft_pg'] is not None and x['out_pg'] is not None and x['out_pg'] < x['draft_pg'])}/{len(recs)}",
         "\n## Output distribution by router strategy"]
from collections import defaultdict
bys = defaultdict(list)
for x in recs: bys[x["strategy"]].append(x["out_pg"])
for k, v in sorted(bys.items()): lines.append(f"- {k} ({len(v)}): {dist(v)}")
lines.append("\n## Output distribution by input length")
for name, lo, hi in [("short <=35", 0, 35), ("mid 36-119", 36, 119), ("long >=120", 120, 10**9)]:
    v = [x["out_pg"] for x in recs if lo <= x["in_wc"] <= hi]
    if v: lines.append(f"- {name} ({len(v)}): {dist(v)}")
lines.append("\n## per-case (draft_pg -> out_pg)")
lines.append("| id | strategy | in_wc | out_wc | draft_pg | out_pg |\n|---|---|---|---|---|---|")
for x in recs: lines.append(f"| {x['id']} | {x['strategy']} | {x['in_wc']} | {x['out_wc']} | {x['draft_pg']} | {x['out_pg']} |")
open(OUT, "w").write("\n".join(lines))

print("\n===== PANGRAM SNAPSHOT =====")
print("OUTPUT dist:", dist(out_pg))
print("DRAFT  dist:", dist(draft_pg))
print(f"mean draft {avg(draft_pg)} -> mean output {avg(out_pg)}; outputs lower than draft: {sum(1 for x in recs if x['draft_pg'] is not None and x['out_pg'] is not None and x['out_pg']<x['draft_pg'])}/{len(recs)}")
for k, v in sorted(bys.items()): print(f"  {k}: {dist(v)}")
print(f"wrote {OUT}")
