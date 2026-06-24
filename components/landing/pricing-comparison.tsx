type Plan = {
  name: string;
  recommended?: boolean;
};

const plans: Plan[] = [
  { name: "Free (trial code)" },
  { name: "Quick Pack" },
  { name: "Value Pack", recommended: true },
  { name: "Pro·API" },
];

const included = "included";
const notIncluded = "not-included";

type ComparisonValue = string | typeof included | typeof notIncluded;

type ComparisonRow = {
  label: string;
  values: [ComparisonValue, ComparisonValue, ComparisonValue, ComparisonValue];
};

const comparisonRows: ComparisonRow[] = [
  {
    label: "Rewrites",
    values: ["3 trial", "10", "30", "90 / mo"],
  },
  {
    label: "Price",
    values: ["Free with code", "NZ$2.50", "NZ$6.90", "NZ$19.90 / mo"],
  },
  {
    label: "Price per rewrite",
    values: ["Free trial", "≈ NZ$0.25", "≈ NZ$0.23", "≈ NZ$0.22"],
  },
  {
    label: "Validity",
    values: ["Trial", "90 days", "90 days", "Monthly"],
  },
  {
    label: "AI Signal (naturalness)",
    values: [included, included, included, included],
  },
  {
    label: "Facts preserved",
    values: [included, included, included, included],
  },
  {
    label: "Server-backed history",
    values: [included, included, included, included],
  },
  {
    label: "REST API + MCP access",
    values: [notIncluded, notIncluded, notIncluded, included],
  },
  {
    label: "Shared web + API balance",
    values: [notIncluded, notIncluded, notIncluded, included],
  },
  {
    label: "Billing",
    values: [
      "No card",
      "One-time",
      "One-time",
      "Subscription · cancel anytime",
    ],
  },
];

function ComparisonCell({ value }: { value: ComparisonValue }) {
  if (value === included) {
    return (
      <span className="pricing-comparison-status">
        <span aria-hidden="true" className="pricing-comparison-check">
          ✓
        </span>
        <span className="pricing-comparison-sr">Included</span>
      </span>
    );
  }

  if (value === notIncluded) {
    return (
      <span className="pricing-comparison-status">
        <span aria-hidden="true" className="pricing-comparison-dash">
          —
        </span>
        <span className="pricing-comparison-sr">Not included</span>
      </span>
    );
  }

  return <span>{value}</span>;
}

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

      <div
        aria-labelledby="pricing-comparison-heading"
        className="pricing-comparison-table-wrap"
        role="region"
        tabIndex={0}
      >
        <table className="pricing-comparison-table">
          <caption>
            Plan-by-plan feature comparison for trial access, one-time packs,
            and Pro/API.
          </caption>
          <thead>
            <tr>
              <th className="pricing-comparison-corner" scope="col">
                <span className="pricing-comparison-sr">Feature</span>
              </th>
              {plans.map((plan) => (
                <th
                  className={
                    "pricing-comparison-plan-head" +
                    (plan.recommended ? " is-recommended" : "")
                  }
                  key={plan.name}
                  scope="col"
                >
                  <span className="pricing-comparison-plan-name">
                    {plan.name}
                  </span>
                  {plan.recommended ? (
                    <span className="pricing-comparison-badge">
                      Most popular
                    </span>
                  ) : null}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {comparisonRows.map((row) => (
              <tr key={row.label}>
                <th scope="row">{row.label}</th>
                {row.values.map((value, index) => {
                  const plan = plans[index];

                  return (
                    <td
                      className={plan.recommended ? "is-recommended" : ""}
                      key={plan.name}
                    >
                      <ComparisonCell value={value} />
                    </td>
                  );
                })}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <p className="pricing-comparison-note">
        Packs expire 90 days after purchase. Pro/API rewrites reset monthly and
        do not roll over.
      </p>
    </section>
  );
}
