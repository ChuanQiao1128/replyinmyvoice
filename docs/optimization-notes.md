# Optimization Notes

Date: 2026-05-17T11:48:37.914Z
Samples evaluated: 8
Evaluation strategy rounds used: 3 of max 5
Average absolute signal drop: 16 pts
Rewrites below 50% AI-like signal: 2/8
Internal target met: no

The best measured production strategy is the compact grounded/texture strategy in `lib/openai.ts`. A third prompt adjustment was tested and performed worse, so it was not kept. The internal target was not met within the bounded evaluation budget. Future prompt work should focus on preserving concrete facts while reducing generic openings, uniform sentence rhythm, and overly polished closings.

| Sample | Tone | Draft | Rewrite | Change | Strategies | Notes |
| --- | --- | ---: | ---: | ---: | ---: | --- |
teacher-late-work | warm | 100% | 100% | 0 pts | 2 | None
teacher-parent-update | warm | 100% | 100% | 0 pts | 2 | No significant risks identified.
sales-proposal-follow-up | warm | 100% | 99% | -1 pts | 2 | No significant risks identified.
sales-demo-reschedule | direct | 100% | 39% | -61 pts | 1 | Review the reply before sending.
workplace-doc-review | direct | 100% | 100% | 0 pts | 2 | None
workplace-delay | warm | 100% | 38% | -62 pts | 1 | None
client-issue-update | warm | 100% | 99% | -1 pts | 2 | None
customer-invoice-question | direct | 100% | 100% | 0 pts | 2 | Review the reply before sending.
