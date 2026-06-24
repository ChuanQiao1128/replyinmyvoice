import Link from "next/link";
import type { Metadata } from "next";

import { SiteHeader } from "../../components/site-header";
import { DevelopersAnchorRedirect } from "./developers-anchor-redirect";

export const metadata: Metadata = {
  title: "Developers",
  description:
    "Reply In My Voice developer hub for REST API docs, MCP setup, API keys, pricing, and legal references.",
  openGraph: {
    title: "Reply In My Voice for developers",
    description:
      "Choose REST API or MCP access for fact-preserving reply rewrites with one shared Pro/API account balance.",
    url: "https://replyinmyvoice.com/developers",
    siteName: "Reply In My Voice",
    type: "website",
    images: "/og.png",
  },
  twitter: {
    card: "summary_large_image",
    title: "Reply In My Voice for developers",
    description:
      "Choose REST API or MCP access for fact-preserving reply rewrites with one shared Pro/API account balance.",
    images: "/og.png",
  },
};

const pathCards = [
  {
    title: "REST API",
    body: "Call the async rewrite API from your own product or backend: submit a draft, poll the job, and return a send-ready reply.",
    href: "/developers/api",
    cta: "REST API reference",
  },
  {
    title: "MCP server",
    body: "Connect Reply In My Voice as a tool inside Claude Code, Claude Desktop, Codex, Cursor, or any MCP host.",
    href: "/developers/mcp",
    cta: "MCP setup",
  },
];

const integrationBenefits = [
  {
    title: "REST API: build it into your product",
    benefits: [
      "Two-endpoint contract: submit a draft, poll the job. Plain JSON, no SDK required (an official TypeScript SDK exists if you want one).",
      "Only succeeded rewrites consume credits. Failed jobs, rejected requests, and polling are free.",
      "Safe retries with Idempotency-Key, documented rate-limit headers, and a published OpenAPI spec.",
    ],
  },
  {
    title: "MCP server: give it to your agent",
    benefits: [
      "Works inside Claude Code, Claude Desktop, Cursor, Codex, and any MCP host, no integration code.",
      "Install in one step: a Cursor deep link or a single claude mcp add command.",
      "Two simple tools (rewrite_email, get_rewrite_result) your agent can call mid-workflow.",
      "Same key and same balance as the REST API and the web workspace.",
    ],
  },
];

const commercialFacts = [
  {
    label: "Key",
    value: "One API key works for both paths",
  },
  {
    label: "Balance",
    value: "Website and API rewrites share one balance",
  },
  {
    label: "Plan",
    value: "Pro/API NZ$19.90/mo",
  },
  {
    label: "Quota",
    value: "90 rewrites/month shared across web + API",
  },
  {
    label: "Unit price",
    value: "≈ NZ$0.22 / rewrite",
  },
  {
    label: "Access",
    value: "No free API tier",
  },
  {
    label: "Rate limit",
    value: "60 requests/min per key",
  },
  {
    label: "Metering",
    value: "Succeeded rewrites consume quota; failed jobs and polling do not.",
  },
];

const legalLinks = [
  {
    title: "API terms",
    href: "/developers/terms",
  },
  {
    title: "Acceptable use",
    href: "/developers/acceptable-use",
  },
  {
    title: "Data handling",
    href: "/developers/data",
  },
];

const evaluationTrialClaim = "A trial code unlocks 3 trial rewrites, no card.";

export default function DevelopersPage() {
  return (
    <main className="rimv">
      <DevelopersAnchorRedirect />
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
              Submit a draft, poll the job, get back a send-ready reply in your
              own voice. One key for REST and MCP, and only succeeded rewrites
              count.
            </p>
            <div className="dev-hero-meta" aria-label="Developer highlights">
              <span>REST API</span>
              <span>MCP server</span>
              <span>Shared Pro/API quota</span>
            </div>
            <div className="hero-cta" style={{ marginTop: 28 }}>
              <Link href="/developers/api" className="btn btn-primary btn-lg">
                REST API reference <span className="btn-arrow">→</span>
              </Link>
              <Link href="/developers/mcp" className="btn btn-ghost btn-lg">
                MCP setup
              </Link>
              <Link href="/developers/keys" className="dev-text-link">
                Get your API key
              </Link>
            </div>
          </div>

          <section className="dev-section" aria-labelledby="integrate-heading">
            <div className="pp-includes-head" id="integrate-heading">
              Why integrate
            </div>
            <div className="dev-two-col">
              {integrationBenefits.map((card) => (
                <article className="v2card" key={card.title}>
                  <h3>{card.title}</h3>
                  <ul className="content-list">
                    {card.benefits.map((benefit) => (
                      <li key={benefit}>{benefit}</li>
                    ))}
                  </ul>
                </article>
              ))}
            </div>
          </section>

          <section className="dev-section" aria-labelledby="evaluation-heading">
            <div className="dev-callout">
              <div className="pp-includes-head" id="evaluation-heading">
                Evaluate before you pay
              </div>
              <p>
                Try the engine in the web workspace first. {evaluationTrialClaim}{" "}
                It is the same engine the API calls, so you can evaluate the
                output before choosing Pro/API.
              </p>
              <div className="hero-cta" style={{ marginTop: 16 }}>
                <Link href="/sign-up" className="btn btn-primary">
                  Try it in the workspace <span className="btn-arrow">→</span>
                </Link>
                <Link href="/pricing#pro" className="btn btn-ghost">
                  Unlock the API · NZ$19.90/mo
                </Link>
              </div>
            </div>
          </section>

          <section className="dev-section" aria-labelledby="paths-heading">
            <div className="pp-includes-head" id="paths-heading">
              Two ways to integrate
            </div>
            <div className="dev-two-col">
              {pathCards.map((card) => (
                <article className="v2card" key={card.title}>
                  <h3>{card.title}</h3>
                  <p>{card.body}</p>
                  <div
                    style={{
                      marginTop: 16,
                      display: "flex",
                      gap: 10,
                      flexWrap: "wrap",
                    }}
                  >
                    <Link href={card.href} className="btn btn-primary">
                      {card.cta} <span className="btn-arrow">→</span>
                    </Link>
                  </div>
                </article>
              ))}
            </div>
            <div className="dev-flow" aria-label="Developer integration steps">
              <div className="dev-step">
                <span>1</span>
                <div>
                  <h3>Get a key</h3>
                  <p>
                    Create one shared key for REST API and MCP.{" "}
                    <Link href="/developers/keys" className="dev-text-link">
                      Get your API key
                    </Link>
                    .
                  </p>
                </div>
              </div>
              <div className="dev-step">
                <span>2</span>
                <div>
                  <h3>Submit a draft</h3>
                  <p>
                    Follow the{" "}
                    <Link href="/developers/api#quickstart" className="dev-text-link">
                      API quickstart
                    </Link>{" "}
                    to create an async rewrite job.
                  </p>
                </div>
              </div>
              <div className="dev-step">
                <span>3</span>
                <div>
                  <h3>Poll the job</h3>
                  <p>
                    Read the job body until it is succeeded, then use the
                    returned rewrittenText in your product.
                  </p>
                </div>
              </div>
            </div>
          </section>

          <section className="dev-section" aria-labelledby="commercial-heading">
            <div className="pp-includes-head" id="commercial-heading">
              Developer facts
            </div>
            <div className="dev-table-wrap">
              <table className="dev-table">
                <tbody>
                  {commercialFacts.map((fact) => (
                    <tr key={fact.label}>
                      <th scope="row">{fact.label}</th>
                      <td>{fact.value}</td>
                    </tr>
                  ))}
                  <tr>
                    <th scope="row">Reference</th>
                    <td>
                      <a href="/api/v1/openapi.json" className="dev-text-link">
                        OpenAPI specification
                      </a>
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
            <div className="dev-callout">
              REST API and MCP access are included with Pro/API.{" "}
              <Link href="/pricing#pro" className="dev-text-link">
                See Pro/API pricing
              </Link>
              .
            </div>
          </section>

          <section className="dev-section" aria-labelledby="legal-heading">
            <div className="pp-includes-head" id="legal-heading">
              Legal
            </div>
            <div className="dev-callout">
              {legalLinks.map((link, index) => (
                <span key={link.href}>
                  {index > 0 ? " · " : ""}
                  <Link href={link.href} className="dev-text-link">
                    {link.title}
                  </Link>
                </span>
              ))}
            </div>
          </section>
        </div>
      </section>
    </main>
  );
}
