import Link from "next/link";
import type { Metadata } from "next";

import { SiteHeader } from "../../components/site-header";

export const metadata: Metadata = {
  title: "We launched",
  description:
    "Reply In My Voice is live — consumer email rewrites and a developer platform for embedding the engine into LLM tools.",
  openGraph: {
    title: "Reply In My Voice is live",
    description:
      "Consumer email rewrites and a developer platform for embedding the engine into LLM tools.",
    url: "https://replyinmyvoice.com/launch",
  },
  twitter: {
    card: "summary_large_image",
    title: "Reply In My Voice is live",
    description:
      "Consumer email rewrites and a developer platform for embedding the engine into LLM tools.",
  },
};

export default function LaunchPage() {
  return (
    <main className="rimv">
      <SiteHeader />
      <section className="page">
        <div className="wrap" style={{ maxWidth: 900 }}>
          <div className="page-head">
            <div className="eyebrow">
              <span className="dot" />
              Launch announcement
            </div>
            <h1>Reply In My Voice is live.</h1>
            <p className="lede">
              Today we&apos;re shipping two products at once: a consumer email
              rewrite engine that keeps your voice, and a developer platform for
              embedding that engine into LLM tools.
            </p>
          </div>

          <div className="card-grid" style={{ marginTop: 44 }}>
            <article className="v2card">
              <h2 style={{ fontSize: 20 }}>For people who write email</h2>
              <p>
                Rewrites that stay in your voice, preserve every fact, and refuse
                to ship a bad result. NZD $9/month with 3 free rewrites — no card
                up front.
              </p>
              <div style={{ marginTop: 16 }}>
                <Link href="/" className="btn btn-primary">
                  Try the rewrite engine <span className="btn-arrow">→</span>
                </Link>
              </div>
            </article>

            <article className="v2card">
              <h2 style={{ fontSize: 20 }}>For developers building agents</h2>
              <p>
                MCP server, Claude Code Skill, and REST API tiers for embedding
                the rewrite engine into Codex, Claude Code, Cursor, and your own
                agent workflows.
              </p>
              <div style={{ marginTop: 16 }}>
                <Link href="/developers" className="btn btn-ghost">
                  Developer docs <span className="btn-arrow">→</span>
                </Link>
              </div>
            </article>
          </div>

          <article className="v2card" style={{ marginTop: 16 }}>
            <h2 style={{ fontSize: 20 }}>What changed today</h2>
            <ul className="content-list">
              <li>Consumer product live at replyinmyvoice.com.</li>
              <li>MCP server v0.1 ready for use via npx.</li>
              <li>Claude Code Skill template available in the docs.</li>
              <li>REST API tiers and developer subscriptions shipping next.</li>
            </ul>
          </article>

          <article className="v2card" style={{ marginTop: 16 }}>
            <h2 style={{ fontSize: 20 }}>Thank you</h2>
            <p>
              Eight months of weekends went into this. To every early reader who
              shared a draft response, flagged a rough edge, or pointed out a way
              the rewrite didn&apos;t sound like them — thank you. Your feedback
              shaped the quality gate that refuses to ship a bad result. Stay in
              touch.
            </p>
            <p
              style={{
                marginTop: 12,
                fontFamily: "var(--mono)",
                fontSize: 13,
                color: "var(--muted)",
              }}
            >
              — Chuan · TimeAwake Ltd.
            </p>
          </article>
        </div>
      </section>
    </main>
  );
}
