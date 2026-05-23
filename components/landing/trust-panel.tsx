const trustItems = [
  {
    k: "Fact-aware",
    h: "Preserves what must stay",
    p: "The workspace asks for the facts before it shapes the reply. The model doesn't invent details or add promises you didn't make.",
  },
  {
    k: "Boundary",
    h: "Reply content isn't stored",
    p: "Reply content is processed for the request and isn't saved to our database. Your drafts don't sit on our servers.",
  },
  {
    k: "Tone presets",
    h: "Warm or Direct, that's it",
    p: "Two presets that actually feel different. No twelve-slider tone studio you'll never touch after the first day.",
  },
  {
    k: "Billing",
    h: "Managed by Stripe",
    p: "Subscriptions are NZD $9/month through Stripe, operated by TimeAwake Ltd. Cancel from your account anytime.",
  },
];

export function TrustPanel() {
  return (
    <section className="block" id="trust">
      <div className="wrap">
        <div className="sec-head">
          <div>
            <span className="sec-num">05 · Trust</span>
          </div>
          <div className="sec-head-lead">
            <h2>
              Built for real
              <br />
              communication workflows.
            </h2>
            <p className="lede">
              The product is small on purpose. Each piece is here because it
              earns its place in a daily writing routine — not to pad a feature
              list.
            </p>
          </div>
        </div>

        <div className="trust">
          {trustItems.map((item) => (
            <div className="trust-item" key={item.k}>
              <div className="k">{item.k}</div>
              <h4>{item.h}</h4>
              <p>{item.p}</p>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
