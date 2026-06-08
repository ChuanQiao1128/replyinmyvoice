#!/usr/bin/env node

const DEFAULT_CONCURRENCY = 10;
const DEFAULT_REQUESTS = 10;
const DEFAULT_POLL_TIMEOUT_MS = 120_000;
const DEFAULT_POLL_INTERVAL_MS = 1_000;
const DEFAULT_DRAFT =
  "Please let the client know the report is still being checked and I will send a clear update soon.";

function usage() {
  return `Usage: node scripts/load-test/api-burst.mjs --url <base-url> [--key <api-key>] [options]

Options:
  --url <base-url>       API host, for example https://api.example.com
  --key <api-key>        API key. Falls back to RIMV_API_KEY or API_KEY.
  --concurrency <n>      Maximum concurrent submit requests. Default: ${DEFAULT_CONCURRENCY}
  --requests <n>         Total submit requests. Default: ${DEFAULT_REQUESTS}
  --poll                 Poll 202 responses until terminal state or timeout.
  --dry-run              Parse args, print the planned run, and send no traffic.
  --help                 Print this help text.
`;
}

function parseArgs(argv) {
  const options = {
    concurrency: DEFAULT_CONCURRENCY,
    requests: DEFAULT_REQUESTS,
    poll: false,
    dryRun: false,
    help: false,
    keySource: "none",
  };

  for (let index = 0; index < argv.length; index += 1) {
    const arg = argv[index];
    const [name, inlineValue] = arg.includes("=") ? arg.split(/=(.*)/s, 2) : [arg, undefined];
    const readValue = () => {
      if (inlineValue !== undefined) {
        return inlineValue;
      }

      index += 1;
      if (index >= argv.length || argv[index].startsWith("--")) {
        throw new Error(`${name} requires a value.`);
      }

      return argv[index];
    };

    switch (name) {
      case "--url":
        options.url = readValue();
        break;
      case "--key":
        options.key = readValue();
        options.keySource = "arg";
        break;
      case "--concurrency":
        options.concurrency = parsePositiveInteger(readValue(), "--concurrency");
        break;
      case "--requests":
        options.requests = parsePositiveInteger(readValue(), "--requests");
        break;
      case "--poll":
        options.poll = true;
        break;
      case "--dry-run":
        options.dryRun = true;
        break;
      case "--help":
        options.help = true;
        break;
      default:
        throw new Error(`Unknown argument: ${arg}`);
    }
  }

  if (!options.key) {
    if (process.env.RIMV_API_KEY) {
      options.key = process.env.RIMV_API_KEY;
      options.keySource = "RIMV_API_KEY";
    } else if (process.env.API_KEY) {
      options.key = process.env.API_KEY;
      options.keySource = "API_KEY";
    }
  }

  return options;
}

function parsePositiveInteger(value, name) {
  if (!/^\d+$/.test(value)) {
    throw new Error(`${name} must be a positive integer.`);
  }

  const parsed = Number(value);
  if (!Number.isSafeInteger(parsed) || parsed < 1) {
    throw new Error(`${name} must be a positive integer.`);
  }

  return parsed;
}

function validateOptions(options) {
  if (options.help) {
    return;
  }

  if (!options.url) {
    throw new Error("--url is required.");
  }

  try {
    new URL(options.url);
  } catch {
    throw new Error("--url must be an absolute URL.");
  }

  if (!options.key && !options.dryRun) {
    throw new Error("--key is required unless RIMV_API_KEY or API_KEY is set.");
  }
}

function endpoint(baseUrl, path) {
  const normalized = baseUrl.endsWith("/") ? baseUrl : `${baseUrl}/`;
  return new URL(path.replace(/^\//, ""), normalized).toString();
}

function percentile(values, percentileRank) {
  if (values.length === 0) {
    return null;
  }

  const sorted = [...values].sort((a, b) => a - b);
  const index = Math.ceil((percentileRank / 100) * sorted.length) - 1;
  return Math.round(sorted[Math.max(0, Math.min(sorted.length - 1, index))]);
}

function increment(map, key) {
  map[key] = (map[key] ?? 0) + 1;
}

async function runPool(total, concurrency, worker) {
  const results = new Array(total);
  let nextIndex = 0;
  const workerCount = Math.min(total, concurrency);

  await Promise.all(
    Array.from({ length: workerCount }, async () => {
      while (nextIndex < total) {
        const current = nextIndex;
        nextIndex += 1;
        results[current] = await worker(current);
      }
    }),
  );

  return results;
}

async function submitRewrite(options, index, runId) {
  const startedAt = performance.now();
  try {
    const response = await fetch(endpoint(options.url, "/api/v1/rewrite"), {
      method: "POST",
      headers: {
        "Authorization": `Bearer ${options.key}`,
        "Content-Type": "application/json",
        "Idempotency-Key": `api-burst-${runId}-${index}`,
      },
      body: JSON.stringify({ draft: DEFAULT_DRAFT }),
    });
    const latencyMs = Math.round(performance.now() - startedAt);
    const text = await response.text();
    const parsed = parseJsonObject(text);
    const id = response.status === 202 && parsed ? parsed.id : undefined;

    return {
      index,
      status: response.status,
      latencyMs,
      id,
    };
  } catch (error) {
    return {
      index,
      status: "network_error",
      latencyMs: Math.round(performance.now() - startedAt),
      error: error instanceof Error ? error.message : String(error),
    };
  }
}

async function pollRewrite(options, id) {
  const startedAt = performance.now();
  const deadline = Date.now() + DEFAULT_POLL_TIMEOUT_MS;
  let attempts = 0;

  while (Date.now() < deadline) {
    attempts += 1;
    try {
      const response = await fetch(endpoint(options.url, `/api/v1/rewrite/${id}`), {
        method: "GET",
        headers: {
          "Authorization": `Bearer ${options.key}`,
        },
      });
      const text = await response.text();
      const parsed = parseJsonObject(text);
      const state = typeof parsed?.status === "string" ? parsed.status : `http_${response.status}`;

      // Only genuine application-terminal states end the poll. A transient non-OK
      // HTTP (429/503 — the status endpoint shares the per-key RPM limiter, so polls
      // under a burst are routinely rate-limited) is NOT terminal: keep retrying
      // until the deadline so terminal-state/latency stats stay accurate.
      const TERMINAL_STATES = new Set(["succeeded", "failed", "expired", "not_found"]);

      if (TERMINAL_STATES.has(state)) {
        return {
          id,
          state,
          attempts,
          latencyMs: Math.round(performance.now() - startedAt),
        };
      }
    } catch (error) {
      return {
        id,
        state: "network_error",
        attempts,
        latencyMs: Math.round(performance.now() - startedAt),
        error: error instanceof Error ? error.message : String(error),
      };
    }

    await sleep(DEFAULT_POLL_INTERVAL_MS);
  }

  return {
    id,
    state: "timeout",
    attempts,
    latencyMs: Math.round(performance.now() - startedAt),
  };
}

function parseJsonObject(text) {
  if (!text) {
    return null;
  }

  try {
    const parsed = JSON.parse(text);
    return parsed && typeof parsed === "object" ? parsed : null;
  } catch {
    return null;
  }
}

function sleep(ms) {
  return new Promise(resolve => setTimeout(resolve, ms));
}

function summarizeSubmits(results) {
  const statusHistogram = {};
  for (const result of results) {
    increment(statusHistogram, String(result.status));
  }

  const accepted202 = statusHistogram["202"] ?? 0;
  const rateLimited429 = statusHistogram["429"] ?? 0;
  return {
    statusHistogram,
    accepted202,
    rateLimited429,
    other: results.length - accepted202 - rateLimited429,
    latencyMs: {
      p50: percentile(results.map(result => result.latencyMs), 50),
      p95: percentile(results.map(result => result.latencyMs), 95),
    },
  };
}

function summarizePolls(results) {
  const terminalStateHistogram = {};
  for (const result of results) {
    increment(terminalStateHistogram, result.state);
  }

  return {
    attempted: results.length,
    terminalStateHistogram,
    latencyMs: {
      p95: percentile(results.map(result => result.latencyMs), 95),
    },
  };
}

async function main() {
  const options = parseArgs(process.argv.slice(2));
  if (options.help) {
    console.log(usage());
    return;
  }

  validateOptions(options);
  const plan = {
    url: options.url,
    concurrency: options.concurrency,
    requests: options.requests,
    poll: options.poll,
    keySource: options.keySource,
  };

  if (options.dryRun) {
    console.log(JSON.stringify({ dryRun: true, plan }, null, 2));
    return;
  }

  const runId = `${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;
  const startedAt = new Date();
  const submitResults = await runPool(
    options.requests,
    options.concurrency,
    index => submitRewrite(options, index, runId),
  );
  const acceptedIds = submitResults
    .filter(result => result.status === 202 && typeof result.id === "string")
    .map(result => result.id);

  const summary = {
    startedAt: startedAt.toISOString(),
    completedAt: new Date().toISOString(),
    durationMs: Date.now() - startedAt.getTime(),
    plan,
    submit: summarizeSubmits(submitResults),
  };

  if (options.poll) {
    const pollResults = await runPool(
      acceptedIds.length,
      options.concurrency,
      index => pollRewrite(options, acceptedIds[index]),
    );
    summary.poll = summarizePolls(pollResults);
    summary.completedAt = new Date().toISOString();
    summary.durationMs = Date.now() - startedAt.getTime();
  }

  console.log(JSON.stringify(summary, null, 2));
}

main().catch(error => {
  console.error(error instanceof Error ? error.message : String(error));
  process.exitCode = 1;
});
