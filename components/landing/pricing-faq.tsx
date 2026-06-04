const questions = [
  {
    q: "What counts as one rewrite?",
    a: "One successful result from the Rewrite button uses one rewrite, even when the system tries a few internal strategies for quality.",
  },
  {
    q: "Do packs expire?",
    a: "Quick and Value packs stay valid for 90 days. Pro/API rewrites reset monthly and do not roll over.",
  },
];

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
        {questions.map((item) => (
          <article className="pricing-faq-item" key={item.q}>
            <h3>{item.q}</h3>
            <p>{item.a}</p>
          </article>
        ))}
      </div>
    </section>
  );
}
