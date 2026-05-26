# C# Rewrite Eval - smoke

Started: 2026-05-26T05:08:11.7404630+00:00
Finished: 2026-05-26T05:10:38.5756060+00:00
Cases evaluated: 3
Successful rewrites: 3/3
Fact pass count: 3/3
Customer-usable pass: 3/3 (output + all must_keep preserved + engine success + no forbidden violation)
Forbidden-claim violations (deterministic screen): 0/3
Naturalness failures recoverable under relaxed gate (rewrite <= 100): 0
Measured rewrites: 3
Average signal drop: 66 pts
Baseline-above-threshold average drop: unavailable pts (0 cases)
Rewrites below 50% signal: 2/3
Model calls: 30
Sapling calls: 33
Model: deepseek-v4-pro
Max attempts: 10

| Case | Category | Tone | Usable | Success | Draft | Rewrite | Change | Facts | Forbidden | Error | Missing facts |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- | --- |
rewrite-draft-001 | teacher_parent | warm | yes | yes | 98% | 23% | -75 | yes | 0 |  | 
rewrite-draft-002 | customer_support | warm | yes | yes | 99% | 51% | -48 | yes | 0 |  | 
rewrite-draft-003 | billing_support | warm | yes | yes | 99% | 25% | -74 | yes | 0 |  | 
