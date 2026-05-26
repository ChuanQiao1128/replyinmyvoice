# C# Rewrite Eval - tail

Started: 2026-05-26T02:31:57.4574670+00:00
Finished: 2026-05-26T02:36:10.9304630+00:00
Cases evaluated: 21
Successful rewrites: 21/21
Fact pass count: 15/21
Customer-usable pass: 15/21 (output + all must_keep preserved + engine success + no forbidden violation)
Forbidden-claim violations (deterministic screen): 0/21
Naturalness failures recoverable under relaxed gate (rewrite <= 40): 0
Measured rewrites: 21
Average signal drop: 19 pts
Baseline-above-threshold average drop: 45 pts (5 cases)
Rewrites below 50% signal: 21/21
Model calls: 50
Sapling calls: 71
Model: deepseek-v4-pro
Max attempts: 10

| Case | Category | Tone | Usable | Success | Draft | Rewrite | Change | Facts | Forbidden | Error | Missing facts |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- | --- |
rewrite-draft-017 | property_logistics | warm | no | yes | 21% | 15% | -6 | no | 0 |  | Entry permission is needed if Drew is not home.
rewrite-draft-029 | hr_recruiting | warm | no | yes | 36% | 5% | -31 | no | 0 |  | The company decided to move forward with another candidate.
rewrite-draft-031 | customer_support | warm | no | yes | 43% | 15% | -28 | no | 0 |  | The team is actively investigating.
rewrite-draft-036 | medical_admin | warm | yes | yes | 34% | 12% | -22 | yes | 0 |  | 
rewrite-draft-038 | school_admin | warm | yes | yes | 41% | 13% | -28 | yes | 0 |  | 
rewrite-draft-040 | nonprofit_community | warm | yes | yes | 27% | 20% | -7 | yes | 0 |  | 
rewrite-draft-044 | teacher_parent | warm | yes | yes | 32% | 7% | -25 | yes | 0 |  | 
rewrite-draft-048 | hr_recruiting | warm | yes | yes | 42% | 0% | -42 | yes | 0 |  | 
rewrite-draft-049 | school_admin | warm | yes | yes | 27% | 17% | -10 | yes | 0 |  | 
rewrite-draft-055 | sales_followup | warm | yes | yes | 30% | 25% | -5 | yes | 0 |  | 
rewrite-draft-057 | property_logistics | warm | yes | yes | 83% | 0% | -83 | yes | 0 |  | 
rewrite-draft-058 | hr_recruiting | warm | yes | yes | 42% | 0% | -42 | yes | 0 |  | 
rewrite-draft-061 | customer_support | warm | yes | yes | 37% | 14% | -23 | yes | 0 |  | 
rewrite-draft-068 | hr_recruiting | warm | no | yes | 27% | 21% | -6 | no | 0 |  | The video link will be sent after the slot is confirmed.
rewrite-draft-069 | nonprofit_community | warm | yes | yes | 30% | 23% | -7 | yes | 0 |  | 
rewrite-draft-075 | sales_followup | warm | yes | yes | 13% | 16% | 3 | yes | 0 |  | 
rewrite-draft-078 | hr_recruiting | warm | yes | yes | 22% | 17% | -5 | yes | 0 |  | 
rewrite-draft-086 | medical_admin | warm | yes | yes | 15% | 13% | -2 | yes | 0 |  | 
rewrite-draft-088 | hr_recruiting | warm | no | yes | 25% | 11% | -14 | no | 0 |  | The hiring team is moving forward with a different applicant.
rewrite-draft-089 | nonprofit_community | warm | yes | yes | 29% | 25% | -4 | yes | 0 |  | 
rewrite-draft-099 | nonprofit_community | warm | no | yes | 33% | 17% | -16 | no | 0 |  | The donation amount is $500.
