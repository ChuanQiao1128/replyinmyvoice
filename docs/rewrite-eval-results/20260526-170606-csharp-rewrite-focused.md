# C# Rewrite Eval - focused

Started: 2026-05-26T17:06:06.7545750+00:00
Finished: 2026-05-26T17:07:35.8118770+00:00
Cases evaluated: 14
Successful rewrites: 14/14
Fact pass count: 14/14
Customer-usable pass: 13/14 (output + all must_keep preserved + engine success + no forbidden violation)
Forbidden-claim violations (deterministic screen): 1/14
Naturalness failures recoverable under relaxed gate (rewrite <= 40): 0
Measured rewrites: 14
Average signal drop: 4 pts
Baseline-above-threshold average drop: unavailable pts (0 cases)
Rewrites below 50% signal: 14/14
Model calls: 15
Sapling calls: 28
Model: deepseek-v4-pro
Max attempts: 10

| Case | Category | Tone | Usable | Success | Draft | Rewrite | Change | Facts | Forbidden | Error | Missing facts |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- | --- |
rewrite-draft-002 | customer_support | warm | yes | yes | 0% | 0% | 0 | yes | 0 |  | 
rewrite-draft-003 | billing_support | warm | yes | yes | 0% | 0% | 0 | yes | 0 |  | 
rewrite-draft-005 | sales_followup | warm | yes | yes | 0% | 0% | 0 | yes | 0 |  | 
rewrite-draft-006 | scheduling | warm | yes | yes | 17% | 20% | 3 | yes | 0 |  | 
rewrite-draft-028 | customer_success | warm | yes | yes | 0% | 14% | 14 | yes | 0 |  | 
rewrite-draft-041 | customer_support | warm | yes | yes | 6% | 6% | 0 | yes | 0 |  | 
rewrite-draft-042 | billing_support | warm | yes | yes | 0% | 19% | 19 | yes | 0 |  | 
rewrite-draft-045 | sales_followup | warm | yes | yes | 8% | 9% | 1 | yes | 0 |  | 
rewrite-draft-049 | school_admin | warm | yes | yes | 27% | 20% | -7 | yes | 0 |  | 
rewrite-draft-061 | customer_support | warm | yes | yes | 37% | 15% | -22 | yes | 0 |  | 
rewrite-draft-066 | medical_admin | warm | yes | yes | 0% | 2% | 2 | yes | 0 |  | 
rewrite-draft-071 | customer_support | warm | no | yes | 22% | 0% | -22 | yes | 1 |  | 
rewrite-draft-074 | teacher_parent | warm | yes | yes | 36% | 9% | -27 | yes | 0 |  | 
rewrite-draft-080 | customer_success | warm | yes | yes | 15% | 0% | -15 | yes | 0 |  | 
