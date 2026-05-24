export type HomepageSampleCase = {
  label: string;
  /** Single-letter monogram shown on the comparison-card tab. */
  icon: string;
  context: string;
  draft: string;
  rewrite: string;
  before: number;
  after: number;
  sourceDocument: "docs/sample-cases.md";
};

/**
 * Homepage demo samples drawn from the documented 100-case rewrite eval set
 * (docs/rewrite-email-eval-cases-100.md). Drafts and rewrites are the real
 * case text; the AI-like signal values shown are representative homepage
 * figures documented in docs/sample-cases.md. The landing-demo-samples test
 * keeps these aligned with that document.
 */
export const homepageSampleCases: HomepageSampleCase[] = [
  {
    label: "Teacher Message",
    icon: "T",
    context:
      "Ms Alvarez asks whether Maya's permission slip and the $12 for the April 9 museum trip already arrived.",
    draft:
      "Hello, I checked and I do not see it. You should send another form and payment so she can attend.",
    rewrite:
      "Hi Ms. Alvarez, I checked the classroom folder basket and payment envelope after dismissal on March 28, and I didn't see Maya's permission slip or the $12 payment. I know you mentioned sending it in her blue folder last Thursday, but it hasn't turned up here. Maya still needs a signed permission slip and the $12 to attend the April 9 science museum trip — the deadline is April 2. You can send in a new form, or I can send an extra one home with her. Just let me know which you'd prefer.",
    before: 74,
    after: 10,
    sourceDocument: "docs/sample-cases.md",
  },
  {
    label: "Sales Follow-Up",
    icon: "S",
    context:
      "Jonah is comparing two vendors and wants the final numbers plus whether onboarding can start before June 1.",
    draft:
      "Thanks for your time. Our platform is a great fit and we can absolutely get you onboarded before June 1. The pricing is attached again. Let me know if you want to sign today.",
    rewrite:
      "Hi Jonah, the final numbers are $1,800/month for 25 seats plus a one-time $650 onboarding fee. The earliest onboarding kickoff is May 28, and full setup usually takes 5 business days after we get the signed contract and data access — so we can start before June 1 but can't guarantee completion by then. Happy to walk through any of it if that helps your comparison.",
    before: 69,
    after: 12,
    sourceDocument: "docs/sample-cases.md",
  },
  {
    label: "Workplace Email",
    icon: "W",
    context:
      "A teammate is upset about being blamed for a launch delay and asks what you actually need from them now.",
    draft:
      "I understand the frustration. We need to talk through what happened and agree on next steps.",
    rewrite:
      "The QA checklist went out on April 18 at 2:16 PM, and Product moved the launch from April 22 to April 29 before QA signoff was complete. Rather than a group retro, our manager wants short 30-minute private follow-ups with you and Jordan separately, focused on timeline, handoff expectations, and launch-readiness criteria. The one thing I need from you now is to confirm your availability for that follow-up so I can get it scheduled.",
    before: 82,
    after: 7,
    sourceDocument: "docs/sample-cases.md",
  },
  {
    label: "Client Reply",
    icon: "C",
    context:
      "A frustrated customer got conflicting answers about returning three final-sale jackets and wants a clear yes or no.",
    draft:
      "Sorry for the confusion. Final sale items cannot be returned, but we may be able to exchange the navy medium if it still has tags.",
    rewrite:
      "I'm sorry for the conflicting information you got. Order #J3009 was three final-sale jackets, so refunds and store credit aren't available — but a one-time size exchange is allowed for unworn items with tags within 30 days. You bought them on February 2, so the exchange window runs to March 3, and today is March 1. The navy medium still has its tags and we have two navy larges in stock. Would you like me to exchange the medium for a large? I won't make any change without your confirmation.",
    before: 88,
    after: 8,
    sourceDocument: "docs/sample-cases.md",
  },
];
