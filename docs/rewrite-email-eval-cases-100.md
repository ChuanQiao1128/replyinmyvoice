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
