# Offline Authenticity Eval — AI-Detection Signal (Pangram) Policy

**Status:** active policy (set 2026-05-26). Governs how Pangram — and any third-party
AI-detection service — is used in this project.

## Principle

Pangram is an **offline diagnostic thermometer, not an online steering wheel.**

- Do **not** make the rewrite agent chase a low Pangram score.
- **Do** use Pangram to discover where the agent's outputs are getting too templated,
  too smooth, too over-polished — i.e. drifting away from the user's own voice.

One line: **don't optimize per-email for the detection score; use the detection score to
find systemic AI-ishness drift across agent versions.**

**Why (empirical, 2026-05-26).** Using Pangram as a per-email optimization target was
tested and is unstable: with the quality gate on (send-ready ≤ 40) and a 5-round cap,
only **2/8** cases produced a usable email; per-round scores **bounce non-monotonically**
(a round can make it worse), and long/structured emails (sales-quote, support-bullets,
enterprise) sat **immovable at ~99%**. The earlier "looked promising" result was an
artifact of disabling the quality gate + 10 rounds + keeping the lowest of noisy
resamples — luck, not control. Evidence:
`docs/rewrite-eval-results/20260526-061424-csharp-rewrite-smoke.*`.

## 1. Position in the system

**Wrong** (detection-signal optimization — proven unstable, prohibited):

```
user email → rewrite agent → Pangram → per-sentence rewrite → Pangram → … → low score
```

**Right** (offline version evaluation):

```
fixed eval set → rewrite agent v1/v2/v3
  → { facts check + voice check + naturalness + Pangram score }
  → analyze failure patterns
  → improve agent prompt / rubric / voice profile
  → re-run eval
```

Pangram never participates in single-email production. It only participates in version
evaluation.

## 2. What Pangram evaluates

Pangram does **not** answer "can this email be sent?" It answers "is our rewrite agent
becoming more AI-ish / templated / over-polished over time?" — an **AI-ishness risk
signal**, not the final judge.

Final judges of a rewrite (Pangram is **last**, advisory):

1. **facts preserved** — nothing dropped or invented
2. **send-ready** — the user can send it as-is
3. **voice match** — sounds like this specific user
4. **naturalness** — reads naturally
5. **edit distance** — how much the user still has to change
6. **Pangram score** — AI-ish drift guardrail (advisory only)

## 3. Fixed eval sets (don't pick 3 at random each time)

- **Smoke set: 10** — quick run on every prompt / agent change.
- **Dev set: 50** — failure analysis and agent tuning.
- **Locked test set: 100–200** — looked at rarely; guards against overfitting to Pangram.

Coverage to include: teacher/parent, customer support, billing, sales, scheduling,
customer success; long / short / list-type / emotional emails; warm + direct tone;
zh/en mixed; raw user bullet notes; real user historical voice samples.

Per-case schema:

```json
{
  "case_id": "045_sales_long",
  "category": "sales",
  "length": "long",
  "input_context": "...",
  "user_notes": "...",
  "must_keep_facts": ["...", "..."],
  "voice_profile": {
    "tone": "direct, warm, concise",
    "avoid": ["generic opener", "over-polished corporate phrasing"],
    "examples": ["..."]
  },
  "gold_quality_notes": "What a good answer should preserve"
}
```

## 4. Eval pipeline (per agent version)

1. Load fixed eval set
2. Generate **one** final rewrite per case
3. Run facts checker
4. Run voice / naturalness evaluator
5. Optional human review on a sample
6. Run Pangram **once** per output
7. Store all scores
8. Compare against baseline
9. Category-level error analysis
10. Update the agent only on recurring quality / voice failures

**Hard rules:** one Pangram score per output. **No best-of-5 / best-of-10. No keeping the
lowest. Never feed Pangram back into a per-email rewrite loop.**

## 5. Scorecard per agent version

| Metric | Baseline | New Agent | Decision |
| --- | --- | --- | --- |
| facts pass rate | 96% | 98% | better |
| send-ready rate | 72% | 81% | better |
| voice match avg | 3.8/5 | 4.2/5 | better |
| user edit distance | 31% | 22% | better |
| Pangram median | 92 | 81 | better |
| Pangram p90 | 99 | 96 | slightly better |
| cost/email | $x | $y | acceptable |

For Pangram, never look at the mean alone. Look at: median, p75, p90, % over 90, % over
95, and breakdowns by category, long-vs-short, structured/list-vs-plain. (Example finding
already observed: long/structured emails 045/061/080 stick at ~99 while short warm emails
sometimes go low — far more useful than an average.)

## 6. Four-quadrant analysis (the most useful lens)

- **A. High Pangram + low human quality** — the most valuable failure. The agent really is
  too templated / generic. Feed these into prompt/rubric improvement. Common causes:
  template openers; rigid three-part structure; every sentence too complete; too-smooth
  tone; uniform sentence length; added pleasantries the user never wrote; over-explaining;
  heavy corporate phrasing; not matching the user's own habits.
- **B. High Pangram + good human quality** — don't rush to change. Pangram may be
  mis-judging, or this kind of business text is inherently AI-like. Mark as **watchlist**;
  do not sacrifice good text for the score.
- **C. Low Pangram + low human quality** — a counter-example proving Pangram must not be
  the target (text got weird/disjointed but scored low). Never learn from these as
  positives.
- **D. Low Pangram + good human quality** — the best positives. Learn **why it is more
  like the user** (more of the user's own words? less template structure? more specific
  context? preserved the user's rhythm?), **not** "how it circumvented the signal."

## 7. Promotion rule

A new rewrite-agent version can be promoted only if:

1. facts pass rate ≥ baseline
2. send-ready rate ≥ baseline
3. voice match ≥ baseline
4. user edit distance ≤ baseline
5. cost and latency acceptable
6. Pangram distribution does not materially regress

Pangram is a **guardrail, not a gate**:

- median should not increase by more than 5–10 points
- p90 should not increase by more than 5–10 points
- % of outputs above 95 should not increase materially
- any category with repeated high-Pangram + low-human-quality failures must be reviewed

**Never** set "every email must be below 40." That pulls us back onto the
detection-gaming path (and is prohibited — see AGENTS.md banned terms).

## 8. Flagged sentences — use patterns, not single-sentence patches

If Pangram surfaces flagged sentences, look at **recurring patterns across 100 emails**,
not single-sentence repairs. If many high-score emails share phrasings like:

> "I wanted to reach out…", "I completely understand…", "That said…",
> "Please let me know if you have any questions.", "I'm happy to help."

add them to the agent's "avoid generic phrasing" list — because they are templated and
un-personal, **not** to move the detection score.

## 9. Agent optimization directions (optimize these, not the score)

**A. Full rewrite → minimal rewrite.** Much of the AI-ishness comes from rewriting too
completely.

> You are not writing from scratch. You are lightly editing the user's intended reply.
> Preserve the user's wording, rhythm, and level of detail unless it is unclear. Do not
> make the message more polished than the user would naturally write.

**B. Preserve the user's own words** (especially bullet notes / dictation).

> Prefer the user's own words over generic professional phrasing. Only replace wording
> when it is confusing, unintentionally rude, or grammatically broken.

**C. No default business template.**

> Do not use generic openers or closers unless the user uses them. Avoid formulaic
> transitions. Do not force every response into greeting + empathy + action + closing.

**D. Voice profile per user.**

```json
{
  "sentence_length": "short to medium",
  "directness": "high",
  "warmth": "medium",
  "formality": "low-medium",
  "common_phrases": ["makes sense", "happy to", "quick note"],
  "avoid_phrases": ["I hope this email finds you well", "I completely understand"],
  "style_rules": ["Uses contractions", "Does not over-explain", "Usually ends with a concrete next step"]
}
```

Pangram validates that adding a voice profile **reduces AI-ish drift overall** — but the
core goal is voice match, not the score.

## 10. Internal rubric (agent-side evaluator — does NOT score Pangram)

The rewrite agent may have an internal evaluator. It scores quality/voice, **never**
Pangram:

```json
{
  "facts_preserved": true,
  "missing_facts": [],
  "added_unsupported_claims": [],
  "voice_match_score": 4,
  "naturalness_score": 4,
  "send_ready": true,
  "too_generic": false,
  "over_polished": false,
  "template_phrasing": ["I hope this message finds you well"],
  "recommended_action": "pass"
}
```

Pangram only validates, in the external offline eval, whether this rubric actually reduces
AI-ish drift.

## 11. Result logging schema (per output, per version)

```json
{
  "case_id": "061_support_list",
  "agent_version": "rewrite-agent-v0.7.3",
  "model": "deepseek/gpt/claude/etc",
  "prompt_hash": "abc123",
  "input_tokens": 842,
  "output_tokens": 312,
  "facts_pass": true,
  "fact_errors": [],
  "send_ready": false,
  "voice_score": 3,
  "naturalness_score": 4,
  "template_score": 2,
  "human_notes": "Too generic, lost user's direct tone",
  "pangram_score": 99,
  "cost": 0.012,
  "latency_ms": 4200
}
```

Lets us later ask: which categories spike Pangram? does high Pangram correlate with low
human quality? which version improved voice? which prompt change hurt long emails? is high
Pangram mostly on list-type emails? does user edit distance correlate with Pangram?

## The locked rule (for the team)

- Pangram is used **only** for offline version comparison.
- **One** Pangram score per case per agent version.
- **No** per-email rewrite loop driven by Pangram.
- **Do not** chase < 40 on any single email.
- Look only at the overall distribution and failure patterns.
- Treat Pangram as an agent-improvement signal **only** when high Pangram co-occurs with
  low voice / low naturalness / template phrasing.

## Naming

This is the **"Offline Authenticity Eval"**, not a detection-gaming pipeline.

> Pangram is used as an offline QA signal to detect whether rewrite outputs are becoming
> overly generic, over-polished, or statistically AI-like. It is **not** a production gate,
> **not** a per-email optimization target, **not** shown to users, and does **not** block
> generation. It only informs agent-level improvements when correlated with human quality
> or voice failures.

This keeps us building **"reply in my voice"**, not gaming an AI-detection signal.
