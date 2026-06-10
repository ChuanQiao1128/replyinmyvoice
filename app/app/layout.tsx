import type { ReactNode } from "react";
import { cookies } from "next/headers";
import { redirect } from "next/navigation";

import { AppShell } from "../../components/app/shell/app-shell";
import type { ShellAccount } from "../../components/app/shell/shell-types";
import { isAdminSession } from "../../lib/admin-auth";
import { fetchAzureAccountSummary } from "../../lib/azure-api";
import { getCurrentSession } from "../../lib/entra-auth";

export const dynamic = "force-dynamic";

const paidStatuses = new Set(["active", "trialing", "testing"]);

export default async function AppLayout({
  children,
}: {
  children: ReactNode;
}) {
  const account = await fetchAzureAccountSummary();

  if (!account) {
    redirect("/sign-in");
  }

  const session = await getCurrentSession();
  const isAdmin = session ? isAdminSession(session) : false;
  const cookieStore = await cookies();
  const devModeDefault = cookieStore.get("rimv_devmode")?.value === "1";

  // Pro/API subscription → API access → the Developers group shows by default.
  const isDeveloperTier = paidStatuses.has(account.subscriptionStatus);

  const shellAccount: ShellAccount = {
    email: account.email,
    isDeveloperTier,
    isAdmin,
    quota: {
      remaining: account.usage.remaining,
      quota: account.usage.quota,
      scopeLabel: isDeveloperTier ? "web + API" : "web",
    },
    rewriteHistoryUserKey: account.externalAuthUserId || account.userId,
  };

  return (
    <AppShell account={shellAccount} devModeDefault={devModeDefault}>
      {children}
    </AppShell>
  );
}
