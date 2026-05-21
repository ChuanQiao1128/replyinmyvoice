# Scenario Evaluation Results

Date: 2026-05-21T10:00:01.951Z
Eval mode: smoke
Strategy: adaptive_rewrite_orchestrator
Naturalness threshold: 40%
Cases evaluated: 10
Draft-only cases: 0
Measured cases: 10
Long cases (300+ words): 0
Long customer-support cases (300+ words): 0
Average AI-like signal drop: 62 pts
Rewrite below 50% AI-like signal: 10/10
Final selected rewrites worse than draft: 0/10
Cases using targeted repair: 10/10
Rejected candidate events: 16
Fact preservation or unsupported-addition failures: 6
Customer-usable pass count: 4/10
Strict signal pass count: 4/10

Customer-usable pass requires: rewritten output exists, all expected facts are preserved, no unsupported names/dates/amounts/counts are added, no quality failure is raised, and the selected rewrite is not worse than the draft when scores are available.
Strict signal pass additionally requires scores available and: if the draft is above 40%, the final rewrite is at or below 40%; if the draft is already at or below 40%, the final rewrite does not raise the signal.

## rimv-email-001

Scenario: General reply
Tone: Warm
Input word count: 75
Input character count: 372
Diagnosis tags: low_specificity
Rewrite plan: Target low_specificity while preserving the user's facts.
Draft AI-like signal: 75%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 0%
Final selected AI-like signal: 0%
Change: -75 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. unsupported:dear ms alvarez
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Expected facts:
- Maya needs a signed permission slip by April 2.
- The $12 payment is still missing.
- Parent can send a new form or ask for one to be sent home.

Forbidden claims:
- Do not imply the parent is wrong or careless.

Before:

```text
Hello, I checked and I do not see it. You should send another form and payment so she can attend.
```

After:

```text
Hi Ms Alvarez,

Maya needs a signed permission slip by April 2.

The $12 payment is still missing. Parent can send a new form or ask for one to be sent home.
```

## rimv-email-002

Scenario: General reply
Tone: Warm
Input word count: 62
Input character count: 322
Diagnosis tags: low_specificity
Rewrite plan: Target low_specificity while preserving the user's facts.
Draft AI-like signal: 63%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 0%
Change: -63 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:new missing_locked:New deadline is Monday, March 18 at 8:00 AM missing_locked:New
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Expected facts:
- New deadline is Monday, March 18 at 8:00 AM.
- No late penalty if submitted by then.
- Standard late policy applies after that.
- Student should email if the file still will not upload.


Before:

```text
You should have planned ahead. The deadline was posted for two weeks. I can accept it late but the policy says points off.
```

After:

```text
Hi Mr. Chen,

New deadline is Monday, March 18 at 8:00 AM.

No late penalty if submitted by then. Standard late policy applies after that. Student should email if the file still will not upload.
```

## rimv-email-003

Scenario: General reply
Tone: Warm
Input word count: 67
Input character count: 359
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 19%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 1%
Final selected AI-like signal: 0%
Change: -19 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. unsupported:so missing_locked:Do not promise completion before June 1. Dropped a no-promise/no-guarantee constraint. | fallback: Deterministic facts-first fallback after escalation miss.
Facts preserved: no
Missing facts: Price is $1,800 per month for 25 seats plus $650 onboarding.; Earliest kickoff is May 28.; Full setup usually takes 5 business days after signature and data access.
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality failure state: yes
Quality failure reason: fact_check_failed
Customer-usable pass: no
Strict signal pass: no

Expected facts:
- Price is $1,800 per month for 25 seats plus $650 onboarding.
- Earliest kickoff is May 28.
- Full setup usually takes 5 business days after signature and data access.

Forbidden claims:
- Do not promise completion before June 1.

Before:

```text
Thanks for your time. Our platform is a great fit and we can absolutely get you onboarded before June 1. The pricing is attached again. Let me know if you want to sign today.
```

After:

```text

```

## rimv-email-004

Scenario: General reply
Tone: Direct
Input word count: 45
Input character count: 244
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 5%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 0%
Final selected AI-like signal: 0%
Change: -5 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:am may unsupported:here unsupported:crm | fallback: Deterministic facts-first fallback after escalation miss.
Facts preserved: no
Missing facts: Core dashboard is ready.; Q2 forecast tab depends on Finance confirming 7 accounts.; Need Finance confirmation by 9:30 AM May 7 to send by 11:00 AM.; Duplicate renewal rows caused the delay.
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality failure state: yes
Quality failure reason: fact_check_failed
Customer-usable pass: no
Strict signal pass: no

Expected facts:
- Core dashboard is ready.
- Q2 forecast tab depends on Finance confirming 7 accounts.
- Need Finance confirmation by 9:30 AM May 7 to send by 11:00 AM.
- Duplicate renewal rows caused the delay.


Before:

```text
I am still working on it and it is delayed because the export was wrong. I can maybe send it tomorrow morning.
```

After:

```text

```

## rimv-email-005

Scenario: Customer support
Tone: Warm
Input word count: 55
Input character count: 270
Diagnosis tags: low_specificity
Rewrite plan: Target low_specificity while preserving the user's facts.
Draft AI-like signal: 29%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 0%
Change: -29 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing_locked:Do not offer a refund. | fallback: Deterministic facts-first fallback after escalation miss.
Facts preserved: no
Missing facts: Order #R8142.; Two of six mugs broken.; Need photos of mugs and packaging.; Express replacement for 2 mugs can be sent at no extra cost if photos arrive by 2:00 PM April 12.
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality failure state: yes
Quality failure reason: fact_check_failed
Customer-usable pass: no
Strict signal pass: no

Expected facts:
- Order #R8142.
- Two of six mugs broken.
- Need photos of mugs and packaging.
- Express replacement for 2 mugs can be sent at no extra cost if photos arrive by 2:00 PM April 12.

Forbidden claims:
- Do not offer a refund.

Before:

```text
Sorry about that. We can replace the mugs. Please send photos and we will process it.
```

After:

```text

```

## rimv-email-006

Scenario: Customer support
Tone: Warm
Input word count: 46
Input character count: 258
Diagnosis tags: low_specificity
Rewrite plan: Target low_specificity while preserving the user's facts.
Draft AI-like signal: 73%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 0%
Final selected AI-like signal: 1%
Change: -72 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:$19 missing:$49 unsupported:reach missing_locked:Basic is $19 monthly, Team is $49 monthly.
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Expected facts:
- Upgrade date May 10.
- Billing cycle May 1 to May 31.
- Basic is $19 monthly, Team is $49 monthly, invoice total is $27.43.
- Explain that it is a prorated difference, not a random fee.


Before:

```text
The amount is prorated because you upgraded mid-cycle. The system calculated it automatically and it is correct.
```

After:

```text
Hi Upgrade,

Upgrade date May 10.

Billing cycle May 1 to May 31. Basic is $19 monthly, Team is $49 monthly, invoice total is $27.43. Explain that it is a prorated difference, not a random fee.
```

## rimv-email-007

Scenario: Customer support
Tone: Warm
Input word count: 47
Input character count: 268
Diagnosis tags: low_specificity
Rewrite plan: Target low_specificity while preserving the user's facts.
Draft AI-like signal: 86%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 0%
Final selected AI-like signal: 0%
Change: -86 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:original missing_locked:Original application date March 3 missing_locked:Updated documents due by April 2 missing_locked:Original Dropped a no-promise/no-guarantee constraint.
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Expected facts:
- Can reopen once within 30 days.
- Original application date March 3.
- Updated documents due by April 2.
- Acceptable documents are recent payslip or unemployment notice.
- No guarantee of approval.


Before:

```text
You were marked ineligible based on the original application. You can send more documents and we can review them.
```

After:

```text
Hi Original,

Can reopen once within 30 days.

Original application date March 3. Updated documents due by April 2. Acceptable documents are recent payslip or unemployment notice. Reopening does not guarantee approval.
```

## rimv-email-008

Scenario: General reply
Tone: Warm
Input word count: 46
Input character count: 249
Diagnosis tags: none
Rewrite plan: Make the draft more natural while preserving the user's facts.
Draft AI-like signal: 86%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 100%
Final selected AI-like signal: 2%
Change: -84 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. missing:offer tuesday missing_locked:Offer Tuesday | fallback: Deterministic facts-first fallback after escalation miss.
Facts preserved: no
Missing facts: Quiz score was 72 percent, not a failure.; Missing two journal entries.; Offer Tuesday, March 26 at 4:15 PM or Friday, March 29 at 8:05 AM.; Ask which time works.
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality failure state: yes
Quality failure reason: fact_check_failed
Customer-usable pass: no
Strict signal pass: no

Expected facts:
- Quiz score was 72 percent, not a failure.
- Missing two journal entries.
- Offer Tuesday, March 26 at 4:15 PM or Friday, March 29 at 8:05 AM.
- Ask which time works.


Before:

```text
Yes, we can meet. Liam did not fail, but he is missing some assignments. Tuesday after school works.
```

After:

```text

```

## rimv-email-009

Scenario: Customer support
Tone: Warm
Input word count: 38
Input character count: 211
Diagnosis tags: low_specificity
Rewrite plan: Target low_specificity while preserving the user's facts.
Draft AI-like signal: 100%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 0%
Final selected AI-like signal: 0%
Change: -100 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. unsupported:only missing_locked:Do not cancel or downgrade without confirmation. | fallback: Deterministic facts-first fallback after escalation miss.
Facts preserved: no
Missing facts: Renewal date June 3.; Current plan is $89 monthly.; Solo plan is $29 monthly.; Do not cancel or downgrade without confirmation.; Ask whether they want cancellation at renewal or downgrade.
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality failure state: yes
Quality failure reason: fact_check_failed
Customer-usable pass: no
Strict signal pass: no

Expected facts:
- Renewal date June 3.
- Current plan is $89 monthly.
- Solo plan is $29 monthly.
- Do not cancel or downgrade without confirmation.
- Ask whether they want cancellation at renewal or downgrade.


Before:

```text
I can cancel it for you, but we also have a cheaper plan. Would you like to downgrade instead?
```

After:

```text

```

## rimv-email-010

Scenario: Customer support
Tone: Warm
Input word count: 54
Input character count: 267
Diagnosis tags: low_specificity
Rewrite plan: Target low_specificity while preserving the user's facts.
Draft AI-like signal: 85%
First candidate AI-like signal: unavailable
Repair candidate AI-like signal: 0%
Final selected AI-like signal: 0%
Change: -85 pts
Rejected candidate reasons: repair: Strong-model escalation fact and naturalness gate. unsupported:send unsupported:contact | fallback: Deterministic facts-first fallback after escalation miss.
Facts preserved: no
Missing facts: Cannot switch email without verification.; Verification options are last four digits of card plus most recent invoice number, or workspace admin Priya Nair can update it.
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality failure state: yes
Quality failure reason: fact_check_failed
Customer-usable pass: no
Strict signal pass: no

Expected facts:
- Cannot switch email without verification.
- Verification options are last four digits of card plus most recent invoice number, or workspace admin Priya Nair can update it.

Forbidden claims:
- Do not promise access before 5 PM.

Before:

```text
We cannot change the email without verification. Please send proof that you own the account and we can help.
```

After:

```text

```
