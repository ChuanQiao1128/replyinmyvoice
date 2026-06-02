import Link from "next/link";
import type { Metadata } from "next";

import { SiteHeader } from "../../components/site-header";

export const metadata: Metadata = {
  title: "Developers",
  description:
    "Reply In My Voice for developers — an HTTP API that drops fact-preserving, in-your-voice reply rewrites straight into your own product. In active development.",
  openGraph: {
    title: "Reply In My Voice for developers",
    description:
      "Call the rewrite engine behind replyinmyvoice.com from your own app over a simple HTTP API. In active development.",
    url: "https://replyinmyvoice.com/developers",
  },
  twitter: {
    card: "summary_large_image",
    title: "Reply In My Voice for developers",
    description:
      "Call the rewrite engine behind replyinmyvoice.com from your own app over a simple HTTP API. In active development.",
  },
};

const apiValue = [
  {
    title: "Runs inside your product",
    body: "Call the rewrite engine straight from your app, CRM, or help desk. Your users get a reply in their own voice without ever leaving your product to paste into our site.",
  },
  {
    title: "The same engine as the site",
    body: "Identical fact-preserving rewrites and the before/after naturalness reference you see on replyinmyvoice.com — exposed over plain HTTP, so any language or runtime can call it.",
  },
  {
    title: "One key, metered per account",
    body: "Authenticate with a single bearer token tied to a Pro / API account. Usage is metered per key, so you can wire rewrites into production with predictable limits.",
  },
];

const endpoints = [
  {
    method: "POST",
    path: "/api/v1/rewrite",
    body: "Rewrite a rough draft into a clear, send-ready reply in the writer's voice — facts preserved, nothing invented.",
  },
  {
    method: "POST",
    path: "/api/v1/analyze-signal",
    body: "Return the before/after naturalness reference signal for a piece of text, the same one shown on the site.",
  },
];

type CodeToken = { t: string; c?: string };
type CodeLine = CodeToken[];

const requestLines: CodeLine[] = [
  [
    { t: "curl", c: "t-cmd" },
    { t: " https://replyinmyvoice.com/api/v1/rewrite " },
    { t: "\\", c: "t-punc" },
  ],
  [
    { t: "  -H", c: "t-flag" },
    { t: " " },
    { t: '"Authorization: Bearer rmv_live_…"', c: "t-str" },
    { t: " " },
    { t: "\\", c: "t-punc" },
  ],
  [
    { t: "  -d", c: "t-flag" },
    { t: " " },
    { t: "'{ ", c: "t-str" },
    { t: '"draft"', c: "t-key" },
    { t: ": ", c: "t-str" },
    { t: '"order is delayed, ships next week"', c: "t-str" },
    { t: " }'", c: "t-str" },
  ],
];

const responseLines: CodeLine[] = [
  [{ t: "{", c: "t-punc" }],
  [
    { t: '  "rewrittenText"', c: "t-key" },
    { t: ": ", c: "t-punc" },
    {
      t: "\"Hi Sam — your order's running a little behind; it ships next week.\"",
      c: "t-str",
    },
    { t: ",", c: "t-punc" },
  ],
  [
    { t: '  "signal"', c: "t-key" },
    { t: ": { ", c: "t-punc" },
    { t: '"draft"', c: "t-key" },
    { t: ": ", c: "t-punc" },
    { t: "78", c: "t-num" },
    { t: ", ", c: "t-punc" },
    { t: '"rewrite"', c: "t-key" },
    { t: ": ", c: "t-punc" },
    { t: "24", c: "t-num" },
    { t: " }", c: "t-punc" },
  ],
  [{ t: "}", c: "t-punc" }],
];

function CodeBlock({ lines }: { lines: CodeLine[] }) {
  return (
    <pre className="api-code">
      <code>
        {lines.map((line, i) => (
          <span className="cl" key={i}>
            {line.map((tok, j) => (
              <span className={tok.c} key={j}>
                {tok.t}
              </span>
            ))}
          </span>
        ))}
      </code>
    </pre>
  );
}

export default function DevelopersPage() {
  return (
    <main className="rimv">
      <SiteHeader />
      <section className="page">
        <div className="wrap">
          <div className="page-head">
            <div className="eyebrow">
              <span className="dot" />
              Developers
            </div>
            <h1>Put the rewrite engine inside your own product.</h1>
            <p className="lede">
              We&apos;re building an HTTP API for replyinmyvoice.com — so you can
              turn a rough draft into a reply in someone&apos;s own voice, with
              the facts preserved, directly from your app. No sending people to
              our site to copy and paste a result back.
            </p>
            <div className="dev-badge">
              <span className="dot" />
              In active development · not yet public
            </div>
            <div className="hero-cta" style={{ marginTop: 28 }}>
              <Link href="/sign-up" className="btn btn-primary btn-lg">
                Get early access <span className="btn-arrow">→</span>
              </Link>
              <a href="#api" className="btn btn-ghost btn-lg">
                See the API
              </a>
            </div>
          </div>

          <div className="pp-includes-head">Why build on the API</div>
          <div className="card-grid">
            {apiValue.map((item) => (
              <article className="v2card" key={item.title}>
                <h3>{item.title}</h3>
                <p>{item.body}</p>
              </article>
            ))}
          </div>

          <div id="api" className="pp-includes-head">
            What the API looks like
          </div>
          <div className="api-panel">
            <div className="api-bar">
              <span className="dots">
                <i />
                <i />
                <i />
              </span>
              <span className="bar-label">POST /api/v1/rewrite</span>
            </div>
            <div className="api-seg">
              <div className="api-seg-label">Request</div>
              <CodeBlock lines={requestLines} />
            </div>
            <div className="api-seg">
              <div className="api-seg-label">
                Response <span className="api-status">200 OK</span>
              </div>
              <CodeBlock lines={responseLines} />
            </div>
          </div>
          <div className="dev-note">
            {
              "// Preview only · the API is in development, so endpoints and shapes may change before launch"
            }
          </div>

          <div className="pp-includes-head">What each endpoint does</div>
          <div className="api-endpoints">
            {endpoints.map((endpoint) => (
              <article className="api-endpoint" key={endpoint.path}>
                <div className="ep-head">
                  <span className="method-badge">{endpoint.method}</span>
                  <span className="ep-path">{endpoint.path}</span>
                </div>
                <p>{endpoint.body}</p>
              </article>
            ))}
          </div>

          <div className="pp-includes-head">Also coming</div>
          <article className="v2card">
            <div className="v2card-head">
              <div className="eyebrow" style={{ color: "var(--muted)" }}>
                MCP
              </div>
              <span className="dev-pill planned">Later</span>
            </div>
            <h2>MCP for Codex</h2>
            <p>
              Later on we&apos;ll expose the same rewrite engine through a Model
              Context Protocol server, so Codex can call it in place. We&apos;ll
              share the details closer to launch.
            </p>
          </article>

          <div style={{ marginTop: 64 }}>
            <div className="final-card">
              <div>
                <div className="eyebrow" style={{ color: "var(--bg)" }}>
                  <span className="dot" style={{ background: "var(--accent)" }} />
                  Be first in line
                </div>
                <h2 style={{ marginTop: 20 }}>
                  Want to build on the
                  <br />
                  rewrite engine?
                </h2>
                <p>
                  Create an account and we&apos;ll email you the moment the API
                  is ready for early access.
                </p>
              </div>
              <div className="cta-side">
                <Link href="/sign-up" className="btn btn-accent">
                  Get early access <span className="btn-arrow">→</span>
                </Link>
                <div className="meta">
                  Trial codes unlock 3 rewrites to try the engine
                </div>
              </div>
            </div>
          </div>
        </div>
      </section>
    </main>
  );
}
