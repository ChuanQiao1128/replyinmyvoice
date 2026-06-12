import Link from "next/link";
import type { Metadata } from "next";
import type { ReactNode } from "react";

import { McpConfigCopyButton } from "../../../components/developers/mcp-config-copy-button";
import { SiteHeader } from "../../../components/site-header";

export const metadata: Metadata = {
  title: "MCP connection guide",
  description:
    "Connect the Reply In My Voice MCP server locally or remotely so LLM hosts can rewrite drafts into natural, concise replies with meaning and facts intact.",
  openGraph: {
    title: "Reply In My Voice MCP server",
    description:
      "Connect Claude Code, Codex, Claude Desktop, or Cursor to Reply In My Voice through local stdio or remote HTTP.",
    url: "https://replyinmyvoice.com/developers/mcp",
    siteName: "Reply In My Voice",
    type: "website",
    images: "/og.png",
  },
  twitter: {
    card: "summary_large_image",
    title: "Reply In My Voice MCP server",
    description:
      "Connect Claude Code, Codex, Claude Desktop, or Cursor to Reply In My Voice through local stdio or remote HTTP.",
    images: "/og.png",
  },
};

const localInstall = `npx @replyinmyvoiceashuman/mcp-server`;

const remoteHttpMcpConfig = {
  mcpServers: {
    replyinmyvoice: {
      url: "https://replyinmyvoice.com/api/mcp",
      headers: { Authorization: "Bearer rmv_live_xxx" },
    },
  },
} as const;

const remoteHttpServerConfig = remoteHttpMcpConfig.mcpServers.replyinmyvoice;
const remoteAuthorizationHeader = `Authorization: ${remoteHttpServerConfig.headers.Authorization}`;

const remoteEndpoint = `${remoteHttpServerConfig.url}
${remoteAuthorizationHeader}`;

const cursorInstallConfigBase64 = Buffer.from(
  JSON.stringify(remoteHttpMcpConfig),
  "utf8",
).toString("base64");

const cursorInstallHref = `cursor://anysphere.cursor-deeplink/mcp/install?name=replyinmyvoice&config=${encodeURIComponent(
  cursorInstallConfigBase64,
)}`;

const claudeRemoteInstall = `claude mcp add replyinmyvoice --transport http --url ${remoteHttpServerConfig.url} --header "${remoteAuthorizationHeader}"`;

const vscodeMcpInstallConfig = {
  name: "replyinmyvoice",
  ...remoteHttpServerConfig,
} as const;

const vscodeRemoteInstall = `code --add-mcp '${JSON.stringify(
  vscodeMcpInstallConfig,
)}'`;

const hostConfigs = [
  {
    title: "Claude Code",
    body: "Use the local command when you want the host to launch the server on your machine. Use the remote URL when your workspace supports HTTP MCP.",
    local: `claude mcp add replyinmyvoice \\
  --env REPLY_IN_MY_VOICE_API_KEY=rmv_live_xxx \\
  -- npx -y @replyinmyvoiceashuman/mcp-server`,
    remote: `claude mcp add replyinmyvoice \\
  --transport http \\
  --url https://replyinmyvoice.com/api/mcp \\
  --header "Authorization: Bearer rmv_live_xxx"`,
  },
  {
    title: "Codex",
    body: "Add one of these entries to your Codex MCP config. The local version reads the key from env; the remote version sends the Bearer header.",
    local: `[mcp_servers.replyinmyvoice]
command = "npx"
args = ["-y", "@replyinmyvoiceashuman/mcp-server"]
env = { REPLY_IN_MY_VOICE_API_KEY = "rmv_live_xxx" }`,
    remote: `[mcp_servers.replyinmyvoice]
url = "https://replyinmyvoice.com/api/mcp"
headers = { Authorization = "Bearer rmv_live_xxx" }`,
  },
  {
    title: "Claude Desktop",
    body: "Put the block inside the app's MCP server config, then fully restart the desktop app so it reloads the server list.",
    local: `{
  "mcpServers": {
    "replyinmyvoice": {
      "command": "npx",
      "args": ["-y", "@replyinmyvoiceashuman/mcp-server"],
      "env": { "REPLY_IN_MY_VOICE_API_KEY": "rmv_live_xxx" }
    }
  }
}`,
    remote: `{
  "mcpServers": {
    "replyinmyvoice": {
      "url": "https://replyinmyvoice.com/api/mcp",
      "headers": { "Authorization": "Bearer rmv_live_xxx" }
    }
  }
}`,
  },
  {
    title: "Cursor",
    body: "Use Settings -> MCP or edit the MCP JSON file directly. The same tool names are available over local stdio and remote HTTP.",
    local: `{
  "mcpServers": {
    "replyinmyvoice": {
      "command": "npx",
      "args": ["-y", "@replyinmyvoiceashuman/mcp-server"],
      "env": { "REPLY_IN_MY_VOICE_API_KEY": "rmv_live_xxx" }
    }
  }
}`,
    remote: `{
  "mcpServers": {
    "replyinmyvoice": {
      "url": "https://replyinmyvoice.com/api/mcp",
      "headers": { "Authorization": "Bearer rmv_live_xxx" }
    }
  }
}`,
  },
];

const errorRows = [
  {
    status: "401",
    cause: "The key is missing, expired, revoked, or copied incorrectly.",
    action: "Create or rotate a key, then update the host config.",
  },
  {
    status: "402",
    cause: "The account has no paid rewrite credits remaining.",
    action: "Send the user to pricing so they can add paid quota.",
  },
  {
    status: "409",
    cause: "The host retried a request with a changed body and reused an idempotency key.",
    action: "Retry with a fresh request or keep the body unchanged.",
  },
  {
    status: "429",
    cause: "The key is sending requests faster than the account limit allows.",
    action: "Back off before retrying the same tool call.",
  },
  {
    status: "working",
    cause: "Remote polling reached its time budget while the rewrite is still running.",
    action: "Call get_rewrite_result with the returned attempt_id.",
  },
];

function CodeBlock({
  children,
  copyLabel,
  label,
  status,
}: {
  children: string;
  copyLabel?: string;
  label: string;
  status?: string;
}) {
  return (
    <div className="api-seg">
      <div className="api-seg-label">
        <span>{label}</span>
        {status ? <span className="api-status">{status}</span> : null}
        {copyLabel ? (
          <McpConfigCopyButton label={copyLabel} text={children} />
        ) : null}
      </div>
      <pre className="api-code">
        <code>{children}</code>
      </pre>
    </div>
  );
}

function HostBlock({
  body,
  local,
  remote,
  title,
}: {
  body: string;
  local: string;
  remote: string;
  title: string;
}) {
  return (
    <article style={{ marginTop: 28 }}>
      <div className="pp-includes-head">{title}</div>
      <p
        style={{
          color: "var(--ink-2)",
          fontSize: 15,
          lineHeight: 1.6,
          marginTop: 12,
          maxWidth: "70ch",
        }}
      >
        {body}
      </p>
      <div className="dev-code-grid">
        <div className="api-panel">
          <div className="api-bar">
            <span className="dots">
              <i />
              <i />
              <i />
            </span>
            <span className="bar-label">local stdio</span>
          </div>
          <CodeBlock copyLabel="Copy local config" label="Local config">
            {local}
          </CodeBlock>
        </div>
        <div className="api-panel">
          <div className="api-bar">
            <span className="dots">
              <i />
              <i />
              <i />
            </span>
            <span className="bar-label">remote HTTP</span>
          </div>
          <CodeBlock copyLabel="Copy remote config" label="Remote config">
            {remote}
          </CodeBlock>
        </div>
      </div>
    </article>
  );
}

function Step({
  children,
  index,
  title,
}: {
  children: ReactNode;
  index: string;
  title: string;
}) {
  return (
    <div className="dev-step">
      <span>{index}</span>
      <div>
        <h3>{title}</h3>
        <p>{children}</p>
      </div>
    </div>
  );
}

export default function DevelopersMcpPage() {
  return (
    <main className="rimv">
      <SiteHeader />
      <section className="page dev-page">
        <div className="wrap">
          <div className="page-head">
            <div className="eyebrow">
              <span className="dot" />
              MCP
            </div>
            <h1>Reply In My Voice MCP server</h1>
            <p className="lede">
              Connect your LLM host to a stable rewrite tool that turns rough
              drafts into natural, concise replies with meaning and facts
              intact. Use local stdio with <code>{localInstall}</code>, or use
              remote HTTP with a Bearer key.
            </p>
            <div className="dev-hero-meta" aria-label="MCP highlights">
              <span>Local stdio</span>
              <span>Remote HTTP</span>
              <span>rewrite_email + get_rewrite_result</span>
            </div>
            <div className="hero-cta" style={{ marginTop: 28 }}>
              <Link href="/developers/keys" className="btn btn-primary btn-lg">
                Get a key <span className="btn-arrow">→</span>
              </Link>
              <a href="#hosts" className="btn btn-ghost btn-lg">
                Host configs
              </a>
              <Link href="/developers/api" className="btn btn-ghost btn-lg">
                API docs
              </Link>
            </div>
          </div>

          <section className="dev-section" aria-labelledby="install-heading">
            <div className="pp-includes-head" id="install-heading">
              Install in your host
            </div>
            <p className="dev-section-note">
              {
                "Replace the rmv_live_xxx placeholder with your key before using the installed config."
              }
            </p>
            <div className="dev-meta-grid">
              <div className="api-panel">
                <div className="api-bar">
                  <span className="dots">
                    <i />
                    <i />
                    <i />
                  </span>
                  <span className="bar-label">Cursor</span>
                </div>
                <div className="api-seg">
                  <div className="api-seg-label">
                    <span>Remote HTTP deeplink</span>
                  </div>
                  <a className="btn btn-accent" href={cursorInstallHref}>
                    Add to Cursor <span className="btn-arrow">→</span>
                  </a>
                </div>
              </div>
              <div className="api-panel">
                <div className="api-bar">
                  <span className="dots">
                    <i />
                    <i />
                    <i />
                  </span>
                  <span className="bar-label">Claude Code</span>
                </div>
                <CodeBlock
                  copyLabel="Copy Claude install command"
                  label="Remote HTTP command"
                >
                  {claudeRemoteInstall}
                </CodeBlock>
              </div>
              <div className="api-panel">
                <div className="api-bar">
                  <span className="dots">
                    <i />
                    <i />
                    <i />
                  </span>
                  <span className="bar-label">VS Code</span>
                </div>
                <CodeBlock
                  copyLabel="Copy VS Code install command"
                  label="Remote HTTP command"
                >
                  {vscodeRemoteInstall}
                </CodeBlock>
              </div>
            </div>
          </section>

          <section className="dev-section" aria-labelledby="what-heading">
            <div className="pp-includes-head" id="what-heading">
              What it gives the host
            </div>
            <div className="card-grid">
              <article className="v2card">
                <h3>One rewrite tool</h3>
                <p>
                  <code>rewrite_email</code> accepts a draft reply and returns
                  the final rewritten text when the job succeeds.
                </p>
              </article>
              <article className="v2card">
                <h3>One follow-up tool</h3>
                <p>
                  <code>get_rewrite_result</code> checks an existing attempt if
                  a remote call is still working when the host reaches its time
                  budget.
                </p>
              </article>
              <article className="v2card">
                <h3>Stable account billing</h3>
                <p>
                  Website rewrites and MCP rewrites draw from the same paid
                  balance, so account owners manage keys and credits in one
                  place.
                </p>
              </article>
            </div>
          </section>

          <section className="dev-section" aria-labelledby="connect-heading">
            <div className="pp-includes-head" id="connect-heading">
              Local and remote connection
            </div>
            <div className="dev-doc-grid">
              <div className="dev-flow" aria-label="Connection steps">
                <Step index="1" title="Create a Bearer key">
                  Open{" "}
                  <Link href="/developers/keys" className="dev-text-link">
                    API keys
                  </Link>
                  , create a key, and copy the plaintext value once.
                </Step>
                <Step index="2" title="Pick local or remote">
                  Local runs <code>{localInstall}</code> through stdio. Remote
                  connects to <code>https://replyinmyvoice.com/api/mcp</code>{" "}
                  and sends <code>Authorization: Bearer rmv_live_xxx</code>.
                </Step>
                <Step index="3" title="Ask for the rewrite">
                  In the host, call <code>rewrite_email</code> with your rough
                  draft reply.
                </Step>
              </div>

              <div className="api-panel dev-api-panel">
                <div className="api-bar">
                  <span className="dots">
                    <i />
                    <i />
                    <i />
                  </span>
                  <span className="bar-label">connection targets</span>
                </div>
                <CodeBlock copyLabel="Copy local target" label="Local stdio">
                  {localInstall}
                </CodeBlock>
                <CodeBlock copyLabel="Copy remote target" label="Remote HTTP">
                  {remoteEndpoint}
                </CodeBlock>
              </div>
            </div>
          </section>

          <section className="dev-section" id="hosts" aria-labelledby="hosts-heading">
            <div className="pp-includes-head" id="hosts-heading">
              Host config blocks
            </div>
            <p className="dev-section-note">
              Replace rmv_live_xxx with your key from the developer dashboard.
            </p>
            {hostConfigs.map((config) => (
              <HostBlock
                body={config.body}
                key={config.title}
                local={config.local}
                remote={config.remote}
                title={config.title}
              />
            ))}
          </section>

          <section className="dev-section" aria-labelledby="tool-reference-heading">
            <div className="pp-includes-head" id="tool-reference-heading">
              Tool reference
            </div>
            <div className="dev-reference-grid api-endpoints">
              <article className="api-endpoint">
                <div className="ep-head">
                  <span className="method-badge">tool</span>
                  <span className="ep-path">rewrite_email</span>
                </div>
                <div className="dev-endpoint-body">
                  <p>
                    Input: <code>draft</code>, a draft reply string from{" "}
                    <code>10 to 2400 characters</code> and within the{" "}
                    <code>300-word draft limit</code>.
                  </p>
                  <p>
                    Output on success: <code>attempt_id</code>,{" "}
                    <code>rewritten</code>, and optional changes.
                  </p>
                  <p>
                    Remote output at the polling cap: <code>status</code>{" "}
                    <code>working</code> with <code>attempt_id</code>.
                  </p>
                </div>
              </article>
              <article className="api-endpoint">
                <div className="ep-head">
                  <span className="method-badge">tool</span>
                  <span className="ep-path">get_rewrite_result</span>
                </div>
                <div className="dev-endpoint-body">
                  <p>
                    Input: <code>attempt_id</code>, the rewrite attempt id
                    returned by <code>rewrite_email</code>.
                  </p>
                  <p>
                    Output: <code>status</code> as <code>working</code>,{" "}
                    <code>succeeded</code>, or <code>failed</code>, plus{" "}
                    <code>rewritten</code> and optional changes when available.
                  </p>
                </div>
              </article>
            </div>
          </section>

          <section className="dev-section" aria-labelledby="remote-heading">
            <div className="pp-includes-head" id="remote-heading">
              Remote working state
            </div>
            <div className="dev-callout">
              Remote rewrites usually finish in a few seconds. The remote HTTP
              endpoint polls for about 50 seconds before returning{" "}
              <code>status</code> <code>working</code> with an{" "}
              <code>attempt_id</code>. The host should poll again by calling{" "}
              <code>get_rewrite_result</code> with that id, using short backoff
              between retries.{" "}
              {
                "Local stdio polls for up to about 2 minutes and currently does not return an attempt_id on timeout; prefer remote HTTP for long-running jobs."
              }
            </div>
          </section>

          <section className="dev-section" aria-labelledby="billing-heading">
            <div className="pp-includes-head" id="billing-heading">
              Billing, quota, and error UX
            </div>
            <div className="dev-callout">
              A successful MCP rewrite uses <strong>1 credit per rewrite</strong>.
              Polling an existing attempt with <code>get_rewrite_result</code>{" "}
              does not use another credit. If a tool call returns{" "}
              <code>402</code>, link the user to{" "}
              <Link href="/pricing" className="dev-text-link">
                pricing
              </Link>{" "}
              so they can add paid quota.
            </div>
            <div className="dev-table-wrap">
              <table className="dev-table">
                <thead>
                  <tr>
                    <th>Status</th>
                    <th>What it means</th>
                    <th>What the host should show</th>
                  </tr>
                </thead>
                <tbody>
                  {errorRows.map((row) => (
                    <tr key={row.status}>
                      <td>
                        <code>{row.status}</code>
                      </td>
                      <td>{row.cause}</td>
                      <td>{row.action}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>

          <div style={{ marginTop: 64 }}>
            <div className="final-card">
              <div>
                <div className="eyebrow" style={{ color: "var(--bg)" }}>
                  <span className="dot" style={{ background: "var(--accent)" }} />
                  Ready to connect
                </div>
                <h2 style={{ marginTop: 20 }}>Create a key, then paste a block.</h2>
                <p>
                  Store the key like a password. Rotate it when a machine,
                  workspace, or teammate no longer needs access.
                </p>
              </div>
              <div className="cta-side">
                <Link href="/developers/keys" className="btn btn-accent">
                  Get a key <span className="btn-arrow">→</span>
                </Link>
                <div className="meta">
                  Paid quota is required before MCP rewrites can run.
                </div>
              </div>
            </div>
          </div>
        </div>
      </section>
    </main>
  );
}
