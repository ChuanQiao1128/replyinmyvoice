# PAY-28: PCI / SAQ-A compliance posture document

**Priority:** P2 · **Owner:** Codex (draft) + Owner (attest) · **Skill:** documentation · **Depends on:** none

## Context
- Repo root: `/Users/qc/Desktop/CloudFlare`
- Payments use **Stripe Checkout (hosted)** + the billing portal — card data never touches our servers, which places us in **PCI DSS SAQ-A** scope. There is no documented compliance posture / SAQ-A attestation, which is standard for a paid product.

## Constraints
See `plans/payment-module-audit.md`. Standard rules (banned terms, no secrets, never push to main). This is primarily a documentation task — no card data, no secrets in the doc.

## Changes required
1. `docs/pci-saq-a-posture.md`: state that all card entry is on Stripe-hosted pages (Checkout + portal), that no PAN/CVV is transmitted to or stored by our app/backend/DB, the resulting SAQ-A scope, the third parties (Stripe) and data flows, and an annual self-assessment checklist + who attests (owner / TimeAwake Ltd).
2. Cross-link from `docs/support-runbook.md` and the Terms/Privacy pages if appropriate (note where, do not necessarily restructure them here).

## Acceptance (machine-checkable)
- [ ] `docs/pci-saq-a-posture.md` exists covering: hosted-only card entry, SAQ-A scope rationale, data-flow summary, annual checklist, attestor.
- [ ] No secrets or card data in the doc; banned-term grep clean.

## Do NOT
- Do NOT claim a completed SAQ-A attestation (owner attests). Do NOT touch `main`.
