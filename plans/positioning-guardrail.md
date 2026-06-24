# Positioning Guardrail — what we may and may not claim

Last updated: 2026-06-24
Owner: ChuanQiao1128 (TimeAwake Ltd)

## Why this exists

Our rewrite engine runs a Sapling-based naturalness gate, and the product shows a
before/after percentage. It is tempting — and wrong — to market this as
"beat AI detection." Nine rounds of internal research proved that promise is
undeliverable: any output that is clean, fact-safe, and sendable scores high
again on a good detector, and the only way to push the number down is to mangle
the text into non-native/translationese (which damages facts and readability).
Detectors also disagree with each other and are noisy (±50 on identical text).

So the number we show is a **de-templating / naturalness proxy**, not a
detection-beating capability. This doc fixes the language so it cannot drift.

## The one-line positioning

> Send it like you wrote it — a clear, natural reply that keeps your facts,
> numbers, and policies exactly as you meant them.

## Vocabulary (single source)

Owner decision (2026-06-24): keep **"AI Signal"** as the primary, attention-
grabbing label, and **pair it with "Naturalness"** so users understand what it
measures. Both appear together — "AI Signal" leads (the hook), "Naturalness"
clarifies (the honest meaning).

- Canonical paired label = **"AI Signal (Naturalness)"** in prominent UI
  (the focal before/after band, history heading, pricing rows, legal copy).
  Tight inline value pills may use **"AI Signal"** alone — "Naturalness" is
  already present in the nearby heading / explanatory line.
- We retired the orphan **"Tone check"** name; that metric is the same AI Signal
  (naturalness) reference.
- The honesty hedge stays everywhere the number appears, and is what keeps the
  catchy "AI Signal" label honest:
  *"a reference signal — lower reads more natural — not a guarantee; review
  before sending."*
- The API/data contract field names (`draftAiLikePercent`,
  `rewriteAiLikePercent`, the `signal` response field) are unchanged — this is a
  **display-label** decision, not a contract change.

## ✅ We MAY say

- call the metric **"AI Signal"** / **"AI Signal (Naturalness)"** — a catchy
  name for a naturalness reference, always shown with the hedge above
- "sounds like you" / "in your voice"
- "natural", "less stiff", "less generic", "less templated"
- "keeps your facts / numbers / dates / policies intact"
- "clean up a draft you wrote with AI so it sounds like you" (quality framing)
- "a reference score to compare drafts — you always review before sending"

## ⛔ We may NOT say (and why)

- Anything promising a message will or won't be **flagged / judged / caught** by
  any person, tool, or system — we proved we cannot deliver this.
- "passes", "guaranteed", "undetectable-style" claims about the score.
- Framing the number as proof of anything other than *reads more natural*.
- Raising the topic of detection at all in user-facing copy — even to deny it.
  Mentioning it plants the wrong expectation.

## Banned substrings (existing CI guard — do not reintroduce)

`humanizer | bypass | undetect | detector | evade` — scanned across
`app components public lib`. This guard is about the words; the rules above are
about the *promise*. Both must hold.

## Recommended follow-up (not yet built)

Add a lightweight copy test that fails the build if user-facing copy under
`app/` or `components/` contains detection-promise phrases (e.g. "won't be
flagged", "passes detection", "guaranteed to"). This makes the guardrail
mechanical, like the banned-term grep, so a future copy edit can't quietly
cross the line. Deferred to avoid false positives until phrasing is agreed.

## References

- Memory: `stop-chasing-ai-detection`, `ta-translation-roundtrip-tested`,
  `detection-three-axis-tradeoff`, `naturalness-gate-noise`.
- This pass landed the dual **"AI Signal (Naturalness)"** label + an AI-draft
  demo case on 2026-06-24. The catchy "AI Signal" hook is retained on purpose;
  the line we must never cross is presenting it as proof a message won't be
  flagged or detected.
