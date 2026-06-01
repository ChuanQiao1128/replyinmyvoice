# PAY-11: [OWNER] First real NZ$ purchase + refund verification (manual)

**Priority:** P1 (launch gate) · **Owner:** **OWNER ONLY — do NOT auto-implement** · **Skill:** — · **Depends on:** PAY-01..PAY-10

> ⚠️ **OWNER-ONLY CHECKPOINT. Codex / any automation MUST NOT execute this.**
> AGENTS.md hard limit: *automation must never initiate a real Stripe charge.* This issue is a human runbook + acceptance record, not a coding task. It must NOT carry the `ready` label and must NOT be picked up by the delivery-pipeline worker.

## Context
- This is M7-001: the first real money path, reserved as the owner's hands-on test. It has never been executed (sandbox/test-mode only to date).
- Run this only AFTER the engineering items (PAY-01..PAY-10) are merged and the live-mode dashboard setup is done.

## Owner prerequisites (Stripe dashboard, owner-only)
- Stripe live account activated (KYC, payout bank, 2FA, statement descriptor, public name "TimeAwake Ltd / Reply In My Voice", support email).
- LIVE prices created for Quick / Value / Pro·API; live `price_…` IDs captured.
- LIVE webhook endpoint `https://replyinmyvoice.com/api/stripe/webhook` with events: `checkout.session.completed`, `customer.subscription.created/updated/deleted`, `charge.refunded`, `charge.dispute.created`, `charge.dispute.closed`, **`invoice.payment_failed`** (per PAY-01). Live `whsec_…` captured.
- Live Customer Portal + receipt/refund emails configured.
- Live secret values placed in Worker secrets + Functions app settings + CI secrets (key names only in any tracked file); `cf:deploy` done.

## Manual verification steps (owner)
1. Buy one **Quick Pack** with a real card on `replyinmyvoice.com/pricing`.
2. Verify: Stripe shows the live payment; `StripeEvent` Processed; a `RewriteCredit` (PURCHASE, +90d) granted; `/app` balance increments by 10; a paid rewrite succeeds and consumes 1; a receipt link is present (PAY-05).
3. (Pro/API, optional) subscribe; verify subscription Active + 90 visible + portal cancel works; simulate a failed renewal and confirm downgrade (PAY-01).
4. Issue a small **real refund** (via the new `/admin` UI from PAY-07); confirm the credit clawback fires and the balance drops.
5. Record the outcome in `plans/decisions-log.md` (and `plans/MONEY-MADE.txt` if used).

## Acceptance
- [ ] One real purchase completed and verified end-to-end (payment → webhook → grant → balance → consume → receipt).
- [ ] One real refund completed and clawback verified.
- [ ] Result recorded in `plans/decisions-log.md`.

## Do NOT
- Do NOT let any automation perform the purchase or refund. Owner does this by hand.
