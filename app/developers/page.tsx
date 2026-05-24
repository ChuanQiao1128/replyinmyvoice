import Link from "next/link";
import type { Metadata } from "next";

import { SiteHeader } from "../../components/site-header";

export const metadata: Metadata = {
  title: "Developers",
  description:
    "Reply In My Voice for developers — an MCP server, Claude Code Skill, and HTTP API for embedding email rewrites into LLM tools. In active development.",
  openGraph: {
    title: "Reply In My Voice for developers",
    description:
      "The rewrite engine is coming to Codex, Claude Code, Cursor, and Continue.dev via an MCP server, a Skill, and an HTTP API.",
    url: "https://replyinmyvoice.com/developers",
  },
  twitter: {
    card: "summary_large_image",
    title: "Reply In My Voice for developers",
    description:
      "The rewrite engine is coming to Codex, Claude Code, Cursor, and Continue.dev via an MCP server, a Skill, and an HTTP API.",
  },
};

const roadmap = [
  {
    tag: "MCP server",
    pill: "Building now",
    pillClass: "now",
    handle: "@replyinmyvoice/mcp-server",
    body: "A Model Context Protocol server that exposes the rewrite engine to any MCP-aware client. Point Claude Code, Codex, Cursor, or Continue.dev at it and ask your agent to rewrite a reply in place.",
    tools: ["rewrite_email", "analyze_signal", "list_scenarios"],
  },
  {
    tag: "Claude Code Skill",
    pill: "Building now",
    pillClass: "now",
    handle: "replyinmyvoice-rewrite",
    body: "A first-party Skill that bundles the prompts and tool calls used on the site, so one instruction drives a full rewrite. Install once, then say /rewrite in any project.",
    tools: [],
  },
  {
    tag: "HTTP REST API",
    pill: "Planned",
    pillClass: "planned",
    handle: "POST /v1/rewrite",
    body: "A standalone REST endpoint for server-to-server use — the same rewrite engine, language-agnostic, with API-key auth for active Pro/API accounts.",
    tools: [],
  },
  {
    tag: "Metered API keys",
    pill: "Exploring",
    pillClass: "exploring",
    handle: "rmv_live_…",
    body: "Per-key usage, quotas, and billing so teams can wire rewrites into their own products. We're still shaping how keys and limits should work.",
    tools: [],
  },
];

const installSnippets = [
  {
    label: "Claude Code",
    body: `claude mcp add reply-in-my-voice -- npx @replyinmyvoice/mcp-server`,
  },
  {
    label: "Codex CLI",
    body: `codex mcp add reply-in-my-voice -- npx @replyinmyvoice/mcp-server`,
  },
  {
    label: "Cursor",
    body: `// ~/.cursor/mcp.json
{
  "mcpServers": {
    "reply-in-my-voice": {
      "command": "npx",
      "args": ["@replyinmyvoice/mcp-server"],
      "env": { "REPLY_IN_MY_VOICE_API_KEY": "rmv_live_..." }
    }
  }
}`,
  },
  {
    label: "Continue.dev",
    body: `# ~/.continue/config.yaml
mcpServers:
  - name: reply-in-my-voice
    command: npx
    args: ["@replyinmyvoice/mcp-server"]
    env:
      REPLY_IN_MY_VOICE_API_KEY: rmv_live_...`,
  },
];

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
            <h1>The developer platform is on the way.</h1>
            <p className="lede">
              The same rewrite engine behind replyinmyvoice.com is becoming
              available to LLM tools — an MCP server, a Claude Code Skill, and an
              HTTP API. Here&apos;s what we&apos;re building and what it will look
              like to use.
            </p>
            <div className="dev-badge">
              <span className="dot" />
              In active development · not yet published
            </div>
            <div className="hero-cta" style={{ marginTop: 28 }}>
              <Link href="/sign-up" className="btn btn-primary btn-lg">
                Get early access <span className="btn-arrow">→</span>
              </Link>
              <a
                href="https://github.com/ChuanQiao1128/replyinmyvoice"
                className="btn btn-ghost btn-lg"
              >
                Follow on GitHub
              </a>
            </div>
          </div>

          <div className="pp-includes-head">What we&apos;re building</div>
          <div className="card-grid">
            {roadmap.map((item) => (
              <article className="v2card" key={item.tag}>
                <div className="v2card-head">
                  <div className="eyebrow" style={{ color: "var(--muted)" }}>
                    {item.tag}
                  </div>
                  <span className={"dev-pill " + item.pillClass}>
                    {item.pill}
                  </span>
                </div>
                <h2>
                  <span className="mono">{item.handle}</span>
                </h2>
                <p>{item.body}</p>
                {item.tools.length ? (
                  <div className="dev-tools">
                    {item.tools.map((tool) => (
                      <span className="dev-tool" key={tool}>
                        {tool}
                      </span>
                    ))}
                  </div>
                ) : null}
              </article>
            ))}
          </div>

          <div className="pp-includes-head">A preview of the developer experience</div>
          <article className="v2card">
            <p style={{ marginTop: 0 }}>
              Wiring it in will look like this once the MCP server ships. These
              commands are a preview — the package isn&apos;t published yet, so
              they won&apos;t resolve today.
            </p>
            <div className="card-grid">
              {installSnippets.map((snippet) => (
                <div key={snippet.label}>
                  <div
                    className="mono"
                    style={{ fontSize: 12, color: "var(--ink-2)" }}
                  >
                    {snippet.label}
                  </div>
                  <pre className="code-block">
                    <code>{snippet.body}</code>
                  </pre>
                </div>
              ))}
            </div>
            <div className="dev-note">
              {"// Preview only · names and APIs may change before launch"}
            </div>
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
                  Create an account and we&apos;ll email you the moment the MCP
                  server, Skill, and API are ready for early access.
                </p>
              </div>
              <div className="cta-side">
                <Link href="/sign-up" className="btn btn-accent">
                  Get early access <span className="btn-arrow">→</span>
                </Link>
                <div className="meta">
                  No card required · 3 free rewrites to try the engine
                </div>
              </div>
            </div>
          </div>
        </div>
      </section>
    </main>
  );
}
