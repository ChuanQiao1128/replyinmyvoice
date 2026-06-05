import type { Metadata } from "next";
import { redirect } from "next/navigation";

import { DeveloperDashboard } from "../../../components/developers/developer-dashboard";
import { SiteHeader } from "../../../components/site-header";
import { fetchAzureAccountSummary } from "../../../lib/azure-api";

export const dynamic = "force-dynamic";

export const metadata: Metadata = {
  title: "Developer dashboard",
  description:
    "Manage API keys, usage, and billing for your Reply In My Voice developer account.",
};

export default async function DeveloperApiKeysPage() {
  const account = await fetchAzureAccountSummary();

  if (!account) {
    redirect("/sign-in");
  }

  return (
    <>
      <SiteHeader />
      <main className="rimv">
        <section className="wrap py-10 sm:py-14">
          <DeveloperDashboard
            paymentGraceEndsAt={account.paymentGraceEndsAt}
            subscriptionStatus={account.subscriptionStatus}
          />
        </section>
      </main>
    </>
  );
}
