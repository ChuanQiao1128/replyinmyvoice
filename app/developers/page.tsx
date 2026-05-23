import Link from "next/link";
import type { Metadata } from "next";

import { SiteHeader } from "../../components/site-header";

export const metadata: Metadata = {
  title: "Developers",
  description:
    "Reply In My Voice developer platform — MCP server and Claude Code Skill for embedding email rewrites into LLM tools.",
  openGraph: {
    title: "Reply In My Voice for developers",
    description:
      "Embed the rewrite engine into Codex, Claude Code, Cursor, and Continue.dev via the MCP server and Skill.",
    url: "https://replyinmyvoice.com/developers",
  },
  twitter: {
    card: "summary_large_image",
    title: "Reply In My Voice for developers",
    description:
      "Embed the rewrite engine into Codex, Claude Code, Cursor, and Continue.dev via the MCP server and Skill.",
  },
};

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
            <h1>Bring the rewrite engine into your tools.</h1>
            <p className="lede">
              The same rewrite engine that powers replyinmyvoice.com is available
              to LLM tools through an MCP server and a Claude Code Skill. Wire it
              into Codex, Claude Code, Cursor, or Continue.dev and ask your agent
              to rewrite a reply directly.
            </p>
            <div className="hero-cta" style={{ marginTop: 28 }}>
              <Link href="/sign-up" className="btn btn-primary btn-lg">
                Create an account <span className="btn-arrow">→</span>
              </Link>
              <a
                href="https://github.com/ChuanQiao1128/replyinmyvoice/tree/main/packages/mcp-server"
                className="btn btn-ghost btn-lg"
              >
                View MCP source
              </a>
            </div>
          </div>

          <div className="card-stack">
            <article className="v2card">
              <div className="eyebrow" style={{ color: "var(--muted)" }}>
                MCP server
              </div>
              <h2>
                <span className="mono">@replyinmyvoice/mcp-server</span>
              </h2>
              <p>
                A Model Context Protocol server that exposes three tools:{" "}
                <span className="mono">rewrite_email</span>,{" "}
                <span className="mono">analyze_signal</span>, and{" "}
                <span className="mono">list_scenarios</span>. Point any MCP-aware
                client at it and authenticate with your API key.
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
            </article>

            <article className="v2card">
              <div className="eyebrow" style={{ color: "var(--muted)" }}>
                Claude Code Skill
              </div>
              <h2>
                <span className="mono">replyinmyvoice-rewrite</span>
              </h2>
              <p>
                A first-party Skill bundles the prompt patterns and tool calls
                used on the site so Claude Code can drive the rewrite engine with
                one instruction. Install once, then say{" "}
                <span className="mono">/rewrite this email</span> in any project.
              </p>
              <pre className="code-block">
                <code>{`# Bundled with the MCP server\nclaude skill install @replyinmyvoice/skill-rewrite`}</code>
              </pre>
            </article>

            <article className="v2card">
              <div className="eyebrow" style={{ color: "var(--muted)" }}>
                HTTP API
              </div>
              <h2>REST endpoint — coming soon</h2>
              <p>
                A standalone REST API for server-to-server use is in progress.
                Want early access? Create an account and we&apos;ll email you
                when the API is ready.
              </p>
              <div style={{ marginTop: 16 }}>
                <Link href="/sign-up" className="btn btn-primary">
                  Get notified <span className="btn-arrow">→</span>
                </Link>
              </div>
            </article>
          </div>
        </div>
      </section>
    </main>
  );
}
