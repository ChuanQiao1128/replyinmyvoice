export type ShellIconName =
  | "pen"
  | "history"
  | "key"
  | "chart"
  | "plug"
  | "card"
  | "user"
  | "docs"
  | "menu"
  | "close"
  | "external"
  | "chevron"
  | "sparkle"
  | "shield";

export type ShellNavItem = {
  label: string;
  href: string;
  icon: ShellIconName;
};

export type ShellNavGroup = {
  id: string;
  label: string;
  items: ShellNavItem[];
};

/**
 * The single nav model consumed by both the desktop sidebar and the mobile
 * drawer. Every group is always visible — developer features are shown to
 * everyone and the pages themselves upsell Pro/API when the account lacks
 * API access.
 */
export const SHELL_NAV: ShellNavGroup[] = [
  {
    id: "create",
    label: "Create",
    items: [
      { label: "Rewrite", href: "/app", icon: "pen" },
      { label: "History", href: "/app/history", icon: "history" },
    ],
  },
  {
    id: "developers",
    label: "Developers",
    items: [
      { label: "API keys", href: "/app/keys", icon: "key" },
      { label: "Usage", href: "/app/usage", icon: "chart" },
      { label: "Connect", href: "/app/connect", icon: "plug" },
    ],
  },
  {
    id: "account",
    label: "Account",
    items: [
      // Billing + receipts + support live inside the Account page for V1;
      // FE-S6 splits them into a dedicated /app/billing page.
      { label: "Account", href: "/app/account", icon: "user" },
    ],
  },
];

export type ShellQuota = {
  remaining: number;
  quota: number;
  /** "web + API" for Pro/API tiers, "web" otherwise — makes the shared pool honest. */
  scopeLabel: string;
};

/** Serializable account shape passed from the server layout into the client shell. */
export type ShellAccount = {
  email: string | null;
  planLabel: string;
  /** Pro/API subscription active → the Developers group shows by default. */
  isDeveloperTier: boolean;
  isAdmin: boolean;
  quota: ShellQuota;
  rewriteHistoryUserKey?: string;
};

export function isNavItemActive(pathname: string, href: string): boolean {
  if (href === "/app") {
    return pathname === "/app";
  }
  return pathname === href || pathname.startsWith(`${href}/`);
}

const developerTierStatuses = new Set(["active", "trialing", "testing"]);

/** Pro/API subscription → API access; everyone else sees the upsell pages. */
export function isDeveloperTierStatus(subscriptionStatus: string): boolean {
  return developerTierStatuses.has(subscriptionStatus.trim().toLowerCase());
}

/** Human plan label — free accounts read "Free", never "Inactive". */
export function planLabelForStatus(subscriptionStatus: string): string {
  const normalized = subscriptionStatus.trim().toLowerCase();
  if (developerTierStatuses.has(normalized)) {
    return "Pro/API";
  }
  if (normalized === "pastdue" || normalized === "past_due") {
    return "Payment issue";
  }
  return "Free";
}
