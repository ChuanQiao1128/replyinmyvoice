# AI Draft Cleanup — baseline (current engine, prod-realistic payload)

Settings: WRITING_SIGNAL_PROVIDER=sapling, EVAL_TARGET_AI_LIKE=20, EVAL_MAX_ATTEMPTS=10, NATURALNESS_THRESHOLD=40. Payload = {roughDraftReply, tone:warm} only.

| id | cat | strategy | in_wc | out_wc | compress | draft_sap | final_sap | attempts | sap_calls | facts | forbid | send_ready | opener | closer | filler | forced | too_close |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| rewrite-draft-002 | customer_support | QuoteListSafe | 118 | 119 | 1.01 | 0 | 0 | 1 | 2 | True | 0 | True | 0 | 0 | 0 | True | 0.99 |
| rewrite-draft-003 | billing_support | SupportPolicyOptions | 136 | 134 | 0.99 | 0 | 0 | 1 | 2 | True | 0 | True | 0 | 0 | 0 | True | 0.83 |
| rewrite-draft-005 | sales_followup | SupportPolicyOptions | 113 | 106 | 0.94 | 0 | 0 | 1 | 2 | True | 0 | True | 0 | 0 | 0 | True | 0.88 |
| rewrite-draft-006 | scheduling | SupportPolicyOptions | 61 | 62 | 1.02 | 17 | 20 | 1 | 2 | True | 0 | True | 0 | 0 | 0 | True | 0.98 |
| rewrite-draft-028 | customer_success | SupportPolicyOptions | 154 | 95 | 0.62 | 0 | 14 | 1 | 2 | True | 0 | True | 0 | 0 | 0 | True | 0.49 |
| rewrite-draft-041 | customer_support | SupportPolicyOptions | 332 | 266 | 0.8 | 6 | 6 | 1 | 2 | True | 0 | True | 0 | 0 | 0 | True | 0.85 |
| rewrite-draft-042 | billing_support | SupportPolicyOptions | 278 | 228 | 0.82 | 0 | 19 | 1 | 2 | True | 0 | True | 0 | 0 | 0 | True | 0.73 |
| rewrite-draft-045 | sales_followup | SupportPolicyOptions | 295 | 296 | 1.0 | 8 | 9 | 1 | 2 | True | 0 | True | 0 | 0 | 0 | True | 0.99 |
| rewrite-draft-049 | school_admin | SupportPolicyOptions | 257 | 227 | 0.88 | 27 | 20 | 1 | 2 | True | 0 | True | 0 | 0 | 0 | True | 0.88 |
| rewrite-draft-061 | customer_support | QuoteListSafe | 202 | 187 | 0.93 | 37 | 15 | 1 | 2 | True | 0 | True | 0 | 0 | 0 | True | 0.73 |
| rewrite-draft-066 | medical_admin | QuoteListSafe | 215 | 178 | 0.83 | 0 | 2 | 1 | 2 | True | 0 | True | 0 | 0 | 0 | True | 0.66 |
| rewrite-draft-071 | customer_support | SupportPolicyOptions | 200 | 104 | 0.52 | 22 | 0 | 1 | 2 | True | 1 | False | 0 | 0 | 0 | True | 0.3 |
| rewrite-draft-074 | teacher_parent | SupportPolicyOptions | 231 | 196 | 0.85 | 36 | 9 | 3 | 4 | True | 0 | True | 0 | 0 | 0 | True | 0.67 |
| rewrite-draft-080 | customer_success | SupportPolicyOptions | 239 | 199 | 0.83 | 15 | 0 | 1 | 2 | True | 0 | True | 0 | 0 | 0 | True | 0.76 |
| aidc-201 | scheduling | SupportPolicyOptions | 33 | 17 | 0.52 | 93 | 0 | 11 | 12 | False | 0 | False | 0 | 0 | 0 | False | 0.42 |
| aidc-202 | customer_support | SupportPolicyOptions | 32 | 25 | 0.78 | 74 | 25 | 15 | 16 | True | 2 | False | 0 | 0 | 0 | False | 0.46 |
| aidc-203 | billing_support | SupportPolicyOptions | 30 | 11 | 0.37 | 66 | 0 | 7 | 8 | False | 0 | False | 0 | 0 | 0 | True | 0.28 |
| aidc-204 | customer_support | SupportPolicyOptions | 87 | 20 | 0.23 | 82 | 0 | 7 | 8 | True | 0 | True | 0 | 0 | 0 | True | 0.18 |
| aidc-205 | sales_followup | FactsFirstReconstruct | 86 | 42 | 0.49 | 57 | 7 | 9 | 10 | True | 0 | True | 0 | 0 | 0 | True | 0.21 |
| aidc-206 | customer_success | FullStructureRewrite | 131 | 31 | 0.24 | 62 | 0 | 7 | 8 | False | 0 | False | 0 | 0 | 0 | True | 0.17 |
| aidc-207 | scheduling | MinimalPolish | 31 | 16 | 0.52 | 71 | 0 | 5 | 6 | False | 0 | False | 0 | 0 | 0 | False | 0.25 |
| aidc-208 | workplace_update | MinimalPolish | 34 | 18 | 0.53 | 46 | 0 | 3 | 4 | False | 0 | False | 0 | 1 | 0 | False | 0.55 |

## facts=False cases (real loss vs matcher false-negative — read the text)

### aidc-201 (scheduling, SupportPolicyOptions) — missing: ['The appointment is confirmed.']
```
Thanks—your appointment is set for Tuesday, June 3 at 3 PM. Reach out if anything shifts.
```
draft: ```
Thanks so much for reaching out! I'm happy to confirm your appointment is set for Tuesday, June 3 at 3 PM. Please let me know if there's anything else I can help with!
```

### aidc-203 (billing_support, SupportPolicyOptions) — missing: ['The amount is $120.00.']
```
Hi,

Invoice #INV-204 for $120.00 is due June 10.

Best,
```
draft: ```
I hope you're doing well! Just a quick note to confirm that invoice #INV-204 for $120.00 is due on June 10. Please reach out if you have any concerns!
```

### aidc-206 (customer_success, FullStructureRewrite) — missing: ['Changes can be discussed if the customer wants.']
```
Hi,

Your Pro plan renews June 30 at $290 for the year, covering up to 8 seats.

Want to adjust anything? We can walk through the options whenever you're ready.

Best,
```
draft: ```
I hope this message finds you well. Thank you so much for being a valued customer with us over the past year. I wanted to reach out regarding the upcoming renewal of your Pro plan. I completely understand that renewals can sometimes be a lot to think about, and I want to assure you that we're here to support you every step of the way. As it currently stands, your Pro plan is set to renew on June 30 at $290 for the year, which covers up to 8 seats. If you'd like to make any changes to your plan, we would be more than happy to discuss the available options with you. Please don't hesitate to reach out at your earliest convenience. We truly appreciate your continued trust and partnership!
```

### aidc-207 (scheduling, MinimalPolish) — missing: ['The sender asks what works for the recipient.']
```
Hi there, Friday won't work for me, but Tuesday afternoon is open. Would that suit you?
```
draft: ```
I hope you're doing well! Unfortunately, Friday won't work for me, but I'd be more than happy to meet Tuesday afternoon instead. Just let me know what works best for you!
```

### aidc-208 (workplace_update, MinimalPolish) — missing: ['The slides are finished.']
```
I wrapped up the slides and sent them over this morning. Feel free to take a look whenever!
```
draft: ```
I hope this finds you well! I just wanted to quickly let you know that I've wrapped up the slides and sent them your way this morning. Feel free to take a look whenever!
```
