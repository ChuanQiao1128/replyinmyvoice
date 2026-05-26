# C# Rewrite Eval - focused

Started: 2026-05-26T17:07:33.2191800+00:00
Finished: 2026-05-26T17:08:56.7934310+00:00
Cases evaluated: 6
Successful rewrites: 6/6
Fact pass count: 3/6
Customer-usable pass: 2/6 (output + all must_keep preserved + engine success + no forbidden violation)
Forbidden-claim violations (deterministic screen): 1/6
Naturalness failures recoverable under relaxed gate (rewrite <= 40): 0
Measured rewrites: 6
Average signal drop: 67 pts
Baseline-above-threshold average drop: 67 pts (6 cases)
Rewrites below 50% signal: 6/6
Model calls: 33
Sapling calls: 37
Model: deepseek-v4-pro
Max attempts: 10

| Case | Category | Tone | Usable | Success | Draft | Rewrite | Change | Facts | Forbidden | Error | Missing facts |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- | --- |
aidc-201 | scheduling | warm | no | yes | 93% | 0% | -93 | no | 0 |  | The appointment is confirmed.
aidc-202 | customer_support | warm | no | yes | 74% | 25% | -49 | yes | 2 |  | 
aidc-203 | billing_support | warm | no | yes | 66% | 0% | -66 | no | 0 |  | The amount is $120.00.
aidc-204 | customer_support | warm | yes | yes | 82% | 0% | -82 | yes | 0 |  | 
aidc-205 | sales_followup | warm | yes | yes | 57% | 7% | -50 | yes | 0 |  | 
aidc-206 | customer_success | warm | no | yes | 62% | 0% | -62 | no | 0 |  | Changes can be discussed if the customer wants.
