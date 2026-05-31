import { redirect } from "next/navigation";

import { AdminUserDetail } from "../../../../components/admin/admin-user-detail";
import { SiteHeader } from "../../../../components/site-header";
import { isAdminSession } from "../../../../lib/admin-auth";
import { getCurrentSession } from "../../../../lib/entra-auth";

export const dynamic = "force-dynamic";

type PageProps = {
  params: Promise<{ userId: string }>;
};

function AdminDenied() {
  return (
    <>
      <SiteHeader />
      <main className="wrap py-16">
        <div className="max-w-2xl rounded-lg border border-line bg-white/70 p-8 shadow-soft">
          <p className="eyebrow">
            <span className="dot" aria-hidden="true" />
            Operations
          </p>
          <h1 className="mt-3 text-3xl font-semibold tracking-normal text-ink">
            Admin access required
          </h1>
          <p className="mt-3 text-sm leading-6 text-ink/65">
            Your signed-in account is not allowed to open this area.
          </p>
        </div>
      </main>
    </>
  );
}

export default async function AdminUserPage({ params }: PageProps) {
  const session = await getCurrentSession();
  if (!session) {
    redirect("/sign-in");
  }

  if (!isAdminSession(session)) {
    return <AdminDenied />;
  }

  const { userId } = await params;

  return (
    <>
      <SiteHeader />
      <AdminUserDetail userId={userId} />
    </>
  );
}
