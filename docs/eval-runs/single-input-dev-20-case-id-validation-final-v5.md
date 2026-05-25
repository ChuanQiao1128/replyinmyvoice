# Scenario Evaluation Results

Date: 2026-05-25T12:47:49.977Z
Eval mode: focused
Strategy: adaptive_rewrite_orchestrator
Naturalness threshold: 40%
Cases evaluated: 20
Selected case ids: rewrite-draft-001, rewrite-draft-002, rewrite-draft-003, rewrite-draft-004, rewrite-draft-005, rewrite-draft-006, rewrite-draft-007, rewrite-draft-008, rewrite-draft-009, rewrite-draft-010, rewrite-draft-011, rewrite-draft-012, rewrite-draft-013, rewrite-draft-014, rewrite-draft-015, rewrite-draft-016, rewrite-draft-017, rewrite-draft-018, rewrite-draft-019, rewrite-draft-020
Draft-only cases: 20
Measured cases: 20
Long cases (300+ words): 0
Long customer-support cases (300+ words): 0
Average AI-like signal drop: 0 pts
Rewrite below 50% AI-like signal: 20/20
Final selected rewrites worse than draft: 0/20
Quality-score measured cases: 20
Quality-score improved cases: 20/20
Quality regressions: 0/20
Average quality-score delta: 1.4 pts
Cases using targeted repair: 0/20
Rejected candidate events: 0
Fact preservation or unsupported-addition failures: 0
Customer-usable pass count: 20/20
Strict signal pass count: 20/20

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
Quality notes: The rewrite improves scannability with line breaks and a clear structure. | It maintains a warm, empathetic tone while keeping the deadline and fee boundaries strict. | The next step is made obvious with a direct request, and unnecessary details (like offering to send a form) are streamlined. | The original draft was somewhat cluttered and less scannable but still contained all necessary information.
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Fact diagnostics:

Extracted facts:
```json
{
  "recipient_name": "Jamie",
  "sender_name_or_role": "",
  "people_mentioned": [
    "Jamie",
    "Maya"
  ],
  "main_purpose": "Notify Jamie that Maya's signed permission slip and $12 payment for the April 9 science museum field trip are missing, and request both by the April 2 deadline.",
  "key_facts": [
    "Checked Maya's folder basket, payment envelope, and teacher log on March 28.",
    "Field trip is on April 9 to the science museum.",
    "Record identifier is FieldTrip-4A-09.",
    "Maya's signed permission slip is not on file.",
    "$12 trip payment is not recorded.",
    "Front office has spare blank forms.",
    "Sender can send a blank form home through the office today.",
    "Deadline to add Maya to the attendance list is April 2.",
    "Cannot add Maya to the attendance list after April 2 if signed slip and $12 are still missing.",
    "Jamie remembers signing the original form.",
    "Sender needs the record to be complete before the trip."
  ],
  "required_actions": [
    "Send a new signed permission slip with the $12 trip fee."
  ],
  "deadlines": [
    "April 2"
  ],
  "dates_times": [
    "March 28",
    "April 9",
    "April 2"
  ],
  "positive_notes": [],
  "concerns": [
    "Frustration because Jamie remembers signing the original form."
  ],
  "policies_or_conditions": [
    "Cannot add Maya to the attendance list after April 2 if signed slip and $12 are still missing."
  ],
  "available_support": [
    "Front office has spare blank forms.",
    "Sender can send a blank form home through the office today."
  ],
  "clarifications": [],
  "facts_that_must_not_change": [
    "I do not have Maya's signed permission slip on file.",
    "I also do not have the $12 trip payment recorded.",
    "I cannot add Maya to the attendance list after April 2 if the signed slip and $12 are still missing.",
    "I need the record to be complete before the trip."
  ],
  "sensitive_points": [
    "Jamie remembers signing the original form, but the record is incomplete."
  ],
  "original_tone": "Informative and slightly apologetic, acknowledging frustration."
}
```

Reviewed facts:
```json
{
  "recipient_name": "Jamie",
  "sender_name_or_role": "",
  "people_mentioned": [
    "Jamie",
    "Maya"
  ],
  "main_purpose": "Notify Jamie that Maya's signed permission slip and $12 payment for the April 9 science museum field trip are missing, and request both by the April 2 deadline.",
  "key_facts": [
    "Checked Maya's folder basket, payment envelope, and teacher log on March 28.",
    "Field trip is on April 9 to the science museum.",
    "Record identifier is FieldTrip-4A-09.",
    "Maya's signed permission slip is not on file.",
    "Front office has spare blank forms.",
    "Jamie remembers signing the original form.",
    "Sender needs the record to be complete before the trip.",
    "April 9 science museum field trip",
    "permission slip",
    "FieldTrip-4A-09"
  ],
  "required_actions": [
    "Send a new signed permission slip with the $12 trip fee."
  ],
  "deadlines": [
    "April 2"
  ],
  "dates_times": [
    "March 28",
    "April 9",
    "April 2"
  ],
  "positive_notes": [],
  "concerns": [
    "Frustration because Jamie remembers signing the original form."
  ],
  "policies_or_conditions": [
    "Right now I do not have Maya's signed permission slip on file, and I also do not have the $12 trip payment recorded",
    "The deadline I have to work with is April 2, and I cannot add Maya to the attendance list after April 2 if the signed slip and $12 are still missing",
    "I know this is a little frustrating because you remember signing the original form, but I need the record to be complete before the trip"
  ],
  "available_support": [
    "Front office has spare blank forms."
  ],
  "clarifications": [],
  "facts_that_must_not_change": [
    "I do not have Maya's signed permission slip on file.",
    "I also do not have the $12 trip payment recorded.",
    "I cannot add Maya to the attendance list after April 2 if the signed slip and $12 are still missing.",
    "I need the record to be complete before the trip.",
    "Jamie",
    "April 9 science museum field trip",
    "permission slip",
    "FieldTrip-4A-09",
    "$12",
    "March 28",
    "April 9",
    "April 2",
    "Maya",
    "Right now I do not have Maya's signed permission slip on file, and I also do not have the $12 trip payment recorded",
    "The deadline I have to work with is April 2, and I cannot add Maya to the attendance list after April 2 if the signed slip and $12 are still missing",
    "I know this is a little frustrating because you remember signing the original form, but I need the record to be complete before the trip"
  ],
  "sensitive_points": [
    "Jamie remembers signing the original form, but the record is incomplete."
  ],
  "original_tone": "Informative and slightly apologetic, acknowledging frustration."
}
```

Added anchors:
```json
[
  {
    "id": "person:jamie",
    "text": "Jamie",
    "normalizedText": "jamie",
    "category": "person",
    "source": "draft",
    "required": true
  },
  {
    "id": "quoted_phrase:april 9 science museum field trip",
    "text": "April 9 science museum field trip",
    "normalizedText": "april 9 science museum field trip",
    "category": "quoted_phrase",
    "source": "draft",
    "required": true
  },
  {
    "id": "task:permission slip",
    "text": "permission slip",
    "normalizedText": "permission slip",
    "category": "task",
    "source": "draft",
    "required": true
  },
  {
    "id": "quoted_phrase:fieldtrip-4a-09",
    "text": "FieldTrip-4A-09",
    "normalizedText": "fieldtrip-4a-09",
    "category": "quoted_phrase",
    "source": "draft",
    "required": true
  },
  {
    "id": "amount:$12",
    "text": "$12",
    "normalizedText": "$12",
    "category": "amount",
    "source": "draft",
    "required": true
  },
  {
    "id": "date:march 28",
    "text": "March 28",
    "normalizedText": "march 28",
    "category": "date",
    "source": "draft",
    "required": true
  },
  {
    "id": "date:april 9",
    "text": "April 9",
    "normalizedText": "april 9",
    "category": "date",
    "source": "draft",
    "required": true
  },
  {
    "id": "date:april 2",
    "text": "April 2",
    "normalizedText": "april 2",
    "category": "date",
    "source": "draft",
    "required": true
  },
  {
    "id": "person:maya",
    "text": "Maya",
    "normalizedText": "maya",
    "category": "person",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:right now i do not have maya's signed permission slip on file, and i also do not have the $12 trip payment recorded",
    "text": "Right now I do not have Maya's signed permission slip on file, and I also do not have the $12 trip payment recorded",
    "normalizedText": "right now i do not have maya's signed permission slip on file, and i also do not have the $12 trip payment recorded",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:the deadline i have to work with is april 2, and i cannot add maya to the attendance list after april 2 if the signed slip and $12 are still missing",
    "text": "The deadline I have to work with is April 2, and I cannot add Maya to the attendance list after April 2 if the signed slip and $12 are still missing",
    "normalizedText": "the deadline i have to work with is april 2, and i cannot add maya to the attendance list after april 2 if the signed slip and $12 are still missing",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:i know this is a little frustrating because you remember signing the original form, but i need the record to be complete before the trip",
    "text": "I know this is a little frustrating because you remember signing the original form, but I need the record to be complete before the trip",
    "normalizedText": "i know this is a little frustrating because you remember signing the original form, but i need the record to be complete before the trip",
    "category": "constraint",
    "source": "draft",
    "required": true
  }
]
```

Rejected extracted facts:
```json
[
  {
    "text": "$12 trip payment is not recorded.",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Sender can send a blank form home through the office today.",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Deadline to add Maya to the attendance list is April 2.",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Cannot add Maya to the attendance list after April 2 if signed slip and $12 are still missing.",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Cannot add Maya to the attendance list after April 2 if signed slip and $12 are still missing.",
    "field": "policies_or_conditions",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Sender can send a blank form home through the office today.",
    "field": "available_support",
    "reason": "No source evidence for hard fact."
  }
]
```

Attempt ledger:
```json
[]
```

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

I checked Maya's folder basket, payment envelope, and teacher log on March 28 for the April 9 science museum field trip (FieldTrip-4A-09). Right now I do not have her signed permission slip on file, and I also do not have the $12 trip payment recorded. I know this is frustrating because you remember signing the original form, but I need the record to be complete before the trip.

The deadline is April 2, and I cannot add Maya to the attendance list after April 2 if the signed slip and $12 are still missing. The front office has spare blank forms if you need one. Please send a new signed permission slip with the $12 trip fee by then.

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
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality score before: 8
Quality score after: 9
Quality score delta: 1 pts
Quality regression: no
Quality notes: The rewritten text improves readability by breaking into short paragraphs and using a conversational em dash, while preserving all facts and boundaries precisely.
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Fact diagnostics:

Extracted facts:
```json
{
  "recipient_name": "Lena",
  "sender_name_or_role": "",
  "people_mentioned": [
    "Lena"
  ],
  "main_purpose": "Addressing a damaged replacement mug from order R4821 and initiating a no-cost replacement.",
  "key_facts": [
    "Photo of cracked replacement mug from order R4821 received.",
    "Order notes checked this morning.",
    "Replacement was marked delivered on May 6.",
    "Photo shows damage to the replacement mug.",
    "Matching saucer looks fine.",
    "Support policy allows one no-cost replacement for the mug after delivery address confirmation.",
    "Original refund window closed on April 30.",
    "Current delivery address needed by Friday at 3 p.m. to queue replacement mug.",
    "If address has changed, new address must be provided in writing before shipment creation."
  ],
  "required_actions": [
    "Lena to reply with current delivery address by Friday at 3 p.m.",
    "If address changed, provide new address in writing."
  ],
  "deadlines": [
    "Friday at 3 p.m."
  ],
  "dates_times": [
    "May 6",
    "April 30",
    "Friday at 3 p.m."
  ],
  "positive_notes": [
    "Matching saucer looks fine."
  ],
  "concerns": [
    "Cracked replacement mug."
  ],
  "policies_or_conditions": [
    "Support policy allows one no-cost replacement for the mug after delivery address confirmation.",
    "Original refund window closed on April 30."
  ],
  "available_support": [
    "One no-cost replacement mug after address confirmation."
  ],
  "clarifications": [],
  "facts_that_must_not_change": [
    "I cannot refund the full order from this ticket because the original refund window closed on April 30.",
    "If the address has changed, I need the new address in writing before I create the shipment."
  ],
  "sensitive_points": [],
  "original_tone": "Professional and helpful."
}
```

Reviewed facts:
```json
{
  "recipient_name": "Lena",
  "sender_name_or_role": "",
  "people_mentioned": [
    "Lena"
  ],
  "main_purpose": "Addressing a damaged replacement mug from order R4821 and initiating a no-cost replacement.",
  "key_facts": [
    "Photo of cracked replacement mug from order R4821 received.",
    "Order notes checked this morning.",
    "Replacement was marked delivered on May 6.",
    "Photo shows damage to the replacement mug.",
    "Matching saucer looks fine.",
    "Original refund window closed on April 30.",
    "If address has changed, new address must be provided in writing before shipment creation.",
    "order R4821"
  ],
  "required_actions": [
    "If address changed, provide new address in writing."
  ],
  "deadlines": [
    "Friday at 3 p.m.",
    "3 p.m.",
    "by Friday"
  ],
  "dates_times": [
    "May 6",
    "April 30",
    "Friday at 3 p.m.",
    "Friday"
  ],
  "positive_notes": [
    "Matching saucer looks fine."
  ],
  "concerns": [
    "Cracked replacement mug."
  ],
  "policies_or_conditions": [
    "Original refund window closed on April 30.",
    "refund window",
    "Our support policy lets me send one no-cost replacement for the mug after I confirm the delivery address",
    "I cannot refund the full order from this ticket because the original refund window closed on April 30",
    "Please reply with the current delivery address by Friday at 3 p.m., and I will queue the replacement mug",
    "If the address has changed, I need the new address in writing before I create the shipment"
  ],
  "available_support": [],
  "clarifications": [],
  "facts_that_must_not_change": [
    "I cannot refund the full order from this ticket because the original refund window closed on April 30.",
    "If the address has changed, I need the new address in writing before I create the shipment.",
    "Lena",
    "refund window",
    "order R4821",
    "May 6",
    "April 30",
    "Friday",
    "3 p.m.",
    "by Friday",
    "Our support policy lets me send one no-cost replacement for the mug after I confirm the delivery address",
    "I cannot refund the full order from this ticket because the original refund window closed on April 30",
    "Please reply with the current delivery address by Friday at 3 p.m., and I will queue the replacement mug",
    "If the address has changed, I need the new address in writing before I create the shipment"
  ],
  "sensitive_points": [],
  "original_tone": "Professional and helpful."
}
```

Added anchors:
```json
[
  {
    "id": "person:lena",
    "text": "Lena",
    "normalizedText": "lena",
    "category": "person",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:refund window",
    "text": "refund window",
    "normalizedText": "refund window",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "quoted_phrase:order r4821",
    "text": "order R4821",
    "normalizedText": "order r4821",
    "category": "quoted_phrase",
    "source": "draft",
    "required": true
  },
  {
    "id": "date:may 6",
    "text": "May 6",
    "normalizedText": "may 6",
    "category": "date",
    "source": "draft",
    "required": true
  },
  {
    "id": "date:april 30",
    "text": "April 30",
    "normalizedText": "april 30",
    "category": "date",
    "source": "draft",
    "required": true
  },
  {
    "id": "date:friday",
    "text": "Friday",
    "normalizedText": "friday",
    "category": "date",
    "source": "draft",
    "required": true
  },
  {
    "id": "deadline:3 p.m.",
    "text": "3 p.m.",
    "normalizedText": "3 p.m.",
    "category": "deadline",
    "source": "draft",
    "required": true
  },
  {
    "id": "deadline:by friday",
    "text": "by Friday",
    "normalizedText": "by friday",
    "category": "deadline",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:our support policy lets me send one no-cost replacement for the mug after i confirm the delivery address",
    "text": "Our support policy lets me send one no-cost replacement for the mug after I confirm the delivery address",
    "normalizedText": "our support policy lets me send one no-cost replacement for the mug after i confirm the delivery address",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:i cannot refund the full order from this ticket because the original refund window closed on april 30",
    "text": "I cannot refund the full order from this ticket because the original refund window closed on April 30",
    "normalizedText": "i cannot refund the full order from this ticket because the original refund window closed on april 30",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:please reply with the current delivery address by friday at 3 p.m., and i will queue the replacement mug",
    "text": "Please reply with the current delivery address by Friday at 3 p.m., and I will queue the replacement mug",
    "normalizedText": "please reply with the current delivery address by friday at 3 p.m., and i will queue the replacement mug",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:if the address has change, i need the new address in writing before i create the shipment",
    "text": "If the address has changed, I need the new address in writing before I create the shipment",
    "normalizedText": "if the address has change, i need the new address in writing before i create the shipment",
    "category": "constraint",
    "source": "draft",
    "required": true
  }
]
```

Rejected extracted facts:
```json
[
  {
    "text": "Support policy allows one no-cost replacement for the mug after delivery address confirmation.",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Current delivery address needed by Friday at 3 p.m. to queue replacement mug.",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Lena to reply with current delivery address by Friday at 3 p.m.",
    "field": "required_actions",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Support policy allows one no-cost replacement for the mug after delivery address confirmation.",
    "field": "policies_or_conditions",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "One no-cost replacement mug after address confirmation.",
    "field": "available_support",
    "reason": "No source evidence for hard fact."
  }
]
```

Attempt ledger:
```json
[]
```

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
Hi Lena,

Thanks for sending the photo of the cracked replacement mug from order R4821. I checked the order notes this morning—the replacement was marked delivered on May 6, and the matching saucer looks fine.

Our support policy lets me send one no-cost replacement for the mug after I confirm the delivery address. I cannot refund the full order from this ticket because the original refund window closed on April 30.

Please reply with the current delivery address by Friday at 3 p.m., and I will queue the replacement mug. If the address has changed, I need the new address in writing before I create the shipment.
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
Quality score after: 8
Quality score delta: 1 pts
Quality regression: no
Quality notes: The rewrite improves structure with paragraph breaks, enhancing readability while preserving all facts and boundaries. Tone is slightly more conversational with contractions like 'can't' and 'don't', adding warmth without sacrificing professionalism.
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Fact diagnostics:

Extracted facts:
```json
{
  "recipient_name": "Marco",
  "sender_name_or_role": "",
  "people_mentioned": [
    "Marco"
  ],
  "main_purpose": "Review invoice INV-8842 and explain credit options",
  "key_facts": [
    "Invoice INV-8842 for Pro Workspace plan",
    "Issued on May 2 for $186.00",
    "Covers May 1 to May 31 billing period",
    "Three seats removed on May 14",
    "$31.50 seat credit can be applied to next invoice",
    "Seat change happened during active billing month",
    "Cannot refund full $186.00 because workspace stayed active and plan not canceled during refund window",
    "Confirmation needed before May 29 to attach credit to June invoice",
    "Earlier note did not explain well",
    "Sender does not want to promise a refund not allowed by billing policy"
  ],
  "required_actions": [
    "Confirm before May 29 to apply $31.50 credit to June invoice"
  ],
  "deadlines": [
    "May 29"
  ],
  "dates_times": [
    "May 2",
    "May 1",
    "May 31",
    "May 14",
    "May 29"
  ],
  "positive_notes": [],
  "concerns": [],
  "policies_or_conditions": [
    "Refund not allowed because workspace stayed active and plan not canceled during refund window",
    "Billing policy does not allow refund"
  ],
  "available_support": [],
  "clarifications": [],
  "facts_that_must_not_change": [
    "Cannot refund the full $186.00 charge because the workspace stayed active and the plan was not canceled during the refund window.",
    "I do not want to promise a refund that the billing policy does not allow."
  ],
  "sensitive_points": [],
  "original_tone": "Apologetic and cautious"
}
```

Reviewed facts:
```json
{
  "recipient_name": "Marco",
  "sender_name_or_role": "",
  "people_mentioned": [
    "Marco",
    "Pro Workspace"
  ],
  "main_purpose": "Review invoice INV-8842 and explain credit options",
  "key_facts": [
    "Invoice INV-8842 for Pro Workspace plan",
    "Issued on May 2 for $186.00",
    "Covers May 1 to May 31 billing period",
    "$31.50 seat credit can be applied to next invoice",
    "Seat change happened during active billing month",
    "INV-8842"
  ],
  "required_actions": [],
  "deadlines": [
    "May 29"
  ],
  "dates_times": [
    "May 2",
    "May 1",
    "May 31",
    "May 14",
    "May 29"
  ],
  "positive_notes": [],
  "concerns": [],
  "policies_or_conditions": [
    "Billing policy does not allow refund",
    "refund window",
    "I cannot refund the full $186.00 charge because the workspace stayed active and the plan was not canceled during the refund window",
    "If you want me to apply the $31.50 credit, please confirm before May 29 so I can attach it to the June invoice",
    "I am sorry the earlier note did not explain this well, but I do not want to promise a refund that the billing policy does not allow"
  ],
  "available_support": [],
  "clarifications": [],
  "facts_that_must_not_change": [
    "Cannot refund the full $186.00 charge because the workspace stayed active and the plan was not canceled during the refund window.",
    "I do not want to promise a refund that the billing policy does not allow.",
    "Marco",
    "refund window",
    "INV-8842",
    "$186.00",
    "$31.50",
    "May 2",
    "May 1",
    "May 31",
    "May 14",
    "May 29",
    "50 seat",
    "Pro Workspace",
    "I cannot refund the full $186.00 charge because the workspace stayed active and the plan was not canceled during the refund window",
    "If you want me to apply the $31.50 credit, please confirm before May 29 so I can attach it to the June invoice",
    "I am sorry the earlier note did not explain this well, but I do not want to promise a refund that the billing policy does not allow"
  ],
  "sensitive_points": [],
  "original_tone": "Apologetic and cautious"
}
```

Added anchors:
```json
[
  {
    "id": "person:marco",
    "text": "Marco",
    "normalizedText": "marco",
    "category": "person",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:refund window",
    "text": "refund window",
    "normalizedText": "refund window",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "quoted_phrase:inv-8842",
    "text": "INV-8842",
    "normalizedText": "inv-8842",
    "category": "quoted_phrase",
    "source": "draft",
    "required": true
  },
  {
    "id": "amount:$186.00",
    "text": "$186.00",
    "normalizedText": "$186.00",
    "category": "amount",
    "source": "draft",
    "required": true
  },
  {
    "id": "amount:$31.50",
    "text": "$31.50",
    "normalizedText": "$31.50",
    "category": "amount",
    "source": "draft",
    "required": true
  },
  {
    "id": "date:may 2",
    "text": "May 2",
    "normalizedText": "may 2",
    "category": "date",
    "source": "draft",
    "required": true
  },
  {
    "id": "date:may 1",
    "text": "May 1",
    "normalizedText": "may 1",
    "category": "date",
    "source": "draft",
    "required": true
  },
  {
    "id": "date:may 31",
    "text": "May 31",
    "normalizedText": "may 31",
    "category": "date",
    "source": "draft",
    "required": true
  },
  {
    "id": "date:may 14",
    "text": "May 14",
    "normalizedText": "may 14",
    "category": "date",
    "source": "draft",
    "required": true
  },
  {
    "id": "date:may 29",
    "text": "May 29",
    "normalizedText": "may 29",
    "category": "date",
    "source": "draft",
    "required": true
  },
  {
    "id": "count:50 seat",
    "text": "50 seat",
    "normalizedText": "50 seat",
    "category": "count",
    "source": "draft",
    "required": true
  },
  {
    "id": "person:pro workspace",
    "text": "Pro Workspace",
    "normalizedText": "pro workspace",
    "category": "person",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:i cannot refund the full $186.00 charge because the workspace stayed active and the plan was not canceled during the refund window",
    "text": "I cannot refund the full $186.00 charge because the workspace stayed active and the plan was not canceled during the refund window",
    "normalizedText": "i cannot refund the full $186.00 charge because the workspace stayed active and the plan was not canceled during the refund window",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:if you want me to apply the $31.50 credit, please confirm before may 29 so i can attach it to the june invoice",
    "text": "If you want me to apply the $31.50 credit, please confirm before May 29 so I can attach it to the June invoice",
    "normalizedText": "if you want me to apply the $31.50 credit, please confirm before may 29 so i can attach it to the june invoice",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:i am sorry the earlier note did not explain this well, but i do not want to promise a refund that the billing policy does not allow",
    "text": "I am sorry the earlier note did not explain this well, but I do not want to promise a refund that the billing policy does not allow",
    "normalizedText": "i am sorry the earlier note did not explain this well, but i do not want to promise a refund that the billing policy does not allow",
    "category": "constraint",
    "source": "draft",
    "required": true
  }
]
```

Rejected extracted facts:
```json
[
  {
    "text": "Three seats removed on May 14",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Cannot refund full $186.00 because workspace stayed active and plan not canceled during refund window",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Confirmation needed before May 29 to attach credit to June invoice",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Earlier note did not explain well",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Sender does not want to promise a refund not allowed by billing policy",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Confirm before May 29 to apply $31.50 credit to June invoice",
    "field": "required_actions",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Refund not allowed because workspace stayed active and plan not canceled during refund window",
    "field": "policies_or_conditions",
    "reason": "No source evidence for hard fact."
  }
]
```

Attempt ledger:
```json
[
  {
    "attemptNo": 1,
    "strategy": "support_policy_options_rewrite",
    "modelRole": "mid_writer",
    "modelName": "deepseek-v4-pro",
    "thinkingMode": "non_thinking",
    "candidateText": "Hi Marco,\n\nI took a look at invoice INV-8842 for your Pro Workspace plan. It was issued on May 2 for $186.00, covering the May 1 to May 31 billing period.\n\nI can’t refund the full $186.00 because the workspace stayed active and the plan wasn’t canceled during the refund window. I don’t want to promise something the billing policy doesn’t allow. I’m sorry the earlier note didn’t explain this well.\n\nThere is a $31.50 seat credit available. If you’d like me to apply it to your June invoice, just confirm before May 29 and I’ll take care of it.",
    "failureAnalysis": "Candidate preselection rejected model candidates: missing:may 14; missing_locked:May 14",
    "failureKinds": [
      "fact_loss"
    ],
    "factGateResult": "failed",
    "structureGateResult": "failed",
    "policyIntentGateResult": "failed",
    "saplingResult": "not_measured",
    "nextStrategyDecision": "facts_first_reconstruct"
  }
]
```

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
Quality score before: 8
Quality score after: 9
Quality score delta: 1 pts
Quality regression: no
Quality notes: The rewrite improves structure by adding paragraph breaks, making it easier to scan and act on without changing any facts.
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Fact diagnostics:

Extracted facts:
```json
{
  "recipient_name": "",
  "sender_name_or_role": "",
  "people_mentioned": [
    "Priya",
    "Sam"
  ],
  "main_purpose": "Update on Acorn dashboard progress and blocking issue with staging data sync",
  "key_facts": [
    "Chart cleanup completed",
    "Export button copy completed",
    "Blocked on final QA pass because staging data sync failed at 9:20 a.m.",
    "Priya owns the sync job and is checking logs",
    "If sync is back before 2 p.m., QA notes can be sent today",
    "If sync is not back by 2 p.m., QA notes move to tomorrow morning",
    "Need Sam to confirm whether to send client a short delay note today or wait until sync status known",
    "Another update will be posted by 2:30 p.m."
  ],
  "required_actions": [
    "Priya: check sync logs",
    "Sam: confirm whether to send client delay note today or wait"
  ],
  "deadlines": [
    "Sync back before 2 p.m. to send QA notes today",
    "Another update by 2:30 p.m."
  ],
  "dates_times": [
    "9:20 a.m.",
    "2 p.m.",
    "2:30 p.m."
  ],
  "positive_notes": [
    "Chart cleanup completed",
    "Export button copy completed"
  ],
  "concerns": [
    "Staging data sync failed",
    "Blocked on final QA pass"
  ],
  "policies_or_conditions": [],
  "available_support": [],
  "clarifications": [],
  "facts_that_must_not_change": [
    "Blocked on final QA pass because staging data sync failed",
    "If sync is not back by 2 p.m., QA notes move to tomorrow morning",
    "Need Sam to confirm whether to send client a short delay note today or wait until sync status known"
  ],
  "sensitive_points": [],
  "original_tone": "neutral, informative"
}
```

Reviewed facts:
```json
{
  "recipient_name": "",
  "sender_name_or_role": "",
  "people_mentioned": [
    "Priya",
    "Sam"
  ],
  "main_purpose": "Update on Acorn dashboard progress and blocking issue with staging data sync",
  "key_facts": [
    "Chart cleanup completed",
    "Export button copy completed",
    "Priya owns the sync job and is checking logs",
    "Acorn dashboard"
  ],
  "required_actions": [
    "Priya: check sync logs",
    "Priya owns the sync job"
  ],
  "deadlines": [
    "Another update by 2:30 p.m.",
    "9:20 a.m.",
    "2 p.m.",
    "2:30 p.m.",
    "before 2 p.m.",
    "by 2:30 p.m."
  ],
  "dates_times": [
    "9:20 a.m.",
    "2 p.m.",
    "2:30 p.m."
  ],
  "positive_notes": [
    "Chart cleanup completed",
    "Export button copy completed"
  ],
  "concerns": [
    "Staging data sync failed",
    "Blocked on final QA pass"
  ],
  "policies_or_conditions": [
    "If the sync is back before 2 p.m., I can still send the QA notes today",
    "If it is not back by then, the QA notes move to tomorrow morning",
    "I need Sam to confirm whether we should send the client a short delay note today or wait until we know the sync status",
    "I will post another update by 2:30 p.m."
  ],
  "available_support": [],
  "clarifications": [],
  "facts_that_must_not_change": [
    "Blocked on final QA pass because staging data sync failed",
    "Acorn dashboard",
    "9:20 a.m.",
    "2 p.m.",
    "2:30 p.m.",
    "before 2 p.m.",
    "by 2:30 p.m.",
    "Priya",
    "Priya owns the sync job",
    "If the sync is back before 2 p.m., I can still send the QA notes today",
    "If it is not back by then, the QA notes move to tomorrow morning",
    "I need Sam to confirm whether we should send the client a short delay note today or wait until we know the sync status",
    "I will post another update by 2:30 p.m."
  ],
  "sensitive_points": [],
  "original_tone": "neutral, informative"
}
```

Added anchors:
```json
[
  {
    "id": "quoted_phrase:acorn dashboard",
    "text": "Acorn dashboard",
    "normalizedText": "acorn dashboard",
    "category": "quoted_phrase",
    "source": "draft",
    "required": true
  },
  {
    "id": "deadline:9:20 a.m.",
    "text": "9:20 a.m.",
    "normalizedText": "9:20 a.m.",
    "category": "deadline",
    "source": "draft",
    "required": true
  },
  {
    "id": "deadline:2 p.m.",
    "text": "2 p.m.",
    "normalizedText": "2 p.m.",
    "category": "deadline",
    "source": "draft",
    "required": true
  },
  {
    "id": "deadline:2:30 p.m.",
    "text": "2:30 p.m.",
    "normalizedText": "2:30 p.m.",
    "category": "deadline",
    "source": "draft",
    "required": true
  },
  {
    "id": "deadline:before 2 p.m.",
    "text": "before 2 p.m.",
    "normalizedText": "before 2 p.m.",
    "category": "deadline",
    "source": "draft",
    "required": true
  },
  {
    "id": "deadline:by 2:30 p.m.",
    "text": "by 2:30 p.m.",
    "normalizedText": "by 2:30 p.m.",
    "category": "deadline",
    "source": "draft",
    "required": true
  },
  {
    "id": "person:priya",
    "text": "Priya",
    "normalizedText": "priya",
    "category": "person",
    "source": "draft",
    "required": true
  },
  {
    "id": "ordered_step:priya owns the sync job",
    "text": "Priya owns the sync job",
    "normalizedText": "priya owns the sync job",
    "category": "ordered_step",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:if the sync is back before 2 p.m., i can still send the qa notes today",
    "text": "If the sync is back before 2 p.m., I can still send the QA notes today",
    "normalizedText": "if the sync is back before 2 p.m., i can still send the qa notes today",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:if it is not back by then, the qa notes move to tomorrow morning",
    "text": "If it is not back by then, the QA notes move to tomorrow morning",
    "normalizedText": "if it is not back by then, the qa notes move to tomorrow morning",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:i need sam to confirm whether we should send the client a short delay note today or wait until we know the sync status",
    "text": "I need Sam to confirm whether we should send the client a short delay note today or wait until we know the sync status",
    "normalizedText": "i need sam to confirm whether we should send the client a short delay note today or wait until we know the sync status",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:i will post another update by 2:30 p.m.",
    "text": "I will post another update by 2:30 p.m.",
    "normalizedText": "i will post another update by 2:30 p.m.",
    "category": "constraint",
    "source": "draft",
    "required": true
  }
]
```

Rejected extracted facts:
```json
[
  {
    "text": "Blocked on final QA pass because staging data sync failed at 9:20 a.m.",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "If sync is back before 2 p.m., QA notes can be sent today",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "If sync is not back by 2 p.m., QA notes move to tomorrow morning",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Need Sam to confirm whether to send client a short delay note today or wait until sync status known",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Another update will be posted by 2:30 p.m.",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Sam: confirm whether to send client delay note today or wait",
    "field": "required_actions",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Sync back before 2 p.m. to send QA notes today",
    "field": "deadlines",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "If sync is not back by 2 p.m., QA notes move to tomorrow morning",
    "field": "facts_that_must_not_change",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Need Sam to confirm whether to send client a short delay note today or wait until sync status known",
    "field": "facts_that_must_not_change",
    "reason": "No source evidence for hard fact."
  }
]
```

Attempt ledger:
```json
[
  {
    "attemptNo": 1,
    "strategy": "support_policy_options_rewrite",
    "modelRole": "mid_writer",
    "modelName": "deepseek-v4-pro",
    "thinkingMode": "non_thinking",
    "candidateText": "Quick update on the Acorn dashboard.\n\nChart cleanup and export button copy are both complete.\n\nThe main issue now: we’re blocked on the final QA pass because the staging data sync failed. Priya owns the sync job and is checking the logs.\n\nIf the sync is back before 2 p.m., I can still send the QA notes today. If it isn’t back by then, the QA notes move to tomorrow morning.\n\nI need Sam to confirm whether we should send the client a short delay note today or wait until we know the sync status.\n\nI will post another update by 2:30 p.m.",
    "failureAnalysis": "Candidate preselection rejected model candidates: missing:9:20 a.m.; missing_locked:9:20 a.m.",
    "failureKinds": [
      "fact_loss"
    ],
    "factGateResult": "failed",
    "structureGateResult": "failed",
    "policyIntentGateResult": "failed",
    "saplingResult": "not_measured",
    "nextStrategyDecision": "facts_first_reconstruct"
  }
]
```

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
Quick update on the Acorn dashboard: I finished the chart cleanup and the export button copy, but I am blocked on the final QA pass because the staging data sync failed again at 9:20 a.m. Priya owns the sync job and is checking the logs now. If the sync is back before 2 p.m., I can still send the QA notes today.

If it is not back by then, the QA notes move to tomorrow morning. I need Sam to confirm whether we should send the client a short delay note today or wait until we know the sync status.

I will post another update by 2:30 p.m.
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
Quality score before: 8
Quality score after: 9
Quality score delta: 1 pts
Quality regression: no
Quality notes: The rewrite improves structure and readability with paragraph breaks, making it easier to scan while preserving all facts, tone, and warmth.
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Fact diagnostics:

Extracted facts:
```json
{
  "recipient_name": "Dev",
  "sender_name_or_role": "",
  "people_mentioned": [
    "Dev"
  ],
  "main_purpose": "Follow-up on Northstar rollout quote Q-7719",
  "key_facts": [
    "Quote Q-7719 is attached",
    "Quote is for 18 seats",
    "Price is $42 per seat per month",
    "Quote includes onboarding",
    "Quote includes admin workspace",
    "Quote includes standard email support package",
    "Quote expires on June 7",
    "Sender can answer scope questions this week",
    "Sender cannot add advanced SSO setup or a discount without a new approval cycle",
    "If current quote matches needs, reply by June 7 to receive order form",
    "If SSO setup is required, sender can start a revised quote"
  ],
  "required_actions": [
    "Reply by June 7 if current quote matches needs",
    "Indicate if SSO setup is required to start revised quote"
  ],
  "deadlines": [
    "June 7"
  ],
  "dates_times": [
    "this week",
    "June 7"
  ],
  "positive_notes": [],
  "concerns": [],
  "policies_or_conditions": [
    "Advanced SSO setup or discount requires new approval cycle"
  ],
  "available_support": [
    "Sender can answer scope questions this week"
  ],
  "clarifications": [],
  "facts_that_must_not_change": [
    "Sender cannot add the advanced SSO setup or a discount to this quote without a new approval cycle"
  ],
  "sensitive_points": [],
  "original_tone": "friendly and professional"
}
```

Reviewed facts:
```json
{
  "recipient_name": "Dev",
  "sender_name_or_role": "",
  "people_mentioned": [
    "Dev"
  ],
  "main_purpose": "Follow-up on Northstar rollout quote Q-7719",
  "key_facts": [
    "Quote Q-7719 is attached",
    "Quote is for 18 seats",
    "Quote includes onboarding",
    "Quote includes admin workspace",
    "Quote includes standard email support package",
    "Quote expires on June 7",
    "Sender can answer scope questions this week",
    "If SSO setup is required, sender can start a revised quote",
    "SSO setup",
    "Northstar rollout"
  ],
  "required_actions": [
    "Indicate if SSO setup is required to start revised quote"
  ],
  "deadlines": [
    "June 7"
  ],
  "dates_times": [
    "this week",
    "June 7"
  ],
  "positive_notes": [],
  "concerns": [],
  "policies_or_conditions": [
    "new approval cycle",
    "advanced SSO",
    "I can answer scope questions this week, but I cannot add the advanced SSO setup or a discount to this quote without a new approval cycle",
    "If the current quote still matches what you need, please reply by June 7 and I can send the order form",
    "If the SSO setup is required, I can start a revised quote instead"
  ],
  "available_support": [
    "Sender can answer scope questions this week"
  ],
  "clarifications": [],
  "facts_that_must_not_change": [
    "Dev",
    "new approval cycle",
    "advanced SSO",
    "SSO setup",
    "Northstar rollout",
    "$42",
    "June 7",
    "18 seats",
    "I can answer scope questions this week, but I cannot add the advanced SSO setup or a discount to this quote without a new approval cycle",
    "If the current quote still matches what you need, please reply by June 7 and I can send the order form",
    "If the SSO setup is required, I can start a revised quote instead"
  ],
  "sensitive_points": [],
  "original_tone": "friendly and professional"
}
```

Added anchors:
```json
[
  {
    "id": "person:dev",
    "text": "Dev",
    "normalizedText": "dev",
    "category": "person",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:new approval cycle",
    "text": "new approval cycle",
    "normalizedText": "new approval cycle",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:advanced sso",
    "text": "advanced SSO",
    "normalizedText": "advanced sso",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "task:sso setup",
    "text": "SSO setup",
    "normalizedText": "sso setup",
    "category": "task",
    "source": "draft",
    "required": true
  },
  {
    "id": "quoted_phrase:northstar rollout",
    "text": "Northstar rollout",
    "normalizedText": "northstar rollout",
    "category": "quoted_phrase",
    "source": "draft",
    "required": true
  },
  {
    "id": "amount:$42",
    "text": "$42",
    "normalizedText": "$42",
    "category": "amount",
    "source": "draft",
    "required": true
  },
  {
    "id": "date:june 7",
    "text": "June 7",
    "normalizedText": "june 7",
    "category": "date",
    "source": "draft",
    "required": true
  },
  {
    "id": "count:18 seats",
    "text": "18 seats",
    "normalizedText": "18 seats",
    "category": "count",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:i can answer scope questions this week, but i cannot add the advanced sso setup or a discount to this quote without a new approval cycle",
    "text": "I can answer scope questions this week, but I cannot add the advanced SSO setup or a discount to this quote without a new approval cycle",
    "normalizedText": "i can answer scope questions this week, but i cannot add the advanced sso setup or a discount to this quote without a new approval cycle",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:if the current quote still matches what you need, please reply by june 7 and i can send the order form",
    "text": "If the current quote still matches what you need, please reply by June 7 and I can send the order form",
    "normalizedText": "if the current quote still matches what you need, please reply by june 7 and i can send the order form",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:if the sso setup is required, i can start a revised quote instead",
    "text": "If the SSO setup is required, I can start a revised quote instead",
    "normalizedText": "if the sso setup is required, i can start a revised quote instead",
    "category": "constraint",
    "source": "draft",
    "required": true
  }
]
```

Rejected extracted facts:
```json
[
  {
    "text": "Price is $42 per seat per month",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Sender cannot add advanced SSO setup or a discount without a new approval cycle",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "If current quote matches needs, reply by June 7 to receive order form",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Reply by June 7 if current quote matches needs",
    "field": "required_actions",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Advanced SSO setup or discount requires new approval cycle",
    "field": "policies_or_conditions",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Sender cannot add the advanced SSO setup or a discount to this quote without a new approval cycle",
    "field": "facts_that_must_not_change",
    "reason": "No source evidence for hard fact."
  }
]
```

Attempt ledger:
```json
[
  {
    "attemptNo": 1,
    "strategy": "support_policy_options_rewrite",
    "modelRole": "mid_writer",
    "modelName": "deepseek-v4-pro",
    "thinkingMode": "non_thinking",
    "candidateText": "Hi Dev,\n\nFollowing up on the Northstar rollout—I’ve attached quote Q-7719. It’s for 18 seats and includes onboarding, admin workspace, and standard email support. The quote expires on June 7.\n\nThis week, I’m available to answer any scope questions. Just a note: I can’t add advanced SSO setup or a discount to this quote without a new approval cycle.\n\nIf everything in the current quote still fits your needs, let me know by June 7 and I’ll send over the order form. If you need SSO setup, I can start a revised quote instead.\n\nLet me know what you think.",
    "failureAnalysis": "Candidate preselection rejected model candidates: missing:$42; missing_locked:$42",
    "failureKinds": [
      "fact_loss"
    ],
    "factGateResult": "failed",
    "structureGateResult": "failed",
    "policyIntentGateResult": "failed",
    "saplingResult": "not_measured",
    "nextStrategyDecision": "facts_first_reconstruct"
  }
]
```

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
Quality score after: 8
Quality score delta: 1 pts
Quality regression: no
Quality notes: The rewrite improves structure with paragraph breaks and an em dash, making it more readable and slightly warmer while preserving all facts.
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Fact diagnostics:

Extracted facts:
```json
{
  "recipient_name": "Ren",
  "sender_name_or_role": "",
  "people_mentioned": [
    "Ren"
  ],
  "main_purpose": "Reschedule appointment",
  "key_facts": [
    "Original appointment: Thursday, May 16 at 11 a.m.",
    "Room unavailable for original time",
    "Offered alternative 1: Thursday, May 16 at 2:30 p.m.",
    "Offered alternative 2: Friday, May 17 at 9 a.m.",
    "Deadline to choose: Wednesday, May 15 at noon",
    "Both times cannot be held after Wednesday noon",
    "Selected slot will be confirmed upon reply"
  ],
  "required_actions": [
    "Ren must choose one of the offered times by Wednesday at noon"
  ],
  "deadlines": [
    "Wednesday, May 15 at noon"
  ],
  "dates_times": [
    "Thursday, May 16 at 11 a.m.",
    "Thursday, May 16 at 2:30 p.m.",
    "Friday, May 17 at 9 a.m.",
    "Wednesday, May 15 at noon"
  ],
  "positive_notes": [],
  "concerns": [],
  "policies_or_conditions": [],
  "available_support": [],
  "clarifications": [],
  "facts_that_must_not_change": [
    "Cannot hold both times after noon on Wednesday"
  ],
  "sensitive_points": [],
  "original_tone": "neutral"
}
```

Reviewed facts:
```json
{
  "recipient_name": "Ren",
  "sender_name_or_role": "",
  "people_mentioned": [
    "Ren"
  ],
  "main_purpose": "Reschedule appointment",
  "key_facts": [
    "Room unavailable for original time"
  ],
  "required_actions": [],
  "deadlines": [
    "11 a.m.",
    "2:30 p.m.",
    "9 a.m.",
    "by Wednesday",
    "after noon"
  ],
  "dates_times": [
    "Thursday, May 16 at 11 a.m.",
    "May 16",
    "Thursday",
    "Friday",
    "Wednesday"
  ],
  "positive_notes": [],
  "concerns": [],
  "policies_or_conditions": [
    "room is unavailable",
    "Hi Ren, I need to move our Thursday, May 16 appointment from 11 a.m",
    "I can offer Thursday at 2:30 p.m",
    "or Friday at 9 a.m",
    "Please choose one by Wednesday at noon",
    "I cannot hold both times after noon, but I will confirm the selected slot as soon as you reply"
  ],
  "available_support": [],
  "clarifications": [],
  "facts_that_must_not_change": [
    "Cannot hold both times after noon on Wednesday",
    "Ren",
    "room is unavailable",
    "May 16",
    "Thursday",
    "Friday",
    "Wednesday",
    "11 a.m.",
    "2:30 p.m.",
    "9 a.m.",
    "by Wednesday",
    "after noon",
    "Hi Ren, I need to move our Thursday, May 16 appointment from 11 a.m",
    "I can offer Thursday at 2:30 p.m",
    "or Friday at 9 a.m",
    "Please choose one by Wednesday at noon",
    "I cannot hold both times after noon, but I will confirm the selected slot as soon as you reply"
  ],
  "sensitive_points": [],
  "original_tone": "neutral"
}
```

Added anchors:
```json
[
  {
    "id": "person:ren",
    "text": "Ren",
    "normalizedText": "ren",
    "category": "person",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:room is unavailable",
    "text": "room is unavailable",
    "normalizedText": "room is unavailable",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "date:may 16",
    "text": "May 16",
    "normalizedText": "may 16",
    "category": "date",
    "source": "draft",
    "required": true
  },
  {
    "id": "date:thursday",
    "text": "Thursday",
    "normalizedText": "thursday",
    "category": "date",
    "source": "draft",
    "required": true
  },
  {
    "id": "date:friday",
    "text": "Friday",
    "normalizedText": "friday",
    "category": "date",
    "source": "draft",
    "required": true
  },
  {
    "id": "date:wednesday",
    "text": "Wednesday",
    "normalizedText": "wednesday",
    "category": "date",
    "source": "draft",
    "required": true
  },
  {
    "id": "deadline:11 a.m.",
    "text": "11 a.m.",
    "normalizedText": "11 a.m.",
    "category": "deadline",
    "source": "draft",
    "required": true
  },
  {
    "id": "deadline:2:30 p.m.",
    "text": "2:30 p.m.",
    "normalizedText": "2:30 p.m.",
    "category": "deadline",
    "source": "draft",
    "required": true
  },
  {
    "id": "deadline:9 a.m.",
    "text": "9 a.m.",
    "normalizedText": "9 a.m.",
    "category": "deadline",
    "source": "draft",
    "required": true
  },
  {
    "id": "deadline:by wednesday",
    "text": "by Wednesday",
    "normalizedText": "by wednesday",
    "category": "deadline",
    "source": "draft",
    "required": true
  },
  {
    "id": "deadline:after noon",
    "text": "after noon",
    "normalizedText": "after noon",
    "category": "deadline",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:hi ren, i need to move our thursday, may 16 appointment from 11 a.m",
    "text": "Hi Ren, I need to move our Thursday, May 16 appointment from 11 a.m",
    "normalizedText": "hi ren, i need to move our thursday, may 16 appointment from 11 a.m",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:i can offer thursday at 2:30 p.m",
    "text": "I can offer Thursday at 2:30 p.m",
    "normalizedText": "i can offer thursday at 2:30 p.m",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:or friday at 9 a.m",
    "text": "or Friday at 9 a.m",
    "normalizedText": "or friday at 9 a.m",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:please choose one by wednesday at noon",
    "text": "Please choose one by Wednesday at noon",
    "normalizedText": "please choose one by wednesday at noon",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:i cannot hold both times after noon, but i will confirm the selected slot as soon as you reply",
    "text": "I cannot hold both times after noon, but I will confirm the selected slot as soon as you reply",
    "normalizedText": "i cannot hold both times after noon, but i will confirm the selected slot as soon as you reply",
    "category": "constraint",
    "source": "draft",
    "required": true
  }
]
```

Rejected extracted facts:
```json
[
  {
    "text": "Original appointment: Thursday, May 16 at 11 a.m.",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Offered alternative 1: Thursday, May 16 at 2:30 p.m.",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Offered alternative 2: Friday, May 17 at 9 a.m.",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Deadline to choose: Wednesday, May 15 at noon",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Both times cannot be held after Wednesday noon",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Selected slot will be confirmed upon reply",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Ren must choose one of the offered times by Wednesday at noon",
    "field": "required_actions",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Wednesday, May 15 at noon",
    "field": "deadlines",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Thursday, May 16 at 2:30 p.m.",
    "field": "dates_times",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Friday, May 17 at 9 a.m.",
    "field": "dates_times",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Wednesday, May 15 at noon",
    "field": "dates_times",
    "reason": "No source evidence for hard fact."
  }
]
```

Attempt ledger:
```json
[]
```

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

I need to move our Thursday, May 16 appointment from 11 a.m.—the room is unavailable at that time.

I can offer Thursday at 2:30 p.m. or Friday at 9 a.m. Please choose one by Wednesday at noon. I cannot hold both times after noon, but I will confirm the selected slot as soon as you reply.
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
Quality score after: 9
Quality score delta: 2 pts
Quality regression: no
Quality notes: Original draft is a single block of text, reducing readability. Rewrite adds paragraph breaks to improve structure and clarity. | Both maintain a warm and respectful tone, clearly avoiding overpromising or false implications. | No factual changes; the rewrite is a direct formatting improvement.
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Fact diagnostics:

Extracted facts:
```json
{
  "recipient_name": "Alina",
  "sender_name_or_role": "",
  "people_mentioned": [
    "product team",
    "hiring manager"
  ],
  "main_purpose": "Update on Senior Support Lead role hiring status",
  "key_facts": [
    "Meeting with product team occurred on May 10",
    "Role discussed: Senior Support Lead",
    "Panel enjoyed learning about Alina's experience with queue operations and onboarding",
    "No final hiring decision has been made",
    "Hiring manager is reviewing last two panel notes this week",
    "Next update expected by Tuesday, May 21",
    "No additional information needed from Alina today",
    "Alina should reply if availability changes before next update"
  ],
  "required_actions": [
    "Reply to thread if availability changes before Tuesday, May 21"
  ],
  "deadlines": [
    "Tuesday, May 21"
  ],
  "dates_times": [
    "May 10",
    "Tuesday, May 21"
  ],
  "positive_notes": [
    "Panel enjoyed learning about Alina's experience with queue operations and onboarding"
  ],
  "concerns": [],
  "policies_or_conditions": [],
  "available_support": [],
  "clarifications": [],
  "facts_that_must_not_change": [
    "We have not made a final hiring decision yet",
    "I do not want to make the status sound more final than it is",
    "There is nothing else you need to send today",
    "If your availability changes before then, please reply to this thread so I can keep the file current"
  ],
  "sensitive_points": [],
  "original_tone": "Professional and considerate"
}
```

Reviewed facts:
```json
{
  "recipient_name": "Alina",
  "sender_name_or_role": "",
  "people_mentioned": [
    "product team",
    "hiring manager",
    "Alina",
    "Senior Support Lead"
  ],
  "main_purpose": "Update on Senior Support Lead role hiring status",
  "key_facts": [
    "Role discussed: Senior Support Lead",
    "Panel enjoyed learning about Alina's experience with queue operations and onboarding",
    "No final hiring decision has been made",
    "Hiring manager is reviewing last two panel notes this week"
  ],
  "required_actions": [],
  "deadlines": [
    "Tuesday, May 21",
    "by Tuesday"
  ],
  "dates_times": [
    "May 10",
    "Tuesday, May 21",
    "May 21",
    "Tuesday"
  ],
  "positive_notes": [
    "Panel enjoyed learning about Alina's experience with queue operations and onboarding"
  ],
  "concerns": [],
  "policies_or_conditions": [
    "We have not made a final hiring decision yet, and I do not want to make the status sound more final than it is",
    "I expect to send the next update by Tuesday, May 21",
    "There is nothing else you need to send today",
    "If your availability changes before then, please reply to this thread so I can keep the file current"
  ],
  "available_support": [],
  "clarifications": [],
  "facts_that_must_not_change": [
    "We have not made a final hiring decision yet",
    "I do not want to make the status sound more final than it is",
    "There is nothing else you need to send today",
    "If your availability changes before then, please reply to this thread so I can keep the file current",
    "Alina",
    "May 10",
    "May 21",
    "Tuesday",
    "by Tuesday",
    "Senior Support Lead",
    "We have not made a final hiring decision yet, and I do not want to make the status sound more final than it is",
    "I expect to send the next update by Tuesday, May 21"
  ],
  "sensitive_points": [],
  "original_tone": "Professional and considerate"
}
```

Added anchors:
```json
[
  {
    "id": "person:alina",
    "text": "Alina",
    "normalizedText": "alina",
    "category": "person",
    "source": "draft",
    "required": true
  },
  {
    "id": "date:may 10",
    "text": "May 10",
    "normalizedText": "may 10",
    "category": "date",
    "source": "draft",
    "required": true
  },
  {
    "id": "date:may 21",
    "text": "May 21",
    "normalizedText": "may 21",
    "category": "date",
    "source": "draft",
    "required": true
  },
  {
    "id": "date:tuesday",
    "text": "Tuesday",
    "normalizedText": "tuesday",
    "category": "date",
    "source": "draft",
    "required": true
  },
  {
    "id": "deadline:by tuesday",
    "text": "by Tuesday",
    "normalizedText": "by tuesday",
    "category": "deadline",
    "source": "draft",
    "required": true
  },
  {
    "id": "person:senior support lead",
    "text": "Senior Support Lead",
    "normalizedText": "senior support lead",
    "category": "person",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:we have not made a final hiring decision yet, and i do not want to make the status sound more final than it is",
    "text": "We have not made a final hiring decision yet, and I do not want to make the status sound more final than it is",
    "normalizedText": "we have not made a final hiring decision yet, and i do not want to make the status sound more final than it is",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:i expect to send the next update by tuesday, may 21",
    "text": "I expect to send the next update by Tuesday, May 21",
    "normalizedText": "i expect to send the next update by tuesday, may 21",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:there is nothing else you need to send today",
    "text": "There is nothing else you need to send today",
    "normalizedText": "there is nothing else you need to send today",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:if your availability changes before then, please reply to this thread so i can keep the file current",
    "text": "If your availability changes before then, please reply to this thread so I can keep the file current",
    "normalizedText": "if your availability changes before then, please reply to this thread so i can keep the file current",
    "category": "constraint",
    "source": "draft",
    "required": true
  }
]
```

Rejected extracted facts:
```json
[
  {
    "text": "Meeting with product team occurred on May 10",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Next update expected by Tuesday, May 21",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "No additional information needed from Alina today",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Alina should reply if availability changes before next update",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Reply to thread if availability changes before Tuesday, May 21",
    "field": "required_actions",
    "reason": "No source evidence for hard fact."
  }
]
```

Attempt ledger:
```json
[
  {
    "attemptNo": 1,
    "strategy": "support_policy_options_rewrite",
    "modelRole": "mid_writer",
    "modelName": "deepseek-v4-pro",
    "thinkingMode": "non_thinking",
    "candidateText": "Alina, the panel enjoyed learning about your experience with queue operations and onboarding for the Senior Support Lead role. We have not made a final hiring decision yet, and I do not want to make the status sound more final than it is. The hiring manager is reviewing the last two panel notes this week. I expect to send the next update by Tuesday, May 21. There is nothing else you need to send today. If your availability changes before then, please reply to this thread so I can keep the file current.",
    "failureAnalysis": "Candidate preselection rejected model candidates: missing:may 10; missing_locked:May 10",
    "failureKinds": [
      "fact_loss"
    ],
    "factGateResult": "failed",
    "structureGateResult": "failed",
    "policyIntentGateResult": "failed",
    "saplingResult": "not_measured",
    "nextStrategyDecision": "facts_first_reconstruct"
  }
]
```

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
Quality score before: 7
Quality score after: 8
Quality score delta: 1 pts
Quality regression: no
Quality notes: The original draft is clear and complete but presented as a single dense paragraph. | The rewrite improves structure with line breaks, enhancing readability. | Contractions ('I'm', 'can't') add slight warmth and a more human tone. | All clinical boundaries and instructions are perfectly preserved.
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Fact diagnostics:

Extracted facts:
```json
{
  "recipient_name": "Nora",
  "sender_name_or_role": "",
  "people_mentioned": [
    "Eli",
    "Dr. Chen"
  ],
  "main_purpose": "Update on lab report release status and next steps",
  "key_facts": [
    "Lab report from May 3 visit is marked received by the office",
    "Report has not been released to the patient portal yet",
    "Sender is not a clinician and cannot interpret results",
    "Release request sent to Dr. Chen's queue at 10:15 a.m. today",
    "Most release requests are reviewed within two business days",
    "If Eli has new or worsening symptoms, call the clinic line instead of waiting for portal reply"
  ],
  "required_actions": [
    "Call clinic line if Eli has new or worsening symptoms"
  ],
  "deadlines": [],
  "dates_times": [
    "May 3 visit",
    "10:15 a.m. today"
  ],
  "positive_notes": [],
  "concerns": [],
  "policies_or_conditions": [
    "Most release requests are reviewed within two business days"
  ],
  "available_support": [
    "Clinic line for new or worsening symptoms"
  ],
  "clarifications": [],
  "facts_that_must_not_change": [
    "Report has not been released to the patient portal yet",
    "Sender is not a clinician and cannot interpret results",
    "If Eli has new or worsening symptoms, please call the clinic line instead of waiting for a portal reply"
  ],
  "sensitive_points": [],
  "original_tone": "Informative and cautious"
}
```

Reviewed facts:
```json
{
  "recipient_name": "Nora",
  "sender_name_or_role": "",
  "people_mentioned": [
    "Eli",
    "Dr. Chen",
    "Nora"
  ],
  "main_purpose": "Update on lab report release status and next steps",
  "key_facts": [
    "Most release requests are reviewed within two business days",
    "If Eli has new or worsening symptoms, call the clinic line instead of waiting for portal reply",
    "report is marked received",
    "Dr. Chen's queue",
    "clinic line"
  ],
  "required_actions": [
    "Call clinic line if Eli has new or worsening symptoms"
  ],
  "deadlines": [
    "10:15 a.m."
  ],
  "dates_times": [
    "May 3 visit",
    "10:15 a.m. today",
    "May 3"
  ],
  "positive_notes": [],
  "concerns": [],
  "policies_or_conditions": [
    "Most release requests are reviewed within two business days",
    "not been released to the patient portal",
    "not a clinician",
    "cannot interpret",
    "I can see that the report is marked received by our office, but it has not been released to the patient portal yet",
    "I am not a clinician, so I cannot interpret the results in this message",
    "If Eli has new or worsening symptoms, please call the clinic line instead of waiting for a portal reply"
  ],
  "available_support": [
    "Clinic line for new or worsening symptoms"
  ],
  "clarifications": [],
  "facts_that_must_not_change": [
    "If Eli has new or worsening symptoms, please call the clinic line instead of waiting for a portal reply",
    "Nora",
    "report is marked received",
    "not been released to the patient portal",
    "not a clinician",
    "cannot interpret",
    "Dr. Chen's queue",
    "clinic line",
    "May 3",
    "10:15 a.m.",
    "Eli",
    "Dr. Chen",
    "I can see that the report is marked received by our office, but it has not been released to the patient portal yet",
    "I am not a clinician, so I cannot interpret the results in this message"
  ],
  "sensitive_points": [],
  "original_tone": "Informative and cautious"
}
```

Added anchors:
```json
[
  {
    "id": "person:nora",
    "text": "Nora",
    "normalizedText": "nora",
    "category": "person",
    "source": "draft",
    "required": true
  },
  {
    "id": "task:report is marked received",
    "text": "report is marked received",
    "normalizedText": "report is marked received",
    "category": "task",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:not been released to the patient portal",
    "text": "not been released to the patient portal",
    "normalizedText": "not been released to the patient portal",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:not a clinician",
    "text": "not a clinician",
    "normalizedText": "not a clinician",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:cannot interpret",
    "text": "cannot interpret",
    "normalizedText": "cannot interpret",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "task:dr. chen's queue",
    "text": "Dr. Chen's queue",
    "normalizedText": "dr. chen's queue",
    "category": "task",
    "source": "draft",
    "required": true
  },
  {
    "id": "task:clinic line",
    "text": "clinic line",
    "normalizedText": "clinic line",
    "category": "task",
    "source": "draft",
    "required": true
  },
  {
    "id": "date:may 3",
    "text": "May 3",
    "normalizedText": "may 3",
    "category": "date",
    "source": "draft",
    "required": true
  },
  {
    "id": "deadline:10:15 a.m.",
    "text": "10:15 a.m.",
    "normalizedText": "10:15 a.m.",
    "category": "deadline",
    "source": "draft",
    "required": true
  },
  {
    "id": "person:eli",
    "text": "Eli",
    "normalizedText": "eli",
    "category": "person",
    "source": "draft",
    "required": true
  },
  {
    "id": "person:dr. chen",
    "text": "Dr. Chen",
    "normalizedText": "dr. chen",
    "category": "person",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:i can see that the report is marked received by our office, but it has not been released to the patient portal yet",
    "text": "I can see that the report is marked received by our office, but it has not been released to the patient portal yet",
    "normalizedText": "i can see that the report is marked received by our office, but it has not been released to the patient portal yet",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:i am not a clinician, so i cannot interpret the results in this message",
    "text": "I am not a clinician, so I cannot interpret the results in this message",
    "normalizedText": "i am not a clinician, so i cannot interpret the results in this message",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:if eli has new or worsening symptoms, please call the clinic line instead of waiting for a portal reply",
    "text": "If Eli has new or worsening symptoms, please call the clinic line instead of waiting for a portal reply",
    "normalizedText": "if eli has new or worsening symptoms, please call the clinic line instead of waiting for a portal reply",
    "category": "constraint",
    "source": "draft",
    "required": true
  }
]
```

Rejected extracted facts:
```json
[
  {
    "text": "Lab report from May 3 visit is marked received by the office",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Report has not been released to the patient portal yet",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Sender is not a clinician and cannot interpret results",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Release request sent to Dr. Chen's queue at 10:15 a.m. today",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Report has not been released to the patient portal yet",
    "field": "facts_that_must_not_change",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Sender is not a clinician and cannot interpret results",
    "field": "facts_that_must_not_change",
    "reason": "No source evidence for hard fact."
  }
]
```

Attempt ledger:
```json
[
  {
    "attemptNo": 1,
    "strategy": "facts_first_reconstruct",
    "modelRole": "mid_writer",
    "modelName": "deepseek-v4-pro",
    "thinkingMode": "non_thinking",
    "candidateText": "Hi Nora,\n\nI checked on Eli's lab report from the May 3 visit. I can see that the report is marked received by our office, but it has not been released to the patient portal yet. It is in Dr. Chen's queue, and most release requests are reviewed within two business days.\n\nAt this point, I am not a clinician, so I cannot interpret the results in this message. If Eli has new or worsening symptoms, please call the clinic line instead of waiting for a portal reply.\n\nLet me know if you have other questions.",
    "failureAnalysis": "Candidate preselection rejected model candidates: missing:10:15 a.m.; missing_locked:10:15 a.m.",
    "failureKinds": [
      "fact_loss"
    ],
    "factGateResult": "failed",
    "structureGateResult": "failed",
    "policyIntentGateResult": "failed",
    "saplingResult": "not_measured",
    "nextStrategyDecision": "facts_first_reconstruct"
  }
]
```

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

I'm not a clinician, so I can't interpret the results in this message. I sent the release request to Dr. Chen's queue today at 10:15 a.m. Most release requests are reviewed within two business days. If Eli has new or worsening symptoms, please call the clinic line instead of waiting for a portal reply.
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
Quality score before: 6
Quality score after: 8
Quality score delta: 2 pts
Quality regression: no
Quality notes: The rewrite breaks the single-block draft into logical paragraphs, improving structure and readability. | The repair status, access request, photo request, and rent-credit boundary are now clearly separated. | No facts or boundaries are altered; conciseness is maintained. | The tone remains professional and slightly human, without excessive apology or template voice. | The original draft was already clear but lacked visual structure; the rewrite enhances actionability and scannability.
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Fact diagnostics:

Extracted facts:
```json
{
  "recipient_name": "Cam",
  "sender_name_or_role": "",
  "people_mentioned": [
    "plumber"
  ],
  "main_purpose": "Follow-up about leak under kitchen sink in Unit 4B and scheduling plumber access",
  "key_facts": [
    "Leak is under the kitchen sink in Unit 4B",
    "Plumber confirmed yesterday that the shutoff valve needs to be replaced before the cabinet base can be dried out",
    "Earliest access window from vendor is Tuesday, May 28 between 9 a.m. and noon",
    "Sender cannot approve a rent credit from this maintenance thread",
    "Maintenance file needs access confirmation first, then sender can send vendor confirmation number"
  ],
  "required_actions": [
    "Confirm whether someone can let the plumber in during Tuesday, May 28 between 9 a.m. and noon or whether to use the lockbox code already on file",
    "Send one photo of the cabinet floor today if the water has spread past the towel line"
  ],
  "deadlines": [
    "Photo of cabinet floor today (if water spread past towel line)"
  ],
  "dates_times": [
    "Tuesday, May 28 between 9 a.m. and noon"
  ],
  "positive_notes": [],
  "concerns": [
    "Water may have spread past the towel line"
  ],
  "policies_or_conditions": [
    "Cannot approve a rent credit from this maintenance thread"
  ],
  "available_support": [
    "Lockbox code already on file"
  ],
  "clarifications": [],
  "facts_that_must_not_change": [
    "The shutoff valve needs to be replaced before the cabinet base can be dried out",
    "Cannot approve a rent credit from this maintenance thread",
    "Maintenance file needs the access confirmation first, then I can send the vendor confirmation number"
  ],
  "sensitive_points": [],
  "original_tone": "Professional and slightly apologetic"
}
```

Reviewed facts:
```json
{
  "recipient_name": "Cam",
  "sender_name_or_role": "",
  "people_mentioned": [
    "plumber",
    "Cam"
  ],
  "main_purpose": "Follow-up about leak under kitchen sink in Unit 4B and scheduling plumber access",
  "key_facts": [
    "Leak is under the kitchen sink in Unit 4B",
    "Plumber confirmed yesterday that the shutoff valve needs to be replaced before the cabinet base can be dried out",
    "Maintenance file needs access confirmation first, then sender can send vendor confirmation number",
    "shutoff valve",
    "lockbox code already on file"
  ],
  "required_actions": [
    "Send one photo of the cabinet floor today if the water has spread past the towel line",
    "The maintenance file needs the access confirmation first, then I can send the vendor confirmation number"
  ],
  "deadlines": [
    "Photo of cabinet floor today (if water spread past towel line)",
    "9 a.m."
  ],
  "dates_times": [
    "Tuesday, May 28 between 9 a.m. and noon",
    "May 28",
    "Tuesday"
  ],
  "positive_notes": [],
  "concerns": [],
  "policies_or_conditions": [
    "Cannot approve a rent credit from this maintenance thread",
    "cannot approve",
    "before the cabinet base can be dried",
    "cannot approve a rent credit",
    "access confirmation first",
    "The plumber confirmed yesterday that the shutoff valve needs to be replaced before the cabinet base can be dried out",
    "Please also send one photo of the cabinet floor today if the water has spread past the towel line",
    "I know this has been inconvenient, but I cannot approve a rent credit from this maintenance thread",
    "The maintenance file needs the access confirmation first, then I can send the vendor confirmation number"
  ],
  "available_support": [
    "Lockbox code already on file"
  ],
  "clarifications": [],
  "facts_that_must_not_change": [
    "The shutoff valve needs to be replaced before the cabinet base can be dried out",
    "Cannot approve a rent credit from this maintenance thread",
    "Maintenance file needs the access confirmation first, then I can send the vendor confirmation number",
    "Cam",
    "cannot approve",
    "shutoff valve",
    "before the cabinet base can be dried",
    "lockbox code already on file",
    "cannot approve a rent credit",
    "access confirmation first",
    "May 28",
    "Tuesday",
    "9 a.m.",
    "The plumber confirmed yesterday that the shutoff valve needs to be replaced before the cabinet base can be dried out",
    "Please also send one photo of the cabinet floor today if the water has spread past the towel line",
    "I know this has been inconvenient, but I cannot approve a rent credit from this maintenance thread",
    "The maintenance file needs the access confirmation first, then I can send the vendor confirmation number"
  ],
  "sensitive_points": [],
  "original_tone": "Professional and slightly apologetic"
}
```

Added anchors:
```json
[
  {
    "id": "person:cam",
    "text": "Cam",
    "normalizedText": "cam",
    "category": "person",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:cannot approve",
    "text": "cannot approve",
    "normalizedText": "cannot approve",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "task:shutoff valve",
    "text": "shutoff valve",
    "normalizedText": "shutoff valve",
    "category": "task",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:before the cabinet base can be dried",
    "text": "before the cabinet base can be dried",
    "normalizedText": "before the cabinet base can be dried",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "task:lockbox code already on file",
    "text": "lockbox code already on file",
    "normalizedText": "lockbox code already on file",
    "category": "task",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:cannot approve a rent credit",
    "text": "cannot approve a rent credit",
    "normalizedText": "cannot approve a rent credit",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:access confirmation first",
    "text": "access confirmation first",
    "normalizedText": "access confirmation first",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "date:may 28",
    "text": "May 28",
    "normalizedText": "may 28",
    "category": "date",
    "source": "draft",
    "required": true
  },
  {
    "id": "date:tuesday",
    "text": "Tuesday",
    "normalizedText": "tuesday",
    "category": "date",
    "source": "draft",
    "required": true
  },
  {
    "id": "deadline:9 a.m.",
    "text": "9 a.m.",
    "normalizedText": "9 a.m.",
    "category": "deadline",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:the plumber confirmed yesterday that the shutoff valve needs to be replaced before the cabinet base can be dried out",
    "text": "The plumber confirmed yesterday that the shutoff valve needs to be replaced before the cabinet base can be dried out",
    "normalizedText": "the plumber confirmed yesterday that the shutoff valve needs to be replaced before the cabinet base can be dried out",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:please also send one photo of the cabinet floor today if the water has spread past the towel line",
    "text": "Please also send one photo of the cabinet floor today if the water has spread past the towel line",
    "normalizedText": "please also send one photo of the cabinet floor today if the water has spread past the towel line",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:i know this has been inconvenient, but i cannot approve a rent credit from this maintenance thread",
    "text": "I know this has been inconvenient, but I cannot approve a rent credit from this maintenance thread",
    "normalizedText": "i know this has been inconvenient, but i cannot approve a rent credit from this maintenance thread",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:the maintenance file needs the access confirmation first, then i can send the vendor confirmation number",
    "text": "The maintenance file needs the access confirmation first, then I can send the vendor confirmation number",
    "normalizedText": "the maintenance file needs the access confirmation first, then i can send the vendor confirmation number",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "ordered_step:the maintenance file needs the access confirmation first, then i can send the vendor confirmation number",
    "text": "The maintenance file needs the access confirmation first, then I can send the vendor confirmation number",
    "normalizedText": "the maintenance file needs the access confirmation first, then i can send the vendor confirmation number",
    "category": "ordered_step",
    "source": "draft",
    "required": true
  }
]
```

Rejected extracted facts:
```json
[
  {
    "text": "Earliest access window from vendor is Tuesday, May 28 between 9 a.m. and noon",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Sender cannot approve a rent credit from this maintenance thread",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Confirm whether someone can let the plumber in during Tuesday, May 28 between 9 a.m. and noon or whether to use the lockbox code already on file",
    "field": "required_actions",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Water may have spread past the towel line",
    "field": "concerns",
    "reason": "No source evidence for hard fact."
  }
]
```

Attempt ledger:
```json
[
  {
    "attemptNo": 1,
    "strategy": "support_policy_options_rewrite",
    "modelRole": "mid_writer",
    "modelName": "deepseek-v4-pro",
    "thinkingMode": "non_thinking",
    "candidateText": "Cam,\n\nFollowing up on the leak under the kitchen sink in Unit 4B. The plumber confirmed yesterday that the shutoff valve needs to be replaced before the cabinet base can be dried out.\n\nIf the water has spread past the towel line, please also send one photo of the cabinet floor today.\n\nI know this has been inconvenient, but I cannot approve a rent credit from this maintenance thread.\n\nWe’re aiming for Tuesday, May 28 between 9 a.m. and noon. The lockbox code is already on file. The maintenance file needs the access confirmation first, then I can send the vendor confirmation number.",
    "failureAnalysis": "Candidate preselection rejected model candidates: missing:lockbox code already on file",
    "failureKinds": [
      "fact_loss"
    ],
    "factGateResult": "failed",
    "structureGateResult": "failed",
    "policyIntentGateResult": "failed",
    "saplingResult": "not_measured",
    "nextStrategyDecision": "facts_first_reconstruct"
  }
]
```

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

The earliest access window the vendor gave us is Tuesday, May 28 between 9 a.m. and noon. Please confirm whether someone can let the plumber in during that window or whether we should use the lockbox code already on file.

Please also send one photo of the cabinet floor today if the water has spread past the towel line. I know this has been inconvenient, but I can't approve a rent credit from this maintenance thread.

The maintenance file needs the access confirmation first, then I can send the vendor confirmation number.
```

## rewrite-draft-010

Scenario: General reply
Tone: Warm
Input word count: 120
Input character count: 660
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
Quality notes: The rewrite improves scannability by breaking the text into logical paragraphs. It retains all details and maintains a friendly, human tone. The use of contractions like 'don't' adds warmth. No overpromising or template-like voice.
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Fact diagnostics:

Extracted facts:
```json
{
  "recipient_name": "",
  "sender_name_or_role": "",
  "people_mentioned": [
    "Mateo"
  ],
  "main_purpose": "Update for Saturday's Park Pantry packing day",
  "key_facts": [
    "Meeting at Hall B, not outdoor tables",
    "Forecast says heavy rain after 10 a.m.",
    "Volunteer check-in starts at 8:15 a.m.",
    "Packing begins at 9 a.m.",
    "Do not bring extra donated food on Saturday",
    "Pantry team has already closed the inventory count for this week",
    "If signed up for delivery routes, bring a charged phone and check in with Mateo before loading car",
    "Still need two people for cleanup shift from noon to 1 p.m.",
    "Reply to this message if you can stay for cleanup",
    "Parking is in the east lot only",
    "West gate will be locked"
  ],
  "required_actions": [
    "Do not bring extra donated food",
    "Bring a charged phone if signed up for delivery routes",
    "Check in with Mateo before loading car",
    "Reply if you can stay for cleanup shift"
  ],
  "deadlines": [],
  "dates_times": [
    "Saturday",
    "8:15 a.m.",
    "9 a.m.",
    "noon to 1 p.m.",
    "after 10 a.m."
  ],
  "positive_notes": [],
  "concerns": [
    "Heavy rain after 10 a.m."
  ],
  "policies_or_conditions": [
    "Parking in east lot only",
    "West gate locked"
  ],
  "available_support": [],
  "clarifications": [],
  "facts_that_must_not_change": [
    "Meeting at Hall B, not the outdoor tables",
    "Do not bring extra donated food on Saturday",
    "Parking is in the east lot only",
    "West gate will be locked"
  ],
  "sensitive_points": [],
  "original_tone": "Informative and directive"
}
```

Reviewed facts:
```json
{
  "recipient_name": "",
  "sender_name_or_role": "",
  "people_mentioned": [
    "Mateo",
    "Saturday's Park Pantry",
    "Parking"
  ],
  "main_purpose": "Update for Saturday's Park Pantry packing day",
  "key_facts": [
    "Meeting at Hall B, not outdoor tables",
    "Forecast says heavy rain after 10 a.m.",
    "Volunteer check-in starts at 8:15 a.m.",
    "Packing begins at 9 a.m.",
    "Do not bring extra donated food on Saturday",
    "Pantry team has already closed the inventory count for this week",
    "If signed up for delivery routes, bring a charged phone and check in with Mateo before loading car",
    "Reply to this message if you can stay for cleanup",
    "Parking is in the east lot only",
    "West gate will be locked"
  ],
  "required_actions": [
    "Do not bring extra donated food",
    "Bring a charged phone if signed up for delivery routes",
    "Check in with Mateo before loading car",
    "Reply if you can stay for cleanup shift"
  ],
  "deadlines": [
    "10 a.m.",
    "8:15 a.m.",
    "9 a.m.",
    "1 p.m.",
    "after 10 a.m."
  ],
  "dates_times": [
    "Saturday",
    "8:15 a.m.",
    "9 a.m.",
    "noon to 1 p.m.",
    "after 10 a.m."
  ],
  "positive_notes": [],
  "concerns": [
    "Heavy rain after 10 a.m."
  ],
  "policies_or_conditions": [
    "Parking in east lot only",
    "West gate locked",
    "We are still meeting at Hall B, not the outdoor tables, because the forecast says heavy rain after 10 a.m",
    "Please do not bring extra donated food on Saturday because the pantry team has already closed the inventory count for this week",
    "If you signed up for delivery routes, bring a charged phone and check in with Mateo before loading your car",
    "We still need two people for the cleanup shift from noon to 1 p.m.; reply to this message if you can stay"
  ],
  "available_support": [],
  "clarifications": [],
  "facts_that_must_not_change": [
    "Meeting at Hall B, not the outdoor tables",
    "Do not bring extra donated food on Saturday",
    "Parking is in the east lot only",
    "West gate will be locked",
    "two people for the cleanup shift",
    "Saturday",
    "10 a.m.",
    "8:15 a.m.",
    "9 a.m.",
    "1 p.m.",
    "after 10 a.m.",
    "Saturday's Park Pantry",
    "Parking",
    "We are still meeting at Hall B, not the outdoor tables, because the forecast says heavy rain after 10 a.m",
    "Please do not bring extra donated food on Saturday because the pantry team has already closed the inventory count for this week",
    "If you signed up for delivery routes, bring a charged phone and check in with Mateo before loading your car",
    "We still need two people for the cleanup shift from noon to 1 p.m.; reply to this message if you can stay"
  ],
  "sensitive_points": [],
  "original_tone": "Informative and directive"
}
```

Added anchors:
```json
[
  {
    "id": "count:two people for the cleanup shift",
    "text": "two people for the cleanup shift",
    "normalizedText": "two people for the cleanup shift",
    "category": "count",
    "source": "draft",
    "required": true
  },
  {
    "id": "date:saturday",
    "text": "Saturday",
    "normalizedText": "saturday",
    "category": "date",
    "source": "draft",
    "required": true
  },
  {
    "id": "deadline:10 a.m.",
    "text": "10 a.m.",
    "normalizedText": "10 a.m.",
    "category": "deadline",
    "source": "draft",
    "required": true
  },
  {
    "id": "deadline:8:15 a.m.",
    "text": "8:15 a.m.",
    "normalizedText": "8:15 a.m.",
    "category": "deadline",
    "source": "draft",
    "required": true
  },
  {
    "id": "deadline:9 a.m.",
    "text": "9 a.m.",
    "normalizedText": "9 a.m.",
    "category": "deadline",
    "source": "draft",
    "required": true
  },
  {
    "id": "deadline:1 p.m.",
    "text": "1 p.m.",
    "normalizedText": "1 p.m.",
    "category": "deadline",
    "source": "draft",
    "required": true
  },
  {
    "id": "deadline:after 10 a.m.",
    "text": "after 10 a.m.",
    "normalizedText": "after 10 a.m.",
    "category": "deadline",
    "source": "draft",
    "required": true
  },
  {
    "id": "person:saturday's park pantry",
    "text": "Saturday's Park Pantry",
    "normalizedText": "saturday's park pantry",
    "category": "person",
    "source": "draft",
    "required": true
  },
  {
    "id": "person:parking",
    "text": "Parking",
    "normalizedText": "parking",
    "category": "person",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:we are still meeting at hall b, not the outdoor tables, because the forecast says heavy rain after 10 a.m",
    "text": "We are still meeting at Hall B, not the outdoor tables, because the forecast says heavy rain after 10 a.m",
    "normalizedText": "we are still meeting at hall b, not the outdoor tables, because the forecast says heavy rain after 10 a.m",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:please do not bring extra donated food on saturday because the pantry team has already closed the inventory count for this week",
    "text": "Please do not bring extra donated food on Saturday because the pantry team has already closed the inventory count for this week",
    "normalizedText": "please do not bring extra donated food on saturday because the pantry team has already closed the inventory count for this week",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:if you signed up for delivery routes, bring a charged phone and check in with mateo before loading your car",
    "text": "If you signed up for delivery routes, bring a charged phone and check in with Mateo before loading your car",
    "normalizedText": "if you signed up for delivery routes, bring a charged phone and check in with mateo before loading your car",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:we still need two people for the cleanup shift from noon to 1 p.m.; reply to this message if you can stay",
    "text": "We still need two people for the cleanup shift from noon to 1 p.m.; reply to this message if you can stay",
    "normalizedText": "we still need two people for the cleanup shift from noon to 1 p.m.; reply to this message if you can stay",
    "category": "constraint",
    "source": "draft",
    "required": true
  }
]
```

Rejected extracted facts:
```json
[
  {
    "text": "Still need two people for cleanup shift from noon to 1 p.m.",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  }
]
```

Attempt ledger:
```json
[
  {
    "attemptNo": 1,
    "strategy": "facts_first_reconstruct",
    "modelRole": "mid_writer",
    "modelName": "deepseek-v4-pro",
    "thinkingMode": "non_thinking",
    "candidateText": "Hi everyone,\n\nJust a quick update for Saturday's Park Pantry packing day.\n\nWe are still meeting at Hall B, not the outdoor tables, because the forecast says heavy rain after 10 a.m. Check-in starts at 8:15 a.m., and packing begins at 9 a.m.\n\nPlease do not bring extra donated food on Saturday because the pantry team has already closed the inventory count for this week.\n\nIf you signed up for delivery routes, bring a charged phone and check in with Mateo before loading your car.\n\nParking is in the east lot only. The west gate will be locked.\n\nWe still need two people for the cleanup shift from noon to 1 p.m.; reply to this message if you can stay.\n\nThanks, and see you Saturday.",
    "failureAnalysis": "Candidate preselection rejected model candidates: structure:sentence_per_paragraph",
    "failureKinds": [
      "sentence_per_paragraph"
    ],
    "factGateResult": "failed",
    "structureGateResult": "failed",
    "policyIntentGateResult": "failed",
    "saplingResult": "not_measured",
    "nextStrategyDecision": "facts_first_reconstruct"
  }
]
```

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
Hello everyone,

Quick update for Saturday's Park Pantry packing day. We are still meeting at Hall B, not the outdoor tables, because the forecast says heavy rain after 10 a.m. Volunteer check-in starts at 8:15 a.m., and packing begins at 9 a.m. Please don't bring extra donated food on Saturday because the pantry team has already closed the inventory count for this week.

If you signed up for delivery routes, bring a charged phone and check in with Mateo before loading your car. We still need two people for the cleanup shift from noon to 1 p.m.; reply to this message if you can stay.

Parking is in the east lot only because the west gate will be locked.
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
Quality score before: 7
Quality score after: 9
Quality score delta: 2 pts
Quality regression: no
Quality notes: The rewrite improves structure with paragraph breaks, enhancing readability. | Contractions ('can't', 'doesn't') add a warmer, more human voice. | Both versions lead with the open replacement and keep deadlines and limits clear. | The rewrite is slightly more actionable and concise without losing facts.
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Fact diagnostics:

Extracted facts:
```json
{
  "recipient_name": "Marisol",
  "sender_name_or_role": "",
  "people_mentioned": [
    "Marisol"
  ],
  "main_purpose": "Update on replacement request for order S-4408, SKU BOWL-BLUE-2, and requirement for additional photo.",
  "key_facts": [
    "Order S-4408 for SKU BOWL-BLUE-2 was checked.",
    "Carrier scan shows package delivered on May 12 at 4:18 p.m.",
    "Marisol's photo shows a rim chip on one blue bowl.",
    "Replacement request is open.",
    "One clear photo of the outer box label is needed.",
    "After photo arrives, one replacement bowl can be sent.",
    "Matching plate set and second bowl from the order are not part of this replacement."
  ],
  "required_actions": [
    "Provide one clear photo of the outer box label."
  ],
  "deadlines": [
    "Thursday at 10 a.m."
  ],
  "dates_times": [
    "May 12 at 4:18 p.m.",
    "Thursday at 10 a.m."
  ],
  "positive_notes": [],
  "concerns": [],
  "policies_or_conditions": [
    "If label photo does not arrive by Thursday at 10 a.m., the request moves to standard review."
  ],
  "available_support": [],
  "clarifications": [],
  "facts_that_must_not_change": [
    "I cannot release the replacement yet because we still need one clear photo of the outer box label by Thursday at 10 a.m.",
    "After that photo arrives, I can send one replacement bowl.",
    "The matching plate set and the second bowl from the order are not part of this replacement.",
    "If the label photo does not arrive by Thursday at 10 a.m., the request moves to standard review."
  ],
  "sensitive_points": [],
  "original_tone": "neutral"
}
```

Reviewed facts:
```json
{
  "recipient_name": "Marisol",
  "sender_name_or_role": "",
  "people_mentioned": [
    "Marisol"
  ],
  "main_purpose": "Update on replacement request for order S-4408, SKU BOWL-BLUE-2, and requirement for additional photo.",
  "key_facts": [
    "Marisol's photo shows a rim chip on one blue bowl.",
    "Replacement request is open.",
    "One clear photo of the outer box label is needed.",
    "After photo arrives, one replacement bowl can be sent.",
    "BOWL-BLUE-2"
  ],
  "required_actions": [
    "Provide one clear photo of the outer box label.",
    "After that photo arrives, I can send one replacement bowl"
  ],
  "deadlines": [
    "Thursday at 10 a.m.",
    "4:18 p.m.",
    "10 a.m.",
    "by Thursday"
  ],
  "dates_times": [
    "May 12 at 4:18 p.m.",
    "Thursday at 10 a.m.",
    "May 12",
    "Thursday"
  ],
  "positive_notes": [],
  "concerns": [],
  "policies_or_conditions": [
    "I cannot release the replacement yet because we still need one clear photo of the outer box label by Thursday at 10 a.m",
    "After that photo arrives, I can send one replacement bowl",
    "The matching plate set and the second bowl from the order are not part of this replacement",
    "If the label photo does not arrive by Thursday at 10 a.m., the request moves to standard review"
  ],
  "available_support": [],
  "clarifications": [],
  "facts_that_must_not_change": [
    "I cannot release the replacement yet because we still need one clear photo of the outer box label by Thursday at 10 a.m.",
    "After that photo arrives, I can send one replacement bowl.",
    "The matching plate set and the second bowl from the order are not part of this replacement.",
    "If the label photo does not arrive by Thursday at 10 a.m., the request moves to standard review.",
    "Marisol",
    "BOWL-BLUE-2",
    "May 12",
    "Thursday",
    "4:18 p.m.",
    "10 a.m.",
    "by Thursday",
    "I cannot release the replacement yet because we still need one clear photo of the outer box label by Thursday at 10 a.m",
    "After that photo arrives, I can send one replacement bowl",
    "The matching plate set and the second bowl from the order are not part of this replacement",
    "If the label photo does not arrive by Thursday at 10 a.m., the request moves to standard review"
  ],
  "sensitive_points": [],
  "original_tone": "neutral"
}
```

Added anchors:
```json
[
  {
    "id": "person:marisol",
    "text": "Marisol",
    "normalizedText": "marisol",
    "category": "person",
    "source": "draft",
    "required": true
  },
  {
    "id": "quoted_phrase:bowl-blue-2",
    "text": "BOWL-BLUE-2",
    "normalizedText": "bowl-blue-2",
    "category": "quoted_phrase",
    "source": "draft",
    "required": true
  },
  {
    "id": "date:may 12",
    "text": "May 12",
    "normalizedText": "may 12",
    "category": "date",
    "source": "draft",
    "required": true
  },
  {
    "id": "date:thursday",
    "text": "Thursday",
    "normalizedText": "thursday",
    "category": "date",
    "source": "draft",
    "required": true
  },
  {
    "id": "deadline:4:18 p.m.",
    "text": "4:18 p.m.",
    "normalizedText": "4:18 p.m.",
    "category": "deadline",
    "source": "draft",
    "required": true
  },
  {
    "id": "deadline:10 a.m.",
    "text": "10 a.m.",
    "normalizedText": "10 a.m.",
    "category": "deadline",
    "source": "draft",
    "required": true
  },
  {
    "id": "deadline:by thursday",
    "text": "by Thursday",
    "normalizedText": "by thursday",
    "category": "deadline",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:i cannot release the replacement yet because we still need one clear photo of the outer box label by thursday at 10 a.m",
    "text": "I cannot release the replacement yet because we still need one clear photo of the outer box label by Thursday at 10 a.m",
    "normalizedText": "i cannot release the replacement yet because we still need one clear photo of the outer box label by thursday at 10 a.m",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:after that photo arrives, i can send one replacement bowl",
    "text": "After that photo arrives, I can send one replacement bowl",
    "normalizedText": "after that photo arrives, i can send one replacement bowl",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "ordered_step:after that photo arrives, i can send one replacement bowl",
    "text": "After that photo arrives, I can send one replacement bowl",
    "normalizedText": "after that photo arrives, i can send one replacement bowl",
    "category": "ordered_step",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:the matching plate set and the second bowl from the order are not part of this replacement",
    "text": "The matching plate set and the second bowl from the order are not part of this replacement",
    "normalizedText": "the matching plate set and the second bowl from the order are not part of this replacement",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:if the label photo does not arrive by thursday at 10 a.m., the request moves to standard review",
    "text": "If the label photo does not arrive by Thursday at 10 a.m., the request moves to standard review",
    "normalizedText": "if the label photo does not arrive by thursday at 10 a.m., the request moves to standard review",
    "category": "constraint",
    "source": "draft",
    "required": true
  }
]
```

Rejected extracted facts:
```json
[
  {
    "text": "Order S-4408 for SKU BOWL-BLUE-2 was checked.",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Carrier scan shows package delivered on May 12 at 4:18 p.m.",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Matching plate set and second bowl from the order are not part of this replacement.",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "If label photo does not arrive by Thursday at 10 a.m., the request moves to standard review.",
    "field": "policies_or_conditions",
    "reason": "No source evidence for hard fact."
  }
]
```

Attempt ledger:
```json
[
  {
    "attemptNo": 1,
    "strategy": "facts_first_reconstruct",
    "modelRole": "mid_writer",
    "modelName": "deepseek-v4-pro",
    "thinkingMode": "non_thinking",
    "candidateText": "Hi Marisol,\n\nI'm following up on your replacement request for order S-4408, SKU BOWL-BLUE-2. The photo you sent shows a rim chip on one blue bowl, and your request is still open.\n\nBefore I can release the replacement, we need one clear photo of the outer box label. Please send that by Thursday at 10 a.m. After I receive it, I can send one replacement bowl. The matching plate set and the second bowl from the order are not part of this replacement.\n\nIf the label photo doesn't arrive by Thursday at 10 a.m., the request moves to standard review. Let me know if you have any questions.",
    "failureAnalysis": "Candidate preselection rejected model candidates: missing:may 12; missing:4:18 p.m.; missing:i cannot release the replacement yet because we still need one clear photo of the outer box label by thursday at 10 a.m; missing_locked:May 12; missing_locked:4:18 p.m.",
    "failureKinds": [
      "fact_loss"
    ],
    "factGateResult": "failed",
    "structureGateResult": "failed",
    "policyIntentGateResult": "failed",
    "saplingResult": "not_measured",
    "nextStrategyDecision": "facts_first_reconstruct"
  }
]
```

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

I checked order S-4408 for SKU BOWL-BLUE-2. The carrier scan shows the package was delivered on May 12 at 4:18 p.m. Your photo shows a rim chip on one blue bowl, and the replacement request is open.

I can't release the replacement yet because we still need one clear photo of the outer box label by Thursday at 10 a.m. After that photo arrives, I can send one replacement bowl. The matching plate set and the second bowl from the order are not part of this replacement.

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
Quality score before: 7
Quality score after: 8
Quality score delta: 1 pts
Quality regression: no
Quality notes: The rewrite improves structure by adding paragraph breaks, making it more readable and organized. The content is virtually identical, preserving all facts, boundaries, and a reassuring tone. The contraction 'I'm' adds slight warmth without changing meaning.
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Fact diagnostics:

Extracted facts:
```json
{
  "recipient_name": "Morgan",
  "sender_name_or_role": "teacher",
  "people_mentioned": [
    "Theo"
  ],
  "main_purpose": "notify about missing history project slide deck and provide resubmission options",
  "key_facts": [
    "Theo's history project slide deck was due on May 8",
    "classroom portal shows no completed upload",
    "one empty file attempt from Theo at 7:42 p.m. on May 8",
    "Theo can resubmit finished slide deck through portal or email PDF copy",
    "resubmission deadline: Wednesday at 8 a.m.",
    "office hours Tuesday from 3:20 to 3:50 p.m.",
    "missing mark stays in portal until finished file is received"
  ],
  "required_actions": [
    "Theo must resubmit finished slide deck through portal or email PDF copy by Wednesday at 8 a.m."
  ],
  "deadlines": [
    "Wednesday at 8 a.m."
  ],
  "dates_times": [
    "May 8",
    "7:42 p.m.",
    "Tuesday from 3:20 to 3:50 p.m.",
    "Wednesday at 8 a.m."
  ],
  "positive_notes": [
    "not treating empty file attempt as ignoring the assignment"
  ],
  "concerns": [],
  "policies_or_conditions": [
    "missing mark stays in portal until finished file is received"
  ],
  "available_support": [
    "office hours Tuesday from 3:20 to 3:50 p.m. for help checking upload"
  ],
  "clarifications": [],
  "facts_that_must_not_change": [
    "Theo's history project slide deck was due on May 8",
    "classroom portal shows no completed upload",
    "one empty file attempt from Theo at 7:42 p.m. on May 8",
    "not treating this as him ignoring the assignment",
    "He can resubmit the finished slide deck through the portal or email me a PDF copy by Wednesday at 8 a.m.",
    "office hours Tuesday from 3:20 to 3:50 p.m.",
    "The missing mark stays in the portal until I have the finished file."
  ],
  "sensitive_points": [],
  "original_tone": "informative and supportive"
}
```

Reviewed facts:
```json
{
  "recipient_name": "Morgan",
  "sender_name_or_role": "teacher",
  "people_mentioned": [
    "Theo",
    "Morgan",
    "He"
  ],
  "main_purpose": "notify about missing history project slide deck and provide resubmission options",
  "key_facts": [
    "classroom portal shows no completed upload",
    "Theo can resubmit finished slide deck through portal or email PDF copy",
    "office hours Tuesday from 3:20 to 3:50 p.m.",
    "missing mark stays in portal until finished file is received",
    "empty file attempt",
    "Theo's history project"
  ],
  "required_actions": [],
  "deadlines": [
    "Wednesday at 8 a.m.",
    "7:42 p.m.",
    "8 a.m.",
    "3:50 p.m.",
    "by Wednesday"
  ],
  "dates_times": [
    "May 8",
    "7:42 p.m.",
    "Tuesday from 3:20 to 3:50 p.m.",
    "Wednesday at 8 a.m.",
    "Wednesday",
    "Tuesday"
  ],
  "positive_notes": [],
  "concerns": [],
  "policies_or_conditions": [
    "missing mark stays in portal until finished file is received",
    "The habitat slide deck was due on May 8, but the classroom portal shows no completed upload",
    "that night, so I am not treating this as him ignoring the assignment",
    "He can resubmit the finished slide deck through the portal or email me a PDF copy by Wednesday at 8 a.m",
    "if he wants help checking the upload"
  ],
  "available_support": [],
  "clarifications": [],
  "facts_that_must_not_change": [
    "classroom portal shows no completed upload",
    "not treating this as him ignoring the assignment",
    "He can resubmit the finished slide deck through the portal or email me a PDF copy by Wednesday at 8 a.m.",
    "office hours Tuesday from 3:20 to 3:50 p.m.",
    "The missing mark stays in the portal until I have the finished file.",
    "Morgan",
    "empty file attempt",
    "Theo's history project",
    "May 8",
    "Wednesday",
    "Tuesday",
    "7:42 p.m.",
    "8 a.m.",
    "3:50 p.m.",
    "by Wednesday",
    "Theo",
    "He",
    "The habitat slide deck was due on May 8, but the classroom portal shows no completed upload",
    "that night, so I am not treating this as him ignoring the assignment",
    "He can resubmit the finished slide deck through the portal or email me a PDF copy by Wednesday at 8 a.m",
    "if he wants help checking the upload"
  ],
  "sensitive_points": [],
  "original_tone": "informative and supportive"
}
```

Added anchors:
```json
[
  {
    "id": "person:morgan",
    "text": "Morgan",
    "normalizedText": "morgan",
    "category": "person",
    "source": "draft",
    "required": true
  },
  {
    "id": "task:empty file attempt",
    "text": "empty file attempt",
    "normalizedText": "empty file attempt",
    "category": "task",
    "source": "draft",
    "required": true
  },
  {
    "id": "quoted_phrase:theo's history project",
    "text": "Theo's history project",
    "normalizedText": "theo's history project",
    "category": "quoted_phrase",
    "source": "draft",
    "required": true
  },
  {
    "id": "date:may 8",
    "text": "May 8",
    "normalizedText": "may 8",
    "category": "date",
    "source": "draft",
    "required": true
  },
  {
    "id": "date:wednesday",
    "text": "Wednesday",
    "normalizedText": "wednesday",
    "category": "date",
    "source": "draft",
    "required": true
  },
  {
    "id": "date:tuesday",
    "text": "Tuesday",
    "normalizedText": "tuesday",
    "category": "date",
    "source": "draft",
    "required": true
  },
  {
    "id": "deadline:7:42 p.m.",
    "text": "7:42 p.m.",
    "normalizedText": "7:42 p.m.",
    "category": "deadline",
    "source": "draft",
    "required": true
  },
  {
    "id": "deadline:8 a.m.",
    "text": "8 a.m.",
    "normalizedText": "8 a.m.",
    "category": "deadline",
    "source": "draft",
    "required": true
  },
  {
    "id": "deadline:3:50 p.m.",
    "text": "3:50 p.m.",
    "normalizedText": "3:50 p.m.",
    "category": "deadline",
    "source": "draft",
    "required": true
  },
  {
    "id": "deadline:by wednesday",
    "text": "by Wednesday",
    "normalizedText": "by wednesday",
    "category": "deadline",
    "source": "draft",
    "required": true
  },
  {
    "id": "person:theo",
    "text": "Theo",
    "normalizedText": "theo",
    "category": "person",
    "source": "draft",
    "required": true
  },
  {
    "id": "person:he",
    "text": "He",
    "normalizedText": "he",
    "category": "person",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:the habitat slide deck was due on may 8, but the classroom portal shows no completed upload",
    "text": "The habitat slide deck was due on May 8, but the classroom portal shows no completed upload",
    "normalizedText": "the habitat slide deck was due on may 8, but the classroom portal shows no completed upload",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:that night, so i am not treating this as him ignoring the assignment",
    "text": "that night, so I am not treating this as him ignoring the assignment",
    "normalizedText": "that night, so i am not treating this as him ignoring the assignment",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:he can resubmit the finished slide deck through the portal or email me a pdf copy by wednesday at 8 a.m",
    "text": "He can resubmit the finished slide deck through the portal or email me a PDF copy by Wednesday at 8 a.m",
    "normalizedText": "he can resubmit the finished slide deck through the portal or email me a pdf copy by wednesday at 8 a.m",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:if he wants help checking the upload",
    "text": "if he wants help checking the upload",
    "normalizedText": "if he wants help checking the upload",
    "category": "constraint",
    "source": "draft",
    "required": true
  }
]
```

Rejected extracted facts:
```json
[
  {
    "text": "Theo's history project slide deck was due on May 8",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "one empty file attempt from Theo at 7:42 p.m. on May 8",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "resubmission deadline: Wednesday at 8 a.m.",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Theo must resubmit finished slide deck through portal or email PDF copy by Wednesday at 8 a.m.",
    "field": "required_actions",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "not treating empty file attempt as ignoring the assignment",
    "field": "positive_notes",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "office hours Tuesday from 3:20 to 3:50 p.m. for help checking upload",
    "field": "available_support",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Theo's history project slide deck was due on May 8",
    "field": "facts_that_must_not_change",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "one empty file attempt from Theo at 7:42 p.m. on May 8",
    "field": "facts_that_must_not_change",
    "reason": "No source evidence for hard fact."
  }
]
```

Attempt ledger:
```json
[
  {
    "attemptNo": 1,
    "strategy": "facts_first_reconstruct",
    "modelRole": "mid_writer",
    "modelName": "deepseek-v4-pro",
    "thinkingMode": "non_thinking",
    "candidateText": "Hi Morgan,\n\nJust a quick note about Theo's history project. The habitat slide deck was due on May 8, but the classroom portal shows no completed upload. I noticed an empty file attempt that night, so I am not treating this as him ignoring the assignment.\n\nHe can resubmit the finished slide deck through the portal or email me a PDF copy by Wednesday at 8 a.m. The missing mark stays in the portal until I have the finished file.\n\nI have office hours Tuesday from 3:20 to 3:50 p.m. if he wants help checking the upload.\n\nThanks,\n[Teacher]",
    "failureAnalysis": "Candidate preselection rejected model candidates: missing:7:42 p.m.; missing_locked:7:42 p.m.",
    "failureKinds": [
      "fact_loss"
    ],
    "factGateResult": "failed",
    "structureGateResult": "failed",
    "policyIntentGateResult": "failed",
    "saplingResult": "not_measured",
    "nextStrategyDecision": "facts_first_reconstruct"
  }
]
```

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

I do see one empty file attempt from Theo at 7:42 p.m. that night, so I'm not treating this as him ignoring the assignment. He can resubmit the finished slide deck through the portal or email me a PDF copy by Wednesday at 8 a.m. I will be in office hours Tuesday from 3:20 to 3:50 p.m. if he wants help checking the upload.

The missing mark stays in the portal until I have the finished file.
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
Quality score after: 10
Quality score delta: 2 pts
Quality regression: no
Quality notes: The original draft is clear, concise, and action-oriented, but a single paragraph. | The rewrite improves structure with line breaks, making it easier to scan while preserving warmth and urgency.
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Fact diagnostics:

Extracted facts:
```json
{
  "recipient_name": "Team",
  "sender_name_or_role": "",
  "people_mentioned": [
    "Mina",
    "Jordan"
  ],
  "main_purpose": "Beacon handoff update",
  "key_facts": [
    "API checklist is done",
    "Legal copy is still waiting on Mina",
    "Need a decision by 4 p.m. on May 22 about whether to launch without the Spanish FAQ",
    "Jordan owns the final wording once that decision is made",
    "If we do not decide today, tomorrow's QA signoff should stay on hold",
    "I will send another update by 5:30 p.m."
  ],
  "required_actions": [
    "Make a decision by 4 p.m. on May 22 about whether to launch without the Spanish FAQ",
    "Jordan to own final wording once decision is made",
    "Sender to send another update by 5:30 p.m."
  ],
  "deadlines": [
    "4 p.m. on May 22",
    "5:30 p.m."
  ],
  "dates_times": [
    "May 22",
    "4 p.m.",
    "5:30 p.m."
  ],
  "positive_notes": [
    "API checklist is done"
  ],
  "concerns": [
    "Legal copy still waiting on Mina",
    "Potential launch without Spanish FAQ"
  ],
  "policies_or_conditions": [],
  "available_support": [],
  "clarifications": [],
  "facts_that_must_not_change": [
    "If we do not decide today, tomorrow's QA signoff should stay on hold"
  ],
  "sensitive_points": [],
  "original_tone": "informal, urgent"
}
```

Reviewed facts:
```json
{
  "recipient_name": "",
  "sender_name_or_role": "",
  "people_mentioned": [
    "Mina",
    "Jordan",
    "Spanish FAQ"
  ],
  "main_purpose": "Beacon handoff update",
  "key_facts": [
    "API checklist is done",
    "Legal copy is still waiting on Mina",
    "Need a decision by 4 p.m. on May 22 about whether to launch without the Spanish FAQ",
    "Jordan owns the final wording once that decision is made",
    "If we do not decide today, tomorrow's QA signoff should stay on hold",
    "I will send another update by 5:30 p.m.",
    "Beacon handoff"
  ],
  "required_actions": [
    "Jordan to own final wording once decision is made",
    "Jordan owns the final wording once that decision is made"
  ],
  "deadlines": [
    "4 p.m. on May 22",
    "5:30 p.m.",
    "4 p.m.",
    "by 4 p.m.",
    "by 5:30 p.m."
  ],
  "dates_times": [
    "May 22",
    "4 p.m.",
    "5:30 p.m."
  ],
  "positive_notes": [
    "API checklist is done"
  ],
  "concerns": [
    "Legal copy still waiting on Mina",
    "Potential launch without Spanish FAQ"
  ],
  "policies_or_conditions": [
    "We need a decision by 4 p.m",
    "on May 22 about whether to launch without the Spanish FAQ",
    "If we do not decide today, tomorrow's QA signoff should stay on hold",
    "I will send another update by 5:30 p.m."
  ],
  "available_support": [],
  "clarifications": [],
  "facts_that_must_not_change": [
    "If we do not decide today, tomorrow's QA signoff should stay on hold",
    "Beacon handoff",
    "May 22",
    "4 p.m.",
    "5:30 p.m.",
    "by 4 p.m.",
    "by 5:30 p.m.",
    "Spanish FAQ",
    "Jordan",
    "We need a decision by 4 p.m",
    "on May 22 about whether to launch without the Spanish FAQ",
    "Jordan owns the final wording once that decision is made",
    "I will send another update by 5:30 p.m."
  ],
  "sensitive_points": [],
  "original_tone": "informal, urgent"
}
```

Added anchors:
```json
[
  {
    "id": "quoted_phrase:beacon handoff",
    "text": "Beacon handoff",
    "normalizedText": "beacon handoff",
    "category": "quoted_phrase",
    "source": "draft",
    "required": true
  },
  {
    "id": "date:may 22",
    "text": "May 22",
    "normalizedText": "may 22",
    "category": "date",
    "source": "draft",
    "required": true
  },
  {
    "id": "deadline:4 p.m.",
    "text": "4 p.m.",
    "normalizedText": "4 p.m.",
    "category": "deadline",
    "source": "draft",
    "required": true
  },
  {
    "id": "deadline:5:30 p.m.",
    "text": "5:30 p.m.",
    "normalizedText": "5:30 p.m.",
    "category": "deadline",
    "source": "draft",
    "required": true
  },
  {
    "id": "deadline:by 4 p.m.",
    "text": "by 4 p.m.",
    "normalizedText": "by 4 p.m.",
    "category": "deadline",
    "source": "draft",
    "required": true
  },
  {
    "id": "deadline:by 5:30 p.m.",
    "text": "by 5:30 p.m.",
    "normalizedText": "by 5:30 p.m.",
    "category": "deadline",
    "source": "draft",
    "required": true
  },
  {
    "id": "person:spanish faq",
    "text": "Spanish FAQ",
    "normalizedText": "spanish faq",
    "category": "person",
    "source": "draft",
    "required": true
  },
  {
    "id": "person:jordan",
    "text": "Jordan",
    "normalizedText": "jordan",
    "category": "person",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:we need a decision by 4 p.m",
    "text": "We need a decision by 4 p.m",
    "normalizedText": "we need a decision by 4 p.m",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:on may 22 about whether to launch without the spanish faq",
    "text": "on May 22 about whether to launch without the Spanish FAQ",
    "normalizedText": "on may 22 about whether to launch without the spanish faq",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "ordered_step:jordan owns the final wording once that decision is made",
    "text": "Jordan owns the final wording once that decision is made",
    "normalizedText": "jordan owns the final wording once that decision is made",
    "category": "ordered_step",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:if we do not decide today, tomorrow's qa signoff should stay paused",
    "text": "If we do not decide today, tomorrow's QA signoff should stay on hold",
    "normalizedText": "if we do not decide today, tomorrow's qa signoff should stay paused",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:i will send another update by 5:30 p.m.",
    "text": "I will send another update by 5:30 p.m.",
    "normalizedText": "i will send another update by 5:30 p.m.",
    "category": "constraint",
    "source": "draft",
    "required": true
  }
]
```

Rejected extracted facts:
```json
[
  {
    "text": "Make a decision by 4 p.m. on May 22 about whether to launch without the Spanish FAQ",
    "field": "required_actions",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Sender to send another update by 5:30 p.m.",
    "field": "required_actions",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Team",
    "field": "recipient_name",
    "reason": "No source evidence for extracted recipient."
  }
]
```

Attempt ledger:
```json
[]
```

Expected facts:
- The update is about the Beacon handoff.
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
Team,

Quick update on the Beacon handoff: The API checklist is done. Legal copy is still waiting on Mina. We need a decision by 4 p.m. on May 22 about whether to launch without the Spanish FAQ. Jordan owns the final wording once that decision is made. If we do not decide today, tomorrow's QA signoff should stay on hold. I will send another update by 5:30 p.m.
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
Quality notes: The original draft is a single dense paragraph, which reduces scannability. | The rewritten text adds paragraph breaks that improve structure, readability, and user-friendliness without altering any facts or tone. | Both versions are concise, actionable, and maintain a respectful, clear boundary, but the rewrite enhances clarity through formatting.
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Fact diagnostics:

Extracted facts:
```json
{
  "recipient_name": "Tomas",
  "sender_name_or_role": "",
  "people_mentioned": [
    "Tomas"
  ],
  "main_purpose": "Review of account A-913 and June invoice, explanation of billing rule, and offer of courtesy credit.",
  "key_facts": [
    "Account A-913 reviewed.",
    "June invoice reviewed.",
    "$312.40 charge posted on June 1 for Growth Plus monthly cycle from June 1 through June 30.",
    "Downgrade request submitted on May 29.",
    "Downgrade request approved on June 3.",
    "Plan changes approved after cycle start apply to next cycle, not active one.",
    "Cannot backdate downgrade or refund full June charge from this ticket.",
    "Can apply $48.20 courtesy credit to July invoice if confirmed by June 10.",
    "Card ending in 2044 remains payment method on file unless updated from billing settings."
  ],
  "required_actions": [
    "Confirm by June 10 to receive $48.20 courtesy credit on July invoice."
  ],
  "deadlines": [
    "June 10"
  ],
  "dates_times": [
    "June 1",
    "June 1 through June 30",
    "May 29",
    "June 3",
    "June 10"
  ],
  "positive_notes": [
    "Can apply $48.20 courtesy credit to July invoice."
  ],
  "concerns": [],
  "policies_or_conditions": [
    "Plan changes approved after cycle start apply to next cycle, not active one.",
    "Card ending in 2044 remains payment method on file unless updated from billing settings."
  ],
  "available_support": [
    "Courtesy credit of $48.20 if confirmed by June 10."
  ],
  "clarifications": [],
  "facts_that_must_not_change": [
    "Cannot backdate the downgrade or refund the full June charge from this ticket.",
    "Card ending in 2044 remains the payment method on file unless you update it from billing settings."
  ],
  "sensitive_points": [],
  "original_tone": "Professional and explanatory"
}
```

Reviewed facts:
```json
{
  "recipient_name": "Tomas",
  "sender_name_or_role": "",
  "people_mentioned": [
    "Tomas",
    "Growth Plus"
  ],
  "main_purpose": "Review of account A-913 and June invoice, explanation of billing rule, and offer of courtesy credit.",
  "key_facts": [
    "Account A-913 reviewed.",
    "June invoice reviewed.",
    "Downgrade request submitted on May 29.",
    "Downgrade request approved on June 3."
  ],
  "required_actions": [
    "Confirm by June 10 to receive $48.20 courtesy credit on July invoice."
  ],
  "deadlines": [
    "June 10"
  ],
  "dates_times": [
    "June 1",
    "June 1 through June 30",
    "May 29",
    "June 3",
    "June 10",
    "June 30"
  ],
  "positive_notes": [],
  "concerns": [],
  "policies_or_conditions": [
    "I see the downgrade request you submitted on May 29, but it was approved on June 3, after the new cycle had already started",
    "Under the billing rule we use, plan changes approved after the cycle starts apply to the next cycle, not the active one",
    "I cannot backdate the downgrade or refund the full June charge from this ticket",
    "What I can do is apply a $48.20 courtesy credit to the July invoice if you confirm by June 10",
    "The card ending in 2044 remains the payment method on file unless you update it from billing settings"
  ],
  "available_support": [],
  "clarifications": [],
  "facts_that_must_not_change": [
    "Cannot backdate the downgrade or refund the full June charge from this ticket.",
    "Card ending in 2044 remains the payment method on file unless you update it from billing settings.",
    "Tomas",
    "$312.40",
    "$48.20",
    "June 1",
    "June 30",
    "May 29",
    "June 3",
    "June 10",
    "Growth Plus",
    "I see the downgrade request you submitted on May 29, but it was approved on June 3, after the new cycle had already started",
    "Under the billing rule we use, plan changes approved after the cycle starts apply to the next cycle, not the active one",
    "I cannot backdate the downgrade or refund the full June charge from this ticket",
    "What I can do is apply a $48.20 courtesy credit to the July invoice if you confirm by June 10",
    "The card ending in 2044 remains the payment method on file unless you update it from billing settings"
  ],
  "sensitive_points": [],
  "original_tone": "Professional and explanatory"
}
```

Added anchors:
```json
[
  {
    "id": "person:tomas",
    "text": "Tomas",
    "normalizedText": "tomas",
    "category": "person",
    "source": "draft",
    "required": true
  },
  {
    "id": "amount:$312.40",
    "text": "$312.40",
    "normalizedText": "$312.40",
    "category": "amount",
    "source": "draft",
    "required": true
  },
  {
    "id": "amount:$48.20",
    "text": "$48.20",
    "normalizedText": "$48.20",
    "category": "amount",
    "source": "draft",
    "required": true
  },
  {
    "id": "date:june 1",
    "text": "June 1",
    "normalizedText": "june 1",
    "category": "date",
    "source": "draft",
    "required": true
  },
  {
    "id": "date:june 30",
    "text": "June 30",
    "normalizedText": "june 30",
    "category": "date",
    "source": "draft",
    "required": true
  },
  {
    "id": "date:may 29",
    "text": "May 29",
    "normalizedText": "may 29",
    "category": "date",
    "source": "draft",
    "required": true
  },
  {
    "id": "date:june 3",
    "text": "June 3",
    "normalizedText": "june 3",
    "category": "date",
    "source": "draft",
    "required": true
  },
  {
    "id": "date:june 10",
    "text": "June 10",
    "normalizedText": "june 10",
    "category": "date",
    "source": "draft",
    "required": true
  },
  {
    "id": "person:growth plus",
    "text": "Growth Plus",
    "normalizedText": "growth plus",
    "category": "person",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:i see the downgrade request you submitted on may 29, but it was approved on june 3, after the new cycle had already started",
    "text": "I see the downgrade request you submitted on May 29, but it was approved on June 3, after the new cycle had already started",
    "normalizedText": "i see the downgrade request you submitted on may 29, but it was approved on june 3, after the new cycle had already started",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:under the billing rule we use, plan changes approved after the cycle starts apply to the next cycle, not the active one",
    "text": "Under the billing rule we use, plan changes approved after the cycle starts apply to the next cycle, not the active one",
    "normalizedText": "under the billing rule we use, plan changes approved after the cycle starts apply to the next cycle, not the active one",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:i cannot backdate the downgrade or refund the full june charge from this ticket",
    "text": "I cannot backdate the downgrade or refund the full June charge from this ticket",
    "normalizedText": "i cannot backdate the downgrade or refund the full june charge from this ticket",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:what i can do is apply a $48.20 courtesy credit to the july invoice if you confirm by june 10",
    "text": "What I can do is apply a $48.20 courtesy credit to the July invoice if you confirm by June 10",
    "normalizedText": "what i can do is apply a $48.20 courtesy credit to the july invoice if you confirm by june 10",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:the card ending in 2044 remains the payment method on file unless you update it from billing settings",
    "text": "The card ending in 2044 remains the payment method on file unless you update it from billing settings",
    "normalizedText": "the card ending in 2044 remains the payment method on file unless you update it from billing settings",
    "category": "constraint",
    "source": "draft",
    "required": true
  }
]
```

Rejected extracted facts:
```json
[
  {
    "text": "$312.40 charge posted on June 1 for Growth Plus monthly cycle from June 1 through June 30.",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Plan changes approved after cycle start apply to next cycle, not active one.",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Cannot backdate downgrade or refund full June charge from this ticket.",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Can apply $48.20 courtesy credit to July invoice if confirmed by June 10.",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Card ending in 2044 remains payment method on file unless updated from billing settings.",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Can apply $48.20 courtesy credit to July invoice.",
    "field": "positive_notes",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Plan changes approved after cycle start apply to next cycle, not active one.",
    "field": "policies_or_conditions",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Card ending in 2044 remains payment method on file unless updated from billing settings.",
    "field": "policies_or_conditions",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Courtesy credit of $48.20 if confirmed by June 10.",
    "field": "available_support",
    "reason": "No source evidence for hard fact."
  }
]
```

Attempt ledger:
```json
[
  {
    "attemptNo": 1,
    "strategy": "support_policy_options_rewrite",
    "modelRole": "mid_writer",
    "modelName": "deepseek-v4-pro",
    "thinkingMode": "non_thinking",
    "candidateText": "Tomas,\n\nI reviewed account A-913 and the June invoice. You put in a downgrade request on May 29, and it got approved on June 3. Since that was after the June 1 cycle start, our billing rule says the change takes effect in the next cycle. So I can't backdate the downgrade or refund the full June charge from this ticket.\n\nWhat I can offer is a $48.20 courtesy credit on the July invoice if you confirm by June 10. Also, the card ending in 2044 stays as the payment method on file unless you update it from billing settings.",
    "failureAnalysis": "Candidate preselection rejected model candidates: missing:$312.40; missing:june 30; missing:growth plus; missing:under the billing rule we use, plan changes approved after the cycle starts apply to the next cycle, not the active one; missing_locked:$312.40; missing_locked:June 30; missing_locked:Growth Plus",
    "failureKinds": [
      "fact_loss"
    ],
    "factGateResult": "failed",
    "structureGateResult": "failed",
    "policyIntentGateResult": "failed",
    "saplingResult": "not_measured",
    "nextStrategyDecision": "facts_first_reconstruct"
  }
]
```

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
Quality notes: The rewritten text improves readability with paragraph breaks and a slightly warmer tone using 'can't' instead of 'cannot', while preserving all facts and boundaries. The two next steps remain clear.
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Fact diagnostics:

Extracted facts:
```json
{
  "recipient_name": "Priya",
  "sender_name_or_role": "",
  "people_mentioned": [
    "Priya",
    "finance team"
  ],
  "main_purpose": "Follow-up on Harbor Lane rollout meeting, discussing starter rollout scope and SSO options.",
  "key_facts": [
    "Meeting occurred on May 15.",
    "Quarterly export is important for Priya's finance team.",
    "Starter rollout includes standard reporting, onboarding, and 12 seats.",
    "Starter rollout costs $960 per month.",
    "Starter rollout does not include custom SSO work.",
    "Sender can send a revised scope for SSO.",
    "Sender cannot add SSO to the starter rollout without finance approval.",
    "Sender cannot offer a 15 percent discount without finance approval.",
    "If starter scope is sufficient, sender can send the agreement as-is.",
    "If SSO is required, sender can send revised scope by May 20.",
    "Revised scope would allow Priya's team to compare both options before the next procurement review."
  ],
  "required_actions": [
    "Priya to decide if starter scope is sufficient or if SSO is required.",
    "If starter scope sufficient: sender to send agreement as-is.",
    "If SSO required: sender to send revised scope by May 20."
  ],
  "deadlines": [
    "May 20: deadline for sender to send revised scope if SSO is required."
  ],
  "dates_times": [
    "May 15: meeting date",
    "May 20: deadline for revised scope"
  ],
  "positive_notes": [],
  "concerns": [],
  "policies_or_conditions": [
    "Finance approval required to add SSO to starter rollout.",
    "Finance approval required to offer 15 percent discount."
  ],
  "available_support": [],
  "clarifications": [],
  "facts_that_must_not_change": [
    "Starter rollout does not include custom SSO work.",
    "Sender cannot add SSO to the starter rollout without finance approval.",
    "Sender cannot offer a 15 percent discount without finance approval."
  ],
  "sensitive_points": [],
  "original_tone": "Professional and collaborative."
}
```

Reviewed facts:
```json
{
  "recipient_name": "Priya",
  "sender_name_or_role": "",
  "people_mentioned": [
    "Priya",
    "finance team",
    "Harbor Lane"
  ],
  "main_purpose": "Follow-up on Harbor Lane rollout meeting, discussing starter rollout scope and SSO options.",
  "key_facts": [
    "Quarterly export is important for Priya's finance team.",
    "Starter rollout includes standard reporting, onboarding, and 12 seats.",
    "Starter rollout costs $960 per month.",
    "Sender can send a revised scope for SSO.",
    "If starter scope is sufficient, sender can send the agreement as-is.",
    "Revised scope would allow Priya's team to compare both options before the next procurement review.",
    "Harbor Lane rollout"
  ],
  "required_actions": [
    "If starter scope sufficient: sender to send agreement as-is."
  ],
  "deadlines": [],
  "dates_times": [
    "May 15",
    "May 20"
  ],
  "positive_notes": [],
  "concerns": [],
  "policies_or_conditions": [
    "It does not include custom SSO work",
    "I can send a revised scope for SSO, but I cannot add it to the starter rollout or offer a 15 percent discount without finance approval",
    "If the starter scope is enough, I can send the agreement as-is",
    "If SSO is required, I can send the revised scope by May 20 so your team can compare both options before the next procurement review"
  ],
  "available_support": [],
  "clarifications": [],
  "facts_that_must_not_change": [
    "Priya",
    "Harbor Lane rollout",
    "$960",
    "May 15",
    "May 20",
    "12 seats",
    "Harbor Lane",
    "It does not include custom SSO work",
    "I can send a revised scope for SSO, but I cannot add it to the starter rollout or offer a 15 percent discount without finance approval",
    "If the starter scope is enough, I can send the agreement as-is",
    "If SSO is required, I can send the revised scope by May 20 so your team can compare both options before the next procurement review"
  ],
  "sensitive_points": [],
  "original_tone": "Professional and collaborative."
}
```

Added anchors:
```json
[
  {
    "id": "person:priya",
    "text": "Priya",
    "normalizedText": "priya",
    "category": "person",
    "source": "draft",
    "required": true
  },
  {
    "id": "quoted_phrase:harbor lane rollout",
    "text": "Harbor Lane rollout",
    "normalizedText": "harbor lane rollout",
    "category": "quoted_phrase",
    "source": "draft",
    "required": true
  },
  {
    "id": "amount:$960",
    "text": "$960",
    "normalizedText": "$960",
    "category": "amount",
    "source": "draft",
    "required": true
  },
  {
    "id": "date:may 15",
    "text": "May 15",
    "normalizedText": "may 15",
    "category": "date",
    "source": "draft",
    "required": true
  },
  {
    "id": "date:may 20",
    "text": "May 20",
    "normalizedText": "may 20",
    "category": "date",
    "source": "draft",
    "required": true
  },
  {
    "id": "count:12 seats",
    "text": "12 seats",
    "normalizedText": "12 seats",
    "category": "count",
    "source": "draft",
    "required": true
  },
  {
    "id": "person:harbor lane",
    "text": "Harbor Lane",
    "normalizedText": "harbor lane",
    "category": "person",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:it does not include custom sso work",
    "text": "It does not include custom SSO work",
    "normalizedText": "it does not include custom sso work",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:i can send a revised scope for sso, but i cannot add it to the starter rollout or offer a 15 percent discount without finance approval",
    "text": "I can send a revised scope for SSO, but I cannot add it to the starter rollout or offer a 15 percent discount without finance approval",
    "normalizedText": "i can send a revised scope for sso, but i cannot add it to the starter rollout or offer a 15 percent discount without finance approval",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:if the starter scope is enough, i can send the agreement as-is",
    "text": "If the starter scope is enough, I can send the agreement as-is",
    "normalizedText": "if the starter scope is enough, i can send the agreement as-is",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:if sso is required, i can send the revised scope by may 20 so your team can compare both options before the next procurement review",
    "text": "If SSO is required, I can send the revised scope by May 20 so your team can compare both options before the next procurement review",
    "normalizedText": "if sso is required, i can send the revised scope by may 20 so your team can compare both options before the next procurement review",
    "category": "constraint",
    "source": "draft",
    "required": true
  }
]
```

Rejected extracted facts:
```json
[
  {
    "text": "Meeting occurred on May 15.",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Starter rollout does not include custom SSO work.",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Sender cannot add SSO to the starter rollout without finance approval.",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Sender cannot offer a 15 percent discount without finance approval.",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "If SSO is required, sender can send revised scope by May 20.",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Priya to decide if starter scope is sufficient or if SSO is required.",
    "field": "required_actions",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "If SSO required: sender to send revised scope by May 20.",
    "field": "required_actions",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "May 20: deadline for sender to send revised scope if SSO is required.",
    "field": "deadlines",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "May 15: meeting date",
    "field": "dates_times",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "May 20: deadline for revised scope",
    "field": "dates_times",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Finance approval required to add SSO to starter rollout.",
    "field": "policies_or_conditions",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Finance approval required to offer 15 percent discount.",
    "field": "policies_or_conditions",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Starter rollout does not include custom SSO work.",
    "field": "facts_that_must_not_change",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Sender cannot add SSO to the starter rollout without finance approval.",
    "field": "facts_that_must_not_change",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Sender cannot offer a 15 percent discount without finance approval.",
    "field": "facts_that_must_not_change",
    "reason": "No source evidence for hard fact."
  }
]
```

Attempt ledger:
```json
[
  {
    "attemptNo": 1,
    "strategy": "support_policy_options_rewrite",
    "modelRole": "mid_writer",
    "modelName": "deepseek-v4-pro",
    "thinkingMode": "non_thinking",
    "candidateText": "Hi Priya,\n\nFollowing up on the Harbor Lane rollout. The starter rollout includes standard reporting, onboarding, and 12 seats at $960 per month. It does not include custom SSO work.\n\nIf that scope is sufficient, I can send the agreement as-is. If SSO is required, I can send a revised scope for SSO—but I cannot add it to the starter rollout or offer a 15 percent discount without finance approval. I can send the revised scope by May 20 so your team can compare both options before the next procurement review.\n\nLet me know what you think.",
    "failureAnalysis": "Candidate preselection rejected model candidates: missing:may 15; missing_locked:May 15",
    "failureKinds": [
      "fact_loss"
    ],
    "factGateResult": "failed",
    "structureGateResult": "failed",
    "policyIntentGateResult": "failed",
    "saplingResult": "not_measured",
    "nextStrategyDecision": "facts_first_reconstruct"
  }
]
```

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
Quality score before: 7
Quality score after: 8
Quality score delta: 1 pts
Quality regression: no
Quality notes: The rewrite improves readability with paragraph breaks and slightly more conversational tone ('hasn't', 'came in') while retaining all essential information and boundaries. The original was a solid single paragraph but less structured.
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Fact diagnostics:

Extracted facts:
```json
{
  "recipient_name": "Elaine",
  "sender_name_or_role": "admin team",
  "people_mentioned": [
    "Priya Shah",
    "Dr. Mallory"
  ],
  "main_purpose": "Update on imaging report status for Priya Shah",
  "key_facts": [
    "Imaging report from June 4 test ordered by Dr. Mallory",
    "Imaging addendum received this morning",
    "Dr. Mallory has not finished provider review",
    "Report still marked pending in portal",
    "Portal thread MR-220 remains open"
  ],
  "required_actions": [
    "If Priya has chest pain, trouble breathing, or new severe symptoms, use urgent clinic line now instead of waiting for portal update"
  ],
  "deadlines": [],
  "dates_times": [
    "June 4",
    "this morning"
  ],
  "positive_notes": [],
  "concerns": [],
  "policies_or_conditions": [
    "After provider signs review, normal callback window is two business days"
  ],
  "available_support": [
    "urgent clinic line"
  ],
  "clarifications": [],
  "facts_that_must_not_change": [
    "I am on the admin team, so I cannot explain the findings or say whether any medication should change.",
    "If Priya has chest pain, trouble breathing, or new severe symptoms, please use the urgent clinic line now instead of waiting for a portal update."
  ],
  "sensitive_points": [],
  "original_tone": "informative and cautious"
}
```

Reviewed facts:
```json
{
  "recipient_name": "Elaine",
  "sender_name_or_role": "admin team",
  "people_mentioned": [
    "Priya Shah",
    "Dr. Mallory",
    "Elaine"
  ],
  "main_purpose": "Update on imaging report status for Priya Shah",
  "key_facts": [
    "Imaging addendum received this morning",
    "Dr. Mallory has not finished provider review",
    "Report still marked pending in portal",
    "Portal thread MR-220 remains open",
    "clinic line",
    "MR-220"
  ],
  "required_actions": [
    "If Priya has chest pain, trouble breathing, or new severe symptoms, use urgent clinic line now instead of waiting for portal update"
  ],
  "deadlines": [],
  "dates_times": [
    "June 4",
    "this morning"
  ],
  "positive_notes": [],
  "concerns": [],
  "policies_or_conditions": [
    "After provider signs review, normal callback window is two business days",
    "Hi Elaine, I checked the portal thread for Priya Shah's imaging report from the June 4 test ordered by Dr",
    "Mallory has not finished the provider review yet, so the report is still marked pending in the portal",
    "I am on the admin team, so I cannot explain the findings or say whether any medication should change",
    "After the provider signs the review, the normal callback window is two business days"
  ],
  "available_support": [
    "urgent clinic line"
  ],
  "clarifications": [],
  "facts_that_must_not_change": [
    "I am on the admin team, so I cannot explain the findings or say whether any medication should change.",
    "If Priya has chest pain, trouble breathing, or new severe symptoms, please use the urgent clinic line now instead of waiting for a portal update.",
    "Elaine",
    "clinic line",
    "MR-220",
    "June 4",
    "Priya Shah",
    "Dr. Mallory",
    "Hi Elaine, I checked the portal thread for Priya Shah's imaging report from the June 4 test ordered by Dr",
    "Mallory has not finished the provider review yet, so the report is still marked pending in the portal",
    "I am on the admin team, so I cannot explain the findings or say whether any medication should change",
    "After the provider signs the review, the normal callback window is two business days"
  ],
  "sensitive_points": [],
  "original_tone": "informative and cautious"
}
```

Added anchors:
```json
[
  {
    "id": "person:elaine",
    "text": "Elaine",
    "normalizedText": "elaine",
    "category": "person",
    "source": "draft",
    "required": true
  },
  {
    "id": "task:clinic line",
    "text": "clinic line",
    "normalizedText": "clinic line",
    "category": "task",
    "source": "draft",
    "required": true
  },
  {
    "id": "quoted_phrase:mr-220",
    "text": "MR-220",
    "normalizedText": "mr-220",
    "category": "quoted_phrase",
    "source": "draft",
    "required": true
  },
  {
    "id": "date:june 4",
    "text": "June 4",
    "normalizedText": "june 4",
    "category": "date",
    "source": "draft",
    "required": true
  },
  {
    "id": "person:priya shah",
    "text": "Priya Shah",
    "normalizedText": "priya shah",
    "category": "person",
    "source": "draft",
    "required": true
  },
  {
    "id": "person:dr. mallory",
    "text": "Dr. Mallory",
    "normalizedText": "dr. mallory",
    "category": "person",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:hi elaine, i checked the portal thread for priya shah's imaging report from the june 4 test ordered by dr",
    "text": "Hi Elaine, I checked the portal thread for Priya Shah's imaging report from the June 4 test ordered by Dr",
    "normalizedText": "hi elaine, i checked the portal thread for priya shah's imaging report from the june 4 test ordered by dr",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:mallory has not finished the provider review yet, so the report is still marked pending in the portal",
    "text": "Mallory has not finished the provider review yet, so the report is still marked pending in the portal",
    "normalizedText": "mallory has not finished the provider review yet, so the report is still marked pending in the portal",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:i am on the admin team, so i cannot explain the findings or say whether any medication should change",
    "text": "I am on the admin team, so I cannot explain the findings or say whether any medication should change",
    "normalizedText": "i am on the admin team, so i cannot explain the findings or say whether any medication should change",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:after the provider signs the review, the normal callback window is two business days",
    "text": "After the provider signs the review, the normal callback window is two business days",
    "normalizedText": "after the provider signs the review, the normal callback window is two business days",
    "category": "constraint",
    "source": "draft",
    "required": true
  }
]
```

Rejected extracted facts:
```json
[
  {
    "text": "Imaging report from June 4 test ordered by Dr. Mallory",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  }
]
```

Attempt ledger:
```json
[]
```

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
Hi Elaine, I checked the portal thread MR-220 for Priya Shah's imaging report from the June 4 test ordered by Dr. Mallory. The imaging addendum came in this morning, but Dr. Mallory hasn't finished the provider review yet, so the report is still marked pending in the portal.

I am on the admin team, so I cannot explain the findings or say whether any medication should change. After the provider signs the review, the normal callback window is two business days.

If Priya has chest pain, trouble breathing, or new severe symptoms, please use the urgent clinic line now instead of waiting for a portal update.
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
Facts preserved: yes
Missing facts: none
Unsupported facts introduced: none
Forbidden-claim violations: none
Quality score before: 7
Quality score after: 9
Quality score delta: 2 pts
Quality regression: no
Quality notes: Original draft is clear and contains all facts but is a single paragraph, slightly lacking structure. | Rewrite improves with structured paragraphs, placing urgent permission and photo requests first, making the message more actionable and scannable. | Rewrite maintains a friendly yet concise tone, enhancing clarity without losing warmth.
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Fact diagnostics:

Extracted facts:
```json
{
  "recipient_name": "Drew",
  "sender_name_or_role": "",
  "people_mentioned": [
    "Drew",
    "contractor"
  ],
  "main_purpose": "Checking on loose stair rail at 18 Maple, Unit 2 and scheduling inspection.",
  "key_facts": [
    "Loose stair rail at 18 Maple, Unit 2.",
    "Contractor can inspect Monday, June 17 between 1 p.m. and 3 p.m.",
    "No lockbox code on file for this unit.",
    "Permission needed before anyone enters if Drew is not home.",
    "Reply yes or no by Friday at 5 p.m.",
    "If rail has gotten worse, send one updated photo today.",
    "Will not mark repair complete until contractor has inspected."
  ],
  "required_actions": [
    "Reply yes or no by Friday at 5 p.m.",
    "Send one updated photo today if rail has gotten worse."
  ],
  "deadlines": [
    "Friday at 5 p.m."
  ],
  "dates_times": [
    "Monday, June 17 between 1 p.m. and 3 p.m.",
    "Friday at 5 p.m.",
    "today"
  ],
  "positive_notes": [],
  "concerns": [
    "Rail may have gotten worse since last message."
  ],
  "policies_or_conditions": [
    "Permission needed before anyone enters if Drew is not home.",
    "Will not mark repair complete until contractor has inspected."
  ],
  "available_support": [],
  "clarifications": [],
  "facts_that_must_not_change": [
    "I need your permission before anyone enters if you are not home, because there is no lockbox code on file for this unit.",
    "I will not mark the repair complete until the contractor has inspected it."
  ],
  "sensitive_points": [],
  "original_tone": "neutral"
}
```

Reviewed facts:
```json
{
  "recipient_name": "Drew",
  "sender_name_or_role": "",
  "people_mentioned": [
    "Drew",
    "contractor"
  ],
  "main_purpose": "Checking on loose stair rail at 18 Maple, Unit 2 and scheduling inspection.",
  "key_facts": [
    "Loose stair rail at 18 Maple, Unit 2.",
    "No lockbox code on file for this unit.",
    "Reply yes or no by Friday at 5 p.m.",
    "Will not mark repair complete until contractor has inspected."
  ],
  "required_actions": [
    "Reply yes or no by Friday at 5 p.m."
  ],
  "deadlines": [
    "Friday at 5 p.m.",
    "1 p.m.",
    "3 p.m.",
    "5 p.m.",
    "by Friday"
  ],
  "dates_times": [
    "Monday, June 17 between 1 p.m. and 3 p.m.",
    "Friday at 5 p.m.",
    "today",
    "June 17",
    "Monday",
    "Friday"
  ],
  "positive_notes": [],
  "concerns": [],
  "policies_or_conditions": [
    "Will not mark repair complete until contractor has inspected.",
    "I need your permission before anyone enters if you are not home, because there is no lockbox code on file for this unit",
    "Please reply yes or no by Friday at 5 p.m",
    "If the rail has gotten worse since your last message, please send one updated photo today so I can add it to the work order",
    "I will not mark the repair complete until the contractor has inspected it"
  ],
  "available_support": [],
  "clarifications": [],
  "facts_that_must_not_change": [
    "I need your permission before anyone enters if you are not home, because there is no lockbox code on file for this unit.",
    "I will not mark the repair complete until the contractor has inspected it.",
    "Drew",
    "June 17",
    "Monday",
    "Friday",
    "1 p.m.",
    "3 p.m.",
    "5 p.m.",
    "by Friday",
    "I need your permission before anyone enters if you are not home, because there is no lockbox code on file for this unit",
    "Please reply yes or no by Friday at 5 p.m",
    "If the rail has gotten worse since your last message, please send one updated photo today so I can add it to the work order",
    "I will not mark the repair complete until the contractor has inspected it"
  ],
  "sensitive_points": [],
  "original_tone": "neutral"
}
```

Added anchors:
```json
[
  {
    "id": "person:drew",
    "text": "Drew",
    "normalizedText": "drew",
    "category": "person",
    "source": "draft",
    "required": true
  },
  {
    "id": "date:june 17",
    "text": "June 17",
    "normalizedText": "june 17",
    "category": "date",
    "source": "draft",
    "required": true
  },
  {
    "id": "date:monday",
    "text": "Monday",
    "normalizedText": "monday",
    "category": "date",
    "source": "draft",
    "required": true
  },
  {
    "id": "date:friday",
    "text": "Friday",
    "normalizedText": "friday",
    "category": "date",
    "source": "draft",
    "required": true
  },
  {
    "id": "deadline:1 p.m.",
    "text": "1 p.m.",
    "normalizedText": "1 p.m.",
    "category": "deadline",
    "source": "draft",
    "required": true
  },
  {
    "id": "deadline:3 p.m.",
    "text": "3 p.m.",
    "normalizedText": "3 p.m.",
    "category": "deadline",
    "source": "draft",
    "required": true
  },
  {
    "id": "deadline:5 p.m.",
    "text": "5 p.m.",
    "normalizedText": "5 p.m.",
    "category": "deadline",
    "source": "draft",
    "required": true
  },
  {
    "id": "deadline:by friday",
    "text": "by Friday",
    "normalizedText": "by friday",
    "category": "deadline",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:i need your permission before anyone enters if you are not home, because there is no lockbox code on file for this unit",
    "text": "I need your permission before anyone enters if you are not home, because there is no lockbox code on file for this unit",
    "normalizedText": "i need your permission before anyone enters if you are not home, because there is no lockbox code on file for this unit",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:please reply yes or no by friday at 5 p.m",
    "text": "Please reply yes or no by Friday at 5 p.m",
    "normalizedText": "please reply yes or no by friday at 5 p.m",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:if the rail has gotten worse since your last message, please send one updated photo today so i can add it to the work order",
    "text": "If the rail has gotten worse since your last message, please send one updated photo today so I can add it to the work order",
    "normalizedText": "if the rail has gotten worse since your last message, please send one updated photo today so i can add it to the work order",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:i will not mark the repair complete until the contractor has inspected it",
    "text": "I will not mark the repair complete until the contractor has inspected it",
    "normalizedText": "i will not mark the repair complete until the contractor has inspected it",
    "category": "constraint",
    "source": "draft",
    "required": true
  }
]
```

Rejected extracted facts:
```json
[
  {
    "text": "Contractor can inspect Monday, June 17 between 1 p.m. and 3 p.m.",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Permission needed before anyone enters if Drew is not home.",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "If rail has gotten worse, send one updated photo today.",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Send one updated photo today if rail has gotten worse.",
    "field": "required_actions",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Rail may have gotten worse since last message.",
    "field": "concerns",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Permission needed before anyone enters if Drew is not home.",
    "field": "policies_or_conditions",
    "reason": "No source evidence for hard fact."
  }
]
```

Attempt ledger:
```json
[]
```

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

I'm following up on the loose stair rail at 18 Maple, Unit 2. I need your permission before anyone enters if you are not home, because there is no lockbox code on file for this unit. Please reply yes or no by Friday at 5 p.m.

If the rail has gotten worse since your last message, please send one updated photo today so I can add it to the work order.

The contractor can inspect on Monday, June 17 between 1 p.m. and 3 p.m. I will not mark the repair complete until the contractor has inspected it.
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
Quality notes: The original draft was clear, warm, and concise but lacked visual structure. | The rewrite improves readability by adding line breaks, making the message feel less rushed and more respectful. | Both preserve the no-decision boundary and timeline perfectly. | The rewrite uses a slightly more conversational 'don't' but maintains professionalism. | Overall, the rewrite is a minor improvement in structure without altering any facts or boundaries.
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Fact diagnostics:

Extracted facts:
```json
{
  "recipient_name": "Noor",
  "sender_name_or_role": "",
  "people_mentioned": [
    "Luis"
  ],
  "main_purpose": "Update on Operations Coordinator application status after phone screen",
  "key_facts": [
    "Phone screen completed on May 9",
    "Application is in hiring manager review",
    "No decision has been made yet",
    "Next review date is May 16",
    "Luis is the recruiter contact",
    "Luis will send an update after the review, no later than May 17",
    "No additional materials needed at this time",
    "Reply requested if availability changes before May 17"
  ],
  "required_actions": [
    "Reply if availability changes before May 17"
  ],
  "deadlines": [
    "May 17 - latest date for update from Luis"
  ],
  "dates_times": [
    "May 9 - phone screen completed",
    "May 16 - next review date",
    "May 17 - latest date for update"
  ],
  "positive_notes": [],
  "concerns": [],
  "policies_or_conditions": [],
  "available_support": [],
  "clarifications": [],
  "facts_that_must_not_change": [
    "We have not made a decision yet",
    "I do not want to suggest there is an offer or rejection before that review is finished",
    "You do not need to send more materials right now",
    "Reply if your availability changes before then"
  ],
  "sensitive_points": [],
  "original_tone": "Neutral, informative, and cautious"
}
```

Reviewed facts:
```json
{
  "recipient_name": "Noor",
  "sender_name_or_role": "",
  "people_mentioned": [
    "Luis",
    "Noor",
    "Operations Coordinator"
  ],
  "main_purpose": "Update on Operations Coordinator application status after phone screen",
  "key_facts": [
    "Application is in hiring manager review",
    "Next review date is May 16",
    "Luis is the recruiter contact",
    "Luis will send an update after the review, no later than May 17"
  ],
  "required_actions": [],
  "deadlines": [],
  "dates_times": [
    "May 16 - next review date",
    "May 9",
    "May 16",
    "May 17"
  ],
  "positive_notes": [],
  "concerns": [],
  "policies_or_conditions": [
    "We have not made a decision yet, and I do not want to suggest there is an offer or rejection before that review is finished",
    "Luis is the recruiter contact for this role, and he will send an update after that review, no later than May 17",
    "You do not need to send more materials right now, but please reply if your availability changes before then"
  ],
  "available_support": [],
  "clarifications": [],
  "facts_that_must_not_change": [
    "We have not made a decision yet",
    "I do not want to suggest there is an offer or rejection before that review is finished",
    "You do not need to send more materials right now",
    "Reply if your availability changes before then",
    "Noor",
    "May 9",
    "May 16",
    "May 17",
    "Operations Coordinator",
    "Luis",
    "We have not made a decision yet, and I do not want to suggest there is an offer or rejection before that review is finished",
    "Luis is the recruiter contact for this role, and he will send an update after that review, no later than May 17",
    "You do not need to send more materials right now, but please reply if your availability changes before then"
  ],
  "sensitive_points": [],
  "original_tone": "Neutral, informative, and cautious"
}
```

Added anchors:
```json
[
  {
    "id": "person:noor",
    "text": "Noor",
    "normalizedText": "noor",
    "category": "person",
    "source": "draft",
    "required": true
  },
  {
    "id": "date:may 9",
    "text": "May 9",
    "normalizedText": "may 9",
    "category": "date",
    "source": "draft",
    "required": true
  },
  {
    "id": "date:may 16",
    "text": "May 16",
    "normalizedText": "may 16",
    "category": "date",
    "source": "draft",
    "required": true
  },
  {
    "id": "date:may 17",
    "text": "May 17",
    "normalizedText": "may 17",
    "category": "date",
    "source": "draft",
    "required": true
  },
  {
    "id": "person:operations coordinator",
    "text": "Operations Coordinator",
    "normalizedText": "operations coordinator",
    "category": "person",
    "source": "draft",
    "required": true
  },
  {
    "id": "person:luis",
    "text": "Luis",
    "normalizedText": "luis",
    "category": "person",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:we have not made a decision yet, and i do not want to suggest there is an offer or rejection before that review is finished",
    "text": "We have not made a decision yet, and I do not want to suggest there is an offer or rejection before that review is finished",
    "normalizedText": "we have not made a decision yet, and i do not want to suggest there is an offer or rejection before that review is finished",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:luis is the recruiter contact for this role, and he will send an update after that review, no later than may 17",
    "text": "Luis is the recruiter contact for this role, and he will send an update after that review, no later than May 17",
    "normalizedText": "luis is the recruiter contact for this role, and he will send an update after that review, no later than may 17",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:you do not need to send more materials right now, but please reply if your availability changes before then",
    "text": "You do not need to send more materials right now, but please reply if your availability changes before then",
    "normalizedText": "you do not need to send more materials right now, but please reply if your availability changes before then",
    "category": "constraint",
    "source": "draft",
    "required": true
  }
]
```

Rejected extracted facts:
```json
[
  {
    "text": "Phone screen completed on May 9",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "No decision has been made yet",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "No additional materials needed at this time",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Reply requested if availability changes before May 17",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Reply if availability changes before May 17",
    "field": "required_actions",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "May 17 - latest date for update from Luis",
    "field": "deadlines",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "May 9 - phone screen completed",
    "field": "dates_times",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "May 17 - latest date for update",
    "field": "dates_times",
    "reason": "No source evidence for hard fact."
  }
]
```

Attempt ledger:
```json
[
  {
    "attemptNo": 1,
    "strategy": "support_policy_options_rewrite",
    "modelRole": "mid_writer",
    "modelName": "deepseek-v4-pro",
    "thinkingMode": "non_thinking",
    "candidateText": "Hi Noor,\n\nHere's an update on your Operations Coordinator application. Your phone screen is now in hiring manager review, with the next review date set for May 16. Luis is the recruiter contact for this role, and he will send an update after that review, no later than May 17.\n\nWe have not made a decision yet, and I do not want to suggest there is an offer or rejection before that review is finished. You do not need to send more materials right now, but please reply if your availability changes before then.\n\nBest,\nOperations Coordinator",
    "failureAnalysis": "Candidate preselection rejected model candidates: missing:may 9; missing_locked:May 9",
    "failureKinds": [
      "fact_loss"
    ],
    "factGateResult": "failed",
    "structureGateResult": "failed",
    "policyIntentGateResult": "failed",
    "saplingResult": "not_measured",
    "nextStrategyDecision": "facts_first_reconstruct"
  }
]
```

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
Quality score before: 7
Quality score after: 9
Quality score delta: 2 pts
Quality regression: no
Quality notes: The original draft is clear and factual but presented in a single block, making it slightly dense. | The rewrite adds line breaks for better structure, enhances readability, and uses contractions ('can't', 'don't'), lending a warmer, more conversational human voice. | All facts, boundaries, and deadlines are accurately preserved without dilution or overpromising.
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Fact diagnostics:

Extracted facts:
```json
{
  "recipient_name": "Aisha",
  "sender_name_or_role": "",
  "people_mentioned": [
    "Mia"
  ],
  "main_purpose": "Acknowledge $250 donation to Weekend Meals campaign and provide receipt information.",
  "key_facts": [
    "Donation amount: $250",
    "Campaign: Weekend Meals",
    "Donation received on May 4",
    "Receipt sent on May 6 to aisha@example.org",
    "Receipt can be used for records",
    "Campaign update will go out later this month",
    "No specific program report attached to this receipt"
  ],
  "required_actions": [
    "Reply by May 15 if donor name or email address on receipt needs correction"
  ],
  "deadlines": [
    "May 15 for correction reply"
  ],
  "dates_times": [
    "Donation received: May 4",
    "Receipt sent: May 6",
    "Correction reply deadline: May 15",
    "Campaign update: later this month"
  ],
  "positive_notes": [
    "Thank you for your donation"
  ],
  "concerns": [],
  "policies_or_conditions": [
    "Cannot give tax advice or say whether the gift is deductible"
  ],
  "available_support": [
    "Mia can update the record before monthly export if correction is requested by May 15"
  ],
  "clarifications": [],
  "facts_that_must_not_change": [
    "Cannot give tax advice or say whether the gift is deductible",
    "Do not have a specific program report attached to this receipt"
  ],
  "sensitive_points": [],
  "original_tone": "Appreciative and informative"
}
```

Reviewed facts:
```json
{
  "recipient_name": "Aisha",
  "sender_name_or_role": "",
  "people_mentioned": [
    "Mia",
    "Aisha",
    "Weekend Meals"
  ],
  "main_purpose": "Acknowledge $250 donation to Weekend Meals campaign and provide receipt information.",
  "key_facts": [
    "Donation amount: $250",
    "Campaign: Weekend Meals",
    "Receipt sent on May 6 to aisha@example.org",
    "Receipt can be used for records",
    "Campaign update will go out later this month",
    "No specific program report attached to this receipt"
  ],
  "required_actions": [],
  "deadlines": [],
  "dates_times": [
    "Receipt sent: May 6",
    "Campaign update: later this month",
    "May 4",
    "May 6",
    "May 15"
  ],
  "positive_notes": [
    "Thank you for your donation"
  ],
  "concerns": [],
  "policies_or_conditions": [
    "Cannot give tax advice or say whether the gift is deductible",
    "The receipt can be used for your records, but I cannot give tax advice or say whether the gift is deductible for your situation",
    "If the donor name or email address on the receipt needs to be corrected, please reply by May 15 so Mia can update the record before the monthly export",
    "The campaign update will go out later this month, but I do not have a specific program report attached to this receipt"
  ],
  "available_support": [],
  "clarifications": [],
  "facts_that_must_not_change": [
    "Cannot give tax advice or say whether the gift is deductible",
    "Do not have a specific program report attached to this receipt",
    "Aisha",
    "aisha@example.org",
    "$250",
    "May 4",
    "May 6",
    "May 15",
    "Weekend Meals",
    "Mia",
    "The receipt can be used for your records, but I cannot give tax advice or say whether the gift is deductible for your situation",
    "If the donor name or email address on the receipt needs to be corrected, please reply by May 15 so Mia can update the record before the monthly export",
    "The campaign update will go out later this month, but I do not have a specific program report attached to this receipt"
  ],
  "sensitive_points": [],
  "original_tone": "Appreciative and informative"
}
```

Added anchors:
```json
[
  {
    "id": "person:aisha",
    "text": "Aisha",
    "normalizedText": "aisha",
    "category": "person",
    "source": "draft",
    "required": true
  },
  {
    "id": "contact:aisha@example.org",
    "text": "aisha@example.org",
    "normalizedText": "aisha@example.org",
    "category": "contact",
    "source": "draft",
    "required": true
  },
  {
    "id": "amount:$250",
    "text": "$250",
    "normalizedText": "$250",
    "category": "amount",
    "source": "draft",
    "required": true
  },
  {
    "id": "date:may 4",
    "text": "May 4",
    "normalizedText": "may 4",
    "category": "date",
    "source": "draft",
    "required": true
  },
  {
    "id": "date:may 6",
    "text": "May 6",
    "normalizedText": "may 6",
    "category": "date",
    "source": "draft",
    "required": true
  },
  {
    "id": "date:may 15",
    "text": "May 15",
    "normalizedText": "may 15",
    "category": "date",
    "source": "draft",
    "required": true
  },
  {
    "id": "person:weekend meals",
    "text": "Weekend Meals",
    "normalizedText": "weekend meals",
    "category": "person",
    "source": "draft",
    "required": true
  },
  {
    "id": "person:mia",
    "text": "Mia",
    "normalizedText": "mia",
    "category": "person",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:the receipt can be used for your records, but i cannot give tax advice or say whether the gift is deductible for your situation",
    "text": "The receipt can be used for your records, but I cannot give tax advice or say whether the gift is deductible for your situation",
    "normalizedText": "the receipt can be used for your records, but i cannot give tax advice or say whether the gift is deductible for your situation",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:if the donor name or email address on the receipt needs to be corrected, please reply by may 15 so mia can update the record before the monthly export",
    "text": "If the donor name or email address on the receipt needs to be corrected, please reply by May 15 so Mia can update the record before the monthly export",
    "normalizedText": "if the donor name or email address on the receipt needs to be corrected, please reply by may 15 so mia can update the record before the monthly export",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:the campaign update will go out later this month, but i do not have a specific program report attached to this receipt",
    "text": "The campaign update will go out later this month, but I do not have a specific program report attached to this receipt",
    "normalizedText": "the campaign update will go out later this month, but i do not have a specific program report attached to this receipt",
    "category": "constraint",
    "source": "draft",
    "required": true
  }
]
```

Rejected extracted facts:
```json
[
  {
    "text": "Donation received on May 4",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Reply by May 15 if donor name or email address on receipt needs correction",
    "field": "required_actions",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "May 15 for correction reply",
    "field": "deadlines",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Donation received: May 4",
    "field": "dates_times",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Correction reply deadline: May 15",
    "field": "dates_times",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Mia can update the record before monthly export if correction is requested by May 15",
    "field": "available_support",
    "reason": "No source evidence for hard fact."
  }
]
```

Attempt ledger:
```json
[
  {
    "attemptNo": 1,
    "strategy": "facts_first_reconstruct",
    "modelRole": "mid_writer",
    "modelName": "deepseek-v4-pro",
    "thinkingMode": "non_thinking",
    "candidateText": "Hi Aisha,\n\nThanks for your $250 donation to the Weekend Meals campaign. A receipt was sent to aisha@example.org on May 6. You can use it for your records, but I can't give tax advice or say whether the gift is deductible for your situation.\n\nIf the donor name or email address on the receipt needs to be corrected, just reply by May 15 so Mia can update the record before the monthly export.\n\nThe campaign update will go out later this month. I don't have a specific program report attached to this receipt, but you'll get that update separately.",
    "failureAnalysis": "Candidate preselection rejected model candidates: missing:may 4; missing_locked:May 4",
    "failureKinds": [
      "fact_loss"
    ],
    "factGateResult": "failed",
    "structureGateResult": "failed",
    "policyIntentGateResult": "failed",
    "saplingResult": "not_measured",
    "nextStrategyDecision": "facts_first_reconstruct"
  }
]
```

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
Quality score after: 8
Quality score delta: 1 pts
Quality regression: no
Quality notes: Original draft is a single paragraph, clear but less readable. | Rewritten draft uses line breaks for improved structure and adds a slight conversational contraction ('can't'), making it feel more human while keeping all facts and firmness.
Quality failure state: no
Quality failure reason: none
Customer-usable pass: yes
Strict signal pass: yes

Fact diagnostics:

Extracted facts:
```json
{
  "recipient_name": "Mr. Rivera",
  "sender_name_or_role": "",
  "people_mentioned": [
    "Jamal"
  ],
  "main_purpose": "Notify about missing permission form and fee for robotics club enrollment",
  "key_facts": [
    "Activity-ROBO-22 permission form is missing",
    "$35 materials fee is missing",
    "Deadline is September 6 at 3:30 p.m. in Room 12",
    "Office must send bus roster at 4 p.m. that day",
    "Jamal is on the interest list but not enrolled",
    "Cannot move Jamal ahead of students with completed forms and fees",
    "Blank form available from main office",
    "Scanned signed copy can be sent before deadline",
    "If form and fee not in Room 12 by 3:30 p.m. on September 6, Jamal stays on waitlist for first session"
  ],
  "required_actions": [
    "Submit Activity-ROBO-22 permission form",
    "Pay $35 materials fee",
    "Deliver to Room 12 by September 6, 3:30 p.m."
  ],
  "deadlines": [
    "September 6 at 3:30 p.m."
  ],
  "dates_times": [
    "September 6 at 3:30 p.m.",
    "4 p.m. on September 6"
  ],
  "positive_notes": [],
  "concerns": [],
  "policies_or_conditions": [
    "Enrollment requires completed form and fee",
    "Students with completed forms and fees are prioritized"
  ],
  "available_support": [
    "Pick up blank form from main office",
    "Send scanned signed copy before deadline"
  ],
  "clarifications": [],
  "facts_that_must_not_change": [
    "Jamal is not enrolled yet",
    "I cannot move him ahead of students with completed forms and fees",
    "If the form and fee are not in Room 12 by 3:30 p.m. on September 6, he will stay on the waitlist for the first session"
  ],
  "sensitive_points": [],
  "original_tone": "Informative and urgent"
}
```

Reviewed facts:
```json
{
  "recipient_name": "Mr. Rivera",
  "sender_name_or_role": "",
  "people_mentioned": [
    "Jamal",
    "Mr. Rivera"
  ],
  "main_purpose": "Notify about missing permission form and fee for robotics club enrollment",
  "key_facts": [
    "Activity-ROBO-22 permission form is missing",
    "$35 materials fee is missing",
    "Deadline is September 6 at 3:30 p.m. in Room 12",
    "Jamal is on the interest list but not enrolled",
    "Blank form available from main office",
    "Scanned signed copy can be sent before deadline",
    "Activity-ROBO-22"
  ],
  "required_actions": [
    "Submit Activity-ROBO-22 permission form",
    "Pay $35 materials fee"
  ],
  "deadlines": [
    "September 6 at 3:30 p.m.",
    "3:30 p.m.",
    "4 p.m.",
    "by 3:30 p.m."
  ],
  "dates_times": [
    "September 6 at 3:30 p.m.",
    "4 p.m. on September 6",
    "September 6"
  ],
  "positive_notes": [],
  "concerns": [],
  "policies_or_conditions": [
    "Students with completed forms and fees are prioritized",
    "Rivera, I checked Jamal's after-school robotics club file",
    "Jamal is on the interest list, but he is not enrolled yet, and I cannot move him ahead of students with completed forms and fees",
    "You can pick up a blank form from the main office or send a scanned signed copy before the deadline",
    "If the form and fee are not in Room 12 by 3:30 p.m"
  ],
  "available_support": [
    "Pick up blank form from main office",
    "Send scanned signed copy before deadline"
  ],
  "clarifications": [],
  "facts_that_must_not_change": [
    "Jamal is not enrolled yet",
    "I cannot move him ahead of students with completed forms and fees",
    "If the form and fee are not in Room 12 by 3:30 p.m. on September 6, he will stay on the waitlist for the first session",
    "Mr. Rivera",
    "Activity-ROBO-22",
    "$35",
    "September 6",
    "3:30 p.m.",
    "4 p.m.",
    "by 3:30 p.m.",
    "Jamal",
    "Rivera, I checked Jamal's after-school robotics club file",
    "Jamal is on the interest list, but he is not enrolled yet, and I cannot move him ahead of students with completed forms and fees",
    "You can pick up a blank form from the main office or send a scanned signed copy before the deadline",
    "If the form and fee are not in Room 12 by 3:30 p.m"
  ],
  "sensitive_points": [],
  "original_tone": "Informative and urgent"
}
```

Added anchors:
```json
[
  {
    "id": "person:mr. rivera",
    "text": "Mr. Rivera",
    "normalizedText": "mr. rivera",
    "category": "person",
    "source": "draft",
    "required": true
  },
  {
    "id": "quoted_phrase:activity-robo-22",
    "text": "Activity-ROBO-22",
    "normalizedText": "activity-robo-22",
    "category": "quoted_phrase",
    "source": "draft",
    "required": true
  },
  {
    "id": "amount:$35",
    "text": "$35",
    "normalizedText": "$35",
    "category": "amount",
    "source": "draft",
    "required": true
  },
  {
    "id": "date:september 6",
    "text": "September 6",
    "normalizedText": "september 6",
    "category": "date",
    "source": "draft",
    "required": true
  },
  {
    "id": "deadline:3:30 p.m.",
    "text": "3:30 p.m.",
    "normalizedText": "3:30 p.m.",
    "category": "deadline",
    "source": "draft",
    "required": true
  },
  {
    "id": "deadline:4 p.m.",
    "text": "4 p.m.",
    "normalizedText": "4 p.m.",
    "category": "deadline",
    "source": "draft",
    "required": true
  },
  {
    "id": "deadline:by 3:30 p.m.",
    "text": "by 3:30 p.m.",
    "normalizedText": "by 3:30 p.m.",
    "category": "deadline",
    "source": "draft",
    "required": true
  },
  {
    "id": "person:jamal",
    "text": "Jamal",
    "normalizedText": "jamal",
    "category": "person",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:rivera, i checked jamal's after-school robotics club file",
    "text": "Rivera, I checked Jamal's after-school robotics club file",
    "normalizedText": "rivera, i checked jamal's after-school robotics club file",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:jamal is on the interest list, but he is not enrolled yet, and i cannot move him ahead of students with completed forms and fees",
    "text": "Jamal is on the interest list, but he is not enrolled yet, and I cannot move him ahead of students with completed forms and fees",
    "normalizedText": "jamal is on the interest list, but he is not enrolled yet, and i cannot move him ahead of students with completed forms and fees",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:you can pick up a blank form from the main office or send a scanned signed copy before the deadline",
    "text": "You can pick up a blank form from the main office or send a scanned signed copy before the deadline",
    "normalizedText": "you can pick up a blank form from the main office or send a scanned signed copy before the deadline",
    "category": "constraint",
    "source": "draft",
    "required": true
  },
  {
    "id": "constraint:if the form and fee are not in room 12 by 3:30 p.m",
    "text": "If the form and fee are not in Room 12 by 3:30 p.m",
    "normalizedText": "if the form and fee are not in room 12 by 3:30 p.m",
    "category": "constraint",
    "source": "draft",
    "required": true
  }
]
```

Rejected extracted facts:
```json
[
  {
    "text": "Office must send bus roster at 4 p.m. that day",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Cannot move Jamal ahead of students with completed forms and fees",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "If form and fee not in Room 12 by 3:30 p.m. on September 6, Jamal stays on waitlist for first session",
    "field": "key_facts",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Deliver to Room 12 by September 6, 3:30 p.m.",
    "field": "required_actions",
    "reason": "No source evidence for hard fact."
  },
  {
    "text": "Enrollment requires completed form and fee",
    "field": "policies_or_conditions",
    "reason": "No source evidence for hard fact."
  }
]
```

Attempt ledger:
```json
[
  {
    "attemptNo": 1,
    "strategy": "facts_first_reconstruct",
    "modelRole": "mid_writer",
    "modelName": "deepseek-v4-pro",
    "thinkingMode": "non_thinking",
    "candidateText": "Mr. Rivera, I checked Jamal's after-school robotics club file. Jamal is on the interest list, but he is not enrolled yet, and I cannot move him ahead of students with completed forms and fees.\n\nTo complete his enrollment, we need the Activity-ROBO-22 permission form and the $35 materials fee. You can pick up a blank form from the main office or send a scanned signed copy before the deadline. Both must be submitted by 3:30 p.m. on September 6 in Room 12. If the form and fee are not in Room 12 by 3:30 p.m., he will stay on the waitlist for the first session.\n\nLet me know if you have any questions.",
    "failureAnalysis": "Candidate preselection rejected model candidates: missing:4 p.m.; missing_locked:4 p.m.",
    "failureKinds": [
      "fact_loss"
    ],
    "factGateResult": "failed",
    "structureGateResult": "failed",
    "policyIntentGateResult": "failed",
    "saplingResult": "not_measured",
    "nextStrategyDecision": "facts_first_reconstruct"
  }
]
```

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

The deadline is September 6 at 3:30 p.m. in Room 12, because the office has to send the bus roster at 4 p.m. that day. Jamal is on the interest list, but he is not enrolled yet, and I can't move him ahead of students with completed forms and fees.

You can pick up a blank form from the main office or send a scanned signed copy before the deadline. If the form and fee are not in Room 12 by 3:30 p.m. on September 6, he will stay on the waitlist for the first session.
```
