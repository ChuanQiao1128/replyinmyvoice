import type { Metadata } from "next";
import Link from "next/link";

import {
  DeveloperUpsell,
  PageHeader,
} from "../../../components/app/shell/shell-primitives";
import { isDeveloperTierStatus } from "../../../components/app/shell/shell-types";
import styles from "../../../components/app/shell/shell.module.css";
import { McpConfigCopyButton } from "../../../components/developers/mcp-config-copy-button";
import {
  claudeCodeHostConfig,
  codexHostConfig,
} from "../../../components/developers/mcp-host-configs";
import { fetchAzureAccountSummary } from "../../../lib/azure-api";

export const dynamic = "force-dynamic";
export const metadata: Metadata = { title: "Connect" };

function McpGuideLink() {
  return (
    <div className="mb-4">
      <Link href="/developers/mcp" className="btn btn-ghost">
        &larr; MCP guide
      </Link>
    </div>
  );
}

function ConfigCard({
  code,
  copyLabel,
  label,
  title,
}: {
  code: string;
  copyLabel: string;
  label: string;
  title: string;
}) {
  return (
    <div className={styles.configCard}>
      <div className={styles.configHeader}>
        <div>
          <h2 className={styles.configTitle}>{title}</h2>
          <div className={styles.configMeta}>{label}</div>
        </div>
        <div className={styles.configCopy}>
          <McpConfigCopyButton label={copyLabel} text={code} />
        </div>
      </div>
      <pre className={styles.codeBlock}>
        <code>{code}</code>
      </pre>
    </div>
  );
}

export default async function ConnectPage() {
  const account = await fetchAzureAccountSummary();
  const subscriptionStatus = account?.subscriptionStatus ?? "inactive";

  if (!isDeveloperTierStatus(subscriptionStatus)) {
    return (
      <>
        <McpGuideLink />
        <PageHeader
          title="Connect"
          description="Use Reply In My Voice inside Claude Code, Codex, or another MCP host."
        />
        <DeveloperUpsell />
      </>
    );
  }

  return (
    <>
      <McpGuideLink />
      <PageHeader
        title="Connect"
        description="Use Reply In My Voice inside Claude Code, Codex, or another MCP host. One key, shared with your web balance."
      />

      <div className={styles.calloutRow}>
        <span>
          Replace <code>rmv_live_xxx</code> with a key from your account.
        </span>
        <Link href="/app/keys" className="btn btn-primary">
          Create a key
        </Link>
        <Link href="/developers/mcp" className="btn btn-ghost">
          Full MCP guide
        </Link>
      </div>

      <ConfigCard
        code={claudeCodeHostConfig.local}
        copyLabel="Copy Claude Code command"
        label="Local stdio command"
        title="Claude Code (CLI)"
      />
      <ConfigCard
        code={codexHostConfig.local}
        copyLabel="Copy Codex local config"
        label="Local stdio config"
        title="Codex (TOML)"
      />
      <ConfigCard
        code={codexHostConfig.remote}
        copyLabel="Copy Codex remote config"
        label="Codex TOML config"
        title="Remote HTTP"
      />
    </>
  );
}
