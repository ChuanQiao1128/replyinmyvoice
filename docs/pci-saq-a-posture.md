# PCI DSS SAQ-A Payment Posture

Last updated: 2026-06-01

Owner / attestor: TimeAwake Ltd, through the product owner/operator for Reply In My Voice.

Status: Draft operational posture for annual self-assessment. This document is not a completed PCI DSS Self-Assessment Questionnaire, Attestation of Compliance, audit report, or legal opinion. The owner must complete and attest the applicable annual PCI DSS self-assessment.

## Scope Summary

Reply In My Voice accepts card-not-present payments for rewrite credit packs and the Pro/API subscription through Stripe-hosted payment surfaces:

- Stripe Checkout is used for pack purchases and subscription checkout.
- Stripe Billing customer portal is used for subscription and billing management.
- The application creates Checkout and portal sessions, then redirects the user to a Stripe-hosted URL.
- Card entry occurs only on Stripe-hosted pages. Reply In My Voice does not host a card-entry form.
- Full card number (PAN) and card security code (CVV) are not transmitted to, processed by, or stored in the Next.js app, C# backend, Azure SQL database, Cloudflare Worker, logs, runbooks, or tracked repository files.

The intended PCI DSS posture is SAQ-A because account-data capture and payment processing are outsourced to Stripe, a PCI-compliant payment processor, and the merchant application only receives Stripe identifiers, payment status, subscription status, amount/currency metadata, receipt or invoice references, and webhook events needed for access control, credit grants, refunds, disputes, support, and reconciliation.

## SAQ-A Rationale

The current implementation is designed to fit SAQ-A rather than a broader e-commerce payment-page scope because:

1. Users are redirected to Stripe-hosted Checkout for payment entry.
2. Users manage billing through the Stripe-hosted customer portal.
3. The app does not include Stripe Elements, an embedded Checkout form, a custom card form, or any field intended to collect PAN or CVV.
4. The app/backend create sessions and process webhook events, but do not receive raw cardholder data.
5. The database stores entitlement and accounting metadata only, such as Stripe customer IDs, subscription IDs, event IDs, payment intent IDs, SKUs, amount/currency, credit balances, status fields, and timestamps.
6. Support processes may reference the customer email, Stripe receipt/invoice reference, payment intent ID, or the last four digits shown in Stripe for identity checks. Support must never request or store full PAN or CVV.

This posture must be re-checked before any payment integration change. Introducing a custom payment form, embedded card fields, direct card-data collection, non-Stripe card processor, or storage of electronic cardholder data may change the applicable PCI DSS scope.

## Payment Data Flow

1. A signed-in user chooses a credit pack, starts Pro/API checkout, or opens billing management from the app.
2. The Next.js route proxies the authenticated request to the C# backend.
3. The backend creates a Stripe Checkout Session or Stripe Billing portal session using server-side Stripe credentials and known SKU/price configuration.
4. The backend returns the Stripe-hosted session URL to the app.
5. The browser redirects the user to Stripe. The user enters card details only on Stripe-hosted pages.
6. Stripe handles payment authorization, card authentication, vaulting, receipts, subscription billing, and portal actions.
7. Stripe sends signed webhook events to the application webhook endpoint.
8. The backend verifies the webhook signature, deduplicates events, and updates entitlement/accounting state such as rewrite credits, subscription status, refunds, disputes, and audit records.
9. The user returns to Reply In My Voice, where the app shows credit balance or subscription status from the backend.

## Third Parties And Stored Metadata

| Party | Role | Card-data boundary |
|---|---|---|
| Stripe | Hosted Checkout, Billing customer portal, payment processing, card vaulting, receipts, invoices, refunds, disputes, and webhook events. | Stripe receives and processes cardholder data. Reply In My Voice relies on Stripe's PCI posture for card-data functions. |
| Cloudflare | Hosts the Next.js Worker/proxy and routes payment API requests. | Handles session and webhook metadata only; no PAN or CVV should pass through application code. |
| Microsoft Azure | Hosts the C# backend and Azure SQL persistence. | Stores account, entitlement, Stripe ID, amount/currency, receipt/invoice, and audit metadata only. |
| Microsoft Entra External ID | Authentication provider. | Authentication only; no card-data role. |

Allowed internal payment metadata includes Stripe customer IDs, checkout session IDs, subscription IDs, payment intent IDs, event IDs, SKUs, amount/currency, receipt or invoice references, credit grants/clawbacks, subscription status, period dates, and audit timestamps.

Prohibited internal data includes full PAN, CVV, raw card-track data, unredacted payment-form screenshots, Stripe secret key values, webhook secret values, private keys, and raw credential material.

## Annual Self-Assessment Checklist

Complete this checklist at least annually and before any live payment-flow redesign:

- Confirm the production purchase flow still redirects to Stripe-hosted Checkout.
- Confirm the customer billing-management flow still uses the Stripe-hosted Billing customer portal.
- Confirm there are no custom card-entry fields, embedded card forms, or card-data collection paths in `app/`, `components/`, `lib/`, backend routes, admin tools, or support tools.
- Review database schema, application logs, support templates, and audit logs for accidental PAN/CVV storage.
- Confirm support copy tells customers to provide receipt/invoice references and never asks for full card number or card security code.
- Review Stripe Dashboard PCI guidance for the account and complete the applicable annual self-assessment form.
- Confirm Stripe remains the only card-data processor for Reply In My Voice payments and retain current Stripe PCI/compliance evidence outside the public repo.
- Review Stripe webhook signature verification and idempotency controls.
- Confirm Terms, Privacy, and support runbook copy still match the hosted-only payment flow.
- Confirm no secret values, card data, or raw credential material are present in tracked files.
- Record the assessment date, form/version used, attestor, evidence location, and next due date in the operator's private compliance records.

## Attestation Responsibility

The annual PCI DSS self-assessment and any Attestation of Compliance must be completed by the owner/operator on behalf of TimeAwake Ltd. Codex may draft documentation and check repository posture, but it must not claim that SAQ-A has been completed or attested.

Suggested private record fields:

- Assessment date.
- PCI DSS / SAQ form version used.
- Attestor name and role.
- Stripe account PCI evidence reviewed.
- Payment-flow changes since the last assessment.
- Exceptions or remediation items.
- Next review due date.

## Incident Handling

If PAN, CVV, or raw cardholder data is ever received in support email, logs, screenshots, docs, or database rows:

1. Do not copy it into issues, commits, docs, chat, or tickets.
2. Restrict access to the affected record immediately.
3. Delete or redact the data from systems where deletion is permitted.
4. Rotate any affected credentials if exposure includes secrets.
5. Record a private incident note with date, affected system, remediation, and owner follow-up.
6. Re-evaluate PCI scope before accepting further payments if card-data handling changed.

## Public Copy Alignment

- `docs/support-runbook.md` links here from the Stripe integration/support sections.
- `/terms` should continue to state that subscriptions, packs, and payment details are managed through Stripe-hosted Checkout and portal pages.
- `/privacy` should continue to state that the app stores account/billing metadata but not full card numbers or card security codes.

## References

- PCI Security Standards Council: PCI DSS SAQ-A merchant eligibility and self-assessment materials.
- Stripe integration security guide: PCI compliance is shared between Stripe and the merchant, and Stripe supports low-risk integrations where payment data is collected directly by Stripe.
- Stripe Checkout documentation: hosted Checkout redirects customers to a Stripe-hosted payment page.
- Stripe customer portal documentation: the Billing customer portal is a Stripe-hosted UI for subscription and billing management.
