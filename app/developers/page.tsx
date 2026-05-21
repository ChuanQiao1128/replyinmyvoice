import Link from "next/link";
import type { Metadata } from "next";

import { LinkButton } from "../../components/ui/button";
import { Card } from "../../components/ui/card";
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
    <main className="min-h-screen bg-paper text-ink">
      <SiteHeader />
      <section className="mx-auto max-w-6xl px-6 pt-14">
        <p className="text-sm font-semibold uppercase tracking-[0.18em] text-clay">
          Developers
        </p>
        <h1 className="mt-3 text-4xl font-semibold md:text-5xl">
          Bring the rewrite engine into your tools.
        </h1>
        <p className="mt-4 max-w-2xl leading-7 text-ink/65">
          The same rewrite engine that powers replyinmyvoice.com is available
          to LLM tools through an MCP server and a Claude Code Skill. Wire it
          into Codex, Claude Code, Cursor, or Continue.dev and ask your agent
          to rewrite a reply directly.
        </p>
        <div className="mt-8 flex flex-wrap gap-3">
          <LinkButton href="/sign-up" variant="primary">
            Create an account
          </LinkButton>
          <Link
            href="https://github.com/ChuanQiao1128/replyinmyvoice/tree/main/packages/mcp-server"
            className="rounded-md border border-line px-4 py-2 text-sm font-medium text-ink/80 hover:text-ink"
          >
            View MCP source
          </Link>
        </div>
      </section>

      <section className="mx-auto mt-16 max-w-6xl px-6">
        <Card className="p-6 md:p-8">
          <p className="text-sm font-semibold uppercase tracking-[0.18em] text-clay">
            MCP server
          </p>
          <h2 className="mt-3 text-3xl font-semibold md:text-4xl">
            @replyinmyvoice/mcp-server
          </h2>
          <p className="mt-4 max-w-2xl leading-7 text-ink/65">
            A Model Context Protocol server that exposes three tools:
            <span className="font-mono"> rewrite_email</span>,
            <span className="font-mono"> analyze_signal</span>, and
            <span className="font-mono"> list_scenarios</span>. Point any
            MCP-aware client at it and authenticate with your API key.
          </p>
          <div className="mt-6 grid gap-4 md:grid-cols-2">
            {installSnippets.map((snippet) => (
              <div key={snippet.label} className="rounded-lg border border-line bg-white/70 p-4">
                <p className="text-sm font-semibold text-ink">{snippet.label}</p>
                <pre className="mt-2 overflow-x-auto rounded-md bg-ink/95 p-3 text-xs leading-5 text-paper">
                  <code>{snippet.body}</code>
                </pre>
              </div>
            ))}
          </div>
        </Card>
      </section>

      <section className="mx-auto mt-12 max-w-6xl px-6">
        <Card className="p-6 md:p-8">
          <p className="text-sm font-semibold uppercase tracking-[0.18em] text-clay">
            Claude Code Skill
          </p>
          <h2 className="mt-3 text-3xl font-semibold md:text-4xl">
            replyinmyvoice-rewrite
          </h2>
          <p className="mt-4 max-w-2xl leading-7 text-ink/65">
            A first-party Skill bundles the prompt patterns and tool calls used
            on the site so Claude Code can drive the rewrite engine with one
            instruction. Install once, then say
            <span className="font-mono"> /rewrite this email</span> in any
            project.
          </p>
          <div className="mt-6 rounded-lg border border-line bg-white/70 p-4">
            <p className="text-sm font-semibold text-ink">Install</p>
            <pre className="mt-2 overflow-x-auto rounded-md bg-ink/95 p-3 text-xs leading-5 text-paper">
              <code>{`# Bundled with the MCP server\nclaude skill install @replyinmyvoice/skill-rewrite`}</code>
            </pre>
          </div>
        </Card>
      </section>

      <section className="mx-auto mt-12 max-w-6xl px-6 pb-20">
        <Card className="p-6 md:p-8">
          <p className="text-sm font-semibold uppercase tracking-[0.18em] text-clay">
            HTTP API
          </p>
          <h2 className="mt-3 text-3xl font-semibold md:text-4xl">
            REST endpoint coming soon
          </h2>
          <p className="mt-4 max-w-2xl leading-7 text-ink/65">
            A standalone REST API for server-to-server use is in progress.
            Want early access? Sign up below — we will email you when the API
            is ready.
          </p>
          <div className="mt-6">
            <LinkButton href="/sign-up" variant="primary">
              Get notified
            </LinkButton>
          </div>
        </Card>
      </section>
    </main>
  );
}
