import type { ReactNode } from "react";
import { redirect } from "next/navigation";

import { AppShell } from "../../components/app/shell/app-shell";
import {
  isDeveloperTierStatus,
  planLabelForStatus,
  type ShellAccount,
} from "../../components/app/shell/shell-types";
import { isAdminSession } from "../../lib/admin-auth";
import { fetchAzureAccountSummary } from "../../lib/azure-api";
import { getCurrentSession } from "../../lib/entra-auth";

export const dynamic = "force-dynamic";

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
  const isDeveloperTier = isDeveloperTierStatus(account.subscriptionStatus);

  const shellAccount: ShellAccount = {
    email: account.email,
    planLabel: planLabelForStatus(account.subscriptionStatus),
    isDeveloperTier,
    isAdmin,
    quota: {
      remaining: account.usage.remaining,
      quota: account.usage.quota,
      scopeLabel: isDeveloperTier ? "web + API" : "web",
    },
    rewriteHistoryUserKey: account.externalAuthUserId || account.userId,
  };

  return <AppShell account={shellAccount}>{children}</AppShell>;
}
