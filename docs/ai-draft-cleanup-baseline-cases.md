AI Draft Cleanup baseline cases — purpose-built AI-generated drafts that fill gaps the
100-case corpus lacks: ultra-short drafts (≤35 words, to exercise the router's MinimalPolish
branch) and drafts with heavy AI filler. Engine-visible input is only `input_draft` +
`tone_preset: warm` (same as prod); all other fields are judge-only metadata. Run with the
current engine at prod settings (Sapling, target 20, max 10, floor 40) — baseline only.

### Case 201 - short scheduling confirmation
- id: aidc-201
- category: scheduling
- source_type: email
- tone_preset: warm
- input_word_count_band: 0-40

#### input_draft
Thanks so much for reaching out! I'm happy to confirm your appointment is set for Tuesday, June 3 at 3 PM. Please let me know if there's anything else I can help with!

#### what_actually_happened
The appointment is confirmed for Tuesday, June 3 at 3 PM.

#### must_keep
- The appointment is confirmed.
- The appointment is on Tuesday, June 3.
- The time is 3 PM.

#### must_not_claim
- Do not invent a location or address.
- Do not add services or details not in the draft.

#### rewrite_quality_targets
Keep it short and natural; drop the canned opener/closer without losing the confirmation, date, or time.

#### expected_rewrite_challenges
Router likely selects MinimalPolish (≤35 words), which may keep the AI filler ("Thanks so much for reaching out", "Please let me know if there's anything else").

### Case 202 - short refund confirmation
- id: aidc-202
- category: customer_support
- source_type: email
- tone_preset: warm
- input_word_count_band: 0-40

#### input_draft
Hi there! Thanks for getting in touch. I've processed your refund of $40.00, which should appear in your account within 3-5 business days. Let me know if you have any questions!

#### what_actually_happened
A refund of $40.00 was processed; it should appear within 3-5 business days.

#### must_keep
- A refund was processed.
- The refund amount is $40.00.
- It should appear within 3-5 business days.

#### must_not_claim
- Do not promise an instant or same-day refund.
- Do not change the refund amount.

#### rewrite_quality_targets
Keep the amount and timeframe exact; cut the generic greeting/closer; sound like a person, not a script.

#### expected_rewrite_challenges
MinimalPolish branch may preserve the templated greeting and "Let me know if you have any questions" closer.

### Case 203 - short invoice reminder
- id: aidc-203
- category: billing_support
- source_type: email
- tone_preset: warm
- input_word_count_band: 0-40

#### input_draft
I hope you're doing well! Just a quick note to confirm that invoice #INV-204 for $120.00 is due on June 10. Please reach out if you have any concerns!

#### what_actually_happened
Invoice INV-204 for $120.00 is due on June 10.

#### must_keep
- The invoice identifier is INV-204.
- The amount is $120.00.
- It is due on June 10.

#### must_not_claim
- Do not waive or change the amount.
- Do not change the due date.

#### rewrite_quality_targets
Preserve the invoice id, amount, and due date; remove "I hope you're doing well" and the generic closer.

#### expected_rewrite_challenges
MinimalPolish may keep "I hope you're doing well" and "Please reach out if you have any concerns".

### Case 204 - filler-heavy support reply
- id: aidc-204
- category: customer_support
- source_type: email
- tone_preset: warm
- input_word_count_band: 40-100

#### input_draft
I hope this email finds you well. I wanted to take a moment to reach out regarding your recent inquiry about order #R-5582. I completely understand your frustration, and please rest assured that we take these matters very seriously. After looking into this, I can confirm that your replacement unit was shipped on May 20 and is expected to arrive by May 27. Please do not hesitate to reach out if you have any further questions or concerns. Thank you so much for your patience and understanding!

#### what_actually_happened
For order R-5582, the replacement unit shipped on May 20 and is expected to arrive by May 27.

#### must_keep
- The order identifier is R-5582.
- The replacement unit shipped on May 20.
- It is expected to arrive by May 27.

#### must_not_claim
- Do not promise a refund.
- Do not guarantee earlier delivery than May 27.

#### rewrite_quality_targets
Strip the heavy filler ("I hope this email finds you well", "please rest assured", "Thank you so much for your patience"); keep the order id and both dates; should be much shorter.

#### expected_rewrite_challenges
FactsFirstReconstruct should rebuild it, but the model may re-add empathy/closing filler that keeps the AI signature.

### Case 205 - filler-heavy sales follow-up
- id: aidc-205
- category: sales_followup
- source_type: email
- tone_preset: warm
- input_word_count_band: 40-100

#### input_draft
I hope you're having a wonderful week! I wanted to personally reach out to follow up on our conversation regarding the Growth plan. I would be more than happy to walk you through everything in detail. As discussed, the Growth plan is priced at $49 per month and includes up to 10 user seats. This quote is valid until June 15. Please don't hesitate to let me know if you have any questions whatsoever — I'm always here to help. Thank you so much for considering us!

#### what_actually_happened
The Growth plan is $49 per month, includes up to 10 user seats, and the quote is valid until June 15.

#### must_keep
- The plan is the Growth plan.
- The price is $49 per month.
- It includes up to 10 user seats.
- The quote is valid until June 15.

#### must_not_claim
- Do not offer a discount.
- Do not promise more than 10 seats.

#### rewrite_quality_targets
Keep price, seat count, and expiry exact; cut the marketing filler; concrete and concise.

#### expected_rewrite_challenges
The draft is dense with sales filler; the model may keep the upbeat template tone.

### Case 206 - filler-heavy renewal notice
- id: aidc-206
- category: customer_success
- source_type: email
- tone_preset: warm
- input_word_count_band: 100-200

#### input_draft
I hope this message finds you well. Thank you so much for being a valued customer with us over the past year. I wanted to reach out regarding the upcoming renewal of your Pro plan. I completely understand that renewals can sometimes be a lot to think about, and I want to assure you that we're here to support you every step of the way. As it currently stands, your Pro plan is set to renew on June 30 at $290 for the year, which covers up to 8 seats. If you'd like to make any changes to your plan, we would be more than happy to discuss the available options with you. Please don't hesitate to reach out at your earliest convenience. We truly appreciate your continued trust and partnership!

#### what_actually_happened
The Pro plan renews on June 30 at $290 for the year, covering up to 8 seats. Changes are possible if the customer asks.

#### must_keep
- The plan is the Pro plan.
- It renews on June 30.
- The price is $290 for the year.
- It covers up to 8 seats.
- Changes can be discussed if the customer wants.

#### must_not_claim
- Do not auto-renew or change the plan without confirmation.
- Do not change the price.
- Do not promise a discount.

#### rewrite_quality_targets
Preserve renewal date, annual price, seat count, and the change-on-request option; remove the long greeting/empathy/closing scaffolding; should be far shorter than the draft.

#### expected_rewrite_challenges
Long (~135 words) so the router selects FullStructureRewrite, which may impose an acknowledge -> status -> options -> next-step structure that reads more AI, not less.

### Case 207 - short reschedule (no policy words)
- id: aidc-207
- category: scheduling
- source_type: email
- tone_preset: warm
- input_word_count_band: 0-40

#### input_draft
I hope you're doing well! Unfortunately, Friday won't work for me, but I'd be more than happy to meet Tuesday afternoon instead. Just let me know what works best for you!

#### what_actually_happened
The sender cannot meet Friday but is available Tuesday afternoon and asks what works for the recipient.

#### must_keep
- The sender cannot meet on Friday.
- The sender is available Tuesday afternoon.
- The sender asks what works for the recipient.

#### must_not_claim
- Do not commit to a specific clock time.
- Do not invent a meeting topic or location.

#### rewrite_quality_targets
Keep it short and natural; drop "I hope you're doing well" and "I'd be more than happy"; preserve the Friday/Tuesday facts and the question.

#### expected_rewrite_challenges
No policy words + <=35 words, so the router selects MinimalPolish ("keep close to the draft") — watch whether it leaves the AI filler in.

### Case 208 - short status update (no policy words)
- id: aidc-208
- category: workplace_update
- source_type: message
- tone_preset: warm
- input_word_count_band: 0-40

#### input_draft
I hope this finds you well! I just wanted to quickly let you know that I've wrapped up the slides and sent them your way this morning. Feel free to take a look whenever!

#### what_actually_happened
The sender finished the slides and sent them this morning.

#### must_keep
- The slides are finished.
- They were sent this morning.

#### must_not_claim
- Do not invent the slides' topic or content.
- Do not add a deadline or next step not in the draft.

#### rewrite_quality_targets
Preserve "slides finished" + "sent this morning"; cut "I hope this finds you well" and "I just wanted to quickly let you know".

#### expected_rewrite_challenges
MinimalPolish branch (<=35 words, no policy words) may keep the templated opener and "Feel free to take a look whenever".
