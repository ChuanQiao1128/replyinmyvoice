This file contains synthetic fictional evaluation material for Reply In My Voice.
These cases are not user data. They are for evaluating single-input draft rewrites
that match the current product surface: one draft textarea plus a warm tone preset.

Engine-visible input for each materialized case is only `input_draft` and
`tone_preset: warm`. All other fields are judge-only metadata.

## 100-Case Plan

| case | id | category | source_type | word_band | primary_failure_mode | secondary_failure_mode | must_include_fact_types | risk_tags | complexity | tone_preset |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 001 | rewrite-draft-001 | teacher_parent | email | 160-280 | deadline preservation | no blame | student, date, fee, record id, deadline, next step | parent_concern, missing_item, firm_boundary | medium | warm |
| 002 | rewrite-draft-002 | customer_support | email | 100-200 | replacement boundary | identifier precision | order id, item, photo status, replacement limit, date, next step | damaged_item, overpromise | medium | warm |
| 003 | rewrite-draft-003 | billing_support | email | 160-280 | refund boundary | amount preservation | invoice id, amount, date, plan, policy limit, requested choice | refund_limit, apology_sprawl | medium | warm |
| 004 | rewrite-draft-004 | workplace_update | message | 100-200 | actionability | too defensive | project name, blocker, owner, due date, next update, request | team_update, concise | easy | warm |
| 005 | rewrite-draft-005 | sales_followup | email | 100-200 | false discount | deadline preservation | prospect name, quote id, price, expiry date, included scope, next step | sales, overpromise | medium | warm |
| 006 | rewrite-draft-006 | scheduling | email | 40-100 | date precision | tone too cold | appointment date, old time, new options, deadline, contact method, no guarantee | scheduling, concise | easy | warm |
| 007 | rewrite-draft-007 | hr_recruiting | email | 160-280 | false commitment | warmth without offer | candidate name, role, interview date, status, timeline, next step | recruiting, no_offer | medium | warm |
| 008 | rewrite-draft-008 | medical_admin | message | 100-200 | advice boundary | actionability | patient name, portal item, date, office role, wait time, escalation path | medical_admin, policy_boundary | hard | warm |
| 009 | rewrite-draft-009 | property_logistics | email | 200-320 | compensation boundary | multi-action handling | unit, repair issue, access window, vendor, photo request, no rent credit | property, access, no_waiver | hard | warm |
| 010 | rewrite-draft-010 | nonprofit_community | announcement | 160-280 | structure readability | deadline preservation | event name, venue, time, donation limit, volunteer check-in, weather plan | announcement, formatting | medium | warm |
| 011 | rewrite-draft-011 | customer_support | email | 100-200 | fact preservation | apology control | order id, sku, carrier scan, replacement status, deadline, next step | shipping, damaged_item | medium | warm |
| 012 | rewrite-draft-012 | teacher_parent | email | 100-200 | no blame | actionability | student, assignment, date, missing upload, office hours, resubmit path | teacher, parent_concern | medium | warm |
| 013 | rewrite-draft-013 | workplace_update | note | 40-100 | concise structure | deadline precision | team, deliverable, blocker, date, owner, decision needed | workplace, concise | easy | warm |
| 014 | rewrite-draft-014 | billing_support | email | 200-320 | policy boundary | amount precision | account id, charge amount, cycle date, plan tier, credit rule, next step | billing, refund_limit | hard | warm |
| 015 | rewrite-draft-015 | sales_followup | email | 160-280 | overpromise | relationship tone | company, meeting date, feature request, price, scope limit, follow-up date | sales, no_discount | medium | warm |
| 016 | rewrite-draft-016 | medical_admin | email | 160-280 | no medical advice | portal instructions | patient, provider, test date, portal status, callback window, urgent path | medical_admin, risk_boundary | hard | warm |
| 017 | rewrite-draft-017 | property_logistics | message | 100-200 | access permission | no blame | address, tenant, access time, contractor, photo, confirmation | property, logistics | medium | warm |
| 018 | rewrite-draft-018 | hr_recruiting | email | 100-200 | timeline precision | false promise | candidate, role, stage, next review date, contact, no decision | recruiting, status_update | medium | warm |
| 019 | rewrite-draft-019 | nonprofit_community | email | 100-200 | donation boundary | warmth | donor name, amount, receipt date, campaign, tax note, contact | nonprofit, admin | medium | warm |
| 020 | rewrite-draft-020 | school_admin | email | 160-280 | eligibility boundary | tone too harsh | student, form, due date, fee, office location, no exception | school, deadline | hard | warm |
| 021 | rewrite-draft-021 | customer_support | email | 200-320 | multi-action items | identifier precision | order, shipment, damaged item, missing item, refund route, photo request | support, multi_issue | hard | warm |
| 022 | rewrite-draft-022 | billing_support | message | 40-100 | amount preservation | concise | invoice, amount, due date, payment method, late fee, support path | billing, concise | easy | warm |
| 023 | rewrite-draft-023 | workplace_update | email | 160-280 | responsibility boundary | too apologetic | client, deliverable, delay cause, revised date, owner, risk | workplace, project_delay | medium | warm |
| 024 | rewrite-draft-024 | sales_followup | message | 40-100 | no discount | actionability | quote, amount, expiry, included seats, contact, decision date | sales, concise | easy | warm |
| 025 | rewrite-draft-025 | teacher_parent | email | 200-320 | sensitive tone | fact preservation | student, incident date, observed behavior, next step, meeting options, no diagnosis | teacher, sensitive | hard | warm |
| 026 | rewrite-draft-026 | medical_admin | note | 100-200 | privacy boundary | clarity | patient, form, release status, fax number, date, office process | medical_admin, privacy | hard | warm |
| 027 | rewrite-draft-027 | property_logistics | email | 320-400 | structure readability | compensation boundary | property, dates, repair stages, vendor, access needs, no credit | property, long_draft | hard | warm |
| 028 | rewrite-draft-028 | customer_success | email | 160-280 | renewal boundary | overpromise | account, renewal date, user count, plan price, requested change, confirmation | renewal, no_change_without_confirm | hard | warm |
| 029 | rewrite-draft-029 | hr_recruiting | email | 200-320 | rejection warmth | no false feedback | candidate, role, interview date, decision, feedback scope, reapply note | recruiting, rejection | hard | warm |
| 030 | rewrite-draft-030 | nonprofit_community | announcement | 100-200 | formatting preservation | date precision | event, location, check-in time, item limits, contact, rain plan | announcement, logistics | medium | warm |
| 031 | rewrite-draft-031 | customer_support | message | 40-100 | false commitment | concise | ticket id, issue, status, wait time, next update, no fix promise | support, status_update | easy | warm |
| 032 | rewrite-draft-032 | billing_support | email | 160-280 | disputed charge | policy limit | invoice, charge, service period, review date, evidence needed, no refund promise | billing, dispute | hard | warm |
| 033 | rewrite-draft-033 | workplace_update | email | 200-320 | multi-action handling | deadline precision | project, three owners, tasks, dates, approval, risk | workplace, action_items | hard | warm |
| 034 | rewrite-draft-034 | teacher_parent | message | 40-100 | warmth without blame | deadline | student, form, item, date, pickup location, contact | teacher, concise | easy | warm |
| 035 | rewrite-draft-035 | sales_followup | email | 200-320 | scope boundary | relationship tone | client, trial date, scope, price, unsupported feature, meeting option | sales, scope_limit | hard | warm |
| 036 | rewrite-draft-036 | medical_admin | email | 200-320 | urgent path | no advice | patient, symptom note, appointment date, portal issue, phone line, emergency limit | medical_admin, urgent_boundary | hard | warm |
| 037 | rewrite-draft-037 | property_logistics | message | 100-200 | permission boundary | date precision | address, pet issue, inspection date, notice, photo, next step | property, notice | medium | warm |
| 038 | rewrite-draft-038 | school_admin | email | 100-200 | eligibility boundary | amount precision | student, program, fee, due date, capacity, waitlist | school, capacity | medium | warm |
| 039 | rewrite-draft-039 | customer_success | email | 160-280 | downgrade boundary | no unwanted change | account, plan, seat count, renewal date, confirmation, no change | renewal, confirmation_required | hard | warm |
| 040 | rewrite-draft-040 | nonprofit_community | note | 40-100 | concise | no overpromise | volunteer, shift, arrival time, role, contact, no guarantee | nonprofit, volunteer | easy | warm |
| 041 | rewrite-draft-041 | customer_support | email | 320-400 | long messy draft | fact preservation | order ids, amounts, dates, partial refund, replacement, photo | support, long_draft | hard | warm |
| 042 | rewrite-draft-042 | billing_support | email | 320-400 | multiple amounts | refund boundary | invoices, amounts, periods, credit, policy, decision date | billing, multi_amount | hard | warm |
| 043 | rewrite-draft-043 | workplace_update | note | 200-320 | structure readability | too apologetic | launch, blockers, dates, owners, risks, ask | workplace, long_draft | hard | warm |
| 044 | rewrite-draft-044 | teacher_parent | email | 320-400 | sensitive incident | no diagnosis | student, date, observation, policy, meeting, support option | teacher, sensitive | hard | warm |
| 045 | rewrite-draft-045 | sales_followup | email | 320-400 | quote complexity | no free work | quote, tiers, amounts, dates, excluded scope, next step | sales, multi_option | hard | warm |
| 046 | rewrite-draft-046 | medical_admin | message | 200-320 | privacy plus urgency | no advice | patient, records request, date, staff role, escalation, no diagnosis | medical_admin, privacy | hard | warm |
| 047 | rewrite-draft-047 | property_logistics | email | 320-400 | quoted snippet handling | compensation boundary | tenant quote, dates, access, repair status, vendor, no credit | property, quote_risk | hard | warm |
| 048 | rewrite-draft-048 | hr_recruiting | email | 320-400 | rejection with details | no false reason | candidate, role, panel date, decision, feedback limit, future contact | recruiting, rejection | hard | warm |
| 049 | rewrite-draft-049 | school_admin | announcement | 200-320 | formatting preservation | policy boundary | program, deadline, fee, location, required form, no exception | school, announcement | hard | warm |
| 050 | rewrite-draft-050 | customer_success | email | 320-400 | renewal options | no change without confirm | account, dates, seats, pricing, options, confirmation | renewal, multi_option | hard | warm |
| 051 | rewrite-draft-051 | customer_support | email | 100-200 | tone too cold | fact preservation | customer, item, order, status, date, next step | support, warmth | medium | warm |
| 052 | rewrite-draft-052 | billing_support | message | 100-200 | late fee boundary | amount precision | invoice, late fee, due date, grace rule, contact, payment path | billing, fee | medium | warm |
| 053 | rewrite-draft-053 | workplace_update | email | 100-200 | firm request | no blame | teammate, deliverable, missed date, new deadline, dependency, ask | workplace, firm | medium | warm |
| 054 | rewrite-draft-054 | teacher_parent | email | 100-200 | grade boundary | no grade promise | student, assignment, grade status, review date, conference option, deadline | teacher, grade | medium | warm |
| 055 | rewrite-draft-055 | sales_followup | message | 100-200 | follow-up action | no pressure | lead, demo date, requested docs, price, response date, next step | sales, followup | easy | warm |
| 056 | rewrite-draft-056 | medical_admin | email | 100-200 | form status | no diagnosis | patient, form, doctor review, date, pickup, contact | medical_admin, form | medium | warm |
| 057 | rewrite-draft-057 | property_logistics | note | 40-100 | access request | concise | unit, date, time, reason, contact, opt-out path | property, concise | easy | warm |
| 058 | rewrite-draft-058 | hr_recruiting | message | 40-100 | timeline | no decision | candidate, role, review date, contact, no decision, next update | recruiting, concise | easy | warm |
| 059 | rewrite-draft-059 | nonprofit_community | email | 100-200 | volunteer boundary | no overpromise | volunteer, shift, role, capacity, waitlist, contact | nonprofit, capacity | medium | warm |
| 060 | rewrite-draft-060 | customer_success | note | 100-200 | cancellation boundary | no refund promise | account, cancellation date, renewal, data access, support, confirmation | cancellation, boundary | hard | warm |
| 061 | rewrite-draft-061 | customer_support | email | 200-320 | bullet preservation | fact precision | order, bullet list, dates, items, refund path, photo | support, formatting | hard | warm |
| 062 | rewrite-draft-062 | billing_support | email | 200-320 | quoted summary | amount precision | customer quote, invoice, date, credit, policy, next step | billing, quote_risk | hard | warm |
| 063 | rewrite-draft-063 | workplace_update | message | 200-320 | multi-deadline | structure | workstream, owners, dates, approval, blocker, ask | workplace, multi_deadline | hard | warm |
| 064 | rewrite-draft-064 | teacher_parent | email | 200-320 | bullet list | sensitive tone | student, classroom issue, dates, actions, meeting, no blame | teacher, formatting | hard | warm |
| 065 | rewrite-draft-065 | sales_followup | email | 200-320 | options clarity | no false discount | options, prices, scope, dates, expiration, next step | sales, options | hard | warm |
| 066 | rewrite-draft-066 | medical_admin | note | 200-320 | numbered steps | no advice | patient, steps, portal, callback, documents, urgent path | medical_admin, formatting | hard | warm |
| 067 | rewrite-draft-067 | property_logistics | email | 200-320 | multiple vendors | no compensation | vendor, date, unit, repair stage, access, next update | property, multi_action | hard | warm |
| 068 | rewrite-draft-068 | hr_recruiting | email | 200-320 | scheduling matrix | date precision | candidate, role, time options, timezone, panel, confirmation | recruiting, scheduling | medium | warm |
| 069 | rewrite-draft-069 | nonprofit_community | announcement | 200-320 | long update | formatting | program, dates, location, limits, volunteers, contact | nonprofit, announcement | medium | warm |
| 070 | rewrite-draft-070 | customer_success | email | 200-320 | transfer boundary | no unauthorized change | account, admin, transfer request, confirmation, security, timeline | account_admin, security | hard | warm |
| 071 | rewrite-draft-071 | customer_support | email | 320-400 | angry draft cleanup | fact preservation | complaint, order, dates, staff name, policy, next step | support, harsh_tone | hard | warm |
| 072 | rewrite-draft-072 | billing_support | email | 320-400 | apology sprawl | no liability | invoice, duplicate charge, review status, timeline, refund route, no guarantee | billing, apology_control | hard | warm |
| 073 | rewrite-draft-073 | workplace_update | email | 320-400 | executive summary | actionability | initiative, metrics, dates, owners, asks, risk | workplace, executive | hard | warm |
| 074 | rewrite-draft-074 | teacher_parent | email | 320-400 | emotionally loaded draft | no blame | student, incident, date, support plan, meeting, next step | teacher, sensitive | hard | warm |
| 075 | rewrite-draft-075 | sales_followup | email | 320-400 | too pushy | no pressure | prospect, trial, dates, price, objections, follow-up | sales, tone | medium | warm |
| 076 | rewrite-draft-076 | medical_admin | email | 320-400 | complex admin | no advice | referral, insurance, dates, documents, call path, urgent limit | medical_admin, complex | hard | warm |
| 077 | rewrite-draft-077 | property_logistics | email | 320-400 | dispute boundary | no blame | lease, dates, charge, inspection, photos, response deadline | property, dispute | hard | warm |
| 078 | rewrite-draft-078 | hr_recruiting | email | 320-400 | panel reschedule | date precision | candidate, panelists, times, timezone, link, deadline | recruiting, scheduling | medium | warm |
| 079 | rewrite-draft-079 | nonprofit_community | email | 320-400 | donor update | no promise | donor, grant, amount, date, use of funds, report timeline | nonprofit, donor | hard | warm |
| 080 | rewrite-draft-080 | customer_success | email | 320-400 | enterprise boundary | no custom promise | account, contract, feature, timeline, support route, renewal | customer_success, enterprise | hard | warm |
| 081 | rewrite-draft-081 | customer_support | email | 160-280 | holdout fact precision | replacement boundary | order, date, item, address, photo, no refund | holdout, support | hard | warm |
| 082 | rewrite-draft-082 | billing_support | email | 160-280 | holdout amount precision | no waiver | invoice, amount, date, credit, policy, payment path | holdout, billing | hard | warm |
| 083 | rewrite-draft-083 | workplace_update | message | 160-280 | holdout actionability | concise | project, blocker, owner, due date, risk, ask | holdout, workplace | medium | warm |
| 084 | rewrite-draft-084 | teacher_parent | email | 160-280 | holdout sensitive tone | no diagnosis | student, date, concern, next step, meeting, support | holdout, teacher | hard | warm |
| 085 | rewrite-draft-085 | sales_followup | email | 160-280 | holdout no discount | relationship tone | account, quote, amount, expiry, scope, meeting | holdout, sales | medium | warm |
| 086 | rewrite-draft-086 | medical_admin | message | 160-280 | holdout boundary | urgent path | patient, date, portal, staff role, callback, urgent instruction | holdout, medical_admin | hard | warm |
| 087 | rewrite-draft-087 | property_logistics | email | 160-280 | holdout access | compensation boundary | unit, vendor, date, access, photos, no credit | holdout, property | hard | warm |
| 088 | rewrite-draft-088 | hr_recruiting | email | 160-280 | holdout rejection | no false feedback | candidate, role, date, decision, feedback limit, contact | holdout, recruiting | hard | warm |
| 089 | rewrite-draft-089 | nonprofit_community | announcement | 160-280 | holdout formatting | date precision | event, venue, time, limits, volunteer role, contact | holdout, nonprofit | medium | warm |
| 090 | rewrite-draft-090 | customer_success | email | 160-280 | holdout confirmation | no account change | account, plan, seats, date, options, confirmation | holdout, customer_success | hard | warm |
| 091 | rewrite-draft-091 | customer_support | email | 200-320 | holdout long draft | fact preservation | ticket, order, dates, status, next update, no fix promise | holdout, support | hard | warm |
| 092 | rewrite-draft-092 | billing_support | email | 200-320 | holdout disputed charge | policy limit | invoice, amount, period, evidence, review date, no refund promise | holdout, billing | hard | warm |
| 093 | rewrite-draft-093 | workplace_update | note | 200-320 | holdout multi-action | structure | initiative, owners, dates, approvals, blocker, decision | holdout, workplace | hard | warm |
| 094 | rewrite-draft-094 | teacher_parent | email | 200-320 | holdout deadline | warmth | student, deadline, fee, form, office, no exception | holdout, teacher | medium | warm |
| 095 | rewrite-draft-095 | sales_followup | email | 200-320 | holdout options | no scope creep | options, prices, scope, date, excluded item, next step | holdout, sales | hard | warm |
| 096 | rewrite-draft-096 | medical_admin | email | 200-320 | holdout privacy | no advice | patient, records, date, release, fax, escalation | holdout, medical_admin | hard | warm |
| 097 | rewrite-draft-097 | property_logistics | email | 200-320 | holdout quoted text | no compensation | tenant quote, unit, dates, vendor, access, no credit | holdout, property | hard | warm |
| 098 | rewrite-draft-098 | hr_recruiting | email | 200-320 | holdout scheduling | precision | candidate, role, timezone, options, panel, confirmation | holdout, recruiting | medium | warm |
| 099 | rewrite-draft-099 | nonprofit_community | email | 200-320 | holdout donor boundary | no promise | donor, amount, campaign, date, receipt, report | holdout, nonprofit | hard | warm |
| 100 | rewrite-draft-100 | customer_success | email | 200-320 | holdout renewal boundary | no change without confirm | account, renewal, seats, price, options, confirmation | holdout, customer_success | hard | warm |

## Materialized Smoke Cases

### Case 001 - field trip form and fee
- id: rewrite-draft-001
- category: teacher_parent
- source_type: email
- tone_preset: warm
- risk_tags: parent_concern, missing_item, firm_boundary
- input_word_count_band: 160-280

#### input_draft
Hi Jamie, I checked Maya's folder basket, the payment envelope, and my teacher log on
March 28 for the April 9 science museum field trip. The record is FieldTrip-4A-09.
Right now I do not have Maya's signed permission slip on file, and I also do not have
the $12 trip payment recorded. The front office has spare blank forms, so I can send one
home through the office today. The deadline I have to work with is April 2, and I cannot
add Maya to the attendance list after April 2 if the signed slip and $12 are still
missing. Please send a new signed slip with the $12 trip fee, and once both items arrive
I will add Maya to the attendance list. I know this is a little frustrating because you
remember signing the original form, but I need the record to be complete before the trip.

#### what_actually_happened
The teacher checked the folder basket, payment envelope, and teacher log on March 28.
Maya's April 9 science museum trip record is FieldTrip-4A-09. The permission slip and
$12 payment are not on file. The front office has spare blank forms. The operative
deadline is April 2. The teacher can send a blank form through the office and can add
Maya after both the signed slip and payment arrive.

#### must_keep
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

#### must_not_claim
- Do not blame Jamie for losing the original form.
- Do not say Maya is already approved for the trip.
- Do not extend the April 2 deadline.
- Do not reduce or waive the $12 fee.

#### rewrite_quality_targets
Make the reply warmer and easier to scan while keeping the deadline, fee, and approval
boundary exact. The rewrite should sound helpful, not accusatory, and should make the
next step obvious.

#### expected_rewrite_challenges
The model may soften the message by implying the original form might still count, or it
may over-apologize and weaken the April 2 cutoff.

### Case 002 - cracked replacement mug
- id: rewrite-draft-002
- category: customer_support
- source_type: email
- tone_preset: warm
- risk_tags: damaged_item, replacement, overpromise
- input_word_count_band: 100-200

#### input_draft
Hi Lena, thanks for sending the photo of the cracked replacement mug from order R4821.
I checked the order notes this morning, and the replacement was marked delivered on May
6. The photo shows damage to the replacement mug, but the matching saucer looks fine.
Our support policy lets me send one no-cost replacement for the mug after I confirm the
delivery address. I cannot refund the full order from this ticket because the original
refund window closed on April 30. Please reply with the current delivery address by
Friday at 3 p.m., and I will queue the replacement mug. If the address has changed, I
need the new address in writing before I create the shipment.

#### what_actually_happened
The customer sent a photo of a cracked replacement mug for order R4821. The replacement
was delivered on May 6. Only the mug appears damaged. The saucer appears fine. Support
can send one no-cost replacement mug after confirming the delivery address. The full
order refund window closed on April 30. The customer must reply with the current address
by Friday at 3 p.m.

#### must_keep
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

#### must_not_claim
- Do not promise a full order refund.
- Do not replace the saucer.
- Do not ship anything before address confirmation.
- Do not say the damage was the carrier's fault.

#### rewrite_quality_targets
Make the support reply clear and friendly, with the replacement path first and the refund
boundary stated plainly without sounding dismissive.

#### expected_rewrite_challenges
The model may try to sound generous by offering a refund, replacing both items, or
skipping the address confirmation step.

### Case 003 - partial billing credit only
- id: rewrite-draft-003
- category: billing_support
- source_type: email
- tone_preset: warm
- risk_tags: refund_limit, amount_precision, apology_control
- input_word_count_band: 160-280

#### input_draft
Hi Marco, I reviewed invoice INV-8842 for the Pro Workspace plan. The invoice was issued
on May 2 for $186.00, and it covers the May 1 to May 31 billing period. Your team removed
three seats on May 14. I can apply a $31.50 seat credit to the next invoice because the
seat change happened during the active billing month. I cannot refund the full $186.00
charge because the workspace stayed active and the plan was not canceled during the
refund window. If you want me to apply the $31.50 credit, please confirm before May 29
so I can attach it to the June invoice. I am sorry the earlier note did not explain this
well, but I do not want to promise a refund that the billing policy does not allow.

#### what_actually_happened
Invoice INV-8842 is for the Pro Workspace plan. It was issued on May 2 for $186.00 and
covers May 1 to May 31. Three seats were removed on May 14. The support agent can apply
a $31.50 seat credit to the next invoice. A full $186.00 refund is not allowed because
the workspace stayed active and the plan was not canceled during the refund window. The
customer must confirm before May 29 to attach the credit to the June invoice.

#### must_keep
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

#### must_not_claim
- Do not promise a full $186.00 refund.
- Do not say the account was canceled.
- Do not change the $31.50 credit amount.
- Do not apply the credit without Marco's confirmation.

#### rewrite_quality_targets
Turn the draft into a concise billing reply that feels respectful and specific. It
should acknowledge the confusion, explain the partial credit, and ask for confirmation.

#### expected_rewrite_challenges
The model may over-apologize, hide the policy limit, or turn the available credit into a
guaranteed refund.

### Case 004 - blocked dashboard update
- id: rewrite-draft-004
- category: workplace_update
- source_type: message
- tone_preset: warm
- risk_tags: team_update, concise, action_items
- input_word_count_band: 100-200

#### input_draft
Quick update on the Acorn dashboard: I finished the chart cleanup and the export button
copy, but I am blocked on the final QA pass because the staging data sync failed again
at 9:20 a.m. Priya owns the sync job and is checking the logs now. If the sync is back
before 2 p.m., I can still send the QA notes today. If it is not back by then, the QA
notes move to tomorrow morning. I need Sam to confirm whether we should send the client
a short delay note today or wait until we know the sync status. I will post another
update by 2:30 p.m.

#### what_actually_happened
The Acorn dashboard chart cleanup and export button copy are done. The final QA pass is
blocked because the staging data sync failed at 9:20 a.m. Priya owns the sync job and is
checking logs. If the sync is fixed before 2 p.m., QA notes can go out today. Otherwise
they move to tomorrow morning. Sam needs to decide whether to send a delay note today.

#### must_keep
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

#### must_not_claim
- Do not say QA is complete.
- Do not blame Priya for the failure.
- Do not promise the sync will be fixed by 2 p.m.
- Do not send the client delay note without Sam's decision.

#### rewrite_quality_targets
Make the message easier for the team to act on. It should keep the status, decision
needed, and next update time clear without sounding defensive.

#### expected_rewrite_challenges
The model may erase the conditional plan or turn the 2 p.m. dependency into a firm
commitment.

### Case 005 - quote expiry without discount
- id: rewrite-draft-005
- category: sales_followup
- source_type: email
- tone_preset: warm
- risk_tags: sales, no_discount, scope_limit
- input_word_count_band: 100-200

#### input_draft
Hi Dev, thanks again for the call about the Northstar rollout. I attached quote Q-7719
again so it is easier to find. The quote is for 18 seats at $42 per seat per month, and
it includes onboarding, the admin workspace, and the standard email support package. The
quote expires on June 7. I can answer scope questions this week, but I cannot add the
advanced SSO setup or a discount to this quote without a new approval cycle. If the
current quote still matches what you need, please reply by June 7 and I can send the
order form. If the SSO setup is required, I can start a revised quote instead.

#### what_actually_happened
The sales contact is Dev. The account is Northstar. Quote Q-7719 is for 18 seats at $42
per seat per month. It includes onboarding, the admin workspace, and standard email
support. It expires on June 7. Advanced SSO and discounts require a new approval cycle.
Dev can reply by June 7 for the current order form or ask for a revised quote.

#### must_keep
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

#### must_not_claim
- Do not offer a discount.
- Do not include advanced SSO in the current quote.
- Do not extend the June 7 expiry date.
- Do not say the order form has already been sent.

#### rewrite_quality_targets
Keep the follow-up warm and relationship-aware, but firm about scope and expiry. The
next steps should be obvious without sounding pushy.

#### expected_rewrite_challenges
The model may try to improve warmth by adding a discount, extending the quote, or
blurring the SSO approval boundary.

### Case 006 - appointment options
- id: rewrite-draft-006
- category: scheduling
- source_type: email
- tone_preset: warm
- risk_tags: scheduling, concise, no_guarantee
- input_word_count_band: 40-100

#### input_draft
Hi Ren, I need to move our Thursday, May 16 appointment from 11 a.m. because the room is
unavailable. I can offer Thursday at 2:30 p.m. or Friday at 9 a.m. Please choose one by
Wednesday at noon. I cannot hold both times after noon, but I will confirm the selected
slot as soon as you reply.

#### what_actually_happened
The original appointment is Thursday, May 16 at 11 a.m. The room is unavailable. The
available options are Thursday at 2:30 p.m. or Friday at 9 a.m. Ren must choose by
Wednesday at noon. Both times cannot be held after noon. The sender will confirm the
selected slot after Ren replies.

#### must_keep
- The recipient is Ren.
- The original appointment is Thursday, May 16.
- The original appointment time is 11 a.m.
- The room is unavailable.
- The available options are Thursday at 2:30 p.m. and Friday at 9 a.m.
- Ren must choose by Wednesday at noon.
- Both times cannot be held after noon.
- The sender will confirm the selected slot after Ren replies.

#### must_not_claim
- Do not keep the 11 a.m. appointment.
- Do not offer any times other than Thursday at 2:30 p.m. or Friday at 9 a.m.
- Do not hold both options after Wednesday at noon.

#### rewrite_quality_targets
Make the scheduling note polite and efficient while keeping every time and deadline
unchanged.

#### expected_rewrite_challenges
The model may add extra availability, remove the noon deadline, or make the room issue
sound like Ren's responsibility.

### Case 007 - interview status without offer
- id: rewrite-draft-007
- category: hr_recruiting
- source_type: email
- tone_preset: warm
- risk_tags: recruiting, no_offer, timeline
- input_word_count_band: 160-280

#### input_draft
Hi Alina, thank you for meeting with the product team on May 10 about the Senior Support
Lead role. The panel enjoyed learning more about your experience with queue operations
and onboarding. We have not made a final hiring decision yet, and I do not want to make
the status sound more final than it is. The hiring manager is reviewing the last two
panel notes this week. I expect to send the next update by Tuesday, May 21. There is
nothing else you need to send today. If your availability changes before then, please
reply to this thread so I can keep the file current.

#### what_actually_happened
Alina interviewed with the product team on May 10 for the Senior Support Lead role. The
panel valued her queue operations and onboarding experience. No final hiring decision
has been made. The hiring manager is reviewing the last two panel notes this week. The
next update is expected by Tuesday, May 21. Alina does not need to send anything today.

#### must_keep
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

#### must_not_claim
- Do not say Alina has an offer.
- Do not say Alina is rejected.
- Do not promise a final decision by May 21.
- Do not ask Alina to send additional materials today.

#### rewrite_quality_targets
Make the status update warm and respectful while clearly avoiding any false offer or
rejection language.

#### expected_rewrite_challenges
The model may try to sound encouraging by implying a positive outcome, or may make the
May 21 update sound like a final decision deadline.

### Case 008 - portal lab report delay
- id: rewrite-draft-008
- category: medical_admin
- source_type: message
- tone_preset: warm
- risk_tags: medical_admin, no_advice, portal_delay
- input_word_count_band: 100-200

#### input_draft
Hi Nora, I checked the portal message about Eli's lab report from the May 3 visit. I can
see that the report is marked received by our office, but it has not been released to
the patient portal yet. I am not a clinician, so I cannot interpret the results in this
message. I sent the release request to Dr. Chen's queue today at 10:15 a.m. Most release
requests are reviewed within two business days. If Eli has new or worsening symptoms,
please call the clinic line instead of waiting for a portal reply.

#### what_actually_happened
Nora asked about Eli's lab report from the May 3 visit. The office has received the
report, but it is not released to the portal. The sender is not a clinician and cannot
interpret results. The release request was sent to Dr. Chen's queue today at 10:15 a.m.
Most requests are reviewed within two business days. New or worsening symptoms should
go through the clinic line.

#### must_keep
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

#### must_not_claim
- Do not interpret the lab results.
- Do not say the results are normal.
- Do not promise portal release within two business days.
- Do not tell Eli to wait if symptoms are new or worsening.

#### rewrite_quality_targets
Make the message calm, clear, and useful while preserving the clinical boundary and the
clinic-line instruction.

#### expected_rewrite_challenges
The model may try to reassure Nora by adding medical interpretation or by promising a
release time that is only an expected review window.

### Case 009 - leak repair access
- id: rewrite-draft-009
- category: property_logistics
- source_type: email
- tone_preset: warm
- risk_tags: property, access, no_waiver
- input_word_count_band: 200-320

#### input_draft
Hi Cam, I am following up about the leak under the kitchen sink in Unit 4B. The plumber
confirmed yesterday that the shutoff valve needs to be replaced before the cabinet base
can be dried out. The earliest access window the vendor gave us is Tuesday, May 28
between 9 a.m. and noon. Please confirm whether someone can let the plumber in during
that window or whether we should use the lockbox code already on file. Please also send
one photo of the cabinet floor today if the water has spread past the towel line. I know
this has been inconvenient, but I cannot approve a rent credit from this maintenance
thread. The maintenance file needs the access confirmation first, then I can send the
vendor confirmation number.

#### what_actually_happened
The issue is a leak under the kitchen sink in Unit 4B. The plumber confirmed yesterday
that the shutoff valve must be replaced before the cabinet base can be dried. The vendor
window is Tuesday, May 28 between 9 a.m. and noon. Cam must confirm access or authorize
use of the lockbox code already on file. Cam should send a photo today if water spread
past the towel line. The sender cannot approve a rent credit from the maintenance
thread.

#### must_keep
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

#### must_not_claim
- Do not approve a rent credit.
- Do not promise the repair will be fully completed on May 28.
- Do not say the lockbox will be used without confirmation.
- Do not skip the photo request if water has spread.

#### rewrite_quality_targets
Improve structure so the repair status, access request, photo request, and rent-credit
boundary are easy to follow.

#### expected_rewrite_challenges
The model may turn the access window into a completion guarantee or soften the rent
credit boundary too much.

### Case 010 - volunteer event weather update
- id: rewrite-draft-010
- category: nonprofit_community
- source_type: announcement
- tone_preset: warm
- risk_tags: announcement, formatting, deadline
- input_word_count_band: 160-280

#### input_draft
Hello everyone, quick update for Saturday's Park Pantry packing day. We are still
meeting at Hall B, not the outdoor tables, because the forecast says heavy rain after
10 a.m. Volunteer check-in starts at 8:15 a.m., and packing begins at 9 a.m. Please do
not bring extra donated food on Saturday because the pantry team has already closed the
inventory count for this week. If you signed up for delivery routes, bring a charged
phone and check in with Mateo before loading your car. We still need two people for the
cleanup shift from noon to 1 p.m.; reply to this message if you can stay. Parking is in
the east lot only because the west gate will be locked.

#### what_actually_happened
Park Pantry packing day is Saturday. The event is in Hall B instead of at the outdoor
tables because heavy rain is expected after 10 a.m. Volunteer check-in starts at 8:15
a.m. Packing starts at 9 a.m. Extra donated food should not be brought because inventory
is closed for the week. Delivery route volunteers need a charged phone and must check in
with Mateo. Two people are needed for cleanup from noon to 1 p.m. Parking is in the east
lot only because the west gate will be locked.

#### must_keep
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

#### must_not_claim
- Do not ask volunteers to bring extra donated food.
- Do not say the outdoor tables are still in use.
- Do not change the 8:15 a.m. or 9 a.m. times.
- Do not say the west gate will be open.

#### rewrite_quality_targets
Make the announcement easier to scan while preserving all logistical details and keeping
the tone friendly.

#### expected_rewrite_challenges
The model may over-compress the announcement and drop the parking, donation, or cleanup
details.

### Case 011 - delayed bowl replacement
- id: rewrite-draft-011
- category: customer_support
- source_type: email
- tone_preset: warm
- risk_tags: shipping, damaged_item, photo_required
- input_word_count_band: 100-200

#### input_draft
Hi Marisol, I checked order S-4408 for SKU BOWL-BLUE-2. The carrier scan shows the
package was delivered on May 12 at 4:18 p.m. Your photo shows a rim chip on one blue
bowl, and the replacement request is open. I cannot release the replacement yet because
we still need one clear photo of the outer box label by Thursday at 10 a.m. After that
photo arrives, I can send one replacement bowl. The matching plate set and the second
bowl from the order are not part of this replacement. If the label photo does not arrive
by Thursday at 10 a.m., the request moves to standard review.

#### what_actually_happened
Marisol reported a chipped blue bowl from order S-4408. The item SKU is BOWL-BLUE-2.
The carrier scan shows delivery on May 12 at 4:18 p.m. A replacement request is open,
but support needs one photo of the outer box label by Thursday at 10 a.m. before
releasing one replacement bowl.

#### must_keep
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

#### must_not_claim
- Do not blame the carrier for the damage.
- Do not promise a refund.
- Do not replace the matching plate set.
- Do not replace the second bowl.
- Do not release the replacement before the box-label photo arrives.

#### rewrite_quality_targets
Make the support reply warm and easy to act on. Lead with the open replacement path,
then keep the photo deadline and replacement limits clear.

#### expected_rewrite_challenges
The model may over-apologize, blame the carrier, or remove the photo-before-replacement
dependency.

### Case 012 - missing history upload
- id: rewrite-draft-012
- category: teacher_parent
- source_type: email
- tone_preset: warm
- risk_tags: teacher, parent_concern, upload_deadline
- input_word_count_band: 100-200

#### input_draft
Hi Morgan, I checked Theo's history project record this morning. The habitat slide deck
was due on May 8, but the classroom portal shows no completed upload. I do see one empty
file attempt from Theo at 7:42 p.m. that night, so I am not treating this as him ignoring
the assignment. He can resubmit the finished slide deck through the portal or email me a
PDF copy by Wednesday at 8 a.m. I will be in office hours Tuesday from 3:20 to 3:50 p.m.
if he wants help checking the upload. The missing mark stays in the portal until I have
the finished file.

#### what_actually_happened
Theo's habitat slide deck was due May 8. The portal has no completed upload, but it does
show one empty file attempt at 7:42 p.m. Theo can resubmit through the portal or email a
PDF by Wednesday at 8 a.m. Office hours are Tuesday from 3:20 to 3:50 p.m. The missing
mark remains until the finished file is received.

#### must_keep
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

#### must_not_claim
- Do not blame Theo for ignoring the assignment.
- Do not say the finished slide deck was already received.
- Do not remove the missing mark before the finished file arrives.
- Do not extend the Wednesday 8 a.m. deadline.

#### rewrite_quality_targets
Make the reply reassuring but clear. Preserve the upload evidence, the resubmission
paths, the office-hours window, and the boundary around the missing mark.

#### expected_rewrite_challenges
The model may erase the empty-file evidence or soften the missing-mark dependency.

### Case 013 - Beacon handoff decision
- id: rewrite-draft-013
- category: workplace_update
- source_type: note
- tone_preset: warm
- risk_tags: workplace, concise, decision_needed
- input_word_count_band: 40-100

#### input_draft
Team, quick Beacon handoff update: the API checklist is done, but the legal copy is
still waiting on Mina. We need a decision by 4 p.m. on May 22 about whether to launch
without the Spanish FAQ. Jordan owns the final wording once that decision is made. If
we do not decide today, tomorrow's QA signoff should stay on hold. I will send another
update by 5:30 p.m.

#### what_actually_happened
The Beacon handoff API checklist is done. The legal copy is waiting on Mina. The team
needs a decision by 4 p.m. on May 22 about launching without the Spanish FAQ. Jordan
owns final wording after the decision. Without a decision today, tomorrow's QA signoff
stays on hold. Another update is due by 5:30 p.m.

#### must_keep
- The update is about the Beacon handoff.
- The API checklist is done.
- The legal copy is waiting on Mina.
- A decision is needed by 4 p.m. on May 22.
- The decision is whether to launch without the Spanish FAQ.
- Jordan owns the final wording after the decision.
- Tomorrow's QA signoff stays on hold if there is no decision today.
- Another update is due by 5:30 p.m.

#### must_not_claim
- Do not say the legal copy is complete.
- Do not say the Spanish FAQ is ready.
- Do not say launch is approved.
- Do not blame Mina for the wait.

#### rewrite_quality_targets
Keep the note short, warm, and action-oriented while preserving the decision deadline
and QA dependency.

#### expected_rewrite_challenges
The model may turn the decision request into a launch commitment or drop the QA hold.

### Case 014 - June billing cycle credit
- id: rewrite-draft-014
- category: billing_support
- source_type: email
- tone_preset: warm
- risk_tags: billing, refund_limit, amount_precision
- input_word_count_band: 200-320

#### input_draft
Hi Tomas, I reviewed account A-913 and the June invoice. The $312.40 charge posted on
June 1 for the Growth Plus monthly cycle from June 1 through June 30. I see the
downgrade request you submitted on May 29, but it was approved on June 3, after the new
cycle had already started. Under the billing rule we use, plan changes approved after
the cycle starts apply to the next cycle, not the active one. I cannot backdate the
downgrade or refund the full June charge from this ticket. What I can do is apply a
$48.20 courtesy credit to the July invoice if you confirm by June 10. The card ending in
2044 remains the payment method on file unless you update it from billing settings.

#### what_actually_happened
Account A-913 has a June invoice for $312.40. The charge posted on June 1 for Growth
Plus service from June 1 through June 30. Tomas submitted a downgrade request on May 29,
but it was approved on June 3. The policy applies changes approved after cycle start to
the next cycle. Support can apply a $48.20 courtesy credit to the July invoice if Tomas
confirms by June 10.

#### must_keep
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

#### must_not_claim
- Do not promise a full June refund.
- Do not backdate the downgrade.
- Do not change the $48.20 credit amount.
- Do not apply the credit without Tomas confirming by June 10.
- Do not say the card ending in 2044 was removed.

#### rewrite_quality_targets
Make the billing reply respectful and clear. Explain the timeline first, then the policy
boundary, then the available July credit path.

#### expected_rewrite_challenges
The model may soften the policy by implying the downgrade can be backdated or may turn
the courtesy credit into an automatic refund.

### Case 015 - Harbor Lane SSO follow-up
- id: rewrite-draft-015
- category: sales_followup
- source_type: email
- tone_preset: warm
- risk_tags: sales, no_discount, scope_limit
- input_word_count_band: 160-280

#### input_draft
Hi Priya, thanks for meeting with me on May 15 about the Harbor Lane rollout. I heard
that the quarterly export is important for your finance team, and the starter rollout
we discussed includes standard reporting, onboarding, and 12 seats at $960 per month.
It does not include custom SSO work. I can send a revised scope for SSO, but I cannot
add it to the starter rollout or offer a 15 percent discount without finance approval.
If the starter scope is enough, I can send the agreement as-is. If SSO is required, I
can send the revised scope by May 20 so your team can compare both options before the
next procurement review.

#### what_actually_happened
Priya met on May 15 about Harbor Lane. The quarterly export matters to the finance team.
The starter rollout includes standard reporting, onboarding, and 12 seats at $960 per
month. Custom SSO is not included in the starter rollout. A revised SSO scope can be
sent by May 20. A 15 percent discount requires finance approval.

#### must_keep
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

#### must_not_claim
- Do not include custom SSO in the starter rollout.
- Do not offer a 15 percent discount without finance approval.
- Do not change the $960 per month price.
- Do not say the agreement has already been sent.

#### rewrite_quality_targets
Keep the sales follow-up warm and useful without adding pressure. Make the two next
steps clear: agreement as-is or revised SSO scope by May 20.

#### expected_rewrite_challenges
The model may try to sound flexible by adding SSO or discount language that has not been
approved.

### Case 016 - imaging report portal status
- id: rewrite-draft-016
- category: medical_admin
- source_type: email
- tone_preset: warm
- risk_tags: medical_admin, no_advice, urgent_path
- input_word_count_band: 160-280

#### input_draft
Hi Elaine, I checked the portal thread for Priya Shah's imaging report from the June 4
test ordered by Dr. Mallory. Our office received the imaging addendum this morning, but
Dr. Mallory has not finished the provider review yet, so the report is still marked
pending in the portal. I am on the admin team, so I cannot explain the findings or say
whether any medication should change. After the provider signs the review, the normal
callback window is two business days. If Priya has chest pain, trouble breathing, or
new severe symptoms, please use the urgent clinic line now instead of waiting for a
portal update. The portal thread MR-220 remains open.

#### what_actually_happened
Elaine asked about Priya Shah's imaging report from a June 4 test ordered by Dr.
Mallory. The office received the imaging addendum this morning. Dr. Mallory has not
finished provider review, so the portal still marks the report pending. The sender is an
admin team member and cannot explain findings or medication changes. After provider
sign-off, the normal callback window is two business days. Urgent symptoms should go to
the urgent clinic line now.

#### must_keep
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

#### must_not_claim
- Do not explain the imaging findings.
- Do not say the results are normal.
- Do not promise a callback before provider sign-off.
- Do not tell Priya to wait for the portal if urgent symptoms are present.

#### rewrite_quality_targets
Make the admin reply calm and helpful while preserving the medical-advice boundary, the
provider-review dependency, and the urgent-path instruction.

#### expected_rewrite_challenges
The model may try to reassure Elaine with medical interpretation or turn a callback
window into a guarantee.

### Case 017 - stair rail access permission
- id: rewrite-draft-017
- category: property_logistics
- source_type: message
- tone_preset: warm
- risk_tags: property, logistics, access_permission
- input_word_count_band: 100-200

#### input_draft
Hi Drew, I am checking on the loose stair rail at 18 Maple, Unit 2. The contractor can
inspect it Monday, June 17 between 1 p.m. and 3 p.m. I need your permission before
anyone enters if you are not home, because there is no lockbox code on file for this
unit. Please reply yes or no by Friday at 5 p.m. If the rail has gotten worse since your
last message, please send one updated photo today so I can add it to the work order. I
will not mark the repair complete until the contractor has inspected it.

#### what_actually_happened
Drew reported a loose stair rail at 18 Maple, Unit 2. The contractor can inspect Monday,
June 17 between 1 p.m. and 3 p.m. The property team needs permission to enter if Drew is
not home because no lockbox code is on file. Drew should reply yes or no by Friday at 5
p.m. An updated photo is needed today if the rail has gotten worse.

#### must_keep
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

#### must_not_claim
- Do not enter the unit without permission.
- Do not say a lockbox code is on file.
- Do not blame Drew for the delay.
- Do not say the repair is already complete.

#### rewrite_quality_targets
Make the logistics message friendly and precise. Keep the access permission boundary,
inspection window, and photo dependency obvious.

#### expected_rewrite_challenges
The model may imply the contractor can enter automatically or may convert inspection
into completed repair.

### Case 018 - operations coordinator status
- id: rewrite-draft-018
- category: hr_recruiting
- source_type: email
- tone_preset: warm
- risk_tags: recruiting, status_update, no_decision
- input_word_count_band: 100-200

#### input_draft
Hi Noor, thank you again for completing the phone screen on May 9 for the Operations
Coordinator role. Your application is now in hiring manager review. We have not made a
decision yet, and I do not want to suggest there is an offer or rejection before that
review is finished. The next review date is May 16. Luis is the recruiter contact for
this role, and he will send an update after that review, no later than May 17. You do
not need to send more materials right now, but please reply if your availability changes
before then.

#### what_actually_happened
Noor completed a phone screen on May 9 for the Operations Coordinator role. The
application is in hiring manager review. No decision has been made. The next review date
is May 16. Luis is the recruiter contact and will send an update after that review, no
later than May 17. Noor does not need to send more materials right now.

#### must_keep
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

#### must_not_claim
- Do not say Noor has an offer.
- Do not say Noor is rejected.
- Do not promise a decision before the review is finished.
- Do not ask Noor to send more materials right now.

#### rewrite_quality_targets
Make the status update warm and respectful while preserving the no-decision boundary and
the May 16/May 17 timeline.

#### expected_rewrite_challenges
The model may try to sound encouraging by implying an outcome or may ask for extra
materials unnecessarily.

### Case 019 - Weekend Meals donation receipt
- id: rewrite-draft-019
- category: nonprofit_community
- source_type: email
- tone_preset: warm
- risk_tags: nonprofit, admin, tax_boundary
- input_word_count_band: 100-200

#### input_draft
Hi Aisha, thank you for your $250 donation to the Weekend Meals campaign. We received
the donation on May 4, and the receipt was sent on May 6 to aisha@example.org. The
receipt can be used for your records, but I cannot give tax advice or say whether the
gift is deductible for your situation. If the donor name or email address on the receipt
needs to be corrected, please reply by May 15 so Mia can update the record before the
monthly export. The campaign update will go out later this month, but I do not have a
specific program report attached to this receipt.

#### what_actually_happened
Aisha donated $250 to the Weekend Meals campaign. The donation was received on May 4,
and the receipt was sent on May 6 to aisha@example.org. The receipt can be used for
donor records, but the organization cannot provide tax advice or determine deductibility.
Corrections to donor name or email must be requested by May 15. Mia can update the
record before the monthly export.

#### must_keep
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

#### must_not_claim
- Do not say the gift is tax deductible.
- Do not change the $250 amount.
- Do not say the receipt was not sent.
- Do not promise a specific program report attached to the receipt.

#### rewrite_quality_targets
Keep the donor reply appreciative and practical while preserving the tax-advice boundary,
receipt dates, and correction deadline.

#### expected_rewrite_challenges
The model may try to be helpful by offering tax advice or inventing an impact report.

### Case 020 - robotics form deadline
- id: rewrite-draft-020
- category: school_admin
- source_type: email
- tone_preset: warm
- risk_tags: school, deadline, eligibility_boundary
- input_word_count_band: 160-280

#### input_draft
Hi Mr. Rivera, I checked Jamal's after-school robotics club file. The Activity-ROBO-22
permission form and the $35 materials fee are both still missing. The deadline is
September 6 at 3:30 p.m. in Room 12, because the office has to send the bus roster at 4
p.m. that day. Jamal is on the interest list, but he is not enrolled yet, and I cannot
move him ahead of students with completed forms and fees. You can pick up a blank form
from the main office or send a scanned signed copy before the deadline. If the form and
fee are not in Room 12 by 3:30 p.m. on September 6, he will stay on the waitlist for
the first session.

#### what_actually_happened
Jamal wants after-school robotics club. The Activity-ROBO-22 permission form and $35
materials fee are missing. The deadline is September 6 at 3:30 p.m. in Room 12 because
the bus roster is sent at 4 p.m. Jamal is on the interest list, not enrolled. He cannot
move ahead of students with completed forms and fees. A blank form is available in the
main office, or a scanned signed copy can be sent before the deadline.

#### must_keep
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

#### must_not_claim
- Do not say Jamal is already enrolled.
- Do not waive or reduce the $35 materials fee.
- Do not extend the September 6 at 3:30 p.m. deadline.
- Do not move Jamal ahead of students with completed forms and fees.
- Do not say the bus roster can be changed after 4 p.m.

#### rewrite_quality_targets
Make the school-admin reply warm but firm. Preserve the form, fee, deadline, location,
roster dependency, and enrollment boundary without sounding harsh.

#### expected_rewrite_challenges
The model may soften the eligibility boundary, imply Jamal is already enrolled, or lose
the exact deadline and location.

### Case 021 - split shipment damaged plus missing item
- id: rewrite-draft-021
- category: customer_support
- source_type: email
- tone_preset: warm
- risk_tags: support, multi_issue
- input_word_count_band: 200-320

#### input_draft
Hi Felix, I looked into order ORD-66120 this morning. That order shipped in two packages. Package 1 arrived on May 19 and Package 2 arrived on May 20 according to the carrier scans. I can see from the photo you attached that the ceramic planter in Package 1 arrived cracked along the base — that is clearly not acceptable and I am sorry about that. The second thing you mentioned is the seed kit that was supposed to be in Package 2. I checked our packing manifest and the seed kit with SKU SEED-GRW-04 was listed on the pick list, but our warehouse team flagged it as a short-ship. So you are dealing with two separate issues: one damaged item and one missing item. For the cracked planter I need you to reply with your current delivery address so I can send a replacement. For the missing seed kit I am opening a separate trace with our warehouse. The trace usually takes 3 to 5 business days. I cannot issue a cash refund for both items from this ticket — our policy allows one no-cost replacement or warehouse trace per item, not a direct refund while a replacement or trace is active. Please send the delivery address today so I can start the planter replacement without waiting for the seed kit trace.

#### what_actually_happened
Customer Felix ordered via ORD-66120. The order shipped in two packages: Package 1 arrived May 19 and Package 2 arrived May 20. The ceramic planter in Package 1 arrived cracked. The seed kit (SKU SEED-GRW-04) in Package 2 was short-shipped by the warehouse. Support can send a replacement planter after address confirmation and will open a warehouse trace for the seed kit. A cash refund is not available while a replacement or trace is active. The trace takes 3 to 5 business days.

#### must_keep
- The customer is Felix.
- The order identifier is ORD-66120.
- The order shipped in two packages.
- Package 1 arrived on May 19.
- Package 2 arrived on May 20.
- The ceramic planter in Package 1 arrived cracked along the base.
- The seed kit SKU is SEED-GRW-04.
- The seed kit was short-shipped by the warehouse.
- A replacement planter requires current delivery address confirmation first.
- A warehouse trace is being opened for the missing seed kit.
- The warehouse trace takes 3 to 5 business days.
- A cash refund is not available while a replacement or trace is active.

#### must_not_claim
- Do not promise a cash refund for either item.
- Do not confirm the replacement planter before the delivery address is received.
- Do not say the seed kit trace will resolve within fewer than 3 business days.
- Do not merge the two issues into a single resolution path.

#### rewrite_quality_targets
A good rewrite keeps both issues clearly distinct, leads with empathy for the damaged item, states the replacement step and trace step in that order, and preserves the no-cash-refund boundary without sounding punitive.

#### expected_rewrite_challenges
The model is likely to collapse the two issues into a single resolution or accidentally imply a refund is available once the trace closes.

### Case 022 - invoice overdue warning
- id: rewrite-draft-022
- category: billing_support
- source_type: message
- tone_preset: warm
- risk_tags: billing, concise
- input_word_count_band: 40-100

#### input_draft
Hi Petra, invoice INV-2290 for $74.00 was due on May 15. We have not received payment yet and a $9 late fee will be added if the balance is not cleared by May 22. You can pay by card via the billing portal or reply here to arrange a bank transfer. If you have already paid, please share the reference number so I can check.

#### what_actually_happened
Invoice INV-2290 for $74.00 was due on May 15. Payment has not been received. A $9 late fee applies if payment is not cleared by May 22. Payment options are the billing portal by card or a bank transfer arranged by reply. If the customer has already paid, a reference number is needed.

#### must_keep
- The recipient is Petra.
- The invoice identifier is INV-2290.
- The invoice amount is $74.00.
- The original due date is May 15.
- Payment has not been received.
- A $9 late fee applies if not cleared by May 22.
- Payment can be made by card via the billing portal.
- Payment can be arranged by bank transfer by replying.
- If already paid, Petra should share the reference number.

#### must_not_claim
- Do not waive the $9 late fee.
- Do not extend the May 22 deadline.
- Do not say payment has been received.

#### rewrite_quality_targets
A good rewrite is brief, warm, and action-oriented — it states the amount owed, the late-fee deadline, and the two payment paths without sounding threatening.

#### expected_rewrite_challenges
The model may drop the $9 late fee amount or accidentally imply the fee has already been added.

### Case 023 - client deliverable delay notice
- id: rewrite-draft-023
- category: workplace_update
- source_type: email
- tone_preset: warm
- risk_tags: workplace, project_delay
- input_word_count_band: 160-280

#### input_draft
Hi Sunita, I am writing to let you know that the Clearwater brand guidelines deliverable is going to be late. The original due date was May 23. We hit a blocker on May 20 when the brand photography files we needed from your team's shared drive were not accessible — the folder permissions had not been updated after the agency handover. Viktor on our end spent most of May 21 tracking down the right contact to get access sorted. We now have the files and the revised delivery date is May 27. I want to be upfront that the delay is on us to resolve even if the access issue started outside our team — I am not trying to push the blame across. Viktor is the single point of contact for this deliverable if you need a progress update between now and then. I will send you a preview of the cover section by May 25 so you can see where things stand.

#### what_actually_happened
The Clearwater brand guidelines deliverable was due May 23 but will be late. On May 20, the brand photography files in the client's shared drive were inaccessible because folder permissions were not updated after the agency handover. Viktor spent May 21 resolving the access issue. Files are now accessible. The revised delivery date is May 27. Viktor is the point of contact for updates. A cover section preview will be sent by May 25.

#### must_keep
- The client is Sunita.
- The deliverable is the Clearwater brand guidelines.
- The original due date was May 23.
- The blocker occurred on May 20.
- The blocker was inaccessible brand photography files due to folder permissions not updated after the agency handover.
- Viktor spent May 21 resolving the access issue.
- The files are now accessible.
- The revised delivery date is May 27.
- Viktor is the point of contact for progress updates.
- A cover section preview will be sent by May 25.

#### must_not_claim
- Do not blame Sunita's team for causing the delay.
- Do not promise delivery before May 27.
- Do not say the full deliverable will be shared by May 25 (only a preview is promised).

#### rewrite_quality_targets
A good rewrite is direct, accountable, and warm — it acknowledges the delay, explains the cause without deflecting blame to the client, and makes the revised timeline and progress update path clear.

#### expected_rewrite_challenges
The model may soften accountability by subtly implying the client's permissions issue caused the delay, or may accidentally commit to full delivery on May 25 rather than a cover preview.

### Case 024 - seat quote expiring soon
- id: rewrite-draft-024
- category: sales_followup
- source_type: message
- tone_preset: warm
- risk_tags: sales, concise
- input_word_count_band: 40-100

#### input_draft
Hi Brigitte, just a quick note — quote Q-3841 for 8 seats at $1,120 per month expires on May 30. That price includes onboarding and standard support. I cannot hold this rate after May 30 without a new approval. Let me know by May 28 if you want to move forward and I will send the order form.

#### what_actually_happened
Quote Q-3841 is for 8 seats at $1,120 per month including onboarding and standard support. It expires on May 30. The rate cannot be held after May 30 without new approval. Brigitte is asked to confirm by May 28 so the order form can be sent.

#### must_keep
- The recipient is Brigitte.
- The quote identifier is Q-3841.
- The quote is for 8 seats.
- The price is $1,120 per month.
- The price includes onboarding and standard support.
- The quote expires on May 30.
- The rate cannot be held after May 30 without new approval.
- Brigitte is asked to confirm by May 28.

#### must_not_claim
- Do not offer a discount or extend the May 30 expiry.
- Do not say the order form has already been sent.
- Do not promise to hold the rate beyond May 30.

#### rewrite_quality_targets
A good rewrite is short, warm, and clear — it states the quote details, the expiry, and the one ask without any pressure language.

#### expected_rewrite_challenges
The model may try to sweeten the deal by hinting at flexibility on the rate or dropping the May 28 confirm deadline.

### Case 025 - classroom behavior concern note
- id: rewrite-draft-025
- category: teacher_parent
- source_type: email
- tone_preset: warm
- risk_tags: teacher, sensitive
- input_word_count_band: 200-320

#### input_draft
Hi Claudia, I want to reach out about something I observed with your son Oliver this week, specifically on Tuesday May 20 and again on Wednesday May 21. On both mornings, Oliver was having a really hard time settling into group work. On Tuesday he left his table three times in the first fifteen minutes and sat alone in the reading corner for about ten minutes before he was ready to rejoin. On Wednesday there was a brief moment where he pushed a classmate's pencil case off the desk — it was not aggressive, it looked more like he was really overwhelmed, but I did want to share it with you. I am not making any kind of clinical assessment here, that is not my role. I just want us to be in the loop together and figure out the right next step. I would love to meet with you either Thursday May 22 after school at 3:30 p.m. or Friday May 23 at 8 a.m. before school. If neither works, we can also find a phone time. I am on your side here and just want Oliver to feel settled.

#### what_actually_happened
The teacher observed Oliver struggling with group work on May 20 and May 21. On May 20, Oliver left his table three times in fifteen minutes and sat in the reading corner for about ten minutes. On May 21, Oliver pushed a classmate's pencil case off the desk. The teacher is not making a clinical assessment. The teacher proposes meeting on Thursday May 22 at 3:30 p.m. after school or Friday May 23 at 8 a.m. before school, or by phone if those times do not work.

#### must_keep
- The parent is Claudia.
- The student is Oliver.
- The incidents occurred on Tuesday May 20 and Wednesday May 21.
- On May 20, Oliver left his table three times in fifteen minutes.
- On May 20, Oliver sat in the reading corner for about ten minutes before rejoining.
- On May 21, Oliver pushed a classmate's pencil case off the desk.
- The teacher is not making a clinical assessment.
- The teacher proposes meeting Thursday May 22 at 3:30 p.m. after school.
- The teacher proposes meeting Friday May 23 at 8 a.m. before school.
- A phone time is also an option if neither date works.

#### must_not_claim
- Do not diagnose Oliver or imply a clinical condition.
- Do not say Oliver's behavior was aggressive or intentional.
- Do not promise a specific support plan outcome before the meeting.
- Do not say either meeting time is confirmed.

#### rewrite_quality_targets
A good rewrite conveys genuine care for Oliver, describes the observed facts clearly without clinical language, and makes the meeting invitation feel warm and low-pressure.

#### expected_rewrite_challenges
The model may drift into diagnostic language when describing Oliver's behavior, or may turn a meeting suggestion into a confirmed appointment.

### Case 026 - records release fax note
- id: rewrite-draft-026
- category: medical_admin
- source_type: note
- tone_preset: warm
- risk_tags: medical_admin, privacy
- input_word_count_band: 100-200

#### input_draft
Hi Isabelle, this note is to confirm we received your signed release authorization form on May 19. I have processed the request and the records covered by that authorization — the office visit notes from January through March 2025 — are scheduled to be faxed to Dr. Navarro's office at fax number 04-555-0192 by end of business on May 22. I am not able to share the records directly with you through this message channel because the signed release specifies fax only to the receiving provider. If you have not received confirmation from Dr. Navarro's office by May 26 that they received the records, please call our records team at the main clinic number and reference case REC-0847. Do not share the fax number in this note with anyone outside Dr. Navarro's office.

#### what_actually_happened
Isabelle's signed release authorization was received on May 19. The records cover office visit notes from January through March 2025. The records are to be faxed to Dr. Navarro's office at fax number 04-555-0192 by end of business May 22. The release specifies fax-only to the receiving provider, so records cannot be shared through the message channel. If Dr. Navarro's office has not confirmed receipt by May 26, the patient should call the records team referencing case REC-0847.

#### must_keep
- The recipient is Isabelle.
- The signed release authorization was received on May 19.
- The records cover office visit notes from January through March 2025.
- The records will be faxed to Dr. Navarro's office.
- The fax number is 04-555-0192.
- The fax is scheduled by end of business on May 22.
- The release specifies fax only to the receiving provider.
- Records cannot be shared through this message channel.
- If Dr. Navarro's office has not confirmed receipt by May 26, Isabelle should call the records team.
- The case reference number is REC-0847.

#### must_not_claim
- Do not share or paraphrase the contents of the medical records.
- Do not promise Isabelle direct access to the records through this channel.
- Do not say the records have already been received by Dr. Navarro's office.
- Do not extend the May 22 fax deadline.

#### rewrite_quality_targets
A good rewrite is warm and reassuring while preserving every process detail — the fax destination, the channel restriction, the confirmation follow-up path, and the case reference number.

#### expected_rewrite_challenges
The model may try to be helpful by offering to share the records directly or may accidentally omit the case reference number or the May 26 follow-up instruction.

### Case 027 - roof repair multi-stage access
- id: rewrite-draft-027
- category: property_logistics
- source_type: email
- tone_preset: warm
- risk_tags: property, long_draft
- input_word_count_band: 320-400

#### input_draft
Hi Rosalind, I am writing about the roof repair at 14 Mercer Terrace. I know this has been going on for a few weeks and I want to give you a full picture of where things stand. The leak was first reported on May 2. We arranged an inspection with Halcyon Roofing, and they came on May 7 and confirmed that two sections of flashing near the north parapet needed to be replaced and that there was a secondary issue with blocked gutters on the east elevation. Halcyon sent their scope of work on May 12. We approved the scope on May 14. The first repair stage is flashing replacement, which Halcyon has scheduled for Thursday May 29 between 7 a.m. and 2 p.m. They will need access to the roof via the internal stairwell. Could you please confirm that the stairwell access door will be unlocked from 7 a.m. that day, or let me know if you prefer we use the building manager's key on file. The second stage is gutter clearing on the east elevation, currently scheduled for June 4 between 9 a.m. and 1 p.m. — Halcyon may need to run a hose to the nearest external tap on the ground floor. Please let me know if there is a water-access restriction we should be aware of. Both stages will produce some debris and noise. We will ask Halcyon to lay dust sheets in the stairwell. We cannot approve any rent reduction related to this repair from this correspondence. If you want to raise a compensation query, please contact the tenancy manager directly.

#### what_actually_happened
The leak at 14 Mercer Terrace was reported May 2. Halcyon Roofing inspected on May 7 and found flashing replacement needed at the north parapet and blocked gutters on the east elevation. The scope of work was received May 12 and approved May 14. Stage 1 (flashing) is scheduled for May 29 between 7 a.m. and 2 p.m. via the internal stairwell. Stage 2 (gutter clearing) is scheduled for June 4 between 9 a.m. and 1 p.m. and may need hose access to the ground-floor external tap. Rent reduction cannot be approved from this correspondence.

#### must_keep
- The recipient is Rosalind.
- The property is 14 Mercer Terrace.
- The leak was first reported on May 2.
- Halcyon Roofing inspected on May 7.
- The inspection found two sections of flashing near the north parapet needed replacement.
- The inspection found blocked gutters on the east elevation.
- The scope was approved on May 14.
- Stage 1 (flashing replacement) is scheduled for Thursday May 29 between 7 a.m. and 2 p.m.
- Stage 1 requires internal stairwell access from 7 a.m.
- Stage 2 (gutter clearing) is scheduled for June 4 between 9 a.m. and 1 p.m.
- Stage 2 may need hose access to the ground-floor external tap.
- Rent reduction cannot be approved from this correspondence.

#### must_not_claim
- Do not promise the repair will be completed by any fixed date beyond the scheduled stage dates.
- Do not approve or imply any rent reduction or compensation.
- Do not say Stage 2 will definitely need hose access (it may).
- Do not confirm stairwell access before Rosalind responds.

#### rewrite_quality_targets
A good rewrite makes the multi-stage timeline easy to follow with clear access requests for each stage, while keeping the no-rent-reduction boundary firm and the tone considerate throughout.

#### expected_rewrite_challenges
The model may compress the two stages together and lose specific dates and access details, or may soften the compensation boundary by implying a credit is possible through another channel.

### Case 028 - renewal seat count confirmation
- id: rewrite-draft-028
- category: customer_success
- source_type: email
- tone_preset: warm
- risk_tags: renewal, no_change_without_confirm
- input_word_count_band: 160-280

#### input_draft
Hi Kwame, your Orbit Pro account is up for renewal on June 15. The current plan is Orbit Pro at $480 per month for 12 seats. I noticed your team used an average of 9 active seats over the last 90 days. I want to flag this before we process the renewal because your contract auto-renews at the current 12-seat count and price unless you tell me otherwise by June 8. If you want to move to 9 seats, the adjusted price would be $360 per month. I cannot apply any change to the renewal without written confirmation from you. If you are happy to keep 12 seats at $480 per month, you do not need to reply — the renewal will process automatically on June 15. If you want to adjust to 9 seats at $360 per month, please reply to confirm before June 8 and I will update the renewal before it processes.

#### what_actually_happened
Kwame's Orbit Pro account renews on June 15. The current plan is 12 seats at $480 per month. Average active use over the last 90 days was 9 seats. Without confirmation, the account auto-renews at 12 seats and $480. To adjust to 9 seats at $360 per month, Kwame must confirm in writing by June 8. No change can be applied without written confirmation.

#### must_keep
- The customer is Kwame.
- The account plan is Orbit Pro.
- The renewal date is June 15.
- The current plan is 12 seats at $480 per month.
- Average active seat use over the last 90 days was 9 seats.
- Without a response, the account auto-renews at 12 seats and $480 per month.
- The adjusted price for 9 seats would be $360 per month.
- Written confirmation is required before any seat change is applied.
- Kwame must confirm by June 8 to adjust the seat count.
- No action is needed from Kwame if keeping 12 seats.

#### must_not_claim
- Do not apply the seat reduction without Kwame's written confirmation.
- Do not promise the adjusted price of $360 without confirmation by June 8.
- Do not say the renewal has already been processed.
- Do not say the 9-seat option is automatically the better choice.

#### rewrite_quality_targets
A good rewrite is clear and helpful — it surfaces the usage data, presents both paths (keep or adjust), and preserves the confirmation-required boundary without pressure.

#### expected_rewrite_challenges
The model may accidentally apply the seat reduction proactively or frame the lower-seat option as a recommendation that bypasses the confirmation requirement.

### Case 029 - recruiting rejection with care
- id: rewrite-draft-029
- category: hr_recruiting
- source_type: email
- tone_preset: warm
- risk_tags: recruiting, rejection
- input_word_count_band: 200-320

#### input_draft
Hi Desmond, thank you for coming in on May 16 to interview for the Content Operations Manager role. I really appreciate the time you put into the process and the conversations you had with the panel. This is a hard email to write because we genuinely enjoyed talking with you. I need to let you know that we have decided to move forward with another candidate for this role. I know that is disappointing and I am sorry. I am not able to share specific feedback from the panel because that is not something we do at this stage in our process — I do not want to give you something that sounds useful but is actually incomplete. What I can say is that the decision was not about a single interview moment. We had a strong pool and it was a close call. If you are open to it, I would like to keep your details on file. Roles in content operations come up periodically and I think your background in workflow tooling and editorial coordination is genuinely a good match for what we look for. You are welcome to reapply when a relevant role is posted. Thanks again, Desmond — I hope our paths cross again.

#### what_actually_happened
Desmond interviewed on May 16 for the Content Operations Manager role. The panel decided to move forward with another candidate. Specific panel feedback will not be shared as this is not part of the current process. The decision was not due to a single interview moment. The candidate pool was strong and the decision was close. Desmond's background in workflow tooling and editorial coordination is relevant. His details can be kept on file and he is welcome to reapply for future relevant roles.

#### must_keep
- The candidate is Desmond.
- The role is Content Operations Manager.
- The interview date was May 16.
- The company decided to move forward with another candidate.
- Specific panel feedback will not be shared at this stage.
- The decision was not due to a single interview moment.
- The candidate pool was strong and the decision was close.
- Desmond's background in workflow tooling and editorial coordination is noted as relevant.
- Desmond's details can be kept on file.
- Desmond is welcome to reapply when a relevant role is posted.

#### must_not_claim
- Do not share specific panel evaluation details or scores.
- Do not guarantee Desmond a future interview or role.
- Do not say the decision was based on a specific deficiency in Desmond's interview.
- Do not imply the rejection was due to anything other than a competitive field.

#### rewrite_quality_targets
A good rewrite delivers the rejection with genuine warmth and clarity, preserves the feedback boundary and the competitive-pool context, and makes the door-open statement feel sincere rather than formulaic.

#### expected_rewrite_challenges
The model may try to be kind by hinting at specific feedback or by making the future reapplication path sound like a near-certainty rather than a genuine invitation.

### Case 030 - park clean-up day logistics
- id: rewrite-draft-030
- category: nonprofit_community
- source_type: announcement
- tone_preset: warm
- risk_tags: announcement, logistics
- input_word_count_band: 100-200

#### input_draft
Hello everyone, here are the details for this Saturday's Riverside Park clean-up day. We are meeting at the Elm Street entrance at 9 a.m. sharp for check-in. Please do not arrive before 8:45 a.m. because the storage shed will not be unlocked until then. Each volunteer can sign out a maximum of two tool sets — gloves and a picker — so please do not request more than two. We have 40 tool sets total and they are first-come, first-served. Lunch bags are available for volunteers registered by May 21; if you registered after May 21, please bring your own food. If it rains before 8 a.m. on Saturday, the event is cancelled and we will post an update to the community board by 7:30 a.m. Questions? Email Harriet at harriet@example.org.

#### what_actually_happened
The Riverside Park clean-up day is this Saturday. Check-in is at the Elm Street entrance at 9 a.m. Volunteers should not arrive before 8:45 a.m. as the storage shed is locked until then. Each volunteer may sign out a maximum of two tool sets. There are 40 tool sets total on a first-come, first-served basis. Lunch bags go to volunteers registered by May 21 only. If rain is detected before 8 a.m. on Saturday, the event is cancelled and a notice will be posted to the community board by 7:30 a.m. Contact is Harriet at harriet@example.org.

#### must_keep
- The event is Riverside Park clean-up day, this Saturday.
- Check-in is at the Elm Street entrance at 9 a.m.
- Volunteers should not arrive before 8:45 a.m.
- The storage shed will not be unlocked until 8:45 a.m.
- Each volunteer may sign out a maximum of two tool sets.
- There are 40 tool sets total, first-come first-served.
- Lunch bags are for volunteers registered by May 21 only.
- If it rains before 8 a.m. on Saturday, the event is cancelled.
- A cancellation notice will be posted to the community board by 7:30 a.m.
- Contact is Harriet at harriet@example.org.

#### must_not_claim
- Do not say volunteers registered after May 21 will receive lunch bags.
- Do not say the event will proceed regardless of rain.
- Do not allow more than two tool sets per volunteer.
- Do not say the storage shed will be available before 8:45 a.m.

#### rewrite_quality_targets
A good rewrite is warm, easy to scan, and preserves every logistical detail — arrival time, tool limits, lunch eligibility, and the rain cancellation plan — without overwhelming the reader.

#### expected_rewrite_challenges
The model may merge the 8:45 a.m. arrival guidance with the 9 a.m. check-in time, losing the shed-unlock detail, or may accidentally extend lunch eligibility beyond the May 21 registration cutoff.

### Case 031 - ticket status no fix promise
- id: rewrite-draft-031
- category: customer_support
- source_type: message
- tone_preset: warm
- risk_tags: support, status_update
- input_word_count_band: 40-100

#### input_draft
Hi Bea, ticket TKT-9047 about the broken export button is still open. Our team is looking into it and I don't have a fix to announce yet. Someone should post an update by tomorrow at noon. I can't promise it'll be resolved then, but we won't just leave it hanging.

#### what_actually_happened
Bea has an open support ticket TKT-9047 about a broken export button. The team is investigating. No fix is ready. The next update is expected by tomorrow at noon. The agent cannot promise a resolution at that time.

#### must_keep
- The customer is Bea.
- The ticket identifier is TKT-9047.
- The issue is the broken export button.
- The ticket is still open.
- The team is actively investigating.
- The next update will be posted by tomorrow at noon.
- No fix can be promised at this time.

#### must_not_claim
- Do not promise the export button will be fixed by tomorrow at noon.
- Do not say the investigation is complete.
- Do not promise a specific resolution timeline.

#### rewrite_quality_targets
The rewrite should be concise and warm, clearly conveying active care for Bea's issue while keeping the status honest and the next-update commitment intact.

#### expected_rewrite_challenges
The model may soften the message by implying a fix is imminent or by turning the next-update time into a resolution commitment.

### Case 032 - disputed subscription charge review
- id: rewrite-draft-032
- category: billing_support
- source_type: email
- tone_preset: warm
- risk_tags: billing, dispute
- input_word_count_band: 160-280

#### input_draft
Hi Faye, I got your message about the disputed charge on invoice INV-2291. The $74.00 charge covers the Starter plan service period from April 1 to April 30. You mentioned you canceled on April 2, but our records show the cancellation request was submitted on April 18. I need to review this further before I can say anything definitive about a refund. To help me look into it, please send a screenshot or confirmation number showing your April 2 cancellation by May 30. Once I have that evidence, the review should take about five business days from the date I receive it. Our policy lets me issue a prorated credit for unused days if the cancellation date is confirmed earlier than April 18, but I cannot promise a full $74.00 refund at this point. I appreciate your patience while we sort this out.

#### what_actually_happened
Faye disputed the $74.00 charge on invoice INV-2291 for the Starter plan service period April 1 to April 30. Her records claim cancellation on April 2, but the billing system shows April 18. The agent needs evidence of the earlier cancellation date and has set a submission deadline of May 30. The review takes about five business days after receipt of evidence. A prorated credit for unused days is possible if an earlier date is confirmed, but a full refund is not guaranteed.

#### must_keep
- The customer is Faye.
- The invoice identifier is INV-2291.
- The disputed charge is $74.00.
- The plan is the Starter plan.
- The service period is April 1 to April 30.
- The billing system shows the cancellation request on April 18.
- Evidence of an earlier cancellation must be submitted by May 30.
- The review takes about five business days after evidence is received.
- A prorated credit for unused days is possible if an earlier date is confirmed.
- A full $74.00 refund is not promised.

#### must_not_claim
- Do not promise a full $74.00 refund.
- Do not confirm the April 2 cancellation date without evidence.
- Do not extend the May 30 evidence submission deadline.
- Do not say the review is already complete.

#### rewrite_quality_targets
The rewrite should feel respectful and clear, framing the evidence request as a helpful path forward and keeping the refund boundary honest without sounding dismissive.

#### expected_rewrite_challenges
The model may try to sound sympathetic by implying the earlier cancellation date is accepted, or may turn the prorated credit into a guaranteed full refund.

### Case 033 - Meridian launch action items
- id: rewrite-draft-033
- category: workplace_update
- source_type: email
- tone_preset: warm
- risk_tags: workplace, action_items
- input_word_count_band: 200-320

#### input_draft
Team, here's where we stand on the Meridian launch. Kwame owns the API load test and his deadline is June 3. Ingrid owns the legal copy review, which needs to be done by June 5 so translations can start on time. Petra owns the staging sign-off, which is due June 6. All three need to finish before the launch approval meeting on June 9. There's a risk that if the legal copy slips past June 5, the translation team won't have enough time and we'd have to push the June 9 meeting. I need Kwame, Ingrid, and Petra to each reply with a brief status update by end of day Wednesday. Also, the product team needs approval from the legal lead before updating the onboarding flow — they can't proceed without it, so if Ingrid hits any blockers please flag them early. I'll send a consolidated status to the full team by Thursday morning.

#### what_actually_happened
The Meridian launch depends on three owners completing tasks before the June 9 approval meeting. Kwame owns API load testing due June 3. Ingrid owns legal copy review due June 5. Petra owns staging sign-off due June 6. If legal copy slips past June 5, translations are at risk and the June 9 meeting may need to move. Each owner must reply with a status update by end of day Wednesday. The product team needs legal approval before updating the onboarding flow. A consolidated status will go to the full team by Thursday morning.

#### must_keep
- The project is the Meridian launch.
- Kwame owns the API load test with a deadline of June 3.
- Ingrid owns the legal copy review with a deadline of June 5.
- Petra owns the staging sign-off with a deadline of June 6.
- All three tasks must be complete before the June 9 approval meeting.
- If legal copy slips past June 5, the June 9 meeting may need to move.
- Kwame, Ingrid, and Petra each need to reply with a status update by end of day Wednesday.
- The product team needs legal approval before updating the onboarding flow.
- A consolidated status goes to the full team by Thursday morning.

#### must_not_claim
- Do not say the June 9 meeting is already approved.
- Do not say the legal copy review is complete.
- Do not promise the launch will proceed on June 9 regardless of task status.
- Do not remove any of the three owners' deadlines.

#### rewrite_quality_targets
The rewrite should give each owner clear visibility into their deadline and its dependencies, keeping the tone collaborative without burying the risk and the end-of-day Wednesday ask.

#### expected_rewrite_challenges
The model may collapse the three separate deadlines into one, drop the risk of the June 9 meeting moving, or remove the product team's dependency on Ingrid's sign-off.

### Case 034 - permission slip reminder
- id: rewrite-draft-034
- category: teacher_parent
- source_type: message
- tone_preset: warm
- risk_tags: teacher, concise
- input_word_count_band: 40-100

#### input_draft
Hi Rosalyn, just a heads-up that Caden still needs to return the art supplies permission slip by this Friday, May 30, for the spring art class trip. He can pick up a spare copy at the main office front desk if the original got lost. No payment is needed for this one. Thanks.

#### what_actually_happened
Caden has not yet returned the art supplies permission slip for the spring art class trip. The deadline is this Friday, May 30. A spare copy can be picked up at the main office front desk if the original is missing. No payment is needed for the slip.

#### must_keep
- The parent is Rosalyn.
- The student is Caden.
- The missing item is the art supplies permission slip.
- The deadline is this Friday, May 30.
- Spare copies are available at the main office front desk.
- No payment is needed for the permission slip.

#### must_not_claim
- Do not blame Caden for losing the original slip.
- Do not extend the May 30 deadline.
- Do not say Caden is in trouble for not returning it yet.

#### rewrite_quality_targets
The rewrite should be brief, friendly, and easy for a parent to act on, with the pickup location and deadline clearly preserved.

#### expected_rewrite_challenges
The model may drop the pickup location or soften the message so much that the May 30 deadline no longer reads as firm.

### Case 035 - Orvell trial scope boundary
- id: rewrite-draft-035
- category: sales_followup
- source_type: email
- tone_preset: warm
- risk_tags: sales, scope_limit
- input_word_count_band: 200-320

#### input_draft
Hi Lennox, thanks for the update after your team's first week with the Orvell trial. Really glad the reporting dashboard is getting good use. I wanted to clarify a few things about the trial scope before we talk pricing. The trial you're on covers the Standard tier at $890 per month for up to 25 users — that's through June 14. The advanced workflow builder and the single sign-on integration aren't included in the Standard tier, so anything your team built around those features during the trial would need the Pro tier at $1,490 per month. I can't add those features to the Standard quote without moving the whole account up. If you'd like to see what Pro looks like, I can set up a 30-minute call for the week of June 9 to walk through it. Otherwise, if Standard still fits what you need, I can send the order form for the $890 rate ahead of the June 14 trial end. Let me know which direction makes most sense for your team.

#### what_actually_happened
Lennox's team is on an Orvell Standard tier trial covering up to 25 users at $890 per month through June 14. The advanced workflow builder and SSO integration are not included in the Standard tier — those require the Pro tier at $1,490 per month. The agent cannot add those features to the Standard quote without upgrading the account. A Pro walkthrough call can be arranged for the week of June 9. The Standard order form can be sent at the $890 rate before the June 14 trial end.

#### must_keep
- The contact is Lennox.
- The product is Orvell.
- The trial covers the Standard tier.
- The Standard tier price is $890 per month.
- The trial covers up to 25 users.
- The trial ends on June 14.
- The advanced workflow builder is not included in Standard.
- The SSO integration is not included in Standard.
- Pro tier is priced at $1,490 per month.
- The agent cannot add Pro features to the Standard quote without upgrading the account.
- A Pro walkthrough call can be arranged for the week of June 9.
- The Standard order form can be sent at the $890 rate before June 14.

#### must_not_claim
- Do not include the advanced workflow builder or SSO in the Standard tier quote.
- Do not offer the Pro tier at the Standard tier price.
- Do not promise the trial will be extended past June 14.
- Do not say Pro features were available during the trial.

#### rewrite_quality_targets
The rewrite should feel genuinely helpful and relationship-aware while keeping the scope boundary clear and both next-step paths (Standard order or Pro call) visible.

#### expected_rewrite_challenges
The model may blur the scope boundary by implying the trial included Pro features, or may offer a blended price that doesn't match either tier.

### Case 036 - referral appointment portal issue
- id: rewrite-draft-036
- category: medical_admin
- source_type: email
- tone_preset: warm
- risk_tags: medical_admin, urgent_boundary
- input_word_count_band: 200-320

#### input_draft
Hi Celeste, I'm reaching out about the referral appointment for your upcoming specialist visit. I can see from our system that the referral was sent to Lakewood Cardiology on May 19, but the patient portal is showing a "pending" status rather than a confirmed appointment. I checked with our referral coordinator this morning and she said the cardiology office typically responds within three to five business days, so you should hear something by May 26 at the latest if everything is on track. The portal status will update automatically once they confirm. If you haven't received any contact from Lakewood Cardiology by May 27, please call our office at 555-0148 between 9 a.m. and 4 p.m. and ask for the referral desk. I'm not able to share clinical details about the referral through this message channel, and I'm not a clinician, so I can't advise you on your symptoms or medical decisions. If you experience chest pain, shortness of breath, or any symptoms that feel urgent, please call 911 or go to your nearest emergency room rather than waiting for the referral to be confirmed.

#### what_actually_happened
Celeste has a pending specialist referral to Lakewood Cardiology sent on May 19. The patient portal shows "pending" status. The referral coordinator says cardiology typically responds within three to five business days, so a response is expected by May 26. The portal will update automatically on confirmation. If no contact is received by May 27, Celeste should call 555-0148 between 9 a.m. and 4 p.m. and ask for the referral desk. The agent is not a clinician and cannot advise on symptoms. Urgent symptoms require emergency services, not waiting.

#### must_keep
- The patient is Celeste.
- The referral was sent to Lakewood Cardiology on May 19.
- The portal status is currently "pending."
- The referral coordinator expects a response within three to five business days.
- A response is expected by May 26 if everything is on track.
- The portal will update automatically once cardiology confirms.
- If no contact is received by May 27, Celeste should call 555-0148 between 9 a.m. and 4 p.m. and ask for the referral desk.
- The agent is not a clinician and cannot advise on symptoms or medical decisions.
- Clinical details cannot be shared through this message channel.
- Urgent symptoms require calling 911 or going to the emergency room.

#### must_not_claim
- Do not interpret or comment on Celeste's symptoms or medical situation.
- Do not promise the referral appointment will be confirmed by May 26.
- Do not say the portal status has already updated to confirmed.
- Do not tell Celeste to wait if she has urgent symptoms.

#### rewrite_quality_targets
The rewrite should feel calm and clear, leading with the current status and next steps while keeping the clinical boundary and urgent-symptom escalation path unambiguous.

#### expected_rewrite_challenges
The model may try to reassure Celeste by implying the appointment is confirmed or may soften the urgent-symptom instruction so it no longer reads as a real escalation path.

### Case 037 - unauthorized pet notice
- id: rewrite-draft-037
- category: property_logistics
- source_type: message
- tone_preset: warm
- risk_tags: property, notice
- input_word_count_band: 100-200

#### input_draft
Hi Desmond, I'm following up about the pet policy at 14 Birchwood Lane, Unit 3. During the routine inspection on May 21 the property manager noted a dog in the unit. Your lease agreement does not include a pet addendum, so keeping a dog in the unit is currently not permitted. To remain in compliance, you have until June 4 to either remove the dog from the unit or submit a completed pet addendum request and a $350 pet deposit to the management office. Photos taken during the May 21 inspection are on file. Please contact the office by June 4 to let us know how you'd like to proceed, and we can walk you through the addendum form if that's the route you want to take.

#### what_actually_happened
During a routine inspection on May 21, the property manager found a dog in Unit 3 at 14 Birchwood Lane. Desmond's lease does not include a pet addendum. He has until June 4 to either remove the dog or submit a completed pet addendum request and a $350 pet deposit. Photos from the May 21 inspection are on file. He should contact the office by June 4 to confirm his decision.

#### must_keep
- The tenant is Desmond.
- The property address is 14 Birchwood Lane, Unit 3.
- A dog was found during the routine inspection on May 21.
- The current lease does not include a pet addendum.
- The compliance deadline is June 4.
- To keep the dog, Desmond must submit a completed pet addendum request and a $350 pet deposit.
- Photos from the May 21 inspection are on file.
- Desmond should contact the office by June 4 to confirm his plan.

#### must_not_claim
- Do not say the pet addendum is already approved.
- Do not waive or reduce the $350 pet deposit.
- Do not extend the June 4 compliance deadline.
- Do not blame Desmond for intentionally violating the lease.

#### rewrite_quality_targets
The rewrite should be respectful and professional, making the two options and the June 4 deadline clear without sounding punitive or accusatory.

#### expected_rewrite_challenges
The model may soften the notice so much that the deadline or the $350 deposit requirement becomes ambiguous, or may imply the pet addendum will automatically be approved.

### Case 038 - summer enrichment waitlist
- id: rewrite-draft-038
- category: school_admin
- source_type: email
- tone_preset: warm
- risk_tags: school, capacity
- input_word_count_band: 100-200

#### input_draft
Hi Paloma, thanks for registering Wren for the Summer Science Enrichment program. The registration fee for the program is $65 per student, and payment is due by June 11. I need to let you know that all 24 available spots have been filled and Wren is currently on the waitlist. If a spot opens before June 11, I will contact you right away. If we don't hear from you by June 11 and a spot opens after that date, your registration will be released and the next family on the waitlist will be contacted instead. If Wren does not get a spot, no payment will be collected. Please keep the June 11 date in mind and reach out if you have questions.

#### what_actually_happened
Paloma registered Wren for the Summer Science Enrichment program. The program fee is $65 per student, due by June 11. All 24 spots are filled and Wren is on the waitlist. If a spot opens before June 11, the school will contact Paloma. If a spot opens after June 11 without prior contact, the registration is released to the next waitlist family. No payment is collected if Wren does not get a spot.

#### must_keep
- The parent is Paloma.
- The student is Wren.
- The program is Summer Science Enrichment.
- The registration fee is $65 per student.
- Payment is due by June 11.
- All 24 spots are filled.
- Wren is currently on the waitlist.
- If a spot opens before June 11, the school will contact Paloma right away.
- If no spot is available by June 11, the registration is released to the next waitlist family.
- No payment is collected if Wren does not get a spot.

#### must_not_claim
- Do not guarantee Wren a spot in the program.
- Do not say the $65 fee has already been charged.
- Do not extend the June 11 payment deadline.
- Do not say the waitlist is short or imply placement is likely.

#### rewrite_quality_targets
The rewrite should be warm and clear, making the waitlist status and the June 11 decision point easy to understand without raising false hopes.

#### expected_rewrite_challenges
The model may over-reassure Paloma by implying placement is likely, or may blur the June 11 release rule so it sounds optional.

### Case 039 - downgrade confirmation hold
- id: rewrite-draft-039
- category: customer_success
- source_type: email
- tone_preset: warm
- risk_tags: renewal, confirmation_required
- input_word_count_band: 160-280

#### input_draft
Hi Valentina, thanks for reaching out about your Horizon account. I got your note about wanting to move from the Business plan to the Essentials plan at renewal. I want to make sure I have this right before I make any changes, because plan changes at renewal affect your seat count and pricing going forward. Right now you're on Business at $420 per month for 14 seats, and the renewal date is July 1. The Essentials plan would be $180 per month for up to 8 seats. Moving to Essentials at renewal means the 14-seat Business workspace configuration would no longer apply after July 1 — I want to be transparent about that before we proceed. I can queue the downgrade for July 1 renewal once you send a written confirmation by June 24. I won't make any changes until I hear back from you in writing.

#### what_actually_happened
Valentina wants to downgrade her Horizon account from the Business plan to the Essentials plan at renewal on July 1. The current plan is Business at $420 per month for 14 seats. The Essentials plan is $180 per month for up to 8 seats. The downgrade affects seat count and workspace configuration. The agent will queue the downgrade only after receiving written confirmation from Valentina by June 24. No changes will be made without written confirmation.

#### must_keep
- The account holder is Valentina.
- The account is Horizon.
- The current plan is Business at $420 per month.
- The current plan covers 14 seats.
- The renewal date is July 1.
- The requested plan is Essentials at $180 per month.
- The Essentials plan covers up to 8 seats.
- The downgrade affects the 14-seat Business workspace configuration.
- Written confirmation is required by June 24.
- No changes will be made without written confirmation.

#### must_not_claim
- Do not apply the downgrade without written confirmation from Valentina.
- Do not say the Essentials plan matches the Business plan's seat count.
- Do not promise a refund for unused Business plan days before July 1.
- Do not extend the June 24 confirmation deadline.

#### rewrite_quality_targets
The rewrite should be warm and transparent, making the seat-count impact visible and the confirmation requirement clear without making Valentina feel pressured either way.

#### expected_rewrite_challenges
The model may downplay the seat-count reduction or complete the downgrade without requiring the written confirmation, or may imply the change is already queued.

### Case 040 - volunteer shift arrival note
- id: rewrite-draft-040
- category: nonprofit_community
- source_type: note
- tone_preset: warm
- risk_tags: nonprofit, volunteer
- input_word_count_band: 40-100

#### input_draft
Hi Greta, you're signed up for the Saturday food-drive shift from 10 a.m. to 1 p.m. Please arrive by 9:50 a.m. so we can go through the briefing before your shift starts. Your role is intake sorting. Contact Hiroshi at the welcome table if anything comes up. We can't guarantee the shift won't run a few minutes over, so plan for some flex time at the end if you can.

#### what_actually_happened
Greta is signed up for a Saturday food-drive volunteer shift from 10 a.m. to 1 p.m. She should arrive by 9:50 a.m. for a briefing. Her assigned role is intake sorting. The on-site contact is Hiroshi at the welcome table. The shift may run slightly over time.

#### must_keep
- The volunteer is Greta.
- The shift is Saturday from 10 a.m. to 1 p.m.
- Greta should arrive by 9:50 a.m. for the briefing.
- The assigned role is intake sorting.
- The on-site contact is Hiroshi at the welcome table.
- The shift may run a few minutes over the scheduled end time.

#### must_not_claim
- Do not promise the shift will end exactly at 1 p.m.
- Do not say Greta's shift has been changed or reassigned.
- Do not promise a specific number of volunteers or capacity.

#### rewrite_quality_targets
The rewrite should be brief, warm, and practical, keeping the arrival time, role, and contact name clear while the caveat about the shift possibly running over stays honest.

#### expected_rewrite_challenges
The model may drop the 9:50 a.m. early-arrival detail or remove the shift-overrun caveat to make the message sound tidier.

### Case 041 - two-order partial refund and replacement
- id: rewrite-draft-041
- category: customer_support
- source_type: email
- tone_preset: warm
- risk_tags: support, long_draft, fact_preservation
- input_word_count_band: 320-400

#### input_draft
Hi Celestine, I know this has been a frustrating few weeks, and I really appreciate you sending all those photos and forwarding both order confirmations. I have been going through everything carefully so I can give you a clear answer instead of a bunch of back-and-forth.

So, order R-8801 is the one with the cracked mixing bowl set. The order was delivered on April 18 and you reported the damage on April 22. Because you reported it within the 5-day damage window, I can process a partial refund for the mixing bowl set only. That partial refund is $29.40 and it will go back to the card on file. It is not a refund for the full order because the rest of the items in R-8801 — the silicone tray set and the dish rack — arrived undamaged per your own photos, so those are not covered.

Order R-8854 is a little different. The blender jar arrived with a cracked lid, and you reported that one on May 3. The delivery scan for R-8854 shows May 1. For this order my options are more limited because the damage was reported outside the 5-day window. What I can do is send one replacement blender lid at no cost as a goodwill gesture, but I cannot process a cash refund for the lid from R-8854. I want to be upfront about that rather than leaving you guessing.

To move both of these forward, I need one thing from you: please send me your current shipping address by May 14 so I can queue the replacement lid. I also need you to confirm in this thread that you are okay with the $29.40 partial refund path for R-8801 instead of a full order refund. Once I have both confirmations, I will queue the refund and the replacement shipment at the same time. If the $29.40 does not show up within five business days after I process it, please reach back out to me directly.

#### what_actually_happened
Celestine reported damage on two separate orders. Order R-8801 (delivered April 18, reported April 22) had a cracked mixing bowl set within the 5-day window, qualifying for a $29.40 partial refund for the mixing bowl set only; the silicone tray set and dish rack in R-8801 were undamaged. Order R-8854 (delivered May 1, reported May 3) had a cracked blender lid handled as a goodwill replacement only — one replacement blender lid, no cash refund. Both actions require Celestine to confirm her current shipping address by May 14 and confirm the partial refund path.

#### must_keep
- The customer is Celestine.
- Order R-8801 was delivered on April 18.
- Damage on R-8801 was reported on April 22.
- The cracked item in R-8801 is the mixing bowl set.
- The partial refund for R-8801 is $29.40.
- No refund covers the full R-8801 order.
- Damage on R-8854 was reported on May 3.
- The cracked item in R-8854 is the blender lid.
- Support will send one replacement blender lid at no cost for R-8854.
- No cash refund is available for R-8854.
- Celestine must send her current shipping address by May 14.
- Celestine must confirm the $29.40 partial refund path in this thread.

#### must_not_claim
- Do not promise a full refund for either order.
- Do not replace the silicone tray set or dish rack from R-8801.
- Do not skip the address confirmation step before queuing the replacement.
- Do not promise the $29.40 will post in fewer than five business days.

#### rewrite_quality_targets
A good rewrite organizes the two-order situation clearly — one section per order — keeps the refund amounts and conditions exact, and ends with a single clear ask covering both confirmations.

#### expected_rewrite_challenges
The model may merge the two orders or overpromise by offering refunds for both, or it may drop the address deadline and confirmation requirement.

### Case 042 - three-invoice credit review
- id: rewrite-draft-042
- category: billing_support
- source_type: email
- tone_preset: warm
- risk_tags: billing, multi_amount, refund_boundary
- input_word_count_band: 320-400

#### input_draft
Hi Bertrand, I've gone through the billing dispute you raised and I want to give you a full picture, because there are three invoices involved and each one has a slightly different situation.

Invoice INV-2210 is for $148.00, covering March 1 through March 31. Your team was on the Standard plan for that whole month. You downgraded on April 1, so this invoice is fully within the Standard billing cycle. I cannot issue a refund for INV-2210 because the service was used for the entire billing period with no changes mid-month.

Invoice INV-2291 is for $148.00, covering April 1 through April 30. Your downgrade was processed on April 1, but the system charged Standard instead of the Basic rate of $89.00 because the plan switch did not propagate before the invoice ran. This one is a legitimate billing error. I can issue a $59.00 credit — that is the $148.00 Standard charge minus the $89.00 Basic rate you should have been charged. I can apply that $59.00 credit to your next invoice, INV-2361, if you confirm by June 5.

Invoice INV-2361 is the current open invoice of $89.00 for May 1 through May 31. This one is correct. After the $59.00 credit is applied, the balance on INV-2361 would be $30.00.

So to recap: no refund for INV-2210, a $59.00 credit from INV-2291 applied to INV-2361, and INV-2361 balance after credit becomes $30.00. I need your confirmation by June 5 to process the credit. If I do not hear back by then, the credit window for INV-2291 closes and the full $89.00 on INV-2361 remains due.

#### what_actually_happened
Bertrand disputed three invoices. INV-2210 ($148.00, March) is valid — full month at Standard with no mid-month changes. INV-2291 ($148.00, April) contains a billing error: the plan switch to Basic ($89.00) did not propagate before the invoice ran, so Bertrand was overcharged by $59.00. A $59.00 credit can be applied to INV-2361 if Bertrand confirms by June 5. INV-2361 ($89.00, May) is correct; after the $59.00 credit the balance would be $30.00.

#### must_keep
- The customer is Bertrand.
- Invoice INV-2210 covers March 1 through March 31 at $148.00.
- INV-2210 cannot be refunded because the full month of Standard service was used.
- Invoice INV-2291 covers April 1 through April 30 at $148.00.
- The downgrade was processed on April 1.
- The Basic rate is $89.00 per month.
- INV-2291 contains a $59.00 billing error.
- A $59.00 credit can be applied to INV-2361.
- Bertrand must confirm by June 5 to receive the credit.
- Invoice INV-2361 covers May 1 through May 31 at $89.00.
- After the $59.00 credit, the INV-2361 balance would be $30.00.
- If Bertrand does not confirm by June 5, the credit window closes.

#### must_not_claim
- Do not refund INV-2210.
- Do not promise a cash refund instead of a credit.
- Do not apply the credit without Bertrand's confirmation.
- Do not change the $59.00 credit amount or the $30.00 post-credit balance.

#### rewrite_quality_targets
A good rewrite keeps the three-invoice structure clear, states each amount and the credit math exactly, and closes with a single unambiguous call to action tied to the June 5 deadline.

#### expected_rewrite_challenges
The model may collapse the three invoices into a vague summary, get the math wrong, or soften the June 5 credit-window closure into a suggestion.

### Case 043 - Prism launch blocker note
- id: rewrite-draft-043
- category: workplace_update
- source_type: note
- tone_preset: warm
- risk_tags: workplace, long_draft, structure_readability
- input_word_count_band: 200-320

#### input_draft
Team — update on the Prism launch. I know we were hoping to ship the full Prism release this Thursday, June 12, but that is not going to happen because we have two blockers I want to be honest about rather than pretend are almost solved.

Blocker one: the payment confirmation webhook is still failing on retry. Soren has been on it since Monday and the fix is in review now, but it has not passed the retry scenario tests yet. Soren's best estimate is that the fix is merged and tested by end of day Wednesday, June 11. If it slips past that, the payment flow cannot go live.

Blocker two: the accessibility audit flagged five keyboard-navigation issues in the checkout flow. Bea owns the fixes. Four of the five are done, but the focus-trap issue on the modal is still open. Bea expects to close it by Wednesday as well, but it is not closed yet and we should not ship knowing it is open.

So the plan right now: if both fixes land by end of Wednesday, June 11, we move the launch to Friday, June 13. If either fix slips, I will send a new launch date by Thursday at noon. I need the team to not announce Friday to anyone externally yet — please hold that until I send the green light. Questions go to me or to Soren and Bea directly.

#### what_actually_happened
The Prism launch originally planned for Thursday, June 12 is blocked by two issues. Soren is fixing a payment confirmation webhook retry failure, expected by end of day June 11. Bea is fixing five keyboard-navigation issues in the checkout flow; four are done and one focus-trap issue is still open, also expected by end of day June 11. If both fix by June 11, launch moves to Friday, June 13. If either slips, a new date will be announced by Thursday at noon.

#### must_keep
- The project is Prism.
- The original launch date was Thursday, June 12.
- Blocker one is a payment confirmation webhook failing on retry.
- Soren owns the webhook fix.
- Soren's estimate is that the fix is merged and tested by end of day Wednesday, June 11.
- Blocker two is five keyboard-navigation issues in the checkout flow.
- Bea owns the keyboard-navigation fixes.
- Four of the five accessibility issues are done.
- The remaining open issue is the focus-trap on the modal.
- Bea expects to close the focus-trap issue by Wednesday, June 11.
- If both fixes land by end of June 11, launch moves to Friday, June 13.
- If either fix slips, a new date will be sent by Thursday at noon.

#### must_not_claim
- Do not say the webhook fix is already passing tests.
- Do not say all five accessibility issues are resolved.
- Do not announce June 13 as a confirmed launch date.
- Do not blame Soren or Bea for the delay.

#### rewrite_quality_targets
A good rewrite keeps both blockers clearly separated, preserves each owner and date, and ends with the conditional plan and the external-announcement hold without sounding alarming or accusatory.

#### expected_rewrite_challenges
The model may soften the note by collapsing both blockers into one, or may present June 13 as a confirmed date rather than a conditional one.

### Case 044 - classroom behavior observation
- id: rewrite-draft-044
- category: teacher_parent
- source_type: email
- tone_preset: warm
- risk_tags: teacher, sensitive, no_diagnosis
- input_word_count_band: 320-400

#### input_draft
Hi Ms. Okonkwo, I wanted to reach out about Felix because what I am seeing in class has come up enough times that it feels important to share before it becomes a bigger pattern, and I also want to make sure I am giving you the chance to share anything you think I should know.

On four separate days — May 6, May 9, May 14, and May 19 — Felix left his assigned group work to sit by himself near the back window. Each time I gave him about five minutes and then checked in quietly. He was polite but said he needed a break. On May 14 he also put his head down for about 15 minutes during independent reading. I am not able to diagnose anything or tell you whether this is a medical or emotional issue — that is completely outside my role. What I can say is that as his fifth-grade teacher I have noticed a shift in the last few weeks compared to earlier in the year.

I have talked with our school counselor, Ms. Patel, and she is available to meet with Felix if you think that would be helpful. Our school policy is that any formal counselor meeting with a student requires a parent or guardian to sign the consent form first — I can send that form home with Felix or email it to you at your preference.

I would really like to set up a time for us to talk. I have availability this coming Thursday, May 22 at 3:45 p.m. or Friday, May 23 at 8:15 a.m. If neither of those works, please suggest a time and I will do my best to match it. I am not writing this to worry you — I just think the earlier we talk, the more we can do together to support Felix.

#### what_actually_happened
Felix's teacher observed him leaving group work to sit alone near the back window on May 6, May 9, May 14, and May 19. Each time he said he needed a break. On May 14 he also put his head down for about 15 minutes during independent reading. The teacher is not diagnosing anything and that assessment is outside their role. The school counselor Ms. Patel is available if the parent consents; a consent form is required per school policy. The teacher is offering a meeting on Thursday, May 22 at 3:45 p.m. or Friday, May 23 at 8:15 a.m.

#### must_keep
- The student is Felix.
- The parent or guardian is Ms. Okonkwo.
- The teacher observed Felix leaving group work on May 6, May 9, May 14, and May 19.
- Each time Felix said he needed a break.
- On May 14 Felix put his head down for about 15 minutes during independent reading.
- The teacher is Felix's fifth-grade teacher.
- The teacher cannot diagnose or assess whether the issue is medical or emotional.
- The school counselor is Ms. Patel.
- A parent or guardian consent form is required before a formal counselor meeting.
- The consent form can be sent home with Felix or emailed to the parent.
- The teacher has meeting availability on Thursday, May 22 at 3:45 p.m.
- The teacher has meeting availability on Friday, May 23 at 8:15 a.m.

#### must_not_claim
- Do not diagnose Felix or suggest a specific medical or emotional condition.
- Do not say a counselor meeting has already been scheduled.
- Do not say Ms. Patel has already spoken with Felix.
- Do not promise the observations are the only factor affecting Felix's performance.

#### rewrite_quality_targets
A good rewrite is warm and factual, describes the specific observations clearly, preserves the policy and consent boundaries, and invites the parent to engage without alarming them or implying a diagnosis.

#### expected_rewrite_challenges
The model may try to be reassuring by softening the observations into vague language, or may inadvertently suggest a diagnosis by framing the behavior as a symptom of something specific.

### Case 045 - three-tier quote follow-up
- id: rewrite-draft-045
- category: sales_followup
- source_type: email
- tone_preset: warm
- risk_tags: sales, multi_option, no_free_work
- input_word_count_band: 320-400

#### input_draft
Hi Ingrid, thank you for the time on Friday and for the detailed notes your team sent over the weekend. I want to make sure quote Q-5544 is easy to compare against the two add-on options I mentioned, so I am laying everything out in one email.

The base quote Q-5544 is 20 seats at $55 per seat per month, which includes the core platform, onboarding, and standard email support. This quote expires on June 20. The total at this tier is $1,100 per month.

Add-on A is the API access package. If your team adds API access, the seat price stays the same at $55 per seat per month but there is an additional flat fee of $300 per month for API access. That brings the monthly total to $1,400.

Add-on B is the dedicated onboarding program, which is a one-time fee of $1,800 and covers four structured onboarding sessions. The dedicated onboarding is not included in the base quote or Add-on A. If you add both API access and dedicated onboarding, the monthly recurring cost is $1,400 plus the one-time $1,800.

One thing I want to be clear about: the custom dashboard integration your team mentioned on Friday is not in any of these three options. That would require a separate scoping conversation and a separate quote. I cannot include it in Q-5544 or the add-ons without a new approval.

All three options expire on June 20. If you would like to go with one of the options as described, please reply by June 20 and I can send the order form the same day. If you need an extension or want to add the custom dashboard scope, let me know and I can start that process separately.

#### what_actually_happened
Ingrid received quote Q-5544 (20 seats at $55/seat/month, $1,100/month total, including core platform, onboarding, standard email support, expires June 20) and two add-on options. Add-on A adds API access for a flat $300/month extra (total $1,400/month). Add-on B is a one-time $1,800 dedicated onboarding program (four sessions). Custom dashboard integration is out of scope for all three options and requires a separate scoping conversation and quote.

#### must_keep
- The contact is Ingrid.
- The quote identifier is Q-5544.
- The base quote covers 20 seats.
- The seat price is $55 per seat per month.
- The base monthly total is $1,100 per month.
- The base quote includes core platform, onboarding, and standard email support.
- All three options expire on June 20.
- Add-on A adds API access for a flat $300 per month (total $1,400/month).
- Add-on B is a one-time fee of $1,800 for four dedicated onboarding sessions.
- Custom dashboard integration is not in any of the three options.
- Custom dashboard integration requires a separate scoping conversation and quote.
- Ingrid must reply by June 20 for the order form.

#### must_not_claim
- Do not include custom dashboard integration in any of the three options.
- Do not change the seat price, the API flat fee, or the dedicated onboarding fee.
- Do not extend the June 20 expiry date.
- Do not offer additional work or scope that is not listed in the three options.

#### rewrite_quality_targets
A good rewrite presents the three tiers in a scannable format, preserves every dollar amount and date, and states the custom-dashboard exclusion clearly without sounding unhelpful.

#### expected_rewrite_challenges
The model may blur the custom dashboard boundary by implying it could be added easily, or may alter one of the fee amounts to make the math sound cleaner.

### Case 046 - records release request status
- id: rewrite-draft-046
- category: medical_admin
- source_type: message
- tone_preset: warm
- risk_tags: medical_admin, privacy, no_advice
- input_word_count_band: 200-320

#### input_draft
Hi Rowena, I am following up on the medical records release request you submitted on May 20. I am the records coordinator here, not a clinician, so I can speak to the process and the status but not to the content of the records or what any of them mean.

The request came in as REL-4490. Right now REL-4490 is in the provider verification step, which is standard before we send anything out. Dr. Vandermeer's team needs to sign off before we can release the file. I sent the verification request to Dr. Vandermeer's office this morning at 9:40 a.m. The normal turnaround for provider verification is three business days from when the request is sent, so the earliest the records could be ready to release is May 23.

Once Dr. Vandermeer's team signs off, we will release the records to the fax number on file, which is (04) 5512-0088. If that fax number has changed or if you need the records released by a different method, please reply to this message before May 23 so I can update the release method. After May 23 I cannot guarantee changes will reach us before the file goes out.

If you have an urgent clinical situation that cannot wait for the normal release window, please call the clinic line directly and ask for the nurse supervisor — that team can escalate if there is a time-critical need. I cannot make that escalation from a records coordinator role.

#### what_actually_happened
Rowena submitted a records release request (REL-4490) on May 20. The request is currently in the provider verification step with Dr. Vandermeer's team. The verification request was sent to Dr. Vandermeer's office at 9:40 a.m. today. Normal turnaround is three business days, so the earliest release is May 23. Records will go to fax (04) 5512-0088 unless Rowena requests a change before May 23. The sender is a records coordinator and cannot interpret record content. Urgent clinical situations should go through the clinic line.

#### must_keep
- The recipient is Rowena.
- The records release request identifier is REL-4490.
- The request was submitted on May 20.
- The request is currently in the provider verification step.
- Dr. Vandermeer's team must sign off before release.
- The verification request was sent to Dr. Vandermeer's office at 9:40 a.m. today.
- Normal turnaround for provider verification is three business days.
- The earliest the records could be ready is May 23.
- The records will be released to fax number (04) 5512-0088.
- Rowena must reply before May 23 to change the release method.
- The sender is a records coordinator and cannot interpret the record content.
- Urgent situations should go through the clinic line to reach the nurse supervisor.

#### must_not_claim
- Do not interpret or summarize the content of the medical records.
- Do not promise the records will be released before May 23.
- Do not say the fax number has already been changed.
- Do not say the records coordinator can handle urgent clinical escalations.

#### rewrite_quality_targets
A good rewrite is calm and organized, keeps the records coordinator's role boundary explicit, preserves the fax number and deadline exactly, and directs urgent needs to the correct escalation path.

#### expected_rewrite_challenges
The model may try to be helpful by offering to interpret records content, or may commit to a release date earlier than May 23 to sound more responsive.

### Case 047 - ceiling fan repair access and quoted tenant complaint
- id: rewrite-draft-047
- category: property_logistics
- source_type: email
- tone_preset: warm
- risk_tags: property, quote_risk, compensation_boundary
- input_word_count_band: 320-400

#### input_draft
Hi Silvio, I am following up on the ceiling fan repair at Unit 7C. I do want to address the note you included in your last message — you wrote "this has been going on for three months and nobody seems to care." I understand that is how it has felt, and I am not dismissing the frustration. What I can tell you is that the work order for the fan was logged on March 10, and the original vendor we assigned canceled twice. We reassigned the work order to a new vendor, Apex Electrical, on April 28.

Apex Electrical has confirmed they can attend on Wednesday, June 4 between 2 p.m. and 5 p.m. They will replace the ceiling fan motor and check the wiring connection at the ceiling bracket. The repair estimate on file is $215 for parts and labor. The $215 is the vendor's estimate and actual cost may vary, but we will not pass any cost above that estimate to you as a tenant — the repair cost is the property's responsibility.

To proceed I need a few things from you. First, please confirm by Monday, June 2 at noon whether someone will be home during the June 4 window, or whether you want to authorize access via the key on file. Second, please send one photo of the fan as it is right now, by Monday June 2 at noon, so the vendor has current documentation before they arrive.

I cannot approve a rent reduction related to the fan repair from this thread. If you want to raise a formal claim, that goes through the tenants' claims form, which is available from the property management office.

#### what_actually_happened
Silvio's Unit 7C ceiling fan work order was logged on March 10. The original vendor canceled twice. A new vendor, Apex Electrical, was assigned on April 28. Apex Electrical will attend on Wednesday, June 4 between 2 p.m. and 5 p.m. to replace the fan motor and check the wiring connection at the ceiling bracket. The repair estimate is $215 for parts and labor; costs above that estimate will not be passed to the tenant. Silvio must confirm access and send a current photo by Monday, June 2 at noon. Rent reduction requests must go through the tenants' claims form.

#### must_keep
- The tenant is Silvio.
- The property unit is Unit 7C.
- The issue is the ceiling fan repair.
- The work order was logged on March 10.
- Apex Electrical was assigned on April 28.
- Apex Electrical will attend on Wednesday, June 4 between 2 p.m. and 5 p.m.
- The repair covers replacing the ceiling fan motor and checking the wiring connection at the ceiling bracket.
- The repair estimate is $215 for parts and labor.
- Costs above the $215 estimate will not be passed to Silvio.
- Silvio must confirm access or authorize key-on-file access by Monday, June 2 at noon.
- Silvio must send one current photo of the fan by Monday, June 2 at noon.
- Rent reduction cannot be approved from this thread.

#### must_not_claim
- Do not promise the repair will be fully completed on June 4.
- Do not approve a rent reduction or credit.
- Do not say the original vendor's cancellations were Silvio's fault.
- Do not guarantee the final repair cost will be exactly $215.

#### rewrite_quality_targets
A good rewrite acknowledges the tenant's frustration without conceding liability, summarizes the repair timeline accurately, keeps the access and photo deadlines precise, and states the rent-reduction boundary without sounding dismissive.

#### expected_rewrite_challenges
The model may try to over-validate the tenant's complaint in a way that implies a compensation commitment, or may drop the June 2 photo deadline while emphasizing the June 4 access window.

### Case 048 - senior analyst role rejection
- id: rewrite-draft-048
- category: hr_recruiting
- source_type: email
- tone_preset: warm
- risk_tags: recruiting, rejection, no_false_reason
- input_word_count_band: 320-400

#### input_draft
Hi Wendell, thank you for the time you gave us across all three stages of the Senior Analyst hiring process. I know you prepared thoroughly for the panel interview on May 13, and I want to give you a respectful and honest update.

The hiring team has completed their review, and I am sorry to tell you that we will not be moving forward with an offer for the Senior Analyst role at this time. This was a genuinely difficult decision. You were not the only strong candidate and the panel had a good discussion. I am not in a position to share the specific rationale behind the final decision — that is something the hiring team keeps confidential — but I want to be clear that this outcome does not reflect poorly on your overall qualifications.

One thing I can share is that the panel noted your experience with financial modeling and your presentation in the May 13 session. I am not able to go further than that in terms of feedback, and I do not want to speculate about what a different outcome would have looked like.

If you are interested in future roles, I would encourage you to check our careers page regularly. Our recruiter for the Analytics team, Priya Fernandes, will be on the careers page as the listed contact for future Analytics openings. I cannot make any promise about future roles or timelines, but we do keep records of candidates who have been through the process.

If you have a question about this specific process, you can reach me at hiring@example.org. Thank you again for the time and effort you put into this — it genuinely was appreciated.

#### what_actually_happened
Wendell went through three stages of the Senior Analyst hiring process and had his panel interview on May 13. The hiring team has decided not to make an offer. The decision is confidential and specific rationale cannot be shared. The panel noted his financial modeling experience and his May 13 presentation. Priya Fernandes is the Analytics team recruiter and is listed on the careers page. The hiring contact is hiring@example.org.

#### must_keep
- The candidate is Wendell.
- The role is Senior Analyst.
- The panel interview was on May 13.
- The hiring team will not be moving forward with an offer.
- The specific rationale for the decision is confidential.
- The panel noted Wendell's experience with financial modeling.
- The panel noted Wendell's presentation in the May 13 session.
- No further detailed feedback beyond those two points can be shared.
- The recruiter for future Analytics roles is Priya Fernandes.
- Priya Fernandes is listed on the careers page as the contact for Analytics openings.
- No promises about future roles or timelines can be made.
- The hiring contact email is hiring@example.org.

#### must_not_claim
- Do not reveal the specific reason the hiring team chose another candidate.
- Do not imply Wendell will definitely be considered for future roles.
- Do not offer feedback beyond the two panel observations mentioned.
- Do not say Wendell performed poorly or give a negative characterization.

#### rewrite_quality_targets
A good rewrite is warm and respectful, delivers the rejection clearly without false hope, shares only the two factual panel observations, and closes with the accurate next-steps path without overpromising.

#### expected_rewrite_challenges
The model may try to soften the rejection by implying a future offer is likely, or may invent additional feedback details to seem more helpful.

### Case 049 - summer enrichment program announcement
- id: rewrite-draft-049
- category: school_admin
- source_type: announcement
- tone_preset: warm
- risk_tags: school, announcement, policy_boundary
- input_word_count_band: 200-320

#### input_draft
Hello Ridgemont families, we are excited to share details about the Summer Enrichment Program for the 2026 summer session. Please read everything below because there are forms, fees, and a hard deadline involved.

The program runs July 7 through July 25 on the Ridgemont East campus, Room 104. It is open to students currently enrolled in grades 3 through 6 at Ridgemont. Students who will be entering grade 3 in the fall are not eligible for this session — enrollment is for current grade 3 through grade 6 students only.

Enrollment is capped at 24 students this session. If we receive more than 24 completed applications, students will be placed on a waitlist in the order applications are received. Being on the interest list from last year does not guarantee a spot — all families must submit a new application this year.

The program fee is $185 per student, due with the application. Applications and payment must reach the main office by Friday, June 13 at 4 p.m. The application form is Form SEP-2026, available at the main office window or on the school website. We cannot accept partial payment, and we cannot hold a spot without payment.

If a student is placed on the waitlist, the $185 payment will be held until a spot is confirmed or until the deadline has passed, at which point we will return the payment if no spot opens.

For questions, please email enrichment@example.com or visit the main office Tuesday through Thursday between 8 a.m. and 3 p.m.

#### what_actually_happened
Ridgemont is offering a Summer Enrichment Program from July 7 to July 25 on the Ridgemont East campus in Room 104. Eligibility is limited to students currently in grades 3 through 6. Enrollment is capped at 24 students. Students beyond 24 go on a waitlist in application order; being on last year's interest list does not guarantee a spot. The fee is $185 per student, due with the application using Form SEP-2026. The deadline is Friday, June 13 at 4 p.m. at the main office. Partial payment is not accepted; no spot is held without payment. Contact is enrichment@example.com or the main office Tuesday through Thursday 8 a.m. to 3 p.m.

#### must_keep
- The program is the Summer Enrichment Program for the 2026 summer session.
- The program runs July 7 through July 25.
- The location is Ridgemont East campus, Room 104.
- Eligibility is for students currently enrolled in grades 3 through 6 at Ridgemont.
- Students entering grade 3 in the fall are not eligible this session.
- Enrollment is capped at 24 students.
- Students beyond 24 go on a waitlist in application-received order.
- Being on last year's interest list does not guarantee a spot.
- The program fee is $185 per student.
- The application form is Form SEP-2026.
- Applications and payment must reach the main office by Friday, June 13 at 4 p.m.
- Contact is enrichment@example.com or the main office Tuesday through Thursday 8 a.m. to 3 p.m.

#### must_not_claim
- Do not say students entering grade 3 in the fall are eligible.
- Do not say last year's interest list carries over to a guaranteed spot.
- Do not say partial payment is accepted or that a spot can be held without full payment.
- Do not extend or soften the June 13 deadline.

#### rewrite_quality_targets
A good rewrite keeps the announcement friendly and easy to scan, preserves the eligibility rule, cap, fee, form name, deadline, and payment policy exactly, and ensures the contact information is accurate.

#### expected_rewrite_challenges
The model may try to be welcoming by softening the eligibility boundary or implying that last year's interest list gives applicants priority.

### Case 050 - renewal options with seat change
- id: rewrite-draft-050
- category: customer_success
- source_type: email
- tone_preset: warm
- risk_tags: renewal, multi_option, no_change_without_confirm
- input_word_count_band: 320-400

#### input_draft
Hi Kasimir, I wanted to reach out ahead of your renewal on July 1 because your account is set up with 40 seats on the Business plan at $62 per seat per month, and you mentioned in March that your team might be smaller by mid-year. I want to make sure you have the full picture before July 1 so nothing changes without your decision.

Your current setup: 40 seats on Business plan, $2,480 per month, renewal date July 1. The account ID is ACC-10034. Right now 34 of the 40 seats are active. The 6 unused seats are still billed because they are part of the contracted commitment.

Option 1 is to renew as-is: 40 seats on Business plan at $62 per seat per month, same $2,480 per month. No action needed if this is your preference — the account will renew automatically on July 1.

Option 2 is to downgrade to 34 seats, which matches your current active count. At $62 per seat the monthly cost would be $2,108. To do this I need your written confirmation before June 20 so I can process the seat change before the renewal runs. If you confirm after June 20, the change will take effect at the next renewal, not July 1.

Option 3 is to upgrade to 50 seats, which your team also mentioned as a possibility. At $62 per seat the monthly cost would be $3,100. Same deadline: I need your written confirmation before June 20.

I will not make any changes to your seat count or plan without your explicit confirmation in writing. Please reply to this email to confirm your choice by June 20 if you want anything other than the auto-renewal.

#### what_actually_happened
Kasimir's account ACC-10034 has 40 seats on the Business plan at $62 per seat per month ($2,480/month) renewing July 1. Currently 34 of 40 seats are active; 6 unused seats are billed as part of the contract. Three options are presented: Option 1 is auto-renew at 40 seats; Option 2 is downgrade to 34 seats at $2,108/month (written confirmation needed by June 20); Option 3 is upgrade to 50 seats at $3,100/month (written confirmation by June 20). No changes will be made without written confirmation.

#### must_keep
- The customer is Kasimir.
- The account identifier is ACC-10034.
- The current plan is Business plan.
- The current seat count is 40 seats.
- The price is $62 per seat per month.
- The current monthly total is $2,480 per month.
- The renewal date is July 1.
- Currently 34 of 40 seats are active.
- Option 2 is a downgrade to 34 seats at $2,108 per month.
- Option 3 is an upgrade to 50 seats at $3,100 per month.
- Written confirmation is required before June 20 for Options 2 or 3.
- No seat count or plan changes will be made without explicit written confirmation.

#### must_not_claim
- Do not change any seat count or plan without Kasimir's written confirmation.
- Do not say the June 20 deadline can be extended.
- Do not alter the per-seat price or the monthly totals for any option.
- Do not say the 6 unused seats are not billed.

#### rewrite_quality_targets
A good rewrite presents the three options clearly with exact numbers, states the confirmation deadline and consequences precisely, and makes the no-change-without-confirmation policy explicit without sounding transactional.

#### expected_rewrite_challenges
The model may try to simplify by collapsing the three options, dropping the June 20 cutoff consequence, or softening the no-change-without-confirmation boundary.

### Case 051 - delayed shipment status
- id: rewrite-draft-051
- category: customer_support
- source_type: email
- tone_preset: warm
- risk_tags: support, warmth
- input_word_count_band: 100-200

#### input_draft
Hi Petra, I looked into your order ORD-29341 this morning. The item you ordered — the Westford wall shelf — shipped on May 19 but the carrier scan shows it got stuck at the Denver sorting facility as of May 21. I know that is not what you were hoping to hear. The carrier's estimated delivery has moved to May 27. I cannot pull it back or reroute it at this stage, but I have flagged the shipment for priority handling on our end. If it hasn't arrived by May 28, reply to this email and I will open a replacement or refund request right away.

#### what_actually_happened
The customer's order ORD-29341 contained a Westford wall shelf. The item shipped on May 19. A carrier scan dated May 21 shows the package is delayed at the Denver sorting facility. The updated carrier estimate is May 27. The support agent cannot reroute the shipment but has flagged it for priority handling. If the item has not arrived by May 28, the customer should reply to request a replacement or refund.

#### must_keep
- The customer is Petra.
- The order identifier is ORD-29341.
- The item is the Westford wall shelf.
- The item shipped on May 19.
- The carrier scan dated May 21 shows the package is at the Denver sorting facility.
- The updated delivery estimate is May 27.
- The agent cannot reroute the shipment at this stage.
- The shipment has been flagged for priority handling.
- If the item has not arrived by May 28, Petra should reply to request a replacement or refund.

#### must_not_claim
- Do not promise delivery by May 27.
- Do not say a replacement has already been sent.
- Do not offer a refund before May 28.
- Do not say the carrier is at fault in a way that implies compensation.

#### rewrite_quality_targets
A good rewrite conveys genuine care for Petra's situation while keeping the shipment facts, current estimate, and the May 28 action path precise and easy to find. Warmth should come from clear communication, not vague reassurance.

#### expected_rewrite_challenges
The model may soften the message by implying the package will definitely arrive on May 27, or may prematurely offer a refund before the May 28 threshold.

### Case 052 - late fee after grace period
- id: rewrite-draft-052
- category: billing_support
- source_type: message
- tone_preset: warm
- risk_tags: billing, fee
- input_word_count_band: 100-200

#### input_draft
Hi Cassian, I checked your account about invoice INV-6603 for $74.00 that was due on May 5. Our records show payment was received on May 13. The plan includes a 5-day grace period, so late fees do not apply until day 6 after the due date, which was May 11. Payment came in on May 13, two days after the grace window closed, so a $9.00 late fee was added automatically. I cannot remove the fee because it was applied correctly under the current billing plan. If you think there was a processing error on your bank's side, please share the payment confirmation and I can look into it further. You can reply here or email billing@example.org.

#### what_actually_happened
Invoice INV-6603 for $74.00 was due on May 5. The plan includes a 5-day grace period, meaning late fees apply from May 11 onward. Payment arrived on May 13, two days after the grace window closed. A $9.00 late fee was added automatically and is policy-compliant. The agent cannot waive it. The customer can share a bank payment confirmation to investigate a possible processing error.

#### must_keep
- The customer is Cassian.
- The invoice identifier is INV-6603.
- The invoice amount is $74.00.
- The invoice due date was May 5.
- The plan includes a 5-day grace period.
- The late fee applies from May 11 onward.
- Payment was received on May 13.
- The late fee amount is $9.00.
- The late fee cannot be removed under the current billing plan.
- The customer can share a payment confirmation to investigate a bank processing error.
- The contact for billing queries is billing@example.org.

#### must_not_claim
- Do not waive or remove the $9.00 late fee.
- Do not say the grace period extends to May 13.
- Do not promise a refund of the late fee pending investigation.

#### rewrite_quality_targets
A good rewrite explains the grace period rule and the resulting late fee clearly and without sounding punitive, while keeping the amounts, dates, and escalation path exact. The tone should be matter-of-fact and kind.

#### expected_rewrite_challenges
The model may blur the grace period boundary, imply the fee could be waived, or omit the May 11 grace cutoff date.

### Case 053 - overdue handoff request
- id: rewrite-draft-053
- category: workplace_update
- source_type: email
- tone_preset: warm
- risk_tags: workplace, firm
- input_word_count_band: 100-200

#### input_draft
Hi Soren, I wanted to flag that the API credential handoff document for the Cormorant integration was due last Friday, May 23. I have not received it yet, and the backend team needs it to complete the staging environment setup, which is currently blocked. Can you please send the document by Wednesday, May 28 at noon? That is the latest we can accept it and still hit the June 4 staging sign-off. If there is something preventing the handoff, let me know today so I can loop in Kenji and look at alternatives. I will be checking my inbox this afternoon.

#### what_actually_happened
The API credential handoff document for the Cormorant integration was due on Friday, May 23. It has not been received. The backend team's staging environment setup is blocked. The new deadline is Wednesday, May 28 at noon. Missing that deadline puts the June 4 staging sign-off at risk. If Soren cannot meet the new deadline, Kenji should be looped in today.

#### must_keep
- The recipient is Soren.
- The deliverable is the API credential handoff document.
- The project is the Cormorant integration.
- The original due date was Friday, May 23.
- The document has not been received.
- The backend team's staging environment setup is blocked.
- The new deadline is Wednesday, May 28 at noon.
- Missing the new deadline risks the June 4 staging sign-off.
- If there is a blocker, Soren should let the sender know today so Kenji can be looped in.

#### must_not_claim
- Do not blame Soren for the delay.
- Do not promise the June 4 date will hold if the document is late.
- Do not say the staging environment is ready.

#### rewrite_quality_targets
A good rewrite makes the request direct and firm without sounding accusatory, keeps the dates exact, and makes the escalation path through Kenji clearly conditional rather than threatening.

#### expected_rewrite_challenges
The model may soften the missed-deadline fact into a gentle reminder that loses the urgency, or may remove the June 4 dependency.

### Case 054 - grade review request
- id: rewrite-draft-054
- category: teacher_parent
- source_type: email
- tone_preset: warm
- risk_tags: teacher, grade
- input_word_count_band: 100-200

#### input_draft
Hi Brigid, thank you for reaching out about Tobias's mark on the May 12 poetry analysis. The assignment was graded as a 61 out of 100 based on the rubric categories of structure, evidence, and written expression. I reviewed the submission again this morning and I believe the mark was applied correctly, but I understand you want a second look. I can schedule a grade conference any day next week between 3:30 and 4:30 p.m. I need you to email me by Friday, May 30 to confirm a day. I cannot change the grade before the review is complete, and even after the review I can only adjust if I find a rubric error.

#### what_actually_happened
Tobias received a grade of 61 out of 100 on a May 12 poetry analysis. The rubric categories are structure, evidence, and written expression. The teacher reviewed the grade again and believes it was applied correctly. A grade conference is available any day next week between 3:30 and 4:30 p.m. The parent must email to confirm a day by Friday, May 30. The grade cannot be changed before the review is complete, and adjustment is only possible if a rubric error is found.

#### must_keep
- The student is Tobias.
- The parent is Brigid.
- The assignment is the May 12 poetry analysis.
- The grade is 61 out of 100.
- The rubric categories are structure, evidence, and written expression.
- The teacher reviewed the submission again this morning.
- A grade conference is available next week between 3:30 and 4:30 p.m.
- The parent must email to confirm a day by Friday, May 30.
- The grade cannot be changed before the review is complete.
- A grade adjustment is only possible if a rubric error is found.

#### must_not_claim
- Do not promise the grade will be changed.
- Do not suggest the current grade is incorrect.
- Do not agree to hold the conference before Friday, May 30's confirmation.

#### rewrite_quality_targets
A good rewrite is warm and collegial, makes the conference invitation clear, and preserves both the policy boundary (grade only changes on rubric error) and the confirmation deadline without sounding defensive.

#### expected_rewrite_challenges
The model may imply the grade is likely to change or drop the rubric-error condition, softening the boundary to avoid sounding firm.

### Case 055 - post-demo follow-up
- id: rewrite-draft-055
- category: sales_followup
- source_type: message
- tone_preset: warm
- risk_tags: sales, followup
- input_word_count_band: 100-200

#### input_draft
Hi Vanessa, thanks for joining the demo on May 22. I hope the workflow builder section was useful. I am attaching the one-pager and the pricing sheet you asked about — the standard plan is $299 per month for up to 25 users and the professional plan is $549 per month for up to 75 users. Both prices are as quoted; I do not have room to adjust them without a procurement conversation. If you can share your internal timeline by May 30, I can route to the right next step. Happy to answer any questions in the meantime.

#### what_actually_happened
Vanessa attended a demo on May 22. She requested a one-pager and pricing sheet. The standard plan is $299 per month for up to 25 users. The professional plan is $549 per month for up to 75 users. Pricing cannot be adjusted without a procurement conversation. Vanessa is asked to share her internal timeline by May 30 so the sales contact can route the next step.

#### must_keep
- The lead is Vanessa.
- The demo took place on May 22.
- Vanessa requested a one-pager and pricing sheet.
- The standard plan is $299 per month for up to 25 users.
- The professional plan is $549 per month for up to 75 users.
- Pricing cannot be adjusted without a procurement conversation.
- Vanessa is asked to share her internal timeline by May 30.

#### must_not_claim
- Do not offer a discount.
- Do not say pricing is negotiable without a procurement conversation.
- Do not pressure Vanessa to decide before May 30.

#### rewrite_quality_targets
A good rewrite is light and collegial, provides the requested documents clearly, and states the pricing without apologizing for it or implying flexibility that does not exist.

#### expected_rewrite_challenges
The model may add a discount offer to seem accommodating or leave out the May 30 timeline request.

### Case 056 - form awaiting doctor review
- id: rewrite-draft-056
- category: medical_admin
- source_type: email
- tone_preset: warm
- risk_tags: medical_admin, form
- input_word_count_band: 100-200

#### input_draft
Hi Odette, I am writing about the occupational health clearance form you submitted on May 15 for your return-to-work appointment. The form has been received by our office and is currently with Dr. Vasquez for review. I am administrative staff and cannot tell you what the outcome will be or interpret any part of the medical review. Dr. Vasquez's review queue typically takes 3 to 5 business days from the submission date, which means you should expect contact by May 22. If you need the form for an urgent employment deadline before May 22, please call the clinic on 555-0192 and ask for the admin desk.

#### what_actually_happened
Odette submitted an occupational health clearance form on May 15 for a return-to-work appointment. The form has been received and is with Dr. Vasquez for review. The sender is administrative staff and cannot interpret the medical outcome. The review queue takes 3 to 5 business days, meaning contact is expected by May 22. If there is an urgent employment deadline before May 22, Odette should call the clinic on 555-0192.

#### must_keep
- The patient is Odette.
- The form is the occupational health clearance form.
- The submission date was May 15.
- The form is with Dr. Vasquez for review.
- The sender is administrative staff.
- The review queue typically takes 3 to 5 business days.
- Contact is expected by May 22.
- For urgent employment deadlines before May 22, Odette should call 555-0192.

#### must_not_claim
- Do not predict the outcome of the medical review.
- Do not interpret any part of the form or its clinical content.
- Do not promise a response before May 22 unless the patient calls.

#### rewrite_quality_targets
A good rewrite is warm and reassuring about the process without stepping into clinical territory, keeping dates and the phone escalation path exact.

#### expected_rewrite_challenges
The model may volunteer an implied positive outcome or omit the urgent escalation phone number to keep the message short.

### Case 057 - maintenance access notice
- id: rewrite-draft-057
- category: property_logistics
- source_type: note
- tone_preset: warm
- risk_tags: property, concise
- input_word_count_band: 40-100

#### input_draft
Hi Rafferty, a plumber will need access to Unit 3B on Thursday, June 5 between 10 a.m. and 1 p.m. to inspect the hot water riser. If that time does not work, please contact me at mgmt@example.com by Wednesday, June 4 at 5 p.m. to arrange an alternative.

#### what_actually_happened
A plumber needs access to Unit 3B on Thursday, June 5 between 10 a.m. and 1 p.m. to inspect the hot water riser. The tenant Rafferty is asked to contact management by Wednesday, June 4 at 5 p.m. if the time does not work.

#### must_keep
- The tenant is Rafferty.
- The unit is 3B.
- The access date is Thursday, June 5.
- The access window is 10 a.m. to 1 p.m.
- The reason is to inspect the hot water riser.
- If the time does not work, Rafferty should contact mgmt@example.com by Wednesday, June 4 at 5 p.m.

#### must_not_claim
- Do not guarantee the inspection will be completed within the window.
- Do not say Rafferty must be present.
- Do not offer to reschedule without requiring contact by June 4.

#### rewrite_quality_targets
A good rewrite is brief, neighbourly, and keeps the date, time window, and opt-out path exact without adding words that are not needed.

#### expected_rewrite_challenges
The model may drop the June 4 opt-out deadline or add an implied obligation for the tenant to be present.

### Case 058 - recruiting timeline update
- id: rewrite-draft-058
- category: hr_recruiting
- source_type: message
- tone_preset: warm
- risk_tags: recruiting, concise
- input_word_count_band: 40-100

#### input_draft
Hi Willard, just a quick note — the panel review for the Operations Coordinator role is still in progress. We expect to complete reviews by June 3. I will reach out as soon as we have an update. No decision has been made yet; please do not read anything into the timing.

#### what_actually_happened
Willard is a candidate for the Operations Coordinator role. The panel review is still in progress. Reviews are expected to be complete by June 3. No decision has been made. The recruiter will reach out with an update after June 3.

#### must_keep
- The candidate is Willard.
- The role is Operations Coordinator.
- The panel review is still in progress.
- Reviews are expected to be complete by June 3.
- No decision has been made yet.
- The recruiter will reach out after June 3 with an update.

#### must_not_claim
- Do not say Willard has progressed to the next stage.
- Do not promise a decision by June 3.
- Do not ask Willard to send additional materials.

#### rewrite_quality_targets
A good rewrite is warm and reassuring, keeps the June 3 timeline clear, and avoids language that implies a positive or negative outcome.

#### expected_rewrite_challenges
The model may accidentally imply Willard is still in contention (which reads as a positive signal) or over-soften the "no decision" statement into something ambiguous.

### Case 059 - volunteer capacity waitlist
- id: rewrite-draft-059
- category: nonprofit_community
- source_type: email
- tone_preset: warm
- risk_tags: nonprofit, capacity
- input_word_count_band: 100-200

#### input_draft
Hi Ignacio, thank you for signing up to volunteer at the Riverside Community Garden harvest day on June 14. Unfortunately the Saturday morning shift (8 a.m. to 12 p.m.) that you selected is now full — we have 20 volunteers confirmed and cannot take any more for that slot. I have added you to the waitlist and you are currently in position 3. If a spot opens before June 10, I will contact you at this email address. There is still space in the afternoon shift from 1 p.m. to 4 p.m. if you would like to switch. Please reply by June 7 if you would like the afternoon shift instead.

#### what_actually_happened
Ignacio signed up to volunteer at the Riverside Community Garden harvest day on June 14. The morning shift from 8 a.m. to 12 p.m. is full at 20 volunteers. Ignacio has been added to the waitlist at position 3. If a spot opens before June 10, Ignacio will be contacted. The afternoon shift from 1 p.m. to 4 p.m. still has space. Ignacio must reply by June 7 to switch to the afternoon shift.

#### must_keep
- The volunteer is Ignacio.
- The event is the Riverside Community Garden harvest day on June 14.
- The morning shift is 8 a.m. to 12 p.m.
- The morning shift is full at 20 volunteers.
- Ignacio is on the waitlist at position 3.
- If a spot opens before June 10, Ignacio will be contacted.
- The afternoon shift is 1 p.m. to 4 p.m.
- The afternoon shift still has space.
- Ignacio must reply by June 7 to switch to the afternoon shift.

#### must_not_claim
- Do not guarantee Ignacio a morning shift spot.
- Do not say Ignacio's waitlist position will improve.
- Do not promise contact before June 10 unless a spot opens.

#### rewrite_quality_targets
A good rewrite thanks Ignacio sincerely, explains the capacity situation without making it feel like a rejection, and presents the afternoon shift alternative and the June 7 response deadline clearly.

#### expected_rewrite_challenges
The model may imply a morning spot is likely to open or omit the waitlist position number to soften the message.

### Case 060 - account cancellation boundary
- id: rewrite-draft-060
- category: customer_success
- source_type: note
- tone_preset: warm
- risk_tags: cancellation, boundary
- input_word_count_band: 100-200

#### input_draft
Hi Dorothea, I received your cancellation request for account ACC-88120. The cancellation will take effect on June 15, which is the end of your current billing cycle. You will keep full access to your workspace and all your data until June 15. After June 15 your data will be held in read-only archive for 30 days, so until July 15. After July 15 the data will be permanently deleted in line with our retention policy. I cannot issue a refund for the remaining days in the June cycle because the plan does not include pro-rata refunds. If you change your mind before June 15, just reply to this note and I can reactivate the account.

#### what_actually_happened
Dorothea requested cancellation of account ACC-88120. The cancellation takes effect on June 15 at the end of the billing cycle. Full workspace access continues until June 15. After June 15, data moves to a read-only archive for 30 days, until July 15. After July 15 data is permanently deleted. No pro-rata refund is available for remaining days in the June cycle. Dorothea can reverse the cancellation before June 15 by replying.

#### must_keep
- The customer is Dorothea.
- The account identifier is ACC-88120.
- The cancellation takes effect on June 15.
- June 15 is the end of the current billing cycle.
- Full access continues until June 15.
- After June 15, data moves to a read-only archive for 30 days.
- The read-only archive period ends on July 15.
- After July 15, data is permanently deleted per the retention policy.
- No pro-rata refund is available for remaining days in the June cycle.
- Dorothea can reactivate the account by replying before June 15.

#### must_not_claim
- Do not promise a refund for days remaining in the June cycle.
- Do not extend data access beyond July 15.
- Do not say the cancellation has not been processed.
- Do not confirm reactivation without Dorothea's reply.

#### rewrite_quality_targets
A good rewrite is calm and clear, confirms the cancellation timeline and data access windows precisely, and leaves the reactivation door open without sounding like a sales retention pitch.

#### expected_rewrite_challenges
The model may omit the July 15 permanent deletion date to avoid sounding harsh, or soften the no-refund boundary into ambiguous language that implies a refund might be possible.

### Case 061 - multi-item return status list
- id: rewrite-draft-061
- category: customer_support
- source_type: email
- tone_preset: warm
- risk_tags: support, formatting
- input_word_count_band: 200-320

#### input_draft
Hi Vivienne, I pulled up order ORD-29447 and reviewed everything against the return you started on June 2. I want to walk through each item because they are at different stages and I do not want any of them to get lost.

Here is where things stand:

- Ceramic pour-over carafe (SKU CPV-11): The return label was emailed on June 3. We have not received the package yet. Once it arrives at our warehouse, I can process a $34.00 refund to your card within 3-5 business days.
- Stainless travel cup (SKU STC-07): The package arrived on June 5 and we confirmed the item is undamaged and unused. The $22.50 refund was issued on June 6 and should appear on your statement within 2 business days.
- Replacement lid (SKU LID-04): You noted the lid was cracked on arrival. I need one photo of the cracked lid before I can send a replacement. Please send the photo by June 10 at noon or the replacement request will close automatically.

I cannot issue a refund for the pour-over carafe until we physically receive the item, and I cannot send the replacement lid before the photo arrives. Everything else is in progress.

#### what_actually_happened
The customer Vivienne has an active return on order ORD-29447, opened June 2. Three items are at different stages: the ceramic pour-over carafe (SKU CPV-11) has a return label sent June 3 but has not arrived at the warehouse yet — a $34.00 refund will follow receipt; the stainless travel cup (SKU STC-07) arrived June 5, was confirmed undamaged, and a $22.50 refund was issued June 6; the replacement lid (SKU LID-04) requires a photo of the cracked lid by June 10 at noon before a replacement can be shipped.

#### must_keep
- The customer is Vivienne.
- The order identifier is ORD-29447.
- The return was opened on June 2.
- The ceramic pour-over carafe SKU is CPV-11.
- The return label for CPV-11 was emailed on June 3.
- The pour-over carafe has not arrived at the warehouse yet.
- The refund for CPV-11 is $34.00.
- The stainless travel cup SKU is STC-07.
- The travel cup arrived on June 5 and was confirmed undamaged and unused.
- The $22.50 refund for STC-07 was issued on June 6.
- The replacement lid SKU is LID-04 and was cracked on arrival.
- A photo of the cracked lid is required by June 10 at noon before a replacement is sent.

#### must_not_claim
- Do not issue a refund for CPV-11 before the warehouse receives the item.
- Do not send the replacement lid before the photo arrives.
- Do not say all three items have been fully resolved.
- Do not change the refund amounts of $34.00 or $22.50.

#### rewrite_quality_targets
A good rewrite preserves the three-item structure with all SKUs, amounts, and dates intact, leads with what has already been resolved, and keeps the photo deadline and warehouse-receipt dependency clear without sounding bureaucratic.

#### expected_rewrite_challenges
The model may flatten the list into prose and lose an SKU or date, or may imply the pour-over refund is already in progress rather than gated on warehouse receipt.

### Case 062 - credited invoice with quoted customer concern
- id: rewrite-draft-062
- category: billing_support
- source_type: email
- tone_preset: warm
- risk_tags: billing, quote_risk
- input_word_count_band: 200-320

#### input_draft
Hi Fletcher, thank you for writing in. You mentioned in your note that you were "charged twice for the same month," and I want to make sure I address that clearly.

I reviewed account BL-5530 and the two invoices in question. Invoice INV-7741 for $98.00 was issued on April 1 for the April billing cycle. Invoice INV-7742 for $98.00 was issued on April 2. After checking the billing log, I can see that INV-7742 was generated in error when our system retried a failed payment attempt and treated it as a new period. That is a system error on our side, not a second billing month.

I have already voided INV-7742, so you owe nothing on that invoice. I applied a $98.00 credit to account BL-5530 today, May 12. The credit will offset the next invoice automatically — you do not need to do anything. If the credit does not appear on your May statement, please reply and I will investigate the posting.

I cannot refund the original INV-7741 charge because that invoice covers the legitimate April service period.

#### what_actually_happened
Customer Fletcher contacted billing claiming a double charge. Account BL-5530 had two invoices of $98.00 each: INV-7741 (April 1, legitimate April cycle) and INV-7742 (April 2, generated in error by a payment-retry system glitch). INV-7742 was voided and a $98.00 credit was applied to the account on May 12. The credit offsets the next invoice automatically. INV-7741 covers legitimate service and cannot be refunded.

#### must_keep
- The customer is Fletcher.
- The account identifier is BL-5530.
- Invoice INV-7741 is for $98.00 and was issued on April 1 for the April billing cycle.
- Invoice INV-7742 is for $98.00 and was issued on April 2.
- INV-7742 was generated in error by a payment-retry system glitch.
- INV-7742 has been voided.
- A $98.00 credit was applied to account BL-5530 on May 12.
- The credit will offset the next invoice automatically.
- Fletcher does not need to take any action.
- Fletcher should reply if the credit does not appear on the May statement.
- The INV-7741 charge covers the legitimate April service period and cannot be refunded.

#### must_not_claim
- Do not refund INV-7741.
- Do not say the credit has already appeared on the May statement.
- Do not imply the account was charged for two legitimate months.
- Do not promise any additional compensation beyond the $98.00 credit.

#### rewrite_quality_targets
A good rewrite explains the invoice error and resolution calmly, preserves Fletcher's quoted concern without over-inflating the apology, and makes the credit path and no-action-needed message easy to understand.

#### expected_rewrite_challenges
The model may over-apologize and imply a broader refund, or it may absorb the customer's quoted phrase in a way that makes it sound like the company admitted to charging for two legitimate months.

### Case 063 - three-workstream deadline update
- id: rewrite-draft-063
- category: workplace_update
- source_type: message
- tone_preset: warm
- risk_tags: workplace, multi_deadline
- input_word_count_band: 200-320

#### input_draft
Hey all, here is the status for the Ironwood launch workstreams heading into this week. Things are a bit fragmented so I want to lay it out clearly.

Content (owner: Petra): The onboarding copy is done. The FAQ page needs two more rounds of legal review before it can publish. Legal said they can turn the first review by June 11, so the FAQ is not unlocked until at least June 12 at best.

Engineering (owner: Soren): The API endpoints are code-complete as of Friday. Final smoke tests are scheduled for June 10. If smoke passes, the staging environment is ready June 10 EOD. If smoke fails, we need to regroup.

Design (owner: Blessing): The brand kit assets are delivered. The loading animation still needs a final sign-off from the client. Blessing is following up with the client today. We have not received client sign-off yet.

The overall launch cannot proceed until legal clears the FAQ, smoke tests pass, and the client signs off on the animation. I need everyone to flag any blockers to me by June 9 at 3 p.m. so we can decide whether to hold the June 12 target or push it.

#### what_actually_happened
The Ironwood launch has three workstreams. Content (Petra): onboarding copy is done; the FAQ page needs two more legal reviews, with the first due June 11 at earliest. Engineering (Soren): API endpoints are code-complete; smoke tests are June 10, staging ready June 10 EOD if they pass. Design (Blessing): brand kit assets delivered; client sign-off on the loading animation is pending, follow-up happening today. Launch requires all three gates: FAQ legal clearance, smoke test pass, and client sign-off. Everyone must flag blockers by June 9 at 3 p.m. to confirm or push the June 12 target.

#### must_keep
- The project is the Ironwood launch.
- The content workstream owner is Petra.
- The FAQ page needs two more legal reviews.
- Legal can deliver the first review by June 11.
- The FAQ is not unlocked until at least June 12.
- The engineering workstream owner is Soren.
- Smoke tests are scheduled for June 10.
- Staging is ready June 10 EOD if smoke passes.
- The design workstream owner is Blessing.
- Client sign-off on the loading animation is still pending.
- Everyone must flag blockers by June 9 at 3 p.m.
- The target launch date is June 12.

#### must_not_claim
- Do not say the FAQ is cleared for publication.
- Do not say smoke tests have already passed.
- Do not say client sign-off on the animation has been received.
- Do not promise the June 12 launch date will hold.

#### rewrite_quality_targets
A good rewrite preserves the three-workstream structure with all owners, dates, and open blockers intact, and makes the June 9 escalation deadline and the conditional June 12 target clear without sounding alarmist.

#### expected_rewrite_challenges
The model may compress the three workstreams into prose, losing an owner name or date, or may imply the launch is on track when it has three unresolved blockers.

### Case 064 - classroom concern with action steps
- id: rewrite-draft-064
- category: teacher_parent
- source_type: email
- tone_preset: warm
- risk_tags: teacher, formatting
- input_word_count_band: 200-320

#### input_draft
Hi Rosalind, I wanted to reach out because I have noticed some things in class over the past two weeks that I think are worth sharing with you about Kaspar.

I am not writing because anything is seriously wrong, but I do want to be proactive. Here is what I have observed and what we have already put in place:

- On May 13, Kaspar had difficulty staying on task during the group mapping project and asked to work alone. I let him move to the reading corner, and he completed his portion independently.
- On May 16 and May 19, he left the classroom twice during transition time without checking in first. I spoke with him each time and reminded him of the check-in procedure.
- Starting May 20, I moved Kaspar's seat to the front row near the whiteboard to reduce distractions. He has responded well so far.

I would like to meet with you to talk about whether there is anything happening outside school that I should be aware of. I have availability on May 28 from 3:30 to 4:15 p.m. or June 2 from 3:15 to 4:00 p.m. Please let me know which works better, or reply if you would prefer a phone call instead.

#### what_actually_happened
The teacher contacted parent Rosalind about Kaspar after observing behavior over two weeks. On May 13 Kaspar had difficulty staying on task and worked alone in the reading corner. On May 16 and May 19 he left the classroom twice without checking in; the teacher spoke with him each time. Starting May 20 his seat was moved to the front row near the whiteboard. The teacher is proposing a meeting on May 28 from 3:30-4:15 p.m. or June 2 from 3:15-4:00 p.m., or a phone call.

#### must_keep
- The student is Kaspar.
- The parent is Rosalind.
- The teacher has observed concerns over the past two weeks.
- On May 13, Kaspar had difficulty staying on task during the group mapping project.
- On May 13, Kaspar moved to the reading corner and completed his portion independently.
- On May 16 and May 19, Kaspar left the classroom twice without checking in.
- The teacher spoke with Kaspar each time and reminded him of the check-in procedure.
- Starting May 20, Kaspar's seat was moved to the front row near the whiteboard.
- Meeting option one is May 28 from 3:30 to 4:15 p.m.
- Meeting option two is June 2 from 3:15 to 4:00 p.m.
- A phone call is also an option.

#### must_not_claim
- Do not diagnose Kaspar with any condition or disorder.
- Do not say the behavior is a disciplinary violation requiring formal action.
- Do not imply Rosalind has been unresponsive or uninvolved.
- Do not present the seat change as a punishment.

#### rewrite_quality_targets
A good rewrite preserves the dated observations and the two meeting-time options, keeps a warm and collaborative tone, and frames the actions taken as supportive steps rather than disciplinary measures.

#### expected_rewrite_challenges
The model may soften the specific dates out of the list, or may hedge so heavily that the meeting request and its two concrete times get buried.

### Case 065 - two-tier proposal with expiry
- id: rewrite-draft-065
- category: sales_followup
- source_type: email
- tone_preset: warm
- risk_tags: sales, options
- input_word_count_band: 200-320

#### input_draft
Hi Callum, great speaking with you on May 22. I put together two options based on what you shared about Fieldstone's needs. I want to be transparent about what each covers so there are no surprises.

Option A — Starter: 10 seats, onboarding, standard support. $580 per month. This option does not include the custom report builder or API access.

Option B — Professional: 10 seats, onboarding, standard support, custom report builder, and API access. $890 per month.

Both quotes are valid until June 6. I cannot discount either option further without a new approval from my manager, and I cannot transfer features from Option B into Option A at the Option A price. If you need fewer than 10 seats, I would need to rebuild the quote entirely because the seat count affects the pricing tier.

If Option A is enough for now, we can start there and upgrade later, but the upgrade would be priced at whatever the rate is at that time, not today's rate. Please let me know which option fits by June 6.

#### what_actually_happened
Sales contact Callum is evaluating two options for Fieldstone. Option A: 10 seats, onboarding, standard support, $580/month, no custom report builder, no API access. Option B: 10 seats, onboarding, standard support, custom report builder, API access, $890/month. Both quotes expire June 6. Discounts require manager approval. Features cannot be mixed between tiers at the lower price. Fewer than 10 seats would require a full rebuild.

#### must_keep
- The contact is Callum.
- The account is Fieldstone.
- The meeting date was May 22.
- Option A is 10 seats, onboarding, standard support at $580 per month.
- Option A does not include the custom report builder or API access.
- Option B is 10 seats, onboarding, standard support, custom report builder, and API access at $890 per month.
- Both quotes are valid until June 6.
- Neither option can be discounted without manager approval.
- Features from Option B cannot be added to Option A at the Option A price.
- A seat count below 10 would require a full quote rebuild.
- An upgrade after starting with Option A would be priced at the rate at that time, not today's rate.

#### must_not_claim
- Do not offer a discount on either option.
- Do not combine Option B features into Option A at the Option A price.
- Do not extend the June 6 expiry.
- Do not guarantee the current rate will be available at upgrade time.

#### rewrite_quality_targets
A good rewrite keeps the two-option structure legible with prices and inclusions intact, explains the scope boundaries without sounding restrictive, and makes the June 6 deadline and next step clear.

#### expected_rewrite_challenges
The model may merge the two options into a single description losing a price or feature, or may blur the boundary about mixing features by framing it as "something we could look into."

### Case 066 - numbered admin steps for records request
- id: rewrite-draft-066
- category: medical_admin
- source_type: note
- tone_preset: warm
- risk_tags: medical_admin, formatting
- input_word_count_band: 200-320

#### input_draft
Hi Gwendolyn, I am following up on your request to have Dr. Okafor's office receive a copy of your imaging records from your April 18 appointment. Here are the steps you need to take to get this moving, because we cannot release records without your signed authorization.

1. Log in to the patient portal at portal.example.org and go to "My Records" then "Release Authorization."
2. Complete the Release of Medical Records form. Make sure you select Dr. Okafor's practice as the recipient and check the box for "Diagnostic Imaging" under record type.
3. Sign and submit the form inside the portal. Do not email a photo of a paper form — the portal version is the only one our records team will accept.
4. Once the signed form is received, our records team typically processes release requests within 5 business days.
5. If you have not heard anything within 7 business days of submitting the form, call the records office at ext. 3114.

I cannot give you a status on the release or contact Dr. Okafor's office on your behalf until the signed authorization is in our system. If you believe this is urgent, please ask Dr. Okafor's office to mark the incoming request as urgent and fax a priority cover sheet to 555-0192.

#### what_actually_happened
Patient Gwendolyn requested that her April 18 imaging records be released to Dr. Okafor's office. The records team requires a signed Release of Medical Records authorization submitted through the patient portal (portal.example.org). The numbered steps guide her through the portal authorization. Processing takes up to 5 business days after receipt. No status update or outreach to Dr. Okafor's office can happen before the form is in the system. If urgent, Dr. Okafor's office should fax a priority cover sheet to 555-0192.

#### must_keep
- The patient is Gwendolyn.
- The records are from the April 18 appointment.
- The recipient is Dr. Okafor's office.
- The authorization must be submitted through the patient portal at portal.example.org.
- The portal path is "My Records" then "Release Authorization."
- The form must select Dr. Okafor's practice as recipient.
- The record type checkbox is "Diagnostic Imaging."
- Only the portal version of the form is accepted; a photo of a paper form is not.
- The records team typically processes requests within 5 business days of receiving the signed form.
- If no response within 7 business days, the patient should call ext. 3114.
- The records team cannot act or contact Dr. Okafor's office until the signed form is in the system.
- Urgent requests: Dr. Okafor's office should fax a priority cover sheet to 555-0192.

#### must_not_claim
- Do not interpret or describe the contents of the imaging records.
- Do not say the release has already been processed or is in progress.
- Do not accept a paper form photo as an alternative to the portal submission.
- Do not promise release within a specific number of days as a guarantee.

#### rewrite_quality_targets
A good rewrite preserves all five numbered steps with their portal path, form details, and extension number intact, and keeps the urgent-fax path visible without making the process sound intimidating.

#### expected_rewrite_challenges
The model may consolidate the numbered steps into prose and lose the portal path, the fax number, or the 7-business-day escalation window.

### Case 067 - two-vendor repair update
- id: rewrite-draft-067
- category: property_logistics
- source_type: email
- tone_preset: warm
- risk_tags: property, multi_action
- input_word_count_band: 200-320

#### input_draft
Hi Ondine, I wanted to give you a full update on the two active repairs in Unit 12C.

HVAC (vendor: Coldstream Services): Coldstream came on May 27 and diagnosed a failed capacitor in the air handler. The part has been ordered and Coldstream expects delivery by June 3. They will schedule the installation call within 24 hours of receiving the part. I will confirm the access window for the installation as soon as I have it — I do not have a confirmed date yet. Please do not adjust your thermostat settings while the capacitor is out; Coldstream flagged that this can strain the fan motor.

Kitchen faucet (vendor: Hartley Plumbing): Hartley completed the faucet replacement on May 28. The work order is closed. I inspected the under-sink area on May 28 and no residual leak was found. If you notice any dripping at the new faucet or under the sink after May 28, please photograph it and reply to this email — I will reopen the Hartley work order.

I cannot approve any rent adjustment or compensation in relation to these repairs through this maintenance thread. If you have a separate concern about rent, that needs to go through the lease management team.

#### what_actually_happened
Unit 12C has two open maintenance items. Coldstream Services diagnosed a failed HVAC capacitor on May 27; the part is ordered with expected delivery by June 3; the installation date is not yet confirmed. Hartley Plumbing completed the kitchen faucet replacement on May 28; a May 28 inspection found no residual leak. If new dripping appears, Ondine should photograph it and reply. No rent adjustment is available through the maintenance thread.

#### must_keep
- The tenant is Ondine.
- The unit is 12C.
- The HVAC vendor is Coldstream Services.
- Coldstream visited on May 27 and diagnosed a failed capacitor.
- The capacitor part is ordered with expected delivery by June 3.
- The HVAC installation date has not been confirmed yet.
- Ondine should not adjust thermostat settings while the capacitor is out.
- The kitchen faucet vendor is Hartley Plumbing.
- Hartley completed the faucet replacement on May 28.
- A May 28 inspection found no residual leak.
- If dripping appears after May 28, Ondine should photograph it and reply.
- Rent adjustment or compensation is not available through the maintenance thread.

#### must_not_claim
- Do not give a confirmed HVAC installation date.
- Do not say the HVAC repair is complete.
- Do not promise any rent credit or compensation.
- Do not say the faucet issue is likely to recur.

#### rewrite_quality_targets
A good rewrite preserves the two-vendor structure with vendor names, visit dates, and next steps intact, and delivers the rent-adjustment boundary plainly without sounding dismissive about the inconvenience.

#### expected_rewrite_challenges
The model may merge the two vendors into a single paragraph and lose a visit date or vendor name, or may soften the rent-adjustment boundary by implying it could be discussed elsewhere.

### Case 068 - interview scheduling matrix
- id: rewrite-draft-068
- category: hr_recruiting
- source_type: email
- tone_preset: warm
- risk_tags: recruiting, scheduling
- input_word_count_band: 200-320

#### input_draft
Hi Dashiell, congratulations on moving to the final interview stage for the Product Operations Analyst role. We are delighted to have you move forward.

I need to lock in a time that works for you and the three-person panel. The panel is in the US/Pacific timezone. Here are the available slots:

- Tuesday, June 10: 10:00-11:30 a.m. PT or 2:00-3:30 p.m. PT
- Thursday, June 12: 9:00-10:30 a.m. PT only (the afternoon slot on this day is taken)
- Friday, June 13: 11:00 a.m.-12:30 p.m. PT only

All interviews are 90 minutes via video call. I will send the video link after confirming your slot. Please reply with your first and second preference by June 6 at 5:00 p.m. PT. If none of the slots work, reply by June 6 as well and I will check with the panel for alternatives — but I cannot guarantee new slots will be available before the June 13 deadline.

If you do not reply by June 6 at 5:00 p.m. PT, the system will release the holds and I will need to restart the scheduling process, which may affect the June 13 deadline.

#### what_actually_happened
Dashiell advanced to the final interview for the Product Operations Analyst role. The three-person panel is in US/Pacific timezone. Available slots: Tuesday June 10 at 10:00-11:30 a.m. PT or 2:00-3:30 p.m. PT; Thursday June 12 at 9:00-10:30 a.m. PT only; Friday June 13 at 11:00 a.m.-12:30 p.m. PT only. All interviews are 90 minutes by video. Dashiell must reply by June 6 at 5:00 p.m. PT with first and second preference. Missing the June 6 deadline releases the holds and may push the June 13 target.

#### must_keep
- The candidate is Dashiell.
- The role is Product Operations Analyst.
- The panel is three people in US/Pacific timezone.
- Tuesday June 10 slots: 10:00-11:30 a.m. PT and 2:00-3:30 p.m. PT.
- Thursday June 12 slot: 9:00-10:30 a.m. PT only.
- Friday June 13 slot: 11:00 a.m.-12:30 p.m. PT only.
- All interviews are 90 minutes via video call.
- The video link will be sent after the slot is confirmed.
- Dashiell must reply by June 6 at 5:00 p.m. PT with first and second preference.
- Missing the June 6 deadline releases the holds and may affect the June 13 deadline.

#### must_not_claim
- Do not say Dashiell has been offered the job.
- Do not guarantee alternative slots if none of the listed times work.
- Do not change any listed time slot or timezone.
- Do not say the June 13 deadline is flexible.

#### rewrite_quality_targets
A good rewrite preserves the full slot matrix with dates and times in Pacific, keeps the 90-minute format and video-link note, and makes the June 6 reply deadline and its consequence clear without sounding threatening.

#### expected_rewrite_challenges
The model may abbreviate the slot list and lose a time option, or may soften the hold-release consequence so much that it no longer reads as a real deadline.

### Case 069 - summer program logistics announcement
- id: rewrite-draft-069
- category: nonprofit_community
- source_type: announcement
- tone_preset: warm
- risk_tags: nonprofit, announcement
- input_word_count_band: 200-320

#### input_draft
Hello Brightway families, here is everything you need for the start of the Summer Makers Program on June 16.

Location: All sessions are at the Riverside Community Center, Room 114. Do not go to the main gymnasium — Room 114 is down the east corridor on the right.

Drop-off and pick-up: Drop-off begins at 8:45 a.m. Pick-up ends at 3:15 p.m. Please be on time for pick-up; we have a staffing constraint after 3:15 p.m. and cannot supervise participants past that time.

What to bring: Each participant should bring a snack, a water bottle, and closed-toe shoes. We will provide all craft supplies. Do not bring personal electronics — they will be stored away and we cannot be responsible for them.

Volunteer spots: We have three volunteer spots available for June 16. If you can help, email volunteers@example.org by June 13. Spots are first-come, first-served.

Weather: This is an indoor program, so weather will not affect the June 16 session.

Questions: Contact the program coordinator at programs@example.org or call 555-0174 before June 14.

#### what_actually_happened
The Brightway Summer Makers Program begins June 16 at Riverside Community Center, Room 114. Drop-off starts at 8:45 a.m.; pick-up ends at 3:15 p.m. with no supervision past that time. Participants need a snack, water bottle, and closed-toe shoes; personal electronics should not be brought. Three volunteer spots are available for June 16 — email volunteers@example.org by June 13, first-come, first-served. The June 16 session is indoors and not weather-dependent. Questions: programs@example.org or 555-0174 before June 14.

#### must_keep
- The program is the Summer Makers Program.
- The start date is June 16.
- The venue is Riverside Community Center, Room 114.
- Room 114 is down the east corridor, not the main gymnasium.
- Drop-off begins at 8:45 a.m.
- Pick-up ends at 3:15 p.m. with no supervision past that time.
- Participants should bring a snack, a water bottle, and closed-toe shoes.
- Personal electronics should not be brought.
- Three volunteer spots are available for June 16.
- Volunteer email is volunteers@example.org by June 13.
- The program is indoors and weather will not affect June 16.
- Contact is programs@example.org or 555-0174 before June 14.

#### must_not_claim
- Do not say the main gymnasium is the correct location.
- Do not say supervision continues past 3:15 p.m.
- Do not promise volunteer spots are still available after the announcement.
- Do not say personal electronics are welcome.

#### rewrite_quality_targets
A good rewrite preserves all six sub-sections (location, drop-off/pick-up, what to bring, volunteer spots, weather, questions) with all times, emails, and phone numbers intact, while making the tone feel welcoming rather than procedural.

#### expected_rewrite_challenges
The model may compress multiple sections and lose the east-corridor directional note, the 3:15 p.m. supervision boundary, or the June 13 volunteer deadline.

### Case 070 - account admin transfer request
- id: rewrite-draft-070
- category: customer_success
- source_type: email
- tone_preset: warm
- risk_tags: account_admin, security
- input_word_count_band: 200-320

#### input_draft
Hi Fernanda, thank you for reaching out about transferring admin access on account WS-8841 to your new IT lead. I want to make sure this goes smoothly and securely, so here is what needs to happen before I can make the change.

For a transfer of primary account admin, our security protocol requires:

1. Verification from the current admin: I need the current primary admin, listed on the account as Henrik Larsson (henrik.larsson@example.org), to reply to this thread or send a separate email from that address confirming the transfer. I cannot act on a request from another account member alone.
2. New admin details: Please provide the full name, work email address, and the title or role of the incoming IT lead so I can set up their access correctly.
3. Effective date: Let me know the date you want the transfer to take effect. I can process same-day if both confirmations are received before 2:00 p.m. EST. Requests after 2:00 p.m. EST are processed the following business day.

Once I have all three items, I will send a confirmation to both Henrik and the new admin, and I will archive the current admin credentials. I cannot process the transfer or create a second simultaneous primary admin before all three items are confirmed.

#### what_actually_happened
Fernanda requested a primary account admin transfer on account WS-8841 to a new IT lead. The current primary admin is Henrik Larsson (henrik.larsson@example.org). The security protocol requires three things: confirmation from Henrik via email, the new admin's full name, work email, and title, and an effective date. Same-day processing requires all three items before 2:00 p.m. EST; after that, the next business day. Upon completion, both Henrik and the new admin receive confirmation and current credentials are archived. No transfer or dual admin state is possible before all three are confirmed.

#### must_keep
- The account contact is Fernanda.
- The account identifier is WS-8841.
- The current primary admin is Henrik Larsson at henrik.larsson@example.org.
- Henrik must reply from that email address confirming the transfer.
- The new admin's full name, work email, and title are required.
- An effective date is required for the transfer.
- Same-day processing requires all three items before 2:00 p.m. EST.
- Requests received after 2:00 p.m. EST are processed the following business day.
- Upon transfer, both Henrik and the new admin receive confirmation.
- Current admin credentials are archived after the transfer.
- The transfer cannot be processed and a second simultaneous primary admin cannot be created before all three items are confirmed.

#### must_not_claim
- Do not process the transfer before receiving all three required items.
- Do not create a second simultaneous primary admin at any stage.
- Do not guarantee same-day processing regardless of the time of submission.
- Do not allow Fernanda's request alone, without Henrik's confirmation, to authorize the transfer.

#### rewrite_quality_targets
A good rewrite preserves the three numbered steps with Henrik's exact email address, the 2:00 p.m. EST same-day cutoff, and the dual-admin prohibition intact, while making the process feel supportive rather than bureaucratic.

#### expected_rewrite_challenges
The model may soften the requirement for Henrik's explicit confirmation, drop the 2:00 p.m. EST same-day cutoff, or inadvertently imply the transfer can begin as soon as Fernanda provides the new admin's details.

### Case 071 - angry customer complaint cleanup
- id: rewrite-draft-071
- category: customer_support
- source_type: email
- tone_preset: warm
- risk_tags: support, harsh_tone
- input_word_count_band: 320-400

#### input_draft
Hi, I am writing this because honestly I am very frustrated and I think your team handled this incredibly poorly. I placed order ORD-58291 on April 14 and when the package finally arrived on April 22 the blender base was cracked straight across the motor housing. I called the support line on April 23 and spoke with someone named Felix who told me a replacement would ship within three business days. It is now April 30 — seven days later — and no replacement has arrived and no one has contacted me. I checked the portal and the ticket is still sitting at CX-77401 with no notes. I want to be clear that I should not have to chase this. I work full time and I do not have time to keep following up on something your team promised to fix. The blender cost $94.00 and I have not been able to use it. I need a straight answer: is a replacement shipping or not? If not, I want a full refund of the $94.00 to the card I paid with. Please do not send me a form response telling me you are sorry. I need action by May 2.

#### what_actually_happened
The customer placed order ORD-58291 on April 14. The package arrived on April 22 with a cracked blender base motor housing. They called support on April 23 and spoke with a representative named Felix, who promised a replacement within three business days. As of April 30 no replacement has arrived and the ticket CX-77401 has no updates. The blender cost $94.00. The customer is demanding either a replacement or a full $94.00 refund by May 2.

#### must_keep
- The order identifier is ORD-58291.
- The order was placed on April 14.
- The package arrived on April 22 with a cracked blender base motor housing.
- The customer called on April 23 and spoke with Felix.
- Felix said a replacement would ship within three business days.
- As of April 30, no replacement has arrived.
- The ticket identifier is CX-77401.
- The blender cost $94.00.
- The customer is requesting either a replacement or a full $94.00 refund.
- The customer wants a resolution by May 2.

#### must_not_claim
- Do not admit that the agent Felix made a binding shipping guarantee.
- Do not promise the replacement has already shipped.
- Do not promise a refund without following the review process.
- Do not extend the May 2 response deadline.

#### rewrite_quality_targets
The rewrite should convert the frustrated draft into a firm, clear support escalation that acknowledges the delay, restates every fact accurately, and sets a concrete next-step expectation without over-committing to a resolution method.

#### expected_rewrite_challenges
A model may try to soothe the customer by implying the refund is already approved, or may strip the ticket number and staff name to seem cleaner.

### Case 072 - duplicate charge apology sprawl
- id: rewrite-draft-072
- category: billing_support
- source_type: email
- tone_preset: warm
- risk_tags: billing, apology_control
- input_word_count_band: 320-400

#### input_draft
Hi Brigitte, I am so, so sorry about this. I genuinely cannot believe this happened and I want you to know I completely understand how upsetting it must be. I reviewed your account and I can confirm that invoice INV-22018 for $159.00 and invoice INV-22019 for $159.00 were both charged to your card on May 10. They should not both have posted because INV-22019 was a system-generated duplicate tied to a billing cycle sync error on our end. I am deeply sorry. Our billing team opened review case BC-4401 on May 11 and they are investigating the duplicate. The review typically takes five to seven business days, so the latest expected close date is May 21. I cannot guarantee a refund before the review closes, but if the duplicate is confirmed the standard resolution is a credit or refund of $159.00. I know that is not the instant answer you deserve, and I am truly sorry you have to wait. Please do not hesitate to reach out if you have not heard from us by May 22.

#### what_actually_happened
Brigitte was charged twice: invoice INV-22018 for $159.00 and invoice INV-22019 for $159.00, both on May 10. INV-22019 is a system-generated duplicate caused by a billing cycle sync error. Review case BC-4401 was opened on May 11. The review takes five to seven business days with a latest expected close date of May 21. If the duplicate is confirmed, the standard resolution is a credit or refund of $159.00. No refund can be guaranteed before the review closes.

#### must_keep
- The customer is Brigitte.
- Invoice INV-22018 is for $159.00.
- Invoice INV-22019 is for $159.00.
- Both invoices were charged on May 10.
- INV-22019 is a system-generated duplicate from a billing cycle sync error.
- Review case BC-4401 was opened on May 11.
- The review takes five to seven business days.
- The latest expected close date is May 21.
- A refund cannot be guaranteed before the review closes.
- If confirmed, the standard resolution is a credit or refund of $159.00.
- Brigitte should follow up if she has not heard back by May 22.

#### must_not_claim
- Do not guarantee a refund before the review closes.
- Do not admit liability beyond describing the billing sync error.
- Do not change the $159.00 refund amount.
- Do not promise the review will close before May 21.

#### rewrite_quality_targets
The rewrite should feel empathetic and clear without burying the facts in excessive apologies. It should lead with acknowledgment, state the facts precisely, and close with a concrete next step.

#### expected_rewrite_challenges
A model is likely to either preserve the apology sprawl (not fixing the primary failure) or over-correct by stripping all warmth and sounding clinical.

### Case 073 - executive initiative summary
- id: rewrite-draft-073
- category: workplace_update
- source_type: email
- tone_preset: warm
- risk_tags: workplace, executive
- input_word_count_band: 320-400

#### input_draft
Hi leadership team, I wanted to give you a full update on the Meridian data platform initiative so everyone has the same picture. As of May 19 we have completed the ingestion layer migration and the QA dashboard is live. The data freshness metric is currently sitting at 94.2%, which is below the 98% target we agreed on in Q1. The root cause is a batch job owned by Riko that has been failing intermittently since May 12 due to a schema mismatch introduced during the May 9 deploy. Riko's team has a patch ready and we are targeting deployment to staging by May 23 and production by May 26. I need two things from leadership: first, sign-off from Claudette on the revised data-freshness SLA interim period (we need this by May 21 so we can notify downstream teams), and second, a go/no-go decision from the steering group on whether to defer the June 3 Phase 2 kickoff if the May 26 production fix is not confirmed clean. If Phase 2 is deferred we are looking at a June 10 kickoff instead. The risk if we proceed on June 3 without a clean fix is that the pipeline feeds the customer-facing analytics portal, which is already on the Q2 roadmap.

#### what_actually_happened
The Meridian data platform initiative completed the ingestion layer migration and the QA dashboard is live. Data freshness is 94.2% against a 98% target. The gap is caused by a batch job owned by Riko that has been failing since May 12 due to a schema mismatch from the May 9 deploy. A patch targets staging by May 23 and production by May 26. Two asks: Claudette's sign-off on the interim SLA by May 21, and a steering group go/no-go on June 3 Phase 2 versus a June 10 deferral.

#### must_keep
- The initiative is Meridian data platform.
- The ingestion layer migration is complete.
- The QA dashboard is live.
- The current data freshness metric is 94.2%.
- The agreed target is 98%.
- The root cause is a batch job owned by Riko.
- The batch job has been failing since May 12.
- The cause of the failure is a schema mismatch from the May 9 deploy.
- Staging patch target is May 23; production target is May 26.
- Claudette's sign-off on the interim SLA is needed by May 21.
- The steering group must decide between June 3 and June 10 Phase 2 kickoff.
- The risk of proceeding June 3 without a clean fix involves the customer-facing analytics portal.

#### must_not_claim
- Do not say the data freshness target of 98% has been met.
- Do not say the May 26 production fix is already confirmed clean.
- Do not blame Riko individually for the failure.
- Do not promise June 3 Phase 2 will proceed.

#### rewrite_quality_targets
The rewrite should deliver a clear executive briefing: status first, gap and root cause second, two explicit asks with deadlines, and the risk of inaction stated plainly without alarm.

#### expected_rewrite_challenges
A model may soften the 94.2% shortfall, drop one of the two leadership asks, or make the patch deployment sound like a completed fix.

### Case 074 - emotionally loaded parent draft
- id: rewrite-draft-074
- category: teacher_parent
- source_type: email
- tone_preset: warm
- risk_tags: teacher, sensitive
- input_word_count_band: 320-400

#### input_draft
Dear Ingrid, I am emailing you because what happened on May 15 in the classroom is something I feel I need to address directly, and honestly I have been putting this off because I was not sure how to word it. During independent reading time on May 15, Soren was observed pushing another student's book off the desk and when I asked him to return to his seat he said he did not care and turned away. I want to be very clear that I am not labeling Soren or suggesting there is something wrong with him. What I am doing is telling you what I observed so we can figure out together what support looks like going forward. Since May 15 I have checked in with Soren every morning during arrival and he has been calm and engaged in those moments. We also have a school counselor, Ms. Vega, who works with students in exactly these situations, and I have already put Soren's name on her list for a voluntary check-in, which is not a disciplinary step. I would really like to meet with you so we can talk through this properly. I have availability on May 22 after 3:30 p.m. or May 24 during the 9 to 10 a.m. window. Please let me know which works and we can confirm by end of day May 21.

#### what_actually_happened
The teacher is emailing Ingrid about an incident involving her son Soren on May 15. Soren pushed another student's book off the desk during independent reading and said he did not care when asked to return to his seat. Since then the teacher has done daily morning check-ins with Soren and added him to school counselor Ms. Vega's voluntary check-in list. Meeting options are May 22 after 3:30 p.m. or May 24 from 9 to 10 a.m., with a response requested by May 21.

#### must_keep
- The parent is Ingrid.
- The student is Soren.
- The incident occurred on May 15 during independent reading time.
- Soren pushed another student's book off the desk.
- Soren said he did not care when asked to return to his seat.
- The teacher is not labeling Soren or diagnosing him.
- Since May 15 the teacher has done daily morning check-ins with Soren.
- Ms. Vega is the school counselor.
- Soren's name was added to Ms. Vega's voluntary check-in list.
- The check-in with Ms. Vega is not a disciplinary step.
- Meeting options are May 22 after 3:30 p.m. or May 24 from 9 to 10 a.m.
- Ingrid should confirm by end of day May 21.

#### must_not_claim
- Do not diagnose or label Soren behaviorally.
- Do not say the check-in with Ms. Vega is disciplinary.
- Do not extend the May 21 confirmation deadline.
- Do not blame Ingrid or suggest parenting issues.

#### rewrite_quality_targets
The rewrite should feel calm, caring, and factual — reporting what happened without judgment, clearly describing the support steps already taken, and making the meeting ask easy to respond to.

#### expected_rewrite_challenges
A model may strip the specific behavioral observations to be kinder, or soften the counselor referral so much that it sounds like there is no concern at all.

### Case 075 - too-pushy trial follow-up
- id: rewrite-draft-075
- category: sales_followup
- source_type: email
- tone_preset: warm
- risk_tags: sales, tone
- input_word_count_band: 320-400

#### input_draft
Hi Svetlana, I wanted to follow up again on the Fieldstone trial that started on May 5 and runs through May 19. I know I already sent a note on May 12, but I have not heard back and honestly I am a little worried you are going to let the trial expire without us connecting. I really think this is the right fit for the Fieldstone workflow and I do not want you to miss the window. The full platform pricing after trial is $780 per month for the 15-seat tier you have been using. I know that might feel like a jump but if you look at what the platform has done for your data export time alone, I genuinely think it pays for itself. I can also get approval to extend the trial by five days to May 24 if you need more time to evaluate, but I need to know by May 18 to put in the request. The other thing I wanted to flag is that the May 19 expiry means your team's data will be in read-only mode for 30 days after that before permanent deletion on June 18. I just want to make sure you have everything you need to make a good decision here, and I am happy to jump on a call anytime this week or next Monday, May 20.

#### what_actually_happened
Svetlana's company Fieldstone has been on a trial since May 5 that ends May 19. The sales contact sent a previous follow-up on May 12. Full platform pricing after trial is $780 per month for 15 seats. A five-day trial extension to May 24 is possible if Svetlana requests it by May 18. After May 19, data goes read-only for 30 days and is permanently deleted on June 18. Call availability is any time this week or Monday May 20.

#### must_keep
- The prospect is Svetlana.
- The company is Fieldstone.
- The trial started on May 5.
- The trial ends on May 19.
- A previous follow-up was sent on May 12.
- Full platform pricing after trial is $780 per month for the 15-seat tier.
- A five-day trial extension to May 24 is available if requested by May 18.
- After May 19, data goes into read-only mode for 30 days.
- Data is permanently deleted on June 18.
- Call availability includes any time this week or Monday May 20.

#### must_not_claim
- Do not pressure Svetlana by implying she is making a mistake.
- Do not reduce the $780 per month price.
- Do not extend the trial beyond May 24 or past the May 18 extension request deadline.
- Do not imply the data will be recoverable after June 18.

#### rewrite_quality_targets
The rewrite should turn the pushy follow-up into a calm, informative note that states the facts clearly — pricing, extension option, data timeline — and invites a reply without urgency language.

#### expected_rewrite_challenges
A model may preserve the pushy tone by keeping phrases like "I am worried" or "I do not want you to miss," or may drop the June 18 deletion date to sound less alarming.

### Case 076 - complex referral and insurance admin
- id: rewrite-draft-076
- category: medical_admin
- source_type: email
- tone_preset: warm
- risk_tags: medical_admin, complex
- input_word_count_band: 320-400

#### input_draft
Hi Cassandra, I am following up on the referral for your appointment with Dr. Osei at Lakewood Specialist Group. The referral was submitted by our office on May 8 under authorization number AUTH-30291. I checked this morning and the insurance plan — Horizon Select PPO — has not responded with an approval or denial as of today, May 20. I want to be upfront that I am an admin coordinator, not a clinician, so I cannot tell you what the referral findings will mean for your care or whether the specialist will recommend any particular treatment. What I can tell you is the current status and next steps. Because the review has now been open for twelve days and the plan's standard window is ten business days, this qualifies for an expedited review request, which our office filed today, May 20, at 11:30 a.m. Insurance typically responds to expedited requests within 72 hours, so we would expect a status update by May 23. If you do not hear from us by May 23, please call our office coordinator line at 503-555-0188 between 9 a.m. and 4 p.m. If you have a new or urgent symptom related to the reason for referral, please do not wait — call the urgent line at 503-555-0199 or go to your nearest urgent care center. Please do not send insurance documents directly to this email thread; fax them to 503-555-0177.

#### what_actually_happened
Cassandra has a referral to Dr. Osei at Lakewood Specialist Group. The referral was submitted on May 8 under authorization number AUTH-30291. As of May 20 the insurance plan, Horizon Select PPO, has not responded. The review has been open twelve days, exceeding the ten business day standard window. The office filed an expedited review request on May 20 at 11:30 a.m. Insurance typically responds within 72 hours, so a status update is expected by May 23. Cassandra should call the coordinator line at 503-555-0188 if she has not heard by May 23. Urgent symptoms should go to 503-555-0199 or urgent care. Documents should be faxed to 503-555-0177.

#### must_keep
- The patient is Cassandra.
- The specialist is Dr. Osei at Lakewood Specialist Group.
- The referral was submitted on May 8.
- The authorization number is AUTH-30291.
- The insurance plan is Horizon Select PPO.
- As of May 20, no approval or denial has been received.
- The review has been open for twelve days, which exceeds the ten business day standard window.
- The office filed an expedited review request on May 20 at 11:30 a.m.
- A status update is expected by May 23.
- The office coordinator line is 503-555-0188, available 9 a.m. to 4 p.m.
- The urgent line for new or urgent symptoms is 503-555-0199.
- Documents should be faxed to 503-555-0177.

#### must_not_claim
- Do not interpret what the referral or specialist visit means for treatment.
- Do not guarantee insurance approval by May 23.
- Do not advise Cassandra to wait on urgent symptoms.
- Do not promise the expedited request will be approved.

#### rewrite_quality_targets
The rewrite should be organized and calm, guiding Cassandra through the current status, what the office has done, when to expect an update, and what to do in urgent situations — all without crossing into clinical advice.

#### expected_rewrite_challenges
A model may collapse the two phone numbers into one, drop the fax instruction, or soften the urgent-symptoms path so it becomes ambiguous.

### Case 077 - tenant deposit dispute
- id: rewrite-draft-077
- category: property_logistics
- source_type: email
- tone_preset: warm
- risk_tags: property, dispute
- input_word_count_band: 320-400

#### input_draft
Hi Wendell, I am writing about the deductions listed in the move-out statement for Unit 12C. The lease for Unit 12C ended on April 30 and the move-out inspection was completed on May 2. The inspection report, reference INS-12C-2024, noted three items: carpet staining in the bedroom beyond normal wear and tear ($220.00), a broken towel bar in the main bathroom ($85.00), and a missing rear screen door ($310.00). These three deductions total $615.00 and have been applied against the $1,200.00 security deposit, leaving a balance of $585.00 that will be returned to the address on file. I understand you may disagree with these findings, and I want to be clear about the process. If you want to dispute any of these charges you have 14 days from the date of this letter, May 12, which means your written dispute must reach us no later than May 26. Please send any written dispute to propertyops@example.org and include the inspection report reference INS-12C-2024 so we can match your response to the correct file. If we do not receive a dispute by May 26, we will mail the $585.00 balance check using the address on file.

#### what_actually_happened
Wendell's lease for Unit 12C ended April 30. The move-out inspection was completed May 2 under reference INS-12C-2024. Three deductions were noted: carpet staining at $220.00, broken towel bar at $85.00, and missing rear screen door at $310.00, totaling $615.00. The security deposit is $1,200.00, leaving a $585.00 balance to be returned. If Wendell disputes the charges, a written dispute must reach the property by May 26 at propertyops@example.org, referencing INS-12C-2024. Without a dispute by May 26, the $585.00 check will be mailed.

#### must_keep
- The tenant is Wendell.
- The unit is Unit 12C.
- The lease ended on April 30.
- The inspection report reference is INS-12C-2024.
- The carpet deduction is $220.00.
- The towel bar deduction is $85.00.
- The screen door deduction is $310.00.
- Total deductions are $615.00.
- The security deposit is $1,200.00.
- The balance to be returned is $585.00.
- The dispute deadline is May 26.
- Written disputes should be sent to propertyops@example.org with reference INS-12C-2024.

#### must_not_claim
- Do not waive or reduce any of the three deductions.
- Do not blame Wendell for the damage.
- Do not extend the May 26 dispute deadline.
- Do not promise the balance will be returned before May 26.

#### rewrite_quality_targets
The rewrite should state the deductions and balances clearly, explain the dispute process warmly and plainly, and make the deadline and contact method easy to act on.

#### expected_rewrite_challenges
A model may soften the deduction amounts or merge the dispute deadline into the refund timeline, making it unclear when Wendell must actually respond.

### Case 078 - panel reschedule with time options
- id: rewrite-draft-078
- category: hr_recruiting
- source_type: email
- tone_preset: warm
- risk_tags: recruiting, scheduling
- input_word_count_band: 320-400

#### input_draft
Hi Valentina, I am sorry to say we need to reschedule the panel interview that was set for Thursday, May 22 at 1:00 p.m. PT for the Senior Product Analyst role. One of our panelists, Kwame, had an unexpected conflict come up this morning, and we cannot hold the panel with only two of the three panelists. I have checked with all three panelists and pulled together the slots where everyone is free. The available options are: Tuesday, May 27 at 10:00 a.m. PT, Tuesday, May 27 at 3:00 p.m. PT, or Thursday, May 29 at 11:00 a.m. PT. Each slot is 75 minutes long. The panel still includes Kwame, Daria, and Roland. I want to make sure we give you enough notice, so the updated video link for whichever slot you choose will be REK-PANEL-447; the link stays the same regardless of which time you pick. Could you please let me know your preference by Friday, May 23 at noon PT so we have time to send the updated calendar invite before the weekend? If none of these times work for you, please reply with your general availability and we will do our best to find another option, but I cannot guarantee the same three panelists will be available for a date further out.

#### what_actually_happened
Valentina's panel interview for Senior Product Analyst, originally scheduled for May 22 at 1:00 p.m. PT, needs to be rescheduled because panelist Kwame has a conflict. The new options are May 27 at 10:00 a.m. PT, May 27 at 3:00 p.m. PT, or May 29 at 11:00 a.m. PT. Each slot is 75 minutes. The panel is Kwame, Daria, and Roland. The video link is REK-PANEL-447. Valentina must respond by May 23 at noon PT. If none of the times work, the same three panelists cannot be guaranteed for later dates.

#### must_keep
- The candidate is Valentina.
- The role is Senior Product Analyst.
- The original interview was May 22 at 1:00 p.m. PT.
- The rescheduling reason is a conflict for panelist Kwame.
- The three panelists are Kwame, Daria, and Roland.
- Available slots are May 27 at 10:00 a.m. PT, May 27 at 3:00 p.m. PT, and May 29 at 11:00 a.m. PT.
- Each slot is 75 minutes.
- The video link is REK-PANEL-447.
- Valentina must respond by May 23 at noon PT.
- The same three panelists cannot be guaranteed for dates further out.

#### must_not_claim
- Do not guarantee the same panelists for any date not listed.
- Do not change the May 23 noon PT response deadline.
- Do not offer any additional time slots beyond the three listed.
- Do not say Kwame's conflict reflects negatively on the process.

#### rewrite_quality_targets
The rewrite should be warm and apologetic for the reschedule while keeping all three options, the video link, the 75-minute duration, and the response deadline clearly laid out.

#### expected_rewrite_challenges
A model may drop one of the three time options or merge the video-link detail into a generic placeholder, losing a key scheduling anchor.

### Case 079 - donor grant progress update
- id: rewrite-draft-079
- category: nonprofit_community
- source_type: email
- tone_preset: warm
- risk_tags: nonprofit, donor
- input_word_count_band: 320-400

#### input_draft
Dear Rosalind, I wanted to give you an update on how the $15,000 grant from the Westbridge Foundation is being used, because we promised a progress note by May 31 in the grant agreement and I want to make sure we deliver that on time. The grant, reference WB-G-2024-09, was received on February 1 and has been designated entirely for the Youth Digital Literacy Program running from February through July 2024. As of May 20 we have spent $8,740 of the $15,000. The funds have gone toward three main areas: $4,200 for facilitator stipends across the spring cohort (16 participants), $3,100 for device loans and repair for students without home equipment, and $1,440 for curriculum licensing through August. The remaining $6,260 will be used for the summer cohort starting June 16 and for the final evaluation report. I want to be honest that we will not have the full program outcomes data until the evaluation is complete in early August, so this letter is an interim progress note rather than a final impact report. We expect to deliver the final report to the Westbridge Foundation by August 22. If you or anyone at the Foundation has questions before then, please reach out to our program coordinator Federica at programs@example.org.

#### what_actually_happened
The Westbridge Foundation grant of $15,000, reference WB-G-2024-09, was received February 1 and designated for the Youth Digital Literacy Program running February through July 2024. As of May 20, $8,740 has been spent: $4,200 on facilitator stipends for 16 spring cohort participants, $3,100 on device loans and repair, and $1,440 on curriculum licensing. The remaining $6,260 is earmarked for the summer cohort starting June 16 and for the final evaluation report. Full outcomes data will not be available until early August. The final report is expected by August 22. Contact is Federica at programs@example.org.

#### must_keep
- The donor is Rosalind.
- The funder is Westbridge Foundation.
- The grant amount is $15,000.
- The grant reference is WB-G-2024-09.
- The grant was received on February 1.
- The program is the Youth Digital Literacy Program, running February through July 2024.
- As of May 20, $8,740 has been spent.
- Facilitator stipends total $4,200 for 16 spring cohort participants.
- The remaining $6,260 is for the summer cohort starting June 16 and the final evaluation report.
- Full program outcomes data will not be available until early August.
- The final report is expected by August 22.
- Program coordinator Federica can be reached at programs@example.org.

#### must_not_claim
- Do not promise final outcome data before early August.
- Do not present this letter as the final impact report.
- Do not say the full $15,000 has been spent.
- Do not promise the summer cohort has already launched.

#### rewrite_quality_targets
The rewrite should read like a credible, warm interim donor update that is honest about what is known, what is still in progress, and when the final report will arrive.

#### expected_rewrite_challenges
A model may try to sound more impressive by inflating the spending figures, implying outcomes data is already available, or omitting the honest caveat that this is an interim rather than final report.

### Case 080 - enterprise feature request boundary
- id: rewrite-draft-080
- category: customer_success
- source_type: email
- tone_preset: warm
- risk_tags: customer_success, enterprise
- input_word_count_band: 320-400

#### input_draft
Hi Octavia, thank you for the call on May 16 and for sending the detailed feature request summary for Kessler Group. I want to make sure I give you an honest response rather than one that just sounds good and ends up creating misaligned expectations. The two features your team outlined — bulk role assignment via CSV and a dedicated audit log export API — are both on the product roadmap, but they are not committed to a specific release date. I cannot tell you they will be ready by your contract renewal on September 1 because our product team has not confirmed a timeline yet. What I can tell you is that Kessler Group's contract, account ID KG-00412, runs through August 31 and the renewal at the current rate is $3,400 per month for the 40-seat Enterprise plan. If either feature lands before renewal, I will notify you immediately. What I can do today is submit a formal feature priority request on your behalf so that Kessler Group's use case is documented in the product backlog — I did this on May 17, reference FPR-2241. I can also connect you with our enterprise support team for a more detailed roadmap conversation. If you want that conversation, please reply and I will set up an intro by the end of this week. I just cannot make a commitment that custom development or a discounted renewal will happen to close this gap.

#### what_actually_happened
Octavia had a call on May 16 to discuss feature requests for Kessler Group. The two features requested — bulk role assignment via CSV and a dedicated audit log export API — are on the product roadmap but without a confirmed release date. The account ID is KG-00412, on an Enterprise plan for 40 seats at $3,400 per month, running through August 31. A formal feature priority request was submitted on May 17, reference FPR-2241. No commitment can be made that the features will be ready by the September 1 renewal. Enterprise support can provide a roadmap conversation if requested.

#### must_keep
- The customer is Octavia.
- The company is Kessler Group.
- The call was on May 16.
- The two requested features are bulk role assignment via CSV and a dedicated audit log export API.
- Both features are on the roadmap but without a confirmed release date.
- The account ID is KG-00412.
- The contract runs through August 31.
- The renewal rate is $3,400 per month for 40-seat Enterprise plan.
- A feature priority request was submitted on May 17, reference FPR-2241.
- The sender cannot commit the features will be ready by the September 1 renewal.
- An enterprise support roadmap conversation is available upon request.

#### must_not_claim
- Do not promise either feature will be delivered before the September 1 renewal.
- Do not offer a discount on the renewal.
- Do not say custom development will close the feature gap.
- Do not promise a specific roadmap date for either feature.

#### rewrite_quality_targets
The rewrite should be direct and warm, setting clear expectations about what is on the roadmap versus what is committed, explaining what has already been done (the FPR submission), and offering a concrete next step without over-promising.

#### expected_rewrite_challenges
A model may try to sound accommodating by implying the features are coming soon or by hinting that a renewal discount is possible to compensate for the gap.

### Case 081 - damaged order replacement boundary
- id: rewrite-draft-081
- category: customer_support
- source_type: email
- tone_preset: warm
- risk_tags: holdout, support
- input_word_count_band: 160-280

#### input_draft
Hi Bridget, I looked into order ORD-55203 this morning. The order was placed on June 3 and was marked delivered on June 8 to your shipping address on file. You reported that the ceramic serving platter arrived cracked along the rim. I can see from the photo you attached — thank you for sending that — that the crack runs about halfway across the top edge. Based on that photo I can process a one-time replacement for the cracked platter only. I cannot issue a refund for the full order because the other three items in the order arrived without any reported damage. The replacement platter will ship to the same address we have on file for ORD-55203. If that address is no longer correct, please reply with the updated address by June 13 so I can update the shipment before it goes out. I cannot hold the replacement order open past June 13 because our warehouse batch runs that evening.

#### what_actually_happened
Bridget reported that the ceramic serving platter from order ORD-55203, placed June 3 and delivered June 8, arrived cracked along the rim. She sent a photo showing the crack runs about halfway across the top edge. The support agent can issue a one-time replacement for the platter only. Three other items in the order arrived without reported damage, so a full order refund is not available. The replacement will ship to the address on file for ORD-55203 unless Bridget provides an update by June 13.

#### must_keep
- The customer is Bridget.
- The order identifier is ORD-55203.
- The order was placed on June 3.
- The order was delivered on June 8.
- The damaged item is the ceramic serving platter.
- The crack runs about halfway across the top edge.
- A photo was provided by the customer.
- The replacement covers the cracked platter only.
- Three other items arrived without reported damage.
- A full order refund is not available.
- The address update deadline is June 13.
- The warehouse batch runs on the evening of June 13.

#### must_not_claim
- Do not promise a full order refund.
- Do not replace any items other than the cracked platter.
- Do not ship before the address is confirmed if the customer indicates the address has changed.
- Do not extend the June 13 address update deadline.

#### rewrite_quality_targets
A good rewrite is warm and appreciative of the photo, makes the one-replacement limit and the refund boundary clear without sounding punitive, and surfaces the June 13 address deadline prominently so the customer doesn't miss it.

#### expected_rewrite_challenges
The model may try to sound generous by offering a full refund or by glossing over the one-replacement limit, or it may omit the June 13 deadline when restructuring.

### Case 082 - partial credit no waiver
- id: rewrite-draft-082
- category: billing_support
- source_type: email
- tone_preset: warm
- risk_tags: holdout, billing
- input_word_count_band: 160-280

#### input_draft
Hi Clarence, I reviewed invoice INV-20741 from your account. The invoice was generated on July 1 for $224.00 and covers the July 1 to July 31 billing period on the Team Plus plan. I can see you downgraded two seats on July 11. Our policy allows a prorated credit for seats removed mid-cycle, which works out to $28.00 for the two seats over the remaining 20 days. I have applied that $28.00 credit to your account and it will appear against invoice INV-20741. I cannot waive the remaining $196.00 balance because the plan was active and the full workspace was in use up to the point of the seat change. If you believe the calculation is off, please reply with the seat names and removal dates and I will review again. Payment of the $196.00 balance is due by July 28.

#### what_actually_happened
Clarence's account was charged $224.00 on invoice INV-20741 dated July 1, covering July 1-31 on the Team Plus plan. Two seats were downgraded on July 11. The prorated credit for the two seats over 20 remaining days is $28.00. The agent applied the $28.00 credit to INV-20741. The remaining balance of $196.00 cannot be waived because the workspace was in active use. Payment of $196.00 is due by July 28.

#### must_keep
- The customer is Clarence.
- The invoice identifier is INV-20741.
- The invoice was generated on July 1.
- The invoice amount is $224.00.
- The billing period is July 1 to July 31.
- The plan is Team Plus.
- Two seats were downgraded on July 11.
- The prorated credit is $28.00.
- The $28.00 credit has been applied to INV-20741.
- The remaining balance is $196.00.
- The $196.00 balance cannot be waived.
- Payment of $196.00 is due by July 28.

#### must_not_claim
- Do not waive the $196.00 remaining balance.
- Do not change the $28.00 credit amount.
- Do not promise additional credits without reviewing Clarence's supplied data.
- Do not extend the July 28 payment deadline.

#### rewrite_quality_targets
A good rewrite explains the credit calculation with clarity and warmth, keeps the no-waiver policy boundary firm but respectful, and makes the July 28 payment date unmistakable.

#### expected_rewrite_challenges
The model may round or alter the $28.00 credit, imply the full balance could be waived on request, or bury the July 28 payment deadline.

### Case 083 - blocked sprint delivery
- id: rewrite-draft-083
- category: workplace_update
- source_type: message
- tone_preset: warm
- risk_tags: holdout, workplace
- input_word_count_band: 160-280

#### input_draft
Update on Falcon reporting module: the data export task and the permission-group filter are both done and merged. The last piece — the CSV download endpoint — is blocked because the staging environment is returning a 502 on all authenticated requests since the infrastructure team pushed a cert update at 7:45 a.m. today. Tobias owns that environment and he is already aware and working on it. Without staging access I cannot run the integration test suite, which is a hard gate before we ship the endpoint. Current risk: if staging is not restored by 3 p.m. today, the CSV endpoint misses the June 20 sprint deadline and rolls to the next sprint. I need a call or Slack message from Tobias with an ETA by noon so I can update the project board. I will post a status update at 3:15 p.m. regardless.

#### what_actually_happened
The data export task and permission-group filter for the Falcon reporting module are merged. The CSV download endpoint is blocked because staging has returned a 502 error since a cert update at 7:45 a.m. today. Tobias owns the staging environment and is working on the issue. Integration tests cannot run without staging access, which is a hard release gate. If staging is not restored by 3 p.m., the CSV endpoint will miss the June 20 sprint deadline and roll to the next sprint. An ETA from Tobias is needed by noon.

#### must_keep
- The project is the Falcon reporting module.
- The data export task is done and merged.
- The permission-group filter is done and merged.
- The CSV download endpoint is blocked.
- Staging has returned a 502 error since 7:45 a.m. today.
- The cert update caused the staging issue.
- Tobias owns the staging environment.
- Tobias is already aware and working on the issue.
- Integration tests cannot run without staging — that is a hard release gate.
- If staging is not restored by 3 p.m., the CSV endpoint misses the June 20 sprint deadline.
- An ETA from Tobias is needed by noon.
- A status update will be posted at 3:15 p.m.

#### must_not_claim
- Do not say the CSV endpoint is complete or shipped.
- Do not blame Tobias for the staging failure.
- Do not promise staging will be restored by 3 p.m.
- Do not remove the June 20 deadline risk.

#### rewrite_quality_targets
A good rewrite is concise and actionable, surfaces the noon ETA request and the 3 p.m. risk window clearly, and keeps the tone collaborative rather than frustrated.

#### expected_rewrite_challenges
The model may collapse the conditional risk into a firm statement ("we will miss the deadline") or drop the noon ETA ask in favor of a vague "please update me."

### Case 084 - classroom concern without diagnosis
- id: rewrite-draft-084
- category: teacher_parent
- source_type: email
- tone_preset: warm
- risk_tags: holdout, teacher
- input_word_count_band: 160-280

#### input_draft
Hi Felicity, I wanted to reach out about something I have been noticing with Kieran over the past two weeks. On May 14 and again on May 19, Kieran had difficulty staying at his table during group work — he left the group three times each session and spent time near the window instead. I am not drawing any conclusions from this and I am definitely not qualified to make any kind of assessment. What I can say is that the pattern is new compared to how he was doing earlier in the semester. I have already spoken with our school counselor, Ms. Hartley, who suggested we meet together before the end of May. I would like to set up a 20-minute meeting this week or next — May 22, 23, or the week of May 26 all work for me. No action is required from Kieran before we meet. Please just reply with a time that works.

#### what_actually_happened
The teacher noticed that Kieran left his group work table three times on May 14 and three times again on May 19, spending time near the window instead. This is a new pattern compared to earlier in the semester. The teacher has spoken with school counselor Ms. Hartley, who recommended a meeting before the end of May. The teacher is offering May 22, 23, or the week of May 26 for a 20-minute meeting. No action is required from Kieran before the meeting.

#### must_keep
- The recipient is Felicity.
- The student is Kieran.
- The observed dates are May 14 and May 19.
- Kieran left his group work table three times each session.
- He spent time near the window.
- The pattern is new compared to earlier in the semester.
- The teacher is not making an assessment or diagnosis.
- The school counselor is Ms. Hartley.
- Ms. Hartley suggested a meeting before the end of May.
- The teacher is offering May 22, May 23, or the week of May 26.
- The meeting would be 20 minutes.
- No action is required from Kieran before the meeting.

#### must_not_claim
- Do not diagnose or suggest a condition for Kieran.
- Do not say Kieran is disruptive or a behavioral problem.
- Do not promise the meeting will result in a formal support plan.
- Do not state that the pattern will definitely continue.

#### rewrite_quality_targets
A good rewrite is warm and non-alarming while being honest about what was observed. It should invite the parent into a collaborative conversation without implying a negative label for Kieran.

#### expected_rewrite_challenges
The model may soften the observation so much that the specific dates and counts disappear, or may slip in diagnostic-sounding language ("attention difficulty") in an effort to seem helpful.

### Case 085 - quote expiry no discount
- id: rewrite-draft-085
- category: sales_followup
- source_type: email
- tone_preset: warm
- risk_tags: holdout, sales
- input_word_count_band: 160-280

#### input_draft
Hi Renata, following up on our conversation last week about the Meridian account. I attached quote Q-8834 for easy reference — it is for 30 seats at $55 per seat per month, which includes the dedicated onboarding package, SSO configuration, and priority email support. The quote is valid until July 15. I understand you mentioned wanting a lower per-seat rate, but I am not able to adjust pricing on this quote without going back through our approvals process, which would also reset the expiry. If the current scope and pricing work for Meridian, the fastest path is to confirm by July 15 and I can send the order form same day. If you need a different configuration I am happy to start a revised quote — just flag the changes you need and I will get back to you within two business days. Looking forward to hearing from you.

#### what_actually_happened
The sales contact is Renata and the account is Meridian. Quote Q-8834 is for 30 seats at $55 per seat per month, including the dedicated onboarding package, SSO configuration, and priority email support. The quote expires July 15. The prospect asked for a lower per-seat rate, which requires a new approvals process and would reset the quote expiry. If confirmed by July 15, the order form can go out the same day. A revised quote can be started and returned within two business days.

#### must_keep
- The contact is Renata.
- The account is Meridian.
- The quote identifier is Q-8834.
- The quote is for 30 seats.
- The price is $55 per seat per month.
- The quote includes the dedicated onboarding package.
- The quote includes SSO configuration.
- The quote expires July 15.
- A lower per-seat rate requires a new approvals process.
- A new approvals process would reset the expiry date.
- If confirmed by July 15, the order form goes out the same day.
- A revised quote can be returned within two business days.

#### must_not_claim
- Do not offer a discount or lower per-seat rate on the current quote.
- Do not extend the July 15 expiry date.
- Do not say the approvals process is fast or guaranteed.
- Do not pressure Renata to decide immediately.

#### rewrite_quality_targets
A good rewrite maintains a warm and professional relationship tone, keeps the quote details precise, and presents the two clear paths (confirm or revise) without applying pressure or implying a discount is coming.

#### expected_rewrite_challenges
The model may try to sound flexible by hinting a discount is possible, or it may drop the scope inclusions in an effort to sound concise.

### Case 086 - portal message and callback boundary
- id: rewrite-draft-086
- category: medical_admin
- source_type: message
- tone_preset: warm
- risk_tags: holdout, medical_admin
- input_word_count_band: 160-280

#### input_draft
Hi Josephine, I received your portal message about the referral for Ivan's cardiology appointment. I checked Ivan's chart this morning and I can confirm that Dr. Okafor submitted the referral on June 2. The referral is now with the cardiology office — we sent it electronically on June 3 — but the appointment scheduling is handled entirely by their office, not ours. I am not a clinician so I cannot advise on the referral priority or medical urgency. What I can do is flag Ivan's file for a callback from our care coordinator, Simone, who can speak to the referral status more fully. Simone's callback window is Tuesday and Thursday between 10 a.m. and 2 p.m. If Ivan experiences any chest pain, shortness of breath, or other urgent symptoms before the appointment, please call 911 or go to the nearest emergency department rather than waiting for a callback.

#### what_actually_happened
Josephine sent a portal message about Ivan's cardiology referral. The practice confirmed Dr. Okafor submitted the referral on June 2, and it was sent electronically to the cardiology office on June 3. Appointment scheduling is handled by the cardiology office. The sender is not a clinician and cannot advise on referral priority or urgency. Care coordinator Simone can give more information; her callback window is Tuesday and Thursday, 10 a.m.-2 p.m. Urgent symptoms require 911 or the emergency department, not a callback.

#### must_keep
- The recipient is Josephine.
- The patient is Ivan.
- The referral is for a cardiology appointment.
- Dr. Okafor submitted the referral on June 2.
- The referral was sent electronically to the cardiology office on June 3.
- Appointment scheduling is handled by the cardiology office.
- The sender is not a clinician.
- The sender cannot advise on referral priority or medical urgency.
- The care coordinator is Simone.
- Simone's callback window is Tuesday and Thursday between 10 a.m. and 2 p.m.
- Urgent symptoms (chest pain, shortness of breath) require 911 or the emergency department.

#### must_not_claim
- Do not advise on referral priority or medical urgency.
- Do not promise a cardiology appointment date.
- Do not say Simone will call outside her Tuesday-Thursday 10 a.m.-2 p.m. window.
- Do not tell Josephine to wait for a callback if Ivan has urgent symptoms.

#### rewrite_quality_targets
A good rewrite is calm and reassuring, clearly separates what the practice has done from what the cardiology office controls, and makes the urgent symptom instruction impossible to miss.

#### expected_rewrite_challenges
The model may bury the urgent symptom instruction in a subordinate clause, or may imply the referral priority is fine in an attempt to be reassuring.

### Case 087 - roof access and no rent credit
- id: rewrite-draft-087
- category: property_logistics
- source_type: email
- tone_preset: warm
- risk_tags: holdout, property
- input_word_count_band: 160-280

#### input_draft
Hi Weston, following up on the roof drainage issue reported for Unit 9C. The property manager sent a roofing vendor out on August 5 to assess the blockage. The vendor confirmed a downspout clog and said the full clearing will require scaffolding access on August 12 between 8 a.m. and 1 p.m. You or an authorized adult must be present during that window, or alternatively you can email our office to authorize unescorted access in writing before August 10. The vendor will take before-and-after photos of the drainage point and the ceiling area below. I know the water staining on the ceiling has been frustrating. I cannot approve a rent credit from this maintenance ticket — that request has to go through the formal dispute process using the PDF form on our tenant portal. Please confirm access by August 10 so I can lock in the vendor booking.

#### what_actually_happened
Unit 9C has a roof drainage issue reported to the property manager. A roofing vendor assessed the blockage on August 5 and confirmed a downspout clog. The full clearing requires scaffolding access on August 12 between 8 a.m. and 1 p.m. Weston or an authorized adult must be present, or written authorization for unescorted access must be emailed before August 10. The vendor will photograph the drainage point and ceiling. A rent credit is not available from the maintenance ticket; the formal dispute process on the tenant portal applies. Access must be confirmed by August 10.

#### must_keep
- The recipient is Weston.
- The unit is 9C.
- The issue is a roof drainage blockage.
- A vendor assessed the issue on August 5.
- The vendor confirmed a downspout clog.
- The full clearing requires scaffolding access on August 12 between 8 a.m. and 1 p.m.
- Weston or an authorized adult must be present during the access window.
- Written authorization for unescorted access must be sent before August 10.
- The vendor will take before-and-after photos of the drainage point and ceiling area.
- A rent credit is not available from the maintenance ticket.
- The rent credit request must go through the formal dispute process on the tenant portal.
- Access must be confirmed by August 10.

#### must_not_claim
- Do not approve a rent credit from this maintenance ticket.
- Do not confirm unescorted access without written authorization from Weston.
- Do not promise the clearing will fully resolve any interior ceiling damage.
- Do not extend the August 10 authorization deadline.

#### rewrite_quality_targets
A good rewrite walks Weston through the access options and the photo process warmly, keeps the rent credit boundary clear without sounding dismissive, and makes August 10 stand out as the action deadline.

#### expected_rewrite_challenges
The model may soften the rent credit boundary so much it implies credit is possible through the maintenance thread, or may conflate the August 10 authorization date with the August 12 access date.

### Case 088 - rejection without false feedback
- id: rewrite-draft-088
- category: hr_recruiting
- source_type: email
- tone_preset: warm
- risk_tags: holdout, recruiting
- input_word_count_band: 160-280

#### input_draft
Hi Svetlana, thank you for interviewing for the Content Operations Manager role on July 22. Our panel appreciated the time you spent with us. After reviewing all candidates this week, we have decided to move forward with a different applicant for this position. This was a genuinely competitive process and the decision was close. I am not able to share panel-level feedback through this email — individual feedback is only available through our formal feedback request process, which you can find on the careers page. We do keep candidate files active for twelve months, so if a role that matches your background opens in that time we may be in touch. You are also welcome to apply directly to future postings. Please feel free to reach out to me at staffing@example.org if you have any questions about our careers process.

#### what_actually_happened
Svetlana interviewed for the Content Operations Manager role on July 22. The hiring team reviewed all candidates and decided to move forward with a different applicant. The decision was described as close and competitive. Panel-level feedback is not shared through this email; it requires the formal feedback request process on the careers page. The candidate file will remain active for twelve months. Svetlana is welcome to apply to future postings and can contact staffing@example.org with questions.

#### must_keep
- The candidate is Svetlana.
- The role is Content Operations Manager.
- The interview date was July 22.
- The hiring team is moving forward with a different applicant.
- The process was competitive and the decision was close.
- Panel-level feedback is not available through this email.
- Feedback requires the formal feedback request process on the careers page.
- The candidate file remains active for twelve months.
- Svetlana is welcome to apply to future postings.
- The contact email is staffing@example.org.

#### must_not_claim
- Do not give a specific reason why Svetlana was not selected.
- Do not promise Svetlana will be contacted for a future role.
- Do not imply the panel identified a particular weakness.
- Do not share panel-level feedback in the body of the email.

#### rewrite_quality_targets
A good rewrite is warm and respectful, delivers the rejection plainly without softening it into ambiguity, and explains the feedback path clearly without suggesting private feedback will be given in this message.

#### expected_rewrite_challenges
The model may try to soften the rejection by implying Svetlana will likely be contacted again soon, or may accidentally include a reason for the decision that functions as implied panel feedback.

### Case 089 - community fundraiser announcement
- id: rewrite-draft-089
- category: nonprofit_community
- source_type: announcement
- tone_preset: warm
- risk_tags: holdout, nonprofit
- input_word_count_band: 160-280

#### input_draft
Hello neighbors, just a reminder about our annual Riverside Community Fundraiser on Saturday, September 13 at Elm Grove Park starting at 10 a.m. The event closes at 3 p.m. Bring items to donate to the silent auction — we are accepting goods valued between $10 and $75 per item; please no perishables and no items above $75 since our insurance cap limits high-value items. Volunteer check-in is at 9 a.m. at the north pavilion. If you signed up to volunteer, please confirm your arrival by September 10 by emailing Gretchen at events@example.org. We will have activities for kids and light refreshments. If weather is severe on the morning of September 13, the event will shift to the Riverside Community Center on Maple Avenue instead. Updates will go to the email list by 7 a.m. that day. See you there!

#### what_actually_happened
The Riverside Community Fundraiser is scheduled for Saturday, September 13 at Elm Grove Park, running 10 a.m. to 3 p.m. Donated auction items must be valued between $10 and $75; no perishables; insurance limits items above $75. Volunteers check in at 9 a.m. at the north pavilion and must confirm arrival by September 10 by emailing Gretchen at events@example.org. In case of severe weather, the event moves to the Riverside Community Center on Maple Avenue, with an email list update by 7 a.m. on event day.

#### must_keep
- The event is the Riverside Community Fundraiser.
- The date is Saturday, September 13.
- The venue is Elm Grove Park.
- The event runs from 10 a.m. to 3 p.m.
- Donated auction items must be valued between $10 and $75.
- No perishable donations.
- No items above $75 due to the insurance cap.
- Volunteer check-in is at 9 a.m. at the north pavilion.
- Volunteers must confirm arrival by September 10.
- The confirmation contact is Gretchen at events@example.org.
- Severe weather moves the event to the Riverside Community Center on Maple Avenue.
- Weather updates will go to the email list by 7 a.m. on September 13.

#### must_not_claim
- Do not accept donations above $75 per item.
- Do not promise the event will proceed at Elm Grove Park regardless of weather.
- Do not promise volunteer shifts that have not been confirmed.
- Do not change the September 10 volunteer confirmation deadline.

#### rewrite_quality_targets
A good rewrite is warm and community-spirited while keeping all logistics — item value limits, volunteer check-in time, the rain plan — clearly structured and easy to scan.

#### expected_rewrite_challenges
The model may drop the $75 insurance cap detail when polishing the donation language, or may make the weather contingency sound optional rather than a real plan.

### Case 090 - plan options without account change
- id: rewrite-draft-090
- category: customer_success
- source_type: email
- tone_preset: warm
- risk_tags: holdout, customer_success
- input_word_count_band: 160-280

#### input_draft
Hi Broderick, I wanted to reach out before your renewal comes up. Your current plan is Business Standard with 12 seats, and it renews on October 1 at $89 per seat per month. I see you reached out last week asking about options, so I put together a quick summary. Option A is to stay on Business Standard at $89 per seat — no change needed, it just auto-renews. Option B is to move up to Business Advanced at $119 per seat per month, which adds the analytics suite, the API access tier, and priority onboarding for new seats. Option C is to scale down to 8 seats at the same Business Standard rate of $89 per seat. I cannot make any of these changes without a written confirmation from you — the account cannot be modified until I receive your reply. If I do not hear back by September 24, the plan will auto-renew at its current terms: 12 seats at $89 per seat. Please reply with your preference before September 24.

#### what_actually_happened
Broderick's account is on the Business Standard plan with 12 seats, renewing October 1 at $89 per seat per month. Three options were presented: Option A (no change, auto-renews), Option B (upgrade to Business Advanced at $119 per seat per month, adding analytics suite, API access tier, and priority onboarding for new seats), and Option C (scale down to 8 seats at $89 per seat). No changes can be made without written confirmation from Broderick. If no reply is received by September 24, the plan auto-renews at the current terms.

#### must_keep
- The customer is Broderick.
- The current plan is Business Standard.
- The current seat count is 12 seats.
- The renewal date is October 1.
- The current price is $89 per seat per month.
- Option B is Business Advanced at $119 per seat per month.
- Business Advanced adds the analytics suite, API access tier, and priority onboarding for new seats.
- Option C is to scale down to 8 seats at $89 per seat.
- No account changes can be made without written confirmation from Broderick.
- If no reply is received by September 24, the plan auto-renews at current terms (12 seats, $89 per seat).
- Broderick must reply by September 24.

#### must_not_claim
- Do not make any account changes without Broderick's written confirmation.
- Do not apply a discount to any of the options.
- Do not promise a response time for processing Broderick's selection.
- Do not state that Option C reduces the renewal date or billing cycle.

#### rewrite_quality_targets
A good rewrite presents the three options in a scannable, warm way, keeps all prices and seat counts exact, and makes September 24 the unmistakable action deadline without pressuring Broderick.

#### expected_rewrite_challenges
The model may merge or blur the seat counts across options, or may imply that a discount is available if Broderick upgrades, in an effort to make Option B sound more attractive.

### Case 091 - long support ticket follow-up
- id: rewrite-draft-091
- category: customer_support
- source_type: email
- tone_preset: warm
- risk_tags: holdout, support
- input_word_count_band: 200-320

#### input_draft
Hi Bridget, I wanted to give you an update on ticket SUP-20847. You first reported the issue with your Meridian Pro subscription portal on October 3, and we acknowledged it on October 4. Our engineers confirmed on October 9 that the root cause is a sync conflict between your account tier and the billing module introduced in the September release. We have escalated this to the platform team and the current status is "under active review." I know that is a frustrating status message and I wish I had a cleaner answer. I cannot promise a fix date at this point — the platform team needs to finish their investigation. What I can tell you is that I will send you a personal update by October 17 whether or not there is a resolution. In the meantime the portal workaround I sent on October 9 still lets you access your subscription settings directly. If anything changes before October 17 I will reach out sooner, and if you hit anything new in the meantime just reply to this ticket and it will come straight to me.

#### what_actually_happened
Customer Bridget opened ticket SUP-20847 on October 3 about a sync issue with the Meridian Pro subscription portal. Support acknowledged on October 4. Engineers confirmed on October 9 that a September release introduced a sync conflict between the account tier and billing module. The issue is escalated to the platform team, currently under active review. A workaround was sent October 9. No fix date can be promised. A personal update will be sent by October 17.

#### must_keep
- The customer is Bridget.
- The ticket identifier is SUP-20847.
- The issue is with the Meridian Pro subscription portal.
- The customer first reported the issue on October 3.
- Support acknowledged on October 4.
- Engineers confirmed the root cause on October 9 as a sync conflict introduced in the September release.
- The issue is escalated to the platform team and is under active review.
- A workaround was sent on October 9 that still allows access to subscription settings.
- No fix date can be promised.
- A personal update will be sent by October 17.

#### must_not_claim
- Do not promise the issue will be resolved before October 17.
- Do not invent a fix date or commit to a resolution timeline beyond the October 17 update.
- Do not say the September release issue has been rolled back.
- Do not offer a refund or account credit.

#### rewrite_quality_targets
The rewrite should feel calm and human, not defensive, while keeping all dates, the ticket ID, and the no-fix-promise boundary intact. The workaround and the next update date should stand out clearly.

#### expected_rewrite_challenges
A model may over-apologize to the point of implying a fix is imminent, or drop the October 4 acknowledgment date and the October 9 workaround details.

### Case 092 - disputed charge with evidence hold
- id: rewrite-draft-092
- category: billing_support
- source_type: email
- tone_preset: warm
- risk_tags: holdout, billing
- input_word_count_band: 200-320

#### input_draft
Hi Wendell, I have reviewed your dispute on invoice INV-33091 for $247.00. The invoice covers the Starter Accelerate plan from November 1 to November 30. You mentioned that the charge appeared on your statement twice, and I can see that a second transaction for $247.00 posted on November 8 under the same invoice number. I have flagged this for our billing team and the review period is up to 10 business days from today, November 12, which means you should expect a resolution by November 26. I cannot approve a refund before the review is complete because the billing team needs to verify whether the duplicate originated on our side or through your payment processor. If the duplicate is confirmed on our end, we will refund $247.00 to the original payment method. If the review finds the transaction originated from your payment processor, we will send you the reference details so you can dispute it there. Please hold off on filing a chargeback with your bank during this window — it will pause our internal review.

#### what_actually_happened
Customer Wendell disputed invoice INV-33091 for $247.00, covering the Starter Accelerate plan from November 1 to November 30. A second $247.00 transaction posted on November 8 under the same invoice number. The billing team review began November 12 and has a 10 business day window, with a resolution expected by November 26. No refund can be approved until the source of the duplicate is confirmed. If it originated on the company's end, $247.00 will be refunded to the original payment method. If it originated from the payment processor, reference details will be provided for a direct dispute.

#### must_keep
- The customer is Wendell.
- The invoice identifier is INV-33091.
- The disputed amount is $247.00.
- The billing period is November 1 to November 30.
- The plan is Starter Accelerate.
- A second $247.00 transaction posted on November 8 under the same invoice number.
- The review period is up to 10 business days from November 12.
- The expected resolution date is November 26.
- A refund cannot be approved before the review is complete.
- If the duplicate is confirmed on the company's end, $247.00 will be refunded to the original payment method.
- Wendell should not file a chargeback during the review window.

#### must_not_claim
- Do not approve or promise a refund before the review completes.
- Do not shorten the 10 business day review window.
- Do not say the duplicate charge was definitely the company's fault.
- Do not tell Wendell to file a chargeback.

#### rewrite_quality_targets
The rewrite should be warm and reassuring without pre-judging the outcome. Both resolution paths and the chargeback caution should be stated clearly and without sounding threatening.

#### expected_rewrite_challenges
The model may drop one of the two resolution paths, soften the chargeback warning to the point of omitting it, or imply the refund is already approved.

### Case 093 - multi-owner sprint decisions needed
- id: rewrite-draft-093
- category: workplace_update
- source_type: note
- tone_preset: warm
- risk_tags: holdout, workplace
- input_word_count_band: 200-320

#### input_draft
Status note for Cobalt Initiative Sprint 4. There are three items that need decisions before we can move forward. First, the API schema handoff between Gideon and the frontend team is blocked because the auth token format was changed on November 5 without a migration guide. Gideon owns this and the target to unblock the frontend is November 10. Second, Helena needs product sign-off on the revised onboarding copy by November 11 so she can hand it to legal. If sign-off slips past November 11 the compliance review window closes and the January 15 launch is at risk. Third, the staging environment budget approval for the additional $800 per month needs Renata's sign-off by November 12. If not approved by then, staging provisioning shifts to the next billing cycle which pushes integration testing to late November. Decisions needed: Gideon to share the migration guide, product to sign off on the onboarding copy, Renata to approve the $800 staging budget.

#### what_actually_happened
Sprint 4 of the Cobalt Initiative has three blockers. Gideon owns the API schema handoff; a November 5 auth token format change without a migration guide is blocking the frontend team, with an unblock target of November 10. Helena needs product sign-off on revised onboarding copy by November 11 or the compliance window closes and the January 15 launch is at risk. Renata must approve $800 per month in additional staging budget by November 12 or integration testing shifts to late November.

#### must_keep
- The initiative is the Cobalt Initiative Sprint 4.
- Gideon owns the API schema handoff.
- The auth token format changed on November 5 without a migration guide.
- The target to unblock the frontend team is November 10.
- Helena needs product sign-off on the revised onboarding copy by November 11.
- If sign-off slips past November 11, the compliance review window closes.
- The January 15 launch is at risk if the compliance window closes.
- Renata must sign off on the $800 per month staging budget by November 12.
- Missing the November 12 deadline shifts integration testing to late November.
- Three decisions are needed: migration guide from Gideon, product sign-off for Helena, Renata's budget approval.

#### must_not_claim
- Do not say any of the three blockers are already resolved.
- Do not imply the January 15 launch date has been moved.
- Do not approve the $800 budget without Renata's explicit sign-off.
- Do not blame any individual for causing the blockers.

#### rewrite_quality_targets
The rewrite should structure the three blockers and decisions so each owner can scan and act quickly. Warmth should come from the framing, not from softening the urgency of the deadlines.

#### expected_rewrite_challenges
The model may merge the three blockers into vague prose or drop the specific date dependencies, especially the link between November 11 and the compliance window.

### Case 094 - late activity form and trip fee
- id: rewrite-draft-094
- category: teacher_parent
- source_type: email
- tone_preset: warm
- risk_tags: holdout, teacher
- input_word_count_band: 200-320

#### input_draft
Hi Rosalind, I wanted to follow up about Fletcher's participation in the Spring Showcase on May 22. We sent the activity consent form and the $15 materials fee notice home on April 28. As of today, May 7, I have not received Fletcher's consent form or the $15 payment. Without both, I cannot list Fletcher as a confirmed participant. Our school office requires all consent forms and fees to be submitted by May 14 so we have time to order the correct materials and arrange group spots. The office is in Building C, Room 101, and they accept checks made out to Ridgecrest Elementary or cash in an envelope with the student's name on it. I understand May can get busy and I know you are managing a lot. If there is something preventing Fletcher from participating or a reason the paperwork did not make it home, please let me know by May 10 so I can note it in the activity file. After May 14 I cannot add late participants to the Showcase roster.

#### what_actually_happened
The teacher is following up with parent Rosalind about student Fletcher's participation in the Spring Showcase on May 22. The activity consent form and $15 materials fee notice were sent home on April 28. As of May 7, neither the form nor payment has been received. The school deadline is May 14. Forms and fees go to the office in Building C, Room 101. Payment can be a check made out to Ridgecrest Elementary or cash in an envelope with the student's name. The teacher asks Rosalind to flag any issues by May 10.

#### must_keep
- The student is Fletcher.
- The parent is Rosalind.
- The event is the Spring Showcase on May 22.
- The consent form and $15 materials fee notice were sent home on April 28.
- As of May 7, neither the form nor the payment has been received.
- The submission deadline is May 14.
- The office is in Building C, Room 101.
- Payment can be a check made out to Ridgecrest Elementary or cash in an envelope with the student's name.
- The teacher asks Rosalind to flag any issues by May 10.
- Late participants cannot be added to the Showcase roster after May 14.

#### must_not_claim
- Do not extend the May 14 deadline.
- Do not waive or reduce the $15 fee.
- Do not confirm Fletcher as a participant before the form and payment arrive.
- Do not blame Rosalind for losing the form.

#### rewrite_quality_targets
The rewrite should be warm and practical, making the deadline, payment method, and office location easy to find while not sounding punitive. The May 10 early-flag request should remain visible.

#### expected_rewrite_challenges
The model may soften the no-late-addition rule to the point of sounding flexible, or drop the Building C, Room 101 detail and payment specifics in favor of general warmth.

### Case 095 - two-tier proposal with excluded scope
- id: rewrite-draft-095
- category: sales_followup
- source_type: email
- tone_preset: warm
- risk_tags: holdout, sales
- input_word_count_band: 200-320

#### input_draft
Hi Celeste, following up on our call last Thursday. I have put together quote Q-4452 with two options based on what you described. Option A is the Core package: 25 seats, monthly billing at $58 per seat, which includes the standard analytics dashboard and email integrations. Option B is the Growth package: 25 seats, annual billing at $52 per seat per month, which includes everything in Core plus the priority support channel and the custom reporting module. The quote is valid until June 20. I want to be clear about one thing — the data migration service we discussed is not included in either option. That requires a separate statement of work and I can introduce you to our implementation team if you decide to move forward. I cannot add data migration to Q-4452 without a new quote. If Option B is interesting but the annual commitment is a concern, I am happy to walk through the numbers again. To proceed with either option please sign and return Q-4452 by June 20 and I will send the order confirmation the same day.

#### what_actually_happened
Sales rep is following up with prospect Celeste. Quote Q-4452 has two options. Option A: Core package, 25 seats, $58 per seat per month, monthly billing, includes analytics dashboard and email integrations. Option B: Growth package, 25 seats, $52 per seat per month on annual billing, includes Core plus priority support and custom reporting. Quote is valid until June 20. Data migration is excluded from both options and requires a separate statement of work. Adding data migration to Q-4452 is not possible without a new quote.

#### must_keep
- The contact is Celeste.
- The quote identifier is Q-4452.
- Option A is the Core package: 25 seats, $58 per seat per month, monthly billing.
- Option A includes the standard analytics dashboard and email integrations.
- Option B is the Growth package: 25 seats, $52 per seat per month, annual billing.
- Option B includes everything in Core plus priority support and the custom reporting module.
- The quote is valid until June 20.
- The data migration service is not included in either option.
- Data migration requires a separate statement of work.
- Data migration cannot be added to Q-4452 without a new quote.
- The signed quote must be returned by June 20 for order confirmation the same day.

#### must_not_claim
- Do not include data migration in Option A or Option B.
- Do not extend the June 20 expiry date.
- Do not offer a price lower than $52 per seat per month.
- Do not promise same-day delivery of a new quote if data migration is requested.

#### rewrite_quality_targets
The rewrite should present both options cleanly so Celeste can compare them at a glance. The data migration exclusion must remain prominent and plainly worded without derailing the relationship tone.

#### expected_rewrite_challenges
The model may blur the scope exclusion to soften the message, drop the $52/$58 distinction, or imply data migration could be bundled informally.

### Case 096 - records release and privacy process
- id: rewrite-draft-096
- category: medical_admin
- source_type: email
- tone_preset: warm
- risk_tags: holdout, medical_admin
- input_word_count_band: 200-320

#### input_draft
Hi Phoebe, thank you for submitting the records release request for your file. Our office received your signed authorization form on January 9. Dr. Hargreaves reviewed the request and confirmed the records can be released under your authorization. The release covers the clinical notes and lab results from your visits between March 1 and December 31 of last year. Our medical records coordinator will prepare the package and send it by secure fax to the receiving provider at the fax number you listed, which ends in 4477. We aim to complete releases within 10 business days of receiving the authorization. Given we received yours on January 9, you should expect the fax to go out by January 23 at the latest. If the receiving provider has not confirmed receipt by January 30, please contact our front desk at extension 204 so we can verify the transmission. I am not able to share the records directly with you by email — they must go to the provider you designated on the form.

#### what_actually_happened
Patient Phoebe submitted a records release authorization form received by the office on January 9. Dr. Hargreaves confirmed the release is authorized. The release covers clinical notes and lab results from March 1 through December 31 of the prior year. The records coordinator will send the package by secure fax to the provider's fax number ending in 4477. The 10 business day processing window from January 9 means the fax should go by January 23. If the provider has not confirmed receipt by January 30, Phoebe should call the front desk at extension 204. Records cannot be emailed directly to the patient.

#### must_keep
- The patient is Phoebe.
- The signed authorization form was received on January 9.
- Dr. Hargreaves reviewed and confirmed the release is authorized.
- The release covers clinical notes and lab results from March 1 through December 31 of the prior year.
- The records will be sent by secure fax to the provider's fax number ending in 4477.
- The processing window is 10 business days from receipt on January 9.
- The fax is expected to go out by January 23 at the latest.
- If the provider has not confirmed receipt by January 30, Phoebe should contact the front desk at extension 204.
- Records cannot be emailed directly to the patient.

#### must_not_claim
- Do not offer to send records directly to the patient by email.
- Do not claim the fax has already been sent.
- Do not promise delivery before January 23.
- Do not provide medical advice or comment on the clinical content of the records.

#### rewrite_quality_targets
The rewrite should be warm and clear, walking Phoebe through the process without burying the fax-only policy or the January 30 follow-up step. The staff role and contact details must remain specific.

#### expected_rewrite_challenges
The model may drop the fax number detail, merge the January 23 and January 30 dates confusingly, or soften the email-delivery restriction to the point of implying it might be possible.

### Case 097 - quoted tenant complaint and repair timeline
- id: rewrite-draft-097
- category: property_logistics
- source_type: email
- tone_preset: warm
- risk_tags: holdout, property
- input_word_count_band: 200-320

#### input_draft
Hi Vance, thank you for your message about Unit 3B. You wrote on February 4: "The bathroom exhaust fan has been rattling nonstop since January 28 and it is affecting my sleep." I want you to know I take that seriously. I logged a maintenance request with ID MNT-8801 on February 5. Our licensed HVAC contractor, Torchlight Services, is scheduled to inspect the unit on February 11 between 10 a.m. and 1 p.m. They will assess whether the fan motor needs replacement or whether the mounting bracket is loose. I will need you or someone you authorize to be present or to leave a key at the office by February 10 at noon. Once Torchlight completes the inspection I will send you a written summary of their findings and the expected repair timeline. I am not able to confirm a rent credit for the disturbance at this stage — that is a decision that requires a written assessment from Torchlight and review by our property manager. Please reply to confirm the February 11 access window works for you.

#### what_actually_happened
Tenant Vance reported a rattling bathroom exhaust fan in Unit 3B that started January 28. Property management received the complaint on February 4 and logged maintenance request MNT-8801 on February 5. Torchlight Services is scheduled to inspect on February 11 between 10 a.m. and 1 p.m. to assess whether the fan motor or mounting bracket is the issue. The tenant or an authorized person must be present, or a key must be left at the office by February 10 at noon. A written summary will follow the inspection. A rent credit is not confirmed and requires Torchlight's written assessment and property manager review.

#### must_keep
- The tenant is Vance.
- The unit is 3B.
- The tenant reported the issue on February 4, quoting that the exhaust fan has been rattling since January 28.
- The maintenance request identifier is MNT-8801, logged on February 5.
- The contractor is Torchlight Services.
- The inspection is scheduled for February 11 between 10 a.m. and 1 p.m.
- The tenant or an authorized person must be present, or a key left at the office by February 10 at noon.
- A written summary of findings and repair timeline will follow the inspection.
- A rent credit is not confirmed and requires a written assessment from Torchlight and property manager review.

#### must_not_claim
- Do not confirm or promise a rent credit.
- Do not promise the fan will be repaired on February 11.
- Do not say Torchlight has already completed an assessment.
- Do not waive the access requirement.

#### rewrite_quality_targets
The rewrite should feel attentive and organized. The tenant's own words should be handled carefully — acknowledged, not minimized. The no-credit boundary must remain clear without sounding dismissive.

#### expected_rewrite_challenges
The model may drop the tenant's quoted complaint, compress the two-step access requirement (present or key by noon), or imply the rent credit decision is imminent.

### Case 098 - panel interview scheduling across time zones
- id: rewrite-draft-098
- category: hr_recruiting
- source_type: email
- tone_preset: warm
- risk_tags: holdout, recruiting
- input_word_count_band: 200-320

#### input_draft
Hi Osbourne, congratulations on advancing to the panel interview stage for the Senior Product Analyst role. I am coordinating with three panelists across two time zones and I want to make sure we find a time that works before the panel slots fill. I have three options available, all times listed in NZST: Option 1 is Tuesday, March 4 at 10 a.m. Option 2 is Wednesday, March 5 at 2 p.m. Option 3 is Thursday, March 6 at 9 a.m. The panel will include Harriet from Product, Desmond from Data Engineering, and one additional panelist I will confirm by February 27. The interview is 90 minutes via Microsoft Teams. I will send the Teams link and a briefing document once you confirm a slot. Please reply with your preferred option, or a ranked list, by February 25 so I can lock the calendar before the panel's availability changes. I cannot guarantee all three options will remain open after February 25.

#### what_actually_happened
Candidate Osbourne has advanced to the panel interview for the Senior Product Analyst role. Three options are available in NZST: Tuesday March 4 at 10 a.m., Wednesday March 5 at 2 p.m., Thursday March 6 at 9 a.m. The panel includes Harriet from Product and Desmond from Data Engineering; a third panelist will be confirmed by February 27. The interview is 90 minutes via Microsoft Teams. Osbourne must reply with a preference or ranked list by February 25. Slots cannot be guaranteed open after February 25.

#### must_keep
- The candidate is Osbourne.
- The role is Senior Product Analyst.
- All times are listed in NZST.
- Option 1 is Tuesday, March 4 at 10 a.m.
- Option 2 is Wednesday, March 5 at 2 p.m.
- Option 3 is Thursday, March 6 at 9 a.m.
- The panel includes Harriet from Product and Desmond from Data Engineering.
- A third panelist will be confirmed by February 27.
- The interview is 90 minutes via Microsoft Teams.
- Osbourne must reply with a preference or ranked list by February 25.
- Slots cannot be guaranteed open after February 25.

#### must_not_claim
- Do not confirm a specific interview time before Osbourne responds.
- Do not guarantee all three options will remain available after February 25.
- Do not name or describe the third panelist before February 27.
- Do not imply the role has been offered.

#### rewrite_quality_targets
The rewrite should feel warm and organized, with the three time options easy to scan and the February 25 reply deadline prominent. The panelist-confirmation caveat should remain visible without overshadowing the invitation tone.

#### expected_rewrite_challenges
The model may drop one of the three time options, confuse the February 25 response deadline with the February 27 panelist confirmation, or imply the third panelist is already known.

### Case 099 - donor update with report timeline
- id: rewrite-draft-099
- category: nonprofit_community
- source_type: email
- tone_preset: warm
- risk_tags: holdout, nonprofit
- input_word_count_band: 200-320

#### input_draft
Dear Sylvester, thank you for your generous gift of $500 to the Clearwater Community Fund on September 15. Your official receipt, reference number DON-2024-1107, was emailed to sylvester@example.com on September 16. The funds have been allocated to our Youth Literacy Initiative, which is the campaign you designated on the donation form. We expect to publish the first use-of-funds report for this campaign in our quarterly newsletter in January 2025. I want to be upfront that the report will cover aggregate results across all donors to the Youth Literacy Initiative, not a personalized breakdown per donor. The January report will describe what the campaign has funded so far, but it will not be a final accounting — the initiative runs through June 2025. If you have questions about how your gift is being used before the January report, please contact our donor relations team at grants@example.org. We are grateful for your support and we will keep you updated as the initiative progresses.

#### what_actually_happened
Donor Sylvester gave $500 to the Clearwater Community Fund on September 15. The receipt with reference number DON-2024-1107 was emailed to sylvester@example.com on September 16. The gift was designated to the Youth Literacy Initiative. The first use-of-funds report will appear in the quarterly newsletter in January 2025, covering aggregate results, not per-donor breakdowns. The initiative runs through June 2025, so the January report will not be a final accounting. Questions before the report can go to grants@example.org.

#### must_keep
- The donor is Sylvester.
- The donation amount is $500.
- The donation was made on September 15 to the Clearwater Community Fund.
- The receipt reference number is DON-2024-1107.
- The receipt was emailed to sylvester@example.com on September 16.
- The funds are designated to the Youth Literacy Initiative.
- The first use-of-funds report will appear in the quarterly newsletter in January 2025.
- The report will cover aggregate results across all donors, not a per-donor breakdown.
- The initiative runs through June 2025, so the January report is not a final accounting.
- Questions before the report can be directed to grants@example.org.

#### must_not_claim
- Do not promise a personalized per-donor breakdown in the report.
- Do not say the January report will be a final accounting of the initiative.
- Do not imply the $500 has not yet been allocated.
- Do not promise a follow-up contact beyond the January newsletter.

#### rewrite_quality_targets
The rewrite should feel grateful and transparent without overpromising report specificity. The January 2025 date, aggregate-only scope, and initiative end date should be easy to find without burying the warm tone under administrative detail.

#### expected_rewrite_challenges
The model may drop the DON-2024-1107 receipt reference, conflate the January report with a final accounting, or soften the aggregate-only note to the point of implying Sylvester will receive individual impact data.

### Case 100 - renewal options pending confirmation
- id: rewrite-draft-100
- category: customer_success
- source_type: email
- tone_preset: warm
- risk_tags: holdout, customer_success
- input_word_count_band: 200-320

#### input_draft
Hi Loretta, your Elevate Business account, account number ACT-55902, is coming up for renewal on December 31. I wanted to reach out early so you have time to review your options. Right now you are on the Standard tier with 12 seats at $190 per seat per year, which comes to $2,280 annually. I have two options to share before the renewal date. Option 1 is to renew as-is: 12 seats, Standard tier, $2,280. Option 2 is to move to the Professional tier at $230 per seat per year. With 12 seats that would be $2,760 annually and would add dedicated onboarding support and the advanced reporting suite. If you want to reduce seats or move to a monthly plan, that is also possible but I need your written confirmation before December 15 to make sure the billing system reflects the change on January 1. I cannot change the account structure after December 20 without it taking effect in the next cycle. Please reply by December 15 with your preferred option and I will send the updated renewal document same day.

#### what_actually_happened
Customer Loretta holds account ACT-55902, an Elevate Business Standard tier account with 12 seats at $190 per seat per year ($2,280 annually), renewing December 31. Option 1 is to renew as-is at $2,280. Option 2 is to upgrade to Professional tier at $230 per seat per year ($2,760 annually), adding dedicated onboarding support and the advanced reporting suite. Seat reductions or a switch to monthly billing are also possible if Loretta confirms in writing by December 15. Account structure changes cannot be processed after December 20 for the January 1 billing date.

#### must_keep
- The customer is Loretta.
- The account identifier is ACT-55902.
- The account is Elevate Business, Standard tier.
- The current plan is 12 seats at $190 per seat per year, totaling $2,280 annually.
- The renewal date is December 31.
- Option 1 is to renew as-is: 12 seats, Standard tier, $2,280.
- Option 2 is Professional tier at $230 per seat per year, totaling $2,760 annually.
- Option 2 adds dedicated onboarding support and the advanced reporting suite.
- Seat reductions or monthly billing changes require written confirmation by December 15.
- Account structure cannot be changed after December 20 for a January 1 effective date.
- Loretta must reply by December 15 to receive an updated renewal document same day.

#### must_not_claim
- Do not change the account structure without Loretta's written confirmation.
- Do not offer a price lower than $190 per seat per year for Standard or $230 for Professional.
- Do not guarantee account changes submitted after December 15 will take effect January 1.
- Do not imply a third pricing option exists beyond what is stated.

#### rewrite_quality_targets
The rewrite should make both options scannable and easy to compare, keep the December 15 confirmation deadline prominent, and sound like a helpful partner without sounding like a sales pitch. The December 20 hard cutoff for system changes must remain unambiguous.

#### expected_rewrite_challenges
The model may merge the December 15 and December 20 dates, drop the per-seat price breakdown for one of the options, or soften the account-change deadline to the point of obscuring it.
