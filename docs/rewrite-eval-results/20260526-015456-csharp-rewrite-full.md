# C# Rewrite Eval - full

Started: 2026-05-26T01:54:56.6031810+00:00
Finished: 2026-05-26T02:04:39.4654340+00:00
Cases evaluated: 100
Successful rewrites: 100/100
Fact pass count: 95/100
Customer-usable pass: 94/100 (output + all must_keep preserved + engine success + no forbidden violation)
Forbidden-claim violations (deterministic screen): 1/100
Naturalness failures recoverable under relaxed gate (rewrite <= 40): 0
Measured rewrites: 100
Average signal drop: 3 pts
Baseline-above-threshold average drop: 15 pts (5 cases)
Rewrites below 50% signal: 100/100
Model calls: 107
Sapling calls: 206
Model: deepseek-v4-pro
Max attempts: 10

| Case | Category | Tone | Usable | Success | Draft | Rewrite | Change | Facts | Forbidden | Error | Missing facts |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- | --- |
rewrite-draft-001 | teacher_parent | warm | yes | yes | 8% | 0% | -8 | yes | 0 |  | 
rewrite-draft-002 | customer_support | warm | yes | yes | 0% | 0% | 0 | yes | 0 |  | 
rewrite-draft-003 | billing_support | warm | yes | yes | 0% | 0% | 0 | yes | 0 |  | 
rewrite-draft-004 | workplace_update | warm | yes | yes | 9% | 17% | 8 | yes | 0 |  | 
rewrite-draft-005 | sales_followup | warm | yes | yes | 0% | 0% | 0 | yes | 0 |  | 
rewrite-draft-006 | scheduling | warm | yes | yes | 17% | 20% | 3 | yes | 0 |  | 
rewrite-draft-007 | hr_recruiting | warm | yes | yes | 15% | 3% | -12 | yes | 0 |  | 
rewrite-draft-008 | medical_admin | warm | yes | yes | 11% | 13% | 2 | yes | 0 |  | 
rewrite-draft-009 | property_logistics | warm | yes | yes | 7% | 0% | -7 | yes | 0 |  | 
rewrite-draft-010 | nonprofit_community | warm | yes | yes | 9% | 11% | 2 | yes | 0 |  | 
rewrite-draft-011 | customer_support | warm | yes | yes | 5% | 0% | -5 | yes | 0 |  | 
rewrite-draft-012 | teacher_parent | warm | yes | yes | 7% | 0% | -7 | yes | 0 |  | 
rewrite-draft-013 | workplace_update | warm | yes | yes | 24% | 17% | -7 | yes | 0 |  | 
rewrite-draft-014 | billing_support | warm | yes | yes | 2% | 0% | -2 | yes | 0 |  | 
rewrite-draft-015 | sales_followup | warm | yes | yes | 0% | 0% | 0 | yes | 0 |  | 
rewrite-draft-016 | medical_admin | warm | yes | yes | 0% | 0% | 0 | yes | 0 |  | 
rewrite-draft-017 | property_logistics | warm | yes | yes | 21% | 27% | 6 | yes | 0 |  | 
rewrite-draft-018 | hr_recruiting | warm | yes | yes | 8% | 14% | 6 | yes | 0 |  | 
rewrite-draft-019 | nonprofit_community | warm | yes | yes | 0% | 0% | 0 | yes | 0 |  | 
rewrite-draft-020 | school_admin | warm | yes | yes | 8% | 7% | -1 | yes | 0 |  | 
rewrite-draft-021 | customer_support | warm | yes | yes | 0% | 0% | 0 | yes | 0 |  | 
rewrite-draft-022 | billing_support | warm | yes | yes | 3% | 4% | 1 | yes | 0 |  | 
rewrite-draft-023 | workplace_update | warm | yes | yes | 1% | 1% | 0 | yes | 0 |  | 
rewrite-draft-024 | sales_followup | warm | yes | yes | 18% | 14% | -4 | yes | 0 |  | 
rewrite-draft-025 | teacher_parent | warm | yes | yes | 23% | 20% | -3 | yes | 0 |  | 
rewrite-draft-026 | medical_admin | warm | yes | yes | 0% | 7% | 7 | yes | 0 |  | 
rewrite-draft-027 | property_logistics | warm | yes | yes | 15% | 16% | 1 | yes | 0 |  | 
rewrite-draft-028 | customer_success | warm | yes | yes | 0% | 0% | 0 | yes | 0 |  | 
rewrite-draft-029 | hr_recruiting | warm | yes | yes | 36% | 40% | 4 | yes | 0 |  | 
rewrite-draft-030 | nonprofit_community | warm | yes | yes | 27% | 24% | -3 | yes | 0 |  | 
rewrite-draft-031 | customer_support | warm | yes | yes | 43% | 35% | -8 | yes | 0 |  | 
rewrite-draft-032 | billing_support | warm | yes | yes | 28% | 16% | -12 | yes | 0 |  | 
rewrite-draft-033 | workplace_update | warm | yes | yes | 0% | 0% | 0 | yes | 0 |  | 
rewrite-draft-034 | teacher_parent | warm | yes | yes | 0% | 0% | 0 | yes | 0 |  | 
rewrite-draft-035 | sales_followup | warm | yes | yes | 6% | 6% | 0 | yes | 0 |  | 
rewrite-draft-036 | medical_admin | warm | yes | yes | 34% | 37% | 3 | yes | 0 |  | 
rewrite-draft-037 | property_logistics | warm | yes | yes | 17% | 15% | -2 | yes | 0 |  | 
rewrite-draft-038 | school_admin | warm | yes | yes | 41% | 35% | -6 | yes | 0 |  | 
rewrite-draft-039 | customer_success | warm | yes | yes | 12% | 12% | 0 | yes | 0 |  | 
rewrite-draft-040 | nonprofit_community | warm | yes | yes | 27% | 35% | 8 | yes | 0 |  | 
rewrite-draft-041 | customer_support | warm | yes | yes | 6% | 0% | -6 | yes | 0 |  | 
rewrite-draft-042 | billing_support | warm | yes | yes | 0% | 7% | 7 | yes | 0 |  | 
rewrite-draft-043 | workplace_update | warm | yes | yes | 0% | 0% | 0 | yes | 0 |  | 
rewrite-draft-044 | teacher_parent | warm | yes | yes | 32% | 25% | -7 | yes | 0 |  | 
rewrite-draft-045 | sales_followup | warm | yes | yes | 8% | 9% | 1 | yes | 0 |  | 
rewrite-draft-046 | medical_admin | warm | yes | yes | 9% | 10% | 1 | yes | 0 |  | 
rewrite-draft-047 | property_logistics | warm | yes | yes | 10% | 8% | -2 | yes | 0 |  | 
rewrite-draft-048 | hr_recruiting | warm | yes | yes | 42% | 39% | -3 | yes | 0 |  | 
rewrite-draft-049 | school_admin | warm | yes | yes | 27% | 31% | 4 | yes | 0 |  | 
rewrite-draft-050 | customer_success | warm | yes | yes | 5% | 0% | -5 | yes | 0 |  | 
rewrite-draft-051 | customer_support | warm | yes | yes | 28% | 24% | -4 | yes | 0 |  | 
rewrite-draft-052 | billing_support | warm | yes | yes | 5% | 10% | 5 | yes | 0 |  | 
rewrite-draft-053 | workplace_update | warm | yes | yes | 5% | 0% | -5 | yes | 0 |  | 
rewrite-draft-054 | teacher_parent | warm | yes | yes | 5% | 4% | -1 | yes | 0 |  | 
rewrite-draft-055 | sales_followup | warm | yes | yes | 30% | 25% | -5 | yes | 0 |  | 
rewrite-draft-056 | medical_admin | warm | yes | yes | 0% | 0% | 0 | yes | 0 |  | 
rewrite-draft-057 | property_logistics | warm | yes | yes | 83% | 32% | -51 | yes | 0 |  | 
rewrite-draft-058 | hr_recruiting | warm | yes | yes | 42% | 34% | -8 | yes | 0 |  | 
rewrite-draft-059 | nonprofit_community | warm | yes | yes | 18% | 15% | -3 | yes | 0 |  | 
rewrite-draft-060 | customer_success | warm | yes | yes | 26% | 13% | -13 | yes | 0 |  | 
rewrite-draft-061 | customer_support | warm | yes | yes | 37% | 34% | -3 | yes | 0 |  | 
rewrite-draft-062 | billing_support | warm | yes | yes | 2% | 0% | -2 | yes | 0 |  | 
rewrite-draft-063 | workplace_update | warm | yes | yes | 4% | 6% | 2 | yes | 0 |  | 
rewrite-draft-064 | teacher_parent | warm | no | yes | 33% | 16% | -17 | no | 0 |  | The teacher has observed concerns over the past two weeks.
rewrite-draft-065 | sales_followup | warm | yes | yes | 0% | 0% | 0 | yes | 0 |  | 
rewrite-draft-066 | medical_admin | warm | yes | yes | 0% | 1% | 1 | yes | 0 |  | 
rewrite-draft-067 | property_logistics | warm | yes | yes | 1% | 0% | -1 | yes | 0 |  | 
rewrite-draft-068 | hr_recruiting | warm | no | yes | 27% | 34% | 7 | no | 0 |  | The video link will be sent after the slot is confirmed.
rewrite-draft-069 | nonprofit_community | warm | yes | yes | 30% | 40% | 10 | yes | 0 |  | 
rewrite-draft-070 | customer_success | warm | no | yes | 31% | 12% | -19 | no | 0 |  | An effective date is required for the transfer.; Requests received after 2:00 p.m. EST are processed the following business day.
rewrite-draft-071 | customer_support | warm | no | yes | 22% | 4% | -18 | yes | 1 |  | 
rewrite-draft-072 | billing_support | warm | yes | yes | 32% | 13% | -19 | yes | 0 |  | 
rewrite-draft-073 | workplace_update | warm | yes | yes | 13% | 0% | -13 | yes | 0 |  | 
rewrite-draft-074 | teacher_parent | warm | yes | yes | 36% | 15% | -21 | yes | 0 |  | 
rewrite-draft-075 | sales_followup | warm | yes | yes | 13% | 26% | 13 | yes | 0 |  | 
rewrite-draft-076 | medical_admin | warm | yes | yes | 38% | 15% | -23 | yes | 0 |  | 
rewrite-draft-077 | property_logistics | warm | yes | yes | 34% | 19% | -15 | yes | 0 |  | 
rewrite-draft-078 | hr_recruiting | warm | yes | yes | 22% | 37% | 15 | yes | 0 |  | 
rewrite-draft-079 | nonprofit_community | warm | yes | yes | 15% | 14% | -1 | yes | 0 |  | 
rewrite-draft-080 | customer_success | warm | yes | yes | 15% | 9% | -6 | yes | 0 |  | 
rewrite-draft-081 | customer_support | warm | yes | yes | 0% | 0% | 0 | yes | 0 |  | 
rewrite-draft-082 | billing_support | warm | yes | yes | 1% | 1% | 0 | yes | 0 |  | 
rewrite-draft-083 | workplace_update | warm | yes | yes | 11% | 10% | -1 | yes | 0 |  | 
rewrite-draft-084 | teacher_parent | warm | yes | yes | 23% | 3% | -20 | yes | 0 |  | 
rewrite-draft-085 | sales_followup | warm | yes | yes | 21% | 8% | -13 | yes | 0 |  | 
rewrite-draft-086 | medical_admin | warm | yes | yes | 15% | 26% | 11 | yes | 0 |  | 
rewrite-draft-087 | property_logistics | warm | yes | yes | 0% | 0% | 0 | yes | 0 |  | 
rewrite-draft-088 | hr_recruiting | warm | yes | yes | 25% | 32% | 7 | yes | 0 |  | 
rewrite-draft-089 | nonprofit_community | warm | no | yes | 29% | 34% | 5 | no | 0 |  | No perishable donations.
rewrite-draft-090 | customer_success | warm | yes | yes | 0% | 0% | 0 | yes | 0 |  | 
rewrite-draft-091 | customer_support | warm | yes | yes | 11% | 10% | -1 | yes | 0 |  | 
rewrite-draft-092 | billing_support | warm | no | yes | 2% | 1% | -1 | no | 0 |  | The disputed amount is $247.00.
rewrite-draft-093 | workplace_update | warm | yes | yes | 1% | 1% | 0 | yes | 0 |  | 
rewrite-draft-094 | teacher_parent | warm | yes | yes | 0% | 0% | 0 | yes | 0 |  | 
rewrite-draft-095 | sales_followup | warm | yes | yes | 0% | 0% | 0 | yes | 0 |  | 
rewrite-draft-096 | medical_admin | warm | yes | yes | 0% | 2% | 2 | yes | 0 |  | 
rewrite-draft-097 | property_logistics | warm | yes | yes | 20% | 14% | -6 | yes | 0 |  | 
rewrite-draft-098 | hr_recruiting | warm | yes | yes | 13% | 19% | 6 | yes | 0 |  | 
rewrite-draft-099 | nonprofit_community | warm | yes | yes | 33% | 32% | -1 | yes | 0 |  | 
rewrite-draft-100 | customer_success | warm | yes | yes | 0% | 0% | 0 | yes | 0 |  | 
