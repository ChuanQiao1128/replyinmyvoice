# Scenario Evaluation Results

Date: 2026-05-18T00:08:14.301Z
Cases evaluated: 15
Measured cases: 15
Average AI-like signal drop: 64 pts
Rewrite below 50% AI-like signal: 11/15
Case pass count: 11/15

Pass requires: all expected facts preserved, scores available, rewrite below 50%, and at least 30 points lower than the draft.

## blank-01-partner-update

Scenario: Blank / custom
Tone: Professional
Diagnosis tags: corporate_polish
Rewrite plan: Target corporate_polish while preserving the user's facts.
Draft AI-like signal: 100%
Rewrite AI-like signal: 96%
Change: -4 pts
Facts preserved: yes
Missing facts: none
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
Quick update: I am writing to provide an update regarding the partner onboarding packet.

The revised document has now been completed and is available for your review. Please note that section three contains the updated pricing language, and section five includes the implementation timeline that was discussed during the call on May 12.
```

## blank-02-community-note

Scenario: Blank / custom
Tone: Warm
Diagnosis tags: corporate_polish
Rewrite plan: Target corporate_polish while preserving the user's facts.
Draft AI-like signal: 100%
Rewrite AI-like signal: 1%
Change: -99 pts
Facts preserved: yes
Missing facts: none
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
Hi everyone,

I wanted to let you know that the Thursday workshop is moving to Room 204 because of maintenance in the library. We'll still start at 6:30pm and cover scholarship forms, supporting documents, and the application timeline.

Thanks for your understanding!

Key details to keep: may.
```

## blank-03-internal-note

Scenario: Blank / custom
Tone: Concise
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 100%
Rewrite AI-like signal: 15%
Change: -85 pts
Facts preserved: yes
Missing facts: none
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
Diagnosis tags: stock_opening, corporate_polish, policy_memo_voice
Rewrite plan: Target stock_opening, corporate_polish, policy_memo_voice while preserving the user's facts.
Draft AI-like signal: 100%
Rewrite AI-like signal: 0%
Change: -100 pts
Facts preserved: yes
Missing facts: none
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
Hi Student,

I see you missed the reflection deadline due to a family issue. While late submissions usually follow the course policy, I'll consider your situation and let you know soon. Just keep in mind that submitting before class tomorrow doesn't guarantee acceptance.

Thanks for reaching out.

Key details to keep: may.
```

## reply-02-sales-followup

Scenario: Email or message reply
Tone: Friendly
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 100%
Rewrite AI-like signal: 4%
Change: -96 pts
Facts preserved: yes
Missing facts: none
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

Hello Jordan, I am following up on our previous communication regarding the proposal.

Please advise whether you would like to proceed with the proposal as discussed. We are happy to provide any additional information that may assist your decision-making process as you evaluate your options.

Key details to keep: We like the reporting feature; decide; next month.
```

## reply-03-parent-question

Scenario: Email or message reply
Tone: Professional
Diagnosis tags: stock_opening, corporate_polish, low_specificity
Rewrite plan: Target stock_opening, corporate_polish, low_specificity while preserving the user's facts.
Draft AI-like signal: 100%
Rewrite AI-like signal: 49%
Change: -51 pts
Facts preserved: yes
Missing facts: none
Pass: yes

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
Hi, I appreciate you bringing this up about Kai's grade. 

The drop is due to two missing participation activities and one missing exit ticket. If you need more details or want to discuss this further, just let me know.

Key details to keep: everything except one exit ticket.
```

## support-01-priya-billing

Scenario: Customer support
Tone: Friendly
Diagnosis tags: support_template_voice
Rewrite plan: Target support_template_voice while preserving the user's facts.
Draft AI-like signal: 100%
Rewrite AI-like signal: 1%
Change: -99 pts
Facts preserved: yes
Missing facts: none
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

Thanks for reaching out about the usage report and the invoice preview. It looks like the discrepancy comes from the three temporary contractors you had during the first week of May. They may still be counted as active seats, which could explain the extra NZD $126.

Please check if those contractors are still active, and if you need help, just send us their names. We want to make sure everything is accurate before the invoice is finalized.

Key details to keep: 18 active seats; 15 regular seats; after the client handover; May 8.
```

## support-02-export-error

Scenario: Customer support
Tone: Professional
Diagnosis tags: stock_opening, corporate_polish, support_template_voice
Rewrite plan: Target stock_opening, corporate_polish, support_template_voice while preserving the user's facts.
Draft AI-like signal: 32%
Rewrite AI-like signal: 0%
Change: -32 pts
Facts preserved: yes
Missing facts: none
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
I see that the custom tags column is missing from your CSV export, which is causing issues for your Monday board packet. 

We're looking into this and will get back to you with an update soon. In the meantime, you might try exporting the report again from the dashboard settings page.

Key details to keep: missing the custom tags column; for our Monday board packet; reconcile April without that column; the missing custom tags column; may.
```

## support-03-login-access

Scenario: Customer support
Tone: Concise
Diagnosis tags: over_safe_tone
Rewrite plan: Target over_safe_tone while preserving the user's facts.
Draft AI-like signal: 100%
Rewrite AI-like signal: 1%
Change: -99 pts
Facts preserved: yes
Missing facts: none
Pass: yes

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
Hi, I see that Mina was added to our workspace yesterday but still sees the old team. Since we've resent the invite twice, it could be due to her previous workspace association.

Please double-check that Mina has accepted the invitation correctly and that she's logging in with the right email address, mina@northstar.example. Let me know if this helps or if there are any other issues.

Key details to keep: after logging in with; may.
```

## cover-01-operations-role

Scenario: Cover letter
Tone: Professional
Diagnosis tags: low_specificity, application_cliche
Rewrite plan: Target low_specificity, application_cliche while preserving the user's facts.
Draft AI-like signal: 100%
Rewrite AI-like signal: 100%
Change: 0 pts
Facts preserved: yes
Missing facts: none
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
I'm excited to apply for the Operations Coordinator position at your nonprofit education program. In my previous role, I managed partner communication, prepared weekly updates, and organized shared documents for a team of eight. I also coordinated schedules effectively, which helped our team stay on track and succeed. 

I believe my experience aligns well with the requirements of this role, and I'm eager to contribute to your mission.

Key details to keep: I prepared weekly partner updates; kept shared folders.
```

## cover-02-customer-success

Scenario: Cover letter
Tone: Warm
Diagnosis tags: low_specificity, application_cliche
Rewrite plan: Target low_specificity, application_cliche while preserving the user's facts.
Draft AI-like signal: 100%
Rewrite AI-like signal: 2%
Change: -98 pts
Facts preserved: yes
Missing facts: none
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
Diagnosis tags: low_specificity
Rewrite plan: Target low_specificity while preserving the user's facts.
Draft AI-like signal: 100%
Rewrite AI-like signal: 100%
Change: 0 pts
Facts preserved: yes
Missing facts: none
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
Please accept my application for the Administrative Assistant position. I have strong organizational skills and attention to detail, with extensive experience in supporting administrative workflows.

At my previous clinic, I managed calendars for three providers, prepared patient follow-up notes, and carefully handled private information according to office procedures. I’d love the chance to bring my skills to your organization.
```

## work-01-design-delay

Scenario: Work update
Tone: Professional
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 100%
Rewrite AI-like signal: 0%
Change: -100 pts
Facts preserved: yes
Missing facts: none
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
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 100%
Rewrite AI-like signal: 4%
Change: -96 pts
Facts preserved: yes
Missing facts: none
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
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 100%
Rewrite AI-like signal: 94%
Change: -6 pts
Facts preserved: no
Missing facts: two asked
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
Hi, the teacher interview notes are ready for you to check out. We completed six interviews this week, and four teachers felt the onboarding copy was too technical. Two also requested a sample response before signing up.

I suggest we update the first screen and add a short example before the next test on Wednesday. Let me know your thoughts!

Key details to keep: asked for a sample response.
```
