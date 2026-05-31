# Support Runbook — `replyinmyvoice.com`

Date: 2026-05-22
Owner: TimeAwake Ltd (ChuanQiao1128)
Scope: How `info@timeawake.co.nz` is monitored, what gets answered automatically, and how to triage customer requests during the early commercial launch of `replyinmyvoice.com`.

This document satisfies M7-004 (confirm support email pipeline).

---

## 1. Support address

| Channel | Address | Who reads it |
|---|---|---|
| Primary | `info@timeawake.co.nz` | Operator (ChuanQiao1128). Also wired as the `From` and `Reply-To` for Stripe receipts and the customer portal. |
| Privacy / data requests | `info@timeawake.co.nz` with subject prefix `[privacy]` | Operator triages within 72h. |
| Security incident | `info@timeawake.co.nz` with subject prefix `[security]` | Operator triages within 24h. |

We deliberately do **not** publish multiple addresses to keep the inbox single-funnel during launch. A future `support@` alias can be added after volume justifies it.

## 2. Inbox monitoring expectations

- Operator checks `info@timeawake.co.nz` at minimum twice per business day (NZ business hours).
- Auto-reply (see §4) sets a 1-business-day response expectation.
- Out-of-band escalations: Stripe disputes go to the operator's phone via Stripe dashboard notifications; webhook failure alerts go via Application Insights / Sentry once those are wired (M7-003).
- If the operator is unavailable for >24h, set an auto-responder with the expected return date. Do not promise faster response than the runbook commits to.

## 3. Stripe integration (user-action steps)

The operator completes these in the Stripe dashboard once. Codex does not touch the live Stripe account from automation.

1. **Receipts** → Stripe Dashboard → Settings → Email → Customer emails → enable "Successful payments" and "Refunds". Set the support email shown on receipts to `info@timeawake.co.nz`.
2. **Customer portal** → Stripe Dashboard → Settings → Billing → Customer portal → enable Subscriptions (cancel + update payment method) and Invoices. Add the support link `mailto:info@timeawake.co.nz` to the portal's "Headline" and "Privacy" / "Terms" footer fields.
3. **Branding** → Stripe Dashboard → Settings → Business settings → Branding → confirm the public business name reads "TimeAwake Ltd / Reply In My Voice" and the support email is `info@timeawake.co.nz`. This is what appears on hosted Stripe pages.

Verification: after the steps, make a test Stripe sandbox checkout (M0-006 already documents test mode) and confirm the receipt email arrives with the right `From:` and a working customer portal link.

Payment security posture: card entry must stay on Stripe-hosted Checkout and customer portal pages. Reply In My Voice support must never request or store full card numbers or card security codes; see `docs/pci-saq-a-posture.md`.

## 4. Auto-reply template

Configure the auto-reply at `info@timeawake.co.nz` to fire on every inbound message. Keep it short.

```
Subject: Got your message — we'll be back to you within 1 business day

Kia ora,

Thanks for writing to Reply In My Voice support. This is an
automated acknowledgement — a real human will reply within
1 business day (NZ time).

If you need to refer to a payment, please include:
  • the email address you used at sign-up
  • the Stripe receipt number (it starts with "ch_" or
    "in_" and is in the receipt we sent you)

For billing questions you can also self-serve in the
customer portal we linked from your receipt: cancel,
change payment method, or download invoices.

— TimeAwake Ltd
   info@timeawake.co.nz
```

Do not include phone numbers, physical addresses, or any URL we don't control. Do not promise refunds, plan changes, or feature deliveries in the auto-reply.

## 5. Common-query playbook

For each common pattern, the operator's reply should follow the same shape: acknowledge, answer, link to self-serve if relevant, set expectation, sign off.

### 5.1 "I want a refund"

- Confirm the email matches a customer in Stripe.
- Use the Stripe customer portal or dashboard to issue a full refund within 14 days of charge per the Terms page (M4-008). After 14 days, decline politely and offer to pause/cancel for next cycle.
- Confirm the refund will arrive within 5–10 business days on the customer's bank.
- Do not issue partial refunds without a documented reason in `plans/decisions-log.md`.

### 5.2 "I can't sign in"

- Most common cause: customer using a different OAuth provider than at sign-up (Google vs Microsoft). Ask which account they used.
- If Entra External ID returned a callback error, ask for the timestamp and a screenshot; cross-reference Application Insights traces.
- Do not reset accounts manually unless we have written confirmation of identity (matching email + last 4 of payment method).

### 5.3 "The rewrite is wrong / changed facts"

- Apologise once. Ask for the rewrite ID (visible in the `/app` workspace history after the rewrite) and the screenshot of the diagnosis panel.
- Tag the case for the LearningOps backlog: add a row to `docs/rewrite-email-eval-cases-100.md` describing the scenario.
- Offer a one-off goodwill credit (one extra month of Pro) if the failure is clearly a regression. Do not commit to a fix timeline.

### 5.4 "How do I cancel?"

- Direct them to the customer portal link in their last Stripe receipt.
- If they cannot find the receipt, cancel from the Stripe dashboard yourself after confirming identity.
- The site uses cancel-at-period-end semantics (M0-006 / Stripe live state). Communicate that they keep access until the end of the current billing period.

### 5.5 "Is there a free trial / discount?"

- No free trial currently. We offer a free tier with a daily quota.
- No promo codes during launch unless the operator has opted in to a coupon in Stripe. Do not invent codes in correspondence.

### 5.6 "Privacy / GDPR / data deletion"

- Acknowledge within 24h. Confirm identity via the email on file.
- Honour deletion within 30 days. For an interim 30 days, the user remains in the database to handle disputes per the Terms (M4-008). State this clearly in the reply.
- Record the request in `plans/decisions-log.md` with date + redacted user identifier (e.g., `user_<first-3-chars-of-email>`).

## 6. Outage communications

If a launch-time outage is declared (see `docs/rollback-plan.md` trigger conditions), the support inbox sends a single proactive update:

```
Subject: Reply In My Voice — service interruption

Kia ora,

Reply In My Voice is currently degraded. We're working on a fix
and expect to be back within the next 2–4 hours. Active
subscriptions will not be charged for service days affected.

Thanks for your patience.

— TimeAwake Ltd
```

Do not send proactive emails for incidents <30 minutes. Do not promise compensation in the proactive email — handle credits 1:1 after restoration.

## 7. Security incident escalation

If an inbound email indicates a security issue (account takeover claim, suspected breach, vulnerability disclosure):

1. Reply within 24h acknowledging receipt only — do not confirm or deny anything technical until investigated.
2. Open a private note in `plans/decisions-log.md` with `security` tag + redacted user identifier.
3. If the report is credible, treat as an incident per the future `engineering:incident-response` workflow. Until that exists, follow `docs/rollback-plan.md` Trigger Conditions §"Privacy / billing incident".
4. Disclose to affected users only after consulting the operator. Do not auto-broadcast.

## 8. Where this runbook lives

- This file: `docs/support-runbook.md` (canonical).
- Auto-reply text: stored in the mail provider's settings; the source of truth here in §4 — copy/paste when reconfiguring.
- Stripe portal copy: managed in Stripe dashboard per §3; cross-link from M4-008 (Terms page) so legal text and operational text stay in sync.
- PCI / SAQ-A payment posture: `docs/pci-saq-a-posture.md`.

## 9. Open items

- M7-003 (Sentry) will add automated alerting that routes to `info@timeawake.co.nz`. Until then, error reports come exclusively from customers.
- M7-006 (UptimeRobot) will add proactive availability monitoring. Until then, outage detection depends on customer reports + manual checks.
- M8-016 (B2B onboarding) will add a separate onboarding email — when it ships, decide whether to split `support@` and `sales@`.

---

Last verified: 2026-05-22 (M7-004 documentation pass — operator still to perform Stripe dashboard config in §3).
