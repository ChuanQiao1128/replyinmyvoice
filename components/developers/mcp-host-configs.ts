export type McpHostConfig = {
  body: string;
  local: string;
  remote: string;
  title: string;
};

export const mcpServerName = "replyinmyvoice";
export const localInstall = "npx @replyinmyvoiceashuman/mcp-server";

export const remoteHttpMcpConfig = {
  mcpServers: {
    replyinmyvoice: {
      url: "https://replyinmyvoice.com/api/mcp",
      headers: { Authorization: "Bearer rmv_live_xxx" },
    },
  },
} as const;

export const remoteHttpServerConfig =
  remoteHttpMcpConfig.mcpServers.replyinmyvoice;
export const remoteAuthorizationHeader = `Authorization: ${remoteHttpServerConfig.headers.Authorization}`;

export const remoteEndpoint = `${remoteHttpServerConfig.url}
${remoteAuthorizationHeader}`;

export const cursorInstallConfigBase64 = Buffer.from(
  JSON.stringify(remoteHttpMcpConfig),
  "utf8",
).toString("base64");

export const cursorInstallHref = `cursor://anysphere.cursor-deeplink/mcp/install?name=replyinmyvoice&config=${encodeURIComponent(
  cursorInstallConfigBase64,
)}`;

export const claudeRemoteInstall = `claude mcp add replyinmyvoice --transport http --url ${remoteHttpServerConfig.url} --header "${remoteAuthorizationHeader}"`;

export const vscodeMcpInstallConfig = {
  name: mcpServerName,
  ...remoteHttpServerConfig,
} as const;

export const vscodeRemoteInstall = `code --add-mcp '${JSON.stringify(
  vscodeMcpInstallConfig,
)}'`;

export const mcpHostConfigs = [
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
] as const satisfies readonly McpHostConfig[];

export const claudeCodeHostConfig = mcpHostConfigs[0];
export const codexHostConfig = mcpHostConfigs[1];
