# C# Rewrite Eval - full

Started: 2026-05-24T03:43:40.3422770+00:00
Finished: 2026-05-24T03:53:09.9812970+00:00
Cases evaluated: 100
Successful rewrites: 100/100
Fact pass count: 86/100
Measured rewrites: 100
Average signal drop: 9 pts
Baseline-above-threshold average drop: 60 pts (13 cases)
Rewrites below 50% signal: 100/100
Model calls: 133
Sapling calls: 206
Model: deepseek-v4-pro
Max attempts: 10

| Case | Category | Tone | Success | Draft | Rewrite | Change | Facts | Error | Missing facts |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | --- | --- |
rimv-email-001 | teacher_parent | warm | yes | 48% | 0% | -48 | yes |  | 
rimv-email-002 | teacher_student | warm | yes | 0% | 0% | 0 | yes |  | 
rimv-email-003 | sales_follow_up | warm | yes | 48% | 0% | -48 | yes |  | 
rimv-email-004 | workplace_update | direct | yes | 0% | 0% | 0 | yes |  | 
rimv-email-005 | customer_support | warm | yes | 0% | 0% | 0 | yes |  | 
rimv-email-006 | billing_refund_proration | warm | yes | 0% | 0% | 0 | yes |  | 
rimv-email-007 | policy_eligibility | warm | yes | 0% | 0% | 0 | yes |  | 
rimv-email-008 | teacher_parent | warm | yes | 0% | 0% | 0 | yes |  | 
rimv-email-009 | subscription_cancellation | warm | yes | 1% | 0% | -1 | yes |  | 
rimv-email-010 | account_access | warm | yes | 0% | 0% | 0 | yes |  | 
rimv-email-011 | teacher_parent | warm | yes | 0% | 0% | 0 | yes |  | 
rimv-email-012 | sales_follow_up | warm | yes | 0% | 0% | 0 | yes |  | 
rimv-email-013 | workplace_update | direct | yes | 0% | 0% | 0 | yes |  | 
rimv-email-014 | billing_refund_proration | warm | yes | 0% | 0% | 0 | yes |  | 
rimv-email-015 | policy_eligibility | warm | yes | 40% | 0% | -40 | yes |  | 
rimv-email-016 | customer_support | warm | yes | 3% | 0% | -3 | yes |  | 
rimv-email-017 | subscription_cancellation | warm | yes | 49% | 0% | -49 | yes |  | 
rimv-email-018 | teacher_student | warm | yes | 0% | 0% | 0 | yes |  | 
rimv-email-019 | account_access | warm | yes | 0% | 0% | 0 | yes |  | 
rimv-email-020 | customer_support | warm | yes | 50% | 0% | -50 | yes |  | 
rimv-email-021 | medical_admin | warm | yes | 0% | 0% | 0 | yes |  | 
rimv-email-022 | medical_admin | warm | yes | 43% | 0% | -43 | yes |  | 
rimv-email-023 | legal_admin | direct | yes | 0% | 0% | 0 | yes |  | 
rimv-email-024 | legal_admin | direct | yes | 0% | 0% | 0 | yes |  | 
rimv-email-025 | real_estate | warm | yes | 0% | 0% | 0 | yes |  | 
rimv-email-026 | real_estate | warm | yes | 0% | 0% | 0 | yes |  | 
rimv-email-027 | recruiting | warm | yes | 0% | 0% | 0 | yes |  | 
rimv-email-028 | recruiting | warm | yes | 6% | 0% | -6 | yes |  | 
rimv-email-029 | hr_admin | warm | yes | 0% | 0% | 0 | yes |  | 
rimv-email-030 | hr_admin | warm | yes | 0% | 0% | 0 | yes |  | 
rimv-email-031 | nonprofit | warm | yes | 50% | 0% | -50 | no |  | Include no-goods-or-services language only as a receipt statement, not advice.
rimv-email-032 | nonprofit | warm | yes | 46% | 0% | -46 | yes |  | 
rimv-email-033 | hospitality | warm | yes | 0% | 0% | 0 | no |  | Rate is $169 nightly.
rimv-email-034 | hospitality | warm | yes | 0% | 0% | 0 | no |  | Graduation dinner June 8.
rimv-email-035 | event_coordination | warm | yes | 0% | 0% | 0 | yes |  | 
rimv-email-036 | event_coordination | warm | yes | 0% | 0% | 0 | yes |  | 
rimv-email-037 | vendor_procurement | direct | yes | 0% | 0% | 0 | yes |  | 
rimv-email-038 | vendor_procurement | direct | yes | 0% | 0% | 0 | yes |  | 
rimv-email-039 | event_coordination | warm | yes | 50% | 0% | -50 | yes |  | 
rimv-email-040 | nonprofit | warm | yes | 0% | 0% | 0 | yes |  | 
rimv-email-041 | customer_support | warm | yes | 0% | 0% | 0 | yes |  | 
rimv-email-042 | billing | warm | yes | 0% | 0% | 0 | yes |  | 
rimv-email-043 | education_admin | warm | yes | 0% | 0% | 0 | yes |  | 
rimv-email-044 | customer_success | warm | yes | 0% | 0% | 0 | yes |  | 
rimv-email-045 | workplace | direct | yes | 27% | 0% | -27 | yes |  | 
rimv-email-046 | workplace | direct | yes | 0% | 0% | 0 | yes |  | 
rimv-email-047 | vendor_procurement | direct | yes | 0% | 0% | 0 | yes |  | 
rimv-email-048 | education_admin | warm | yes | 0% | 0% | 0 | yes |  | 
rimv-email-049 | education | warm | yes | 0% | 0% | 0 | yes |  | 
rimv-email-050 | sales | warm | yes | 0% | 0% | 0 | yes |  | 
rimv-email-051 | customer_support | warm | yes | 98% | 0% | -98 | yes |  | 
rimv-email-052 | customer_support | warm | yes | 0% | 0% | 0 | yes |  | 
rimv-email-053 | customer_success | warm | yes | 0% | 0% | 0 | yes |  | 
rimv-email-054 | workplace | direct | yes | 0% | 0% | 0 | no |  | Status update should use neutral revised-after-SKU-changes wording.
rimv-email-055 | workplace | direct | yes | 0% | 0% | 0 | yes |  | 
rimv-email-056 | vendor_procurement | direct | yes | 0% | 0% | 0 | yes |  | 
rimv-email-057 | education_admin | warm | yes | 1% | 0% | -1 | yes |  | 
rimv-email-058 | education_admin | warm | yes | 48% | 0% | -48 | yes |  | 
rimv-email-059 | sales | warm | yes | 0% | 0% | 0 | yes |  | 
rimv-email-060 | customer_support | warm | yes | 0% | 0% | 0 | yes |  | 
rimv-email-061 | education | warm | yes | 0% | 0% | 0 | yes |  | 
rimv-email-062 | education | warm | yes | 0% | 0% | 0 | yes |  | 
rimv-email-063 | student_support | warm | yes | 0% | 0% | 0 | yes |  | 
rimv-email-064 | student_support | warm | yes | 0% | 0% | 0 | yes |  | 
rimv-email-065 | workplace | direct | yes | 0% | 0% | 0 | yes |  | 
rimv-email-066 | workplace | direct | yes | 0% | 0% | 0 | yes |  | 
rimv-email-067 | customer_success | warm | yes | 0% | 0% | 0 | yes |  | 
rimv-email-068 | customer_success | warm | yes | 0% | 0% | 0 | yes |  | 
rimv-email-069 | healthcare_admin | warm | yes | 0% | 0% | 0 | no |  | Waitlist is available.; Staff cannot interpret symptoms or change priority.
rimv-email-070 | healthcare_admin | warm | yes | 0% | 0% | 0 | yes |  | 
rimv-email-071 | property_management | warm | yes | 0% | 0% | 0 | no |  | Pet means non-emergency entry requires permission.
rimv-email-072 | insurance_admin | warm | yes | 0% | 0% | 0 | yes |  | 
rimv-email-073 | logistics | direct | yes | 0% | 0% | 0 | yes |  | 
rimv-email-074 | professional_services | direct | yes | 0% | 0% | 0 | yes |  | 
rimv-email-075 | nonprofit_donor_relations | warm | yes | 50% | 0% | -50 | yes |  | 
rimv-email-076 | customer_support | warm | yes | 0% | 0% | 0 | yes |  | 
rimv-email-077 | education | warm | yes | 2% | 0% | -2 | no |  | Participation is strong.
rimv-email-078 | workplace | direct | yes | 0% | 0% | 0 | yes |  | 
rimv-email-079 | professional_services | direct | yes | 0% | 0% | 0 | no |  | Arbor Lane LLC packet uses March 18 Draft v7.
rimv-email-080 | logistics | direct | yes | 0% | 0% | 0 | yes |  | 
rimv-email-081 | education | warm | yes | 0% | 0% | 0 | yes |  | 
rimv-email-082 | sales | warm | yes | 0% | 0% | 0 | no |  | Priya Shah at Meridian Lab.
rimv-email-083 | customer_support | warm | yes | 100% | 0% | -100 | no |  | Kettle cost $74 and was bought February 2.; One-year warranty may cover heating failure.
rimv-email-084 | billing_support | warm | yes | 0% | 0% | 0 | no |  | Alex Moreno.
rimv-email-085 | customer_success | warm | yes | 0% | 0% | 0 | yes |  | 
rimv-email-086 | workplace | direct | yes | 0% | 0% | 0 | yes |  | 
rimv-email-087 | customer_support | warm | yes | 0% | 0% | 0 | yes |  | 
rimv-email-088 | education | warm | yes | 98% | 0% | -98 | no |  | Teacher may give feedback on student-written drafts but cannot write paragraphs for students.
rimv-email-089 | vendor_management | warm | yes | 0% | 0% | 0 | yes |  | 
rimv-email-090 | workplace | direct | yes | 0% | 0% | 0 | yes |  | 
rimv-email-091 | sales | warm | yes | 0% | 0% | 0 | yes |  | 
rimv-email-092 | education | warm | yes | 14% | 0% | -14 | yes |  | 
rimv-email-093 | billing_support | warm | yes | 0% | 0% | 0 | yes |  | 
rimv-email-094 | subscription_support | warm | yes | 0% | 0% | 0 | yes |  | 
rimv-email-095 | vendor_management | warm | yes | 0% | 0% | 0 | yes |  | 
rimv-email-096 | workplace | direct | yes | 0% | 0% | 0 | no |  | Marcus Lee.
rimv-email-097 | customer_support | warm | yes | 0% | 0% | 0 | yes |  | 
rimv-email-098 | scheduling | warm | yes | 0% | 0% | 0 | no |  | 6 client stakeholders and 4 internal team members.
rimv-email-099 | customer_success | warm | yes | 0% | 0% | 0 | yes |  | 
rimv-email-100 | professional_services | direct | yes | 0% | 0% | 0 | yes |  | 
