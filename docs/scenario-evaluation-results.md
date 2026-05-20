# Scenario Evaluation Results

Date: 2026-05-20T07:16:53.538Z
Eval mode: full
Strategy: adaptive_rewrite_orchestrator
Naturalness threshold: 40%
Cases evaluated: 60
Draft-only cases: 38
Measured cases: 60
Long cases (300+ words): 10
Long customer-support cases (300+ words): 6
Average AI-like signal drop: 85 pts
Rewrite below 50% AI-like signal: 60/60
Final selected rewrites worse than draft: 0/60
Cases using targeted repair: 52/60
Rejected candidate events: 53
Fact preservation or unsupported-addition failures: 0
Customer-usable pass count: 60/60
Strict signal pass count: 60/60

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
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:short reflection paragraph from friday missing:partial credit missing:friday unsupported:these
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

Jordan is missing the reading response, vocabulary practice, and the short reflection paragraph from Friday. He should start with the reading response and vocabulary practice because those can be done quickly.

Then he can work on the reflection paragraph. If he turns everything in by the end of this week, I can still accept it for partial credit.

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
Final selected AI-like signal: 1%
Change: -99 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:partial credit unsupported:these
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

## draft-only-02-teacher-kai

Scenario: General reply
Tone: Direct
Input word count: 50
Input character count: 284
Diagnosis tags: policy_memo_voice
Rewrite plan: Target policy_memo_voice while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 2%
Final selected AI-like signal: 0%
Change: -100 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:not promising unsupported:he unsupported:remember unsupported:let
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

Kai's grade changed because two participation activities and one exit ticket are still missing. The course policy says late work may not receive full credit, but he can submit the exit ticket before class tomorrow and I will review it.

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
Final selected AI-like signal: 3%
Change: -97 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:lab notes missing:late but complete missing:tuesday unsupported:let
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

Amelia did complete the lab notes, but I have not received the reflection paragraph from Tuesday. She can bring it to lunch study hall on Thursday.

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
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:cannot enter the quiz score unsupported:he malformed:dangling_closing
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
First candidate AI-like signal: 0%
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

I hope you’re doing well! I wanted to let you know that Sam’s project looks strong, but there are three sources missing in the bibliography. If he can add those by Friday, that would help a lot. Also, please remember I need the final version instead of the draft by May 22.

Thanks!
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

Noah turned in the worksheet but did not attach the graph. Please ask him to upload the graph in Canvas by 6pm tonight.

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
Repair candidate AI-like signal: 0%
Final selected AI-like signal: 0%
Change: -98 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:alex missing:permission slip
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

Maya can still join the science fair group, but the permission slip is due Thursday morning. The group is meeting in Room 18 after school.

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
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:scheduled with me unsupported:hey jordan unsupported:let
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

Omar has improved his reading log, but the March 14 response is still missing. He can turn in that response for half credit.

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
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 2%
Change: -98 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:sarah missing:base subscription
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

The tax line increased because the billing address changed to Australia. The base subscription is unchanged.

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
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:refund window missing:may 10 unsupported:good unsupported:let Dropped a no-promise/no-guarantee constraint.
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

Hi Eli, the refund window ended May 10.

We can offer account credit, but manager approval is required before I can apply it. I cannot promise the credit today.
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
Repair candidate AI-like signal: 0%
Final selected AI-like signal: 0%
Change: -97 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:mina missing:northeast region missing:data is still safe missing:underlying campaign data is still safe missing:northeast
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
Hi Mina,

The April CSV export is missing the custom tags column for the Northeast region. The underlying campaign data is still safe.

We are checking the export job and will send a corrected file before Monday at 10am if the check confirms the issue.
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
Repair candidate AI-like signal: 0%
Final selected AI-like signal: 33%
Change: -67 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:base plan missing:base plan did not change missing:18 active seats missing:15 regular seats unsupported:let missing_locked:did not change
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

## draft-only-13-support-login

Scenario: General reply
Tone: Warm
Input word count: 42
Input character count: 255
Diagnosis tags: low_specificity
Rewrite plan: Target low_specificity while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 1%
Final selected AI-like signal: 0%
Change: -100 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:old pilot workspace missing:resent the invite twice missing:mina@northstar.example unsupported:support
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

Keep signing in with mina@northstar.example. If you still land in the old pilot workspace, this is probably a workspace association issue, not a new invite issue.

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

The Starter plan credit and the Team plan charge are shown as separate lines because the plan changed on May 3. That usually means proration, not a duplicate charge.

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
Repair candidate AI-like signal: 0%
Final selected AI-like signal: 37%
Change: -63 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:claire
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

Ticket #4821 is still open for duplicate notifications. The delivery logs are being reviewed, but the root cause is not confirmed yet.

I will send the next status note before noon so your team can decide whether to pause the campaign.
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
Repair candidate AI-like signal: 91%
Final selected AI-like signal: 2%
Change: -98 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:rows unsupported:remember missing_locked:1 through 13
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

The import failed because row 14 has an invalid date format. Rows 1 through 13 were not saved.

Please fix row 14 and upload the file again. Do not delete the existing project.
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
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 0%
Change: -55 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:jordan missing:renewal proposal missing:finance thread unsupported:could
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

Thanks for looking at the renewal proposal. I will send a shorter summary of the two plan options for your finance thread.

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
Repair candidate AI-like signal: 11%
Final selected AI-like signal: 0%
Change: -96 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:leah missing:will not include pricing
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

We can move the demo to Thursday at 3pm. I will keep the agenda focused on reporting, team templates, and the approval workflow.

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
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:mateo missing:pricing language missing:implementation timeline missing:section three missing:section five missing:rollout notes missing:legal team unsupported:attached
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

I attached the revised proposal with the implementation timeline from our May 12 call. Section three has the pricing language, and section five has the rollout notes.

Please send comments by Friday if your legal team wants changes.
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
Repair candidate AI-like signal: 98%
Final selected AI-like signal: 0%
Change: -100 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:professor missing:student missing:dear student
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

Late submissions are generally subject to the course policy and may not be accepted. However, I understand that you have indicated a family issue. I will review the situation and respond accordingly. Please be advised that submitting before class tomorrow does not guarantee that it will be accepted.

Hi Professor, I missed the reflection deadline because of a family issue this week. I know the policy says late work may not be accepted, but is there any way I can still submit it before class tomorrow.
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
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:one missing exit ticket unsupported:there
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
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:source file arrived late missing:unfortunately unsupported:could
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
Quick update: Unfortunately, the revised screenshots are not available at this time due to a delay in receiving the updated design file.

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
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:last three failed events
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
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: 1%
Change: -99 pts
Rejected candidate reasons: none
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

## work-04-long-launch-readiness

Scenario: Work update
Tone: Direct
Input word count: 376
Input character count: 2178
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 99%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 1%
Final selected AI-like signal: 0%
Change: -99 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:last three failed events missing:help article links unsupported:we've unsupported:let template_phrase:moving forward
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
Hi launch team,

The current launch readiness status is as follows.

The payment flow passed the latest smoke test, the onboarding checklist has been reviewed, and the help article links have been updated in the footer. The remaining item is the webhook retry log, where I am reviewing the last three failed events from this morning. I expect to post the result before the 2pm launch check. At this time, I do not recommend delaying the launch unless the retry log shows a repeated payment event failure.

The rewrite should keep the payment flow, onboarding checklist, help article links, webhook retry log, last three failed events, and 2pm launch check. It should not add new test results.
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
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:unfortunately unsupported:we're
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

## draft-only-20-sales-checkin

Scenario: General reply
Tone: Direct
Input word count: 40
Input character count: 222
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 94%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 0%
Final selected AI-like signal: 0%
Change: -94 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:nora missing:not push for a decision unsupported:as finance
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

I will not push for a decision this week. Your team is still comparing Vendor A and Vendor B, and finance asked for the security questionnaire first.

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
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:devon missing:12 additional seats missing:july 1 missing:july unsupported:once missing_locked:July 1
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

The expansion quote includes 12 additional seats starting July 1. It doesn't include the analytics add-on yet.

If you want analytics included, I can send a second quote after your manager approves it.
```

## draft-only-36-cover-support

Scenario: General reply
Tone: Direct
Input word count: 42
Input character count: 251
Diagnosis tags: low_specificity
Rewrite plan: Target low_specificity while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 11%
Final selected AI-like signal: 1%
Change: -99 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:do not make me sound senior
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

Please keep the wording at the Support Specialist level; do not make me sound senior.
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
Repair candidate AI-like signal: 98%
Final selected AI-like signal: 0%
Change: -100 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:reporting feature malformed:dangling_closing
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
Diagnosis tags: support_template_voice
Rewrite plan: Target support_template_voice while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 1%
Final selected AI-like signal: 12%
Change: -88 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:may 8 missing:15 regular seats missing:priya unsupported:3 users
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
Diagnosis tags: stock_opening, corporate_polish, support_template_voice
Rewrite plan: Target stock_opening, corporate_polish, support_template_voice while preserving the user's facts.
Draft AI-like signal: 32%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 0%
Change: -32 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:monday unsupported:unfortunately unsupported:sorry
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
The April CSV export is missing the custom tags column, but the underlying campaign data is still safe.

We are checking the export job. If the check confirms the issue, we will send a corrected file for the Monday board packet.
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
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:resent the invite twice unsupported:let unsupported:support
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

Mina's email address is mina@northstar.example, and she should keep signing in with it. If she still lands in the old team, this is likely a workspace association issue rather than a new invite issue.

The invite was resent twice, so I would not keep repeating that step.

The next useful step is for support to check which workspace mina@northstar.example is linked to and whether the newest Northstar invitation attached to the right account. That should explain why she cannot reach the right workspace.
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
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:base plan missing:finance manager missing:nzd $126 missing:18 active seats missing:15 regular seats unsupported:let
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
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:northeast region missing:data is still safe missing:northeast unsupported:updates
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

## support-06-login-workspace-long

Scenario: Customer support
Tone: Direct
Input word count: 337
Input character count: 2074
Diagnosis tags: over_safe_tone
Rewrite plan: Target over_safe_tone while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 1%
Change: -99 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:resent the invite twice unsupported:first
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
- old pilot workspace
- billing report folder

Before:

```text
Hello, thank you for contacting support. We understand the issue regarding Mina's account access and apologize for the inconvenience caused by the workspace mismatch. Based on the information provided, this may be related to the user's previous workspace association or an incomplete invitation acceptance flow. Please ensure that Mina is logging in with the correct email address, mina@northstar.example, and that she has accepted the most recent invitation. If the problem persists, our team can review the account association and provide additional guidance regarding the next steps.

The reply needs to acknowledge the steps already taken without repeating them as if they are new instructions. It should make clear that Mina should keep using mina@northstar.example and that the previous workspace association may be the reason she is landing in the old pilot workspace. It should also preserve the fact that the invite was resent twice and that the missing destination is the billing report folder. Do not promise that support will unlink the account immediately unless the user asks for that change.
```

After:

```text
Hi Mina,

Mina's email address is mina@northstar.example, and she should keep signing in with it. If she still lands in the old pilot workspace, this is likely a workspace association issue rather than a new invite issue.

The invite was resent twice, so I would not keep repeating that step.

The next useful step is for support to check which workspace mina@northstar.example is linked to and whether the newest Northstar invitation attached to the right account. That should explain why she cannot reach the billing report folder.
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
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 0%
Change: 0 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:starter plan missing:old plan credit missing:new plan charge missing:shared templates missing:finance thread missing:starter unsupported:following
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
Rejected candidate reasons: initial: Fact reconstruct final naturalness gate. | targeted_repair: Targeted repair of high-risk sentences after the first final missed the naturalness gate. | repair: Strong-model escalation naturalness and fact gate.
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
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: 0%
Change: -78 pts
Rejected candidate reasons: none
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

## cover-05-long-support-specialist

Scenario: Cover letter
Tone: Warm
Input word count: 367
Input character count: 2388
Diagnosis tags: low_specificity, application_cliche
Rewrite plan: Target low_specificity, application_cliche while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 0%
Change: -100 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:help center articles
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

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
I am excited to submit my application for the Support Specialist role.

In my previous position, I answered customer questions through email and chat, summarized recurring issues for our product team, and updated help center articles when we noticed the same question coming up repeatedly.

I enjoy making complicated product details easier for customers to understand.

That kind of practical support experience would fit a small SaaS support team.

Please keep the wording at the Support Specialist level; do not make me sound senior.
```

## reply-04-long-sales-renewal

Scenario: Email or message reply
Tone: Warm
Input word count: 408
Input character count: 2418
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 0%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 0%
Change: 0 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:reporting feature missing:team templates missing:two other vendors unsupported:there unsupported:let
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
Hi Jordan,

Thanks for looking at the renewal proposal. I can send a shorter summary of the two plan options for your internal thread. Glad the reporting feature and team templates are still useful.

No rush from my side; first week of June works while you compare the two other vendors.
```

## draft-only-35-cover-program

Scenario: General reply
Tone: Warm
Input word count: 45
Input character count: 282
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 0%
Change: -100 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:32 volunteers missing:attendance numbers unsupported:coordinator
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Expected facts:
- Program Manager
- 32 volunteers
- monthly partner updates
- weekend workshop schedules
- attendance numbers
- grant reports
- education access

Before:

```text
I am applying for the Program Manager role. In my current job, I coordinate 32 volunteers, prepare monthly partner updates, manage weekend workshop schedules, and track attendance numbers for grant reports. I care about education access but do not want the letter to sound generic.
```

After:

```text
I am applying for the Program Manager role. In my current job, I coordinate 32 volunteers, prepare monthly partner updates, manage weekend workshop schedules, and track attendance numbers for grant reports.

I care about education access but don't want the letter to sound generic.
```

## draft-only-37-general-policy

Scenario: General reply
Tone: Warm
Input word count: 31
Input character count: 207
Diagnosis tags: low_specificity
Rewrite plan: Target low_specificity while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 0%
Change: -100 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:do not need to send them again missing:agenda is unchanged
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Expected facts:
- already submitted questions
- do not need to send them again
- agenda is unchanged
- Saturday's workshop
- Printed scholarship drafts

Before:

```text
Participants who already submitted questions do not need to send them again. The agenda is unchanged, and the room change only affects this Saturday's workshop. Printed scholarship drafts are still welcome.
```

After:

```text
Saturday workshop note:

The agenda is unchanged. The room change only affects this Saturday's workshop.

Already submitted questions? No need to send them again. Printed scholarship drafts are still welcome.
```

## blank-01-partner-update

Scenario: Blank / custom
Tone: Direct
Input word count: 70
Input character count: 449
Diagnosis tags: corporate_polish
Rewrite plan: Target corporate_polish while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 1%
Change: -99 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:section three missing:section five missing:may 12
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

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
Quick update: section three has the updated pricing language, and section five has the implementation timeline from our May 12 call.

Please take a look when you can so we can keep moving.
```

## blank-02-community-note

Scenario: Blank / custom
Tone: Warm
Input word count: 57
Input character count: 371
Diagnosis tags: corporate_polish
Rewrite plan: Target corporate_polish while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 99%
Final selected AI-like signal: 0%
Change: -100 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. malformed:dangling_closing
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

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
Thursday workshop update: we're moving to Room 204 because of library maintenance.

We'll still start at 6:30pm and cover scholarship forms, supporting documents, and the application timeline.
```

## blank-03-internal-note

Scenario: Blank / custom
Tone: Direct
Input word count: 63
Input character count: 375
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 0%
Final selected AI-like signal: 15%
Change: -85 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. unsupported:let
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

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

## cover-01-operations-role

Scenario: Cover letter
Tone: Direct
Input word count: 89
Input character count: 601
Diagnosis tags: low_specificity, application_cliche
Rewrite plan: Target low_specificity, application_cliche while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 0%
Change: -100 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. unsupported:keeping
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

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
I am interested in the Operations Coordinator position.

In my last role, I prepared weekly partner updates, kept shared folders organized, and helped schedule meetings for a team of eight.
```

## cover-02-customer-success

Scenario: Cover letter
Tone: Warm
Input word count: 89
Input character count: 565
Diagnosis tags: low_specificity, application_cliche
Rewrite plan: Target low_specificity, application_cliche while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 16%
Change: -84 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:summarize action items unsupported:helping
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

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
I am excited to apply for the Customer Success Associate role.

In my current position, I respond to customer questions, summarize action items after calls, and update our help center articles when a pattern appears. They want someone who can answer customer questions clearly and write helpful follow-up notes.
```

## cover-03-admin-assistant

Scenario: Cover letter
Tone: Direct
Input word count: 79
Input character count: 592
Diagnosis tags: low_specificity
Rewrite plan: Target low_specificity while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: 0%
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
- three providers
- patient follow-up notes
- private information

Before:

```text
Please accept my application for the Administrative Assistant position. I am highly organized and detail oriented, and I have extensive experience supporting administrative workflows. At my previous clinic, I managed calendars for three providers, prepared patient follow-up notes, and handled private information carefully according to office procedures. I would welcome the opportunity to bring my skills to your organization.
```

After:

```text
I’m writing to apply for the Administrative Assistant position. I’m highly organized and detail-oriented, with a solid background in supporting administrative workflows. In my previous roles, I managed calendars for three providers and prepared patient follow-up notes. I’ve always handled private information carefully, following office procedures closely. I’m looking forward to the chance to bring my skills to your organization.
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
Repair candidate AI-like signal: 0%
Final selected AI-like signal: 0%
Change: -46 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:ava missing:logo color has not changed missing:desktop today
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

The homepage mockup is ready, but the mobile version still needs one spacing check. I can send desktop today and mobile by Wednesday morning.

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
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:po number missing:april 30
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
Final selected AI-like signal: 8%
Change: -92 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:grace missing:source file arrived late
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
Hi Grace,

The source file arrived late, so the report will not be ready by noon. I can send the clean version by 4pm Friday after one quality check.

The dashboard numbers are unchanged.
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
Repair candidate AI-like signal: 0%
Final selected AI-like signal: 0%
Change: -93 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:2pm launch check missing:2pm missing:before 2pm
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
Quick update: the payment flow passed the smoke test, the onboarding checklist is done, and the help article links are live. I am still reviewing the last three failed webhook events before the 2pm launch check.
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
Repair candidate AI-like signal: 0%
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
Nina owns the API fix, and Omar is responsible for the QA script. It’s essential that we have everything resolved before the Friday demo. I’m aware of the vendor API timeout issue following the 11am retry, and we should monitor this closely.
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
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: 1%
Change: -99 pts
Rejected candidate reasons: none
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

## draft-only-28-work-screenshots

Scenario: General reply
Tone: Direct
Input word count: 40
Input character count: 246
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 0%
Final selected AI-like signal: 0%
Change: -100 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:source file arrived late missing:target
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Expected facts:
- revised screenshots
- source file arrived late
- one quality check
- pricing table
- section three
- partner logo
- final slide
- 4pm Friday

Before:

```text
The revised screenshots are delayed because the updated design source file arrived late this morning. They still need one quality check, especially the pricing table in section three and the partner logo on the final slide. Target is 4pm Friday.
```

After:

```text
The revised screenshots are delayed because the updated design source file arrived late this morning. They still need one quality check, especially the pricing table in section three and the partner logo on the final slide.

Target is 4pm Friday.
```

## draft-only-29-work-board

Scenario: General reply
Tone: Warm
Input word count: 36
Input character count: 199
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 99%
Final selected AI-like signal: 0%
Change: -100 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:pdf unsupported:board just unsupported:finance
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Expected facts:
- board packet
- finance chart
- April revenue number
- May forecast
- 9am tomorrow
- finance confirms

Before:

```text
The board packet is ready except for the finance chart. The chart needs the April revenue number and the May forecast. I can send the final PDF by 9am tomorrow if finance confirms the numbers today.
```

After:

```text
The board packet is ready except for the finance chart. The chart needs the April revenue number and the May forecast.

I can send the final PDF by 9am tomorrow if finance confirms the numbers today.
```

## draft-only-30-work-handoff

Scenario: General reply
Tone: Direct
Input word count: 32
Input character count: 186
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
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Expected facts:
- authentication
- billing
- Search is not included
- rollback notes
- release doc
- Maya
- support FAQ review

Before:

```text
The handoff is complete for authentication and billing. Search is not included in this release. I added the rollback notes to the release doc and tagged Maya for the support FAQ review.
```

After:

```text
The handoff is complete for authentication and billing. Search is not included in this release.

I added the rollback notes to the release doc and tagged Maya for the support FAQ review.
```

## draft-only-31-work-meeting

Scenario: General reply
Tone: Warm
Input word count: 37
Input character count: 198
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 0%
Change: -100 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. unsupported:let
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Expected facts:
- 1:1
- Tuesday
- Wednesday
- client workshop
- 10am
- 2pm
- hiring plan feedback

Before:

```text
I need to move our 1:1 from Tuesday to Wednesday because the client workshop now overlaps. I can do Wednesday at 10am or 2pm. The hiring plan feedback is ready, so this is only a scheduling change.
```

After:

```text
I need to move our 1:1 from Tuesday to Wednesday because the client workshop now overlaps. I can do Wednesday at 10am or 2pm.

The hiring plan feedback is ready, so this is only a scheduling change.
```
