import type { Metadata } from "next";
import { redirect } from "next/navigation";

import { ApiKeysPanel } from "../../../components/developers/api-keys-panel";
import { SiteHeader } from "../../../components/site-header";
import { getCurrentSession } from "../../../lib/entra-auth";

export const dynamic = "force-dynamic";

export const metadata: Metadata = {
  title: "API keys",
  description: "Create and revoke Reply In My Voice API keys for your account.",
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
          <ApiKeysPanel />
        </section>
      </main>
    </>
  );
}
