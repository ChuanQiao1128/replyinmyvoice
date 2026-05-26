# C# Rewrite Eval - focused

Started: 2026-05-26T17:10:31.4552450+00:00
Finished: 2026-05-26T17:10:44.0285550+00:00
Cases evaluated: 2
Successful rewrites: 2/2
Fact pass count: 0/2
Customer-usable pass: 0/2 (output + all must_keep preserved + engine success + no forbidden violation)
Forbidden-claim violations (deterministic screen): 0/2
Naturalness failures recoverable under relaxed gate (rewrite <= 40): 0
Measured rewrites: 2
Average signal drop: 59 pts
Baseline-above-threshold average drop: 59 pts (2 cases)
Rewrites below 50% signal: 2/2
Model calls: 5
Sapling calls: 7
Model: deepseek-v4-pro
Max attempts: 10

| Case | Category | Tone | Usable | Success | Draft | Rewrite | Change | Facts | Forbidden | Error | Missing facts |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- | --- |
aidc-207 | scheduling | warm | no | yes | 71% | 0% | -71 | no | 0 |  | The sender asks what works for the recipient.
aidc-208 | workplace_update | warm | no | yes | 46% | 0% | -46 | no | 0 |  | The slides are finished.
