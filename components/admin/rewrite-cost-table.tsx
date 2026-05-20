import Link from "next/link";

import type { RecentRewriteCostLog } from "../../lib/admin/metrics";

function percent(value: number | null) {
  return value === null ? "n/a" : `${value}%`;
}

function money(value: number) {
  return `$${value.toFixed(4)}`;
}

function dateTime(value: Date) {
  return new Intl.DateTimeFormat("en-NZ", {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(value);
}

export function RewriteCostTable({ rows }: { rows: RecentRewriteCostLog[] }) {
  if (rows.length === 0) {
    return (
      <div className="rounded-lg border border-line bg-white p-6 text-sm text-ink/60">
        No rewrite cost logs yet.
      </div>
    );
  }

  return (
    <div className="overflow-x-auto rounded-lg border border-line bg-white">
      <table className="min-w-full divide-y divide-line text-left text-sm">
        <thead className="bg-paper-deep text-xs uppercase tracking-[0.08em] text-ink/45">
          <tr>
            <th className="px-4 py-3">Date</th>
            <th className="px-4 py-3">User</th>
            <th className="px-4 py-3">Scenario</th>
            <th className="px-4 py-3">Status</th>
            <th className="px-4 py-3">Signal</th>
            <th className="px-4 py-3">Loop</th>
            <th className="px-4 py-3">Cost</th>
            <th className="px-4 py-3" />
          </tr>
        </thead>
        <tbody className="divide-y divide-line">
          {rows.map((row) => (
            <tr key={row.id}>
              <td className="whitespace-nowrap px-4 py-3 text-ink/65">
                {dateTime(row.createdAt)}
              </td>
              <td className="px-4 py-3 text-ink/65">
                {row.userEmail ?? row.userId ?? "Unknown"}
              </td>
              <td className="px-4 py-3">
                <div className="font-semibold text-ink">{row.scenario}</div>
                <div className="text-xs text-ink/45">{row.tonePreset}</div>
              </td>
              <td className="px-4 py-3">
                <span className="rounded-md bg-paper-deep px-2 py-1 text-xs font-semibold text-ink/65">
                  {row.status}
                </span>
              </td>
              <td className="whitespace-nowrap px-4 py-3 text-ink/65">
                {percent(row.draftAiLikePercent)} → {percent(row.rewriteAiLikePercent)}
                <div className="text-xs text-ink/45">
                  {row.changePoints === null ? "n/a" : `${row.changePoints} pts`}
                </div>
              </td>
              <td className="whitespace-nowrap px-4 py-3 text-ink/65">
                {row.internalStrategies}s / {row.repairCandidates}r
                {row.usedEscalation ? (
                  <span className="ml-2 rounded bg-clay/10 px-1.5 py-0.5 text-xs text-clay">
                    strong
                  </span>
                ) : null}
              </td>
              <td className="whitespace-nowrap px-4 py-3 font-semibold">
                {money(row.totalEstimatedCostUsd)}
              </td>
              <td className="px-4 py-3 text-right">
                <Link className="font-semibold text-clay" href={`/admin/rewrites/${row.id}`}>
                  View
                </Link>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
