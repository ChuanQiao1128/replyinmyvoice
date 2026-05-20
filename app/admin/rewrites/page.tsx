import { AdminShell } from "../../../components/admin/admin-shell";
import { RewriteCostTable } from "../../../components/admin/rewrite-cost-table";
import { getRecentRewriteCostLogs } from "../../../lib/admin/metrics";
import { requireAdminUser } from "../../../lib/admin-auth";

export const dynamic = "force-dynamic";

export default async function AdminRewritesPage() {
  await requireAdminUser();
  const rows = await getRecentRewriteCostLogs({ limit: 75 });

  return (
    <AdminShell>
      <div className="mb-4">
        <h2 className="text-2xl font-semibold">Rewrite requests</h2>
        <p className="mt-1 text-sm text-ink/55">
          Recent request-level quality and estimated cost telemetry.
        </p>
      </div>
      <RewriteCostTable rows={rows} />
    </AdminShell>
  );
}
