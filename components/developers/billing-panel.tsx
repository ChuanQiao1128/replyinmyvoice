"use client";

import {
  AlertCircle,
  CalendarClock,
  CreditCard,
  Download,
  ExternalLink,
  FileText,
  Loader2,
  ReceiptText,
  RefreshCw,
} from "lucide-react";
import React, { useCallback, useEffect, useMemo, useState } from "react";

import type {
  AzureAccountSummary,
  AzureBillingHistoryItem,
} from "../../lib/azure-api";
import { Button } from "../ui/button";
import { Card } from "../ui/card";

type BillingState =
  | { status: "loading" }
  | {
      account: AzureAccountSummary;
      history: AzureBillingHistoryItem[];
      status: "ready";
    }
  | { message: string; status: "error" };

function formatStatus(status: string | null) {
  const normalized = status?.trim();
  if (!normalized) {
    return "Not recorded";
  }

  return normalized
    .replace(/[-_]+/g, " ")
    .replace(/\b\w/g, (match) => match.toUpperCase());
}

function formatDate(value: string | null | undefined) {
  if (!value) {
    return "Not recorded";
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return "Not recorded";
  }

  return new Intl.DateTimeFormat(undefined, {
    day: "numeric",
    month: "short",
    year: "numeric",
  }).format(date);
}

function formatMoney(
  amount: number | null,
  currency: string | null,
  type?: string,
) {
  if (amount == null) {
    return "Not recorded";
  }

  const signedAmount = type === "refund" && amount > 0 ? -amount : amount;
  const sign = signedAmount < 0 ? "-" : "";
  const normalizedCurrency = currency?.trim().toUpperCase();
  const value = (Math.abs(signedAmount) / 100).toFixed(2);
  return normalizedCurrency ? `${sign}${normalizedCurrency} ${value}` : `${sign}${value}`;
}

function formatRecordType(type: string) {
  const normalized = type.trim().toLowerCase();
  const labels: Record<string, string> = {
    dispute: "Dispute",
    pack: "Pack",
    refund: "Refund",
    subscription: "Subscription",
  };

  if (labels[normalized]) {
    return labels[normalized];
  }

  return formatStatus(type);
}

function planLabel(account: AzureAccountSummary) {
  const status = account.subscriptionStatus.trim().toLowerCase();
  if (account.usage.scope === "paid" || status === "active" || status === "trialing") {
    return "Pro/API";
  }

  return "Free workspace";
}

function periodLabel(account: AzureAccountSummary) {
  const status = account.subscriptionStatus.trim().toLowerCase();
  if (status === "active" || status === "trialing") {
    return "Renews / period ends";
  }

  return "Period end";
}

function periodEnd(account: AzureAccountSummary) {
  return account.currentPeriodEnd ?? account.usage.periodEnd;
}

function usageSummary(account: AzureAccountSummary) {
  const cadence =
    account.usage.scope === "paid" ? "this billing period" : "from your free allowance";
  return `${account.usage.remaining} of ${account.usage.quota} rewrites remaining ${cadence}`;
}

function safeExternalUrl(value: string | null | undefined) {
  if (!value) {
    return null;
  }

  try {
    const url = new URL(value);
    return url.protocol === "https:" ? url.toString() : null;
  } catch {
    return null;
  }
}

function statusClass(status: string | null) {
  const normalized = status?.trim().toLowerCase();
  if (!normalized) {
    return "border-line bg-paper text-ink/55";
  }

  if (["paid", "succeeded", "active"].includes(normalized)) {
    return "border-sage/25 bg-sky text-sage";
  }

  if (["open", "pending", "processing", "trialing"].includes(normalized)) {
    return "border-clay/25 bg-clay/10 text-clay";
  }

  return "border-rust/25 bg-rust/10 text-rust";
}

async function readJsonError(response: Response) {
  const payload = (await response.json().catch(() => null)) as {
    detail?: string;
    error?: string;
    title?: string;
  } | null;
  return payload?.error ?? payload?.detail ?? payload?.title;
}

async function fetchJson<T>(path: string) {
  const response = await fetch(path, {
    cache: "no-store",
  });

  if (response.status === 401) {
    window.location.assign("/sign-in");
    return null;
  }

  if (!response.ok) {
    throw new Error((await readJsonError(response)) ?? "Could not load billing.");
  }

  return (await response.json()) as T;
}

async function loadBilling() {
  const [account, history] = await Promise.all([
    fetchJson<AzureAccountSummary>("/api/me"),
    fetchJson<AzureBillingHistoryItem[]>("/api/me/billing/history"),
  ]);

  if (!account || !history) {
    return null;
  }

  return { account, history };
}

async function openPaymentPortal() {
  const response = await fetch("/api/stripe/portal", {
    method: "POST",
  });
  const payload = (await response.json().catch(() => null)) as {
    error?: string;
    url?: string;
  } | null;

  if (!response.ok || !payload?.url) {
    throw new Error(payload?.error ?? "Could not open payment management.");
  }

  window.location.href = payload.url;
}

function receiptLink(item: AzureBillingHistoryItem) {
  const receiptUrl = safeExternalUrl(item.receiptUrl);
  if (receiptUrl) {
    return {
      href: receiptUrl,
      label: "View receipt",
    };
  }

  const invoiceUrl = safeExternalUrl(item.hostedInvoiceUrl);
  if (invoiceUrl) {
    return {
      href: invoiceUrl,
      label: "View invoice",
    };
  }

  return null;
}

export function BillingHistoryTable({
  history,
}: {
  history: AzureBillingHistoryItem[];
}) {
  return (
    <section
      aria-labelledby="developer-billing-history-title"
      className="rounded-lg border border-line bg-white/80 p-6 shadow-soft sm:p-8"
    >
      <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
        <div className="min-w-0">
          <span className="inline-flex items-center gap-2 font-mono text-[11px] font-semibold uppercase tracking-[0.16em] text-sage">
            <ReceiptText className="h-4 w-4" aria-hidden="true" />
            Receipts
          </span>
          <h2 id="developer-billing-history-title" className="mt-3 text-2xl">
            Unified billing history
          </h2>
          <p className="mt-2 max-w-2xl text-sm text-ink/65">
            Packs, subscription invoices, and refund records from your account.
          </p>
        </div>
        <div className="flex w-full flex-col gap-2 sm:w-auto sm:items-end">
          <a
            className="inline-flex min-h-11 w-full items-center justify-center gap-2 rounded-lg border border-line bg-white px-4 py-2 text-sm font-semibold text-ink transition hover:bg-paper focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-clay/35 focus-visible:ring-offset-2 focus-visible:ring-offset-paper sm:w-auto"
            download
            href="/api/me/billing/export"
          >
            <Download className="h-4 w-4" aria-hidden="true" />
            Export CSV
          </a>
          {history.length > 0 ? (
            <span className="rounded-full border border-line bg-paper px-3 py-1 text-xs font-semibold text-ink/55">
              {history.length} {history.length === 1 ? "record" : "records"}
            </span>
          ) : null}
        </div>
      </div>

      {history.length === 0 ? (
        <div className="mt-6 rounded-md border border-dashed border-line bg-paper px-4 py-8 text-sm text-ink/55">
          No billing records yet.
        </div>
      ) : (
        <div className="mt-6 overflow-x-auto">
          <table className="min-w-[820px] text-left text-sm">
            <thead className="border-b border-line bg-paper-deep/40 text-xs uppercase text-ink/50">
              <tr>
                <th className="px-4 py-3 font-semibold">Date</th>
                <th className="px-4 py-3 font-semibold">Type</th>
                <th className="px-4 py-3 font-semibold">Description</th>
                <th className="px-4 py-3 font-semibold">Amount</th>
                <th className="px-4 py-3 font-semibold">Status</th>
                <th className="px-4 py-3 font-semibold">Receipt / invoice</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-line">
              {history.map((item, index) => {
                const link = receiptLink(item);
                const description = item.description?.trim() || "Billing record";

                return (
                  <tr
                    className="bg-white/45"
                    key={`${item.date}-${item.type}-${index}`}
                  >
                    <td className="whitespace-nowrap px-4 py-4">
                      {formatDate(item.date)}
                    </td>
                    <td className="whitespace-nowrap px-4 py-4 font-semibold">
                      {formatRecordType(item.type)}
                    </td>
                    <td className="max-w-[320px] px-4 py-4 text-ink/75">
                      <span className="line-clamp-2">{description}</span>
                    </td>
                    <td className="whitespace-nowrap px-4 py-4">
                      {formatMoney(item.amount, item.currency, item.type)}
                    </td>
                    <td className="whitespace-nowrap px-4 py-4">
                      <span
                        className={`inline-flex rounded-full border px-2.5 py-1 text-xs font-semibold ${statusClass(
                          item.status,
                        )}`}
                      >
                        {formatStatus(item.status)}
                      </span>
                    </td>
                    <td className="whitespace-nowrap px-4 py-4">
                      {link ? (
                        <a
                          className="inline-flex items-center gap-1.5 font-semibold text-clay underline-offset-4 hover:underline"
                          href={link.href}
                          rel="noreferrer"
                          target="_blank"
                        >
                          {link.label}
                          <ExternalLink className="h-3.5 w-3.5" aria-hidden="true" />
                        </a>
                      ) : (
                        <span className="text-ink/45">Not available</span>
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}

function PlanSummary({
  account,
  error,
  loading,
  onManagePayment,
}: {
  account: AzureAccountSummary;
  error: string | null;
  loading: boolean;
  onManagePayment: () => void;
}) {
  const stats = useMemo(
    () => [
      {
        icon: CreditCard,
        label: "Current plan",
        value: planLabel(account),
      },
      {
        icon: CalendarClock,
        label: periodLabel(account),
        value: formatDate(periodEnd(account)),
      },
      {
        icon: FileText,
        label: "Usage",
        value: usageSummary(account),
      },
    ],
    [account],
  );

  return (
    <section
      aria-labelledby="developer-billing-plan-title"
      className="rounded-lg border border-line bg-white/80 p-6 shadow-soft sm:p-8"
    >
      <div className="flex flex-col gap-5 lg:flex-row lg:items-start lg:justify-between">
        <div className="min-w-0">
          <span className="inline-flex items-center gap-2 font-mono text-[11px] font-semibold uppercase tracking-[0.16em] text-sage">
            <CreditCard className="h-4 w-4" aria-hidden="true" />
            Billing
          </span>
          <h2 id="developer-billing-plan-title" className="mt-3 text-3xl">
            Plan and payments
          </h2>
          <p className="mt-2 max-w-2xl text-sm text-ink/65">
            Review your current plan, period end, and Stripe-hosted payment
            settings.
          </p>
        </div>
        <Button
          className="w-full sm:w-auto"
          disabled={loading}
          onClick={onManagePayment}
          type="button"
          variant="secondary"
        >
          {loading ? (
            <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
          ) : (
            <CreditCard className="h-4 w-4" aria-hidden="true" />
          )}
          Manage payment method
        </Button>
      </div>

      <div className="mt-6 grid gap-4 md:grid-cols-3">
        {stats.map((stat) => {
          const Icon = stat.icon;

          return (
            <Card className="p-5" key={stat.label}>
              <div className="flex items-start gap-3">
                <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-md bg-sky text-sage">
                  <Icon className="h-4 w-4" aria-hidden="true" />
                </div>
                <div className="min-w-0">
                  <p className="font-mono text-[11px] font-semibold uppercase tracking-[0.16em] text-ink/45">
                    {stat.label}
                  </p>
                  <p className="mt-2 break-words text-lg font-semibold text-ink">
                    {stat.value}
                  </p>
                </div>
              </div>
            </Card>
          );
        })}
      </div>

      <div className="mt-4 rounded-md border border-line bg-paper px-4 py-3 text-sm text-ink/65">
        Status:{" "}
        <span className="font-semibold text-ink">
          {formatStatus(account.subscriptionStatus)}
        </span>
      </div>

      {error ? (
        <p className="mt-4 text-sm font-medium text-rust" role="alert">
          {error}
        </p>
      ) : null}
    </section>
  );
}

export function BillingPanel() {
  const [billingState, setBillingState] = useState<BillingState>({
    status: "loading",
  });
  const [portalLoading, setPortalLoading] = useState(false);
  const [portalError, setPortalError] = useState<string | null>(null);

  const refreshBilling = useCallback(async () => {
    setBillingState({ status: "loading" });

    try {
      const billing = await loadBilling();
      if (billing) {
        setBillingState({ ...billing, status: "ready" });
      }
    } catch (error) {
      setBillingState({
        message: error instanceof Error ? error.message : "Could not load billing.",
        status: "error",
      });
    }
  }, []);

  useEffect(() => {
    let isCurrent = true;

    async function loadInitialBilling() {
      try {
        const billing = await loadBilling();
        if (isCurrent && billing) {
          setBillingState({ ...billing, status: "ready" });
        }
      } catch (error) {
        if (isCurrent) {
          setBillingState({
            message:
              error instanceof Error ? error.message : "Could not load billing.",
            status: "error",
          });
        }
      }
    }

    void loadInitialBilling();

    return () => {
      isCurrent = false;
    };
  }, []);

  async function handleManagePayment() {
    setPortalLoading(true);
    setPortalError(null);

    try {
      await openPaymentPortal();
    } catch (error) {
      setPortalError(
        error instanceof Error
          ? error.message
          : "Could not open payment management.",
      );
      setPortalLoading(false);
    }
  }

  const readyState = billingState.status === "ready" ? billingState : null;

  return (
    <div className="space-y-5">
      {billingState.status === "loading" ? (
        <div className="flex items-center gap-3 rounded-lg border border-line bg-white/75 px-4 py-6 text-sm text-ink/65 shadow-soft">
          <Loader2 className="h-5 w-5 animate-spin" aria-hidden="true" />
          Loading billing...
        </div>
      ) : null}

      {billingState.status === "error" ? (
        <div className="rounded-lg border border-rust/25 bg-rust/5 p-5 shadow-soft">
          <div className="flex items-start gap-3">
            <AlertCircle
              className="mt-0.5 h-5 w-5 text-rust"
              aria-hidden="true"
            />
            <div className="min-w-0">
              <p className="font-semibold text-rust">Could not load billing.</p>
              <p className="mt-1 text-sm text-ink/65">{billingState.message}</p>
              <Button
                className="mt-4"
                onClick={() => void refreshBilling()}
                type="button"
                variant="secondary"
              >
                <RefreshCw className="h-4 w-4" aria-hidden="true" />
                Try again
              </Button>
            </div>
          </div>
        </div>
      ) : null}

      {readyState ? (
        <>
          <PlanSummary
            account={readyState.account}
            error={portalError}
            loading={portalLoading}
            onManagePayment={() => void handleManagePayment()}
          />
          <BillingHistoryTable history={readyState.history} />
        </>
      ) : null}
    </div>
  );
}
