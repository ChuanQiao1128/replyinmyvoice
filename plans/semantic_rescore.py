#!/usr/bin/env python3
"""Semantic re-scorer for the AI Draft Cleanup baseline (offline; NO engine re-run).

Replaces the over-literal deterministic fact-matcher + keyword forbidden-screen with an
LLM judge (DeepSeek). Exact-match for amounts/dates/IDs/names; semantic equivalence for
status/action/intent. Re-scores the 22 SAVED outputs and reports where the deterministic
graders gave false negatives (facts) / false positives (forbidden). Reads keys from
.env.local; HTTPS via curl (system cert store). Read-only except the report it writes."""
import json, os, re, subprocess, sys

JSONS = [
    "docs/rewrite-eval-results/20260526-170606-csharp-rewrite-focused.json",
    "docs/rewrite-eval-results/20260526-170733-csharp-rewrite-focused.json",
    "docs/rewrite-eval-results/20260526-171031-csharp-rewrite-focused.json",
]
CASE_FILES = ["docs/rewrite-email-eval-cases-100.md", "docs/ai-draft-cleanup-baseline-cases.md"]
OUT = "docs/rewrite-eval-results/ai-draft-baseline-semantic-rescore.md"

def env(name, default=None):
    try:
        for line in open(".env.local"):
            s = line.strip()
            if s.startswith(name + "="):
                return s.split("=", 1)[1].strip().strip('"').strip("'")
    except FileNotFoundError:
        pass
    return os.environ.get(name, default)

KEY = env("DEEPSEEK_API_KEY") or env("OPENAI_API_KEY")
BASE = (env("OPENAI_BASE_URL", "https://api.deepseek.com") or "https://api.deepseek.com").rstrip("/")
MODEL = env("OPENAI_MODEL_MID_WRITER") or env("OPENAI_MODEL") or "deepseek-v4-pro"
if not KEY:
    sys.exit("ERROR: no DEEPSEEK_API_KEY/OPENAI_API_KEY in .env.local")
if BASE.endswith("/chat/completions"):
    URL = BASE
elif BASE.endswith("/v1"):
    URL = BASE + "/chat/completions"
else:
    URL = BASE + "/v1/chat/completions"

def section(block, name):
    m = re.search(rf"#### {name}\s*\n(.*?)(?:\n#### |\Z)", block, re.S)
    return m.group(1).strip() if m else ""

def list_items(text):
    return [ln[2:].strip() for ln in text.splitlines() if ln.strip().startswith("- ")]

cases = {}
for f in CASE_FILES:
    try:
        md = open(f, encoding="utf-8").read()
    except FileNotFoundError:
        continue
    for b in re.split(r"(?m)^### Case ", md)[1:]:
        mid = re.search(r"^- id:\s*(\S+)", b, re.M)
        if not mid:
            continue
        cid = mid.group(1).strip()
        cases[cid] = {
            "draft": section(b, "input_draft"),
            "must_keep": list_items(section(b, "must_keep")),
            "must_not_claim": list_items(section(b, "must_not_claim")),
        }

rows = []
for jf in JSONS:
    rows += json.load(open(jf))["rows"]

SYS = (
    "You are a strict evaluation judge for an email-rewriting system. Decide whether a REWRITE "
    "preserved each required fact from the source and whether it made any forbidden claim. Rules: "
    "(1) For amounts/money/dates/deadlines/invoice numbers/IDs/codes/proper names: require EXACT "
    "preservation — a changed or dropped number, date, or name = missing or contradicted. "
    "(2) For statuses/actions/intent/requests: accept semantic equivalence — e.g. 'set' preserves "
    "'confirmed'; 'wrapped up' preserves 'finished'; 'Would that suit you?' preserves 'asks what works'; "
    "'we can walk through the options' preserves 'changes can be discussed'. "
    "(3) A must_not_claim is violated ONLY if the rewrite actually asserts the forbidden thing, changes "
    "an amount/time/liability, or turns uncertainty into certainty. 'refund in 3-5 business days' does "
    "NOT violate 'no instant refund'. Do not flag a word just because it appears. Return JSON only."
)

def judge(rewrite, mk, mn):
    user = (
        f"REWRITE:\n{rewrite}\n\nMUST_KEEP (each must be preserved):\n"
        + "\n".join("- " + f for f in mk)
        + "\n\nMUST_NOT_CLAIM:\n" + "\n".join("- " + f for f in mn)
        + '\n\nReturn JSON: {"facts":[{"fact":"...","status":"preserved|missing|contradicted|unverifiable",'
        '"evidence_quote":"...","reason":"..."}],"forbidden":[{"rule":"...","violated":true,"reason":"..."}],'
        '"meaning_changed":false,"send_ready":true}'
    )
    body = {"model": MODEL, "temperature": 0, "response_format": {"type": "json_object"},
            "max_tokens": 1600, "messages": [{"role": "system", "content": SYS}, {"role": "user", "content": user}]}
    if "deepseek" in BASE:
        body["thinking"] = {"type": "disabled"}
    p = subprocess.run(
        ["curl", "-s", "-X", "POST", URL, "-H", f"Authorization: Bearer {KEY}",
         "-H", "Content-Type: application/json", "--data-binary", "@-", "--max-time", "90"],
        input=json.dumps(body), capture_output=True, text=True)
    if p.returncode != 0 or not p.stdout.strip():
        return {"err": f"curl_rc{p.returncode}: {p.stderr[:120]}"}
    try:
        content = json.loads(p.stdout)["choices"][0]["message"]["content"]
        return json.loads(content)
    except Exception as e:
        return {"err": f"{type(e).__name__}: {str(e)[:120]}"}

results = []
for i, r in enumerate(rows, 1):
    cid = r["Id"]
    c = cases.get(cid, {"must_keep": [], "must_not_claim": []})
    text = r.get("RewrittenText") or ""
    j = judge(text, c["must_keep"], c["must_not_claim"]) if text else {"err": "empty"}
    det_facts = r.get("FactsPreserved")
    det_forbid = len(r.get("ForbiddenViolations") or [])
    if "err" in j:
        results.append({"id": cid, "err": j["err"], "det_facts": det_facts, "det_forbid": det_forbid})
    else:
        facts = j.get("facts", [])
        bad = [f for f in facts if f.get("status") in ("missing", "contradicted")]
        unver = [f for f in facts if f.get("status") == "unverifiable"]
        viol = [x for x in j.get("forbidden", []) if x.get("violated")]
        results.append({
            "id": cid, "det_facts": det_facts, "det_forbid": det_forbid,
            "sem_facts_pass": len(bad) == 0, "sem_bad": bad, "sem_unver": len(unver),
            "sem_forbid": len(viol), "sem_viol": viol,
            "meaning_changed": j.get("meaning_changed"), "send_ready": j.get("send_ready"),
        })
    print(f"  {i}/{len(rows)} {cid} judged", flush=True)

ok = [x for x in results if "err" not in x]
sem_facts = sum(x["sem_facts_pass"] for x in ok)
det_facts = sum(bool(x["det_facts"]) for x in ok)
sem_forbid = sum(x["sem_forbid"] > 0 for x in ok)
det_forbid = sum(x["det_forbid"] > 0 for x in ok)
fact_FN = [x["id"] for x in ok if x["sem_facts_pass"] and not x["det_facts"]]      # det said fail, really pass
fact_FP = [x["id"] for x in ok if (not x["sem_facts_pass"]) and x["det_facts"]]    # det said pass, really fail
forbid_FP = [x["id"] for x in ok if x["det_forbid"] > 0 and x["sem_forbid"] == 0]  # det flagged, really clean
forbid_FN = [x["id"] for x in ok if x["det_forbid"] == 0 and x["sem_forbid"] > 0]  # det missed a real one

lines = ["# AI Draft Cleanup baseline — semantic re-score (LLM judge)", "",
         f"Judged {len(ok)}/{len(results)} cases ({len(results)-len(ok)} judge errors). Model: {MODEL}.", "",
         f"- **True facts pass (semantic): {sem_facts}/{len(ok)}**  vs deterministic {det_facts}/{len(ok)}",
         f"- deterministic fact FALSE-NEGATIVES (said fail, really preserved): {fact_FN}",
         f"- deterministic fact false-positives (said pass, really lost): {fact_FP}",
         f"- **True forbidden violations (semantic): {sem_forbid}/{len(ok)}**  vs deterministic {det_forbid}/{len(ok)}",
         f"- deterministic forbidden FALSE-POSITIVES (flagged, really clean): {forbid_FP}",
         f"- deterministic forbidden false-negatives (missed a real one): {forbid_FN}",
         f"- meaning_changed: {[x['id'] for x in ok if x.get('meaning_changed')]}",
         f"- send_ready (judge): {sum(bool(x.get('send_ready')) for x in ok)}/{len(ok)}", "",
         "## Cases the judge says really lost/contradicted a fact (the ones that matter)"]
for x in ok:
    if not x["sem_facts_pass"]:
        for b in x["sem_bad"]:
            lines.append(f"- **{x['id']}**: {b.get('status')} — {b.get('fact')} — {b.get('reason')}")
lines += ["", "## Real forbidden violations (judge)"]
for x in ok:
    for v in x.get("sem_viol", []):
        lines.append(f"- **{x['id']}**: {v.get('rule')} — {v.get('reason')}")
errs = [x for x in results if "err" in x]
if errs:
    lines += ["", "## judge errors", *[f"- {x['id']}: {x['err']}" for x in errs]]
open(OUT, "w").write("\n".join(lines))

print("\n===== SEMANTIC RE-SCORE =====")
print(f"true facts pass: {sem_facts}/{len(ok)}  (deterministic said {det_facts}/{len(ok)})")
print(f"deterministic fact false-negatives: {fact_FN}")
print(f"deterministic fact false-positives (real losses det missed): {fact_FP}")
print(f"true forbidden violations: {sem_forbid}/{len(ok)}  (deterministic flagged {det_forbid}/{len(ok)})")
print(f"deterministic forbidden false-positives: {forbid_FP}")
print(f"send_ready(judge): {sum(bool(x.get('send_ready')) for x in ok)}/{len(ok)}  meaning_changed: {[x['id'] for x in ok if x.get('meaning_changed')]}")
print(f"wrote {OUT}")
