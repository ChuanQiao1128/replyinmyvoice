import Link from "next/link";

type AuthMode = "sign-in" | "sign-up";

const authCopy = {
  "sign-in": {
    eyebrow: "Welcome back",
    heading: "Sign in to your reply workspace.",
    body: "Use your email verification code or Google to open your drafts, remaining usage, and billing state.",
    cardTitle: "Continue securely",
    cardBody: "Email code sign-in sends a one-time verification code before returning you to the app.",
    alternateText: "Need an account?",
    alternateHref: "/sign-up",
    alternateLabel: "Start here",
  },
  "sign-up": {
    eyebrow: "Create your account",
    heading: "Start with three free reply rewrites.",
    body: "Create an account with an email verification code or Google, then try the workspace before choosing a rewrite pack or Pro/API.",
    cardTitle: "Create your account",
    cardBody: "Email code sign-in verifies your address before sending you back to the workspace.",
    alternateText: "Already signed up?",
    alternateHref: "/sign-in",
    alternateLabel: "Sign in",
  },
} satisfies Record<
  AuthMode,
  {
    eyebrow: string;
    heading: string;
    body: string;
    cardTitle: string;
    cardBody: string;
    alternateText: string;
    alternateHref: string;
    alternateLabel: string;
  }
>;

const highlights = [
  "Facts stay in the reply",
  "Warm or Direct tone",
  "Value Pack: 30 rewrites for NZ$6.90",
];

export function GoogleOAuthCard({ mode = "sign-in" }: { mode?: AuthMode }) {
  const copy = authCopy[mode];

  return (
    <main
      className="rimv"
      style={{
        minHeight: "100vh",
        display: "flex",
        alignItems: "center",
        background: "var(--bg)",
        padding: "48px 0",
      }}
    >
      <div className="wrap" style={{ width: "100%" }}>
        <div
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(auto-fit, minmax(320px, 1fr))",
            gap: 48,
            alignItems: "center",
          }}
        >
          <div>
            <Link href="/" className="brand">
              <span className="brand-mark" aria-hidden="true">
                R
              </span>
              <span>Reply In My Voice</span>
            </Link>

            <div className="eyebrow" style={{ marginTop: 32 }}>
              <span className="dot" />
              {copy.eyebrow}
            </div>
            <h1 style={{ marginTop: 16, fontSize: "clamp(32px, 4vw, 50px)", lineHeight: 1.05 }}>
              {copy.heading}
            </h1>
            <p className="hero-lead" style={{ marginTop: 18, fontSize: 17 }}>
              {copy.body}
            </p>

            <ul
              style={{
                listStyle: "none",
                padding: 0,
                margin: "28px 0 0",
                display: "flex",
                flexDirection: "column",
                gap: 10,
              }}
            >
              {highlights.map((highlight) => (
                <li
                  key={highlight}
                  style={{ position: "relative", paddingLeft: 24, fontSize: 14.5, color: "var(--ink-2)" }}
                >
                  <span
                    aria-hidden="true"
                    style={{
                      position: "absolute",
                      left: 0,
                      top: "0.4em",
                      width: 12,
                      height: 8,
                      borderLeft: "1.5px solid var(--accent)",
                      borderBottom: "1.5px solid var(--accent)",
                      transform: "rotate(-45deg)",
                    }}
                  />
                  {highlight}
                </li>
              ))}
            </ul>
          </div>

          <section
            aria-labelledby="auth-card-title"
            style={{
              background: "var(--card)",
              border: "1px solid var(--rule)",
              borderRadius: 18,
              padding: 32,
              boxShadow: "0 30px 60px -40px rgba(17,21,15,0.16)",
            }}
          >
            <div className="eyebrow" style={{ color: "var(--muted)" }}>
              Email code sign-in
            </div>
            <h2 id="auth-card-title" style={{ fontSize: 24, marginTop: 8 }}>
              {copy.cardTitle}
            </h2>
            <p style={{ marginTop: 8, fontSize: 14, lineHeight: 1.5, color: "var(--ink-2)" }}>
              {copy.cardBody}
            </p>

            <form action="/api/auth/login" style={{ marginTop: 24 }}>
              <input type="hidden" name="redirectTo" value="/app" />
              <label
                htmlFor="auth-email"
                style={{
                  fontFamily: "var(--mono)",
                  fontSize: 12.5,
                  color: "var(--ink-2)",
                  letterSpacing: "0.02em",
                }}
              >
                Email address
              </label>
              <div
                style={{
                  marginTop: 8,
                  display: "flex",
                  alignItems: "center",
                  border: "1px solid var(--rule-2)",
                  borderRadius: 10,
                  padding: "12px 14px",
                  background: "var(--bg)",
                }}
              >
                <input
                  id="auth-email"
                  name="loginHint"
                  type="email"
                  autoComplete="email"
                  required
                  maxLength={320}
                  placeholder="you@example.com"
                  style={{
                    flex: 1,
                    minWidth: 0,
                    border: 0,
                    background: "transparent",
                    outline: "none",
                    fontSize: 15,
                    color: "var(--ink)",
                    fontFamily: "var(--sans)",
                  }}
                />
              </div>
              <button
                type="submit"
                className="btn btn-primary btn-lg"
                style={{ width: "100%", justifyContent: "center", marginTop: 12 }}
              >
                Continue with email code
              </button>
            </form>

            <a
              href="/api/auth/login?redirectTo=/app"
              className="btn btn-ghost btn-lg"
              style={{ width: "100%", justifyContent: "center", marginTop: 10 }}
            >
              <span
                aria-hidden="true"
                style={{
                  display: "inline-flex",
                  height: 18,
                  width: 18,
                  alignItems: "center",
                  justifyContent: "center",
                  borderRadius: 4,
                  border: "1px solid var(--rule-2)",
                  fontFamily: "var(--mono)",
                  fontSize: 12,
                  fontWeight: 600,
                }}
              >
                G
              </span>
              Continue with Google
            </a>

            <div
              style={{
                marginTop: 22,
                paddingTop: 18,
                borderTop: "1px solid var(--rule)",
                fontSize: 14,
                color: "var(--ink-2)",
              }}
            >
              <span>{copy.alternateText}</span>{" "}
              <Link
                href={copy.alternateHref}
                style={{ color: "var(--ink)", fontWeight: 500, textDecoration: "underline", textUnderlineOffset: 3 }}
              >
                {copy.alternateLabel}
              </Link>
            </div>
          </section>
        </div>
      </div>
    </main>
  );
}
