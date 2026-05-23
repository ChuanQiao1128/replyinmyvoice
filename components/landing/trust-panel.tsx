const trustItems = [
  {
    k: "Fact-credible",
    h: "Keeps the important details visible",
    p: "The workspace asks for the message, draft, and facts before shaping the reply, so dates, names, deadlines, and promises stay anchored.",
  },
  {
    k: "Decision layer",
    h: "Built for the moment before send",
    p: "Use it when you know what happened but the wording feels too stiff, too vague, or sharper than you mean.",
  },
  {
    k: "Tone check",
    h: "A reference, not a promise",
    p: "Tone check helps compare drafts while you remain the person who reviews the reply and decides what to send.",
  },
  {
    k: "Billing",
    h: "Managed by Stripe",
    p: "Starter is NZ$9.90/month, Pro/API is NZ$19.90/month, and one-time options are available for short bursts. Payments are managed by Stripe.",
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
              Built for messages
              <br />
              that matter.
            </h2>
            <p className="lede">
              The product stays focused on lower-anxiety replies: clear wording,
              visible facts, and a copy-ready message you can still review in
              your own judgment.
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
