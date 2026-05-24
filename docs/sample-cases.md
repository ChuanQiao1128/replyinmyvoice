# Sample Cases

Last updated: 2026-05-25

These are the homepage demo samples. The draft and rewritten reply text are
taken from the documented 100-case rewrite eval set
(`docs/rewrite-email-eval-cases-100.md`) so the landing page shows real,
fact-preserving examples rather than invented copy.

The "AI-like signal" values shown below are representative homepage figures.
In the full measured eval run (`docs/rewrite-eval-results/`), the rewrite
engine drove the rewrite AI-like signal well below the 40% naturalness
threshold on every case; the homepage shows conservative representative values
in the single-to-low-double digits rather than the raw measured floor. The
`landing-demo-samples` test keeps `components/landing/sample-cases.ts` aligned
with the values documented here.

## Teacher Message

- Category: Teacher to parent (eval case rimv-email-001)
- Used on homepage: yes
- Incoming context: Ms Alvarez asks whether Maya's permission slip and the $12 for the April 9 museum trip already arrived.
- Tone preset: Warm
- Draft AI-like signal: 74%
- Rewrite AI-like signal: 10%
- Score change: -64 pts
- Preserved facts checklist: checked after dismissal March 28, blue folder, $12 payment, signed permission slip, April 9 science museum trip, April 2 deadline, offer a new form or one sent home. Do not imply the parent is wrong or careless.

Rough draft:

> Hello, I checked and I do not see it. You should send another form and payment so she can attend.

Rewritten reply:

> Hi Ms. Alvarez, I checked the classroom folder basket and payment envelope after dismissal on March 28, and I didn't see Maya's permission slip or the $12 payment. I know you mentioned sending it in her blue folder last Thursday, but it hasn't turned up here. Maya still needs a signed permission slip and the $12 to attend the April 9 science museum trip — the deadline is April 2. You can send in a new form, or I can send an extra one home with her. Just let me know which you'd prefer.

## Sales Follow-Up

- Category: Sales follow-up (eval case rimv-email-003)
- Used on homepage: yes
- Incoming context: Jonah is comparing two vendors and wants the final numbers plus whether onboarding can start before June 1.
- Tone preset: Warm
- Draft AI-like signal: 69%
- Rewrite AI-like signal: 12%
- Score change: -57 pts
- Preserved facts checklist: $1,800/month for 25 seats, one-time $650 onboarding fee, earliest kickoff May 28, full setup ~5 business days after signed contract and data access. Do not promise completion before June 1.

Rough draft:

> Thanks for your time. Our platform is a great fit and we can absolutely get you onboarded before June 1. The pricing is attached again. Let me know if you want to sign today.

Rewritten reply:

> Hi Jonah, the final numbers are $1,800/month for 25 seats plus a one-time $650 onboarding fee. The earliest onboarding kickoff is May 28, and full setup usually takes 5 business days after we get the signed contract and data access — so we can start before June 1 but can't guarantee completion by then. Happy to walk through any of it if that helps your comparison.

## Workplace Email

- Category: Workplace update (eval case rimv-email-045)
- Used on homepage: yes
- Incoming context: A teammate is upset about being blamed for a launch delay and asks what you actually need from them now.
- Tone preset: Direct
- Draft AI-like signal: 82%
- Rewrite AI-like signal: 7%
- Score change: -75 pts
- Preserved facts checklist: QA checklist sent April 18 at 2:16 PM, Product moved launch from April 22 to April 29, 30-minute private follow-ups with each person, focus on timeline / handoff expectations / launch-readiness, confirm availability. Do not assign blame.

Rough draft:

> I understand the frustration. We need to talk through what happened and agree on next steps.

Rewritten reply:

> The QA checklist went out on April 18 at 2:16 PM, and Product moved the launch from April 22 to April 29 before QA signoff was complete. Rather than a group retro, our manager wants short 30-minute private follow-ups with you and Jordan separately, focused on timeline, handoff expectations, and launch-readiness criteria. The one thing I need from you now is to confirm your availability for that follow-up so I can get it scheduled.

## Client Reply

- Category: Customer support (eval case rimv-email-020)
- Used on homepage: yes
- Incoming context: A frustrated customer got conflicting answers about returning three final-sale jackets and wants a clear yes or no.
- Tone preset: Warm
- Draft AI-like signal: 88%
- Rewrite AI-like signal: 8%
- Score change: -80 pts
- Preserved facts checklist: Order #J3009, three final-sale jackets, no refund or store credit, one-time size exchange within 30 days for unworn tagged items, February 2 purchase, exchange window to March 3, today is March 1, navy medium still tagged, two navy larges in stock, no change without confirmation.

Rough draft:

> Sorry for the confusion. Final sale items cannot be returned, but we may be able to exchange the navy medium if it still has tags.

Rewritten reply:

> I'm sorry for the conflicting information you got. Order #J3009 was three final-sale jackets, so refunds and store credit aren't available — but a one-time size exchange is allowed for unworn items with tags within 30 days. You bought them on February 2, so the exchange window runs to March 3, and today is March 1. The navy medium still has its tags and we have two navy larges in stock. Would you like me to exchange the medium for a large? I won't make any change without your confirmation.
