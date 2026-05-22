# Sleep Run Cost Budget

Cap: NZ$20 cumulative DeepSeek + Sapling spend across the entire overnight run.
Per-issue cap: NZ$5.

Format: `<ISO> | <issue-id> | <provider> | <USD estimate> | <NZD estimate> | running total NZD`

DeepSeek Pro rough rates (USD per million tokens):
- Input: ~$0.27
- Output: ~$1.10
- ~1/10th of OpenAI GPT-4o, so eval is cheap.

NZD per USD: ~1.65 (use ADMIN_NZD_PER_USD from .env.local if set).

---

2026-05-22T16:00:32+12:00 | M4-001 | Sapling | USD $0.0173 | NZ$0.03 | failed draft-signal attempts only; no DeepSeek/OpenAI calls

Running total: NZ$0.03
