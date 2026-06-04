const comparisonItems = [
  {
    name: "Trial",
    detail: "Trial code access for 3 rewrites",
  },
  {
    name: "Quick Pack",
    detail: "10 rewrites · valid 90 days",
  },
  {
    name: "Value Pack",
    detail: "30 rewrites · valid 90 days",
  },
  {
    name: "Pro·API",
    detail: "90 rewrites/mo · monthly subscription",
  },
];

export function PricingComparison() {
  return (
    <section
      aria-labelledby="pricing-comparison-heading"
      className="pricing-section pricing-comparison"
    >
      {/* PRICING-COMPARISON: fleshed out in PRICE-02 */}
      <div className="pricing-section-head">
        <h2 id="pricing-comparison-heading">Compare what you get</h2>
      </div>
      <ul className="pricing-comparison-list">
        {comparisonItems.map((item) => (
          <li className="pricing-comparison-item" key={item.name}>
            <span>{item.name}</span>
            <p>{item.detail}</p>
          </li>
        ))}
      </ul>
    </section>
  );
}
