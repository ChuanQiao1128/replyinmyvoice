import type { Metadata } from "next";
import { redirect } from "next/navigation";

import { DeveloperDashboard } from "../../../components/developers/developer-dashboard";
import { SiteHeader } from "../../../components/site-header";
import { getCurrentSession } from "../../../lib/entra-auth";

export const dynamic = "force-dynamic";

export const metadata: Metadata = {
  title: "Developer dashboard",
  description:
    "Manage API keys, usage, and billing for your Reply In My Voice developer account.",
};

export default async function DeveloperApiKeysPage() {
  const session = await getCurrentSession();

  if (!session) {
    redirect("/sign-in");
  }

  return (
    <>
      <SiteHeader />
      <main className="rimv">
        <section className="wrap py-10 sm:py-14">
          <DeveloperDashboard />
        </section>
      </main>
    </>
  );
}
