import type { Metadata } from "next";

import { LinkButton } from "../../components/ui/button";
import { Card } from "../../components/ui/card";
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
    <div className="min-h-screen bg-background text-foreground">
      <SiteHeader />
      <main className="mx-auto max-w-3xl px-6 py-16 space-y-12">
        <header className="space-y-4">
          <p className="text-sm uppercase tracking-wide text-muted-foreground">
            Launch announcement
          </p>
          <h1 className="text-4xl font-semibold tracking-tight">
            Reply In My Voice is live.
          </h1>
          <p className="text-lg text-muted-foreground">
            Today we are shipping two products at once: a consumer email
            rewrite engine that keeps your voice, and a developer platform for
            embedding that engine into LLM tools.
          </p>
        </header>

        <section className="grid gap-6 md:grid-cols-2">
          <Card className="p-6 space-y-4">
            <h2 className="text-xl font-semibold">For people who write email</h2>
            <p className="text-sm text-muted-foreground">
              Rewrites that stay in your voice, preserve every fact, and refuse
              to ship a bad result. NZ$9 per month with a free trial — no card
              up front.
            </p>
            <LinkButton href="/" variant="primary">
              Try the rewrite engine
            </LinkButton>
          </Card>

          <Card className="p-6 space-y-4">
            <h2 className="text-xl font-semibold">For developers building agents</h2>
            <p className="text-sm text-muted-foreground">
              MCP server, Claude Code Skill, and REST API tiers for embedding
              the rewrite engine into Codex, Claude Code, Cursor, and your own
              agent workflows.
            </p>
            <LinkButton href="/developers" variant="secondary">
              Developer docs
            </LinkButton>
          </Card>
        </section>

        <section className="space-y-3">
          <h2 className="text-2xl font-semibold">What changed today</h2>
          <ul className="list-disc pl-6 space-y-1 text-sm text-muted-foreground">
            <li>Consumer product live at replyinmyvoice.com.</li>
            <li>MCP server v0.1 ready for use via npx.</li>
            <li>Claude Code Skill template available in the docs.</li>
            <li>REST API tiers and developer subscriptions shipping next.</li>
          </ul>
        </section>

        <section className="space-y-3">
          <h2 className="text-2xl font-semibold">Thank you</h2>
          <p className="text-sm text-muted-foreground">
            Eight months of weekends went into this. To every early reader who
            shared a draft response, flagged a rough edge, or pointed out a
            way the rewrite did not sound like them — thank you. Your
            feedback shaped the quality gate that refuses to ship a bad
            result. Stay in touch.
          </p>
          <p className="text-sm text-muted-foreground">— Chuan · TimeAwake Ltd</p>
        </section>
      </main>
    </div>
  );
}
