# AI Draft Cleanup v1 — Spec (for review; NOT implemented)

**Status:** draft for review (2026-05-27). No code changed, no agent strategy changed.
Implementation is gated on owner review.

## Why this spec (the real product scenario)

A user pastes an AI-written email draft, won't edit it themselves, and wants it rewritten to
read more like a natural human email — **without losing facts, inventing facts, or changing
meaning**, and with **lower AI-detection risk** as a release-level outcome metric (see
`docs/offline-authenticity-eval.md`). There is **no human voice source** at cold start, so
this is **not** "reply in my voice." It is **AI Draft Cleanup / AI-to-Natural rewrite**.

## Grounding: what the prod audit (2026-05-27) found

| Question | Finding (read-only) |
| --- | --- |
| Fields `/api/rewrite` populates in `RewriteRequest` | The browser sends **only** `{ roughDraftReply, tone:"warm" }` (`rewrite-workspace.tsx:354`); the Next route forwards verbatim (`app/api/rewrite/route.ts:169,187`); the Functions endpoint deserializes the body straight into `RewriteRequest` and only validates `RoughDraftReply ≥10 chars` + `Tone ∈ {warm,direct}` (`RewriteHttpFunctions.cs:54,166`). |
| `MessageToReplyTo` / `WhatHappened` / `FactsToPreserve` / `Audience` / `Purpose` in prod | **All null.** The workspace never sends them. (A richer schema exists in `lib/validation.ts` / `lib/fact-extraction.ts`, but that is **dead Slice-7 TS**, not the live C# path.) |
| Where the fact ledger is extracted from in prod | `RewriteInputAnalyzer.CombineRequestText` = the **draft alone** (other fields empty) → `FactLedgerExtractor.Extract` works off the pasted draft only. |
| What the fact gate can / cannot check | **Can:** source-faithfulness — the rewrite didn't drop or alter facts the extractor found in the draft (names, dates, money, counts, IDs, negatives, certainty). **Cannot:** whether those facts are *true*, and any fact the extractor *missed* (the gate is only as complete as draft→ledger extraction). So the only guarantee is **source-faithful**, not factually-true. |
| Sapling calls per email (in the prod loop) | Per email: `1 (draft) + N (attempts)` measurements; each measurement is 1 call on success, up to 3 on transient failure (`MaxWritingSignalAttempts=3`). Typical ≈ 2–3 calls; up to 11 on a full 10-attempt run; theoretical max 33. **Sapling is a per-email in-loop production dependency today.** |
| Runtime thresholds / flags (defaults) | `AI_SIGNAL_TARGET=20`, `REWRITE_MAX_ATTEMPTS=10`, `NATURALNESS_THRESHOLD=40`, `WRITING_SIGNAL_TIMEOUT_SEC=10`, `OPENAI_TIMEOUT_SEC=60`, `TOTAL_REWRITE_BUDGET_SEC=0` (off), `WRITING_SIGNAL_PROVIDER=sapling`; model `deepseek-v4-pro`, temp `0.4`, `max_tokens 2800`, thinking disabled. |
| Router strategy for typical drafts | By word count: **≤35w → `MinimalPolish`** ("keep close to the draft"); **36–119w → `FactsFirstReconstruct`**; **≥120w → `FullStructureRewrite`** ("acknowledge→status→options→next step"). Overrides: lists/quotes → `QuoteListSafeRewrite`; messy thread → `MessyThreadCleanupRewrite`; policy language → `SupportPolicyOptionsRewrite`. (`RewriteEngineCore.cs:206–227`) |

**Two consequences that shape this spec:**
1. The draft is the **only** fact source → the **draft→fact/intent extractor is the single most
   important component**, and the product guarantee is **source-faithful** (we won't change the
   draft's facts), not factually-true.
2. The router routes by surface length, so short AI drafts get **polished** (AI wording kept) and
   long AI drafts get the **acknowledge→status→next-step template** — the exact structure that
   kept cases 045/061/080 pinned at ~99% in the Pangram run. The router + prompt are the levers.

## 1. Router changes

Add a strategy `FactsFirstCleanup` (De-template Rebuild). Route by **provenance**, not length:

```text
confirmed human rough notes (future UI signal)  → MinimalPolish / light edit
AI draft OR unknown source (current default)     → FactsFirstCleanup / De-template Rebuild
long or structured AI draft                      → FactsFirstCleanup, keep structure ONLY when
                                                    the facts require it (quote, steps, list)
```

- AI/unknown drafts default to **rebuild from facts/intent**, regardless of length — not
  `MinimalPolish` (keeps AI wording) and not the default 3-part `FullStructureRewrite`.
- `FullStructureRewrite` must stop defaulting to acknowledge→status→options→next-step. Structure
  is applied only when the content needs it (quotes, numbered steps, true lists).
- **Input classifier (cheap):** heuristics (generic openers, uniform sentence length, length) to
  separate "already-natural draft" (light touch) from "AI/templated" (rebuild). The single pasted
  textarea has no provenance flag today, so **default unknown → rebuild** (safe: preserves facts
  either way). *Open risk:* aggressively rebuilding an already-good human draft could shift its
  voice — mitigate by light-touch when the draft already reads natural.

## 2. Prompt changes

Remove the current contradictions in the system prompt (`OpenAiCompatibleRewriteModelClient.cs:81`):
it says "from provided facts" yet also "keep the draft's greeting", "2–4 paragraphs
(acknowledge, key facts, next step)", and a default sign-off — all of which preserve the AI
signature. Replace the structure-preserving lines with:

```text
The draft may be AI-generated.
Use it only as a source of facts, intent, constraints, and recipient context.
Do not preserve its wording, paragraph structure, opener, closer, or transitions by default.
Rebuild the email from the extracted facts.
Remove generic business filler.
Prefer shorter, plain, concrete sentences.
Do not add new facts, dates, reasons, promises, apologies, emotions, commitments, or claims.
Do not change the meaning.
Allow very short replies when the facts only require a short reply.
```

Keep the existing hard fact rules (preserve names/dates/money/counts/IDs/negatives; preserve
uncertainty — no may→is drift; no invented promises/discounts/timelines/policies/people; no
professional-advice redirects unless the facts provide them; no unsupported judgment labels).

## 2a. Short-reply skeleton trimming (drafted 2026-05-27; NOT implemented)

Baseline finding: the main residual AI tell is email scaffolding on short content — even a
one-line fact gets wrapped in `Hi, … Best,`. Proposed prompt rule (draft only):

> If the rewritten message is one or two short sentences, do not add a greeting or sign-off
> unless the draft clearly requires one. For short replies, prefer a direct 1–2 sentence
> answer over a full email skeleton.

Example:
- Over-structured: `Hi Alex,` / `Tuesday at 3 works for me.` / `Best,`
- Better: `Tuesday at 3 works for me.`

Low-risk; likely helps both naturalness and AI-detection risk. Validate via the offline eval
(after the scorer fix + an offline AI-detection snapshot), not wired into the engine yet.

## 3. Compression target

```text
The output should usually be shorter than the draft unless the draft is already concise.
Delete sentences that do not carry a fact, the intent, or a necessary next step.
```

Never shorten by dropping a fact — facts win over length.

## 4. Internal template-risk checker (replaces per-email Sapling in the loop)

Per the AI-detection-signal policy, **no external detection service runs in the per-email prod
loop**. Replace the in-loop Sapling call with a cheap internal checker:

**Rule-based checks:** generic-opener count, generic-closer count, corporate-filler count,
unnecessary-empathy count, transition-word overload, forced three-part structure,
output-too-close-to-draft-wording, output-longer-than-needed, unsupported additions.

**LLM judge (cheap):**
```json
{
  "too_generic": true,
  "over_polished": false,
  "sounds_like_ai_template": true,
  "main_reasons": ["generic opener", "overly formal closing", "unnecessary empathy sentence"],
  "repair_instruction": "Remove generic opener and shorten the closing."
}
```
One repair pass on failure. Sapling / Pangram / GPTZero stay in **offline release eval only**.
(Bonus: removes a per-email external dependency and its latency/cost.)

## 5. Fact lock (strengthen — prod fact source is the draft alone)

```text
draft → extract atomic facts + intent + constraints
      → rebuild reply from that spec (not by paraphrasing the draft's sentences)
      → fact checker: facts_preserved / missing_facts / unsupported_additions / meaning_changed / send_ready
      → output
```
Facts are checked before AI-ishness. The guarantee is **source-faithful**. Because the draft is
the only source, invest in the extractor's coverage — a missed fact is an unprotected fact.

## 6. Candidate selection — best-of-3 by internal rubric (NOT external-detection best-of)

Optionally generate 2–3 candidates (e.g. concise / warmer / direct) and pick by the internal
rubric (facts, naturalness, template-risk, length, send-ready). **Never** generate-10 →
an external detection service → keep-lowest (validated unstable + detection-gaming).

## 7. Eval plan (offline, AI-draft-focused)

Fixed eval set must include: short AI draft, long AI draft, structured AI draft, scheduling,
billing, customer support, sales, customer success, the 045/061/080 stuck-at-99 cases, emails
with obvious AI filler, and high-fact-density emails (nothing safe to delete).

Per case, record:
```text
facts_preserved · unsupported_additions · meaning_changed · send_ready
average_length · template_phrase_rate · router_strategy_selected · offline_AI_detection_high_risk
```

Success bar (release-level KPI, **not** per-email < 40):
```text
facts / send-ready do not drop
template risk drops
average length becomes more reasonable
offline AI-detection high-risk rate drops vs baseline
```

## 8. Customer-facing positioning

> Turn AI-sounding drafts into natural, send-ready emails without changing the meaning.

Do **not** promise "guaranteed to pass AI detection."

## Out of scope for v1

- Not voice-first (no human voice source at cold start; defer voice profiles to phase 2).
- No external AI-detection service in the per-email prod loop.
- This spec is for review only — not implemented, not wired into the engine, no prod change.
