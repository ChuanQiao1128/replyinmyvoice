# Payment Receipts And Tax Invoices

PAY-21 surfaces Stripe-hosted receipt links from `GET /api/me/payments`. The app does not generate PDF receipts and does not calculate tax in the frontend.

Current verification:

- Purchase rows expose `receiptUrl` from the payment ledger when Stripe receipt capture has populated it.
- The account page links directly to the hosted Stripe receipt.
- No PAY-20 Stripe Tax or `automatic_tax` implementation is present in this branch. When Stripe Tax and invoicing are enabled, the GST line must come from the Stripe-hosted receipt or invoice linked by `receiptUrl`, not from local tax math.
