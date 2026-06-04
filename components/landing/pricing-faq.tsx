const questions = [
  {
    q: "What counts as one rewrite?",
    a: "One click on Rewrite counts as one usage attempt, even if we try multiple internal strategies to improve it. Validation, sign-in, payment, and server errors are not counted.",
  },
  {
    q: "What happens when a pack runs out?",
    a: "You can buy another pack or move to Pro/API. One-time packs do not auto-charge when they run out.",
  },
  {
    q: "Do packs expire?",
    a: "Yes. One-time packs are valid for 90 days from purchase. Pro/API rewrites reset each monthly period and do not roll over.",
  },
  {
    q: "Can I cancel Pro/API?",
    a: "Yes, anytime through the Stripe billing portal. Access continues until the end of the current billing period.",
  },
  {
    q: "Do I need a card to try it?",
    a: "No. A trial code unlocks 3 rewrites with no card.",
  },
  {
    q: "Is my content private?",
    a: "Recent rewrites stay in your browser's local history and are not saved to our database. Avoid pasting passwords, payment details, or highly sensitive personal data.",
  },
  {
    q: "What currency is this, and is tax included?",
    a: "Prices are in New Zealand dollars (NZ$). Stripe applies any required tax at checkout.",
  },
  {
    q: "Can I get a refund?",
    a: "Reach out at info@timeawake.co.nz and we'll work toward a reasonable resolution. Refund decisions depend on the situation, so we do not promise automatic refunds.",
  },
] as const;

export function PricingFaq() {
  return (
    <section
      aria-labelledby="pricing-faq-heading"
      className="pricing-section pricing-faq"
    >
      {/* PRICING-FAQ: expanded in PRICE-03 */}
      <div className="pricing-section-head">
        <h2 id="pricing-faq-heading">Questions & answers</h2>
      </div>
      <div className="pricing-faq-list">
        {questions.map((item, index) => (
          <details className="pricing-faq-item" key={item.q} open={index === 0}>
            <summary>{item.q}</summary>
            <p className="pricing-faq-answer">{item.a}</p>
          </details>
        ))}
      </div>
    </section>
  );
}
