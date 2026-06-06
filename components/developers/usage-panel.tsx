"use client";

import { AlertCircle, Clock3, Download, Loader2, RefreshCw } from "lucide-react";
import { useCallback, useEffect, useMemo, useState } from "react";

import { Button, LinkButton } from "../ui/button";
import { Card } from "../ui/card";
import { UsageBarChart, type UsageSeriesPoint } from "./usage-bar-chart";
import { downloadCsvFile } from "../../lib/client-csv-download";

type UsageBucket = {
  calls: number;
  failed: number;
  succeeded: number;
};

type UsageSummary = {
  last30dCalls: number;
  monthToDate: UsageBucket;
  periodEnd?: string | null;
  quota: number;
  remaining: number;
  today: UsageBucket;
  used: number;
  yesterday: UsageBucket;
};

type RecentUsageCall = {
  createdAt: string;
  endpoint: string;
  keyLast4: string | null;
  latencyMs: number | null;
  statusCode: number;
};

type UsageState =
  | { status: "loading" }
  | {
      recent: RecentUsageCall[];
      series: UsageSeriesPoint[];
      status: "ready";
      summary: UsageSummary;
    }
  | { message: string; status: "error" };

const emptyBucket: UsageBucket = {
  calls: 0,
  failed: 0,
  succeeded: 0,
};

const emptySummary: UsageSummary = {
  last30dCalls: 0,
  monthToDate: emptyBucket,
  periodEnd: null,
  quota: 0,
  remaining: 0,
  today: emptyBucket,
  used: 0,
  yesterday: emptyBucket,
};

function normalizeBucket(bucket: Partial<UsageBucket> | undefined): UsageBucket {
  return {
    calls: safeNumber(bucket?.calls),
    failed: safeNumber(bucket?.failed),
    succeeded: safeNumber(bucket?.succeeded),
  };
}

function normalizeSummary(summary: Partial<UsageSummary> | null): UsageSummary {
  if (!summary) {
    return emptySummary;
  }

  return {
    last30dCalls: safeNumber(summary.last30dCalls),
    monthToDate: normalizeBucket(summary.monthToDate),
    periodEnd: summary.periodEnd ?? null,
    quota: safeNumber(summary.quota),
    remaining: safeNumber(summary.remaining),
    today: normalizeBucket(summary.today),
    used: safeNumber(summary.used),
    yesterday: normalizeBucket(summary.yesterday),
  };
}

function safeNumber(value: number | undefined | null) {
  return Number.isFinite(value) && value && value > 0 ? value : 0;
}

function formatDateTime(value: string) {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return "Not recorded";
  }

  return new Intl.DateTimeFormat(undefined, {
    day: "numeric",
    hour: "numeric",
    minute: "2-digit",
    month: "short",
  }).format(date);
}

function formatPeriodEnd(value: string | null | undefined) {
  if (!value) {
    return "No period end recorded";
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return "No period end recorded";
  }

  return new Intl.DateTimeFormat(undefined, {
    day: "numeric",
    month: "long",
    year: "numeric",
  }).format(date);
}

function statusClass(statusCode: number) {
  if (statusCode >= 200 && statusCode < 300) {
    return "border-sage/25 bg-sky text-sage";
  }

  return "border-rust/25 bg-rust/10 text-rust";
}

function formatLatency(value: number | null) {
  return Number.isFinite(value) && value !== null ? `${value} ms` : "Not recorded";
}

function formatKeyLast4(value: string | null) {
  return value ? `••••${value}` : "No key";
}

async function readJsonError(response: Response) {
  const payload = (await response.json().catch(() => null)) as {
    detail?: string;
    error?: string;
    title?: string;
  } | null;
  return payload?.error ?? payload?.detail ?? payload?.title;
}

async function fetchJson<T>(path: string) {
  const response = await fetch(path, {
    cache: "no-store",
  });

  if (response.status === 401) {
    window.location.assign("/sign-in");
    return null;
  }

  if (!response.ok) {
    throw new Error((await readJsonError(response)) ?? "Could not load usage.");
  }

  return (await response.json()) as T;
}

async function loadUsage() {
  const [summary, series, recent] = await Promise.all([
    fetchJson<UsageSummary>("/api/me/api-usage/summary"),
    fetchJson<UsageSeriesPoint[]>("/api/me/api-usage/series?days=30"),
    fetchJson<RecentUsageCall[]>("/api/me/api-usage/recent?limit=50"),
  ]);

  return {
    recent: recent ?? [],
    series: series ?? [],
    summary: normalizeSummary(summary),
  };
}

function SummaryCard({
  bucket,
  label,
}: {
  bucket: UsageBucket;
  label: string;
}) {
  return (
    <Card className="p-5">
      <p className="font-mono text-[11px] font-semibold uppercase tracking-[0.16em] text-sage">
        {label}
      </p>
      <p className="mt-4 text-4xl font-semibold tracking-normal text-ink">
        {bucket.calls}
      </p>
      <p className="mt-1 text-sm text-ink/60">calls</p>
      <div className="mt-4 grid grid-cols-2 gap-2 text-sm">
        <div className="rounded-md border border-sage/20 bg-sky px-3 py-2">
          <p className="font-semibold text-sage">{bucket.succeeded}</p>
          <p className="text-ink/60">succeeded</p>
        </div>
        <div className="rounded-md border border-rust/20 bg-rust/5 px-3 py-2">
          <p className="font-semibold text-rust">{bucket.failed}</p>
          <p className="text-ink/60">failed</p>
        </div>
      </div>
    </Card>
  );
}

export function UsagePanel() {
  const [usageState, setUsageState] = useState<UsageState>({ status: "loading" });
  const [exportState, setExportState] = useState<
    { status: "idle" | "loading" } | { message: string; status: "error" }
  >({ status: "idle" });

  const refreshUsage = useCallback(async () => {
    setUsageState({ status: "loading" });

    try {
      const usage = await loadUsage();
      setUsageState({ ...usage, status: "ready" });
    } catch (error) {
      setUsageState({
        message: error instanceof Error ? error.message : "Could not load usage.",
        status: "error",
      });
    }
  }, []);

  const exportUsage = useCallback(async () => {
    setExportState({ status: "loading" });

    try {
      await downloadCsvFile({
        filename: "api-usage.csv",
        path: "/api/me/api-usage/export?limit=1000",
      });
      setExportState({ status: "idle" });
    } catch (error) {
      setExportState({
        message: error instanceof Error ? error.message : "Could not export CSV.",
        status: "error",
      });
    }
  }, []);

  useEffect(() => {
    let isCurrent = true;

    async function loadInitialUsage() {
      try {
        const usage = await loadUsage();
        if (isCurrent) {
          setUsageState({ ...usage, status: "ready" });
        }
      } catch (error) {
        if (isCurrent) {
          setUsageState({
            message:
              error instanceof Error ? error.message : "Could not load usage.",
            status: "error",
          });
        }
      }
    }

    void loadInitialUsage();

    return () => {
      isCurrent = false;
    };
  }, []);

  const readyState = usageState.status === "ready" ? usageState : null;
  const summary = readyState?.summary ?? emptySummary;
  const noCalls = Boolean(
    readyState &&
      summary.today.calls === 0 &&
      summary.yesterday.calls === 0 &&
      summary.monthToDate.calls === 0 &&
      readyState.recent.length === 0,
  );
  const exhausted = readyState ? summary.remaining <= 0 && summary.quota > 0 : false;

  const cards = useMemo(
    () => [
      { bucket: summary.today, label: "Today" },
      { bucket: summary.yesterday, label: "Yesterday" },
      { bucket: summary.monthToDate, label: "Month-to-date" },
    ],
    [summary],
  );

  return (
    <div className="space-y-5">
      <section className="rounded-lg border border-line bg-white/80 p-6 shadow-soft sm:p-8">
        <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
          <div className="min-w-0">
            <span className="inline-flex items-center gap-2 font-mono text-[11px] font-semibold uppercase tracking-[0.16em] text-sage">
              <Clock3 className="h-4 w-4" aria-hidden="true" />
              Usage
            </span>
            <h2 className="mt-3 text-3xl">API usage</h2>
            <p className="mt-2 max-w-2xl text-sm text-ink/65">
              Review calls by day, monitor remaining quota, and inspect recent
              API activity.
            </p>
          </div>
          <div className="flex w-full flex-col gap-2 sm:w-auto">
            <div className="flex w-full flex-col gap-2 sm:flex-row">
              <Button
                className="w-full sm:w-auto"
                disabled={exportState.status === "loading"}
                onClick={() => void exportUsage()}
                type="button"
                variant="secondary"
              >
                {exportState.status === "loading" ? (
                  <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
                ) : (
                  <Download className="h-4 w-4" aria-hidden="true" />
                )}
                {exportState.status === "loading" ? "Exporting..." : "Export CSV"}
              </Button>
              <Button
                className="w-full sm:w-auto"
                disabled={usageState.status === "loading"}
                onClick={() => void refreshUsage()}
                type="button"
                variant="secondary"
              >
                {usageState.status === "loading" ? (
                  <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
                ) : (
                  <RefreshCw className="h-4 w-4" aria-hidden="true" />
                )}
                Refresh
              </Button>
            </div>
            {exportState.status === "error" ? (
              <p className="max-w-xs text-xs font-semibold text-rust" role="alert">
                {exportState.message}
              </p>
            ) : null}
          </div>
        </div>
      </section>

      {usageState.status === "loading" ? (
        <div className="flex items-center gap-3 rounded-lg border border-line bg-white/75 px-4 py-6 text-sm text-ink/65 shadow-soft">
          <Loader2 className="h-5 w-5 animate-spin" aria-hidden="true" />
          Loading usage...
        </div>
      ) : null}

      {usageState.status === "error" ? (
        <div className="rounded-lg border border-rust/25 bg-rust/5 p-5 shadow-soft">
          <div className="flex items-start gap-3">
            <AlertCircle
              className="mt-0.5 h-5 w-5 text-rust"
              aria-hidden="true"
            />
            <div className="min-w-0">
              <p className="font-semibold text-rust">Could not load usage.</p>
              <p className="mt-1 text-sm text-ink/65">{usageState.message}</p>
              <Button
                className="mt-4"
                onClick={() => void refreshUsage()}
                type="button"
                variant="secondary"
              >
                Try again
              </Button>
            </div>
          </div>
        </div>
      ) : null}

      {readyState ? (
        <>
          <div className="grid gap-4 md:grid-cols-3">
            {cards.map((card) => (
              <SummaryCard
                bucket={card.bucket}
                key={card.label}
                label={card.label}
              />
            ))}
          </div>

          <section className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_300px]">
            <UsageBarChart points={readyState.series} />

            <Card className="p-5">
              <p className="font-mono text-[11px] font-semibold uppercase tracking-[0.16em] text-sage">
                Remaining quota
              </p>
              <p className="mt-4 text-4xl font-semibold tracking-normal text-ink">
                {summary.remaining}
                <span className="text-base font-medium text-ink/50">
                  {" "}
                  of {summary.quota}
                </span>
              </p>
              <p className="mt-2 text-sm text-ink/60">
                {summary.used} used this period. Period ends{" "}
                {formatPeriodEnd(summary.periodEnd)}.
              </p>

              {exhausted ? (
                <div className="mt-5 rounded-md border border-rust/25 bg-rust/5 p-4">
                  <p className="text-sm font-semibold text-rust">
                    Quota exhausted.
                  </p>
                  <p className="mt-1 text-sm text-ink/65">
                    Buy more rewrites or choose a plan with API access to keep
                    sending requests.
                  </p>
                  <LinkButton className="mt-4 w-full" href="/pricing">
                    View pricing
                  </LinkButton>
                </div>
              ) : null}
            </Card>
          </section>

          <section
            aria-labelledby="recent-api-calls-title"
            className="rounded-lg border border-line bg-white/80 p-6 shadow-soft sm:p-8"
          >
            <div className="flex flex-col gap-2 sm:flex-row sm:items-end sm:justify-between">
              <div>
                <h2 id="recent-api-calls-title" className="text-2xl">
                  Recent calls
                </h2>
                <p className="mt-2 text-sm text-ink/65">
                  Latest API requests across your keys.
                </p>
              </div>
              {noCalls ? (
                <span className="rounded-full border border-line bg-paper px-3 py-1 text-xs font-semibold text-ink/55">
                  No calls yet
                </span>
              ) : null}
            </div>

            {readyState.recent.length === 0 ? (
              <div className="mt-6 rounded-md border border-dashed border-line bg-paper px-4 py-8 text-sm text-ink/55">
                No calls yet. Recent API activity will appear here after your
                first request.
              </div>
            ) : (
              <div className="mt-6 overflow-x-auto">
                <table className="min-w-full text-left text-sm">
                  <thead className="border-b border-line bg-paper-deep/40 text-xs uppercase text-ink/50">
                    <tr>
                      <th className="px-4 py-3 font-semibold">Time</th>
                      <th className="px-4 py-3 font-semibold">Endpoint</th>
                      <th className="px-4 py-3 font-semibold">Status</th>
                      <th className="px-4 py-3 font-semibold">Latency</th>
                      <th className="px-4 py-3 font-semibold">Key</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-line">
                    {readyState.recent.map((call, index) => (
                      <tr
                        className="bg-white/45"
                        key={`${call.createdAt}-${call.endpoint}-${index}`}
                      >
                        <td className="whitespace-nowrap px-4 py-4">
                          {formatDateTime(call.createdAt)}
                        </td>
                        <td className="max-w-[280px] px-4 py-4 font-mono text-xs text-ink/70">
                          <span className="break-all">{call.endpoint}</span>
                        </td>
                        <td className="whitespace-nowrap px-4 py-4">
                          <span
                            className={`inline-flex rounded-full border px-2.5 py-1 text-xs font-semibold ${statusClass(
                              call.statusCode,
                            )}`}
                          >
                            {call.statusCode}
                          </span>
                        </td>
                        <td className="whitespace-nowrap px-4 py-4">
                          {formatLatency(call.latencyMs)}
                        </td>
                        <td className="whitespace-nowrap px-4 py-4 font-mono text-xs text-ink/65">
                          {formatKeyLast4(call.keyLast4)}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>
        </>
      ) : null}
    </div>
  );
}
