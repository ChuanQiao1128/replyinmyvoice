#!/usr/bin/env python3
"""Read-only analysis of the AI Draft Cleanup baseline runs.

Reads the eval output JSONs (current engine, prod-realistic payload) + the two
case files for drafts, derives the router strategy (mirrors RewriteInputAnalyzer),
and computes the metric table the owner asked for. No engine/eval code touched.
"""
import json, re, sys, statistics

JSONS = [
    "docs/rewrite-eval-results/20260526-170606-csharp-rewrite-focused.json",  # A: corpus
    "docs/rewrite-eval-results/20260526-170733-csharp-rewrite-focused.json",  # B: gap (filler)
    "docs/rewrite-eval-results/20260526-171031-csharp-rewrite-focused.json",  # C: MinimalPolish
]
CASE_FILES = [
    "docs/rewrite-email-eval-cases-100.md",
    "docs/ai-draft-cleanup-baseline-cases.md",
]

# Mirror RewriteInputAnalyzer (RewriteEngineCore.cs) exactly.
POLICY = re.compile(r"\b(refund|credit|charge|invoice|subscription|seat|transfer|enrollment|registration|eligible|eligibility|availability|policy|cancel|cancellation|confirm|confirmation|approval|approve|no change|without confirmation)\b", re.I)
LISTQ = re.compile(r'(?m)^\s*(?:\d+[.)]|[-*•]|>|")')
MESSY = re.compile(r"\b(forwarded message|from:|sent:|subject:|wrote:|on .+ wrote)\b", re.I)
WORD = re.compile(r"[\w'\-]+", re.U)

def word_count(t): return len(WORD.findall(t or ""))

def initial_strategy(draft):
    if MESSY.search(draft): return "MessyThreadCleanup"
    if LISTQ.search(draft): return "QuoteListSafe"
    if POLICY.search(draft): return "SupportPolicyOptions"
    wc = word_count(draft)
    if wc <= 35: return "MinimalPolish"
    if wc >= 120: return "FullStructureRewrite"
    return "FactsFirstReconstruct"

OPENERS = ["i hope this email finds you well","i hope this message finds you well","i hope you're doing well","i hope you are doing well","i hope this finds you well","i hope you're having","i wanted to reach out","i wanted to take a moment","i wanted to personally reach out","thank you for reaching out","thanks for reaching out","i just wanted to","i wanted to let you know","i wanted to follow up"]
CLOSERS = ["please don't hesitate","please do not hesitate","please feel free","feel free to","let me know if you have any","please let me know if there","looking forward","thank you so much for your patience","we truly appreciate","thank you so much for considering","i'm always here to help","at your earliest convenience","don't hesitate to reach out"]
FILLER = ["i completely understand","please rest assured","more than happy","every step of the way","we take these matters","whatsoever","valued customer","take a moment","at your earliest convenience"]

def count_phrases(text, phrases):
    t = (text or "").lower()
    return sum(t.count(p) for p in phrases)

def paragraphs(text):
    return [p for p in re.split(r"\n\s*\n", (text or "").strip()) if p.strip()]

GREETING = re.compile(r"^\s*(hi|hello|hey|dear)\b", re.I)
SIGNOFF = re.compile(r"\b(best|regards|best regards|thanks|thank you|sincerely|cheers)\s*,?\s*$", re.I)

def forced_structure(text):
    paras = paragraphs(text)
    has_greet = bool(GREETING.search(text or ""))
    last = paras[-1] if paras else ""
    has_sign = bool(SIGNOFF.search((text or "").strip().splitlines()[-1] if (text or "").strip() else ""))
    return has_greet and has_sign and len(paras) >= 3

def jaccard(a, b):
    sa, sb = set(WORD.findall((a or "").lower())), set(WORD.findall((b or "").lower()))
    if not sa or not sb: return 0.0
    return len(sa & sb) / len(sa | sb)

def parse_drafts(path):
    out = {}
    try:
        md = open(path, encoding="utf-8").read()
    except FileNotFoundError:
        return out
    blocks = re.split(r"(?m)^### Case ", md)[1:]
    for b in blocks:
        mid = re.search(r"^- id:\s*(\S+)", b, re.M)
        md_draft = re.search(r"#### input_draft\s*\n(.*?)\n#### ", b, re.S)
        if mid and md_draft:
            out[mid.group(1).strip()] = md_draft.group(1).strip()
    return out

drafts = {}
for f in CASE_FILES:
    drafts.update(parse_drafts(f))

rows = []
for jf in JSONS:
    data = json.load(open(jf))
    for r in data["rows"]:
        rows.append(r)

def m(r):
    rid = r["Id"]
    draft = drafts.get(rid, "")
    out = r.get("RewrittenText") or ""
    in_wc, out_wc = word_count(draft), word_count(out)
    attempts = (r.get("AttemptsUsed") or 0) + (r.get("FailedAttempts") or 0)
    if attempts == 0:
        attempts = len(r.get("AttemptHistory") or []) + (1 if r.get("Success") else 0)
    return {
        "id": rid,
        "cat": r.get("Category", ""),
        "strategy": initial_strategy(draft),
        "in_wc": in_wc,
        "out_wc": out_wc,
        "compress": round(out_wc / in_wc, 2) if in_wc else None,
        "draft_sap": r.get("DraftAiLikePercent"),
        "final_sap": r.get("RewriteAiLikePercent"),
        "attempts": attempts,
        "sap_calls": 1 + attempts,
        "facts": r.get("FactsPreserved"),
        "missing": r.get("MissingFacts") or [],
        "forbid": len(r.get("ForbiddenViolations") or []),
        "send_ready": r.get("CustomerUsable"),
        "opener": count_phrases(out, OPENERS),
        "closer": count_phrases(out, CLOSERS),
        "filler": count_phrases(out, FILLER),
        "forced": forced_structure(out),
        "too_close": round(jaccard(draft, out), 2),
        "out": out,
    }

ms = [m(r) for r in rows]

# ---- full table to file ----
hdr = ["id","cat","strategy","in_wc","out_wc","compress","draft_sap","final_sap","attempts","sap_calls","facts","forbid","send_ready","opener","closer","filler","forced","too_close"]
lines = ["# AI Draft Cleanup — baseline (current engine, prod-realistic payload)", "",
         "Settings: WRITING_SIGNAL_PROVIDER=sapling, EVAL_TARGET_AI_LIKE=20, EVAL_MAX_ATTEMPTS=10, NATURALNESS_THRESHOLD=40. Payload = {roughDraftReply, tone:warm} only.", "",
         "| " + " | ".join(hdr) + " |", "|" + "|".join(["---"]*len(hdr)) + "|"]
for x in ms:
    lines.append("| " + " | ".join(str(x[h]) for h in hdr) + " |")
lines += ["", "## facts=False cases (real loss vs matcher false-negative — read the text)", ""]
for x in ms:
    if not x["facts"]:
        lines.append(f"### {x['id']} ({x['cat']}, {x['strategy']}) — missing: {x['missing']}")
        lines.append("```\n" + x["out"] + "\n```")
        lines.append(f"draft: ```\n{drafts.get(x['id'],'')}\n```\n")
open("docs/rewrite-eval-results/ai-draft-baseline-analysis.md","w").write("\n".join(lines))

# ---- console summary ----
def grp(pred): return [x for x in ms if pred(x)]
aidc = grp(lambda x: x["id"].startswith("aidc-"))
corpus = grp(lambda x: x["id"].startswith("rewrite-draft-"))
print(f"TOTAL {len(ms)} cases | facts pass {sum(x['facts'] for x in ms)}/{len(ms)} | send_ready {sum(bool(x['send_ready']) for x in ms)}/{len(ms)} | forbidden>0 {sum(x['forbid']>0 for x in ms)}")
print(f"  corpus(14): facts {sum(x['facts'] for x in corpus)}/{len(corpus)} | AI-draft(8): facts {sum(x['facts'] for x in aidc)}/{len(aidc)}")
from collections import Counter
print("strategy dist:", dict(Counter(x['strategy'] for x in ms)))
def avg(vals):
    vals=[v for v in vals if v is not None]; return round(statistics.mean(vals),2) if vals else None
print(f"compression mean: all={avg([x['compress'] for x in ms])} aidc={avg([x['compress'] for x in aidc])} corpus={avg([x['compress'] for x in corpus])}")
print(f"final_sap mean: all={avg([x['final_sap'] for x in ms])} | draft_sap mean: all={avg([x['draft_sap'] for x in ms])}")
print(f"template phrases in OUTPUT (opener+closer+filler) — cases with >0: {sum((x['opener']+x['closer']+x['filler'])>0 for x in ms)}/{len(ms)}")
print(f"forced 3-part structure: {sum(x['forced'] for x in ms)}/{len(ms)}  | output too close to draft (jaccard>0.5): {sum(x['too_close']>0.5 for x in ms)}/{len(ms)}")
print("\nfacts=False:", [(x['id'], x['strategy'], x['missing'][:2]) for x in ms if not x['facts']])
print("forbidden>0:", [(x['id'], x['forbid']) for x in ms if x['forbid']>0])
print("\nMinimalPolish cases (compression + too_close):", [(x['id'], x['compress'], x['too_close'], f"opener+closer+filler={x['opener']+x['closer']+x['filler']}") for x in ms if x['strategy']=="MinimalPolish"])
print("FullStructureRewrite cases (forced struct?):", [(x['id'], x['forced'], x['out_wc'], x['compress']) for x in ms if x['strategy']=="FullStructureRewrite"])
print("\nwrote docs/rewrite-eval-results/ai-draft-baseline-analysis.md")
