import { redirect } from "next/navigation";

import { PaywallCard } from "../../components/app/paywall-card";
import { RewriteWorkspace } from "../../components/app/rewrite-workspace";
import { SiteHeader } from "../../components/site-header";
import { shouldShowAdminEntry } from "../../lib/admin-visible";
import { getUsageStatus, isPaidSubscriptionStatus } from "../../lib/quota";
import { getCurrentAppUser } from "../../lib/users";

export const dynamic = "force-dynamic";

export default async function AppPage() {
  const user = await getCurrentAppUser();

  if (!user) {
    redirect("/sign-in");
  }

  const usage = await getUsageStatus(user);
  const paid = isPaidSubscriptionStatus(user.subscriptionStatus);
  const showAdmin = shouldShowAdminEntry({
    userId: user.clerkUserId,
    email: user.email,
  });

  if (usage.exhausted && !paid) {
    return (
      <>
        <SiteHeader showAdmin={showAdmin} />
        <PaywallCard
          description="Your 3 free rewrites have been used. Upgrade to keep using the workspace for everyday replies."
          status="Free quota used"
          title="Keep writing in your own voice."
        />
      </>
    );
  }

  if (usage.exhausted && paid) {
    return (
      <>
        <SiteHeader showAdmin={showAdmin} />
        <PaywallCard
          action="portal"
          description="Your monthly rewrite quota has been used for this billing period. You can manage billing or come back when the next period starts."
          status="Monthly quota used"
          title="Your monthly limit has been reached."
        />
      </>
    );
  }

  const usageLabel = paid
    ? `${usage.remaining} of ${usage.quota} rewrites remaining this billing period`
    : `${usage.remaining} of ${usage.quota} free rewrites remaining`;

  return (
    <>
      <SiteHeader showAdmin={showAdmin} />
      <RewriteWorkspace
        paid={paid}
        subscriptionStatus={user.subscriptionStatus}
        usageLabel={usageLabel}
      />
    </>
  );
}
