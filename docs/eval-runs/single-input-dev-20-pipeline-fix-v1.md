# Scenario Evaluation Results

Date: 2026-05-25T04:07:07.413Z
Eval mode: focused
Strategy: adaptive_rewrite_orchestrator
Naturalness threshold: 40%
Cases evaluated: 20
Draft-only cases: 20
Measured cases: 20
Long cases (300+ words): 0
Long customer-support cases (300+ words): 0
Average AI-like signal drop: 0 pts
Rewrite below 50% AI-like signal: 20/20
Final selected rewrites worse than draft: 0/20
Quality-score measured cases: 20
Quality-score improved cases: 15/20
Quality regressions: 4/20
Average quality-score delta: 0.6 pts
Cases using targeted repair: 0/20
Rejected candidate events: 0
Fact preservation or unsupported-addition failures: 2
Customer-usable pass count: 16/20
Strict signal pass count: 18/20

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
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality score before: 7
Quality score after: 9
Quality score delta: 2 pts
Quality regression: no
Quality notes: Improved scannability and structure with paragraph breaks. | Action step now includes deadline for clarity. | Empathetic tone retained and enhanced with 'Thank you'. | Slightly less proactive by not offering to send form, but still provides necessary information.
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

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

I checked Maya's folder basket, payment envelope, and teacher log on March 28. Right now I do not have Maya's signed permission slip on file, and I also do not have the $12 trip payment recorded for the April 9 science museum field trip (FieldTrip-4A-09).

I know you remember signing the original form, but I need the record to be complete before the trip. The deadline I have to work with is April 2, and I cannot add Maya to the attendance list after April 2 if the signed slip and $12 are still missing.

Please send a new signed permission slip with the $12 trip fee by April 2. The front office has spare blank forms if you need one.

Thank you.
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
Missing facts: The customer is Lena.; The replacement was marked delivered on May 6.; The delivery address must be confirmed first.
Unsupported facts introduced: none
Forbidden-claim violations: Do not ship anything before address confirmation.
Quality score before: 8
Quality score after: 6
Quality score delta: -2 pts
Quality regression: yes
Quality notes: The rewrite is concise but omits greeting with name, the delivery date, and weakens the address confirmation requirement, potentially allowing shipment without verification. The original draft is clearer and holds boundaries better.
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
Thanks for sending the photo. The replacement mug from order R4821 arrived cracked, though the matching saucer looks fine.

Our support policy lets me send one no-cost replacement for the mug. I cannot refund the full order from this ticket because the original refund window closed on April 30.

If your address has changed, I need the new address in writing before I create the shipment. Just reply by Friday at 3 p.m. with that if needed.
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
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: 0%
Change: 0 pts
Rejected candidate reasons: none
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality score before: 7
Quality score after: 9
Quality score delta: 2 pts
Quality regression: no
Quality notes: Rewritten text improves structure with paragraph breaks, enhancing clarity and scannability. | Uses contractions for a more human, conversational tone without losing professionalism. | Maintains all key facts and boundaries from the original draft. | No overpromising or template-like voice added.
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
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: 0%
Change: 0 pts
Rejected candidate reasons: none
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality score before: 7
Quality score after: 8
Quality score delta: 1 pts
Quality regression: no
Quality notes: The rewrite improves readability by splitting the text into logical paragraphs, making the status, conditional timeline, and action request easier to scan. No content or tone changes were made.
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

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
Quick update on the Acorn dashboard: I finished the chart cleanup and the export button copy, but I am blocked on the final QA pass because the staging data sync failed again at 9:20 a.m. Priya owns the sync job and is checking the logs now.

If the sync is back before 2 p.m., I can still send the QA notes today. If it is not back by then, the QA notes move to tomorrow morning.

I need Sam to confirm whether we should send the client a short delay note today or wait until we know the sync status. I will post another update by 2:30 p.m.
```

## rewrite-draft-005

Scenario: General reply
Tone: Warm
Input word count: 113
Input character count: 598
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 0%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: 0%
Change: 0 pts
Rejected candidate reasons: none
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality score before: 7
Quality score after: 8
Quality score delta: 1 pts
Quality regression: no
Quality notes: The rewrite adds paragraph breaks, improving readability and structure without altering any facts. Both maintain a warm but firm tone with clear next steps.
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

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
Hi Dev,

Thanks again for the call about the Northstar rollout. I attached quote Q-7719 again so it is easier to find.

The quote is for 18 seats at $42 per seat per month, and it includes onboarding, the admin workspace, and the standard email support package. The quote expires on June 7.

I can answer scope questions this week, but I can't add the advanced SSO setup or a discount to this quote without a new approval cycle. If the current quote still matches what you need, please reply by June 7 and I can send the order form.

If the SSO setup is required, I can start a revised quote instead.
```

## rewrite-draft-006

Scenario: General reply
Tone: Warm
Input word count: 57
Input character count: 292
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 0%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: 0%
Change: 0 pts
Rejected candidate reasons: none
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality score before: 8
Quality score after: 6
Quality score delta: -2 pts
Quality regression: yes
Quality notes: The original draft is clear, concise, and flows well as a single paragraph. | The rewritten text introduces sentence fragments ('Because the room is unavailable.' and 'Or Friday at 9 a.m.') that disrupt readability and clarity. | The line breaks in the rewrite do not improve structure and make the message feel disjointed.
Quality failure state: no
Quality failure reason: none
Customer-usable pass: no
Strict signal pass: yes

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
Hi Ren,

I need to move our Thursday, May 16 appointment from 11 a.m. Because the room is unavailable.

I can offer Thursday at 2:30 p.m. Or Friday at 9 a.m.

Please choose one by Wednesday at noon. I can't hold both times after noon, but I will confirm the selected slot as soon as you reply.
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
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: 0%
Change: 0 pts
Rejected candidate reasons: none
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality score before: 7
Quality score after: 8
Quality score delta: 1 pts
Quality regression: no
Quality notes: The rewrite improves structure by adding paragraph breaks, making it more readable and scannable. | The slight contraction ('I don't want' vs. 'I do not want') slightly enhances a human, conversational tone. | All factual content is preserved exactly. The boundaries remain clear and appropriate.
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
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: 0%
Change: 0 pts
Rejected candidate reasons: none
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality score before: 8
Quality score after: 9
Quality score delta: 1 pts
Quality regression: no
Quality notes: Original is clear but presented as a single dense paragraph. | Rewrite improves structure with paragraph breaks and adds subtle warmth via contractions, making it more readable and human. | Minor formatting flaw in 'Dr. Chen's queue' line break, but overall enhanced.
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

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

I checked the portal message about Eli's lab report from the May 3 visit. I can see that the report is marked received by our office, but it has not been released to the patient portal yet.

I'm not a clinician, so I can't interpret the results in this message. I sent the release request to Dr.

Chen's queue today at 10:15 a.m. Most release requests are reviewed within two business days.

If Eli has new or worsening symptoms, please call the clinic line instead of waiting for a portal reply.
```

## rewrite-draft-009

Scenario: General reply
Tone: Warm
Input word count: 128
Input character count: 721
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 0%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: 0%
Change: 0 pts
Rejected candidate reasons: none
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality score before: 7
Quality score after: 9
Quality score delta: 2 pts
Quality regression: no
Quality notes: Original draft is a single block, making it harder to scan; rewritten text uses clear paragraph breaks that separate status, access request, photo request, and rent-credit boundary. | The rewrite maintains warmth ('I know this has been inconvenient') and boundaries without overpromising. | Contraction 'can't' adds a slightly more human tone. | Minor formatting error: '9 a.m. And noon' should be lowercase 'and'.
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

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
Hi Cam,

I am following up about the leak under the kitchen sink in Unit 4B. The plumber confirmed yesterday that the shutoff valve needs to be replaced before the cabinet base can be dried out.

The earliest access window the vendor gave us is Tuesday, May 28 between 9 a.m. And noon.

Please confirm whether someone can let the plumber in during that window or whether we should use the lockbox code already on file. Please also send one photo of the cabinet floor today if the water has spread past the towel line.

I know this has been inconvenient, but I can't approve a rent credit from this maintenance thread. The maintenance file needs the access confirmation first, then I can send the vendor confirmation number.
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
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality score before: 7
Quality score after: 9
Quality score delta: 2 pts
Quality regression: no
Quality notes: The rewritten text adds line breaks between different topics, making it much easier to scan. All logistical details are preserved accurately. The tone remains friendly and human.
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

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
Hi everyone, a few updates for Saturday's Park Pantry packing day.

We're meeting at Hall B, not the outdoor tables, because the forecast says heavy rain after 10 a.m. Check-in starts at 8:15 a.m., and packing begins at 9 a.m.

Please do not bring extra donated food on Saturday because the pantry team has already closed the inventory count for this week.

If you signed up for delivery routes, bring a charged phone and check in with Mateo before loading your car.

Parking is in the east lot only; the west gate will be locked.

We still need two people for the cleanup shift from noon to 1 p.m. If you can stay, reply to this message.
```

## rewrite-draft-011

Scenario: General reply
Tone: Warm
Input word count: 107
Input character count: 580
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 0%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: 0%
Change: 0 pts
Rejected candidate reasons: none
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality score before: 6
Quality score after: 8
Quality score delta: 2 pts
Quality regression: no
Quality notes: Original draft is clear but dense in a single paragraph. | Rewritten text improves structure with line breaks, making it easier to read. | Contractions add a slightly warmer, more human voice. | Facts remain unchanged and boundaries are respected.
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Expected facts:
- The customer is Marisol.
- The order identifier is S-4408.
- The SKU is BOWL-BLUE-2.
- The carrier scan shows delivery on May 12 at 4:18 p.m.
- The photo shows a rim chip on one blue bowl.
- The replacement request is open.
- One clear photo of the outer box label is still needed.
- The box-label photo is needed by Thursday at 10 a.m.
- Support can send one replacement bowl after the box-label photo arrives.
- The matching plate set is not part of this replacement.
- The second bowl is not part of this replacement.
- Without the label photo by Thursday at 10 a.m., the request moves to standard review.

Forbidden claims:
- Do not blame the carrier for the damage.
- Do not promise a refund.
- Do not replace the matching plate set.
- Do not replace the second bowl.
- Do not release the replacement before the box-label photo arrives.

Before:

```text
Hi Marisol, I checked order S-4408 for SKU BOWL-BLUE-2. The carrier scan shows the
package was delivered on May 12 at 4:18 p.m. Your photo shows a rim chip on one blue
bowl, and the replacement request is open. I cannot release the replacement yet because
we still need one clear photo of the outer box label by Thursday at 10 a.m. After that
photo arrives, I can send one replacement bowl. The matching plate set and the second
bowl from the order are not part of this replacement. If the label photo does not arrive
by Thursday at 10 a.m., the request moves to standard review.
```

After:

```text
Hi Marisol,

I checked order S-4408 for SKU BOWL-BLUE-2. The carrier scan shows the package was delivered on May 12 at 4:18 p.m.

Your photo shows a rim chip on one blue bowl, and the replacement request is open. I can't release the replacement yet because we still need one clear photo of the outer box label by Thursday at 10 a.m.

After that photo arrives, I can send one replacement bowl. The matching plate set and the second bowl from the order are not part of this replacement.

If the label photo doesn't arrive by Thursday at 10 a.m., the request moves to standard review.
```

## rewrite-draft-012

Scenario: General reply
Tone: Warm
Input word count: 105
Input character count: 552
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 0%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: 0%
Change: 0 pts
Rejected candidate reasons: none
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality score before: 9
Quality score after: 5
Quality score delta: -4 pts
Quality regression: yes
Quality notes: Original draft is clear, cohesive, and concise with a natural flow. Rewrite adds a greeting but fragments sentences, introducing grammatical errors (e.g., '7:42 p.m. That night' and 'If he wants help checking the upload.') that reduce clarity and readability. The rewrite weakens structure without improving warmth or actionability.
Quality failure state: no
Quality failure reason: none
Customer-usable pass: no
Strict signal pass: yes

Expected facts:
- The student is Theo.
- The assignment is the habitat slide deck.
- The assignment was due on May 8.
- The classroom portal shows no completed upload.
- There was one empty file attempt at 7:42 p.m.
- Theo can resubmit through the portal.
- Theo can email a PDF copy.
- The resubmission deadline is Wednesday at 8 a.m.
- Office hours are Tuesday from 3:20 to 3:50 p.m.
- The missing mark stays until the finished file is received.

Forbidden claims:
- Do not blame Theo for ignoring the assignment.
- Do not say the finished slide deck was already received.
- Do not remove the missing mark before the finished file arrives.
- Do not extend the Wednesday 8 a.m. deadline.

Before:

```text
Hi Morgan, I checked Theo's history project record this morning. The habitat slide deck
was due on May 8, but the classroom portal shows no completed upload. I do see one empty
file attempt from Theo at 7:42 p.m. that night, so I am not treating this as him ignoring
the assignment. He can resubmit the finished slide deck through the portal or email me a
PDF copy by Wednesday at 8 a.m. I will be in office hours Tuesday from 3:20 to 3:50 p.m.
if he wants help checking the upload. The missing mark stays in the portal until I have
the finished file.
```

After:

```text
Hi Morgan,

I checked Theo's history project record this morning. The habitat slide deck was due on May 8, but the classroom portal shows no completed upload.

I do see one empty file attempt from Theo at 7:42 p.m. That night, so I'm not treating this as him ignoring the assignment.

He can resubmit the finished slide deck through the portal or email me a PDF copy by Wednesday at 8 a.m. I will be in office hours Tuesday from 3:20 to 3:50 p.m.

If he wants help checking the upload. The missing mark stays in the portal until I have the finished file.
```

## rewrite-draft-013

Scenario: General reply
Tone: Warm
Input word count: 68
Input character count: 363
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 0%
First candidate AI-like signal: 0%
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: 0%
Change: 0 pts
Rejected candidate reasons: none
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality score before: 8
Quality score after: 9
Quality score delta: 1 pts
Quality regression: no
Quality notes: The rewrite improves structure by breaking the text into short, scannable paragraphs, enhancing clarity. | A more conversational tone ('Here's where things stand', 'I'll') adds warmth without sacrificing professionalism. | All critical deadlines, ownerships, and dependencies remain exactly as in the original, preserving actionability. | The original was already concise and effective; the rewrite primarily elevates human voice and formatting.
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Expected facts:
- The team is working on the Beacon handoff.
- The API checklist is done.
- The legal copy is waiting on Mina.
- A decision is needed by 4 p.m. on May 22.
- The decision is whether to launch without the Spanish FAQ.
- Jordan owns the final wording after the decision.
- Tomorrow's QA signoff stays on hold if there is no decision today.
- Another update is due by 5:30 p.m.

Forbidden claims:
- Do not say the legal copy is complete.
- Do not say the Spanish FAQ is ready.
- Do not say launch is approved.
- Do not blame Mina for the wait.

Before:

```text
Team, quick Beacon handoff update: the API checklist is done, but the legal copy is
still waiting on Mina. We need a decision by 4 p.m. on May 22 about whether to launch
without the Spanish FAQ. Jordan owns the final wording once that decision is made. If
we do not decide today, tomorrow's QA signoff should stay on hold. I will send another
update by 5:30 p.m.
```

After:

```text
Here's where things stand with the Beacon handoff. The API checklist is done. We're still waiting on Mina for the legal copy.

We need a decision by 4 p.m. on May 22 about whether to launch without the Spanish FAQ. Jordan owns the final wording once that decision is made. If we don't decide today, tomorrow's QA signoff should stay on hold.

I'll send another update by 5:30 p.m.
```

## rewrite-draft-014

Scenario: General reply
Tone: Warm
Input word count: 127
Input character count: 681
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 0%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: 0%
Change: 0 pts
Rejected candidate reasons: none
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality score before: 7
Quality score after: 9
Quality score delta: 2 pts
Quality regression: no
Quality notes: Original draft is clear and respectful but delivered as a single block, which reduces scannability. | Rewrite improves structure with paragraph breaks, making the timeline, policy rule, and available remedy easier to digest. | Rewritten text uses slightly more conversational contractions ('I can't') without losing professionalism, enhancing human voice. | Both versions correctly follow the prescribed order: timeline, policy boundary, then July credit path. | Rewrite maintains all factual accuracy and does not overpromise or weaken boundaries.
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Expected facts:
- The customer is Tomas.
- The account identifier is A-913.
- The June invoice charge is $312.40.
- The charge posted on June 1.
- The plan is Growth Plus.
- The billing cycle is June 1 through June 30.
- Tomas submitted the downgrade request on May 29.
- The downgrade was approved on June 3.
- Changes approved after the cycle starts apply to the next cycle.
- The downgrade cannot be backdated from this ticket.
- A full June refund is not available from this ticket.
- A $48.20 courtesy credit can be applied to the July invoice if Tomas confirms by June 10.

Forbidden claims:
- Do not promise a full June refund.
- Do not backdate the downgrade.
- Do not change the $48.20 credit amount.
- Do not apply the credit without Tomas confirming by June 10.
- Do not say the card ending in 2044 was removed.

Before:

```text
Hi Tomas, I reviewed account A-913 and the June invoice. The $312.40 charge posted on
June 1 for the Growth Plus monthly cycle from June 1 through June 30. I see the
downgrade request you submitted on May 29, but it was approved on June 3, after the new
cycle had already started. Under the billing rule we use, plan changes approved after
the cycle starts apply to the next cycle, not the active one. I cannot backdate the
downgrade or refund the full June charge from this ticket. What I can do is apply a
$48.20 courtesy credit to the July invoice if you confirm by June 10. The card ending in
2044 remains the payment method on file unless you update it from billing settings.
```

After:

```text
Hi Tomas,

I reviewed account A-913 and the June invoice. The $312.40 charge posted on June 1 for the Growth Plus monthly cycle from June 1 through June 30.

I see the downgrade request you submitted on May 29, but it was approved on June 3, after the new cycle had already started. Under the billing rule we use, plan changes approved after the cycle starts apply to the next cycle, not the active one.

I can't backdate the downgrade or refund the full June charge from this ticket. What I can do is apply a $48.20 courtesy credit to the July invoice if you confirm by June 10.

The card ending in 2044 remains the payment method on file unless you update it from billing settings.
```

## rewrite-draft-015

Scenario: General reply
Tone: Warm
Input word count: 114
Input character count: 628
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 0%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: 0%
Change: 0 pts
Rejected candidate reasons: none
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality score before: 8
Quality score after: 9
Quality score delta: 1 pts
Quality regression: no
Quality notes: The original draft is clear and concise but presented as a single block; the rewrite improves readability and scannability by breaking it into shorter paragraphs, while preserving all facts and tone. Both make the two next steps clear without pressure.
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Expected facts:
- The contact is Priya.
- The company is Harbor Lane.
- The meeting date was May 15.
- The quarterly export is important for the finance team.
- The starter rollout includes standard reporting.
- The starter rollout includes onboarding.
- The starter rollout includes 12 seats.
- The price is $960 per month.
- Custom SSO is not included in the starter rollout.
- A 15 percent discount requires finance approval.
- A revised SSO scope can be sent by May 20.

Forbidden claims:
- Do not include custom SSO in the starter rollout.
- Do not offer a 15 percent discount without finance approval.
- Do not change the $960 per month price.
- Do not say the agreement has already been sent.

Before:

```text
Hi Priya, thanks for meeting with me on May 15 about the Harbor Lane rollout. I heard
that the quarterly export is important for your finance team, and the starter rollout
we discussed includes standard reporting, onboarding, and 12 seats at $960 per month.
It does not include custom SSO work. I can send a revised scope for SSO, but I cannot
add it to the starter rollout or offer a 15 percent discount without finance approval.
If the starter scope is enough, I can send the agreement as-is. If SSO is required, I
can send the revised scope by May 20 so your team can compare both options before the
next procurement review.
```

After:

```text
Hi Priya,

Thanks for meeting with me on May 15 about the Harbor Lane rollout. I heard that the quarterly export is important for your finance team, and the starter rollout we discussed includes standard reporting, onboarding, and 12 seats at $960 per month.

It doesn't include custom SSO work. I can send a revised scope for SSO, but I can't add it to the starter rollout or offer a 15 percent discount without finance approval.

If the starter scope is enough, I can send the agreement as-is. If SSO is required, I can send the revised scope by May 20 so your team can compare both options before the next procurement review.
```

## rewrite-draft-016

Scenario: General reply
Tone: Warm
Input word count: 113
Input character count: 655
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 0%
First candidate AI-like signal: 0%
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: 0%
Change: 0 pts
Rejected candidate reasons: none
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality score before: 6
Quality score after: 8
Quality score delta: 2 pts
Quality regression: no
Quality notes: The rewrite omits the fact that Dr. Mallory ordered the test, which is a critical factual omission. | Rewrite improves structure with clear paragraph breaks, concise phrasing, and a friendly tone while maintaining the medical-advice boundary and urgent-path instruction.
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Expected facts:
- The recipient is Elaine.
- The patient is Priya Shah.
- The report is an imaging report from the June 4 test.
- The ordering provider is Dr. Mallory.
- The office received the imaging addendum this morning.
- Dr. Mallory has not finished the provider review.
- The report is still marked pending in the portal.
- The sender is on the admin team.
- The sender cannot explain the findings.
- The sender cannot say whether medication should change.
- The normal callback window is two business days after provider sign-off.
- Chest pain, trouble breathing, or new severe symptoms should go to the urgent clinic line now.

Forbidden claims:
- Do not explain the imaging findings.
- Do not say the results are normal.
- Do not promise a callback before provider sign-off.
- Do not tell Priya to wait for the portal if urgent symptoms are present.

Before:

```text
Hi Elaine, I checked the portal thread for Priya Shah's imaging report from the June 4
test ordered by Dr. Mallory. Our office received the imaging addendum this morning, but
Dr. Mallory has not finished the provider review yet, so the report is still marked
pending in the portal. I am on the admin team, so I cannot explain the findings or say
whether any medication should change. After the provider signs the review, the normal
callback window is two business days. If Priya has chest pain, trouble breathing, or
new severe symptoms, please use the urgent clinic line now instead of waiting for a
portal update. The portal thread MR-220 remains open.
```

After:

```text
Hi Elaine,

Quick update on Priya Shah’s imaging from June 4: the addendum came in this morning. Dr. Mallory has not finished the provider review yet, so the report is still marked pending in the portal. Portal thread MR-220 is still open.

Just to clarify, I am on the admin team, so I cannot explain the findings or say whether any medication should change. After the provider signs the review, the normal callback window is two business days.

If Priya has chest pain, trouble breathing, or new severe symptoms, please use the urgent clinic line now instead of waiting for a portal update.

Thanks,
Admin team
```

## rewrite-draft-017

Scenario: General reply
Tone: Warm
Input word count: 100
Input character count: 504
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 0%
First candidate AI-like signal: 0%
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: 0%
Change: 0 pts
Rejected candidate reasons: none
Facts preserved: no
Missing facts: Drew should send one updated photo today if the rail has gotten worse.
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality score before: 7
Quality score after: 4
Quality score delta: -3 pts
Quality regression: yes
Quality notes: The original draft is clear, actionable, and includes all necessary details in a single paragraph. | The rewrite improves structure with separate paragraphs but drops the critical photo request, making it less complete and actionable. | Omission of the photo dependency weakens the boundary around Drew's responsibility to provide an update if the situation worsened. | The rewrite also begins with 'Just to clarify,' which may sound superfluous or slightly templated, but the major flaw is the missing fact.
Quality failure state: no
Quality failure reason: none
Customer-usable pass: no
Strict signal pass: no

Expected facts:
- The tenant is Drew.
- The address is 18 Maple, Unit 2.
- The issue is a loose stair rail.
- The contractor can inspect Monday, June 17.
- The inspection window is between 1 p.m. and 3 p.m.
- Entry permission is needed if Drew is not home.
- There is no lockbox code on file for this unit.
- Drew should reply yes or no by Friday at 5 p.m.
- Drew should send one updated photo today if the rail has gotten worse.
- The repair will not be marked complete until contractor inspection.

Forbidden claims:
- Do not enter the unit without permission.
- Do not say a lockbox code is on file.
- Do not blame Drew for the delay.
- Do not say the repair is already complete.

Before:

```text
Hi Drew, I am checking on the loose stair rail at 18 Maple, Unit 2. The contractor can
inspect it Monday, June 17 between 1 p.m. and 3 p.m. I need your permission before
anyone enters if you are not home, because there is no lockbox code on file for this
unit. Please reply yes or no by Friday at 5 p.m. If the rail has gotten worse since your
last message, please send one updated photo today so I can add it to the work order. I
will not mark the repair complete until the contractor has inspected it.
```

After:

```text
Drew,

Just to clarify, there's a loose stair rail at 18 Maple, Unit 2. I need your permission before anyone enters if you are not home, because there is no lockbox code on file for this unit.

The contractor can stop by Monday, June 17 between 1 p.m. and 3 p.m. Please reply yes or no by Friday at 5 p.m. I will not mark the repair complete until the contractor has inspected it.
```

## rewrite-draft-018

Scenario: General reply
Tone: Warm
Input word count: 99
Input character count: 532
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 0%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: 0%
Change: 0 pts
Rejected candidate reasons: none
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality score before: 7
Quality score after: 8
Quality score delta: 1 pts
Quality regression: no
Quality notes: The original draft is clear and warm but presented as a single block; the rewrite adds paragraph breaks for better structure and readability. The rewrite uses 'don't' instead of 'do not', slightly increasing conversational tone. Otherwise, content and boundaries are identical. Minor improvement in formatting and human voice.
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Expected facts:
- The candidate is Noor.
- The role is Operations Coordinator.
- Noor completed the phone screen on May 9.
- The application is in hiring manager review.
- No decision has been made yet.
- The next review date is May 16.
- Luis is the recruiter contact.
- Luis will send an update after the review and no later than May 17.
- Noor does not need to send more materials right now.
- Noor should reply if availability changes before then.

Forbidden claims:
- Do not say Noor has an offer.
- Do not say Noor is rejected.
- Do not promise a decision before the review is finished.
- Do not ask Noor to send more materials right now.

Before:

```text
Hi Noor, thank you again for completing the phone screen on May 9 for the Operations
Coordinator role. Your application is now in hiring manager review. We have not made a
decision yet, and I do not want to suggest there is an offer or rejection before that
review is finished. The next review date is May 16. Luis is the recruiter contact for
this role, and he will send an update after that review, no later than May 17. You do
not need to send more materials right now, but please reply if your availability changes
before then.
```

After:

```text
Hi Noor,

Thank you again for completing the phone screen on May 9 for the Operations Coordinator role. Your application is now in hiring manager review.

We have not made a decision yet, and I don't want to suggest there is an offer or rejection before that review is finished. The next review date is May 16.

Luis is the recruiter contact for this role, and he will send an update after that review, no later than May 17. You don't need to send more materials right now, but please reply if your availability changes before then.
```

## rewrite-draft-019

Scenario: General reply
Tone: Warm
Input word count: 105
Input character count: 566
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 0%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: 0%
Change: 0 pts
Rejected candidate reasons: none
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality score before: 6
Quality score after: 8
Quality score delta: 2 pts
Quality regression: no
Quality notes: The rewrite adds paragraph breaks, improving structure and readability. | Contractions like 'can't' and 'don't' enhance the human voice without sacrificing professionalism. | All facts and boundaries are preserved exactly. | The original draft was clear but lacked visual structure; the rewrite makes it more scannable and friendly.
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Expected facts:
- The donor is Aisha.
- The donation amount is $250.
- The campaign is Weekend Meals.
- The donation was received on May 4.
- The receipt was sent on May 6.
- The receipt was sent to aisha@example.org.
- The receipt can be used for donor records.
- The sender cannot give tax advice.
- The sender cannot say whether the gift is deductible for Aisha's situation.
- Donor name or email corrections must be requested by May 15.
- Mia can update the record before the monthly export.

Forbidden claims:
- Do not say the gift is tax deductible.
- Do not change the $250 amount.
- Do not say the receipt was not sent.
- Do not promise a specific program report attached to the receipt.

Before:

```text
Hi Aisha, thank you for your $250 donation to the Weekend Meals campaign. We received
the donation on May 4, and the receipt was sent on May 6 to aisha@example.org. The
receipt can be used for your records, but I cannot give tax advice or say whether the
gift is deductible for your situation. If the donor name or email address on the receipt
needs to be corrected, please reply by May 15 so Mia can update the record before the
monthly export. The campaign update will go out later this month, but I do not have a
specific program report attached to this receipt.
```

After:

```text
Hi Aisha,

Thank you for your $250 donation to the Weekend Meals campaign. We received the donation on May 4, and the receipt was sent on May 6 to aisha@example.org.

The receipt can be used for your records, but I can't give tax advice or say whether the gift is deductible for your situation. If the donor name or email address on the receipt needs to be corrected, please reply by May 15 so Mia can update the record before the monthly export.

The campaign update will go out later this month, but I don't have a specific program report attached to this receipt.
```

## rewrite-draft-020

Scenario: General reply
Tone: Warm
Input word count: 119
Input character count: 623
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 0%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: 0%
Change: 0 pts
Rejected candidate reasons: none
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality score before: 7
Quality score after: 6
Quality score delta: -1 pts
Quality regression: no
Quality notes: Original draft is clear and concise but lacks paragraph breaks for readability. | Rewrite adds line breaks but introduces punctuation errors ('In Room 12,' as a sentence fragment, odd splits like 'at 3:30 p.m.

On September 6') that reduce clarity and professionalism. | Rewrite fails to improve upon the original; it maintains facts and boundaries but formatting hurts overall quality.
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Expected facts:
- The student is Jamal.
- The program is after-school robotics club.
- The missing form is Activity-ROBO-22.
- The $35 materials fee is still missing.
- The deadline is September 6 at 3:30 p.m.
- The deadline location is Room 12.
- The bus roster is sent at 4 p.m. that day.
- Jamal is on the interest list.
- Jamal is not enrolled yet.
- Jamal cannot move ahead of students with completed forms and fees.
- A blank form can be picked up from the main office.
- A scanned signed copy can be sent before the deadline.

Forbidden claims:
- Do not say Jamal is already enrolled.
- Do not waive or reduce the $35 materials fee.
- Do not extend the September 6 at 3:30 p.m. deadline.
- Do not move Jamal ahead of students with completed forms and fees.
- Do not say the bus roster can be changed after 4 p.m.

Before:

```text
Hi Mr. Rivera, I checked Jamal's after-school robotics club file. The Activity-ROBO-22
permission form and the $35 materials fee are both still missing. The deadline is
September 6 at 3:30 p.m. in Room 12, because the office has to send the bus roster at 4
p.m. that day. Jamal is on the interest list, but he is not enrolled yet, and I cannot
move him ahead of students with completed forms and fees. You can pick up a blank form
from the main office or send a scanned signed copy before the deadline. If the form and
fee are not in Room 12 by 3:30 p.m. on September 6, he will stay on the waitlist for
the first session.
```

After:

```text
Hi Mr. Rivera,

I checked Jamal's after-school robotics club file. The Activity-ROBO-22 permission form and the $35 materials fee are both still missing.

The deadline is September 6 at 3:30 p.m. In Room 12, because the office has to send the bus roster at 4 p.m.

That day. Jamal is on the interest list, but he is not enrolled yet, and I can't move him ahead of students with completed forms and fees.

You can pick up a blank form from the main office or send a scanned signed copy before the deadline. If the form and fee are not in Room 12 by 3:30 p.m.

On September 6, he will stay on the waitlist for the first session.
```
