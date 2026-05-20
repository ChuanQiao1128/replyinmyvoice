# Scenario Evaluation Results

Date: 2026-05-19T23:49:58.799Z
Strategy: fact_reconstruct
Naturalness threshold: 40%
Cases evaluated: 40
Draft-only cases: 29
Measured cases: 40
Long cases (300+ words): 2
Long customer-support cases (300+ words): 0
Average AI-like signal drop: 89 pts
Rewrite below 50% AI-like signal: 40/40
Final selected rewrites worse than draft: 0/40
Cases using targeted repair: 37/40
Rejected candidate events: 39
Fact preservation or unsupported-addition failures: 0
Customer-usable pass count: 40/40
Strict signal pass count: 40/40

Customer-usable pass requires: rewritten output exists, all expected facts are preserved, no unsupported names/dates/amounts/counts are added, no quality failure is raised, and the selected rewrite is not worse than the draft when scores are available.
Strict signal pass additionally requires scores available and: if the draft is above 40%, the final rewrite is at or below 40%; if the draft is already at or below 40%, the final rewrite does not raise the signal.

## draft-only-01-teacher-jordan

Scenario: General reply
Tone: Warm
Input word count: 64
Input character count: 387
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 89%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 0%
Change: -89 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate.
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
- reading response
- vocabulary practice
- Friday
- end of this week
- partial credit
- Ms. Carter

Before:

```text
Hi Monica,

Jordan is missing the reading response, vocabulary practice, and the short reflection paragraph from Friday. He should start with the reading response and vocabulary practice because those can be done quickly. Then he can work on the reflection paragraph. If he turns everything in by the end of this week, I can still accept it for partial credit.

Best regards,
Ms. Carter
```

After:

```text
Hi Monica,

Jordan is missing the reading response, vocabulary practice, and the short reflection paragraph from Friday.

He should start with the reading response and vocabulary practice because those can be done quickly.

Then he can work on the reflection paragraph.

If he turns everything in by the end of this week, I can still accept it for partial credit.

Best regards,
Ms. Carter
```

## draft-only-01b-teacher-jordan-long-polished

Scenario: General reply
Tone: Warm
Input word count: 253
Input character count: 1490
Diagnosis tags: stock_opening
Rewrite plan: Target stock_opening while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 0%
Change: -100 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate.
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

For Jordan, the missing work is still the main issue. He has three assignments missing from the past two weeks: reading response, vocabulary practice, the short reflection paragraph from last Friday.

He has been participating in class discussions, but the grade is being pulled down by the missing written work.

Have him start with the reading response and vocabulary practice, since those should be the quickest.

Then he can work on the short reflection paragraph from last Friday.

If he submits all three by this Friday at 5 p.m., I can still give partial credit.

He does not need to redo work he already completed; this is only about the missing assignments.

He can also come by during lunch on Tuesday or Thursday if any instructions are unclear.

Best regards,
Ms. Carter
```

## draft-only-02-teacher-kai

Scenario: General reply
Tone: Direct
Input word count: 50
Input character count: 284
Diagnosis tags: policy_memo_voice
Rewrite plan: Target policy_memo_voice while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: 100%
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 0%
Change: -100 pts
Rejected candidate reasons: initial: Fact reconstruct final naturalness gate. | targeted_repair: Targeted repair of high-risk sentences after the first final missed the naturalness gate. | repair: Strong-model escalation naturalness and fact gate.
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Expected facts:
- Lena
- Kai
- two participation activities
- one exit ticket
- course policy
- before class tomorrow
- not promising

Before:

```text
Hi Lena, Kai's grade changed because two participation activities and one exit ticket are still missing. The course policy says late work may not receive full credit, but he can submit the exit ticket before class tomorrow and I will review it. I am not promising a grade change yet.
```

After:

```text
Hi Lena,

Kai's grade changed because two participation activities and one exit ticket are still missing.

The course policy says late work may not receive full credit, but he can submit the exit ticket before class tomorrow and I will review it.

I'm not promising a grade change yet.
```

## draft-only-03-teacher-amelia

Scenario: General reply
Tone: Warm
Input word count: 41
Input character count: 219
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 2%
Change: -98 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate.
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Expected facts:
- Mr. Ortiz
- Amelia
- lab notes
- reflection paragraph
- Tuesday
- Thursday
- late but complete

Before:

```text
Hi Mr. Ortiz, Amelia did complete the lab notes, but I have not received the reflection paragraph from Tuesday. She can bring it to lunch study hall on Thursday. If she submits it then, I can mark it late but complete.
```

After:

```text
Hi Mr. Ortiz,

Amelia did complete the lab notes, but I have not received the reflection paragraph from Tuesday.

She can bring it to lunch study hall on Thursday.

If she submits it then, I can mark it late but complete.
```

## draft-only-04-teacher-ravi

Scenario: General reply
Tone: Direct
Input word count: 38
Input character count: 211
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 26%
Change: -74 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate.
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Expected facts:
- Priya
- Ravi
- quiz
- Monday
- Wednesday at 8:15am
- Friday after school
- cannot enter the quiz score

Before:

```text
Hi Priya, Ravi was absent for the quiz on Monday and still needs to schedule the make-up. The available times are Wednesday at 8:15am or Friday after school. I cannot enter the quiz score until he completes it.
```

After:

```text
Hi Priya,

Ravi missed Monday's quiz and still needs to make it up.

The available times are Wednesday at 8:15am or Friday after school. I can't enter the quiz score until he completes it.
```

## draft-only-05-teacher-sam

Scenario: General reply
Tone: Warm
Input word count: 38
Input character count: 214
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 38%
Change: -62 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate.
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Expected facts:
- Dana
- Sam
- bibliography
- three sources
- Friday
- final version
- May 22

Before:

```text
Hi Dana, Sam's project is strong, but the bibliography is missing three sources. If he adds the sources by Friday, I can grade the final version instead of the draft version. The presentation date is still May 22.
```

After:

```text
Hi Dana,

Sam's project is strong, but the bibliography is missing three sources.

If he adds the sources by Friday, I can grade the final version instead of the draft version.

The presentation date is still May 22.
```

## draft-only-06-teacher-noah

Scenario: General reply
Tone: Direct
Input word count: 37
Input character count: 189
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 4%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: 0%
Change: -4 pts
Rejected candidate reasons: none
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Expected facts:
- Ms. Nguyen
- Noah
- worksheet
- graph
- Canvas
- 6pm tonight
- only the graph file

Before:

```text
Hi Ms. Nguyen, Noah turned in the worksheet but did not attach the graph. Please ask him to upload the graph in Canvas by 6pm tonight. I will not need a new worksheet, only the graph file.
```

After:

```text
Hi Ms. Nguyen,

Noah turned in the worksheet but did not attach the graph.

Please ask him to upload the graph in Canvas by 6pm tonight.

I will not need a new worksheet, only the graph file.
```

## draft-only-07-teacher-maya

Scenario: General reply
Tone: Warm
Input word count: 37
Input character count: 203
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 98%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 98%
Final selected AI-like signal: 0%
Change: -98 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate.
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Expected facts:
- Alex
- Maya
- science fair group
- permission slip
- Thursday morning
- Room 18
- project idea

Before:

```text
Hi Alex, Maya can still join the science fair group, but the permission slip is due Thursday morning. The group is meeting in Room 18 after school. She should bring the signed slip and her project idea.
```

After:

```text
Hi Alex,

Maya can still join the science fair group, but the permission slip is due Thursday morning.

The group is meeting in Room 18 after school.

She should bring the signed slip and her project idea.
```

## draft-only-08-teacher-omar

Scenario: General reply
Tone: Direct
Input word count: 37
Input character count: 199
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 93%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 0%
Change: -93 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate.
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Expected facts:
- Jordan
- Omar
- reading log
- March 14
- half credit
- quiz retake
- scheduled with me

Before:

```text
Hi Jordan, Omar has improved his reading log, but the March 14 response is still missing. He can turn in that response for half credit. The quiz retake is separate and needs to be scheduled with me.
```

After:

```text
Hi Jordan,

Omar has improved his reading log, but the March 14 response is still missing.

He can turn in that response for half credit.

The quiz retake is separate and needs to be scheduled with me.
```

## draft-only-09-support-tax

Scenario: General reply
Tone: Warm
Input word count: 34
Input character count: 202
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 12%
Final selected AI-like signal: 2%
Change: -98 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate.
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Expected facts:
- Sarah
- tax line
- Australia
- base subscription
- June 1
- May invoice
- not be recalculated

Before:

```text
Hi Sarah, the tax line increased because the billing address changed to Australia. The base subscription is unchanged. The new tax amount starts on June 1, and the May invoice will not be recalculated.
```

After:

```text
Hi Sarah,

The tax line increased because the billing address changed to Australia.

The base subscription is unchanged.

The new tax amount starts on June 1, and the May invoice will not be recalculated.
```

## draft-only-10-support-refund

Scenario: General reply
Tone: Direct
Input word count: 29
Input character count: 160
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 0%
Change: -100 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate.
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Expected facts:
- Eli
- refund window
- May 10
- account credit
- manager approval
- cannot promise

Before:

```text
Hi Eli, the refund window ended May 10. We can offer account credit, but manager approval is required before I can apply it. I cannot promise the credit today.
```

After:

```text
Hi Eli,

The refund window ended May 10.

We can offer account credit, but manager approval is required before I can apply it.

I can't promise the credit today.
```

## draft-only-11-support-export

Scenario: General reply
Tone: Warm
Input word count: 45
Input character count: 251
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 97%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 8%
Final selected AI-like signal: 8%
Change: -89 pts
Rejected candidate reasons: none
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Expected facts:
- Mina
- April CSV export
- custom tags column
- Northeast region
- data is still safe
- Monday at 10am

Before:

```text
Hi Mina, the April CSV export is missing the custom tags column for the Northeast region. The underlying campaign data is still safe. We are checking the export job and will send a corrected file before Monday at 10am if the check confirms the issue.
```

After:

```text
Mina,

The custom tags column is missing in the April CSV export for the Northeast. Please check the export job before Monday at 10am. The underlying campaign data is still safe.

Thank you.
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
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 0%
Change: -100 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate.
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

The usage report shows 18 active seats, but the renewal only approved 15 regular seats.

The extra NZD $126 appears tied to three temporary contractors who were active during May.

The base plan did not change.
```

## draft-only-13-support-login

Scenario: General reply
Tone: Warm
Input word count: 42
Input character count: 255
Diagnosis tags: low_specificity
Rewrite plan: Target low_specificity while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 0%
Change: -100 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate.
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Expected facts:
- Mina
- mina@northstar.example
- old pilot workspace
- workspace association issue
- resent the invite twice
- account link

Before:

```text
Hi Mina, keep signing in with mina@northstar.example. If you still land in the old pilot workspace, this is probably a workspace association issue, not a new invite issue. We already resent the invite twice, so support should check the account link next.
```

After:

```text
Hi Mina,

Keep signing in with mina@northstar.example.

If you still land in the old pilot workspace, this is probably a workspace association issue, not a new invite issue.

We already resent the invite twice, so support should check the account link next.
```

## draft-only-14-support-plan-change

Scenario: General reply
Tone: Direct
Input word count: 44
Input character count: 250
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 0%
Change: -100 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate.
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Expected facts:
- Arun
- Starter plan credit
- Team plan charge
- May 3
- proration
- not a duplicate charge
- invoice screenshot

Before:

```text
Hi Arun, the Starter plan credit and the Team plan charge are shown as separate lines because the plan changed on May 3. That usually means proration, not a duplicate charge. I still need the invoice screenshot before I can confirm the final amount.
```

After:

```text
Hi Arun,

The Starter plan credit and the Team plan charge are shown as separate lines because the plan changed on May 3.

That usually means proration, not a duplicate charge.

I still need the invoice screenshot before I can confirm the final amount.
```

## draft-only-15-support-notifications

Scenario: General reply
Tone: Warm
Input word count: 43
Input character count: 246
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 0%
Change: -100 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate.
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Expected facts:
- Claire
- ticket #4821
- duplicate notifications
- delivery logs
- root cause is not confirmed
- before noon
- pause the campaign

Before:

```text
Hi Claire, ticket #4821 is still open for duplicate notifications. The delivery logs are being reviewed, but the root cause is not confirmed yet. I will send the next status note before noon so your team can decide whether to pause the campaign.
```

After:

```text
Hi Claire,

ticket #4821 is still under review for the duplicate notifications.

What is confirmed: the duplicate notification issue is still happening, and the delivery logs are being checked. What is not confirmed yet is the root cause or whether a campaign pause is required.

I know your account team needs an answer on whether to pause the campaign before noon. We will send the next status update as soon as the log review gives a clear recommendation.
```

## draft-only-16-support-import

Scenario: General reply
Tone: Direct
Input word count: 35
Input character count: 185
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 2%
Change: -98 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate.
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Expected facts:
- Omar
- row 14
- invalid date format
- Rows 1 through 13
- not saved
- upload the file again
- Do not delete

Before:

```text
Hi Omar, the import failed because row 14 has an invalid date format. Rows 1 through 13 were not saved. Please fix row 14 and upload the file again. Do not delete the existing project.
```

After:

```text
Hi Omar,

The import failed because row 14 has an invalid date format.

Rows 1 through 13 were not saved.

Please fix row 14 and upload the file again.

Do not delete the existing project.
```

## draft-only-17-sales-renewal

Scenario: General reply
Tone: Warm
Input word count: 45
Input character count: 247
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 55%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 58%
Final selected AI-like signal: 0%
Change: -55 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate.
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Expected facts:
- Jordan
- renewal proposal
- two plan options
- finance thread
- two other vendors
- first week of June

Before:

```text
Hi Jordan, thanks for looking at the renewal proposal. I will send a shorter summary of the two plan options for your finance thread. I know your team is also comparing two other vendors, and the earliest decision point is the first week of June.
```

After:

```text
Hi Jordan,

Thanks for looking at the renewal proposal.

I will send a shorter summary of the two plan options for your finance thread.

I know your team is also comparing two other vendors, and the earliest decision point is the first week of June.
```

## draft-only-18-sales-demo

Scenario: General reply
Tone: Direct
Input word count: 35
Input character count: 188
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 96%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 98%
Final selected AI-like signal: 0%
Change: -96 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate.
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Expected facts:
- Leah
- Thursday at 3pm
- reporting
- team templates
- approval workflow
- will not include pricing

Before:

```text
Hi Leah, we can move the demo to Thursday at 3pm. I will keep the agenda focused on reporting, team templates, and the approval workflow. I will not include pricing unless you ask for it.
```

After:

```text
Hi Leah,

We can move the demo to Thursday at 3pm.

I will keep the agenda focused on reporting, team templates, and the approval workflow.

I will not include pricing unless you ask for it.
```

## draft-only-19-sales-proposal

Scenario: General reply
Tone: Warm
Input word count: 40
Input character count: 242
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 2%
Change: -98 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate.
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Expected facts:
- Mateo
- May 12
- section three
- pricing language
- section five
- rollout notes
- Friday
- legal team

Before:

```text
Hi Mateo, I attached the revised proposal with the implementation timeline from our May 12 call. Section three has the pricing language, and section five has the rollout notes. Please send comments by Friday if your legal team wants changes.
```

After:

```text
Hi Mateo,

I attached the revised proposal with the implementation timeline from our May 12 call.

Section three has the pricing language, and section five has the rollout notes.

Please send comments by Friday if your legal team wants changes.
```

## draft-only-20-sales-checkin

Scenario: General reply
Tone: Direct
Input word count: 40
Input character count: 222
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 94%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 93%
Final selected AI-like signal: 0%
Change: -94 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate.
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Expected facts:
- Nora
- not push for a decision
- Vendor A
- Vendor B
- security questionnaire
- today
- next Tuesday

Before:

```text
Hi Nora, I will not push for a decision this week. Your team is still comparing Vendor A and Vendor B, and finance asked for the security questionnaire first. I can send the questionnaire today and follow up next Tuesday.
```

After:

```text
Hi Nora,

I will not push for a decision this week.

Your team is still comparing Vendor A and Vendor B, and finance asked for the security questionnaire first.

I can send the questionnaire today and follow up next Tuesday.
```

## draft-only-21-sales-expansion

Scenario: General reply
Tone: Warm
Input word count: 36
Input character count: 212
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 94%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 0%
Change: -94 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate.
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Expected facts:
- Devon
- 12 additional seats
- July 1
- does not include the analytics add-on
- second quote
- manager approves

Before:

```text
Hi Devon, the expansion quote includes 12 additional seats starting July 1. It does not include the analytics add-on yet. If you want analytics included, I can send a second quote after your manager approves it.
```

After:

```text
Hi Devon,

The expansion quote includes 12 additional seats starting July 1.

It doesn't include the analytics add-on yet.

If you want analytics included, I can send a second quote after your manager approves it.
```

## draft-only-22-client-design

Scenario: General reply
Tone: Direct
Input word count: 32
Input character count: 182
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 46%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 0%
Change: -46 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate.
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Expected facts:
- Ava
- homepage mockup
- mobile version
- spacing check
- desktop today
- Wednesday morning
- logo color has not changed

Before:

```text
Hi Ava, the homepage mockup is ready, but the mobile version still needs one spacing check. I can send desktop today and mobile by Wednesday morning. The logo color has not changed.
```

After:

```text
Hi Ava,

The homepage mockup is ready, but the mobile version still needs one spacing check.

I can send desktop today and mobile by Wednesday morning.

The logo color has not changed.
```

## draft-only-23-client-invoice

Scenario: General reply
Tone: Warm
Input word count: 36
Input character count: 180
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 99%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 0%
Change: -99 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate.
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Expected facts:
- Ben
- invoice #317
- April 30
- PO-8842
- NZD $2,450
- May 20

Before:

```text
Hi Ben, invoice #317 was sent on April 30, but the PO number was missing. I have attached a corrected copy with PO-8842. The amount is still NZD $2,450 and the due date is May 20.
```

After:

```text
Hi Ben,

Hi Ben, invoice #317 was sent on April 30, but the PO number was missing.

I have attached a corrected copy with PO-8842. The amount is still NZD $2,450 and the due date is May 20.
```

## draft-only-24-client-delay

Scenario: General reply
Tone: Direct
Input word count: 34
Input character count: 186
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 13%
Change: -87 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate.
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Expected facts:
- Grace
- source file arrived late
- not be ready by noon
- 4pm Friday
- one quality check
- dashboard numbers are unchanged

Before:

```text
Hi Grace, the source file arrived late, so the report will not be ready by noon. I can send the clean version by 4pm Friday after one quality check. The dashboard numbers are unchanged.
```

After:

```text
Quick update: Hi Grace, the source file arrived late, so the report will not be ready by noon.

I can send the clean version by 4pm Friday after one quality check.

The dashboard numbers are unchanged.
```

## reply-01-teacher-extension

Scenario: Email or message reply
Tone: Warm
Input word count: 99
Input character count: 596
Diagnosis tags: stock_opening, corporate_polish, policy_memo_voice
Rewrite plan: Target stock_opening, corporate_polish, policy_memo_voice while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: 0%
Change: -100 pts
Rejected candidate reasons: none
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

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

Dear Student, I acknowledge receipt of your email regarding the missed reflection deadline.

Late submissions are generally subject to the course policy and may not be accepted. However, I understand that you have indicated a family issue. I will review the situation and respond accordingly. Please be advised that submitting before class tomorrow does not guarantee that it will be accepted. Hi Professor, I missed the reflection deadline because of a family issue this week.
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
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate.
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

## work-01-design-delay

Scenario: Work update
Tone: Direct
Input word count: 70
Input character count: 406
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 0%
Change: -100 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate.
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

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
Unfortunately, the revised screenshots are not available at this time due to a delay in receiving the updated design file.

The source file arrived late this morning and still requires one quality check before it can be shared externally.

I expect to send the screenshots by 4pm Friday if there are no further issues.
```

## work-02-launch-risk

Scenario: Work update
Tone: Direct
Input word count: 69
Input character count: 384
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 4%
Change: -96 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate.
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

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
Tone: Warm
Input word count: 73
Input character count: 436
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 1%
Change: -99 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate.
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

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
Quick update from the six interviews with teachers this week:

The teacher interview notes are ready for review.

Four teachers said the onboarding copy felt too technical. Two asked if they could see a sample response before signing up.

I think the first screen should be updated before Wednesday, with one short example added.
```

## 2026-05-20 Clean-Final Regression

Focused regression covered the Priya-style internal note leak:

```text
The May 8 client handover is referenced.
```

Result:

- deterministic clean-final check rejects `is referenced` meta language
- low Naturalness Check alone is not enough to pass
- pipeline repairs/escalates before returning a user-visible rewrite
- prompt contract forbids internal analysis wording in finalizer, targeted repair, and escalation
- targeted unit result: 23/23 relevant rewrite-pipeline tests passed

## work-04-long-launch-readiness

Scenario: Work update
Tone: Direct
Input word count: 376
Input character count: 2178
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 99%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 3%
Change: -96 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate.
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
The current launch readiness status is as follows.

The payment flow passed the latest smoke test, the onboarding checklist has been reviewed, and the help article links have been updated in the footer.

The remaining item is the webhook retry log, where I am reviewing the last three failed events from this morning.

I expect to post the result before the 2pm launch check.

At this time, I don't recommend delaying the launch unless the retry log shows a repeated payment event failure.

The rewrite should keep the payment flow, onboarding checklist, help article links, webhook retry log, last three failed events, and 2pm launch check.

It should not add new test results.

It should make the update easier to scan, but it should not turn into a formal status report with too many headings.

The final message should be clear enough that a manager can see what is ready and what is still being checked.

If the text uses bullets, keep them short.

If it uses paragraphs, make each paragraph do a different job: ready items, pending review, and launch recommendation.

Do not make the launch sound approved if the retry log still has to be checked.
```

## work-05-long-design-delay

Scenario: Work update
Tone: Direct
Input word count: 348
Input character count: 2055
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 3%
Change: -97 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate.
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

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
Quick update: Unfortunately, the revised screenshots are not yet available because the updated design source file arrived later than expected this morning.

The screen captures still need one quality check before they can be shared externally, especially because the pricing table changed in section three and the partner logo appears on the final slide.

I expect to send the screenshots by 4pm Friday if there are no further issues.
```

## draft-only-36-cover-support

Scenario: General reply
Tone: Direct
Input word count: 42
Input character count: 251
Diagnosis tags: low_specificity
Rewrite plan: Target low_specificity while preserving the user's facts.
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
- Support Specialist
- email and chat
- recurring issues
- product team
- help center articles
- do not make me sound senior

Before:

```text
I am applying for the Support Specialist role. I answer customer questions by email and chat, summarize recurring issues for the product team, and update help center articles when the same question keeps coming up. Please do not make me sound senior.
```

After:

```text
I am applying for the Support Specialist role.

I answer customer questions by email and chat, summarize recurring issues for the product team, and update help center articles when the same question keeps coming up.

Please don't make me sound senior.
```

## reply-02-sales-followup

Scenario: Email or message reply
Tone: Warm
Input word count: 71
Input character count: 446
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 99%
Final selected AI-like signal: 0%
Change: -100 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate.
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

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

## support-01-priya-billing

Scenario: Customer support
Tone: Warm
Input word count: 169
Input character count: 965
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 12%
Change: -88 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate.
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

Before:

```text
Hi Priya, Thank you for contacting us regarding the usage report and invoice preview in your account. We understand that there appears to be a discrepancy between the number of active seats shown in the dashboard and the number of seats approved during your renewal. The most likely explanation is that the three temporary contractors were counted as active seats during May. Even if a user only joins for part of the month, prorated charges may apply. Please check whether the contractors are still active and send us their names if you would like assistance.
```

After:

```text
Hi Priya,

Thanks for laying this out. The jump looks tied to the three temporary contractors being counted during May, not a base plan change.

That explains why the dashboard shows 18 active seats instead of the 15 regular seats your team approved. The NZD $126 increase looks like the prorated charge for the days those accounts had access.

Before the invoice is finalized, check whether those contractor accounts are still active. They may still be active if they were not removed after May 8. If you send over their names or email addresses, we can help confirm their status. We will not change anything unless you ask us to.
```

## support-02-export-error

Scenario: Customer support
Tone: Direct
Input word count: 86
Input character count: 505
Diagnosis tags: stock_opening, corporate_polish
Rewrite plan: Target stock_opening, corporate_polish while preserving the user's facts.
Draft AI-like signal: 32%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 98%
Final selected AI-like signal: 0%
Change: -32 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate.
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

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
The April exports are missing the custom tags column.

The underlying data does not look deleted from what you described.

Use the dashboard export as the source for the other fields while we check why that column is missing. If you need a corrected file for the Monday board packet, send us the export settings you used.
```

## support-03-login-access

Scenario: Customer support
Tone: Direct
Input word count: 75
Input character count: 484
Diagnosis tags: over_safe_tone
Rewrite plan: Target over_safe_tone while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 1%
Change: -99 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate.
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

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
Hi Mina,

Mina should keep signing in with mina@northstar.example. If she still lands in the old team, this is likely a workspace association issue rather than a new invite issue.

Since you already resent the invite twice, I would not keep repeating that step.

The next useful step is for support to check which workspace mina@northstar.example is linked to and whether the newest Northstar invitation attached to the right account. That should explain why she cannot reach the right workspace.
```

## draft-only-25-work-launch

Scenario: General reply
Tone: Warm
Input word count: 36
Input character count: 212
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 93%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 0%
Change: -93 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate.
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Expected facts:
- payment flow
- smoke test
- onboarding checklist
- help article links
- last three failed webhook events
- 2pm launch check

Before:

```text
Quick update: the payment flow passed the smoke test, the onboarding checklist is done, and the help article links are live. I am still reviewing the last three failed webhook events before the 2pm launch check.
```

After:

```text
Quick update: the payment flow passed the smoke test, the onboarding checklist is done, and the help article links are live.

I am still reviewing the last three failed webhook events before the 2pm launch check.
```

## draft-only-26-work-blockers

Scenario: General reply
Tone: Direct
Input word count: 35
Input character count: 182
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: 0%
Change: -100 pts
Rejected candidate reasons: none
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Expected facts:
- Nina
- API fix
- Omar
- QA script
- Friday demo
- vendor API timeout
- 11am retry

Before:

```text
Nina owns the API fix, Omar owns the QA script, and both are due before the Friday demo. The only blocker is the vendor API timeout. I will post another update after the 11am retry.
```

After:

```text
Nina owns the API fix, Omar owns the QA script, and both are due before the Friday demo.

The only blocker is the vendor API timeout.

I will post another update after the 11am retry.
```

## draft-only-27-work-research

Scenario: General reply
Tone: Warm
Input word count: 35
Input character count: 223
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 86%
Final selected AI-like signal: 1%
Change: -99 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate.
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Expected facts:
- six teacher interviews
- four teachers
- two asked
- sample response
- first screen
- Wednesday

Before:

```text
We finished six teacher interviews this week. Four teachers said the onboarding copy felt too technical, and two asked for a sample response before signing up. I recommend updating the first screen before Wednesday's test.
```

After:

```text
Quick update from the six interviews with teachers this week:

The teacher interview notes are ready for review.

Four teachers said the onboarding copy felt too technical. Two asked if they could see a sample response before signing up.

I think the first screen should be updated before Wednesday, with one short example added.
```
