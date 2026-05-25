# Scenario Evaluation Results

Date: 2026-05-25T02:30:16.432Z
Eval mode: smoke
Strategy: adaptive_rewrite_orchestrator
Naturalness threshold: 40%
Cases evaluated: 10
Draft-only cases: 10
Measured cases: 10
Long cases (300+ words): 0
Long customer-support cases (300+ words): 0
Average AI-like signal drop: 3 pts
Rewrite below 50% AI-like signal: 10/10
Final selected rewrites worse than draft: 1/10
Quality-score measured cases: 10
Quality-score improved cases: 2/10
Quality regressions: 7/10
Average quality-score delta: -1.9 pts
Cases using targeted repair: 4/10
Rejected candidate events: 2
Fact preservation or unsupported-addition failures: 8
Customer-usable pass count: 2/10
Strict signal pass count: 2/10

Customer-usable pass requires: rewritten output exists, all expected facts are preserved, no unsupported names/dates/amounts/counts are added, no quality failure or material quality regression is raised, and the selected rewrite is not worse than the draft when scores are available.
Strict signal pass additionally requires scores available and: if the draft is above 40%, the final rewrite is at or below 40%; if the draft is already at or below 40%, the final rewrite does not raise the signal.

## rewrite-draft-001

Scenario: General reply
Tone: Warm
Input word count: 149
Input character count: 783
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 0%
First candidate AI-like signal: 0%
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: 0%
Change: 0 pts
Rejected candidate reasons: none
Facts preserved: no
Missing facts: Maya cannot be added after April 2 if the signed slip and $12 are still missing.
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality score before: 7
Quality score after: 5
Quality score delta: -2 pts
Quality regression: yes
Quality notes: The rewrite improves scannability and warmth with line breaks and empathetic language. | However, it fails to state the critical boundary that Maya cannot be added after April 2 if items are missing, weakening the urgency and exactness of the approval condition. | The original draft, while less structured and more wordy, includes all necessary facts and boundaries.
Quality failure state: no
Quality failure reason: none
Customer-usable pass: no
Strict signal pass: no

Expected facts:
- The student is Maya.
- The trip is the April 9 science museum field trip.
- The record identifier is FieldTrip-4A-09.
- The teacher checked the folder basket, payment envelope, and teacher log on March 28.
- The signed permission slip is not on file.
- The $12 trip payment is not recorded.
- The front office has spare blank forms.
- The deadline is April 2.
- Maya cannot be added after April 2 if the signed slip and $12 are still missing.
- The next step is to send a new signed slip with the $12 trip fee.

Forbidden claims:
- Do not blame Jamie for losing the original form.
- Do not say Maya is already approved for the trip.
- Do not extend the April 2 deadline.
- Do not reduce or waive the $12 fee.

Before:

```text
Hi Jamie, I checked Maya's folder basket, the payment envelope, and my teacher log on
March 28 for the April 9 science museum field trip. The record is FieldTrip-4A-09.
Right now I do not have Maya's signed permission slip on file, and I also do not have
the $12 trip payment recorded. The front office has spare blank forms, so I can send one
home through the office today. The deadline I have to work with is April 2, and I cannot
add Maya to the attendance list after April 2 if the signed slip and $12 are still
missing. Please send a new signed slip with the $12 trip fee, and once both items arrive
I will add Maya to the attendance list. I know this is a little frustrating because you
remember signing the original form, but I need the record to be complete before the trip.
```

After:

```text
Hi Jamie,

On March 28, I checked Maya's folder basket, payment envelope, and teacher log. The signed permission slip for the April 9 science museum field trip (FieldTrip-4A-09) isn't on file, and the $12 payment hasn't come in yet.

I know it's frustrating, especially since you remember signing the original. I just need the record to be complete before the trip. The front office has blank forms available. Please send a new signed slip with the $12 fee by April 2.

Let me know if you have any questions.
```

## rewrite-draft-002

Scenario: General reply
Tone: Warm
Input word count: 117
Input character count: 668
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 0%
First candidate AI-like signal: 0%
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: 0%
Change: 0 pts
Rejected candidate reasons: none
Facts preserved: no
Missing facts: The photo shows damage to the mug.; The delivery address must be confirmed first.; The address is needed by Friday at 3 p.m.
Unsupported facts introduced: none
Forbidden-claim violations: Do not ship anything before address confirmation.
Quality score before: 8
Quality score after: 4
Quality score delta: -4 pts
Quality regression: yes
Quality notes: Original is warmer, more structured, and clear about action needed. Rewrite loses warmth, misstates the link between refund window and replacement, and weakens the call to action by not specifying what to reply with.
Quality failure state: no
Quality failure reason: none
Customer-usable pass: no
Strict signal pass: no

Expected facts:
- The customer is Lena.
- The order identifier is R4821.
- The damaged item is the replacement mug.
- The replacement was marked delivered on May 6.
- The photo shows damage to the mug.
- The matching saucer looks fine.
- Support can send one no-cost replacement mug.
- The delivery address must be confirmed first.
- The full order refund window closed on April 30.
- The address is needed by Friday at 3 p.m.

Forbidden claims:
- Do not promise a full order refund.
- Do not replace the saucer.
- Do not ship anything before address confirmation.
- Do not say the damage was the carrier's fault.

Before:

```text
Hi Lena, thanks for sending the photo of the cracked replacement mug from order R4821.
I checked the order notes this morning, and the replacement was marked delivered on May
6. The photo shows damage to the replacement mug, but the matching saucer looks fine.
Our support policy lets me send one no-cost replacement for the mug after I confirm the
delivery address. I cannot refund the full order from this ticket because the original
refund window closed on April 30. Please reply with the current delivery address by
Friday at 3 p.m., and I will queue the replacement mug. If the address has changed, I
need the new address in writing before I create the shipment.
```

After:

```text
Lena, the replacement mug from order R4821 arrived cracked. It was marked delivered on May 6, and the matching saucer is fine. Since the original refund window closed on April 30, we'll send a no-cost replacement. If your address has changed, please provide the new address in writing before the shipment is created. Reply by Friday at 3 p.m.
```

## rewrite-draft-003

Scenario: General reply
Tone: Warm
Input word count: 132
Input character count: 698
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 0%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 0%
Final selected AI-like signal: 0%
Change: 0 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:may 14 missing_locked:May 14
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality score before: 8
Quality score after: 9
Quality score delta: 1 pts
Quality regression: no
Quality notes: The original draft is clear and covers all points, but uses formal contractions like 'I cannot'. The rewrite adds paragraph breaks for better structure, uses contractions ('can't', 'don't') for a warmer human voice, and maintains all facts. It effectively acknowledges confusion, explains the credit, and asks for confirmation.
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Expected facts:
- The customer is Marco.
- The invoice identifier is INV-8842.
- The plan is Pro Workspace.
- The invoice was issued on May 2.
- The invoice amount is $186.00.
- The billing period is May 1 to May 31.
- Three seats were removed on May 14.
- The available credit is $31.50.
- The $31.50 credit can be applied to the next invoice.
- A full $186.00 refund is not allowed.
- Marco must confirm before May 29.
- The credit would attach to the June invoice.

Forbidden claims:
- Do not promise a full $186.00 refund.
- Do not say the account was canceled.
- Do not change the $31.50 credit amount.
- Do not apply the credit without Marco's confirmation.

Before:

```text
Hi Marco, I reviewed invoice INV-8842 for the Pro Workspace plan. The invoice was issued
on May 2 for $186.00, and it covers the May 1 to May 31 billing period. Your team removed
three seats on May 14. I can apply a $31.50 seat credit to the next invoice because the
seat change happened during the active billing month. I cannot refund the full $186.00
charge because the workspace stayed active and the plan was not canceled during the
refund window. If you want me to apply the $31.50 credit, please confirm before May 29
so I can attach it to the June invoice. I am sorry the earlier note did not explain this
well, but I do not want to promise a refund that the billing policy does not allow.
```

After:

```text
Hi Marco,

I reviewed invoice INV-8842 for the Pro Workspace plan. The invoice was issued on May 2 for $186.00, and it covers the May 1 to May 31 billing period.

Your team removed three seats on May 14. I can apply a $31.50 seat credit to the next invoice because the seat change happened during the active billing month.

I can't refund the full $186.00 charge because the workspace stayed active and the plan was not canceled during the refund window. If you want me to apply the $31.50 credit, please confirm before May 29 so I can attach it to the June invoice.

I am sorry the earlier note did not explain this well, but I don't want to promise a refund that the billing policy doesn't allow.
```

## rewrite-draft-004

Scenario: General reply
Tone: Warm
Input word count: 108
Input character count: 540
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 0%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 0%
Final selected AI-like signal: 0%
Change: 0 pts
Rejected candidate reasons: none
Facts preserved: no
Missing facts: If the sync is back before 2 p.m., QA notes can still go out today.; If the sync is not back before 2 p.m., QA notes move to tomorrow morning.; Sam needs to decide whether to send a client delay note today.
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality score before: 7
Quality score after: 5
Quality score delta: -2 pts
Quality regression: yes
Quality notes: Original draft includes all necessary information, is clear, and concise. | Rewritten text improves scannability with bullets but drops critical details: the conditional timing for QA notes and the decision needed from Sam, reducing actionability and completeness. | The rewrite's strength is a cleaner structure, but it fails to keep the original's must-keep facts, resulting in a less informative update.
Quality failure state: no
Quality failure reason: none
Customer-usable pass: no
Strict signal pass: no

Expected facts:
- The project is the Acorn dashboard.
- Chart cleanup is finished.
- Export button copy is finished.
- Final QA is blocked by the staging data sync.
- The staging data sync failed at 9:20 a.m.
- Priya owns the sync job.
- Priya is checking the logs.
- If the sync is back before 2 p.m., QA notes can still go out today.
- If the sync is not back before 2 p.m., QA notes move to tomorrow morning.
- Sam needs to decide whether to send a client delay note today.
- Another update is due by 2:30 p.m.

Forbidden claims:
- Do not say QA is complete.
- Do not blame Priya for the failure.
- Do not promise the sync will be fixed by 2 p.m.
- Do not send the client delay note without Sam's decision.

Before:

```text
Quick update on the Acorn dashboard: I finished the chart cleanup and the export button
copy, but I am blocked on the final QA pass because the staging data sync failed again
at 9:20 a.m. Priya owns the sync job and is checking the logs now. If the sync is back
before 2 p.m., I can still send the QA notes today. If it is not back by then, the QA
notes move to tomorrow morning. I need Sam to confirm whether we should send the client
a short delay note today or wait until we know the sync status. I will post another
update by 2:30 p.m.
```

After:

```text
Quick update on the Acorn dashboard:

- Chart cleanup finished
- Export button copy finished
- Staging data sync failed, so we're blocked on final QA pass
- Priya owns the sync job and is checking logs

Timing: The sync was expected before 2 p.m. (we flagged this at 9:20 a.m.). I'll post another update by 2:30 p.m.
```

## rewrite-draft-005

Scenario: General reply
Tone: Warm
Input word count: 113
Input character count: 598
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 0%
First candidate AI-like signal: 0%
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: 0%
Change: 0 pts
Rejected candidate reasons: none
Facts preserved: no
Missing facts: Advanced SSO requires a new approval cycle.; A discount requires a new approval cycle.; Dev can reply by June 7 for the current order form.
Unsupported facts introduced: none
Forbidden-claim violations: Do not extend the June 7 expiry date.
Quality score before: 8
Quality score after: 6
Quality score delta: -2 pts
Quality regression: yes
Quality notes: Rewrite is more concise and has a warmer tone, but it omits critical scope boundaries: the new approval cycle for SSO/discount, the explicit reply deadline for the order form, and firmness around expiry. This reduces clarity and actionability while weakening necessary boundaries.
Quality failure state: no
Quality failure reason: none
Customer-usable pass: no
Strict signal pass: no

Expected facts:
- The contact is Dev.
- The account is Northstar.
- The quote identifier is Q-7719.
- The quote is for 18 seats.
- The price is $42 per seat per month.
- The quote includes onboarding.
- The quote includes the admin workspace.
- The quote includes standard email support.
- The quote expires on June 7.
- Advanced SSO requires a new approval cycle.
- A discount requires a new approval cycle.
- Dev can reply by June 7 for the current order form.

Forbidden claims:
- Do not offer a discount.
- Do not include advanced SSO in the current quote.
- Do not extend the June 7 expiry date.
- Do not say the order form has already been sent.

Before:

```text
Hi Dev, thanks again for the call about the Northstar rollout. I attached quote Q-7719
again so it is easier to find. The quote is for 18 seats at $42 per seat per month, and
it includes onboarding, the admin workspace, and the standard email support package. The
quote expires on June 7. I can answer scope questions this week, but I cannot add the
advanced SSO setup or a discount to this quote without a new approval cycle. If the
current quote still matches what you need, please reply by June 7 and I can send the
order form. If the SSO setup is required, I can start a revised quote instead.
```

After:

```text
Hi Dev, just a quick follow-up on Northstar rollout quote Q-7719. It's for 18 seats at $42 per seat per month and includes onboarding, admin workspace, and standard email support. The quote expires on June 7. If you have any scope questions, I can answer them this week. Also, if SSO setup is required, I can start a revised quote. No rush from my side—let me know if anything comes up.
```

## rewrite-draft-006

Scenario: General reply
Tone: Warm
Input word count: 57
Input character count: 292
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 0%
First candidate AI-like signal: 0%
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: 0%
Change: 0 pts
Rejected candidate reasons: none
Facts preserved: no
Missing facts: Both times cannot be held after noon.
Unsupported facts introduced: noon on Friday
Forbidden-claim violations: Do not offer any times other than Thursday at 2:30 p.m. or Friday at 9 a.m.
Quality score before: 9
Quality score after: 4
Quality score delta: -5 pts
Quality regression: yes
Quality notes: The rewrite added unauthorized time options, changing the facts. | The boundary about not holding both times after noon was dropped, weakening clarity. | The original draft was clear and efficient; the rewrite introduced inaccuracies.
Quality failure state: no
Quality failure reason: none
Customer-usable pass: no
Strict signal pass: no

Expected facts:
- The recipient is Ren.
- The original appointment is Thursday, May 16.
- The original appointment time is 11 a.m.
- The room is unavailable.
- The available options are Thursday at 2:30 p.m. and Friday at 9 a.m.
- Ren must choose by Wednesday at noon.
- Both times cannot be held after noon.
- The sender will confirm the selected slot after Ren replies.

Forbidden claims:
- Do not keep the 11 a.m. appointment.
- Do not offer any times other than Thursday at 2:30 p.m. or Friday at 9 a.m.
- Do not hold both options after Wednesday at noon.

Before:

```text
Hi Ren, I need to move our Thursday, May 16 appointment from 11 a.m. because the room is
unavailable. I can offer Thursday at 2:30 p.m. or Friday at 9 a.m. Please choose one by
Wednesday at noon. I cannot hold both times after noon, but I will confirm the selected
slot as soon as you reply.
```

After:

```text
Ren, quick update: the room for your Thursday, May 16 appointment at 11 a.m. isn’t available. Here are some other options: Wednesday at noon, Thursday at 2:30 p.m., Friday at 9 a.m., or after noon on Friday. Let me know your preference by Wednesday at noon, and I’ll confirm the new time.
```

## rewrite-draft-007

Scenario: General reply
Tone: Warm
Input word count: 106
Input character count: 573
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 0%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 0%
Final selected AI-like signal: 0%
Change: 0 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:may 10 missing_locked:May 10
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality score before: 7
Quality score after: 8
Quality score delta: 1 pts
Quality regression: no
Quality notes: Original draft is clear, warm, and concise but lacks paragraph breaks, reducing readability. Rewrite adds line breaks, improving structure and scannability without altering content. Both texts effectively avoid overpromising and maintain respectful boundaries.
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Expected facts:
- The candidate is Alina.
- The role is Senior Support Lead.
- The interview was with the product team.
- The interview date was May 10.
- The panel noted experience with queue operations.
- The panel noted experience with onboarding.
- No final hiring decision has been made.
- The hiring manager is reviewing the last two panel notes this week.
- The next update is expected by Tuesday, May 21.
- Alina does not need to send anything today.
- Alina should reply if availability changes.

Forbidden claims:
- Do not say Alina has an offer.
- Do not say Alina is rejected.
- Do not promise a final decision by May 21.
- Do not ask Alina to send additional materials today.

Before:

```text
Hi Alina, thank you for meeting with the product team on May 10 about the Senior Support
Lead role. The panel enjoyed learning more about your experience with queue operations
and onboarding. We have not made a final hiring decision yet, and I do not want to make
the status sound more final than it is. The hiring manager is reviewing the last two
panel notes this week. I expect to send the next update by Tuesday, May 21. There is
nothing else you need to send today. If your availability changes before then, please
reply to this thread so I can keep the file current.
```

After:

```text
Hi Alina,

Thank you for meeting with the product team on May 10 about the Senior Support Lead role. The panel enjoyed learning more about your experience with queue operations and onboarding.

We have not made a final hiring decision yet, and I don't want to make the status sound more final than it is. The hiring manager is reviewing the last two panel notes this week.

I expect to send the next update by Tuesday, May 21. There is nothing else you need to send today.

If your availability changes before then, please reply to this thread so I can keep the file current.
```

## rewrite-draft-008

Scenario: General reply
Tone: Warm
Input word count: 94
Input character count: 505
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 0%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 32%
Final selected AI-like signal: 32%
Change: 32 pts
Rejected candidate reasons: none
Facts preserved: no
Missing facts: The office has received the report.; The report has not been released to the patient portal yet.; The release request was sent to Dr. Chen's queue.
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality score before: 7
Quality score after: 4
Quality score delta: -3 pts
Quality regression: yes
Quality notes: Original draft is factual and retains all key information, directly stating status and actions. | Rewrite adds warmth and structure but omits critical details: receipt status, unreleased status, and Dr. Chen's queue. | Rewrite changes the 10:15 a.m. reference to message arrival, altering the factual claim about the release request. | Rewrite's vagueness weakens the clinical boundary and may cause confusion about report status.
Quality failure state: no
Quality failure reason: none
Customer-usable pass: no
Strict signal pass: no

Expected facts:
- The recipient is Nora.
- The patient is Eli.
- The lab report is from the May 3 visit.
- The office has received the report.
- The report has not been released to the patient portal yet.
- The sender is not a clinician.
- The sender cannot interpret the results.
- The release request was sent to Dr. Chen's queue.
- The release request was sent today at 10:15 a.m.
- Most release requests are reviewed within two business days.
- New or worsening symptoms should go through the clinic line.

Forbidden claims:
- Do not interpret the lab results.
- Do not say the results are normal.
- Do not promise portal release within two business days.
- Do not tell Eli to wait if symptoms are new or worsening.

Before:

```text
Hi Nora, I checked the portal message about Eli's lab report from the May 3 visit. I can
see that the report is marked received by our office, but it has not been released to
the patient portal yet. I am not a clinician, so I cannot interpret the results in this
message. I sent the release request to Dr. Chen's queue today at 10:15 a.m. Most release
requests are reviewed within two business days. If Eli has new or worsening symptoms,
please call the clinic line instead of waiting for a portal reply.
```

After:

```text
Hi Nora,

Thanks for reaching out about the lab report from Eli's visit with Dr. Chen on May 3. While I'm not a clinician and can't interpret results, I can let you know that most release requests are reviewed within two business days.

One thing to keep in mind: if Eli has any new or worsening symptoms, please call the clinic line instead of waiting for a portal reply. Your message came in today at 10:15 a.m., and we're working on it.

Let me know if you have any other questions.
```

## rewrite-draft-009

Scenario: General reply
Tone: Warm
Input word count: 128
Input character count: 721
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 0%
First candidate AI-like signal: 0%
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: 0%
Change: 0 pts
Rejected candidate reasons: none
Facts preserved: no
Missing facts: The alternative is to use the lockbox code already on file.
Unsupported facts introduced: today by 9 a.m; by 9 a.m
Forbidden-claim violations: none
Quality score before: 8
Quality score after: 6
Quality score delta: -2 pts
Quality regression: yes
Quality notes: Original draft is clear, structured, and includes all necessary details with warmth. Rewritten text omits key facts (plumber's confirmation yesterday, cabinet drying dependency, lockbox alternative) and adds an unsolicited deadline for the photo, reducing clarity and completeness while making the request feel less considerate.
Quality failure state: no
Quality failure reason: none
Customer-usable pass: no
Strict signal pass: no

Expected facts:
- The recipient is Cam.
- The location is Unit 4B.
- The issue is a leak under the kitchen sink.
- The plumber confirmed yesterday that the shutoff valve needs replacement.
- The cabinet base can be dried only after the shutoff valve is replaced.
- The vendor window is Tuesday, May 28 between 9 a.m. and noon.
- Cam must confirm whether someone can provide access.
- The alternative is to use the lockbox code already on file.
- Cam should send one photo today if water has spread past the towel line.
- The sender cannot approve a rent credit from the maintenance thread.
- Access confirmation is needed before the vendor confirmation number can be sent.

Forbidden claims:
- Do not approve a rent credit.
- Do not promise the repair will be fully completed on May 28.
- Do not say the lockbox will be used without confirmation.
- Do not skip the photo request if water has spread.

Before:

```text
Hi Cam, I am following up about the leak under the kitchen sink in Unit 4B. The plumber
confirmed yesterday that the shutoff valve needs to be replaced before the cabinet base
can be dried out. The earliest access window the vendor gave us is Tuesday, May 28
between 9 a.m. and noon. Please confirm whether someone can let the plumber in during
that window or whether we should use the lockbox code already on file. Please also send
one photo of the cabinet floor today if the water has spread past the towel line. I know
this has been inconvenient, but I cannot approve a rent credit from this maintenance
thread. The maintenance file needs the access confirmation first, then I can send the
vendor confirmation number.
```

After:

```text
Cam, following up on the leak under the kitchen sink in Unit 4B. The plumber is scheduled for Tuesday, May 28 between 9 a.m. and noon to replace the shutoff valve. Before I can share the vendor confirmation number, the maintenance file requires your access confirmation. Please confirm that you'll be available or provide access instructions. Additionally, if water has spread past the towel line, send one photo of the cabinet floor today by 9 a.m. Just to clarify, I cannot approve a rent credit from this maintenance thread. Let me know once you've confirmed access, and I'll send the confirmation number right away.
```

## rewrite-draft-010

Scenario: General reply
Tone: Warm
Input word count: 120
Input character count: 660
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 0%
First candidate AI-like signal: 0%
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: 0%
Change: 0 pts
Rejected candidate reasons: none
Facts preserved: no
Missing facts: Inventory count is already closed for the week.; Two cleanup volunteers are needed from noon to 1 p.m.
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality score before: 7
Quality score after: 6
Quality score delta: -1 pts
Quality regression: no
Quality notes: The rewrite improves scannability with paragraph breaks and a friendlier tone, but it omits the reason for not bringing extra food (closed inventory) and the specific number of cleanup volunteers needed, reducing completeness and actionability. The causal link between rain and the indoor location is also removed, making the fact less clear.
Quality failure state: no
Quality failure reason: none
Customer-usable pass: no
Strict signal pass: no

Expected facts:
- The event is Saturday's Park Pantry packing day.
- The event is meeting at Hall B.
- The event is not meeting at the outdoor tables.
- The reason is heavy rain expected after 10 a.m.
- Volunteer check-in starts at 8:15 a.m.
- Packing starts at 9 a.m.
- Volunteers should not bring extra donated food on Saturday.
- Inventory count is already closed for the week.
- Delivery route volunteers should bring a charged phone.
- Delivery route volunteers should check in with Mateo before loading.
- Two cleanup volunteers are needed from noon to 1 p.m.
- Parking is in the east lot only.

Forbidden claims:
- Do not ask volunteers to bring extra donated food.
- Do not say the outdoor tables are still in use.
- Do not change the 8:15 a.m. or 9 a.m. times.
- Do not say the west gate will be open.

Before:

```text
Hello everyone, quick update for Saturday's Park Pantry packing day. We are still
meeting at Hall B, not the outdoor tables, because the forecast says heavy rain after
10 a.m. Volunteer check-in starts at 8:15 a.m., and packing begins at 9 a.m. Please do
not bring extra donated food on Saturday because the pantry team has already closed the
inventory count for this week. If you signed up for delivery routes, bring a charged
phone and check in with Mateo before loading your car. We still need two people for the
cleanup shift from noon to 1 p.m.; reply to this message if you can stay. Parking is in
the east lot only because the west gate will be locked.
```

After:

```text
Hi everyone,

Here are the details for Saturday's Park Pantry packing day.

We're meeting in Hall B, not at the outdoor tables. Check-in starts at 8:15 a.m., and packing begins at 9 a.m.

If you're on a delivery route, bring a charged phone and check in with Mateo before loading your car.

Parking is only in the east lot — the west gate will be locked. Also, please don't bring extra donated food on Saturday.

Heavy rain is expected after 10 a.m., so keep that in mind.

If you can stay for the cleanup shift from noon to 1 p.m., just reply and let us know.

Thanks.
```
