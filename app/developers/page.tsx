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
    cta: "Open API reference",
    secondaryHref: "/developers/api#quickstart",
    secondaryCta: "API quickstart",
  },
  {
    title: "MCP server",
    body: "Connect Reply In My Voice as a tool inside Claude Code, Claude Desktop, Codex, Cursor, or any MCP host.",
    href: "/developers/mcp",
    cta: "Open MCP setup",
    secondaryHref: "/developers/keys",
    secondaryCta: "Manage keys",
  },
];

const foundationFacts = [
  "One API key works for REST API and MCP.",
  "Website and API rewrites share one balance.",
  "Succeeded rewrites consume quota; failed jobs and polling do not.",
];

const commercialFacts = [
  "Pro/API NZ$19.90/mo",
  "90 rewrites/month shared across web + API",
  "No free API tier",
  "60 requests/min per key",
];

const legalLinks = [
  {
    title: "API Terms of Use",
    body: "Eligibility, key security, paid quota, metering, service limits, and account changes.",
    href: "/developers/terms",
  },
  {
    title: "Acceptable Use Policy",
    body: "Responsible content, system load, access sharing, and customer obligations.",
    href: "/developers/acceptable-use",
  },
  {
    title: "Data & Retention",
    body: "API request storage, async job records, the bounded retention window, deletion, and processors.",
    href: "/developers/data",
  },
];

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
              Choose the REST API when you are building into your own app, or
              use MCP when you want the rewrite tool inside an LLM host. Both
              paths use the same paid account balance.
            </p>
            <div className="dev-hero-meta" aria-label="Developer highlights">
              <span>REST API</span>
              <span>MCP server</span>
              <span>Shared Pro/API quota</span>
            </div>
            <div className="hero-cta" style={{ marginTop: 28 }}>
              <Link href="/developers/api" className="btn btn-primary btn-lg">
                REST API <span className="btn-arrow">→</span>
              </Link>
              <Link href="/developers/mcp" className="btn btn-ghost btn-lg">
                MCP setup
              </Link>
              <Link href="/developers/api#quickstart" className="btn btn-ghost btn-lg">
                API quickstart
              </Link>
              <a href="/api/v1/openapi.json" className="btn btn-ghost btn-lg">
                OpenAPI specification
              </a>
            </div>
          </div>

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
                    <Link href={card.secondaryHref} className="btn btn-ghost">
                      {card.secondaryCta}
                    </Link>
                  </div>
                </article>
              ))}
            </div>
          </section>

          <section className="dev-section" aria-labelledby="foundation-heading">
            <div className="pp-includes-head" id="foundation-heading">
              Shared key foundation
            </div>
            <div className="dev-callout">
              One API key works for both paths, and your website and API
              rewrites share one balance.{" "}
              <Link href="/developers/keys" className="dev-text-link">
                Get your API key
              </Link>
              .
            </div>
            <div className="card-grid">
              {foundationFacts.map((fact) => (
                <article className="v2card" key={fact}>
                  <h3>{fact}</h3>
                </article>
              ))}
            </div>
          </section>

          <section className="dev-section" aria-labelledby="commercial-heading">
            <div className="pp-includes-head" id="commercial-heading">
              Commercial facts
            </div>
            <div className="dev-meta-grid">
              {commercialFacts.map((fact) => (
                <article className="v2card" key={fact}>
                  <h3>{fact}</h3>
                </article>
              ))}
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
            <div className="card-grid">
              {legalLinks.map((link) => (
                <article className="v2card" key={link.href}>
                  <h3>{link.title}</h3>
                  <p>{link.body}</p>
                  <p>
                    <Link href={link.href} className="dev-text-link">
                      Read the page
                    </Link>
                  </p>
                </article>
              ))}
            </div>
          </section>
        </div>
      </section>
    </main>
  );
}
