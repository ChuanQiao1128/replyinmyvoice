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
              Get the reply ready
              <br />
              before you send.
            </h2>
            <p>
              Use it for extension requests, lecturer emails, client replies,
              and awkward drafts where the facts matter and the wording needs
              one careful pass.
            </p>
          </div>
          <div className="cta-side">
            <Link href="/sign-up" className="btn btn-accent">
              Start rewriting <span className="btn-arrow">→</span>
            </Link>
            <div className="meta">3 free rewrites · No card required</div>
            <div className="meta" style={{ marginTop: 12 }}>
              Value Pack NZ$6.90 · 30 rewrites · most popular
            </div>
            <div className="meta" style={{ marginTop: 12 }}>
              Quick Pack NZ$2.50 · Pro/API NZ$19.90/mo with API
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}
