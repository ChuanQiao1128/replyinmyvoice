import React from "react";

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

const reassuranceItems = [
  "No card needed to try",
  "Packs don't auto-renew",
  "Cancel Pro/API anytime",
  "Secure checkout via Stripe",
  "History stays in your browser",
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
      <ul className="pp-reassurance" aria-label="Pricing reassurance">
        {reassuranceItems.map((item) => (
          <li className="pp-reassurance-item" key={item}>
            {item}
          </li>
        ))}
      </ul>
      <p className="pp-credibility">
        <strong>Replies that still sound like you.</strong> Built for teacher,
        sales, workplace, and client replies. Operated by TimeAwake Ltd.
      </p>
    </section>
  );
}
