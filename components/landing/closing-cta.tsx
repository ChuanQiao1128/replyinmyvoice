import Link from "next/link";

export function ClosingCta() {
  return (
    <section className="final">
      <div className="wrap">
        <div className="final-card">
          <div>
            <div className="eyebrow" style={{ color: "var(--bg)" }}>
              <span className="dot" style={{ background: "var(--accent)" }} />
              Ready when you are
            </div>
            <h2 style={{ marginTop: 20 }}>
              Prepare replies
              <br />
              that feel ready to send.
            </h2>
            <p>
              Keep the facts intact, choose the tone, compare the writing
              signal, and copy a cleaner reply into your existing email
              workflow.
            </p>
          </div>
          <div className="cta-side">
            <Link href="/sign-up" className="btn btn-accent">
              Start rewriting <span className="btn-arrow">→</span>
            </Link>
            <div className="meta">3 free rewrites · No card required</div>
            <div className="meta" style={{ marginTop: 12 }}>
              NZD $9/mo · 40 rewrites · Cancel anytime
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}
