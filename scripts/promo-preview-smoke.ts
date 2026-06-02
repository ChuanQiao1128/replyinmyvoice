import { spawn, type ChildProcessWithoutNullStreams } from "node:child_process";
import { createHmac, randomUUID } from "node:crypto";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { createServer, type IncomingMessage, type Server } from "node:http";
import { tmpdir } from "node:os";
import { isAbsolute, join } from "node:path";
import { fileURLToPath } from "node:url";

import {
  getTurnstileSecretKey,
  turnstileTestSiteKey,
} from "../lib/turnstile";

const defaultPreviewPort = 8789;
const defaultAzureMockPort = 45935;
const previewHost = "127.0.0.1";
const dummyTurnstileToken = "XXXX.DUMMY.TOKEN.XXXX";
const smokeClientIp = "203.0.113.88";
const requiredTurnstileEnv = [
  "NEXT_PUBLIC_TURNSTILE_SITE_KEY",
  "TURNSTILE_SECRET_KEY",
] as const;

type RequiredTurnstileEnvName = (typeof requiredTurnstileEnv)[number];

type PreviewEnvInput = {
  appUrl: string;
  authSessionSecret: string;
  azureApiBaseUrl: string;
  promoProxySharedSecret: string;
  turnstileSecretKey: string;
  turnstileSiteKey: string;
};

type LaunchCommandInput = {
  configPath: string;
  port: number;
};

type PreviewSessionCookieInput = {
  accessToken: string;
  authSessionSecret: string;
  email: string;
  expiresAtEpochSeconds: number;
  name: string;
  subject: string;
};

type PromoRedeemCase = {
  code: string;
  expectedError: string;
  expectedStatus: number;
};

type PreviewSmokeOptions = {
  azureMockPort?: number;
  cwd?: string;
  env?: NodeJS.ProcessEnv;
  previewPort?: number;
  timeoutMs?: number;
};

type WranglerConfig = Record<string, unknown> & {
  vars?: Record<string, unknown>;
};

type ResolvedTurnstileEnv = {
  secretKey: string;
  siteKey: string;
};

type CapturedRedeem = {
  clientIp: string | null;
  proxySecretAccepted: boolean;
};

type AzureMockState = {
  capturedRedeems: CapturedRedeem[];
  proxySecret: string;
};

export function validatePreviewSmokeEnv(
  env: Partial<Record<string, string | undefined>>,
):
  | { ok: true }
  | { missing: RequiredTurnstileEnvName[]; ok: false } {
  const missing = requiredTurnstileEnv.filter((name) => !env[name]?.trim());
  return missing.length ? { missing, ok: false } : { ok: true };
}

export function buildPreviewLaunchCommand(input: LaunchCommandInput) {
  return {
    args: [
      "run",
      "cf:preview",
      "--",
      "--config",
      input.configPath,
      "--ip",
      previewHost,
      "--port",
      String(input.port),
      "--log-level",
      "warn",
    ],
    command: "npm",
  };
}

export function buildPreviewEnvFile(input: PreviewEnvInput) {
  return [
    envLine("NEXT_PRIVATE_MINIMAL_MODE", "1"),
    envLine("NEXT_PUBLIC_APP_URL", input.appUrl),
    envLine("NEXT_PUBLIC_AZURE_API_BASE_URL", input.azureApiBaseUrl),
    envLine("AUTH_SESSION_SECRET", input.authSessionSecret),
    envLine("PROMO_PROXY_SHARED_SECRET", input.promoProxySharedSecret),
    envLine("NEXT_PUBLIC_TURNSTILE_SITE_KEY", input.turnstileSiteKey),
    envLine("TURNSTILE_SECRET_KEY", input.turnstileSecretKey),
  ].join("\n");
}

export function buildPreviewWranglerConfig({
  baseConfig,
  previewEnv,
  projectRoot,
}: {
  baseConfig: WranglerConfig;
  previewEnv: PreviewEnvInput;
  projectRoot?: string;
}): WranglerConfig {
  const normalized = normalizeWranglerPaths(baseConfig, projectRoot);
  return {
    ...normalized,
    routes: [],
    vars: {
      ...(normalized.vars ?? {}),
      AUTH_SESSION_SECRET: previewEnv.authSessionSecret,
      NEXT_PUBLIC_APP_URL: previewEnv.appUrl,
      NEXT_PUBLIC_AZURE_API_BASE_URL: previewEnv.azureApiBaseUrl,
      NEXT_PUBLIC_TURNSTILE_SITE_KEY: previewEnv.turnstileSiteKey,
      PROMO_PROXY_SHARED_SECRET: previewEnv.promoProxySharedSecret,
      TURNSTILE_SECRET_KEY: previewEnv.turnstileSecretKey,
    },
  };
}

export function createPreviewSessionCookieHeader(input: PreviewSessionCookieInput) {
  const sessionCookie = createSignedCookieValue(
    {
      email: input.email,
      exp: input.expiresAtEpochSeconds,
      name: input.name,
      sub: input.subject,
    },
    input.authSessionSecret,
  );
  const accessCookie = createSignedCookieValue(
    {
      accessToken: input.accessToken,
      accessTokenExp: input.expiresAtEpochSeconds,
      exp: input.expiresAtEpochSeconds,
    },
    input.authSessionSecret,
  );
  const accessMetaCookie = createSignedCookieValue(
    {
      chunks: 1,
      exp: input.expiresAtEpochSeconds,
    },
    input.authSessionSecret,
  );

  return [
    `rimv_session=${sessionCookie}`,
    `rimv_access_0=${accessCookie}`,
    `rimv_access_meta=${accessMetaCookie}`,
  ].join("; ");
}

export function buildPromoRedeemCases(): PromoRedeemCase[] {
  return [
    { code: "PROMO_INVALID", expectedError: "invalid_code", expectedStatus: 422 },
    { code: "PROMO_EXPIRED", expectedError: "code_expired", expectedStatus: 422 },
    {
      code: "PROMO_ALREADY",
      expectedError: "already_redeemed",
      expectedStatus: 409,
    },
  ];
}

export async function runPromoPreviewSmoke(options: PreviewSmokeOptions = {}) {
  const env = options.env ?? process.env;
  const turnstile = resolveTurnstilePreviewEnv(env);

  const cwd = options.cwd ?? process.cwd();
  const previewPort = options.previewPort ?? defaultPreviewPort;
  const azureMockPort = options.azureMockPort ?? defaultAzureMockPort;
  const timeoutMs = options.timeoutMs ?? 180_000;
  const appUrl = `http://${previewHost}:${previewPort}`;
  const azureApiBaseUrl = `http://${previewHost}:${azureMockPort}`;
  const authSessionSecret = env.AUTH_SESSION_SECRET?.trim() || randomUUID();
  const promoProxySharedSecret =
    env.PROMO_PROXY_SHARED_SECRET?.trim() || randomUUID();
  const tempDir = await mkdtemp(join(tmpdir(), "promo-preview-smoke-"));
  const envFilePath = join(tempDir, ".env.preview");
  const configPath = join(tempDir, "wrangler.promo-preview.jsonc");
  const xdgConfigHome = join(tempDir, "xdg-config");
  const azureState: AzureMockState = {
    capturedRedeems: [],
    proxySecret: promoProxySharedSecret,
  };
  let azureMock: Server | undefined;
  let preview: ChildProcessWithoutNullStreams | undefined;

  try {
    azureMock = await startAzureMock({
      port: azureMockPort,
      state: azureState,
    });
    await writeFile(
      envFilePath,
      buildPreviewEnvFile({
        appUrl,
        authSessionSecret,
        azureApiBaseUrl,
        promoProxySharedSecret,
        turnstileSecretKey: turnstile.secretKey,
        turnstileSiteKey: turnstile.siteKey,
      }),
      { mode: 0o600 },
    );
    await writeFile(
      configPath,
      JSON.stringify(
        buildPreviewWranglerConfig({
          baseConfig: await readWranglerConfig(cwd),
          previewEnv: {
            appUrl,
            authSessionSecret,
            azureApiBaseUrl,
            promoProxySharedSecret,
            turnstileSecretKey: turnstile.secretKey,
            turnstileSiteKey: turnstile.siteKey,
          },
          projectRoot: cwd,
        }),
        null,
        2,
      ),
      { mode: 0o600 },
    );
    await mkdir(xdgConfigHome, { recursive: true });

    preview = await launchPreview({
      configPath,
      cwd,
      env: {
        ...env,
        AUTH_SESSION_SECRET: authSessionSecret,
        NEXT_PUBLIC_APP_URL: appUrl,
        NEXT_PUBLIC_AZURE_API_BASE_URL: azureApiBaseUrl,
        PROMO_PROXY_SHARED_SECRET: promoProxySharedSecret,
        XDG_CONFIG_HOME: xdgConfigHome,
      },
      port: previewPort,
      timeoutMs,
    });

    await smokePages(appUrl);
    await smokePromoRedeemCases({
      appUrl,
      authSessionSecret,
      cases: buildPromoRedeemCases(),
    });
    assertForwardingObserved(azureState);

    return {
      azureRedeemCalls: azureState.capturedRedeems.length,
      baseUrl: appUrl,
      pagesChecked: ["/", "/pricing", "/sign-in", "/app"],
      redeemCases: buildPromoRedeemCases().map((item) => item.code),
    };
  } finally {
    if (preview) {
      await stopProcess(preview);
    }
    if (azureMock) {
      await closeServer(azureMock);
    }
    await rm(tempDir, { force: true, recursive: true });
  }
}

function resolveTurnstilePreviewEnv(
  env: Partial<Record<string, string | undefined>>,
): ResolvedTurnstileEnv {
  const siteKey = env.NEXT_PUBLIC_TURNSTILE_SITE_KEY?.trim() || turnstileTestSiteKey;
  const secretKey = env.TURNSTILE_SECRET_KEY?.trim() || getTurnstileSecretKey();
  if (!secretKey) {
    throw new Error("Missing required Turnstile preview secret.");
  }
  return { secretKey, siteKey };
}

function envLine(name: string, value: string) {
  if (value.includes("\n") || value.includes("\r")) {
    throw new Error(`Invalid environment value for ${name}.`);
  }
  return `${name}=${value}`;
}

function createSignedCookieValue(payload: unknown, secret: string) {
  const encodedPayload = Buffer.from(JSON.stringify(payload), "utf8").toString(
    "base64url",
  );
  const signature = createHmac("sha256", secret)
    .update(encodedPayload)
    .digest("base64url");
  return `${encodedPayload}.${signature}`;
}

async function readBody(request: IncomingMessage) {
  let body = "";
  for await (const chunk of request) {
    body += String(chunk);
  }
  return body;
}

function statusForPromoCode(code: string | undefined) {
  switch (code?.trim().toUpperCase()) {
    case "PROMO_EXPIRED":
      return { error: "code_expired", status: 422 };
    case "PROMO_ALREADY":
      return { error: "already_redeemed", status: 409 };
    default:
      return { error: "invalid_code", status: 422 };
  }
}

async function startAzureMock({
  port,
  state,
}: {
  port: number;
  state: AzureMockState;
}) {
  const server = createServer((request, response) => {
    function send(status: number, body: unknown) {
      response.writeHead(status, { "Content-Type": "application/json" });
      response.end(JSON.stringify(body));
    }

    if (request.method === "GET" && request.url === "/api/me") {
      send(200, {
        currentPeriodEnd: null,
        email: "promo-preview@example.test",
        externalAuthUserId: "promo-preview-user",
        promo: {
          eligible: true,
          hasRedeemed: false,
          trialExpiresAt: null,
          trialRemaining: 0,
        },
        subscriptionStatus: "none",
        usage: {
          exhausted: true,
          periodEnd: null,
          periodKey: "free:lifetime",
          quota: 0,
          remaining: 0,
          reserved: 0,
          scope: "free",
          sources: [],
          used: 0,
        },
        userId: "promo-preview-user",
      });
      return;
    }

    if (request.method === "POST" && request.url === "/api/promo/redeem") {
      void readBody(request).then((rawBody) => {
        const payload = parseJsonObject(rawBody);
        state.capturedRedeems.push({
          clientIp: request.headers["x-client-ip"]?.toString() ?? null,
          proxySecretAccepted:
            request.headers["x-rimv-proxy-secret"] === state.proxySecret,
        });
        const result = statusForPromoCode(
          typeof payload?.code === "string" ? payload.code : undefined,
        );
        send(result.status, { error: result.error });
      });
      return;
    }

    send(404, { error: "not_found" });
  });

  await new Promise<void>((resolve) => {
    server.listen(port, previewHost, resolve);
  });
  return server;
}

function parseJsonObject(value: string) {
  try {
    const parsed = JSON.parse(value) as unknown;
    return parsed && typeof parsed === "object" && !Array.isArray(parsed)
      ? (parsed as Record<string, unknown>)
      : null;
  } catch {
    return null;
  }
}

function normalizeWranglerPaths(
  config: WranglerConfig,
  projectRoot: string | undefined,
): WranglerConfig {
  if (!projectRoot) {
    return config;
  }

  const normalized: WranglerConfig = { ...config };
  if (typeof normalized.main === "string" && !isAbsolute(normalized.main)) {
    normalized.main = join(projectRoot, normalized.main);
  }

  const assets = normalized.assets;
  if (assets && typeof assets === "object" && !Array.isArray(assets)) {
    const assetRecord = assets as Record<string, unknown>;
    if (
      typeof assetRecord.directory === "string" &&
      !isAbsolute(assetRecord.directory)
    ) {
      normalized.assets = {
        ...assetRecord,
        directory: join(projectRoot, assetRecord.directory),
      };
    }
  }

  return normalized;
}

async function readWranglerConfig(cwd: string): Promise<WranglerConfig> {
  const rawConfig = await readFile(join(cwd, "wrangler.jsonc"), "utf8");
  const parsed = JSON.parse(rawConfig) as unknown;
  if (!parsed || typeof parsed !== "object" || Array.isArray(parsed)) {
    throw new Error("wrangler.jsonc was not an object.");
  }
  return parsed as WranglerConfig;
}

async function launchPreview({
  configPath,
  cwd,
  env,
  port,
  timeoutMs,
}: {
  configPath: string;
  cwd: string;
  env: NodeJS.ProcessEnv;
  port: number;
  timeoutMs: number;
}) {
  const { args, command } = buildPreviewLaunchCommand({ configPath, port });
  const output: string[] = [];
  const child = spawn(command, args, {
    cwd,
    detached: true,
    env,
    stdio: "pipe",
  });
  child.stdout.on("data", (chunk) => pushOutput(output, chunk));
  child.stderr.on("data", (chunk) => pushOutput(output, chunk));

  await waitForPreviewReady({
    child,
    output,
    timeoutMs,
    url: `http://${previewHost}:${port}/`,
  });

  return child;
}

function pushOutput(output: string[], chunk: Buffer) {
  output.push(chunk.toString("utf8"));
  if (output.length > 80) {
    output.splice(0, output.length - 80);
  }
}

async function waitForPreviewReady({
  child,
  output,
  timeoutMs,
  url,
}: {
  child: ChildProcessWithoutNullStreams;
  output: string[];
  timeoutMs: number;
  url: string;
}) {
  const startedAt = Date.now();
  let exited = false;
  child.once("exit", () => {
    exited = true;
  });

  while (Date.now() - startedAt < timeoutMs) {
    if (exited) {
      throw new Error(
        `OpenNext preview exited before serving requests.\n${output.join("")}`,
      );
    }

    try {
      const response = await fetch(url, { redirect: "manual" });
      if (response.status > 0) {
        return;
      }
    } catch {
      // Preview is still starting.
    }

    await delay(1_000);
  }

  throw new Error(`Timed out waiting for OpenNext preview at ${url}.`);
}

async function smokePages(appUrl: string) {
  await expectStatus(`${appUrl}/`, 200);
  await expectStatus(`${appUrl}/pricing`, 200);
  await expectStatus(`${appUrl}/sign-in`, 200);

  const appResponse = await fetch(`${appUrl}/app`, { redirect: "manual" });
  if (![302, 303, 307, 308].includes(appResponse.status)) {
    throw new Error(`/app returned ${appResponse.status}; expected an auth redirect.`);
  }
  const location = appResponse.headers.get("location") ?? "";
  if (!location.includes("/sign-in")) {
    throw new Error("/app did not redirect signed-out users to sign in.");
  }
}

async function expectStatus(url: string, expectedStatus: number) {
  const response = await fetch(url, { redirect: "manual" });
  if (response.status !== expectedStatus) {
    throw new Error(`${new URL(url).pathname} returned ${response.status}.`);
  }
}

async function smokePromoRedeemCases({
  appUrl,
  authSessionSecret,
  cases,
}: {
  appUrl: string;
  authSessionSecret: string;
  cases: PromoRedeemCase[];
}) {
  const cookie = createPreviewSessionCookieHeader({
    accessToken: "promo-preview-access-token",
    authSessionSecret,
    email: "promo-preview@example.test",
    expiresAtEpochSeconds: Math.floor(Date.now() / 1000) + 60 * 60,
    name: "Promo Preview",
    subject: "promo-preview-user",
  });

  for (const item of cases) {
    const response = await fetch(`${appUrl}/api/promo/redeem`, {
      body: JSON.stringify({
        code: item.code,
        turnstileToken: dummyTurnstileToken,
      }),
      headers: {
        "CF-Connecting-IP": smokeClientIp,
        "Content-Type": "application/json",
        Cookie: cookie,
        Origin: appUrl,
      },
      method: "POST",
      redirect: "manual",
    });
    const payload = await response.json().catch(() => null) as {
      error?: unknown;
    } | null;

    if (response.status !== item.expectedStatus) {
      const errorCode =
        typeof payload?.error === "string" ? ` error=${payload.error}` : "";
      throw new Error(
        `${item.code} returned ${response.status}; expected ${item.expectedStatus}.${errorCode}`,
      );
    }
    if (payload?.error !== item.expectedError) {
      throw new Error(`${item.code} returned an unexpected error code.`);
    }
  }
}

function assertForwardingObserved(state: AzureMockState) {
  const cases = buildPromoRedeemCases();
  if (state.capturedRedeems.length !== cases.length) {
    throw new Error(
      `Azure mock saw ${state.capturedRedeems.length} redeem calls; expected ${cases.length}.`,
    );
  }

  const missingTrustedIp = state.capturedRedeems.some(
    (item) => item.clientIp !== smokeClientIp,
  );
  if (missingTrustedIp) {
    throw new Error("Preview redeem did not forward the trusted client IP.");
  }

  const rejectedProxySecret = state.capturedRedeems.some(
    (item) => !item.proxySecretAccepted,
  );
  if (rejectedProxySecret) {
    throw new Error("Preview redeem did not forward the expected proxy secret.");
  }
}

async function closeServer(server: Server) {
  await new Promise<void>((resolve, reject) => {
    server.close((error) => {
      if (error) {
        reject(error);
        return;
      }
      resolve();
    });
  });
}

async function stopProcess(child: ChildProcessWithoutNullStreams) {
  if (child.exitCode !== null || child.killed) {
    return;
  }

  const exitPromise = new Promise<boolean>((resolve) => {
    child.once("exit", () => resolve(true));
  });
  signalProcessGroup(child, "SIGTERM");
  const exited = await Promise.race([
    exitPromise,
    delay(5_000).then(() => false),
  ]);
  if (!exited) {
    signalProcessGroup(child, "SIGKILL");
    await Promise.race([
      exitPromise,
      delay(2_000).then(() => false),
    ]);
  }
  child.stdout.destroy();
  child.stderr.destroy();
}

function signalProcessGroup(
  child: ChildProcessWithoutNullStreams,
  signal: NodeJS.Signals,
) {
  try {
    if (child.pid) {
      process.kill(-child.pid, signal);
      return;
    }
  } catch {
    // Fall back to the direct child below.
  }

  try {
    child.kill(signal);
  } catch {
    // The process may have exited between checks.
  }
}

function delay(milliseconds: number) {
  return new Promise((resolve) => setTimeout(resolve, milliseconds));
}

function isMainModule() {
  return process.argv[1]
    ? fileURLToPath(import.meta.url) === process.argv[1]
    : false;
}

if (isMainModule()) {
  runPromoPreviewSmoke({
    azureMockPort: integerEnv("PROMO_AZURE_MOCK_PORT"),
    previewPort: integerEnv("PROMO_PREVIEW_PORT"),
  })
    .then((result) => {
      console.log("promo_preview_smoke_passed", JSON.stringify(result));
    })
    .catch((error: unknown) => {
      const message = error instanceof Error ? error.message : String(error);
      console.error("promo_preview_smoke_failed", message);
      process.exitCode = 1;
    });
}

function integerEnv(name: string) {
  const rawValue = process.env[name]?.trim();
  if (!rawValue) {
    return undefined;
  }
  const value = Number(rawValue);
  return Number.isInteger(value) && value > 0 ? value : undefined;
}
