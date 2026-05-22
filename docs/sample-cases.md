# Sample Cases

Last updated: 2026-05-22

These selected homepage samples are documented so the landing page can show stable Naturalness Check values without calling Sapling on every render.

The displayed homepage values below are historical internal sample values aligned with the prior optimization work in `docs/optimization-notes.md`. Future refreshes should update this file with a complete measured run before changing homepage sample values.

## Latest M4-001 Refresh Attempt

- Date: 2026-05-22
- Runner: Codex local production rewrite pipeline
- Route: `rewriteWithFactReconstruct`
- Sample count: 4
- Sample categories: teacher message, sales follow-up, workplace email, client reply
- Input size: 177 to 204 words per case across message, draft, and context fields
- Result: blocked before model generation because the draft Naturalness Check signal returned `timeout_or_network` for every case.
- DeepSeek/OpenAI calls: 0
- Sapling attempts: 8
- Estimated Sapling characters: 3,460
- Estimated provider cost: USD $0.0173, approximately NZ$0.03 at 1.65 NZD/USD
- Homepage values changed: no
- Follow-up: rerun this refresh when Sapling is reachable from the execution environment. Do not use the attempted cases as measured homepage examples until draft and rewrite signals are available.

### Attempted Replacement Cases

These cases were prepared for the M4-001 refresh with no personal names in the sample text. The preserved-facts checklists are expectations only because no rewrite was generated.

#### Teacher Message Refresh Case

- ID: `teacher-late-reflection`
- Input word count: 177
- Rough draft word count: 71
- Signal result: unavailable
- Preserved facts checklist: family issue, missed reflection deadline, late-work policy, review after class tomorrow, no guaranteed acceptance.

Incoming message:

> Hi Professor, I missed the reflection deadline because of a family issue this week. I know the course policy says late work may not be accepted. If I finish it tonight, can I still submit it before class tomorrow, or should I wait until we talk?

Rough draft:

> Thank you for your message regarding the missed reflection deadline. I understand that you experienced a family issue, and I appreciate you letting me know. Late submissions are handled according to the course policy and cannot be automatically approved. I will review the circumstances and let you know the next appropriate step after class tomorrow. Please understand that submitting the work before then does not guarantee that it will be accepted.

#### Sales Follow-Up Refresh Case

- ID: `sales-vendor-comparison`
- Input word count: 183
- Rough draft word count: 65
- Signal result: unavailable
- Preserved facts checklist: proposal sent Tuesday, reporting workflow, two other vendors, shorter summary for finance, next week's review, no pressure or discount promise.

Incoming message:

> Thanks for sending the proposal on Tuesday. We like the reporting workflow, but the team is still comparing two other vendors. Finance asked for a shorter summary before next week's review. We probably will not make a decision until after that meeting.

Rough draft:

> Hello, I am following up regarding the proposal that was sent on Tuesday. I understand your team is still evaluating other vendors and that finance requires a shorter summary before the upcoming review. Please advise whether you would like us to proceed with the package as discussed, or whether additional information would be useful. We would be pleased to answer any questions at your convenience.

#### Workplace Email Refresh Case

- ID: `workplace-partner-numbers`
- Input word count: 188
- Rough draft word count: 73
- Signal result: unavailable
- Preserved facts checklist: revised numbers, partner update, draft deck review tomorrow morning, source file arrived late, one quality check, 4pm Friday target.

Incoming message:

> Can you send the revised numbers today? I need to include them in the partner update, and the draft deck is going out for review tomorrow morning. If anything is still uncertain, please tell me what changed and when the final version will be ready.

Rough draft:

> Unfortunately, the requested revised numbers are not available at this time because the updated source file arrived later than expected. I understand that the partner update is important and that the deck is scheduled for review tomorrow morning. I will review the underlying source file and provide the final figures as soon as the information has been checked. The current target is 4pm Friday, assuming the quality check does not identify another issue.

#### Client Reply Refresh Case

- ID: `client-report-totals`
- Input word count: 204
- Rough draft word count: 68
- Signal result: unavailable
- Preserved facts checklist: report totals changed, referral section, partner-referral category, hidden last month and included this month, export formula has not changed, line-by-line note today, no full reissue promise.

Incoming message:

> Hi, the totals in this month's report look different from last month, especially in the referral section. Can you explain what changed before I send this to our director? I do not want to forward the wrong numbers if this is a reporting issue.

Rough draft:

> Thank you for flagging the discrepancy in the report totals. We apologize for any confusion caused by the change from last month. Our team is currently reviewing the relevant information to determine the reason for the difference. Based on the initial check, this month's report appears to include a partner-referral category that was hidden last month. I will provide a line-by-line note today once the review is complete.

## Usage / Cost Estimate

- Total selected homepage sample count: 4
- Total evaluation sample count represented in the prior optimization run: 8
- Sapling calls represented by selected homepage values: 8
- Estimated characters sent to Sapling for selected homepage draft/rewrite pairs: 2,157
- Average characters per selected homepage pair: 539
- Notes: prior development evaluation eventually hit Sapling `429` capacity errors after repeated calls. The 2026-05-22 M4-001 refresh attempt hit `timeout_or_network` before generation. Unavailable scores are not counted as target-met results.

## Teacher Message

- Category: Teacher message
- Used on homepage: yes
- Incoming context: Maya asks whether she can still submit a missed reflection after a family issue.
- Rough draft word count: 56
- Rough draft estimated character count: 366
- Rewritten reply word count: 46
- Rewritten reply estimated character count: 259
- Displayed excerpt word count: 102
- Displayed excerpt estimated character count: 625
- Sapling call count used for selected result: 2
- Estimated Sapling characters consumed: 625
- Draft AI-like signal: 81%
- Rewrite AI-like signal: 39%
- Score change: -42 pts
- Preserved facts checklist: Maya, missed reflection, family issue, late-work policy, review tomorrow/next step, no guaranteed approval.

Rough draft:

> Dear Maya, I acknowledge receipt of your email regarding the missed reflection. Late submissions are generally not accepted under the course policy. I will review the circumstances you described and determine whether any exception can be considered. Please be advised that approval is not guaranteed and further information may be required before a decision is made.

Rewritten reply:

> Hi Maya, thanks for letting me know what happened. I can look at this with you tomorrow and check it against the late-work policy before deciding the next step. If there is anything else I should understand about the family issue, send it through before then.

## Sales Follow-Up

- Category: Sales follow-up
- Used on homepage: yes
- Incoming context: Jordan says the team is still comparing vendors and may need another week.
- Rough draft word count: 51
- Rough draft estimated character count: 309
- Rewritten reply word count: 42
- Rewritten reply estimated character count: 233
- Displayed excerpt word count: 93
- Displayed excerpt estimated character count: 542
- Sapling call count used for selected result: 2
- Estimated Sapling characters consumed: 542
- Draft AI-like signal: 76%
- Rewrite AI-like signal: 41%
- Score change: -35 pts
- Preserved facts checklist: Jordan, proposal from Tuesday, vendor comparison, another week, no discount promise.

Rough draft:

> Hello Jordan, I am following up regarding the proposal sent last Tuesday. Please advise whether your team has completed its vendor comparison and whether you would like to proceed with the package as discussed. I would appreciate any update you can provide so that we can determine the appropriate next steps.

Rewritten reply:

> Hi Jordan, just checking back on the proposal from Tuesday. If your team is still comparing vendors, no problem. I can send a shorter version with the two options side by side, or answer anything that would help you decide next week.

## Workplace Email

- Category: Workplace email
- Used on homepage: yes
- Incoming context: A teammate needs revised numbers for a partner update, but the source file arrived late.
- Rough draft word count: 47
- Rough draft estimated character count: 295
- Rewritten reply word count: 38
- Rewritten reply estimated character count: 185
- Displayed excerpt word count: 85
- Displayed excerpt estimated character count: 480
- Sapling call count used for selected result: 2
- Estimated Sapling characters consumed: 480
- Draft AI-like signal: 73%
- Rewrite AI-like signal: 32%
- Score change: -41 pts
- Preserved facts checklist: revised numbers, source file arrived late, partner update, final version by 4pm Friday.

Rough draft:

> Unfortunately, the requested numbers are not available at this time because the source information was delayed. I understand that the partner update is important, and I will provide the revised figures as soon as the underlying file has been checked and the information is ready for circulation.

Rewritten reply:

> The source file came in late, so I need one more check before I send the revised numbers. I know you need them for the partner update. I will get the final version to you by 4pm Friday.

## Client Reply

- Category: Client reply
- Used on homepage: yes
- Incoming context: Priya asks why this month's report totals look different from last month.
- Rough draft word count: 48
- Rough draft estimated character count: 294
- Rewritten reply word count: 39
- Rewritten reply estimated character count: 216
- Displayed excerpt word count: 87
- Displayed excerpt estimated character count: 510
- Sapling call count used for selected result: 2
- Estimated Sapling characters consumed: 510
- Draft AI-like signal: 79%
- Rewrite AI-like signal: 37%
- Score change: -42 pts
- Preserved facts checklist: Priya, report totals changed, hidden category now included, line-by-line note today.

Rough draft:

> Dear Priya, we apologize for any inconvenience caused by the discrepancy in the report totals. Our team is currently looking into the matter and will provide an update as soon as possible. We appreciate your patience while we review the relevant information and determine what may have changed.

Rewritten reply:

> Hi Priya, thanks for flagging this. I am checking the report now because this month includes a category that was hidden last month. I will send you a clear line-by-line note today so you can see exactly what changed.
