import Link from "next/link";
import type { Metadata } from "next";
import type { ReactNode } from "react";

import { SiteHeader } from "../../components/site-header";

export const metadata: Metadata = {
  title: "Developers",
  description:
    "Reply In My Voice developer API docs for async, fact-preserving reply rewrites in CRM, help desk, and support workflows.",
  openGraph: {
    title: "Reply In My Voice for developers",
    description:
      "Add async, fact-preserving reply rewrites to your own product with a paid-quota API key.",
    url: "https://replyinmyvoice.com/developers",
  },
  twitter: {
    card: "summary_large_image",
    title: "Reply In My Voice for developers",
    description:
      "Add async, fact-preserving reply rewrites to your own product with a paid-quota API key.",
  },
};

const overviewCards = [
  {
    title: "Built for reply workflows",
    body: "Drop in-voice, fact-preserving rewrites into a CRM, help desk, support inbox, or internal workflow where people already write customer replies.",
  },
  {
    title: "Async by design",
    body: "Submit a draft, receive a job id immediately, then poll for processing, succeeded, or failed. Your client never waits on the full rewrite engine.",
  },
  {
    title: "Shared paid balance",
    body: "You pay per succeeded rewrite. API calls and website rewrites draw from the same account balance, and an API key works only when paid quota is available.",
  },
];

const errorRows = [
  {
    status: "400",
    code: "invalid_request",
    cause: "Missing JSON, missing draft, empty draft, or a draft value that is not text.",
  },
  {
    status: "400",
    code: "input_too_long",
    cause: "Draft is over 300 words or over 2400 chars.",
  },
  {
    status: "401",
    code: "invalid_key",
    cause: "Bearer key is missing, malformed, revoked, expired, or not recognized.",
  },
  {
    status: "402",
    code: "quota_exhausted",
    cause: "The account has no paid rewrite quota remaining.",
  },
  {
    status: "409",
    code: "idempotency_conflict",
    cause: "The same Idempotency-Key was reused with a different request body.",
  },
  {
    status: "429",
    code: "rate_limited",
    cause: "The key has reached its 60 requests per minute limit.",
  },
];

const submitCurl = `curl https://replyinmyvoice.com/api/v1/rewrite \\
  -H "Authorization: Bearer rmv_live_xxx" \\
  -H "Content-Type: application/json" \\
  -H "Idempotency-Key: crm-reply-123" \\
  -d '{ "draft": "Sam, your order is delayed and ships next week." }'`;

const submitResponse = `HTTP/1.1 202 Accepted
Location: /api/v1/rewrite/rw_123

{
  "id": "rw_123",
  "status": "processing"
}`;

const pollCurl = `curl https://replyinmyvoice.com/api/v1/rewrite/rw_123 \\
  -H "Authorization: Bearer rmv_live_xxx"`;

const pollProcessing = `{
  "id": "rw_123",
  "status": "processing"
}`;

const pollSucceeded = `{
  "id": "rw_123",
  "status": "succeeded",
  "rewrittenText": "Hi Sam, thanks for your patience. Your order is running a little behind and ships next week.",
  "signal": {
    "draft": 78,
    "rewrite": 24
  }
}`;

const pollFailed = `{
  "id": "rw_123",
  "status": "failed",
  "error": {
    "code": "rewrite_failed",
    "message": "The rewrite could not be completed."
  }
}`;

const usageResponse = `{
  "scope": "paid",
  "periodKey": "paid:sub_123:2026-07-01T00:00:00Z",
  "quota": 90,
  "used": 12,
  "remaining": 78,
  "periodEnd": "2026-07-01T00:00:00Z"
}`;

const errorBody = `{
  "error": {
    "code": "input_too_long",
    "message": "Draft must be 300 words or fewer and 2400 characters or fewer."
  }
}`;

function CodeBlock({
  children,
  label,
  status,
}: {
  children: string;
  label: string;
  status?: string;
}) {
  return (
    <div className="api-seg">
      <div className="api-seg-label">
        {label}
        {status ? <span className="api-status">{status}</span> : null}
      </div>
      <pre className="api-code">
        <code>{children}</code>
      </pre>
    </div>
  );
}

function EndpointCard({
  children,
  method,
  path,
  title,
}: {
  children: ReactNode;
  method: string;
  path: string;
  title: string;
}) {
  return (
    <article className="api-endpoint">
      <div className="ep-head">
        <span className="method-badge">{method}</span>
        <span className="ep-path">{path}</span>
      </div>
      <h3>{title}</h3>
      <div className="dev-endpoint-body">{children}</div>
    </article>
  );
}

export default function DevelopersPage() {
  return (
    <main className="rimv">
      <SiteHeader />
      <section className="page dev-page">
        <div className="wrap">
          <div className="page-head">
            <div className="eyebrow">
              <span className="dot" />
              Developers
            </div>
            <h1>Add in-voice rewrites to your own product.</h1>
            <p className="lede">
              The Reply In My Voice API lets your product submit a rough draft,
              receive an async rewrite job, and poll for a send-ready reply that
              preserves the user&apos;s facts and writing intent.
            </p>
            <div className="dev-hero-meta" aria-label="API highlights">
              <span>Async v1 API</span>
              <span>Paid quota required</span>
              <span>Shared website + API balance</span>
            </div>
            <div className="hero-cta" style={{ marginTop: 28 }}>
              <Link href="/developers/keys" className="btn btn-primary btn-lg">
                Get your API key <span className="btn-arrow">→</span>
              </Link>
              <a href="#quickstart" className="btn btn-ghost btn-lg">
                Quickstart
              </a>
            </div>
          </div>

          <section className="dev-section" aria-labelledby="overview-heading">
            <div className="pp-includes-head" id="overview-heading">
              Overview
            </div>
            <div className="card-grid">
              {overviewCards.map((item) => (
                <article className="v2card" key={item.title}>
                  <h3>{item.title}</h3>
                  <p>{item.body}</p>
                </article>
              ))}
            </div>
          </section>

          <section className="dev-section" id="quickstart" aria-labelledby="quickstart-heading">
            <div className="pp-includes-head" id="quickstart-heading">
              Quickstart
            </div>
            <div className="dev-doc-grid">
              <div className="dev-flow" aria-label="Quickstart steps">
                <div className="dev-step">
                  <span>1</span>
                  <div>
                    <h3>Create a key</h3>
                    <p>
                      Open{" "}
                      <Link href="/developers/keys" className="dev-text-link">
                        API keys
                      </Link>
                      , create a key, and copy the plaintext value once. Keys
                      start with <code>rmv_live_</code>.
                    </p>
                  </div>
                </div>
                <div className="dev-step">
                  <span>2</span>
                  <div>
                    <h3>Submit a draft</h3>
                    <p>
                      Send <code>{'{ "draft": "..." }'}</code> to{" "}
                      <code>POST /api/v1/rewrite</code>. The response is{" "}
                      <code>202 Accepted</code> with an <code>id</code>,{" "}
                      <code>status</code>, and <code>Location</code> header.
                    </p>
                  </div>
                </div>
                <div className="dev-step">
                  <span>3</span>
                  <div>
                    <h3>Poll for the result</h3>
                    <p>
                      Poll <code>{"GET /api/v1/rewrite/{id}"}</code> every 1-2 s
                      until the job is <code>succeeded</code> or{" "}
                      <code>failed</code>.
                    </p>
                  </div>
                </div>
              </div>

              <div className="api-panel dev-api-panel">
                <div className="api-bar">
                  <span className="dots">
                    <i />
                    <i />
                    <i />
                  </span>
                  <span className="bar-label">Submit and poll</span>
                </div>
                <CodeBlock label="Submit request">{submitCurl}</CodeBlock>
                <CodeBlock label="Submit response" status="202 Accepted">
                  {submitResponse}
                </CodeBlock>
                <CodeBlock label="Poll request">{pollCurl}</CodeBlock>
                <CodeBlock label="Poll response">{pollSucceeded}</CodeBlock>
              </div>
            </div>
          </section>

          <section className="dev-section" aria-labelledby="auth-heading">
            <div className="pp-includes-head" id="auth-heading">
              Authentication
            </div>
            <div className="dev-two-col">
              <article className="v2card">
                <h3>Bearer keys</h3>
                <p>
                  Authenticate each v1 request with{" "}
                  <code>Authorization: Bearer rmv_live_xxx</code>. Treat keys
                  like credentials: store them server-side, rotate them on a
                  schedule, revoke unused keys, and never log full key values.
                </p>
              </article>
              <article className="v2card">
                <h3>Lifecycle</h3>
                <p>
                  Create keys from the signed-in developer dashboard, copy the
                  plaintext once, use masked key values for support workflows,
                  and revoke old keys after clients have moved to the new one.
                </p>
              </article>
            </div>
          </section>

          <section className="dev-section" id="api" aria-labelledby="api-heading">
            <div className="pp-includes-head" id="api-heading">
              API reference
            </div>
            <div className="api-endpoints dev-reference-grid">
              <EndpointCard
                method="POST"
                path="/api/v1/rewrite"
                title="Submit a rewrite"
              >
                <p>
                  Request body is exactly <code>{'{ "draft": "..." }'}</code>.
                  The draft must be at or below 300 words and at or below 2400
                  chars. <code>signal</code> is not accepted in the request.
                </p>
                <ul className="dev-list">
                  <li>
                    Required header:{" "}
                    <code>Authorization: Bearer rmv_live_xxx</code>
                  </li>
                  <li>
                    Optional header: <code>Idempotency-Key</code>
                  </li>
                  <li>
                    Response: <code>202 Accepted</code>, <code>Location</code>,
                    and <code>{'{ "id": "...", "status": "processing" }'}</code>
                  </li>
                </ul>
              </EndpointCard>

              <EndpointCard
                method="GET"
                path="/api/v1/rewrite/{id}"
                title="Fetch job status"
              >
                <p>
                  Returns the current terminal or non-terminal state for the
                  rewrite job owned by the API key&apos;s account.
                </p>
                <ul className="dev-list">
                  <li>
                    <code>processing</code>: keep polling with backoff.
                  </li>
                  <li>
                    <code>succeeded</code>: includes <code>rewrittenText</code>{" "}
                    and <code>signal</code>.
                  </li>
                  <li>
                    <code>failed</code>: includes an error object and is
                    uncharged.
                  </li>
                </ul>
              </EndpointCard>

              <EndpointCard method="GET" path="/api/v1/usage" title="Read usage">
                <p>
                  Returns the account&apos;s current paid quota window as seen by
                  this key.
                </p>
                <ul className="dev-list">
                  <li>
                    Fields: <code>scope</code>, <code>periodKey</code>,{" "}
                    <code>quota</code>, <code>used</code>,{" "}
                    <code>remaining</code>, <code>periodEnd</code>
                  </li>
                  <li>
                    API calls and website rewrites share the same balance.
                  </li>
                  <li>
                    No free tier: keys require paid quota before they work.
                  </li>
                </ul>
              </EndpointCard>
            </div>

            <div className="dev-code-grid">
              <div className="api-panel">
                <div className="api-bar">
                  <span className="dots">
                    <i />
                    <i />
                    <i />
                  </span>
                  <span className="bar-label">
                    {"GET /api/v1/rewrite/{id}"}
                  </span>
                </div>
                <CodeBlock label="Processing">{pollProcessing}</CodeBlock>
                <CodeBlock label="Succeeded">{pollSucceeded}</CodeBlock>
                <CodeBlock label="Failed">{pollFailed}</CodeBlock>
              </div>
              <div className="api-panel">
                <div className="api-bar">
                  <span className="dots">
                    <i />
                    <i />
                    <i />
                  </span>
                  <span className="bar-label">GET /api/v1/usage</span>
                </div>
                <CodeBlock label="Usage response">{usageResponse}</CodeBlock>
                <CodeBlock label="Error body">{errorBody}</CodeBlock>
              </div>
            </div>
          </section>

          <section className="dev-section" aria-labelledby="errors-heading">
            <div className="pp-includes-head" id="errors-heading">
              Errors, limits, and idempotency
            </div>
            <div className="dev-callout">
              {
                "Rejected requests, failed jobs, and timeouts are uncharged; only a succeeded rewrite costs 1."
              }
            </div>
            <div className="dev-table-wrap">
              <table className="dev-table">
                <thead>
                  <tr>
                    <th>Status</th>
                    <th>Code</th>
                    <th>When it happens</th>
                    <th>Charged</th>
                  </tr>
                </thead>
                <tbody>
                  {errorRows.map((row) => (
                    <tr key={row.code}>
                      <td>{row.status}</td>
                      <td>
                        <code>{row.code}</code>
                      </td>
                      <td>{row.cause}</td>
                      <td>No</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <div className="dev-two-col">
              <article className="v2card">
                <h3>Rate limits</h3>
                <p>
                  Each key allows 60 requests per minute. Use{" "}
                  <code>X-RateLimit-Limit</code>,{" "}
                  <code>X-RateLimit-Remaining</code>, and{" "}
                  <code>X-RateLimit-Reset</code> headers to slow clients before
                  they receive <code>429 rate_limited</code>.
                </p>
              </article>
              <article className="v2card">
                <h3>Idempotency-Key</h3>
                <p>
                  Send one <code>Idempotency-Key</code> per logical submit. The
                  same key and same draft return the same job id; the same key
                  with a changed body returns <code>409 idempotency_conflict</code>.
                </p>
              </article>
            </div>
          </section>

          <section className="dev-section" aria-labelledby="guides-heading">
            <div className="pp-includes-head" id="guides-heading">
              Guides
            </div>
            <div className="dev-two-col">
              <article className="v2card">
                <h3>Poll with backoff</h3>
                <p>
                  Start with a 1 s delay after submit, then poll every 1-2 s
                  until <code>status</code> is <code>succeeded</code> or{" "}
                  <code>failed</code>. Avoid tight loops; submit is fast, but the
                  rewrite job may take longer under load.
                </p>
              </article>
              <article className="v2card">
                <h3>Handle failed jobs and timeouts</h3>
                <p>
                  A <code>failed</code> result or client-side timeout is
                  uncharged. It is safe to resubmit the same draft; use a fresh{" "}
                  <code>Idempotency-Key</code> when you want a new attempt.
                </p>
              </article>
            </div>
          </section>

          <section className="dev-section" aria-labelledby="pricing-heading">
            <div className="pp-includes-head" id="pricing-heading">
              Pricing, quota, data, and signal
            </div>
            <div className="dev-meta-grid">
              <article className="v2card">
                <h3>Pricing & quota</h3>
                <p>
                  API usage draws from the same paid rewrite balance as the
                  website. A submit request is not billed by itself; one
                  succeeded rewrite consumes one rewrite from the account
                  balance. When quota reaches zero, calls return{" "}
                  <code>402 quota_exhausted</code> until more paid quota is
                  available.
                </p>
              </article>
              <article className="v2card">
                <h3>Data & privacy</h3>
                <p>
                  API rewrite inputs, outputs, status, and metadata are retained
                  for a bounded 30-day retention window so async jobs,
                  idempotency, account history, and support workflows can work.
                </p>
              </article>
              <article className="v2card">
                <h3>signal</h3>
                <p>
                  <code>signal</code> appears only on a succeeded response. It is
                  an informational naturalness reference, may evolve over time,
                  and is not a guarantee. Lower values read more natural.
                </p>
              </article>
            </div>
          </section>

          <section className="dev-section" aria-labelledby="legal-heading">
            <div className="pp-includes-head" id="legal-heading">
              Legal
            </div>
            <div className="card-grid">
              <article className="v2card">
                <h3>API Terms of Use</h3>
                <p>
                  Draft API terms covering eligibility, key security, paid
                  quota, metering, service limits, and account changes.
                </p>
                <p>
                  <Link href="/developers/terms" className="dev-text-link">
                    Read the draft page
                  </Link>
                </p>
              </article>
              <article className="v2card">
                <h3>Acceptable Use Policy</h3>
                <p>
                  Draft API usage rules for responsible content, system load,
                  access sharing, and customer obligations.
                </p>
                <p>
                  <Link
                    href="/developers/acceptable-use"
                    className="dev-text-link"
                  >
                    Read the draft page
                  </Link>
                </p>
              </article>
              <article className="v2card">
                <h3>Data & Retention</h3>
                <p>
                  Draft data notes covering RewriteAttempt storage, the bounded
                  30-day retention window, deletion, and processors.
                </p>
                <p>
                  <Link href="/developers/data" className="dev-text-link">
                    Read the draft page
                  </Link>
                </p>
              </article>
            </div>
          </section>

          <div style={{ marginTop: 64 }}>
            <div className="final-card">
              <div>
                <div className="eyebrow" style={{ color: "var(--bg)" }}>
                  <span className="dot" style={{ background: "var(--accent)" }} />
                  Start building
                </div>
                <h2 style={{ marginTop: 20 }}>
                  Create a key and run your
                  <br />
                  first rewrite.
                </h2>
                <p>
                  Use the developer dashboard to create, rotate, revoke, and
                  monitor API keys for your account.
                </p>
              </div>
              <div className="cta-side">
                <Link href="/developers/keys" className="btn btn-accent">
                  Get your API key <span className="btn-arrow">→</span>
                </Link>
                <div className="meta">
                  Paid quota required before API keys can process rewrites.
                </div>
              </div>
            </div>
          </div>
        </div>
      </section>
    </main>
  );
}
