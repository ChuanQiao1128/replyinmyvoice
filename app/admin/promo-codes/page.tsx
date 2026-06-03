import type { Metadata } from "next";
import { redirect } from "next/navigation";

import { PromoCodesAdmin } from "../../../components/admin/promo-codes-admin";
import { SiteHeader } from "../../../components/site-header";
import { adminPromoCodesFromPayload } from "../../../lib/admin-promo-codes";
import { getAzureApiBaseUrl } from "../../../lib/azure-api";
import { getCurrentAccessToken } from "../../../lib/entra-auth";

export const dynamic = "force-dynamic";

export const metadata: Metadata = {
  title: "Promo codes admin",
  description: "Admin promo code management for Reply In My Voice.",
};

const adminPath = "/admin/promo-codes";

async function readJsonPayload(response: Response) {
  try {
    return await response.json();
  } catch {
    return null;
  }
}

function NoPermissionView() {
  return (
    <>
      <SiteHeader />
      <main className="min-h-screen bg-paper">
        <section className="wrap py-16">
          <div className="max-w-2xl rounded-lg border border-line bg-white/80 p-8 shadow-crisp">
            <p className="text-sm font-semibold uppercase text-clay">Admin</p>
            <h1 className="mt-3 text-3xl font-semibold text-ink">
              Admin access required
            </h1>
            <p className="mt-3 text-ink/70">
              You do not have permission to manage promo codes. Sign in with an
              approved admin account to continue.
            </p>
          </div>
        </section>
      </main>
    </>
  );
}

export default async function AdminPromoCodesPage() {
  const accessToken = await getCurrentAccessToken();
  if (!accessToken) {
    redirect(`/sign-in?redirectTo=${adminPath}`);
  }

  let response: Response;
  try {
    response = await fetch(`${getAzureApiBaseUrl()}/api/console/promo-codes`, {
      cache: "no-store",
      headers: {
        Authorization: `Bearer ${accessToken}`,
      },
    });
  } catch {
    return (
      <>
        <SiteHeader />
        <PromoCodesAdmin
          initialCodes={[]}
          initialError="Could not load promo codes."
        />
      </>
    );
  }

  if (response.status === 401) {
    redirect(`/sign-in?redirectTo=${adminPath}`);
  }

  if (response.status === 403) {
    return <NoPermissionView />;
  }

  const payload = await readJsonPayload(response);
  const initialCodes = response.ok ? adminPromoCodesFromPayload(payload) : [];
  const initialError = response.ok ? "" : "Could not load promo codes.";

  return (
    <>
      <SiteHeader />
      <PromoCodesAdmin initialCodes={initialCodes} initialError={initialError} />
    </>
  );
}
