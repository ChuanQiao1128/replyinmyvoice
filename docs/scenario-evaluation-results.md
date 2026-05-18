# Scenario Evaluation Results

Date: 2026-05-18T02:06:27.243Z
Cases evaluated: 26
Measured cases: 26
Long cases (300+ words): 10
Long customer-support cases (300+ words): 5
Average AI-like signal drop: 60 pts
Rewrite below 50% AI-like signal: 20/26
Final selected rewrites worse than draft: 0/26
Cases using targeted repair: 25/26
Rejected candidate events: 77
Case pass count: 14/26

Pass requires: all expected facts preserved, scores available, final rewrite lower than the draft, and either below 50% or at least 30 points lower than the draft.

## blank-01-partner-update

Scenario: Blank / custom
Tone: Professional
Input word count: 70
Input character count: 449
Diagnosis tags: corporate_polish
Rewrite plan: Target corporate_polish while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: 100%
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 1%
Change: -99 pts
Rejected candidate reasons: initial: Rewrite AI-like signal is not lower than the draft. | repair: Rewrite AI-like signal is not lower than the draft. | initial: Rewrite dropped required details or became too short. | repair: Rewrite AI-like signal is not lower than the draft.
Facts preserved: no
Missing facts: section three; section five; May 12
Unsupported facts introduced: not detected
Quality failure state: yes
Pass: no

Expected facts:
- section three
- section five
- May 12

Before:

```text
I am writing to provide an update regarding the partner onboarding packet. The revised document has now been completed and is available for your review. Please note that section three contains the updated pricing language, and section five includes the implementation timeline that was discussed during the call on May 12. Kindly review the attached document and provide any feedback at your earliest convenience so that we may proceed accordingly.
```

After:

```text

```

## live-priya-billing-100-regression

Scenario: Customer support
Tone: Warm
Input word count: 284
Input character count: 1854
Diagnosis tags: support_template_voice, corporate_polish, too_balanced_structure
Rewrite plan: Preserve the billing facts first, then rewrite as a plain support reply with the finance-manager explanation and requested next step intact.
Draft AI-like signal: 100%
First candidate AI-like signal: 100%
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 0%
Change: -100 pts
Rejected candidate reasons: initial model candidate remained high and missed critical completeness; repair remained high; deterministic facts-first candidate passed after preserving `finance manager`.
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: not detected
Quality failure state: no
Pass: yes

Expected facts:
- Priya
- three contractor accounts
- first week of May / May
- 18 active seats
- 15 regular seats
- base plan
- NZD $126
- finance manager
- invoice finalized
- names or email addresses
- no changes unless requested

Before:

```text
Hi Priya,

Thanks for explaining the situation clearly.

Based on what you described, the increase is most likely related to the three contractor accounts that were added during the first week of May. Even if those users were only added temporarily, they may still count as active seats for the days they had access to the workspace. That would explain why the dashboard is showing 18 active seats instead of the 15 regular seats your team approved.

In plain English, this looks like a prorated seat charge rather than a change to your base plan. So the extra NZD $126 is likely coming from the three temporary users being active for part of the billing period, not from the plan price itself changing.

You could explain it to your finance manager like this:

"The May invoice preview appears to be higher because three temporary contractor users were active in the workspace during the month. The extra charge looks like a prorated seat charge for those users, rather than a change to the base plan."

For the next step, please check whether those three contractor accounts are still active in the workspace. If they are, they should be removed or deactivated before the invoice is finalized so they do not continue to affect billing going forward.

We will not make any changes yet, as requested. If you send over the names or email addresses of the three contractors, we can help confirm whether they are still active.
```

After:

```text
Hi Priya,

Thanks for laying this out. The jump looks tied to the three contractor accounts being counted during May, not a base plan change.

That explains why the dashboard shows 18 active seats instead of the 15 regular seats your team approved. The NZD $126 increase looks like the prorated charge for the days those accounts had access.

For your finance manager, you can say: "The May invoice preview is higher because the three contractor accounts were active during the month. The extra charge appears to be prorated seat usage, not a change to the base plan."

Before the invoice is finalized, check whether those contractor accounts are still active. They may still be active if they were not removed after the short project ended. If you send over their names or email addresses, we can help confirm their status. We will not change anything unless you ask us to.
```

## blank-02-community-note

Scenario: Blank / custom
Tone: Warm
Input word count: 57
Input character count: 371
Diagnosis tags: corporate_polish
Rewrite plan: Target corporate_polish while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: 1%
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: 1%
Change: -99 pts
Rejected candidate reasons: none
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: not detected
Quality failure state: no
Pass: yes

Expected facts:
- Thursday
- Room 204
- 6:30pm

Before:

```text
This message is to inform families that the Thursday workshop will be moved to Room 204 due to maintenance in the library. The start time remains 6:30pm, and the session will still cover scholarship forms, supporting documents, and the application timeline. We apologize for any inconvenience this change may cause and appreciate your understanding regarding the matter.
```

After:

```text
Hi families,

I wanted to let you know that the Thursday workshop is moving to Room 204 because of maintenance in the library. We'll still start at 6:30pm and cover scholarship forms, supporting documents, and the application timeline.

Thanks for your understanding!
```

## blank-03-internal-note

Scenario: Blank / custom
Tone: Concise
Input word count: 63
Input character count: 375
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: 100%
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 15%
Change: -85 pts
Rejected candidate reasons: initial: Rewrite AI-like signal is not lower than the draft. | repair: Rewrite AI-like signal is not lower than the draft.
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: not detected
Quality failure state: no
Pass: yes

Expected facts:
- Vendor A
- Vendor B
- Vendor C
- Friday

Before:

```text
The purpose of this note is to summarize the current status of the vendor review. Vendor A has provided the revised security questionnaire, Vendor B is still waiting on legal approval, and Vendor C has requested an extension until Friday. Based on the current timeline, I recommend that we do not make a final decision until all three responses are available for comparison.
```

After:

```text
Quick note: The purpose of this note is to summarize the current status of the vendor review.

Vendor A has provided the revised security questionnaire, Vendor B is still waiting on legal approval, and Vendor C has requested an extension until Friday. Based on the current timeline, I recommend that we do not make a final decision until all three responses are available for comparison.
```

## reply-01-teacher-extension

Scenario: Email or message reply
Tone: Warm
Input word count: 99
Input character count: 596
Diagnosis tags: stock_opening, corporate_polish, policy_memo_voice
Rewrite plan: Target stock_opening, corporate_polish, policy_memo_voice while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: 100%
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 0%
Change: -100 pts
Rejected candidate reasons: initial: Rewrite AI-like signal is not lower than the draft. | repair: Rewrite AI-like signal is not lower than the draft. | initial: Rewrite dropped required details or became too short.
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: not detected
Quality failure state: no
Pass: yes

Expected facts:
- family issue
- before class tomorrow
- course policy

Before:

```text
Dear Student, I acknowledge receipt of your email regarding the missed reflection deadline. Late submissions are generally subject to the course policy and may not be accepted. However, I understand that you have indicated a family issue. I will review the situation and respond accordingly. Please be advised that submitting before class tomorrow does not guarantee that it will be accepted.
```

After:

```text
Hi Professor,

I missed the reflection deadline because of a family issue this week. I know the policy says late work may not be accepted, but is there any way I can still submit it before class tomorrow?

Dear Student,

Thanks for your message. I understand that you missed the deadline due to a family issue. While late submissions usually follow the course policy, I will look into your situation. Please note that submitting it before class does not guarantee acceptance, but I appreciate your effort to reach out.

Best, 

Professor
```

## reply-02-sales-followup

Scenario: Email or message reply
Tone: Friendly
Input word count: 71
Input character count: 446
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: 100%
Repair candidate AI-like signal: 25%
Final selected AI-like signal: 0%
Change: -100 pts
Rejected candidate reasons: initial: Rewrite dropped required details or became too short. | repair: Repair dropped required details or became too short.
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: not detected
Quality failure state: no
Pass: yes

Expected facts:
- Jordan
- reporting feature
- next month

Before:

```text
Hello Jordan, I am following up on our previous communication regarding the proposal. Please advise whether you would like to proceed with the proposal as discussed. We are happy to provide any additional information that may assist your decision-making process as you evaluate your options.
```

After:

```text
Hi Jordan,

I can send a shorter summary for your internal thread. Glad the reporting feature is still useful.

No rush from my side; next month works while you compare the two other vendors.
```

## reply-03-parent-question

Scenario: Email or message reply
Tone: Professional
Input word count: 70
Input character count: 420
Diagnosis tags: stock_opening, corporate_polish, low_specificity
Rewrite plan: Target stock_opening, corporate_polish, low_specificity while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: 98%
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 98%
Change: -2 pts
Rejected candidate reasons: initial: Rewrite AI-like signal stayed high and did not drop enough. | repair: Rewrite AI-like signal is not lower than the draft. | initial: Rewrite AI-like signal stayed high and did not drop enough. | repair: Rewrite AI-like signal is not lower than the draft.
Facts preserved: no
Missing facts: Kai; two missing participation activities; one missing exit ticket
Unsupported facts introduced: not detected
Quality failure state: yes
Pass: no

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

```

## support-01-priya-billing

Scenario: Customer support
Tone: Friendly
Input word count: 169
Input character count: 965
Diagnosis tags: support_template_voice
Rewrite plan: Target support_template_voice while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: 0%
Repair candidate AI-like signal: 2%
Final selected AI-like signal: 18%
Change: -82 pts
Rejected candidate reasons: initial: Rewrite dropped required details or became too short. | repair: Repair dropped required details or became too short.
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: not detected
Quality failure state: no
Pass: yes

Expected facts:
- Priya
- 18 active seats
- 15 regular seats
- NZD $126
- May 8

Before:

```text
Hi Priya, Thank you for contacting us regarding the usage report and invoice preview in your account. We understand that there appears to be a discrepancy between the number of active seats shown in the dashboard and the number of seats approved during your renewal. The most likely explanation is that the three temporary contractors were counted as active seats during May. Even if a user only joins for part of the month, prorated charges may apply. Please check whether the contractors are still active and send us their names if you would like assistance.
```

After:

```text
Hi Priya,

Thanks for laying this out. Based on what you described, the higher invoice preview is most likely from the three temporary contractors being counted as active seats during May, rather than a base plan change. Your team approved 15 regular seats.

Even if someone only has access for part of the month, that access can still create a prorated seat charge for the days they were active. That would explain why the dashboard shows 18 active seats instead of the 15 regular seats your team approved. It also lines up with the NZD $126 increase you are seeing.

For the next step, check whether those contractor accounts are still active. They may still be active if they were not removed after May 8. If you want us to help confirm, send over their names or email addresses and we can check their status. We will not change anything unless you ask us to.
```

## support-02-export-error

Scenario: Customer support
Tone: Professional
Input word count: 86
Input character count: 505
Diagnosis tags: stock_opening, corporate_polish, support_template_voice
Rewrite plan: Target stock_opening, corporate_polish, support_template_voice while preserving the user's facts.
Draft AI-like signal: 32%
First candidate AI-like signal: 98%
Repair candidate AI-like signal: 74%
Final selected AI-like signal: 0%
Change: -32 pts
Rejected candidate reasons: initial: Rewrite dropped required details or became too short. | repair: Rewrite AI-like signal is not lower than the draft. | initial: Rewrite dropped required details or became too short.
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: not detected
Quality failure state: no
Pass: yes

Expected facts:
- custom tags column
- Monday board packet
- April

Before:

```text
Thank you for reaching out. We apologize for any inconvenience caused by the missing custom tags column in your CSV export. Our team is currently investigating the matter and will provide an update as soon as possible. In the meantime, please be advised that you may try exporting the report again from the dashboard settings page.
```

After:

```text
The CSV export from your dashboard is missing the custom tags column, which is important for your Monday board packet. We understand that this makes it difficult for your team to reconcile April data. Our team is looking into this issue to find out why the column is not included in the export. 

While we investigate, you can try exporting the report again from the dashboard settings page. If you need the corrected file before Monday, please provide the export settings you used, and we will assist you further.
```

## support-03-login-access

Scenario: Customer support
Tone: Concise
Input word count: 75
Input character count: 484
Diagnosis tags: over_safe_tone
Rewrite plan: Target over_safe_tone while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: 28%
Repair candidate AI-like signal: 98%
Final selected AI-like signal: 28%
Change: -72 pts
Rejected candidate reasons: initial: Rewrite dropped required details or became too short. | repair: Repair dropped required details or became too short. | initial: Rewrite dropped required details or became too short. | repair: Repair dropped required details or became too short.
Facts preserved: no
Missing facts: Mina; mina@northstar.example; resent the invite twice
Unsupported facts introduced: not detected
Quality failure state: yes
Pass: no

Expected facts:
- Mina
- mina@northstar.example
- resent the invite twice

Before:

```text
Hello, thank you for contacting support. We understand the issue regarding Mina's account access and apologize for the inconvenience. It may be related to the user's previous workspace association. Please ensure that the invitation has been accepted correctly and that the user is logging in with the appropriate email address.
```

After:

```text

```

## cover-01-operations-role

Scenario: Cover letter
Tone: Professional
Input word count: 89
Input character count: 601
Diagnosis tags: low_specificity, application_cliche
Rewrite plan: Target low_specificity, application_cliche while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: 100%
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 100%
Change: 0 pts
Rejected candidate reasons: initial: Rewrite AI-like signal is not lower than the draft. | repair: Rewrite AI-like signal is not lower than the draft. | initial: Rewrite AI-like signal is not lower than the draft. | repair: Rewrite AI-like signal is not lower than the draft.
Facts preserved: no
Missing facts: weekly partner updates; shared folders; team of eight
Unsupported facts introduced: not detected
Quality failure state: yes
Pass: no

Expected facts:
- weekly partner updates
- shared folders
- team of eight

Before:

```text
I am writing to express my interest in the Operations Coordinator position. I am a passionate and results-driven professional with a proven track record of managing communication, coordinating schedules, and supporting team success. In my last role, I prepared weekly partner updates, kept shared folders organized, and helped schedule meetings for a team of eight. I believe I would be a perfect fit for your dynamic team.
```

After:

```text

```

## cover-02-customer-success

Scenario: Cover letter
Tone: Warm
Input word count: 89
Input character count: 565
Diagnosis tags: low_specificity, application_cliche
Rewrite plan: Target low_specificity, application_cliche while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: 100%
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 2%
Change: -98 pts
Rejected candidate reasons: initial: Rewrite AI-like signal is not lower than the draft. | repair: Rewrite AI-like signal is not lower than the draft.
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: not detected
Quality failure state: no
Pass: yes

Expected facts:
- summarize action items
- help center articles
- Customer Success Associate

Before:

```text
I am excited to apply for the Customer Success Associate role. I have always been passionate about helping customers and delivering excellent service. In my current position, I respond to customer questions, summarize action items after calls, and update our help center articles when a pattern appears. I am confident that my communication skills and positive attitude would make me a strong addition to the team.
```

After:

```text
I'm interested in this role because the day-to-day work matches things I've already done.

I am excited to apply for the Customer Success Associate role. I have always been passionate about helping customers and delivering excellent service. In my current position, I respond to customer questions, summarize action items after calls, and update our help center articles when a pattern appears.

I'd be glad to talk through how I could help.
```

## cover-03-admin-assistant

Scenario: Cover letter
Tone: Concise
Input word count: 79
Input character count: 592
Diagnosis tags: low_specificity
Rewrite plan: Target low_specificity while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: 100%
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 100%
Change: 0 pts
Rejected candidate reasons: initial: Rewrite AI-like signal is not lower than the draft. | repair: Rewrite AI-like signal is not lower than the draft. | initial: Rewrite AI-like signal is not lower than the draft. | repair: Rewrite AI-like signal is not lower than the draft.
Facts preserved: no
Missing facts: three providers; patient follow-up notes; private information
Unsupported facts introduced: not detected
Quality failure state: yes
Pass: no

Expected facts:
- three providers
- patient follow-up notes
- private information

Before:

```text
Please accept my application for the Administrative Assistant position. I am highly organized and detail oriented, and I have extensive experience supporting administrative workflows. At my previous clinic, I managed calendars for three providers, prepared patient follow-up notes, and handled private information carefully according to office procedures. I would welcome the opportunity to bring my skills to your organization.
```

After:

```text

```

## work-01-design-delay

Scenario: Work update
Tone: Professional
Input word count: 70
Input character count: 406
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: 100%
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 0%
Change: -100 pts
Rejected candidate reasons: initial: Rewrite AI-like signal is not lower than the draft. | repair: Rewrite AI-like signal is not lower than the draft.
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: not detected
Quality failure state: no
Pass: yes

Expected facts:
- source file arrived late
- one quality check
- 4pm Friday

Before:

```text
Unfortunately, the revised screenshots are not available at this time due to a delay in receiving the updated design file. The source file arrived late this morning and still requires one quality check before it can be shared externally. I expect to send the screenshots by 4pm Friday if there are no further issues.
```

After:

```text
Quick update: Unfortunately, the revised screenshots are not available at this time due to a delay in receiving the updated design file.

The source file arrived late this morning and still requires one quality check before it can be shared externally.

I expect to send the screenshots by 4pm Friday if there are no further issues.
```

## work-02-launch-risk

Scenario: Work update
Tone: Concise
Input word count: 69
Input character count: 384
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: 100%
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 4%
Change: -96 pts
Rejected candidate reasons: initial: Rewrite AI-like signal is not lower than the draft. | repair: Rewrite AI-like signal is not lower than the draft.
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: not detected
Quality failure state: no
Pass: yes

Expected facts:
- payment flow
- last three failed events
- 2pm launch check

Before:

```text
The current launch status is that the payment flow has passed the latest smoke test, but the webhook retry log still needs review. I am checking the last three failed events and will post the result before the 2pm launch check. At this time, I do not recommend changing the launch decision until that review is complete.
```

After:

```text
Quick update: The current launch status is that the payment flow has passed the latest smoke test, but the webhook retry log still needs review.

I am checking the last three failed events and will post the result before the 2pm launch check.

At this time, I do not recommend changing the launch decision until that review is complete.
```

## work-03-research-summary

Scenario: Work update
Tone: Friendly
Input word count: 73
Input character count: 436
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: 100%
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 100%
Change: 0 pts
Rejected candidate reasons: initial: Rewrite AI-like signal is not lower than the draft. | repair: Rewrite AI-like signal is not lower than the draft. | initial: Rewrite AI-like signal is not lower than the draft. | repair: Rewrite AI-like signal is not lower than the draft.
Facts preserved: no
Missing facts: six interviews; four teachers; two asked; Wednesday
Unsupported facts introduced: not detected
Quality failure state: yes
Pass: no

Expected facts:
- six interviews
- four teachers
- two asked
- Wednesday

Before:

```text
I am writing to inform you that the teacher interview notes are now ready for review. We completed six interviews this week. Four teachers mentioned that the onboarding copy felt too technical, and two asked for a sample response before signing up. I recommend updating the first screen and adding one short example before the next test on Wednesday.
```

After:

```text

```

## support-04-priya-billing-long-regression

Scenario: Customer support
Tone: Friendly
Input word count: 541
Input character count: 3056
Diagnosis tags: stock_opening, over_explained, over_safe_tone
Rewrite plan: Target stock_opening, over_explained, over_safe_tone while preserving the user's facts.
Draft AI-like signal: 89%
First candidate AI-like signal: 100%
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 0%
Change: -89 pts
Rejected candidate reasons: initial: Rewrite dropped required details or became too short. | repair: Repair dropped required details or became too short.
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: not detected
Quality failure state: no
Pass: yes

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

Thanks for laying this out. Based on what you described, the higher invoice preview is most likely from the three temporary contractors being counted as active seats during May, rather than a base plan change. Your team approved 15 regular seats.

Even if someone only has access for part of the month, that access can still create a prorated seat charge for the days they were active. That would explain why the dashboard shows 18 active seats instead of the 15 regular seats your team approved. It also lines up with the NZD $126 increase you are seeing.

A simple note for the finance manager would be: "The invoice preview increased because the three temporary contractors were active during the month. The extra charge appears to be prorated seat usage, not a change to the base plan."

For the next step, check whether those contractor accounts are still active. They may still be active if they were not removed after May 8. If you want us to help confirm, send over their names or email addresses and we can check their status. We will not change anything unless you ask us to.
```

## support-05-data-export-long

Scenario: Customer support
Tone: Professional
Input word count: 383
Input character count: 2253
Diagnosis tags: over_safe_tone, support_template_voice
Rewrite plan: Target over_safe_tone, support_template_voice while preserving the user's facts.
Draft AI-like signal: 0%
First candidate AI-like signal: 100%
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 0%
Change: 0 pts
Rejected candidate reasons: initial: Rewrite dropped required details or became too short. | repair: Repair dropped required details or became too short. | initial: Rewrite dropped required details or became too short. | repair: Repair dropped required details or became too short.
Facts preserved: no
Missing facts: custom tags column; April; May; Monday; 10am; Northeast region
Unsupported facts introduced: not detected
Quality failure state: yes
Pass: no

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

```

## support-06-login-workspace-long

Scenario: Customer support
Tone: Concise
Input word count: 337
Input character count: 2074
Diagnosis tags: over_safe_tone
Rewrite plan: Target over_safe_tone while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: 100%
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 2%
Change: -98 pts
Rejected candidate reasons: initial: Rewrite dropped required details or became too short. | repair: Repair dropped required details or became too short. | initial: Rewrite dropped required details or became too short.
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: not detected
Quality failure state: no
Pass: yes

Expected facts:
- Mina
- mina@northstar.example
- resent the invite twice
- old pilot workspace
- billing report folder

Before:

```text
Hello, thank you for contacting support. We understand the issue regarding Mina's account access and apologize for the inconvenience caused by the workspace mismatch. Based on the information provided, this may be related to the user's previous workspace association or an incomplete invitation acceptance flow. Please ensure that Mina is logging in with the correct email address, mina@northstar.example, and that she has accepted the most recent invitation. If the problem persists, our team can review the account association and provide additional guidance regarding the next steps.

The reply needs to acknowledge the steps already taken without repeating them as if they are new instructions. It should make clear that Mina should keep using mina@northstar.example and that the previous workspace association may be the reason she is landing in the old pilot workspace. It should also preserve the fact that the invite was resent twice and that the missing destination is the billing report folder. Do not promise that support will unlink the account immediately unless the user asks for that change.
```

After:

```text
Mina accepted the invitation for the Northstar workspace yesterday but is still landing in the old pilot workspace when she logs in with mina@northstar.example. Since you've already resent the invite twice and suggested she use a private browser window, it seems likely that her email is still linked to the previous workspace. This could be why she can't access the billing report folder. 

To address this, Mina should continue to log in with the same email. If the issue continues, we can check the workspace link from our side to see if there's an account association problem. Let us know if you want us to look into it further.
```

## support-07-plan-change-long

Scenario: Customer support
Tone: Warm
Input word count: 364
Input character count: 2173
Diagnosis tags: stock_opening, over_safe_tone
Rewrite plan: Target stock_opening, over_safe_tone while preserving the user's facts.
Draft AI-like signal: 0%
First candidate AI-like signal: 100%
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 0%
Change: 0 pts
Rejected candidate reasons: initial: Rewrite AI-like signal is not lower than the draft. | repair: Rewrite AI-like signal is not lower than the draft. | initial: Rewrite dropped required details or became too short. | repair: Repair dropped required details or became too short.
Facts preserved: no
Missing facts: Starter plan; Team plan; May 3; shared templates; old plan credit; new plan charge
Unsupported facts introduced: not detected
Quality failure state: yes
Pass: no

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

```

## support-08-delayed-response-long

Scenario: Customer support
Tone: Professional
Input word count: 308
Input character count: 1918
Diagnosis tags: corporate_polish
Rewrite plan: Target corporate_polish while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: 100%
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 1%
Change: -99 pts
Rejected candidate reasons: initial: Rewrite AI-like signal is not lower than the draft. | repair: Rewrite AI-like signal is not lower than the draft. | initial: Rewrite dropped required details or became too short.
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: not detected
Quality failure state: no
Pass: yes

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
We opened ticket #4821 on Friday about the duplicate notifications sent to client contacts. The issue is still happening this morning. Our engineering team is currently reviewing the delivery logs and notification events to understand what's causing the duplicates. 

As of now, we can't confirm when a full resolution will be in place. We understand that your account team needs to decide whether to pause the campaign before noon. We will update you as soon as we have more information, but at this moment, the duplicate notifications are still being investigated.
```

## cover-04-long-program-manager

Scenario: Cover letter
Tone: Professional
Input word count: 399
Input character count: 2683
Diagnosis tags: over_safe_tone, application_cliche
Rewrite plan: Target over_safe_tone, application_cliche while preserving the user's facts.
Draft AI-like signal: 98%
First candidate AI-like signal: 100%
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 100%
Change: 2 pts
Rejected candidate reasons: initial: Rewrite dropped required details or became too short. | repair: Repair dropped required details or became too short. | initial: Rewrite AI-like signal is not lower than the draft. | repair: Repair dropped required details or became too short.
Facts preserved: no
Missing facts: 32 people; monthly partner updates; weekend workshops; grant reports; education access
Unsupported facts introduced: not detected
Quality failure state: yes
Pass: no

Expected facts:
- 32 people
- monthly partner updates
- weekend workshops
- grant reports
- education access

Before:

```text
I am writing to express my strong interest in the Program Manager position. I am a highly motivated and results-oriented professional with extensive experience supporting community programs and coordinating stakeholders. In my current role, I coordinate a volunteer roster of 32 people, prepare monthly partner updates, manage the schedule for weekend workshops, and track attendance and completion numbers for grant reports. I am passionate about education access and believe my background makes me an excellent fit for your organization. I would welcome the opportunity to contribute my skills to your team and support the continued success of your programs.

The current draft sounds polished but generic. It should be rewritten to put the concrete experience first: 32 volunteers, monthly partner updates, weekend workshop schedule, attendance and completion numbers, and grant reports. The application can mention education access because it is in the original draft, but it should not overstate passion or claim a perfect fit. The final version should feel like a person explaining why this specific role matches work they have actually done.

It should also keep the application focused on the employer's needs. The reader should quickly understand that the applicant can keep details organized across people, dates, documents, and reporting deadlines. Avoid opening with a grand statement. A more believable version might begin with the applicant's actual coordination work and then connect that work to the nonprofit's program manager role.
```

After:

```text

```

## cover-05-long-support-specialist

Scenario: Cover letter
Tone: Friendly
Input word count: 367
Input character count: 2388
Diagnosis tags: low_specificity, application_cliche
Rewrite plan: Target low_specificity, application_cliche while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: 100%
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 1%
Change: -99 pts
Rejected candidate reasons: initial: Rewrite AI-like signal is not lower than the draft. | repair: Repair dropped required details or became too short.
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: not detected
Quality failure state: no
Pass: yes

Expected facts:
- email and chat
- recurring issues
- product team
- help center articles

Before:

```text
I am excited to submit my application for the Support Specialist role. I have always been passionate about helping customers succeed and providing excellent service. In my previous position, I answered customer questions through email and chat, summarized recurring issues for our product team, and updated help center articles when we noticed the same question coming up repeatedly. I enjoy making complicated product details easier for customers to understand. I believe I would be a valuable addition to your team and would appreciate the opportunity to discuss my qualifications further.

The rewrite should remove broad phrases like passionate about helping customers and valuable addition to your team. It should keep the actual work: email and chat support, recurring issues, product team summaries, and help center article updates. It should also keep the idea that the applicant likes making complicated product details easier to understand. Do not add metrics, customer satisfaction scores, management experience, or technical certifications.

The final letter can be warm, but it should still feel like a real applicant wrote it. It should not over-explain every support task. The strongest version will probably have two or three compact paragraphs: why the role fits, what the applicant has done, and why that experience would help a small SaaS support team.
```

After:

```text
I'm interested in this role because the day-to-day work matches things I've already done.

I am excited to submit my application for the Support Specialist role. I have always been passionate about helping customers succeed and providing excellent service. In my previous position, I answered customer questions through email and chat, summarized recurring issues for our product team, and updated help center articles when we noticed the same question coming up repeatedly.

I'd be glad to talk through how I could help.
```

## work-04-long-launch-readiness

Scenario: Work update
Tone: Concise
Input word count: 376
Input character count: 2178
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 99%
First candidate AI-like signal: 100%
Repair candidate AI-like signal: 0%
Final selected AI-like signal: 0%
Change: -99 pts
Rejected candidate reasons: initial: Rewrite AI-like signal is not lower than the draft.
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: not detected
Quality failure state: no
Pass: yes

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
Here's the update on launch readiness:

The payment flow has passed the latest smoke test. The onboarding checklist is complete, and the help article links in the footer are updated.

The webhook retry log is still being reviewed. I'm currently looking into the last three failed events from this morning. I plan to have that information ready before the 2pm launch check.

Right now, I don't see a reason to delay the launch unless the retry log shows repeated payment event failures.
```

## work-05-long-design-delay

Scenario: Work update
Tone: Professional
Input word count: 348
Input character count: 2055
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: 79%
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 3%
Change: -97 pts
Rejected candidate reasons: initial: Rewrite dropped required details or became too short. | repair: Rewrite AI-like signal is not lower than the draft. | initial: Rewrite dropped required details or became too short. | repair: Rewrite AI-like signal is not lower than the draft.
Facts preserved: no
Missing facts: source file arrived later; one quality check; pricing table; section three; 4pm Friday
Unsupported facts introduced: not detected
Quality failure state: yes
Pass: no

Expected facts:
- source file arrived later
- one quality check
- pricing table
- section three
- 4pm Friday

Before:

```text
Unfortunately, the revised screenshots are not yet available because the updated design source file arrived later than expected this morning. The screen captures still need one quality check before they can be shared externally, especially because the pricing table changed in section three and the partner logo appears on the final slide. I expect to send the screenshots by 4pm Friday if there are no further issues. I will let you know sooner if the quality check reveals anything that changes that timing.

The rewrite should keep the updated design source file arriving late, one quality check, pricing table, section three, partner logo, final slide, and 4pm Friday. It should not promise delivery earlier than 4pm. It should also preserve the note that timing could change if the quality check reveals an issue. The message should sound like a practical work update rather than a formal excuse.

A good version can be direct: what happened, what remains, and when the recipient will get the screenshots. Avoid phrases like unfortunately, at this time, and if there are no further issues if they make the message sound like a customer service macro.
```

After:

```text

```

## blank-04-long-policy-note

Scenario: Blank / custom
Tone: Warm
Input word count: 236
Input character count: 1469
Diagnosis tags: corporate_polish
Rewrite plan: Target corporate_polish while preserving the user's facts.
Draft AI-like signal: 96%
First candidate AI-like signal: 98%
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 98%
Change: 2 pts
Rejected candidate reasons: initial: Rewrite AI-like signal is not lower than the draft. | repair: Rewrite AI-like signal is not lower than the draft. | initial: Rewrite dropped required details or became too short. | repair: Rewrite AI-like signal is not lower than the draft.
Facts preserved: no
Missing facts: Saturday; Room 204; 6:30pm; scholarship forms; application timeline
Unsupported facts introduced: not detected
Quality failure state: yes
Pass: no

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

```

## reply-04-long-sales-renewal

Scenario: Email or message reply
Tone: Friendly
Input word count: 408
Input character count: 2418
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 0%
First candidate AI-like signal: 1%
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 0%
Change: 0 pts
Rejected candidate reasons: initial: Rewrite AI-like signal is not lower than the draft. | repair: Rewrite AI-like signal is not lower than the draft. | initial: Rewrite dropped required details or became too short. | repair: Repair dropped required details or became too short.
Facts preserved: no
Missing facts: Jordan; reporting feature; team templates; two other vendors; first week of June
Unsupported facts introduced: not detected
Quality failure state: yes
Pass: no

Expected facts:
- Jordan
- reporting feature
- team templates
- two other vendors
- first week of June

Before:

```text
Hello Jordan, I am following up regarding the renewal proposal and your ongoing evaluation process. We appreciate your interest in the reporting feature and the new team templates. We understand that your finance team is comparing multiple vendors before making a final decision. Please let us know whether you would like to proceed with one of the plan options, and we would be happy to provide any additional information that may assist your decision-making process during the first week of June.

The rewrite should sound like a helpful sales follow-up, not a pressure email. It should acknowledge that they like the reporting feature and team templates, offer to send a shorter plan-options summary, and leave room for the finance comparison. Avoid phrases like decision-making process, proceed with the proposal, and happy to provide any additional information if they make the note feel generic. The best version should sound like one person responding to Jordan's actual message.

Preserve Jordan's name if it appears in the draft, but do not invent a last name or company. Do not claim that the plan options are attached unless the draft says so. A good reply should be short enough to send in a thread but specific enough that it clearly answers the buyer's request.
```

After:

```text

```
