"use client";

import { BarChart3, CreditCard, KeyRound } from "lucide-react";
import Link from "next/link";
import { usePathname, useSearchParams } from "next/navigation";
import { useId } from "react";

import { PastDueBanner } from "../app/past-due-banner";
import { ApiKeysPanel } from "./api-keys-panel";
import { BillingPanel } from "./billing-panel";
import { UsagePanel } from "./usage-panel";

type DashboardTab = "keys" | "usage" | "billing";

const tabs: {
  description: string;
  href: string;
  icon: typeof KeyRound;
  id: DashboardTab;
  label: string;
}[] = [
  {
    description: "Create, reveal, and revoke API keys.",
    href: "/app/keys",
    icon: KeyRound,
    id: "keys",
    label: "Keys",
  },
  {
    description: "Review call volume, quota, and recent requests.",
    href: "/app/usage",
    icon: BarChart3,
    id: "usage",
    label: "Usage",
  },
  {
    description: "Plan and payment records.",
    href: "/app/keys?tab=billing",
    icon: CreditCard,
    id: "billing",
    label: "Billing",
  },
];

type Props = {
  paymentGraceEndsAt: string | null;
  subscriptionStatus: string;
  initialTab?: DashboardTab;
};

function routeMatches(pathname: string | null, href: string) {
  return pathname === href || pathname?.startsWith(`${href}/`);
}

function tabFromRoute(
  pathname: string | null,
  tabParam: string | null,
  fallback: DashboardTab,
): DashboardTab {
  if (routeMatches(pathname, "/app/usage")) {
    return "usage";
  }

  if (routeMatches(pathname, "/app/keys")) {
    return tabParam === "billing" ? "billing" : "keys";
  }

  return fallback;
}

export function DeveloperDashboard({
  paymentGraceEndsAt,
  subscriptionStatus,
  initialTab = "keys",
}: Props) {
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const activeTab = tabFromRoute(pathname, searchParams.get("tab"), initialTab);
  const tabGroupId = useId();

  return (
    <div className="space-y-6">
      {subscriptionStatus === "PastDue" ? (
        <PastDueBanner paymentGraceEndsAt={paymentGraceEndsAt} />
      ) : null}

      <section className="rounded-lg border border-line bg-white/80 p-6 shadow-soft sm:p-8">
        <div className="flex flex-col gap-5 lg:flex-row lg:items-end lg:justify-between">
          <div className="min-w-0 space-y-3">
            <span className="inline-flex items-center gap-2 font-mono text-[11px] font-semibold uppercase tracking-[0.16em] text-sage">
              <KeyRound className="h-4 w-4" aria-hidden="true" />
              Developer portal
            </span>
            <div>
              <h1 className="break-words text-4xl sm:text-5xl">
                Developer dashboard
              </h1>
              <p className="mt-3 max-w-2xl text-base text-ink/65">
                Manage keys, track API usage, and keep billing details in one
                signed-in workspace.
              </p>
            </div>
          </div>

          <div
            aria-label="Developer dashboard sections"
            className="grid gap-2 rounded-lg border border-line bg-paper p-1 sm:inline-grid sm:grid-cols-3"
            role="tablist"
          >
            {tabs.map((tab) => {
              const Icon = tab.icon;
              const selected = activeTab === tab.id;

              return (
                <Link
                  aria-controls={`${tabGroupId}-${tab.id}-panel`}
                  aria-current={selected ? "page" : undefined}
                  aria-selected={selected}
                  className={`inline-flex min-h-11 items-center justify-center gap-2 rounded-md px-4 py-2 text-sm font-semibold transition focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-clay/35 ${
                    selected
                      ? "bg-white text-ink shadow-soft"
                      : "text-ink/60 hover:bg-white/70 hover:text-ink"
                  }`}
                  href={tab.href}
                  id={`${tabGroupId}-${tab.id}-tab`}
                  key={tab.id}
                  role="tab"
                >
                  <Icon className="h-4 w-4" aria-hidden="true" />
                  {tab.label}
                </Link>
              );
            })}
          </div>
        </div>

        <div className="mt-6 grid gap-3 md:grid-cols-3">
          {tabs.map((tab) => (
            <div
              className={`rounded-md border px-4 py-3 ${
                activeTab === tab.id
                  ? "border-sage/25 bg-sky text-ink"
                  : "border-line bg-paper text-ink/60"
              }`}
              key={tab.id}
            >
              <p className="text-sm font-semibold">{tab.label}</p>
              <p className="mt-1 text-sm">{tab.description}</p>
            </div>
          ))}
        </div>
      </section>

      <section
        aria-labelledby={`${tabGroupId}-keys-tab`}
        hidden={activeTab !== "keys"}
        id={`${tabGroupId}-keys-panel`}
        role="tabpanel"
      >
        {activeTab === "keys" ? <ApiKeysPanel /> : null}
      </section>

      <section
        aria-labelledby={`${tabGroupId}-usage-tab`}
        hidden={activeTab !== "usage"}
        id={`${tabGroupId}-usage-panel`}
        role="tabpanel"
      >
        {activeTab === "usage" ? <UsagePanel /> : null}
      </section>

      <section
        aria-labelledby={`${tabGroupId}-billing-tab`}
        hidden={activeTab !== "billing"}
        id={`${tabGroupId}-billing-panel`}
        role="tabpanel"
      >
        {activeTab === "billing" ? <BillingPanel /> : null}
      </section>
    </div>
  );
}
