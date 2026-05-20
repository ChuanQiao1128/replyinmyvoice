import Link from "next/link";

import { AdminShell } from "../../components/admin/admin-shell";
import { MetricCard } from "../../components/admin/metric-card";
import { RewriteCostTable } from "../../components/admin/rewrite-cost-table";
import { getAdminOverviewMetrics, getRecentRewriteCostLogs } from "../../lib/admin/metrics";
import { requireAdminUser } from "../../lib/admin-auth";
import { optionalEnv } from "../../lib/env";

export const dynamic = "force-dynamic";

function money(value: number | null) {
  if (value === null) {
    return "n/a";
  }
  return `$${value.toFixed(4)}`;
}

function number(value: number | null) {
  return value === null ? "n/a" : value.toFixed(1);
}

function percent(value: number | null) {
  return value === null ? "n/a" : `${Math.round(value * 100)}%`;
}

function nzd(value: number | null) {
  const rate = Number(optionalEnv("ADMIN_NZD_PER_USD", ""));
  if (value === null || !Number.isFinite(rate) || rate <= 0) {
    return null;
  }
  return `NZ$${(value * rate).toFixed(4)}`;
}

export default async function AdminPage() {
  await requireAdminUser();

  const [metrics, recent] = await Promise.all([
    getAdminOverviewMetrics(),
    getRecentRewriteCostLogs({ limit: 10 }),
  ]);
  const averageCostNzd = nzd(metrics.averageSuccessCostUsd30d);

  return (
    <AdminShell>
      <section className="grid gap-3 md:grid-cols-2 lg:grid-cols-4">
        <MetricCard
          helper={`${metrics.requests7d} in 7d / ${metrics.requests30d} in 30d`}
          label="Requests"
          value={String(metrics.requests24h)}
        />
        <MetricCard
          helper={`${metrics.qualityFailureCount30d} quality failures / ${metrics.serverFailureCount30d} server failures`}
          label="Success 30d"
          value={String(metrics.successCount30d)}
        />
        <MetricCard
          helper="Average measured draft-to-rewrite drop"
          label="Signal Drop"
          value={`${number(metrics.averageSignalDrop30d)} pts`}
        />
        <MetricCard
          helper={averageCostNzd ? `${averageCostNzd} estimated` : "USD estimate"}
          label="Avg Cost / Success"
          value={money(metrics.averageSuccessCostUsd30d)}
        />
        <MetricCard
          helper="Requests with final signal below 50%"
          label="Below 50%"
          value={percent(metrics.below50Rate30d)}
        />
        <MetricCard
          helper="95th percentile request cost"
          label="P95 Cost"
          value={money(metrics.p95CostUsd30d)}
        />
        <MetricCard
          helper={`OpenAI ${money(metrics.openAiCostUsd30d)} / Sapling ${money(metrics.saplingCostUsd30d)}`}
          label="Total AI Cost"
          value={money(metrics.totalCostUsd30d)}
        />
        <MetricCard
          helper={`${number(metrics.averageStrategies30d)} strategies avg`}
          label="Escalation Rate"
          value={percent(metrics.escalationRate30d)}
        />
      </section>

      <section className="mt-6 grid gap-4 lg:grid-cols-[1fr_340px]">
        <div>
          <div className="mb-3 flex items-center justify-between">
            <h2 className="text-xl font-semibold">Recent rewrites</h2>
            <Link className="text-sm font-semibold text-clay" href="/admin/rewrites">
              View all
            </Link>
          </div>
          <RewriteCostTable rows={recent} />
        </div>

        <aside className="rounded-lg border border-line bg-white p-4">
          <h2 className="text-lg font-semibold">Pricing support</h2>
          <p className="mt-2 text-sm text-ink/60">
            Internal estimates only. Use the first 50-100 real requests before
            finalizing quota and live price.
          </p>
          <div className="mt-4 space-y-3 text-sm">
            {[40, 50, 100].map((quota) => {
              const cost = metrics.averageSuccessCostUsd30d === null
                ? null
                : metrics.averageSuccessCostUsd30d * quota;
              return (
                <div className="flex justify-between gap-4" key={quota}>
                  <span>{quota} rewrites</span>
                  <span className="font-semibold">{money(cost)}</span>
                </div>
              );
            })}
          </div>
          {metrics.topScenarios.length ? (
            <div className="mt-5">
              <h3 className="text-sm font-semibold">Top cost scenarios</h3>
              <ul className="mt-2 space-y-2 text-sm text-ink/60">
                {metrics.topScenarios.map((scenario) => (
                  <li className="flex justify-between gap-3" key={scenario.scenario}>
                    <span>{scenario.scenario}</span>
                    <span>{money(scenario.totalCostUsd)}</span>
                  </li>
                ))}
              </ul>
            </div>
          ) : null}
        </aside>
      </section>
    </AdminShell>
  );
}
