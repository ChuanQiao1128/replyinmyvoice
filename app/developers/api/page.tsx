import Link from "next/link";
import type { Metadata } from "next";
import type { ReactNode } from "react";

import { SiteHeader } from "../../../components/site-header";
import openApiSpec from "../../../public/openapi.json";

export const metadata: Metadata = {
  title: "REST API reference",
  description:
    "Reply In My Voice REST API reference for async, fact-preserving reply rewrites in CRM, help desk, and support workflows.",
  openGraph: {
    title: "Reply In My Voice REST API reference",
    description:
      "Add async, fact-preserving reply rewrites to your own product with a paid-quota API key.",
    url: "https://replyinmyvoice.com/developers/api",
    siteName: "Reply In My Voice",
    type: "website",
    images: "/og.png",
  },
  twitter: {
    card: "summary_large_image",
    title: "Reply In My Voice REST API reference",
    description:
      "Add async, fact-preserving reply rewrites to your own product with a paid-quota API key.",
    images: "/og.png",
  },
};

const errorRows = [
  {
    status: "400",
    code: "invalid_request",
    cause:
      "Missing JSON, missing draft, empty draft, a draft under 10 characters, or a draft value that is not text.",
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
    status: "404",
    code: "not_found",
    cause: "Unknown job id, or a job owned by another account/key.",
  },
  {
    status: "409",
    code: "idempotency_conflict",
    cause: "The same Idempotency-Key was reused with a different request body.",
  },
  {
    status: "413",
    code: "payload_too_large",
    cause: "Request body exceeded the 64 KB transport limit.",
  },
  {
    status: "429",
    code: "rate_limited",
    cause: "The key has reached its 60 requests per minute limit.",
  },
  {
    status: "500",
    code: "rewrite_failed",
    cause: "Submit-time failure before a rewrite job is accepted.",
  },
  {
    status: "502",
    code: "proxy_request_failed",
    cause: "Gateway error between the website API route and backend.",
  },
];

const exampleJobId = "7f3c2c1a-9d4e-4b8a-b1f2-3a5d8e9c0f11";
const shortDraftQuickstartNote =
  "Drafts under 10 characters are rejected before a job is accepted and are uncharged.";
const rateLimitRetryAfterNote =
  "429 responses also include Retry-After so clients know how many seconds to wait.";
const supportContextNote =
  "If you contact support about an API response, include the response timestamp and endpoint.";
const idempotencyLimitNote = "Idempotency-Key must be 120 characters or fewer.";
const signalRequestNote = "signal is not accepted in the request.";
const periodEndNote =
  "periodEnd is string | null; paid subscription windows return a timestamp, while credit-only windows may return null.";
const versioningNote =
  "The REST API is path-versioned under /api/v1. Breaking changes ship under a new path version so existing v1 clients can keep their integration stable while they migrate.";
const whatToExpectNote =
  "Jobs usually finish in seconds and may take up to about 50 s under load. If a job is still processing after about 60 s, treat the attempt as failed; it is uncharged. Resubmit with a fresh Idempotency-Key.";

const httpMethods = ["get", "post", "put", "patch", "delete"] as const;

type HttpMethod = (typeof httpMethods)[number];
type OpenApiParameterRef = { $ref: string };
type OpenApiParameter = {
  name: string;
  in: string;
  required?: boolean;
};
type OpenApiOperation = {
  summary?: string;
  description?: string;
  security?: Array<Record<string, unknown>>;
  parameters?: Array<OpenApiParameter | OpenApiParameterRef>;
  requestBody?: {
    required?: boolean;
  };
  responses?: Record<string, { description?: string }>;
};
type OpenApiPathItem = Partial<Record<HttpMethod, OpenApiOperation>>;
type OpenApiRow = {
  key: string;
  method: string;
  path: string;
  summary: string;
  description: string;
  inputs: string[];
  responses: string[];
};

const openApiPaths = openApiSpec.paths as Record<string, OpenApiPathItem>;
const openApiParameters = openApiSpec.components.parameters as Record<
  string,
  OpenApiParameter
>;

function refName(ref: string) {
  return ref.split("/").at(-1) ?? ref;
}

function parameterLabel(parameter: OpenApiParameter | OpenApiParameterRef) {
  const resolved =
    "$ref" in parameter ? openApiParameters[refName(parameter.$ref)] : parameter;

  if (!resolved) {
    return "$ref" in parameter ? refName(parameter.$ref) : parameter.name;
  }

  return `${resolved.name} ${resolved.required ? "required" : "optional"} ${
    resolved.in
  }`;
}

function operationInputs(operation: OpenApiOperation) {
  const inputs = operation.parameters?.map(parameterLabel) ?? [];

  if (operation.security?.length) {
    inputs.unshift("Authorization bearer key");
  }

  if (operation.requestBody) {
    inputs.push(
      operation.requestBody.required ? "JSON body required" : "JSON body optional",
    );
  }

  return inputs.length > 0 ? inputs : ["No parameters"];
}

function operationResponses(operation: OpenApiOperation) {
  return Object.entries(operation.responses ?? {}).map(([status, response]) =>
    response.description ? `${status}: ${response.description}` : status,
  );
}

const openApiRows: OpenApiRow[] = Object.entries(openApiPaths).flatMap(
  ([path, pathItem]) =>
    httpMethods.flatMap((method) => {
      const operation = pathItem[method];

      if (!operation) {
        return [];
      }

      return [
        {
          key: `${method}:${path}`,
          method: method.toUpperCase(),
          path,
          summary: operation.summary ?? `${method.toUpperCase()} ${path}`,
          description: operation.description ?? "",
          inputs: operationInputs(operation),
          responses: operationResponses(operation),
        },
      ];
    }),
);

const submitCurl = `curl https://replyinmyvoice.com/api/v1/rewrite \\
  -H "Authorization: Bearer rmv_live_xxx" \\
  -H "Content-Type: application/json" \\
  -H "Idempotency-Key: crm-reply-123" \\
  -d '{ "draft": "Sam, your order is delayed and ships next week." }'`;

const submitNode = `const apiKey = process.env.RIMV_API_KEY ?? "rmv_live_xxx";

const response = await fetch("https://replyinmyvoice.com/api/v1/rewrite", {
  method: "POST",
  headers: {
    Authorization: "Bearer " + apiKey,
    "Content-Type": "application/json",
    "Idempotency-Key": "crm-reply-123",
  },
  body: JSON.stringify({
    draft: "Sam, your order is delayed and ships next week.",
  }),
});

console.log(response.status, response.headers.get("Location"));
console.log(await response.json());`;

const submitPython = `import os
import requests

api_key = os.environ.get("RIMV_API_KEY", "rmv_live_xxx")

response = requests.post(
    "https://replyinmyvoice.com/api/v1/rewrite",
    headers={
        "Authorization": f"Bearer {api_key}",
        "Content-Type": "application/json",
        "Idempotency-Key": "crm-reply-123",
    },
    json={"draft": "Sam, your order is delayed and ships next week."},
    timeout=30,
)

print(response.status_code, response.headers.get("Location"))
print(response.json())`;

const sdkInstall = `npm install replyinmyvoice-api`;

const sdkQuickstart = `import { RimvApiError, createClient } from "replyinmyvoice-api";

const client = createClient({
  apiKey: process.env.RIMV_API_KEY!,
});

try {
  const { rewrittenText } = await client.rewrite(
    "Sam, your order is delayed and ships next week.",
  );

  console.log(rewrittenText);
} catch (error) {
  if (error instanceof RimvApiError) {
    console.error(error.code, error.message, error.status);
  }

  throw error;
}`;

const submitResponse = `HTTP/1.1 202 Accepted
Location: /api/v1/rewrite/${exampleJobId}
X-RateLimit-Limit: 60
X-RateLimit-Remaining: 59
X-RateLimit-Reset: 1812345678

{
  "id": "${exampleJobId}",
  "status": "processing"
}`;

const pollCurl = `curl https://replyinmyvoice.com/api/v1/rewrite/${exampleJobId} \\
  -H "Authorization: Bearer rmv_live_xxx"`;

const pollNode = `const apiKey = process.env.RIMV_API_KEY ?? "rmv_live_xxx";
const id = "${exampleJobId}";

const response = await fetch(
  "https://replyinmyvoice.com/api/v1/rewrite/" + encodeURIComponent(id),
  {
    headers: {
      Authorization: "Bearer " + apiKey,
    },
  },
);

console.log(await response.json());`;

const pollPython = `import os
import requests

api_key = os.environ.get("RIMV_API_KEY", "rmv_live_xxx")
job_id = "${exampleJobId}"

response = requests.get(
    f"https://replyinmyvoice.com/api/v1/rewrite/{job_id}",
    headers={"Authorization": f"Bearer {api_key}"},
    timeout=30,
)

print(response.json())`;

const pollProcessing = `{
  "id": "${exampleJobId}",
  "status": "processing"
}`;

const pollSucceeded = `{
  "id": "${exampleJobId}",
  "status": "succeeded",
  "rewrittenText": "Hi Sam, thanks for your patience. Your order is running a little behind and ships next week.",
  "signal": {
    "draft": 78,
    "rewrite": 24
  }
}`;

const pollFailed = `{
  "id": "${exampleJobId}",
  "status": "failed",
  "error": {
    "code": "engine_unavailable",
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

const rateLimitedResponse = `HTTP/1.1 429 Too Many Requests
Retry-After: 42
X-RateLimit-Limit: 60
X-RateLimit-Remaining: 0
X-RateLimit-Reset: 1812345678

{
  "error": {
    "code": "rate_limited",
    "message": "Request limit reached. Please retry later."
  }
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
              REST API
            </div>
            <h1>REST API reference</h1>
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
              <Link href="/developers/mcp" className="btn btn-ghost btn-lg">
                MCP setup
              </Link>
              <a href="#quickstart" className="btn btn-ghost btn-lg">
                Quickstart
              </a>
              <a href="#openapi" className="btn btn-ghost btn-lg">
                Browse full spec
              </a>
            </div>
          </div>

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
                    <p>{shortDraftQuickstartNote}</p>
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
                <CodeBlock label="Submit request - curl">{submitCurl}</CodeBlock>
                <CodeBlock label="Submit request - Node (fetch)">
                  {submitNode}
                </CodeBlock>
                <CodeBlock label="Submit request - Python (requests)">
                  {submitPython}
                </CodeBlock>
                <CodeBlock label="Submit response" status="202 Accepted">
                  {submitResponse}
                </CodeBlock>
                <CodeBlock label="Poll request - curl">{pollCurl}</CodeBlock>
                <CodeBlock label="Poll request - Node (fetch)">
                  {pollNode}
                </CodeBlock>
                <CodeBlock label="Poll request - Python (requests)">
                  {pollPython}
                </CodeBlock>
                <CodeBlock label="Poll response">{pollSucceeded}</CodeBlock>
              </div>
            </div>
            <div className="dev-two-col">
              <article className="v2card">
                <h3>Official SDK</h3>
                <p>
                  Install the published TypeScript client when you want the
                  submit-and-poll loop handled for you. The <code>rewrite()</code>{" "}
                  helper submits the draft, polls until completion, and returns
                  <code>rewrittenText</code>.
                </p>
                <p>
                  Handle <code>RimvApiError</code> for API errors, rate limits,
                  quota errors, failed jobs, and client-side timeouts.
                </p>
                <a
                  href="https://www.npmjs.com/package/replyinmyvoice-api"
                  className="dev-text-link"
                >
                  View replyinmyvoice-api on npm
                </a>
              </article>
              <div className="api-panel dev-api-panel">
                <div className="api-bar">
                  <span className="dots">
                    <i />
                    <i />
                    <i />
                  </span>
                  <span className="bar-label">TypeScript client</span>
                </div>
                <CodeBlock label="Install">{sdkInstall}</CodeBlock>
                <CodeBlock label="Auto-polling SDK rewrite">
                  {sdkQuickstart}
                </CodeBlock>
              </div>
            </div>
          </section>

          <section className="dev-section" id="auth" aria-labelledby="auth-heading">
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
                  The draft must be at least 10 characters, at or below 300
                  words, and at or below 2400 chars. {signalRequestNote}
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
                    {periodEndNote}
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
            <div className="dev-callout">{versioningNote}</div>

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

          <section className="dev-section" id="openapi" aria-labelledby="openapi-heading">
            <div className="pp-includes-head" id="openapi-heading">
              Browse the full spec
            </div>
            <p className="dev-section-note">
              This read-only view is generated from the checked-in OpenAPI
              specification. It describes endpoints, required inputs, and
              responses without running calls or collecting key values.
            </p>
            <div
              className="dev-table-wrap openapi-table-wrap"
              aria-label="Read-only OpenAPI operations"
            >
              <table className="dev-table openapi-table">
                <thead>
                  <tr>
                    <th>Method</th>
                    <th>Endpoint</th>
                    <th>Summary</th>
                    <th>Inputs</th>
                    <th>Responses</th>
                  </tr>
                </thead>
                <tbody>
                  {openApiRows.map((row) => (
                    <tr key={row.key}>
                      <td>
                        <span className="method-badge">{row.method}</span>
                      </td>
                      <td>
                        <code>{row.path}</code>
                      </td>
                      <td>
                        <strong>{row.summary}</strong>
                        {row.description ? <p>{row.description}</p> : null}
                      </td>
                      <td>
                        <ul className="openapi-list">
                          {row.inputs.map((input) => (
                            <li key={input}>{input}</li>
                          ))}
                        </ul>
                      </td>
                      <td>
                        <ul className="openapi-list">
                          {row.responses.map((response) => (
                            <li key={response}>{response}</li>
                          ))}
                        </ul>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <p className="openapi-caption">
              OpenAPI specification:{" "}
              <a href="/api/v1/openapi.json" className="dev-text-link">
                Raw OpenAPI JSON
              </a>
            </p>
          </section>

          <section className="dev-section" id="errors" aria-labelledby="errors-heading">
            <div className="pp-includes-head" id="errors-heading">
              Errors, limits, and idempotency
            </div>
            <div className="dev-callout">
              {
                "Rejected requests, failed jobs, and timeouts are uncharged; only a succeeded rewrite costs 1."
              }
            </div>
            <div className="dev-callout">{supportContextNote}</div>
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
                  they receive <code>429 rate_limited</code>.{" "}
                  {rateLimitRetryAfterNote}
                </p>
                <CodeBlock label="429 context" status="429 Too Many Requests">
                  {rateLimitedResponse}
                </CodeBlock>
              </article>
              <article className="v2card">
                <h3>Idempotency-Key</h3>
                <p>
                  Send one <code>Idempotency-Key</code> per logical submit. The
                  same key and same draft return the same job id; the same key
                  with a changed body returns{" "}
                  <code>409 idempotency_conflict</code>. {idempotencyLimitNote}
                </p>
              </article>
            </div>
          </section>

          <section className="dev-section" id="guides" aria-labelledby="guides-heading">
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
              <article className="v2card">
                <h3>What to expect</h3>
                <p>{whatToExpectNote}</p>
              </article>
            </div>
          </section>

          <section className="dev-section" id="pricing" aria-labelledby="pricing-heading">
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

        </div>
      </section>
    </main>
  );
}
