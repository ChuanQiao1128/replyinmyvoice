import { notFound } from "next/navigation";

import { AdminShell } from "../../../../components/admin/admin-shell";
import { Card } from "../../../../components/ui/card";
import { getRewriteCostLogDetail } from "../../../../lib/admin/metrics";
import { requireAdminUser } from "../../../../lib/admin-auth";
import { optionalEnv } from "../../../../lib/env";

export const dynamic = "force-dynamic";

function money(value: number) {
  return `$${value.toFixed(6)}`;
}

function percent(value: number | null) {
  return value === null ? "n/a" : `${value}%`;
}

function dateTime(value: Date | null) {
  if (!value) {
    return "n/a";
  }
  return new Intl.DateTimeFormat("en-NZ", {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(value);
}

export default async function AdminRewriteDetailPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  await requireAdminUser();
  const { id } = await params;
  const detail = await getRewriteCostLogDetail(id);

  if (!detail) {
    notFound();
  }

  const showRawText = optionalEnv("ADMIN_ALLOW_RAW_REWRITE_TEXT", "false") === "true";

  return (
    <AdminShell>
      <div className="mb-4">
        <h2 className="text-2xl font-semibold">Rewrite detail</h2>
        <p className="mt-1 text-sm text-ink/55">{detail.requestId}</p>
      </div>

      <section className="grid gap-4 lg:grid-cols-3">
        <Card className="p-4">
          <h3 className="font-semibold">Request</h3>
          <dl className="mt-3 space-y-2 text-sm text-ink/65">
            <div className="flex justify-between gap-3">
              <dt>Status</dt>
              <dd className="font-semibold text-ink">{detail.status}</dd>
            </div>
            <div className="flex justify-between gap-3">
              <dt>User</dt>
              <dd>{detail.userEmail ?? detail.userId ?? "Unknown"}</dd>
            </div>
            <div className="flex justify-between gap-3">
              <dt>Scenario</dt>
              <dd>{detail.scenario}</dd>
            </div>
            <div className="flex justify-between gap-3">
              <dt>Tone</dt>
              <dd>{detail.tonePreset}</dd>
            </div>
            <div className="flex justify-between gap-3">
              <dt>Started</dt>
              <dd>{dateTime(detail.startedAt)}</dd>
            </div>
            <div className="flex justify-between gap-3">
              <dt>Duration</dt>
              <dd>{detail.durationMs === null ? "n/a" : `${detail.durationMs} ms`}</dd>
            </div>
          </dl>
        </Card>

        <Card className="p-4">
          <h3 className="font-semibold">Signal</h3>
          <dl className="mt-3 space-y-2 text-sm text-ink/65">
            <div className="flex justify-between gap-3">
              <dt>Draft</dt>
              <dd>{percent(detail.draftAiLikePercent)}</dd>
            </div>
            <div className="flex justify-between gap-3">
              <dt>Rewrite</dt>
              <dd>{percent(detail.rewriteAiLikePercent)}</dd>
            </div>
            <div className="flex justify-between gap-3">
              <dt>Change</dt>
              <dd>{detail.changePoints === null ? "n/a" : `${detail.changePoints} pts`}</dd>
            </div>
            <div className="flex justify-between gap-3">
              <dt>Strategies</dt>
              <dd>{detail.internalStrategies}</dd>
            </div>
            <div className="flex justify-between gap-3">
              <dt>Repairs</dt>
              <dd>{detail.repairCandidates}</dd>
            </div>
            <div className="flex justify-between gap-3">
              <dt>Strong model</dt>
              <dd>{detail.usedEscalation ? "yes" : "no"}</dd>
            </div>
          </dl>
        </Card>

        <Card className="p-4">
          <h3 className="font-semibold">Cost</h3>
          <dl className="mt-3 space-y-2 text-sm text-ink/65">
            <div className="flex justify-between gap-3">
              <dt>Total</dt>
              <dd className="font-semibold text-ink">{money(detail.totalEstimatedCostUsd)}</dd>
            </div>
            <div className="flex justify-between gap-3">
              <dt>OpenAI</dt>
              <dd>{money(detail.openAiCostUsd)}</dd>
            </div>
            <div className="flex justify-between gap-3">
              <dt>Sapling</dt>
              <dd>{money(detail.saplingCostUsd)}</dd>
            </div>
            <div className="flex justify-between gap-3">
              <dt>Tokens</dt>
              <dd>{detail.openAiInputTokens} in / {detail.openAiOutputTokens} out</dd>
            </div>
            <div className="flex justify-between gap-3">
              <dt>Sapling chars</dt>
              <dd>{detail.saplingCharacters}</dd>
            </div>
            <div className="flex justify-between gap-3">
              <dt>Models</dt>
              <dd className="text-right">{detail.modelsUsed.join(", ") || "n/a"}</dd>
            </div>
          </dl>
        </Card>
      </section>

      <section className="mt-5">
        <h3 className="mb-3 text-xl font-semibold">Provider calls</h3>
        <div className="overflow-x-auto rounded-lg border border-line bg-white">
          <table className="min-w-full divide-y divide-line text-left text-sm">
            <thead className="bg-paper-deep text-xs uppercase tracking-[0.08em] text-ink/45">
              <tr>
                <th className="px-4 py-3">Provider</th>
                <th className="px-4 py-3">Role</th>
                <th className="px-4 py-3">Model</th>
                <th className="px-4 py-3">Usage</th>
                <th className="px-4 py-3">Cost</th>
                <th className="px-4 py-3">Status</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-line">
              {detail.providerCalls.map((call) => (
                <tr key={call.id}>
                  <td className="px-4 py-3">{call.provider}</td>
                  <td className="px-4 py-3">{call.role}</td>
                  <td className="px-4 py-3">{call.model ?? "n/a"}</td>
                  <td className="px-4 py-3 text-ink/65">
                    {call.provider === "openai"
                      ? `${call.inputTokens ?? 0} in / ${call.outputTokens ?? 0} out`
                      : `${call.characters ?? 0} chars`}
                  </td>
                  <td className="px-4 py-3">{money(call.estimatedCostUsd)}</td>
                  <td className="px-4 py-3">
                    {call.success ? "success" : call.errorCode ?? "failed"}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>

      <section className="mt-5">
        <h3 className="mb-3 text-xl font-semibold">Sample content</h3>
        {showRawText && detail.learningSample ? (
          <div className="grid gap-4 lg:grid-cols-2">
            <Card className="p-4">
              <h4 className="font-semibold">Draft</h4>
              <pre className="mt-3 whitespace-pre-wrap text-sm leading-6 text-ink/70">
                {detail.learningSample.roughDraftReply}
              </pre>
            </Card>
            <Card className="p-4">
              <h4 className="font-semibold">Rewrite</h4>
              <pre className="mt-3 whitespace-pre-wrap text-sm leading-6 text-ink/70">
                {detail.learningSample.rewrittenText ?? "No rewritten text stored."}
              </pre>
            </Card>
          </div>
        ) : (
          <Card className="p-4 text-sm text-ink/60">
            Raw rewrite text is hidden in this environment. Set
            `ADMIN_ALLOW_RAW_REWRITE_TEXT=true` only for approved internal debugging.
          </Card>
        )}
      </section>
    </AdminShell>
  );
}
