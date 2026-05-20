# Scenario Evaluation Results

Date: 2026-05-20T08:53:13.604Z
Eval mode: smoke
Strategy: adaptive_rewrite_orchestrator
Naturalness threshold: 40%
Cases evaluated: 11
Draft-only cases: 4
Measured cases: 11
Long cases (300+ words): 7
Long customer-support cases (300+ words): 5
Average AI-like signal drop: 66 pts
Rewrite below 50% AI-like signal: 11/11
Final selected rewrites worse than draft: 0/11
Cases using targeted repair: 11/11
Rejected candidate events: 13
Fact preservation or unsupported-addition failures: 0
Customer-usable pass count: 11/11
Strict signal pass count: 11/11

Customer-usable pass requires: rewritten output exists, all expected facts are preserved, no unsupported names/dates/amounts/counts are added, no quality failure is raised, and the selected rewrite is not worse than the draft when scores are available.
Strict signal pass additionally requires scores available and: if the draft is above 40%, the final rewrite is at or below 40%; if the draft is already at or below 40%, the final rewrite does not raise the signal.

## draft-only-01b-teacher-jordan-long-polished

Scenario: General reply
Tone: Warm
Input word count: 253
Input character count: 1490
Diagnosis tags: stock_opening
Rewrite plan: Target stock_opening while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: 100%
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 1%
Change: -99 pts
Rejected candidate reasons: initial: Fact reconstruct final naturalness gate. | repair: Strong-model escalation naturalness and fact gate.
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Expected facts:
- Monica
- Jordan
- three assignments
- past two weeks
- reading response
- vocabulary practice
- short reflection paragraph
- last Friday
- class discussions
- this Friday at 5 p.m.
- partial credit
- does not need to redo
- Tuesday
- Thursday
- Ms. Carter

Before:

```text
Hi Monica,

Thank you for reaching out and sharing your concerns about Jordan's grade. I understand how stressful this situation can feel, and I appreciate you checking in so we can work together to help him catch up.

Right now, Jordan is missing three assignments from the past two weeks: the reading response, the vocabulary practice, and the short reflection paragraph from last Friday. He continues to participate in class discussions, and I want to recognize that his verbal contributions have been thoughtful. However, the missing written work is beginning to have a significant impact on his overall grade.

I recommend that Jordan begin with the reading response and vocabulary practice, since those should be the quickest to complete. After that, he can work on the short reflection paragraph. If he submits all three assignments by this Friday at 5 p.m., I will still accept them for partial credit.

I also want to clarify that he does not need to redo any work he has already completed. This only applies to the three missing assignments listed above. If he has any questions about the instructions, he is welcome to come see me during lunch on Tuesday or Thursday, and I would be happy to walk him through what is expected.

Thank you again for your support and for helping Jordan take responsibility for catching up. I believe he can get back on track if he completes the missing work this week and builds a more consistent routine moving forward.

Best regards,

Ms. Carter
```

After:

```text
Hi Monica,

For Jordan, the missing work is still the main issue. He has three assignments missing from the past two weeks: reading response, vocabulary practice, the short reflection paragraph from last Friday. He has been participating in class discussions, but the grade is being pulled down by the missing written work.

Have him start with the reading response and vocabulary practice, since those should be the quickest. Then he can work on the short reflection paragraph from last Friday. If he submits all three by this Friday at 5 p.m., I can still give partial credit.

He does not need to redo work he already completed; this is only about the missing assignments. He can also come by during lunch on Tuesday or Thursday if any instructions are unclear.

Best regards,
Ms. Carter
```

## draft-only-16b-implementation-schedule-regression

Scenario: General reply
Tone: Warm
Input word count: 352
Input character count: 2027
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 0%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 0%
Change: 0 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:cannot approve missing:onboarding timeline missing:training session missing:cannot approve shift changes missing:approved by column missing:weekly reconciliation report missing:sms reminders are not part of this phase missing:additional nzd $480 setup fee missing:phase-two item missing:regional managers missing:tuesday, 4 june missing:thursday, 6 june missing:friday, 7 june missing:4 june missing:6 june missing:tuesday missing:thursday missing:friday missing:10:30 a.m. missing:2 p.m. missing:after 2 p.m. missing:avery unsupported:remember unsupported:let unsupported:overall
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Expected facts:
- Morgan
- Tuesday, 4 June
- Thursday, 6 June at 10:30 a.m.
- Friday, 7 June after 2 p.m.
- Monday, 17 June
- user-permission issue
- warehouse supervisors
- cannot approve shift changes
- approved by column
- weekly reconciliation report
- SMS reminders are not part of this phase
- NZD $480
- phase-two item
- regional managers
- Avery

Before:

```text
Hi Morgan,

Thank you for sending over the revised onboarding timeline and the notes from yesterday's implementation call. I reviewed the update with our operations lead this morning, and I want to clarify a few points before we confirm the schedule with the broader team.

The overall timeline still looks workable, but the training session originally planned for Tuesday, 4 June will need to move. Our payroll team has a quarter-end review that afternoon, and three of the supervisors who need the training will not be available. The best replacement time on our side is Thursday, 6 June at 10:30 a.m. If that does not work for your team, the backup option is Friday, 7 June after 2 p.m.

Please do not change the go-live date yet. We are still aiming for Monday, 17 June, as long as the user-permission issue is resolved by the end of next week. The main blocker is that the warehouse supervisors can see the dashboard, but they cannot approve shift changes. We also noticed that the export file is missing the approved by column, which our finance team needs for the weekly reconciliation report.

For now, please keep the project scope the same. We are not adding the SMS reminder feature in this phase, and we are not ready to approve the additional NZD $480 setup fee for that feature. I do want the team to document it as a possible phase-two item, because our regional managers may ask about it later.

Could you send us an updated implementation note that includes the proposed training time change, the current permission issue for warehouse supervisors, the missing approved by column in the export, a clear note that SMS reminders are not part of this phase, and confirmation that the go-live date is still Monday, 17 June unless the permission issue is not resolved?

Please keep the tone calm and practical. I do not want the message to sound like we are blaming your team. I just want the internal stakeholders to understand what changed, what is still blocked, and what we are not approving yet.

Best,

Avery
```

After:

```text
Hi Morgan,

Thanks for sending the revised onboarding timeline and notes from yesterday's implementation call.

The training session needs to move from Tuesday, 4 June because payroll has a quarter-end review and three supervisors are unavailable. Our preferred replacement is Thursday, 6 June at 10:30 a.m.; if that does not work, Friday, 7 June after 2 p.m. is the backup.

Please keep Monday, 17 June as the go-live date for now, as long as the user-permission issue is resolved by the end of next week. The current blocker is that the warehouse supervisors can see the dashboard but cannot approve shift changes. The export is also missing the approved by column, which finance needs for the weekly reconciliation report.

Please keep the project scope the same. SMS reminders are not part of this phase, and we are not approving the additional NZD $480 setup fee. It is fine to document SMS reminders as a possible phase-two item in case regional managers ask later.

Could you send an updated implementation note that covers the training time change, the permission issue, the missing approved by column, the SMS scope note, and confirmation that the go-live date is still Monday, 17 June unless the permission issue is not resolved? Please keep it calm and practical so stakeholders understand what changed without sounding like we are blaming your team.

Best,
Avery
```

## draft-only-12-support-seat-count

Scenario: General reply
Tone: Direct
Input word count: 38
Input character count: 219
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 1%
Final selected AI-like signal: 33%
Change: -67 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:base plan missing:base plan did not change missing:18 active seats missing:15 regular seats unsupported:here unsupported:as missing_locked:did not change malformed:dangling_closing
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Expected facts:
- Priya
- 18 active seats
- 15 regular seats
- NZD $126
- three temporary contractors
- May
- base plan did not change

Before:

```text
Hi Priya, the usage report shows 18 active seats, but the renewal only approved 15 regular seats. The extra NZD $126 appears tied to three temporary contractors who were active during May. The base plan did not change.
```

After:

```text
Hi Priya,

The usage report shows 18 active seats, but the renewal only approved 15 regular seats. The extra NZD $126 appears tied to three temporary contractors who were active during May.

The base plan did not change.
```

## reply-03-parent-question

Scenario: Email or message reply
Tone: Direct
Input word count: 70
Input character count: 420
Diagnosis tags: stock_opening, corporate_polish, low_specificity
Rewrite plan: Target stock_opening, corporate_polish, low_specificity while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 3%
Change: -97 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:one missing exit ticket
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Expected facts:
- Kai
- two missing participation activities
- one missing exit ticket

Before:

```text
Thank you for reaching out regarding Kai's grade. I understand your concern. The grade change is due to two missing participation activities and one missing exit ticket. I can provide additional details if needed and would be happy to discuss this matter further at your earliest convenience.
```

After:

```text
The grade change is from two missing participation activities and one missing exit ticket for Kai.

I can send the details if that would help.
```

## support-04-priya-billing-long-regression

Scenario: Customer support
Tone: Warm
Input word count: 541
Input character count: 3056
Diagnosis tags: stock_opening, over_explained, over_safe_tone
Rewrite plan: Target stock_opening, over_explained, over_safe_tone while preserving the user's facts.
Draft AI-like signal: 89%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 0%
Change: -89 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:base plan missing:finance manager missing:nzd $126 missing:18 active seats missing:15 regular seats missing:priya unsupported:let missing_locked:No change to the base plan.
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Expected facts:
- Priya
- 18 active seats
- 15 regular seats
- NZD $126
- May 8
- finance manager
- base plan

Before:

```text
Hi Priya,

Thanks for reaching out. I can understand why this looks confusing, especially with the temporary contractors added for only a short project.

From what you described, the most likely reason the May invoice preview is higher is that the three temporary contractors were counted as active seats during May. Even if a user only joins for part of the month, they can still create a prorated seat charge for the days they had access. In plain English: you are not necessarily being charged for three full extra seats for the whole month, but the system may be charging for the portion of May when those users were active.

The base plan itself does not appear to be the issue based on the information here. The difference seems more likely related to the active seat count increasing from 15 to 18 during the billing period.

To help you explain this internally, you could summarize it to finance like this:

“The May invoice preview is higher because three temporary contractor accounts were active in the workspace during the month. The additional NZD $126 appears to be a prorated usage charge for those temporary seats, rather than a change to our base plan.”

For next steps, please check whether the three contractor accounts are still showing as active in the workspace. If they are, you can remove or deactivate them before the invoice is finalized so they do not continue affecting future billing. We will not make any changes on your behalf unless you ask us to.

Please send us the names or email addresses of the three contractor users if you would like us to help confirm whether they are still active.

Thanks,
Reply In My Voice Team
```

After:

```text
Hi Priya,

Thanks for laying this out. The jump looks tied to the three temporary contractors being counted during May, not a base plan change.

That explains why the dashboard shows 18 active seats instead of the 15 regular seats your team approved. The NZD $126 increase looks like the prorated charge for the days those accounts had access.

For your finance manager, you can say: "The May invoice preview is higher because the three temporary contractors were active during the month. The extra charge appears to be prorated seat usage, not a change to the base plan."

Before the invoice is finalized, check whether those contractor accounts are still active. They may still be active if they were not removed after May 8. If you send over their names or email addresses, we can help confirm their status. We will not change anything unless you ask us to.
```

## support-05-data-export-long

Scenario: Customer support
Tone: Direct
Input word count: 383
Input character count: 2253
Diagnosis tags: over_safe_tone, support_template_voice
Rewrite plan: Target over_safe_tone, support_template_voice while preserving the user's facts.
Draft AI-like signal: 0%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 0%
Change: 0 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:northeast region missing:data is still safe missing:northeast
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Expected facts:
- custom tags column
- April
- May
- Monday
- 10am
- Northeast region

Before:

```text
Thank you for contacting us regarding the missing custom tags column in your April and May CSV exports. We understand the importance of this information for your campaign reconciliation and Monday board packet. Our team is currently investigating the export behavior to determine whether the issue is related to the dashboard filters or the CSV generation process. Please be assured that there is no indication that the underlying campaign data has been deleted or changed. In the meantime, you may attempt to generate the report again using the standard dashboard export workflow. We will provide an update as soon as more information is available.

We recognize that your team is relying on this export for an internal reporting deadline and that the missing column creates extra manual work. The key point to communicate is that the missing CSV column does not automatically mean the underlying tag data is gone. It may be a report-generation issue or a configuration issue in the export path. A useful reply should separate what is confirmed, what is still being checked, and what the customer can do before Monday at 10am without implying that engineering has already found the cause.
```

After:

```text
The April and May CSV exports for the Northeast region are missing the custom tags column, but the underlying campaign data is still safe.

We are checking the export job. If the check confirms the issue, we will send a corrected file before Monday at 10am.
```

## support-07-plan-change-long

Scenario: Customer support
Tone: Warm
Input word count: 364
Input character count: 2173
Diagnosis tags: stock_opening, over_safe_tone
Rewrite plan: Target stock_opening, over_safe_tone while preserving the user's facts.
Draft AI-like signal: 0%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 0%
Final selected AI-like signal: 0%
Change: 0 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:starter plan missing:shared templates missing:finance thread missing:starter unsupported:following
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Expected facts:
- Starter plan
- Team plan
- May 3
- shared templates
- old plan credit
- new plan charge

Before:

```text
Thank you for reaching out regarding the invoice preview after your recent plan change from Starter to Team. We understand that billing adjustments can be confusing when prorated charges and credits appear as separate invoice lines. The invoice preview may show both the credit for unused time on the Starter plan and the charge for the Team plan for the remaining portion of the billing period. This does not necessarily mean that you are being charged twice. Rather, it may reflect the standard prorated adjustment process. Please review the line items carefully, and contact us if you would like additional assistance.

A good answer should explain the likely invoice structure without sounding defensive. It should say that separate credit and charge lines can appear during a mid-cycle plan change, and that this usually reflects proration rather than double billing. It should preserve May 3, Starter, Team, shared templates, old plan credit, and new plan charge. It should invite the customer to send a screenshot or invoice line items if they want the team to confirm the preview, but it should not promise an adjustment or refund.
```

After:

```text
Hi finance team,

The Starter plan to Team plan change on May 3 for shared templates can show as separate invoice lines.

In plain English, the old plan credit and the new plan charge usually appear separately during proration, so that layout is usually not a duplicate charge.

You can use that explanation in the finance thread.

If you send the invoice preview or line items, we can confirm whether the preview is showing the expected credit-and-charge adjustment before the invoice is finalized.
```

## support-08-delayed-response-long

Scenario: Customer support
Tone: Direct
Input word count: 308
Input character count: 1918
Diagnosis tags: corporate_polish
Rewrite plan: Target corporate_polish while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: 100%
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 0%
Change: -100 pts
Rejected candidate reasons: initial: Fact reconstruct final naturalness gate. | repair: Strong-model escalation naturalness and fact gate.
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Expected facts:
- ticket #4821
- Friday
- duplicate notifications
- before noon
- pause the campaign

Before:

```text
Thank you for following up on ticket #4821 regarding the duplicate notifications being sent to client contacts. We apologize for the continued inconvenience. Our engineering team is actively investigating the matter and working to identify the root cause. At this time, we are still reviewing the relevant delivery logs and notification events. We understand that your account team needs guidance before noon regarding whether to pause the campaign. We will provide an update as soon as possible once additional information is available.

The reply should avoid a vague 'we are investigating' message because the customer already knows that. It should keep ticket #4821, Friday, duplicate notifications, delivery logs, noon, and the pause decision. It should say what is confirmed in plain language: duplicate notifications are still being reviewed and logs are being checked. It should also be honest about what is not confirmed yet. If no firm resolution time is available, the next update time should be stated cautiously and not invented.
```

After:

```text
Hi account team,

ticket #4821 opened on Friday is still under review for the duplicate notifications.

What is confirmed: the duplicate notification issue is still happening, and the delivery logs are being checked. What is not confirmed yet is the root cause or whether a campaign pause is required.

I know your account team needs an answer on whether to pause the campaign before noon. We will send the next status update as soon as the log review gives a clear recommendation.
```

## support-09-course-transfer-refund-long-regression

Scenario: Customer support
Tone: Direct
Input word count: 430
Input character count: 2513
Diagnosis tags: support_template_voice
Rewrite plan: Target support_template_voice while preserving the user's facts.
Draft AI-like signal: 78%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 16%
Final selected AI-like signal: 0%
Change: -78 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:best regards customer support team missing:daniel unsupported:let Dropped availability constraint.
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Expected facts:
- Daniel
- June weekend cohort
- Saturday, 6 June
- Saturday, 20 July
- seat availability
- seven days before the course begins
- registration timestamp
- course credit
- will not update your registration or cancel your current seat unless you clearly confirm

Before:

```text
Hi Daniel,

Thank you for contacting us and for explaining your situation in detail. We understand that your work schedule has changed unexpectedly, and that this has made it difficult for you to attend the online course session you originally registered for.

After reviewing the information you provided, it appears that your current enrollment is for the June weekend cohort, which is scheduled to begin on Saturday, 6 June. Since you notified us before the course start date, you may still be eligible to move your enrollment to a later cohort, depending on seat availability.

At this stage, the next available cohort is scheduled to begin on Saturday, 20 July. If that date works better for you, we can help move your registration to that session. Please note that your course access, learning materials, and live session links would be updated once the transfer is confirmed.

Regarding your question about a refund, our standard policy is that refund requests must be submitted at least seven days before the course begins. Since your message was sent before the course start date but close to the deadline, we would need to review the exact registration timestamp before confirming whether a full refund is available. If a full refund is not available, we may still be able to offer a course credit or transfer your enrollment to a future session.

In plain terms, you currently have two possible options:

1. You can transfer your enrollment to a later cohort, subject to availability.
2. You can request a refund review, and our team will confirm whether the timing meets the refund policy.

Before we make any changes, please reply and confirm whether you would prefer to move your enrollment to the July cohort or proceed with a refund review. We will not update your registration or cancel your current seat unless you clearly confirm which option you would like to choose.

Best regards,
Customer Support Team
```

After:

```text
Hi Daniel,

Your current enrollment is for the June weekend cohort, which starts on Saturday, 6 June. Because you contacted us before the course start date, you may still be eligible to transfer to a later cohort, depending on seat availability.

The next available cohort appears to start on Saturday, 20 July. If that works better, we can review moving your registration there once you confirm.

For a refund, the policy requires requests at least seven days before the course begins. We would need to check the exact registration timestamp before confirming whether a full refund is available. If a full refund is not available, course credit may still be an option.

Please reply with whether you prefer the July transfer or a refund review. We will not update your registration or cancel your current seat unless you clearly confirm which option you want.

Best regards,
Customer Support Team
```

## work-04-long-launch-readiness

Scenario: Work update
Tone: Direct
Input word count: 376
Input character count: 2178
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 99%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 0%
Final selected AI-like signal: 1%
Change: -98 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:last three failed events missing:help article links unsupported:here
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Expected facts:
- payment flow
- onboarding checklist
- help article links
- last three failed events
- 2pm launch check

Before:

```text
The current launch readiness status is as follows. The payment flow passed the latest smoke test, the onboarding checklist has been reviewed, and the help article links have been updated in the footer. The remaining item is the webhook retry log, where I am reviewing the last three failed events from this morning. I expect to post the result before the 2pm launch check. At this time, I do not recommend delaying the launch unless the retry log shows a repeated payment event failure.

The rewrite should keep the payment flow, onboarding checklist, help article links, webhook retry log, last three failed events, and 2pm launch check. It should not add new test results. It should make the update easier to scan, but it should not turn into a formal status report with too many headings. The final message should be clear enough that a manager can see what is ready and what is still being checked.

If the text uses bullets, keep them short. If it uses paragraphs, make each paragraph do a different job: ready items, pending review, and launch recommendation. Do not make the launch sound approved if the retry log still has to be checked.
```

After:

```text
Hi launch channel attendees,

The current launch readiness status is as follows.

The payment flow passed the latest smoke test, the onboarding checklist has been reviewed, and the help article links have been updated in the footer. The remaining item is the webhook retry log, where I am reviewing the last three failed events from this morning. I expect to post the result before the 2pm launch check. At this time, I do not recommend delaying the launch unless the retry log shows a repeated payment event failure.

The rewrite should keep the payment flow, onboarding checklist, help article links, webhook retry log, last three failed events, and 2pm launch check. It should not add new test results.
```

## blank-04-long-policy-note

Scenario: Blank / custom
Tone: Warm
Input word count: 236
Input character count: 1469
Diagnosis tags: corporate_polish
Rewrite plan: Target corporate_polish while preserving the user's facts.
Draft AI-like signal: 96%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 0%
Change: -96 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:agenda is unchanged missing:tuesday unsupported:feel missing_locked:scholarship forms missing_locked:supporting documents missing_locked:application timeline
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Expected facts:
- Saturday
- Room 204
- 6:30pm
- scholarship forms
- application timeline

Before:

```text
This message is to inform participants that the Saturday workshop location has changed due to maintenance in the library. The session will now take place in Room 204, and the start time remains 6:30pm. The agenda is unchanged and will still include scholarship forms, supporting documents, and the application timeline. Participants who already submitted questions do not need to send them again. We apologize for any inconvenience this change may cause and appreciate your understanding regarding the matter.

Additional details for the note: families received the original workshop reminder on Tuesday, so this update should focus only on the room change and not repeat the full registration instructions. The library maintenance is temporary and does not affect the rest of the program schedule. Participants may still bring printed scholarship drafts if they want feedback during the session. The tone should be warm and clear, but the note should not sound like a legal notice or a school district memo.

The note will be sent by text and email, so it needs to be easy to understand without extra context. It should keep Saturday, Room 204, 6:30pm, scholarship forms, supporting documents, application timeline, and the fact that already-submitted questions do not need to be resent. Do not add a new deadline, a new contact person, or a different reason for the room change. The best version should sound like a coordinator giving a practical update to families.
```

After:

```text
Saturday workshop update: we're moving to Room 204 because of library maintenance.

We'll still start at 6:30pm and cover scholarship forms, supporting documents, and the application timeline.

The agenda is unchanged.

Families received the original workshop reminder on Tuesday, so this update should focus only on the room change.

If you already submitted questions, you do not need to send them again. Printed scholarship drafts are still welcome.
```
