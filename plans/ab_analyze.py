#!/usr/bin/env python3
"""Protocol-compliant A/B readout for the eval-only variant experiment (V0-V4, 22-case smoke).

Per docs/rewrite-eval-results/ab-readout-protocol.md:
  0) confirm V0 reproduces the trustworthy baseline
  1) semantic hard gate per variant (facts >= V0, forbidden=0, meaning=0, no new material loss)
  2) Pangram ONLY on gate survivors + V0 (median/p75/p90/%>=90), never mean/single-case
  2b) paired same-case delta per survivor vs V0
  3) decision table {kill, needs larger eval, candidate}

Offline; reads keys from .env.local; HTTPS via curl. No engine re-run (uses the 5 saved outputs)."""
import json, re, statistics, subprocess, sys
from collections import OrderedDict

VARIANTS = OrderedDict([
    ("v0", ("baseline (current engine)", "docs/rewrite-eval-results/20260526-225635-csharp-rewrite-focused-v0.json")),
    ("v1", ("short-skeleton-trim", "docs/rewrite-eval-results/20260526-225936-csharp-rewrite-focused-v1.json")),
    ("v2", ("no-default-greeting/signoff", "docs/rewrite-eval-results/20260526-230241-csharp-rewrite-focused-v2.json")),
    ("v3", ("facts-first routing", "docs/rewrite-eval-results/20260526-230600-csharp-rewrite-focused-v3.json")),
    ("v4", ("combined (v2+v3)", "docs/rewrite-eval-results/20260526-230923-csharp-rewrite-focused-v4.json")),
])
CASE_FILES = ["docs/rewrite-email-eval-cases-100.md", "docs/ai-draft-cleanup-baseline-cases.md"]
OUT = "docs/rewrite-eval-results/ab-readout.md"

def env(name, d=None):
    try:
        for line in open(".env.local"):
            s = line.strip()
            if s.startswith(name + "="):
                return s.split("=", 1)[1].strip().strip('"').strip("'")
    except FileNotFoundError:
        pass
    return d

DKEY = env("DEEPSEEK_API_KEY") or env("OPENAI_API_KEY")
BASE = (env("OPENAI_BASE_URL", "https://api.deepseek.com") or "https://api.deepseek.com").rstrip("/")
MODEL = env("OPENAI_MODEL_MID_WRITER") or env("OPENAI_MODEL") or "deepseek-v4-pro"
PKEY = env("PANGRAM_API_KEY")
DURL = BASE + ("/chat/completions" if BASE.endswith("/v1") else "/v1/chat/completions")
if not DKEY or not PKEY:
    sys.exit("missing DEEPSEEK_API_KEY or PANGRAM_API_KEY in .env.local")

WORD = re.compile(r"[\w'\-]+", re.U)
def wc(t): return len(WORD.findall(t or ""))
MATERIAL = re.compile(r"\$|\d|\b(January|February|March|April|May|June|July|August|September|October|November|December|Monday|Tuesday|Wednesday|Thursday|Friday|Saturday|Sunday)\b|[A-Z]{2,}|[A-Z][A-Za-z]+-?\d")
def is_material(fact): return bool(MATERIAL.search(fact or ""))

def section(block, name):
    m = re.search(rf"#### {name}\s*\n(.*?)(?:\n#### |\Z)", block, re.S)
    return m.group(1).strip() if m else ""
def items(t): return [ln[2:].strip() for ln in t.splitlines() if ln.strip().startswith("- ")]
CASES = {}
for f in CASE_FILES:
    try: md = open(f, encoding="utf-8").read()
    except FileNotFoundError: continue
    for b in re.split(r"(?m)^### Case ", md)[1:]:
        mid = re.search(r"^- id:\s*(\S+)", b, re.M)
        if mid:
            CASES[mid.group(1).strip()] = {"draft": section(b, "input_draft"),
                                           "mk": items(section(b, "must_keep")), "mn": items(section(b, "must_not_claim"))}

SYS = ("You are a strict evaluation judge for an email-rewriting system. Decide whether a REWRITE preserved each "
    "required fact and made no forbidden claim. (1) amounts/money/dates/deadlines/invoice numbers/IDs/codes/proper "
    "names: EXACT preservation - a changed/dropped one = missing or contradicted. (2) statuses/actions/intent/requests: "
    "accept semantic equivalence ('set'~'confirmed','wrapped up'~'finished'). (3) a must_not_claim is violated ONLY if the "
    "rewrite actually asserts the forbidden thing, changes an amount/time/liability, or turns uncertainty into certainty - "
    "'refund in 3-5 business days' does NOT violate 'no instant refund'. Return JSON only.")
def judge(rw, mk, mn):
    body = {"model": MODEL, "temperature": 0, "response_format": {"type": "json_object"}, "max_tokens": 1600,
        "messages": [{"role": "system", "content": SYS}, {"role": "user", "content":
            f"REWRITE:\n{rw}\n\nMUST_KEEP:\n" + "\n".join("- "+x for x in mk) + "\n\nMUST_NOT_CLAIM:\n" + "\n".join("- "+x for x in mn) +
            '\n\nJSON: {"facts":[{"fact":"...","status":"preserved|missing|contradicted|unverifiable","reason":"..."}],'
            '"forbidden":[{"rule":"...","violated":true,"reason":"..."}],"meaning_changed":false,"send_ready":true}'}]}
    if "deepseek" in BASE: body["thinking"] = {"type": "disabled"}
    p = subprocess.run(["curl","-s","-X","POST",DURL,"-H",f"Authorization: Bearer {DKEY}","-H","Content-Type: application/json","--data-binary","@-","--max-time","90"],
                       input=json.dumps(body), capture_output=True, text=True)
    try:
        return json.loads(json.loads(p.stdout)["choices"][0]["message"]["content"])
    except Exception as e:
        return {"err": str(e)[:80]}
def pangram(text):
    if not (text or "").strip(): return None
    p = subprocess.run(["curl","-s","-X","POST","https://text.api.pangram.com/v3","-H",f"x-api-key: {PKEY}","-H","Content-Type: application/json","--data-binary","@-","--max-time","90"],
                       input=json.dumps({"text": text}), capture_output=True, text=True)
    try:
        j = json.loads(p.stdout); w = [x.get("ai_assistance_score") for x in (j.get("windows") or []) if isinstance(x.get("ai_assistance_score"),(int,float))]
        return round(statistics.mean(w)*100) if w else round((j.get("fraction_ai") or 0)*100)
    except Exception:
        return None

def rows_of(path):
    out = {}
    for r in json.load(open(path))["rows"]:
        out[r["Id"]] = r.get("RewrittenText") or ""
    return out

# ---- judge every variant ----
data = {}
for v, (desc, path) in VARIANTS.items():
    rws = rows_of(path); per = {}
    for cid, rw in rws.items():
        c = CASES.get(cid, {"mk": [], "mn": []})
        j = judge(rw, c["mk"], c["mn"])
        facts = j.get("facts", []) if "err" not in j else []
        bad = [f for f in facts if f.get("status") in ("missing", "contradicted")]
        per[cid] = {"rw": rw, "pass": ("err" not in j) and not bad,
                    "material_loss": [f for f in bad if is_material(f.get("fact",""))],
                    "minor_loss": [f for f in bad if not is_material(f.get("fact",""))],
                    "forbid": sum(1 for x in j.get("forbidden", []) if x.get("violated")),
                    "meaning": bool(j.get("meaning_changed")), "send": bool(j.get("send_ready")), "err": j.get("err")}
    data[v] = per
    print(f"judged {v} ({len(per)} cases)", flush=True)

def agg(v):
    per = data[v]; n = len(per)
    return {"n": n, "pass": sum(x["pass"] for x in per.values()),
            "material": [c for c, x in per.items() if x["material_loss"]],
            "minor": [c for c, x in per.items() if x["minor_loss"] and not x["material_loss"]],
            "forbid": sum(x["forbid"] for x in per.values()),
            "meaning": sum(x["meaning"] for x in per.values()),
            "send": sum(x["send"] for x in per.values())}
A = {v: agg(v) for v in VARIANTS}
v0pass = A["v0"]["pass"]

# ---- gate ----
survivors = []
for v in VARIANTS:
    if v == "v0": continue
    a = A[v]
    gate = (not a["material"]) and a["forbid"] == 0 and a["meaning"] == 0 and a["pass"] >= v0pass and a["send"] == a["n"]
    if gate: survivors.append(v)

# ---- Pangram on V0 + survivors ----
pg = {}
for v in ["v0"] + survivors:
    pg[v] = {cid: pangram(x["rw"]) for cid, x in data[v].items()}
    print(f"pangram {v}", flush=True)

def dist(scores):
    s = sorted(x for x in scores if x is not None)
    if not s: return "n/a"
    def q(p): k=(len(s)-1)*p; f=int(k); return s[f] if f+1>=len(s) else round(s[f]+(s[f+1]-s[f])*(k-f))
    return f"median={q(.5)} p75={q(.75)} p90={q(.9)} %>=90={round(100*sum(x>=90 for x in s)/len(s))}% %>=95={round(100*sum(x>=95 for x in s)/len(s))}% (mean={round(statistics.mean(s),1)} ref)"

# ---- report ----
L = ["# A/B readout (eval-only, 22-case smoke; protocol-locked)", "",
     f"Judge {MODEL}; Pangram mean-window. n per variant varies with engine success.", "",
     "## Step 0 — V0 reproducibility",
     f"V0 semantic facts {A['v0']['pass']}/{A['v0']['n']}, forbidden {A['v0']['forbid']}, meaning {A['v0']['meaning']}, send-ready {A['v0']['send']}/{A['v0']['n']}; Pangram: {dist(list(pg['v0'].values()))}",
     "(expected ~21/22 facts, 0 forbidden, median ~96 — compare.)", "",
     "## Step 1 — semantic hard gate", "",
     "| variant | desc | facts pass | material loss | minor loss | forbidden | meaning | send-ready | gate |",
     "|---|---|---|---|---|---|---|---|---|"]
for v, (desc, _) in VARIANTS.items():
    a = A[v]; gate = "V0" if v == "v0" else ("PASS" if v in survivors else "kill")
    L.append(f"| {v} | {desc} | {a['pass']}/{a['n']} | {','.join(a['material']) or '-'} | {len(a['minor'])} | {a['forbid']} | {a['meaning']} | {a['send']}/{a['n']} | {gate} |")
L += ["", f"Survivors (Pangram-scored): {', '.join(survivors) or 'NONE'}", "", "## Step 2/2b — Pangram on survivors + V0 (paired vs V0)"]
for v in survivors:
    L.append(f"\n### {v} — {VARIANTS[v][0]}\nOutput Pangram: {dist(list(pg[v].values()))}")
    imp = wor = unc = hr2n = n2hr = 0
    L.append("| case | V0 | "+v+" | delta | hr V0 | hr "+v+" | facts |\n|---|---|---|---|---|---|---|")
    for cid in pg["v0"]:
        a, b = pg["v0"].get(cid), pg[v].get(cid)
        if a is None or b is None: continue
        d = b - a; hr0, hrv = a >= 90, b >= 90
        if d <= -10: imp += 1
        elif d >= 10: wor += 1
        else: unc += 1
        if hr0 and not hrv: hr2n += 1
        if (not hr0) and hrv: n2hr += 1
        L.append(f"| {cid} | {a} | {b} | {d:+d} | {'Y' if hr0 else '-'} | {'Y' if hrv else '-'} | {'same' if data[v][cid]['pass']==data['v0'].get(cid,{}).get('pass') else 'diff'} |")
    L.append(f"\n**{v} paired:** improved(<=-10) {imp} · worsened(>=+10) {wor} · unchanged {unc} · high-risk->non {hr2n} · non->high-risk {n2hr}")
open(OUT, "w").write("\n".join(L))

print("\n===== A/B READOUT =====")
print(f"V0: facts {A['v0']['pass']}/{A['v0']['n']} forbid {A['v0']['forbid']} meaning {A['v0']['meaning']} | Pangram {dist(list(pg['v0'].values()))}")
for v in VARIANTS:
    a = A[v]
    print(f"{v} {VARIANTS[v][0]}: facts {a['pass']}/{a['n']} material_loss={a['material'] or '-'} minor={len(a['minor'])} forbid {a['forbid']} meaning {a['meaning']} send {a['send']}/{a['n']} -> {'V0' if v=='v0' else ('PASS' if v in survivors else 'kill')}")
for v in survivors:
    print(f"  {v} Pangram: {dist(list(pg[v].values()))}")
print(f"survivors: {survivors}")
print(f"wrote {OUT}")
