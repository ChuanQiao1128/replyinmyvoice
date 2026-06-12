import type { Metadata } from "next";
import Link from "next/link";

import {
  DeveloperUpsell,
  PageHeader,
} from "../../../components/app/shell/shell-primitives";
import { isDeveloperTierStatus } from "../../../components/app/shell/shell-types";
import styles from "../../../components/app/shell/shell.module.css";
import { fetchAzureAccountSummary } from "../../../lib/azure-api";

export const dynamic = "force-dynamic";
export const metadata: Metadata = { title: "Connect" };

const CLAUDE_CODE = `claude mcp add replyinmyvoice \\
  --env REPLY_IN_MY_VOICE_API_KEY=rmv_live_xxx \\
  -- npx -y @replyinmyvoiceashuman/mcp-server`;

const JSON_CONFIG = `{
  "mcpServers": {
    "replyinmyvoice": {
      "command": "npx",
      "args": ["-y", "@replyinmyvoiceashuman/mcp-server"],
      "env": { "REPLY_IN_MY_VOICE_API_KEY": "rmv_live_xxx" }
    }
  }
}`;

const CURL = `curl https://replyinmyvoice.com/api/v1/rewrite \\
  -H "Authorization: Bearer rmv_live_xxx" \\
  -H "Content-Type: application/json" \\
  -d '{"draft":"your rough reply"}'`;

function ConfigCard({ title, code }: { title: string; code: string }) {
  return (
    <div className={styles.configCard}>
      <h2 className={styles.configTitle}>{title}</h2>
      <pre className={styles.codeBlock}>{code}</pre>
    </div>
  );
}

export default async function ConnectPage() {
  const account = await fetchAzureAccountSummary();
  const subscriptionStatus = account?.subscriptionStatus ?? "inactive";

  if (!isDeveloperTierStatus(subscriptionStatus)) {
    return (
      <>
        <PageHeader
          title="Connect"
          description="Use Reply In My Voice inside Claude Code, Claude Desktop, Codex, Cursor, or any MCP host — or call the REST API directly."
        />
        <DeveloperUpsell />
      </>
    );
  }

  return (
    <>
      <PageHeader
        title="Connect"
        description="Use Reply In My Voice inside Claude Code, Claude Desktop, Codex, Cursor, or any MCP host — or call the REST API directly. One key, shared with your web balance."
      />

      <div className={styles.calloutRow}>
        <span>
          Replace <code>rmv_live_xxx</code> with a key from your account.
        </span>
        <Link href="/app/keys" className="btn btn-primary">
          Create a key
        </Link>
        <Link href="/developers/mcp" className="btn btn-ghost">
          Full setup guide
        </Link>
      </div>

      <ConfigCard title="Claude Code (CLI)" code={CLAUDE_CODE} />
      <ConfigCard
        title="Claude Desktop / Codex / Cursor (config file)"
        code={JSON_CONFIG}
      />
      <ConfigCard title="REST API (curl)" code={CURL} />
    </>
  );
}
