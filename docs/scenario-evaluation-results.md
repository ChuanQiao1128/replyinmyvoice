# Scenario Evaluation Results

Date: 2026-05-22T01:13:49.219Z
Eval mode: focused
Strategy: adaptive_rewrite_orchestrator
Naturalness threshold: 40%
Cases evaluated: 30
Draft-only cases: 24
Measured cases: 0
Long cases (300+ words): 1
Long customer-support cases (300+ words): 0
Average AI-like signal drop: unavailable
Rewrite below 50% AI-like signal: 0/0
Final selected rewrites worse than draft: 0/0
Cases using targeted repair: 0/30
Rejected candidate events: 0
Fact preservation or unsupported-addition failures: 30
Customer-usable pass count: 0/30
Strict signal pass count: 0/30

Customer-usable pass requires: rewritten output exists, all expected facts are preserved, no unsupported names/dates/amounts/counts are added, no quality failure is raised, and the selected rewrite is not worse than the draft when scores are available.
Strict signal pass additionally requires scores available and: if the draft is above 40%, the final rewrite is at or below 40%; if the draft is already at or below 40%, the final rewrite does not raise the signal.

## draft-only-01-teacher-jordan

Scenario: General reply
Tone: Warm
Input word count: 64
Input character count: 387
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: unavailable
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: unavailable
Change: unavailable
Rejected candidate reasons: none
Facts preserved: no
Missing facts: Monica; Jordan; reading response; vocabulary practice; Friday; end of this week; partial credit; Ms. Carter
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality failure state: yes
Quality failure reason: signal_unavailable
Customer-usable pass: no
Strict signal pass: no

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

```

## draft-only-01b-teacher-jordan-long-polished

Scenario: General reply
Tone: Warm
Input word count: 253
Input character count: 1490
Diagnosis tags: stock_opening
Rewrite plan: Target stock_opening while preserving the user's facts.
Draft AI-like signal: unavailable
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: unavailable
Change: unavailable
Rejected candidate reasons: none
Facts preserved: no
Missing facts: Monica; Jordan; three assignments; past two weeks; reading response; vocabulary practice; short reflection paragraph; last Friday; class discussions; this Friday at 5 p.m.; partial credit; does not need to redo; Tuesday; Thursday; Ms. Carter
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality failure state: yes
Quality failure reason: signal_unavailable
Customer-usable pass: no
Strict signal pass: no

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

```

## draft-only-02-teacher-kai

Scenario: General reply
Tone: Direct
Input word count: 50
Input character count: 284
Diagnosis tags: policy_memo_voice
Rewrite plan: Target policy_memo_voice while preserving the user's facts.
Draft AI-like signal: unavailable
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: unavailable
Change: unavailable
Rejected candidate reasons: none
Facts preserved: no
Missing facts: Lena; Kai; two participation activities; one exit ticket; course policy; before class tomorrow; not promising
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality failure state: yes
Quality failure reason: signal_unavailable
Customer-usable pass: no
Strict signal pass: no

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

```

## draft-only-03-teacher-amelia

Scenario: General reply
Tone: Warm
Input word count: 41
Input character count: 219
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: unavailable
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: unavailable
Change: unavailable
Rejected candidate reasons: none
Facts preserved: no
Missing facts: Mr. Ortiz; Amelia; lab notes; reflection paragraph; Tuesday; Thursday; late but complete
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality failure state: yes
Quality failure reason: signal_unavailable
Customer-usable pass: no
Strict signal pass: no

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

```

## draft-only-04-teacher-ravi

Scenario: General reply
Tone: Direct
Input word count: 38
Input character count: 211
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: unavailable
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: unavailable
Change: unavailable
Rejected candidate reasons: none
Facts preserved: no
Missing facts: Priya; Ravi; quiz; Monday; Wednesday at 8:15am; Friday after school; cannot enter the quiz score
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality failure state: yes
Quality failure reason: signal_unavailable
Customer-usable pass: no
Strict signal pass: no

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

```

## draft-only-05-teacher-sam

Scenario: General reply
Tone: Warm
Input word count: 38
Input character count: 214
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: unavailable
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: unavailable
Change: unavailable
Rejected candidate reasons: none
Facts preserved: no
Missing facts: Dana; Sam; bibliography; three sources; Friday; final version; May 22
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality failure state: yes
Quality failure reason: signal_unavailable
Customer-usable pass: no
Strict signal pass: no

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

```

## draft-only-06-teacher-noah

Scenario: General reply
Tone: Direct
Input word count: 37
Input character count: 189
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: unavailable
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: unavailable
Change: unavailable
Rejected candidate reasons: none
Facts preserved: no
Missing facts: Ms. Nguyen; Noah; worksheet; graph; Canvas; 6pm tonight; only the graph file
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality failure state: yes
Quality failure reason: signal_unavailable
Customer-usable pass: no
Strict signal pass: no

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

```

## draft-only-07-teacher-maya

Scenario: General reply
Tone: Warm
Input word count: 37
Input character count: 203
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: unavailable
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: unavailable
Change: unavailable
Rejected candidate reasons: none
Facts preserved: no
Missing facts: Alex; Maya; science fair group; permission slip; Thursday morning; Room 18; project idea
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality failure state: yes
Quality failure reason: signal_unavailable
Customer-usable pass: no
Strict signal pass: no

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

```

## draft-only-08-teacher-omar

Scenario: General reply
Tone: Direct
Input word count: 37
Input character count: 199
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: unavailable
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: unavailable
Change: unavailable
Rejected candidate reasons: none
Facts preserved: no
Missing facts: Jordan; Omar; reading log; March 14; half credit; quiz retake; scheduled with me
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality failure state: yes
Quality failure reason: signal_unavailable
Customer-usable pass: no
Strict signal pass: no

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

```

## draft-only-09-support-tax

Scenario: General reply
Tone: Warm
Input word count: 34
Input character count: 202
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: unavailable
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: unavailable
Change: unavailable
Rejected candidate reasons: none
Facts preserved: no
Missing facts: Sarah; tax line; Australia; base subscription; June 1; May invoice; not be recalculated
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality failure state: yes
Quality failure reason: signal_unavailable
Customer-usable pass: no
Strict signal pass: no

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

```

## draft-only-10-support-refund

Scenario: General reply
Tone: Direct
Input word count: 29
Input character count: 160
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: unavailable
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: unavailable
Change: unavailable
Rejected candidate reasons: none
Facts preserved: no
Missing facts: Eli; refund window; May 10; account credit; manager approval; cannot promise
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality failure state: yes
Quality failure reason: signal_unavailable
Customer-usable pass: no
Strict signal pass: no

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

```

## draft-only-11-support-export

Scenario: General reply
Tone: Warm
Input word count: 45
Input character count: 251
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: unavailable
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: unavailable
Change: unavailable
Rejected candidate reasons: none
Facts preserved: no
Missing facts: Mina; April CSV export; custom tags column; Northeast region; data is still safe; Monday at 10am
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality failure state: yes
Quality failure reason: signal_unavailable
Customer-usable pass: no
Strict signal pass: no

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

```

## draft-only-12-support-seat-count

Scenario: General reply
Tone: Direct
Input word count: 38
Input character count: 219
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: unavailable
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: unavailable
Change: unavailable
Rejected candidate reasons: none
Facts preserved: no
Missing facts: Priya; 18 active seats; 15 regular seats; NZD $126; three temporary contractors; May; base plan did not change
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality failure state: yes
Quality failure reason: signal_unavailable
Customer-usable pass: no
Strict signal pass: no

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

```

## draft-only-13-support-login

Scenario: General reply
Tone: Warm
Input word count: 42
Input character count: 255
Diagnosis tags: low_specificity
Rewrite plan: Target low_specificity while preserving the user's facts.
Draft AI-like signal: unavailable
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: unavailable
Change: unavailable
Rejected candidate reasons: none
Facts preserved: no
Missing facts: Mina; mina@northstar.example; old pilot workspace; workspace association issue; resent the invite twice; account link
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality failure state: yes
Quality failure reason: signal_unavailable
Customer-usable pass: no
Strict signal pass: no

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

```

## reply-01-teacher-extension

Scenario: Email or message reply
Tone: Warm
Input word count: 99
Input character count: 596
Diagnosis tags: stock_opening, corporate_polish, policy_memo_voice
Rewrite plan: Target stock_opening, corporate_polish, policy_memo_voice while preserving the user's facts.
Draft AI-like signal: unavailable
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: unavailable
Change: unavailable
Rejected candidate reasons: none
Facts preserved: no
Missing facts: family issue; before class tomorrow; course policy
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality failure state: yes
Quality failure reason: signal_unavailable
Customer-usable pass: no
Strict signal pass: no

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

```

## reply-03-parent-question

Scenario: Email or message reply
Tone: Direct
Input word count: 70
Input character count: 420
Diagnosis tags: stock_opening, corporate_polish, low_specificity
Rewrite plan: Target stock_opening, corporate_polish, low_specificity while preserving the user's facts.
Draft AI-like signal: unavailable
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: unavailable
Change: unavailable
Rejected candidate reasons: none
Facts preserved: no
Missing facts: Kai; two missing participation activities; one missing exit ticket
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality failure state: yes
Quality failure reason: signal_unavailable
Customer-usable pass: no
Strict signal pass: no

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

## work-01-design-delay

Scenario: Work update
Tone: Direct
Input word count: 70
Input character count: 406
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: unavailable
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: unavailable
Change: unavailable
Rejected candidate reasons: none
Facts preserved: no
Missing facts: source file arrived late; one quality check; 4pm Friday
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality failure state: yes
Quality failure reason: signal_unavailable
Customer-usable pass: no
Strict signal pass: no

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

```

## work-02-launch-risk

Scenario: Work update
Tone: Direct
Input word count: 69
Input character count: 384
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: unavailable
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: unavailable
Change: unavailable
Rejected candidate reasons: none
Facts preserved: no
Missing facts: payment flow; last three failed events; 2pm launch check
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality failure state: yes
Quality failure reason: signal_unavailable
Customer-usable pass: no
Strict signal pass: no

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

```

## work-03-research-summary

Scenario: Work update
Tone: Warm
Input word count: 73
Input character count: 436
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: unavailable
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: unavailable
Change: unavailable
Rejected candidate reasons: none
Facts preserved: no
Missing facts: six interviews; four teachers; two asked; Wednesday
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality failure state: yes
Quality failure reason: signal_unavailable
Customer-usable pass: no
Strict signal pass: no

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

## work-04-long-launch-readiness

Scenario: Work update
Tone: Direct
Input word count: 376
Input character count: 2178
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: unavailable
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: unavailable
Change: unavailable
Rejected candidate reasons: none
Facts preserved: no
Missing facts: payment flow; onboarding checklist; help article links; last three failed events; 2pm launch check
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality failure state: yes
Quality failure reason: signal_unavailable
Customer-usable pass: no
Strict signal pass: no

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

```

## draft-only-14-support-plan-change

Scenario: General reply
Tone: Direct
Input word count: 44
Input character count: 250
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: unavailable
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: unavailable
Change: unavailable
Rejected candidate reasons: none
Facts preserved: no
Missing facts: Arun; Starter plan credit; Team plan charge; May 3; proration; not a duplicate charge; invoice screenshot
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality failure state: yes
Quality failure reason: signal_unavailable
Customer-usable pass: no
Strict signal pass: no

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

```

## draft-only-15-support-notifications

Scenario: General reply
Tone: Warm
Input word count: 43
Input character count: 246
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: unavailable
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: unavailable
Change: unavailable
Rejected candidate reasons: none
Facts preserved: no
Missing facts: Claire; ticket #4821; duplicate notifications; delivery logs; root cause is not confirmed; before noon; pause the campaign
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality failure state: yes
Quality failure reason: signal_unavailable
Customer-usable pass: no
Strict signal pass: no

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

```

## draft-only-16-support-import

Scenario: General reply
Tone: Direct
Input word count: 35
Input character count: 185
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: unavailable
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: unavailable
Change: unavailable
Rejected candidate reasons: none
Facts preserved: no
Missing facts: Omar; row 14; invalid date format; Rows 1 through 13; not saved; upload the file again; Do not delete
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality failure state: yes
Quality failure reason: signal_unavailable
Customer-usable pass: no
Strict signal pass: no

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

```

## draft-only-17-sales-renewal

Scenario: General reply
Tone: Warm
Input word count: 45
Input character count: 247
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: unavailable
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: unavailable
Change: unavailable
Rejected candidate reasons: none
Facts preserved: no
Missing facts: Jordan; renewal proposal; two plan options; finance thread; two other vendors; first week of June
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality failure state: yes
Quality failure reason: signal_unavailable
Customer-usable pass: no
Strict signal pass: no

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

```

## draft-only-18-sales-demo

Scenario: General reply
Tone: Direct
Input word count: 35
Input character count: 188
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: unavailable
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: unavailable
Change: unavailable
Rejected candidate reasons: none
Facts preserved: no
Missing facts: Leah; Thursday at 3pm; reporting; team templates; approval workflow; will not include pricing
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality failure state: yes
Quality failure reason: signal_unavailable
Customer-usable pass: no
Strict signal pass: no

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

```

## draft-only-19-sales-proposal

Scenario: General reply
Tone: Warm
Input word count: 40
Input character count: 242
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: unavailable
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: unavailable
Change: unavailable
Rejected candidate reasons: none
Facts preserved: no
Missing facts: Mateo; May 12; section three; pricing language; section five; rollout notes; Friday; legal team
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality failure state: yes
Quality failure reason: signal_unavailable
Customer-usable pass: no
Strict signal pass: no

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

```

## draft-only-20-sales-checkin

Scenario: General reply
Tone: Direct
Input word count: 40
Input character count: 222
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: unavailable
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: unavailable
Change: unavailable
Rejected candidate reasons: none
Facts preserved: no
Missing facts: Nora; not push for a decision; Vendor A; Vendor B; security questionnaire; today; next Tuesday
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality failure state: yes
Quality failure reason: signal_unavailable
Customer-usable pass: no
Strict signal pass: no

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

```

## draft-only-21-sales-expansion

Scenario: General reply
Tone: Warm
Input word count: 36
Input character count: 212
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: unavailable
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: unavailable
Change: unavailable
Rejected candidate reasons: none
Facts preserved: no
Missing facts: Devon; 12 additional seats; July 1; does not include the analytics add-on; second quote; manager approves
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality failure state: yes
Quality failure reason: signal_unavailable
Customer-usable pass: no
Strict signal pass: no

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

```

## draft-only-35-cover-program

Scenario: General reply
Tone: Warm
Input word count: 45
Input character count: 282
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: unavailable
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: unavailable
Change: unavailable
Rejected candidate reasons: none
Facts preserved: no
Missing facts: Program Manager; 32 volunteers; monthly partner updates; weekend workshop schedules; attendance numbers; grant reports; education access
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality failure state: yes
Quality failure reason: signal_unavailable
Customer-usable pass: no
Strict signal pass: no

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

```

## draft-only-36-cover-support

Scenario: General reply
Tone: Direct
Input word count: 42
Input character count: 251
Diagnosis tags: low_specificity
Rewrite plan: Target low_specificity while preserving the user's facts.
Draft AI-like signal: unavailable
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: unavailable
Final selected AI-like signal: unavailable
Change: unavailable
Rejected candidate reasons: none
Facts preserved: no
Missing facts: Support Specialist; email and chat; recurring issues; product team; help center articles; do not make me sound senior
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality failure state: yes
Quality failure reason: signal_unavailable
Customer-usable pass: no
Strict signal pass: no

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

```
