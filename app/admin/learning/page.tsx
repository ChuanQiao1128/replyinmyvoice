import {
  CheckCircle2,
  ExternalLink,
  RefreshCcw,
  XCircle,
} from "lucide-react";
import { revalidatePath } from "next/cache";

import { AdminShell } from "../../../components/admin/admin-shell";
import { Button } from "../../../components/ui/button";
import {
  getRecentLearningRuns,
  parseStrategyCandidateReviewStatus,
  updateStrategyCandidateStatus,
  type AdminLearningFinding,
  type AdminLearningRun,
  type AdminStrategyCandidate,
  type StrategyCandidateReviewStatus,
} from "../../../lib/admin/learning";
import { requireAdminUser } from "../../../lib/admin-auth";

export const dynamic = "force-dynamic";

function dateTime(value: Date | null) {
  if (!value) {
    return "n/a";
  }

  return new Intl.DateTimeFormat("en-NZ", {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(value);
}

function statusLabel(value: string | null) {
  return value?.replaceAll("_", " ") ?? "n/a";
}

function statusBadgeClass(status: string) {
  if (status === "approved" || status === "promoted") {
    return "border-emerald-200 bg-emerald-50 text-emerald-800";
  }

  if (status === "needs_revision" || status === "blocked") {
    return "border-amber-200 bg-amber-50 text-amber-800";
  }

  if (status === "rejected") {
    return "border-red-200 bg-red-50 text-red-800";
  }

  return "border-line bg-paper text-ink/70";
}

async function updateCandidateReview(formData: FormData) {
  "use server";

  await requireAdminUser();

  const candidateId = String(formData.get("candidateId") ?? "");
  const status = parseStrategyCandidateReviewStatus(formData.get("status"));

  if (!status) {
    throw new Error("Unsupported strategy candidate status");
  }

  await updateStrategyCandidateStatus({ candidateId, status });
  revalidatePath("/admin/learning");
}

function StatusBadge({ status }: { status: string }) {
  return (
    <span
      className={`inline-flex rounded-full border px-2.5 py-1 text-xs font-semibold capitalize ${statusBadgeClass(status)}`}
    >
      {statusLabel(status)}
    </span>
  );
}

function RunList({ runs }: { runs: AdminLearningRun[] }) {
  return (
    <aside className="space-y-3">
      <h2 className="text-sm font-semibold uppercase tracking-[0.12em] text-ink/50">
        Recent runs
      </h2>
      {runs.map((run) => (
        <a
          className="block rounded-lg border border-line bg-white p-3 text-sm transition hover:border-clay/40 hover:bg-paper"
          href={`#run-${run.id}`}
          key={run.id}
        >
          <div className="flex items-center justify-between gap-3">
            <span className="font-semibold text-ink">{dateTime(run.startedAt)}</span>
            <StatusBadge status={run.status} />
          </div>
          <div className="mt-2 flex flex-wrap gap-x-3 gap-y-1 text-xs text-ink/55">
            <span>{run.findingCount} findings</span>
            <span>{run.candidateCount} candidates</span>
            <span>{statusLabel(run.promotionDecision)}</span>
          </div>
        </a>
      ))}
    </aside>
  );
}

function CandidateActionButton({
  currentStatus,
  icon,
  label,
  status,
  variant,
}: {
  currentStatus: string;
  icon: React.ReactNode;
  label: string;
  status: StrategyCandidateReviewStatus;
  variant: "primary" | "secondary" | "ghost" | "clay";
}) {
  return (
    <Button
      className="min-w-[8.75rem]"
      disabled={currentStatus === status}
      name="status"
      title={label}
      type="submit"
      value={status}
      variant={variant}
    >
      {icon}
      {label}
    </Button>
  );
}

function CandidatePanel({ candidate }: { candidate: AdminStrategyCandidate }) {
  return (
    <div className="rounded-md border border-line bg-paper/70 p-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h4 className="font-semibold">{candidate.title}</h4>
          <div className="mt-2 flex flex-wrap gap-2 text-xs text-ink/55">
            <span>{candidate.patchTarget ?? "no patch target"}</span>
            <span>{candidate.patchAction ?? "no patch action"}</span>
            <span>{candidate.riskLevel} risk</span>
            <span>{candidate.evidenceCount} cases</span>
          </div>
        </div>
        <StatusBadge status={candidate.status} />
      </div>

      <dl className="mt-4 grid gap-3 text-sm md:grid-cols-2">
        <div>
          <dt className="font-semibold text-ink">Proposed change</dt>
          <dd className="mt-1 text-ink/65">{candidate.proposedChangeSummary}</dd>
        </div>
        <div>
          <dt className="font-semibold text-ink">Required eval</dt>
          <dd className="mt-1 text-ink/65">{candidate.requiredEval}</dd>
        </div>
        <div className="md:col-span-2">
          <dt className="font-semibold text-ink">Candidate patch</dt>
          <dd className="mt-1 whitespace-pre-wrap rounded-md border border-line bg-white p-3 font-mono text-xs leading-5 text-ink/70">
            {candidate.patchText ?? "No structured patch text stored."}
          </dd>
        </div>
        {candidate.requiredRegressionTest ? (
          <div className="md:col-span-2">
            <dt className="font-semibold text-ink">Regression test</dt>
            <dd className="mt-1 text-ink/65">
              {candidate.requiredRegressionTest}
            </dd>
          </div>
        ) : null}
      </dl>

      <div className="mt-4 flex flex-wrap items-center justify-between gap-3 border-t border-line pt-4">
        {candidate.linkedWorkHref ? (
          <a
            className="inline-flex items-center gap-1.5 text-sm font-semibold text-clay hover:text-clay/80"
            href={candidate.linkedWorkHref}
            rel="noreferrer"
            target="_blank"
          >
            Linked work
            <ExternalLink aria-hidden="true" className="h-4 w-4" />
          </a>
        ) : (
          <span className="text-sm text-ink/45">No linked work</span>
        )}

        <form
          action={updateCandidateReview}
          className="flex flex-wrap gap-2"
        >
          <input name="candidateId" type="hidden" value={candidate.id} />
          <CandidateActionButton
            currentStatus={candidate.status}
            icon={<CheckCircle2 aria-hidden="true" className="h-4 w-4" />}
            label="Approve"
            status="approved"
            variant="clay"
          />
          <CandidateActionButton
            currentStatus={candidate.status}
            icon={<RefreshCcw aria-hidden="true" className="h-4 w-4" />}
            label="Needs revision"
            status="needs_revision"
            variant="secondary"
          />
          <CandidateActionButton
            currentStatus={candidate.status}
            icon={<XCircle aria-hidden="true" className="h-4 w-4" />}
            label="Reject"
            status="rejected"
            variant="ghost"
          />
        </form>
      </div>
    </div>
  );
}

function FindingPanel({ finding }: { finding: AdminLearningFinding }) {
  return (
    <article className="border-t border-line pt-5 first:border-t-0 first:pt-0">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h3 className="text-lg font-semibold">
            {finding.primaryDiagnosisTag ?? finding.failureType}
          </h3>
          <div className="mt-2 flex flex-wrap gap-2 text-xs text-ink/55">
            <span>{finding.scenario ?? "all scenarios"}</span>
            <span>{finding.commonTone ?? "mixed tone"}</span>
            <span>{finding.failureType}</span>
            <span>{finding.severity} severity</span>
            <span>{finding.evidenceCount} cases</span>
          </div>
        </div>
        <StatusBadge status={finding.promotionRecommendation} />
      </div>

      <p className="mt-3 text-sm leading-6 text-ink/65">
        {finding.recommendation}
      </p>

      <div className="mt-4 grid gap-4 lg:grid-cols-[1fr_1.4fr]">
        <div>
          <h4 className="text-sm font-semibold">Cluster details</h4>
          <div className="mt-2 flex flex-wrap gap-2">
            {finding.diagnosisTags.length ? (
              finding.diagnosisTags.map((tag) => (
                <span
                  className="rounded-full border border-line bg-white px-2.5 py-1 text-xs text-ink/60"
                  key={tag}
                >
                  {tag}
                </span>
              ))
            ) : (
              <span className="text-sm text-ink/45">No diagnosis tags stored.</span>
            )}
          </div>

          <h4 className="mt-4 text-sm font-semibold">Evidence cases</h4>
          <ul className="mt-2 space-y-1 text-sm text-ink/60">
            {finding.sampleRefs.length ? (
              finding.sampleRefs.map((ref) => <li key={ref}>{ref}</li>)
            ) : (
              <li>No evidence refs stored.</li>
            )}
          </ul>
        </div>

        <div className="space-y-3">
          <h4 className="text-sm font-semibold">Proposed candidates</h4>
          {finding.candidates.length ? (
            finding.candidates.map((candidate) => (
              <CandidatePanel candidate={candidate} key={candidate.id} />
            ))
          ) : (
            <div className="rounded-md border border-line bg-paper/70 p-4 text-sm text-ink/55">
              No strategy candidate was generated for this finding.
            </div>
          )}
        </div>
      </div>
    </article>
  );
}

function RunPanel({ run }: { run: AdminLearningRun }) {
  return (
    <section
      className="rounded-lg border border-line bg-white p-4"
      id={`run-${run.id}`}
    >
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h2 className="text-xl font-semibold">Run {run.id}</h2>
          <p className="mt-1 text-sm text-ink/55">
            Started {dateTime(run.startedAt)} · finished {dateTime(run.finishedAt)}
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          <StatusBadge status={run.status} />
          <StatusBadge status={run.promotionDecision} />
        </div>
      </div>

      <dl className="mt-4 grid gap-3 text-sm sm:grid-cols-2 lg:grid-cols-4">
        <div>
          <dt className="text-ink/50">Samples</dt>
          <dd className="font-semibold">{run.sampleCount}</dd>
        </div>
        <div>
          <dt className="text-ink/50">Measured</dt>
          <dd className="font-semibold">{run.measuredCount}</dd>
        </div>
        <div>
          <dt className="text-ink/50">Findings</dt>
          <dd className="font-semibold">{run.findingCount}</dd>
        </div>
        <div>
          <dt className="text-ink/50">Candidates</dt>
          <dd className="font-semibold">{run.candidateCount}</dd>
        </div>
      </dl>

      <div className="mt-4 flex flex-wrap gap-3 text-sm text-ink/55">
        <span>Validation: {statusLabel(run.validationStatus)}</span>
        <span>Digest: {run.digestPath ?? "n/a"}</span>
        {run.errorMessage ? <span>Error: {run.errorMessage}</span> : null}
      </div>

      <div className="mt-5 space-y-5">
        {run.findings.length ? (
          run.findings.map((finding) => (
            <FindingPanel finding={finding} key={finding.id} />
          ))
        ) : (
          <div className="rounded-md border border-line bg-paper p-4 text-sm text-ink/55">
            No findings were recorded for this run.
          </div>
        )}
      </div>
    </section>
  );
}

export default async function AdminLearningPage() {
  await requireAdminUser();
  const runs = await getRecentLearningRuns({ limit: 12 });

  return (
    <AdminShell>
      <div className="mb-5">
        <h2 className="text-2xl font-semibold">LearningOps review</h2>
        <p className="mt-1 text-sm text-ink/55">
          Recent learning runs, finding clusters, and strategy candidates.
        </p>
      </div>

      {runs.length ? (
        <section className="grid gap-5 lg:grid-cols-[300px_1fr]">
          <RunList runs={runs} />
          <div className="space-y-5">
            {runs.map((run) => (
              <RunPanel key={run.id} run={run} />
            ))}
          </div>
        </section>
      ) : (
        <div className="rounded-lg border border-line bg-white p-6 text-sm text-ink/60">
          No LearningOps runs have been recorded yet.
        </div>
      )}
    </AdminShell>
  );
}
