# Dispute And Chargeback Runbook

Date: 2026-06-01
Owner: TimeAwake Ltd operator
Scope: Manual handling for Stripe disputes and chargebacks for `replyinmyvoice.com`.

This runbook is operational guidance only. The app must never auto-submit dispute evidence, initiate live charges, or make live Stripe account changes from automation. The owner submits evidence manually in Stripe.

## Current System Behavior

- Stripe sends `charge.dispute.created` and `charge.dispute.closed` webhooks to the app.
- `StripeEventService.RevokeDisputedChargeCreditsAsync` finds purchase credits by `payment_intent` and revokes unused credits by setting granted credits down to consumed credits.
- The same revoke path runs again on dispute closure, so webhook replay and closure do not create extra credits.
- Existing admin suspension is available through the admin API and can be used for repeat-disputer policy enforcement after owner review.
- The admin user detail API already returns purchase records, credit history, usage periods, subscription summary, and cost-to-date for a known user. A direct `payment_intent` evidence lookup endpoint was not added in PAY-26.

## Where Disputes Appear

Check disputes in Stripe Dashboard:

1. Open Stripe Dashboard.
2. Go to **Payments** -> **Disputes**.
3. Open the dispute.
4. Record the dispute id, charge id, payment intent id, amount, currency, reason/category, customer, current status, and the evidence deadline shown by Stripe.
5. Open the payment timeline from the dispute page and confirm it matches a Reply In My Voice purchase.

Stripe also notifies dispute outcomes by email, Dashboard status, API object status, and the `charge.dispute.closed` webhook.

Official Stripe references:

- https://docs.stripe.com/disputes/responding
- https://docs.stripe.com/disputes/how-disputes-work
- https://docs.stripe.com/disputes/reason-codes-defense-requirements

## Response Deadline And SLA

The authoritative deadline is the date shown on the Stripe dispute details page. For API users, Stripe exposes this as the dispute evidence due-by timestamp. Do not assume a fixed number of days because the deadline can vary by network, payment method, and dispute type.

Owner SLA:

- Triage every new dispute the same business day it appears in Stripe.
- Gather evidence within 2 business days.
- Submit the final evidence package at least 48 hours before Stripe's evidence deadline.
- If the dispute is discovered with less than 48 hours remaining, submit the best complete package the same day.
- If Stripe marks the dispute as not challengeable, record the loss and do not attempt to work around Stripe's process.

Important: Stripe allows only one final evidence submission for a dispute response. Assemble the full package before clicking submit.

## Evidence Checklist

Collect only evidence relevant to the specific Stripe dispute reason. Keep the package factual, concise, and internally consistent.

Required evidence for every challenge:

- Purchase record: Stripe charge id, payment intent id, customer id, amount, currency, SKU, purchase date, and Stripe receipt/invoice details if available.
- Internal payment record: matching admin payment row from `AdminService.GetUserDetailAsync`, including `paymentIntentId`, `sku`, `amountTotal`, `currency`, `grantedAt`, `expiresAt`, `creditsGranted`, `creditsConsumed`, and `creditsRemaining`.
- Usage history: usage periods showing rewrites used and any reserved rewrites around the purchase and dispute dates.
- Credit ledger: purchase credit grant, consumed credits, remaining credits, and any clawback from the dispute webhook.
- Terms acceptance context: the Terms page and checkout flow used for the purchase, including the billing, refund, cancellation, and dispute language that applied at the time.
- Customer account context: user email, Stripe customer id, subscription status if Pro/API, created date, and any prior support/refund communications.
- Support communications: relevant emails where the customer asked for help, cancellation, refund, access, or billing clarification.

Dispute-reason additions:

- Credit not processed / cancellation: include refund or cancellation policy, any cancellation request logs, and any refund decision or refund transaction proof.
- Duplicate charge: include Stripe payment timeline, matching internal purchase rows, and explanation of whether one or multiple Stripe payments exist.
- Product or service not received: include account creation, purchase grant, usage logs, and evidence that credits/service access were available after purchase.
- Unauthorized claim: include Stripe checkout/customer details available in Dashboard, account email, purchase timestamp, and usage after purchase. Do not include secrets, tokens, private keys, raw logs containing credentials, or unrelated customer content.

Do not include external links as evidence unless Stripe specifically accepts them. Attach files or paste text into the Stripe evidence form because the issuer may not open links.

## Evidence Context Procedure

Use the existing admin surfaces to assemble evidence:

1. From Stripe, copy the dispute id, charge id, payment intent id, customer id, customer email, amount, currency, reason, and evidence deadline.
2. In the admin user list or database-safe admin tooling, find the user by email or Stripe customer id.
3. Open admin user detail for that user.
4. In the `payments` section, find the row whose `paymentIntentId` matches the disputed payment intent.
5. Copy the matching payment row and the surrounding `usage`, `credits`, and `subscription` summary into an internal note.
6. Confirm whether the dispute webhook already revoked unused credits by checking that `creditsRemaining` is zero or reduced to the consumed amount for the disputed purchase.
7. Attach or paste only the minimum necessary records into Stripe. Redact unrelated users, provider internals, tokens, request ids that are not useful to the issuer, and private rewrite contents unless the customer explicitly supplied the same content in the dispute and it is necessary to prove service delivery.

## Submission Steps

1. Decide whether to accept or challenge.
   - Accept when the customer claim is valid, evidence is weak, the dispute is not challengeable, or the amount does not justify the response effort.
   - Challenge when records show the purchase was valid, the customer received credits or service access, and the evidence directly answers the dispute reason.
2. If challenging, open the Stripe dispute details page and click **Counter dispute**.
3. Select the response type that matches the dispute reason and product type.
4. Fill in a short factual explanation:
   - what the customer bought,
   - when they bought it,
   - what access or credits were granted,
   - what usage occurred,
   - what policy applied,
   - why the dispute claim is incorrect or already resolved.
5. Upload the evidence files Stripe requests for that reason. Combine multiple records into one PDF when Stripe only allows one file for an evidence type.
6. Review the final package against the checklist.
7. Submit before the Stripe deadline.
8. Save an internal note with dispute id, payment intent id, evidence submitted, submission date, and expected outcome date.

## Outcome Handling

### Won

- Stripe returns the disputed amount through the normal dispute resolution flow.
- Record the win in the internal payment/support notes.
- Confirm `charge.dispute.closed` was processed.
- Do not automatically grant new credits. The current webhook path revokes unused credits on dispute creation and closure. If the owner decides the customer should keep service after a genuine misunderstanding, issue a manual admin credit grant with an audit reason that references the won dispute.
- If the user was suspended during investigation, decide whether to unsuspend based on customer communication and abuse risk.

### Lost

- Treat the chargeback as final unless Stripe provides a specific next step.
- Confirm the webhook processed and unused credits remain revoked.
- Do not issue an additional refund for the same payment.
- Record the loss and reason in internal notes.
- If the user still has active access through another purchase or subscription, review whether continued service is appropriate.

### Accepted

- Accepting the dispute means Reply In My Voice is not contesting the chargeback.
- Confirm unused credits are revoked.
- Record the rationale, such as valid refund claim, insufficient evidence, low amount, or not challengeable.

## Repeat-Disputer Policy

Use manual owner review. Do not auto-suspend from a single dispute.

Policy:

- One dispute: revoke unused credits through the webhook, triage evidence, and contact the customer if useful.
- Two disputes from the same user or Stripe customer within 12 months: suspend the account through the existing admin suspension endpoint while the second dispute is investigated. Unsuspend only after the owner records a clear reason.
- Any lost dispute plus abusive support behavior, false identity details, or repeated refund pressure: suspend the account and deny further paid access unless the owner approves reinstatement.
- A won dispute does not automatically clear the repeat-disputer count. The owner may mark a dispute as a genuine mistake in internal notes and exclude it from future policy decisions.
- Pro/API subscriptions: if suspended, also review Stripe subscription status and cancel in Stripe Dashboard when continued billing would be inappropriate.

Admin suspension record:

- Action: suspend user.
- Reason: `repeat_dispute_review`, `lost_dispute`, or `dispute_abuse_review`.
- Include dispute id, payment intent id, number of disputes in the last 12 months, and owner decision summary.

## Dispute Lifecycle Model

### States

| State | Meaning |
|---|---|
| `detected` | Stripe dispute is visible in Dashboard or received by webhook. |
| `triaged` | Owner recorded deadline, reason, amount, and matching internal user/payment. |
| `evidence_ready` | Evidence checklist is complete and reviewed. |
| `submitted` | Owner submitted evidence in Stripe. |
| `accepted` | Owner accepted the dispute or Stripe did not allow a challenge. |
| `under_review` | Stripe/issuer is reviewing submitted evidence. |
| `won` | Issuer overturned the dispute. |
| `lost` | Issuer upheld the dispute. |
| `suspended_review` | User is suspended during repeat-disputer or abuse review. |

### Events

External events:

- Stripe creates dispute.
- Stripe evidence deadline approaches.
- Owner submits evidence.
- Owner accepts dispute.
- Stripe closes dispute as won.
- Stripe closes dispute as lost.
- Customer withdraws dispute.

Internal commands:

- Match dispute to user and purchase.
- Revoke unused credits through webhook processing.
- Record evidence package.
- Suspend user.
- Unsuspend user.
- Grant manual goodwill/reinstatement credits after owner approval.

### Transition Table

| From | Event | To | Side effects | Rejection behavior |
|---|---|---|---|---|
| none | Stripe creates dispute | `detected` | Webhook revokes unused disputed purchase credits. | If payment intent is missing, record manual investigation need. |
| `detected` | Match user/payment and deadline | `triaged` | Internal note contains dispute id, payment intent id, reason, deadline. | If no user/payment match, investigate Stripe customer and do not submit incomplete evidence. |
| `triaged` | Evidence checklist completed | `evidence_ready` | Evidence package ready for owner review. | Missing purchase or usage records blocks challenge unless owner accepts risk. |
| `evidence_ready` | Owner submits evidence | `submitted` | Stripe receives final evidence package. | Do not submit if evidence is inconsistent or deadline has passed. |
| `submitted` | Stripe marks response under review | `under_review` | Await issuer decision. | No extra files are sent after final submission. |
| `detected` or `triaged` | Owner accepts/not challengeable | `accepted` | No challenge submitted; unused credits remain revoked. | Do not later attempt to challenge the same final dispute. |
| `under_review` | Stripe closes won | `won` | Record outcome; optionally review suspension and manual credit grant. | Do not auto-grant credits. |
| `under_review` or `accepted` | Stripe closes lost | `lost` | Record loss; credits remain revoked; review suspension policy. | Do not issue duplicate refund for same payment. |
| any non-terminal | Repeat-disputer threshold reached | `suspended_review` | Admin suspension with audit reason. | Suspension requires owner review and admin authorization. |
| `suspended_review` | Owner approves reinstatement | prior non-terminal or terminal note | Admin unsuspension with audit reason. | Do not unsuspend without recorded rationale. |

### Invariants

- A disputed purchase never has more granted credits than consumed credits after dispute webhook processing.
- Evidence is submitted manually by the owner, never automatically by the app.
- Stripe's displayed evidence deadline is the source of truth.
- A final evidence submission cannot be edited or supplemented later.
- Secrets, tokens, private keys, credentials, and unrelated customer data are never included in evidence files or docs.
- Repeat-disputer suspension is an owner decision recorded in the admin audit trail.

### Illegal Transitions

- `detected` -> `submitted` without matching internal payment and completing the evidence checklist.
- `accepted` -> `submitted` after final acceptance.
- `won` -> automatic credit grant.
- `lost` -> duplicate refund for the same payment.
- Any state -> automatic Stripe evidence submission.
- Any state -> live charge creation from automation.

### Persistence Implications

- Existing persistent evidence lives in `RewriteCredit`, `UsagePeriod`, `AppUser`, `StripeEvent`, and `AdminAuditLog`.
- `RewriteCredit.StripePaymentIntentId` is the key field for matching a disputed payment intent to purchase credits.
- `AdminService.GetUserDetailAsync` is the current read surface for purchase, usage, credit, and subscription context after the user is known.
- Suspension decisions are persisted through `AppUser.SuspendedAt` and `AdminAuditLog`.
- PAY-26 intentionally does not add a dispute table or auto-submission store.

### Test Checklist For Future Code Changes

If a future issue adds a direct dispute evidence endpoint or dispute table, cover:

- Seeded disputed payment returns matching user purchase record, credits, usage periods, and subscription summary.
- Unknown payment intent returns not found without leaking other users.
- Duplicate `charge.dispute.created` and `charge.dispute.closed` webhooks leave credits clamped at consumed credits.
- Won dispute does not automatically grant credits.
- Repeat-disputer suspension requires admin authorization and writes an audit log.
- Evidence output redacts secrets and excludes private rewrite contents by default.

## Last Verified

- 2026-06-01: Runbook created for PAY-26.
- Official Stripe docs checked on 2026-06-01.
- Code references checked: `StripeEventService.RevokeDisputedChargeCreditsAsync`, `AdminService.GetUserDetailAsync`, and admin suspension flow.
