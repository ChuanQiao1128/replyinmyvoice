import type { Metadata } from "next";
import Link from "next/link";

import {
  DeveloperUpsell,
  PageHeader,
} from "../../../components/app/shell/shell-primitives";
import { isDeveloperTierStatus } from "../../../components/app/shell/shell-types";
import { DeveloperDashboard } from "../../../components/developers/developer-dashboard";
import { fetchAzureAccountSummary } from "../../../lib/azure-api";

export const dynamic = "force-dynamic";
export const metadata: Metadata = { title: "API keys" };

function ApiDocsLink() {
  return (
    <div className="mb-4">
      <Link href="/developers/api" className="btn btn-ghost">
        &larr; API docs
      </Link>
    </div>
  );
}

// The developer console lives inside the app shell; /developers/keys 301s
// here. Accounts without Pro/API see the upsell instead of an error.
export default async function KeysPage() {
  const account = await fetchAzureAccountSummary();
  const subscriptionStatus = account?.subscriptionStatus ?? "inactive";

  if (!isDeveloperTierStatus(subscriptionStatus)) {
    return (
      <>
        <ApiDocsLink />
        <PageHeader
          title="API keys"
          description="Create and manage keys for the REST API and MCP server."
        />
        <DeveloperUpsell />
      </>
    );
  }

  return (
    <div className="rimv">
      <ApiDocsLink />
      <DeveloperDashboard
        initialTab="keys"
        paymentGraceEndsAt={account?.paymentGraceEndsAt ?? null}
        subscriptionStatus={subscriptionStatus}
      />
    </div>
  );
}
