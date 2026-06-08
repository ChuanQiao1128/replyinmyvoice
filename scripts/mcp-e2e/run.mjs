#!/usr/bin/env node
import { existsSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const apiKeyEnvName = "REPLY_IN_MY_VOICE_API_KEY";
const baseUrlEnvName = "REPLY_IN_MY_VOICE_BASE_URL";
const defaultBaseUrl = "https://replyinmyvoice.com";
const defaultTimeoutMs = 180_000;
const scriptDir = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(scriptDir, "../..");

const rewriteArgs = {
  context:
    "A parent asked whether tomorrow's conference can move from 3:30 PM to 4:00 PM.",
  draft:
    "Please tell them 4:00 PM works and that I will send the updated calendar invite today.",
  tone: "warm",
};

async function main() {
  const options = parseArgs(process.argv.slice(2));

  if (options.help) {
    printHelp();
    return;
  }

  const apiKey = process.env[apiKeyEnvName]?.trim();
  const dryRun = options.dryRun || !apiKey;

  if (dryRun) {
    console.log(
      apiKey
        ? "[mcp-e2e] Dry run requested; no requests sent."
        : `[mcp-e2e] ${apiKeyEnvName} is not set; dry run complete with no requests sent.`,
    );
    return;
  }

  if (!apiKey.startsWith("rmv_test_")) {
    throw new Error(`${apiKeyEnvName} must be a test key starting with rmv_test_.`);
  }

  const baseUrl = normalizeBaseUrl(
    options.baseUrl ?? process.env[baseUrlEnvName] ?? defaultBaseUrl,
  );
  const timeoutMs = normalizePositiveInteger(options.timeoutMs, defaultTimeoutMs);

  console.log(`[mcp-e2e] Target: ${baseUrl}`);

  const before = await readUsage({ apiKey, baseUrl, label: "before stdio" });
  const stdioText = await runStdioRewrite({ apiKey, baseUrl, timeoutMs });
  const afterStdio = await readUsage({ apiKey, baseUrl, label: "after stdio" });
  assertRemainingDrop({
    after: afterStdio,
    before,
    label: "stdio rewrite_email",
  });
  console.log(`[mcp-e2e] stdio returned ${stdioText.length} chars.`);

  if (options.skipRemote) {
    console.log("[mcp-e2e] Remote transport skipped by flag.");
    return;
  }

  const remoteText = await runRemoteRewrite({ apiKey, baseUrl, timeoutMs });
  const afterRemote = await readUsage({ apiKey, baseUrl, label: "after remote" });
  assertRemainingDrop({
    after: afterRemote,
    before: afterStdio,
    label: "remote rewrite_email",
  });
  console.log(`[mcp-e2e] remote returned ${remoteText.length} chars.`);
}

function parseArgs(args) {
  const options = {
    baseUrl: undefined,
    dryRun: false,
    help: false,
    skipRemote: false,
    timeoutMs: undefined,
  };

  for (let index = 0; index < args.length; index += 1) {
    const arg = args[index];

    if (arg === "--dry-run") {
      options.dryRun = true;
    } else if (arg === "--help" || arg === "-h") {
      options.help = true;
    } else if (arg === "--skip-remote") {
      options.skipRemote = true;
    } else if (arg === "--base-url") {
      options.baseUrl = readFlagValue(args, (index += 1), arg);
    } else if (arg.startsWith("--base-url=")) {
      options.baseUrl = arg.slice("--base-url=".length);
    } else if (arg === "--timeout-ms") {
      options.timeoutMs = readFlagValue(args, (index += 1), arg);
    } else if (arg.startsWith("--timeout-ms=")) {
      options.timeoutMs = arg.slice("--timeout-ms=".length);
    } else {
      throw new Error(`Unknown argument: ${arg}`);
    }
  }

  return options;
}

function readFlagValue(args, index, flag) {
  const value = args[index];
  if (!value || value.startsWith("--")) {
    throw new Error(`${flag} requires a value.`);
  }

  return value;
}

function printHelp() {
  console.log(`Usage: node scripts/mcp-e2e/run.mjs [options]

Options:
  --dry-run              Exit without starting transports or sending requests.
  --base-url <url>       API origin. Defaults to ${defaultBaseUrl}.
  --skip-remote          Run only the stdio transport check.
  --timeout-ms <ms>      Per-call timeout. Defaults to ${defaultTimeoutMs}.
  -h, --help             Show this help.

Live runs require ${apiKeyEnvName} set to a key with the rmv_test_ prefix.`);
}

async function runStdioRewrite({ apiKey, baseUrl, timeoutMs }) {
  const { Client } = await import("@modelcontextprotocol/sdk/client/index.js");
  const { StdioClientTransport } = await import(
    "@modelcontextprotocol/sdk/client/stdio.js"
  );
  const command = resolveStdioCommand();
  const client = new Client({
    name: "replyinmyvoice-mcp-e2e-stdio",
    version: "0.0.1",
  });
  const transport = new StdioClientTransport({
    ...command,
    cwd: repoRoot,
    env: stdioEnv({ apiKey, baseUrl }),
    stderr: "pipe",
  });

  pipeChildStderr(transport, "stdio");

  try {
    await client.connect(transport, { timeout: timeoutMs });
    const result = await client.callTool(
      { arguments: rewriteArgs, name: "rewrite_email" },
      undefined,
      { timeout: timeoutMs, resetTimeoutOnProgress: true },
    );

    return extractFinalText(result, "stdio");
  } finally {
    await closeClient(client);
  }
}

async function runRemoteRewrite({ apiKey, baseUrl, timeoutMs }) {
  const { Client } = await import("@modelcontextprotocol/sdk/client/index.js");
  const { StreamableHTTPClientTransport } = await import(
    "@modelcontextprotocol/sdk/client/streamableHttp.js"
  );
  const client = new Client({
    name: "replyinmyvoice-mcp-e2e-remote",
    version: "0.0.1",
  });
  const transport = new StreamableHTTPClientTransport(new URL("/api/mcp", baseUrl), {
    requestInit: {
      headers: {
        Authorization: `Bearer ${apiKey}`,
      },
    },
  });

  try {
    await client.connect(transport, { timeout: timeoutMs });
    const result = await client.callTool(
      { arguments: rewriteArgs, name: "rewrite_email" },
      undefined,
      { timeout: timeoutMs, resetTimeoutOnProgress: true },
    );

    return extractFinalText(result, "remote");
  } finally {
    await closeClient(client);
  }
}

function resolveStdioCommand() {
  const builtBin = join(repoRoot, "packages/mcp-server/dist/bin.js");
  if (existsSync(builtBin)) {
    return {
      args: [builtBin],
      command: process.execPath,
    };
  }

  const tsxBin = join(repoRoot, "node_modules/tsx/dist/cli.mjs");
  const sourceBin = join(repoRoot, "packages/mcp-server/src/bin.ts");
  if (existsSync(tsxBin) && existsSync(sourceBin)) {
    return {
      args: [tsxBin, sourceBin],
      command: process.execPath,
    };
  }

  throw new Error(
    "Could not find packages/mcp-server/dist/bin.js or local tsx to start the stdio server.",
  );
}

function stdioEnv({ apiKey, baseUrl }) {
  const env = {};
  for (const key of [
    "HOME",
    "PATH",
    "SystemRoot",
    "TEMP",
    "TMP",
    "TMPDIR",
    "USERPROFILE",
  ]) {
    if (process.env[key]) {
      env[key] = process.env[key];
    }
  }

  env[apiKeyEnvName] = apiKey;
  env[baseUrlEnvName] = baseUrl;
  env.NODE_ENV = process.env.NODE_ENV ?? "test";
  return env;
}

function pipeChildStderr(transport, label) {
  const stderr = transport.stderr;
  if (!stderr) {
    return;
  }

  stderr.on("data", (chunk) => {
    process.stderr.write(`[mcp-e2e:${label}] ${chunk}`);
  });
}

async function readUsage({ apiKey, baseUrl, label }) {
  const response = await fetch(new URL("/api/v1/usage", baseUrl), {
    cache: "no-store",
    headers: {
      Authorization: `Bearer ${apiKey}`,
    },
    method: "GET",
  });
  const body = await readJson(response);

  if (!response.ok) {
    throw new Error(
      `Usage request failed (${label}): HTTP ${response.status} ${formatApiError(body)}`,
    );
  }

  if (!isUsage(body)) {
    throw new Error(`Usage response was missing numeric remaining (${label}).`);
  }

  return body;
}

async function readJson(response) {
  const text = await response.text();
  if (!text) {
    return undefined;
  }

  try {
    return JSON.parse(text);
  } catch {
    return undefined;
  }
}

function isUsage(value) {
  return (
    typeof value === "object" &&
    value !== null &&
    Number.isFinite(value.remaining) &&
    Number.isFinite(value.used)
  );
}

function assertRemainingDrop({ after, before, label }) {
  const delta = before.remaining - after.remaining;
  if (delta !== 1) {
    throw new Error(
      `${label} expected remaining usage to drop by 1, got ${delta} (${before.remaining} -> ${after.remaining}).`,
    );
  }
}

function extractFinalText(result, label) {
  const structured = result?.structuredContent;
  if (typeof structured !== "object" || structured === null) {
    throw new Error(`${label} result did not include structured content.`);
  }

  const text = structured.rewritten;
  if (typeof text !== "string" || !text.trim()) {
    throw new Error(`${label} result did not include final rewritten text.`);
  }

  const normalizedText = text.trim();
  if (
    typeof structured.attempt_id === "string" &&
    normalizedText === structured.attempt_id.trim()
  ) {
    throw new Error(`${label} returned an attempt id instead of rewritten text.`);
  }

  if ("status" in structured && structured.status === "working") {
    throw new Error(`${label} returned a working status instead of final text.`);
  }

  return normalizedText;
}

async function closeClient(client) {
  try {
    await client.close();
  } catch (error) {
    console.warn(`[mcp-e2e] Client close warning: ${errorMessage(error)}`);
  }
}

function normalizeBaseUrl(value) {
  const trimmed = String(value ?? "").trim();
  if (!trimmed) {
    return defaultBaseUrl;
  }

  return trimmed.replace(/\/+$/, "");
}

function normalizePositiveInteger(value, fallback) {
  const parsed = Number(value);
  if (!Number.isFinite(parsed) || parsed < 1) {
    return fallback;
  }

  return Math.floor(parsed);
}

function formatApiError(body) {
  if (typeof body !== "object" || body === null) {
    return "";
  }

  const error = body.error;
  if (typeof error !== "object" || error === null) {
    return "";
  }

  const code = typeof error.code === "string" ? error.code : "api_error";
  const message = typeof error.message === "string" ? error.message : "";
  return message ? `${code}: ${message}` : code;
}

function errorMessage(error) {
  return error instanceof Error ? error.message : String(error);
}

main().catch((error) => {
  console.error(`[mcp-e2e] ${errorMessage(error)}`);
  process.exitCode = 1;
});
