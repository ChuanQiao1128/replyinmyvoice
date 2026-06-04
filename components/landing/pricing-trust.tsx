const includes = [
  {
    k: "Tone check",
    p: "A before/after reference signal on every rewrite — a guide, never a guarantee.",
  },
  {
    k: "Facts preserved",
    p: "Dates, names, and amounts you mark stay intact through the rewrite.",
  },
  {
    k: "Warm · Direct",
    p: "Two simple tone presets that shape the reply around your real context.",
  },
  {
    k: "Private history",
    p: "Recent rewrites stay in your browser only — not saved to our database.",
  },
];

export function PricingTrust() {
  return (
    <section
      aria-labelledby="pricing-trust-heading"
      className="pricing-section pricing-trust"
    >
      {/* PRICING-TRUST: expanded in PRICE-04 */}
      <h2 className="pp-includes-head" id="pricing-trust-heading">
        Every plan includes
      </h2>
      <div className="pp-includes">
        {includes.map((item) => (
          <div className="pp-include" key={item.k}>
            <div className="k">{item.k}</div>
            <p>{item.p}</p>
          </div>
        ))}
      </div>
    </section>
  );
}
