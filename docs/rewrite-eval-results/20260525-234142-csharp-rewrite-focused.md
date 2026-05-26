# C# Rewrite Eval - focused

Started: 2026-05-25T23:41:42.7866810+00:00
Finished: 2026-05-25T23:44:52.2416410+00:00
Cases evaluated: 40
Successful rewrites: 40/40
Fact pass count: 39/40
Customer-usable pass: 39/40 (output + all must_keep preserved + engine success + no forbidden violation)
Forbidden-claim violations (deterministic screen): 0/40
Naturalness failures recoverable under relaxed gate (rewrite <= 40): 0
Measured rewrites: 40
Average signal drop: 1 pts
Baseline-above-threshold average drop: 9 pts (2 cases)
Rewrites below 50% signal: 40/40
Model calls: 46
Sapling calls: 82
Model: deepseek-v4-pro
Max attempts: 10

| Case | Category | Tone | Usable | Success | Draft | Rewrite | Change | Facts | Forbidden | Error | Missing facts |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- | --- |
rewrite-draft-001 | teacher_parent | warm | yes | yes | 8% | 0% | -8 | yes | 0 |  | 
rewrite-draft-002 | customer_support | warm | yes | yes | 0% | 0% | 0 | yes | 0 |  | 
rewrite-draft-003 | billing_support | warm | yes | yes | 0% | 0% | 0 | yes | 0 |  | 
rewrite-draft-004 | workplace_update | warm | yes | yes | 9% | 20% | 11 | yes | 0 |  | 
rewrite-draft-005 | sales_followup | warm | yes | yes | 0% | 0% | 0 | yes | 0 |  | 
rewrite-draft-006 | scheduling | warm | yes | yes | 17% | 25% | 8 | yes | 0 |  | 
rewrite-draft-007 | hr_recruiting | warm | yes | yes | 15% | 0% | -15 | yes | 0 |  | 
rewrite-draft-008 | medical_admin | warm | yes | yes | 11% | 16% | 5 | yes | 0 |  | 
rewrite-draft-009 | property_logistics | warm | yes | yes | 7% | 0% | -7 | yes | 0 |  | 
rewrite-draft-010 | nonprofit_community | warm | yes | yes | 9% | 0% | -9 | yes | 0 |  | 
rewrite-draft-011 | customer_support | warm | yes | yes | 5% | 0% | -5 | yes | 0 |  | 
rewrite-draft-012 | teacher_parent | warm | yes | yes | 7% | 0% | -7 | yes | 0 |  | 
rewrite-draft-013 | workplace_update | warm | yes | yes | 24% | 20% | -4 | yes | 0 |  | 
rewrite-draft-014 | billing_support | warm | yes | yes | 2% | 0% | -2 | yes | 0 |  | 
rewrite-draft-015 | sales_followup | warm | yes | yes | 0% | 0% | 0 | yes | 0 |  | 
rewrite-draft-016 | medical_admin | warm | yes | yes | 0% | 0% | 0 | yes | 0 |  | 
rewrite-draft-017 | property_logistics | warm | yes | yes | 21% | 32% | 11 | yes | 0 |  | 
rewrite-draft-018 | hr_recruiting | warm | yes | yes | 8% | 16% | 8 | yes | 0 |  | 
rewrite-draft-019 | nonprofit_community | warm | yes | yes | 0% | 0% | 0 | yes | 0 |  | 
rewrite-draft-020 | school_admin | warm | yes | yes | 8% | 14% | 6 | yes | 0 |  | 
rewrite-draft-021 | customer_support | warm | yes | yes | 0% | 0% | 0 | yes | 0 |  | 
rewrite-draft-022 | billing_support | warm | yes | yes | 3% | 3% | 0 | yes | 0 |  | 
rewrite-draft-023 | workplace_update | warm | yes | yes | 1% | 4% | 3 | yes | 0 |  | 
rewrite-draft-024 | sales_followup | warm | yes | yes | 18% | 18% | 0 | yes | 0 |  | 
rewrite-draft-025 | teacher_parent | warm | yes | yes | 23% | 18% | -5 | yes | 0 |  | 
rewrite-draft-026 | medical_admin | warm | yes | yes | 0% | 0% | 0 | yes | 0 |  | 
rewrite-draft-027 | property_logistics | warm | yes | yes | 15% | 12% | -3 | yes | 0 |  | 
rewrite-draft-028 | customer_success | warm | yes | yes | 0% | 0% | 0 | yes | 0 |  | 
rewrite-draft-029 | hr_recruiting | warm | yes | yes | 36% | 33% | -3 | yes | 0 |  | 
rewrite-draft-030 | nonprofit_community | warm | yes | yes | 27% | 27% | 0 | yes | 0 |  | 
rewrite-draft-031 | customer_support | warm | no | yes | 43% | 26% | -17 | no | 0 |  | No fix can be promised at this time.
rewrite-draft-032 | billing_support | warm | yes | yes | 28% | 28% | 0 | yes | 0 |  | 
rewrite-draft-033 | workplace_update | warm | yes | yes | 0% | 0% | 0 | yes | 0 |  | 
rewrite-draft-034 | teacher_parent | warm | yes | yes | 0% | 0% | 0 | yes | 0 |  | 
rewrite-draft-035 | sales_followup | warm | yes | yes | 6% | 6% | 0 | yes | 0 |  | 
rewrite-draft-036 | medical_admin | warm | yes | yes | 34% | 38% | 4 | yes | 0 |  | 
rewrite-draft-037 | property_logistics | warm | yes | yes | 17% | 17% | 0 | yes | 0 |  | 
rewrite-draft-038 | school_admin | warm | yes | yes | 41% | 40% | -1 | yes | 0 |  | 
rewrite-draft-039 | customer_success | warm | yes | yes | 12% | 12% | 0 | yes | 0 |  | 
rewrite-draft-040 | nonprofit_community | warm | yes | yes | 27% | 27% | 0 | yes | 0 |  | 
