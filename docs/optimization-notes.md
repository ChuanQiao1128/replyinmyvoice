# Optimization Notes

Date: 2026-05-17T14:16:44.779Z
Samples evaluated: 8
Evaluation strategy rounds used: 3 of max 5
Average absolute signal drop: 89 pts
Rewrites below 50% AI-like signal: 8/8
Internal target met: yes

Final selected strategy:

- First attempt: OpenAI plain email-thread note with compact, concrete paragraphs.
- Fallback attempt: deterministic thread fallback using only user-provided facts and a short opening + blank line + concrete next step structure.
- Fallback is only tried when the first candidate remains above 50% AI-like signal or does not reduce the draft by at least 30 points.

The internal target was met in the final run. Keep teacher/parent and invoice scenarios in the evaluation set because they are useful regression cases.

| Sample | Tone | Draft | Rewrite | Change | Strategies | Notes |
| --- | --- | ---: | ---: | ---: | ---: | --- |
teacher-late-work | warm | 100% | 1% | -99 pts | 2 | Review before sending if the context needs more detail.
teacher-parent-update | warm | 100% | 1% | -99 pts | 2 | Review before sending if the context needs more detail.
sales-proposal-follow-up | warm | 100% | 19% | -81 pts | 1 | Review the reply before sending.
sales-demo-reschedule | direct | 100% | 2% | -98 pts | 1 | Review the reply before sending.
workplace-doc-review | direct | 100% | 49% | -51 pts | 1 | Review the reply before sending.
workplace-delay | warm | 100% | 15% | -85 pts | 1 | None.
client-issue-update | warm | 100% | 0% | -100 pts | 1 | None
customer-invoice-question | direct | 100% | 0% | -100 pts | 2 | Review before sending if the context needs more detail.
