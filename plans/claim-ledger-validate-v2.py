#!/usr/bin/env python3
"""ClaimLedger v2 validator — runs the frozen claim-ledger-v1 prompt against 10 corpus cases
   and saves the raw DeepSeek JSON outputs to /tmp/claim_ledger_validation_v2/ for human review.

   This is the offline reference harness. The same prompt + parsing lives in C# at:
       backend-dotnet/src/ReplyInMyVoice.Domain/Quality/ClaimLedgerExtractor.cs
   Both must stay in sync. To re-validate after any prompt edit, run this script first
   (cheap: ~$0.01 DeepSeek) before touching the C# const string.

   The prompt is patched with 3 calibration fixes from the v1 review:
   1. source_span MUST be exact substring (007 C001 was paraphrased in v1)
   2. meta-communication statements are NOT claims (007 C004 was over-extracted in v1)
   3. hedged verbs (expect/hope/think/believe) → uncertainty, not certainty (007 C006 in v1)

   Usage:  cd /Users/qc/Desktop/CloudFlare && python3 plans/claim-ledger-validate-v2.py
   Needs:  DEEPSEEK_API_KEY in .env.local
"""
import os, json, re, sys
from openai import OpenAI

REPO = '/Users/qc/Desktop/CloudFlare'
for line in open(REPO + '/.env.local', errors='ignore'):
    s = line.strip()
    if s and not s.startswith('#') and '=' in s:
        k, v = s.split('=', 1)
        os.environ.setdefault(k.strip(), v.strip().strip('"').strip("'"))

client = OpenAI(api_key=os.environ['DEEPSEEK_API_KEY'], base_url='https://api.deepseek.com')

CASE_IDS = [
    "rewrite-draft-001", "rewrite-draft-005", "rewrite-draft-007", "rewrite-draft-008",
    "rewrite-draft-013", "rewrite-draft-014", "rewrite-draft-017", "rewrite-draft-028",
    "rewrite-draft-029", "rewrite-draft-041",
]

corpus = open(REPO + '/docs/rewrite-email-eval-cases-100.md').read()
def get_draft(case_id):
    m = re.search(r'-\s*id:\s*' + re.escape(case_id) + r'\b', corpus)
    if not m: return None
    seg = corpus[m.end():]
    dm = re.search(r'####\s*input_draft\s*\n(.*?)(?=\n####\s|\n###\s)', seg, re.S)
    return dm.group(1).strip() if dm else None

SYSTEM = """You extract STRUCTURED ATOMIC CLAIMS from an email so a translator can be verified for fidelity. Return JSON only.

For each meaningful statement, output:
{
  "id": "C001",
  "source_span": "exact substring of the source email",
  "subject": "who/what performs the action or holds the state",
  "action": "verb or state predicate",
  "object": "what is acted on or stated about (null if intransitive)",
  "modality": "one of: requirement | permission | capability | uncertainty | prohibition | certainty | offer",
  "polarity": "positive | negative",
  "time_scope": "specific date / duration / deadline / temporal qualifier (null if none)",
  "condition": "any IF/UNLESS condition this claim depends on (null if none)",
  "must_preserve": ["short list of phrases/properties that must survive any rewrite"]
}

Rules:
1. Skip greetings, sign-offs, polite filler ("thanks for", "hope this finds you well", etc.)
2. Each claim must be ATOMIC: one subject, one action, one object. Split compound sentences.
3. source_span MUST be an EXACT verbatim substring of the input — do NOT normalize tense, person, word order, or rephrase. If you reconstruct the subject (e.g. resolving a pronoun in `subject`), the source_span must still be the original wording. A naive `draft.contains(source_span)` check must pass.
4. NEVER invent claims not in the source.
5. Capture ALL meaningful claims — a missed claim is a missed drift detection.
6. SKIP meta-communication statements — sentences where the writer comments on how they are phrasing the message rather than asserting a fact about the world. Examples to skip: "I do not want to make this sound more final than it is", "let me explain", "to be clear", "I'll keep this short".
7. Modality calibration: verbs that hedge confidence (expect / hope / think / believe / suppose / guess / probably / likely) are modality=`uncertainty`, even if the surrounding tense is declarative. Reserve `certainty` for unhedged assertions of fact or definite future action ("will", "is", "was", "have done").

Return JSON: {"claims": [...]}"""

out_dir = '/tmp/claim_ledger_validation_v2'
os.makedirs(out_dir, exist_ok=True)
summary = []

for cid in CASE_IDS:
    draft = get_draft(cid)
    if not draft:
        print(f"{cid}: draft not found, skipping"); continue
    resp = client.chat.completions.create(
        model="deepseek-chat",
        messages=[{"role":"system","content":SYSTEM},{"role":"user","content":draft}],
        temperature=0.0,
        response_format={"type":"json_object"},
        max_tokens=2500,
    )
    content = resp.choices[0].message.content
    open(f'{out_dir}/{cid}.json', 'w').write(content)
    open(f'{out_dir}/{cid}.draft.txt', 'w').write(draft)
    try:
        data = json.loads(content)
        claims = data.get('claims', [])
        # Run substring check
        bad_spans = [c for c in claims if c.get('source_span') and c['source_span'] not in draft]
        summary.append((cid, len(claims), len(bad_spans), draft, claims))
    except json.JSONDecodeError as e:
        summary.append((cid, -1, -1, draft, str(e)))

print(f"=== v2: {len(summary)} cases, JSON saved to {out_dir} ===\n")
for cid, n, bad, draft, claims in summary:
    flag = "OK" if bad == 0 else f"BAD_SPAN×{bad}"
    print(f"--- {cid} ({len(draft.split())} words) → {n} claims, {flag} ---")
