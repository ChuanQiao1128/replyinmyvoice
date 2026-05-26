# C# Rewrite Eval - smoke

Started: 2026-05-26T06:14:24.2191850+00:00
Finished: 2026-05-26T06:17:38.1041790+00:00
Cases evaluated: 8
Successful rewrites: 2/8
Fact pass count: 2/8
Customer-usable pass: 2/8 (output + all must_keep preserved + engine success + no forbidden violation)
Forbidden-claim violations (deterministic screen): 0/8
Naturalness failures recoverable under relaxed gate (rewrite <= 40): 0
Measured rewrites: 2
Average signal drop: 75 pts
Baseline-above-threshold average drop: 75 pts (2 cases)
Rewrites below 50% signal: 2/2
Model calls: 33
Sapling calls: 41
Model: deepseek-v4-pro
Max attempts: 5

| Case | Category | Tone | Usable | Success | Draft | Rewrite | Change | Facts | Forbidden | Error | Missing facts |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- | --- |
rewrite-draft-001 | teacher_parent | warm | no | no | unavailable | unavailable | unavailable | no | 0 | naturalness_gate_failed | The student is Maya.; The trip is the April 9 science museum field trip.; The record identifier is FieldTrip-4A-09.; The teacher checked the folder basket, payment envelope, and teacher log on March 28.; The signed permission slip is not on file.; The $12 trip payment is not recorded.; The front office has spare blank forms.; The deadline is April 2.; Maya cannot be added after April 2 if the signed slip and $12 are still missing.; The next step is to send a new signed slip with the $12 trip fee.
rewrite-draft-002 | customer_support | warm | no | no | unavailable | unavailable | unavailable | no | 0 | naturalness_gate_failed | The customer is Lena.; The order identifier is R4821.; The damaged item is the replacement mug.; The replacement was marked delivered on May 6.; The photo shows damage to the mug.; The matching saucer looks fine.; Support can send one no-cost replacement mug.; The delivery address must be confirmed first.; The full order refund window closed on April 30.; The address is needed by Friday at 3 p.m.
rewrite-draft-003 | billing_support | warm | yes | yes | 99% | 24% | -75 | yes | 0 |  | 
rewrite-draft-006 | scheduling | warm | no | no | unavailable | unavailable | unavailable | no | 0 | naturalness_gate_failed | The recipient is Ren.; The original appointment is Thursday, May 16.; The original appointment time is 11 a.m.; The room is unavailable.; The available options are Thursday at 2:30 p.m. and Friday at 9 a.m.; Ren must choose by Wednesday at noon.; Both times cannot be held after noon.; The sender will confirm the selected slot after Ren replies.
rewrite-draft-045 | sales_followup | warm | no | no | unavailable | unavailable | unavailable | no | 0 | naturalness_gate_failed | The contact is Ingrid.; The quote identifier is Q-5544.; The base quote covers 20 seats.; The seat price is $55 per seat per month.; The base monthly total is $1,100 per month.; The base quote includes core platform, onboarding, and standard email support.; All three options expire on June 20.; Add-on A adds API access for a flat $300 per month (total $1,400/month).; Add-on B is a one-time fee of $1,800 for four dedicated onboarding sessions.; Custom dashboard integration is not in any of the three options.; Custom dashboard integration requires a separate scoping conversation and quote.; Ingrid must reply by June 20 for the order form.
rewrite-draft-061 | customer_support | warm | no | no | unavailable | unavailable | unavailable | no | 0 | naturalness_gate_failed | The customer is Vivienne.; The order identifier is ORD-29447.; The return was opened on June 2.; The ceramic pour-over carafe SKU is CPV-11.; The return label for CPV-11 was emailed on June 3.; The pour-over carafe has not arrived at the warehouse yet.; The refund for CPV-11 is $34.00.; The stainless travel cup SKU is STC-07.; The travel cup arrived on June 5 and was confirmed undamaged and unused.; The $22.50 refund for STC-07 was issued on June 6.; The replacement lid SKU is LID-04 and was cracked on arrival.; A photo of the cracked lid is required by June 10 at noon before a replacement is sent.
rewrite-draft-074 | teacher_parent | warm | yes | yes | 99% | 25% | -74 | yes | 0 |  | 
rewrite-draft-080 | customer_success | warm | no | no | unavailable | unavailable | unavailable | no | 0 | naturalness_gate_failed | The customer is Octavia.; The company is Kessler Group.; The call was on May 16.; The two requested features are bulk role assignment via CSV and a dedicated audit log export API.; Both features are on the roadmap but without a confirmed release date.; The account ID is KG-00412.; The contract runs through August 31.; The renewal rate is $3,400 per month for 40-seat Enterprise plan.; A feature priority request was submitted on May 17, reference FPR-2241.; The sender cannot commit the features will be ready by the September 1 renewal.; An enterprise support roadmap conversation is available upon request.
