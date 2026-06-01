"use client";

import {
  AlertCircle,
  CheckCircle2,
  ExternalLink,
  LifeBuoy,
  Loader2,
  LogOut,
  ReceiptText,
  Send,
  Trash2,
  UserRound,
} from "lucide-react";
import React, { type FormEvent, useEffect, useMemo, useState } from "react";

import type {
  AzureAccountPayment,
  AzureAccountSummary,
  AzureBillingSupportRequest,
} from "../../lib/azure-api";
import { Button, LinkButton } from "../ui/button";
import { Card } from "../ui/card";
import { Input } from "../ui/input";
import { Textarea } from "../ui/textarea";

type AccountState =
  | { status: "loading" }
  | {
      status: "ready";
      account: AzureAccountSummary;
      payments: AzureAccountPayment[];
      supportRequests: AzureBillingSupportRequest[];
    }
  | { status: "error"; message: string };

type SupportRequestType = "refund" | "billing-question";

function formatStatus(status: string) {
  const normalized = status.trim();
  if (!normalized) {
    return "Unknown";
  }

  return normalized
    .replace(/[-_]+/g, " ")
    .replace(/\b\w/g, (match) => match.toUpperCase());
}

function formatDate(value: string | null) {
  if (!value) {
    return "Not set";
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return "Not set";
  }

  return new Intl.DateTimeFormat(undefined, {
    day: "numeric",
    month: "short",
    year: "numeric",
  }).format(date);
}

function formatDateTime(value: string | null) {
  if (!value) {
    return "Not set";
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return "Not set";
  }

  return new Intl.DateTimeFormat(undefined, {
    day: "numeric",
    hour: "numeric",
    minute: "2-digit",
    month: "short",
    year: "numeric",
  }).format(date);
}

function paymentOptionLabel(payment: AzureAccountPayment) {
  const intent = payment.paymentIntentId ?? "No payment intent";
  return `${intent} · ${formatMoney(payment.amount, payment.currency)} · ${formatDate(payment.date)}`;
}

function requestTypeLabel(type: SupportRequestType) {
  return type === "refund" ? "Refund request" : "Billing question";
}

function usageCopy(account: AzureAccountSummary) {
  const { usage } = account;
  const cadence =
    usage.scope === "paid" ? "this billing period" : "from your free allowance";
  return `${usage.remaining} of ${usage.quota} rewrites remaining ${cadence}`;
}

function formatMoney(amount: number | null, currency: string | null) {
  if (amount == null) {
    return "Not recorded";
  }

  const value = (amount / 100).toFixed(2);
  const normalizedCurrency = currency?.trim().toUpperCase();
  return normalizedCurrency ? `${normalizedCurrency} ${value}` : value;
}

function formatSku(sku: string | null) {
  const normalized = sku?.trim();
  if (!normalized) {
    return "Purchase";
  }

  const labels: Record<string, string> = {
    focus_pack: "Focus Pack",
    pro_api: "Pro / API",
    quick_pack: "Quick Pack",
    value_pack: "Value Pack",
  };
  const labelKey = normalized.toLowerCase();
  if (labels[labelKey]) {
    return labels[labelKey];
  }

  return normalized
    .replace(/[-_]+/g, " ")
    .replace(/\b\w/g, (match) => match.toUpperCase());
}

function safeReceiptUrl(value: string | null) {
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

async function readJsonError(response: Response) {
  const payload = (await response.json().catch(() => null)) as {
    detail?: string;
    error?: string;
    title?: string;
  } | null;
  return payload?.error ?? payload?.detail ?? payload?.title;
}

async function loadAccount() {
  const response = await fetch("/api/me", {
    cache: "no-store",
  });

  if (response.status === 401) {
    window.location.assign("/sign-in");
    return null;
  }

  if (!response.ok) {
    throw new Error(
      (await readJsonError(response)) ?? "Could not load account details.",
    );
  }

  return (await response.json()) as AzureAccountSummary;
}

async function loadPayments() {
  const response = await fetch("/api/me/payments", {
    cache: "no-store",
  });

  if (response.status === 401) {
    window.location.assign("/sign-in");
    return [];
  }

  if (!response.ok) {
    throw new Error((await readJsonError(response)) ?? "Could not load purchases.");
  }

  return (await response.json()) as AzureAccountPayment[];
}

async function loadSupportRequests() {
  const response = await fetch("/api/billing-support-requests", {
    cache: "no-store",
  });

  if (response.status === 401) {
    window.location.assign("/sign-in");
    return [];
  }

  if (!response.ok) {
    throw new Error(
      (await readJsonError(response)) ?? "Could not load billing support requests.",
    );
  }

  return (await response.json()) as AzureBillingSupportRequest[];
}

async function loadAccountBundle() {
  const account = await loadAccount();
  if (!account) {
    return null;
  }

  const [payments, supportRequests] = await Promise.all([
    loadPayments(),
    loadSupportRequests(),
  ]);

  return { account, payments, supportRequests };
}

export function PurchaseHistorySection({
  payments,
}: {
  payments: AzureAccountPayment[];
}) {
  return (
    <section
      aria-labelledby="purchase-history-title"
      className="rounded-lg border border-line bg-white/75 p-6 shadow-soft sm:p-8"
    >
      <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
        <div className="min-w-0 space-y-3">
          <span className="inline-flex items-center gap-2 font-mono text-[11px] font-semibold uppercase tracking-[0.16em] text-sage">
            <ReceiptText className="h-4 w-4" aria-hidden="true" />
            Receipts
          </span>
          <div>
            <h2 id="purchase-history-title" className="text-2xl sm:text-3xl">
              Receipts / Purchase history
            </h2>
            <p className="mt-2 max-w-2xl text-sm text-ink/65">
              View Stripe-hosted receipts for completed purchases.
            </p>
          </div>
        </div>
      </div>

      {payments.length > 0 ? (
        <div className="mt-6 overflow-x-auto">
          <table className="min-w-full text-left text-sm">
            <thead className="border-b border-line bg-paper-deep/40 text-xs uppercase text-ink/50">
              <tr>
                <th className="px-4 py-3 font-semibold">Pack</th>
                <th className="px-4 py-3 font-semibold">Date</th>
                <th className="px-4 py-3 font-semibold">Amount</th>
                <th className="px-4 py-3 font-semibold">Expiry</th>
                <th className="px-4 py-3 font-semibold">Remaining</th>
                <th className="px-4 py-3 font-semibold">Receipt</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-line">
              {payments.map((payment, index) => {
                const receiptUrl = safeReceiptUrl(payment.receiptUrl);
                return (
                  <tr
                    className="bg-white/45"
                    key={`${payment.date}-${payment.sku ?? "purchase"}-${index}`}
                  >
                    <td className="whitespace-nowrap px-4 py-4 font-semibold">
                      {formatSku(payment.sku)}
                    </td>
                    <td className="whitespace-nowrap px-4 py-4">
                      {formatDate(payment.date)}
                    </td>
                    <td className="whitespace-nowrap px-4 py-4">
                      {formatMoney(payment.amount, payment.currency)}
                    </td>
                    <td className="whitespace-nowrap px-4 py-4">
                      {formatDate(payment.expiry)}
                    </td>
                    <td className="whitespace-nowrap px-4 py-4">
                      {payment.remaining} remaining
                    </td>
                    <td className="whitespace-nowrap px-4 py-4">
                      {receiptUrl ? (
                        <a
                          className="inline-flex items-center gap-1.5 font-semibold text-clay underline-offset-4 hover:underline"
                          href={receiptUrl}
                          rel="noreferrer"
                          target="_blank"
                        >
                          View receipt
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
      ) : (
        <div className="mt-6 rounded-md border border-dashed border-line bg-paper px-4 py-6 text-sm text-ink/55">
          No purchases yet.
        </div>
      )}
    </section>
  );
}

export function AccountPanel() {
  const [accountState, setAccountState] = useState<AccountState>({
    status: "loading",
  });
  const [showDeleteDialog, setShowDeleteDialog] = useState(false);
  const [confirmation, setConfirmation] = useState("");
  const [deleteError, setDeleteError] = useState<string | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);
  const [isSigningOut, setIsSigningOut] = useState(false);
  const [supportType, setSupportType] = useState<SupportRequestType>("refund");
  const [supportPaymentIntentId, setSupportPaymentIntentId] = useState("");
  const [supportMessage, setSupportMessage] = useState("");
  const [supportError, setSupportError] = useState<string | null>(null);
  const [supportNotice, setSupportNotice] = useState<string | null>(null);
  const [isSubmittingSupport, setIsSubmittingSupport] = useState(false);

  useEffect(() => {
    let isCurrent = true;

    async function refreshAccount() {
      try {
        const bundle = await loadAccountBundle();
        if (isCurrent && bundle) {
          setAccountState({ ...bundle, status: "ready" });
        }
      } catch (error) {
        if (isCurrent) {
          setAccountState({
            message:
              error instanceof Error
                ? error.message
                : "Could not load account details.",
            status: "error",
          });
        }
      }
    }

    void refreshAccount();

    return () => {
      isCurrent = false;
    };
  }, []);

  const readyAccount =
    accountState.status === "ready" ? accountState.account : undefined;
  const payments = accountState.status === "ready" ? accountState.payments : [];
  const supportRequests =
    accountState.status === "ready" ? accountState.supportRequests : [];
  const paymentOptions = useMemo(
    () => payments.filter((payment) => payment.paymentIntentId),
    [payments],
  );
  const email = readyAccount?.email?.trim() || "No email on file";
  const canConfirmDelete = useMemo(() => {
    const value = confirmation.trim();
    return (
      value === "DELETE" ||
      (!!readyAccount?.email &&
        value.toLowerCase() === readyAccount.email.toLowerCase())
    );
  }, [readyAccount?.email, confirmation]);

  function closeDeleteDialog() {
    if (isDeleting) {
      return;
    }

    setShowDeleteDialog(false);
    setConfirmation("");
    setDeleteError(null);
  }

  function signOut() {
    setIsSigningOut(true);
    window.location.assign("/api/auth/logout");
  }

  async function submitSupportRequest(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (accountState.status !== "ready" || isSubmittingSupport) {
      return;
    }

    const message = supportMessage.trim();
    if (message.length < 10) {
      setSupportError("Add a short message with the billing details.");
      return;
    }

    setSupportError(null);
    setSupportNotice(null);
    setIsSubmittingSupport(true);

    try {
      const response = await fetch("/api/billing-support-requests", {
        body: JSON.stringify({
          message,
          relatedPaymentIntentId: supportPaymentIntentId || null,
          type: supportType,
        }),
        cache: "no-store",
        headers: {
          "Content-Type": "application/json",
        },
        method: "POST",
      });

      if (!response.ok) {
        throw new Error(
          (await readJsonError(response)) ?? "Could not send this request.",
        );
      }

      const created = (await response.json()) as AzureBillingSupportRequest;
      setAccountState({
        ...accountState,
        supportRequests: [created, ...accountState.supportRequests],
      });
      setSupportMessage("");
      setSupportNotice("Request received. We sent a confirmation to your email.");
    } catch (error) {
      setSupportError(
        error instanceof Error ? error.message : "Could not send this request.",
      );
    } finally {
      setIsSubmittingSupport(false);
    }
  }

  async function deleteAccount() {
    if (!canConfirmDelete || isDeleting) {
      return;
    }

    setDeleteError(null);
    setIsDeleting(true);

    try {
      const response = await fetch("/api/me", {
        cache: "no-store",
        method: "DELETE",
      });

      if (!response.ok) {
        throw new Error(
          (await readJsonError(response)) ?? "Could not delete this account.",
        );
      }

      const logoutResponse = await fetch("/api/auth/logout", {
        cache: "no-store",
        method: "POST",
      });

      if (!logoutResponse.ok && !logoutResponse.redirected) {
        window.location.assign("/api/auth/logout");
        return;
      }

      window.location.assign("/");
    } catch (error) {
      setDeleteError(
        error instanceof Error
          ? error.message
          : "Could not delete this account.",
      );
      setIsDeleting(false);
    }
  }

  if (accountState.status === "loading") {
    return (
      <Card className="p-8">
        <div className="flex items-center gap-3 text-ink/65">
          <Loader2 className="h-5 w-5 animate-spin" aria-hidden="true" />
          <p>Loading account details...</p>
        </div>
      </Card>
    );
  }

  if (accountState.status === "error") {
    return (
      <Card className="p-8">
        <div className="flex flex-col gap-5">
          <div className="flex items-start gap-3">
            <AlertCircle className="mt-0.5 h-5 w-5 text-rust" aria-hidden="true" />
            <div className="space-y-2">
              <h2 className="text-2xl">Account details are unavailable.</h2>
              <p className="max-w-2xl text-sm text-ink/65">
                {accountState.message}
              </p>
            </div>
          </div>
          <div className="flex flex-wrap gap-3">
            <Button
              onClick={() => {
                setAccountState({ status: "loading" });
                void loadAccount()
                  .then(async (loadedAccount) => {
                    if (loadedAccount) {
                      const [loadedPayments, loadedSupportRequests] =
                        await Promise.all([loadPayments(), loadSupportRequests()]);
                      setAccountState({
                        account: loadedAccount,
                        payments: loadedPayments,
                        status: "ready",
                        supportRequests: loadedSupportRequests,
                      });
                    }
                  })
                  .catch((error) => {
                    setAccountState({
                      message:
                        error instanceof Error
                          ? error.message
                          : "Could not load account details.",
                      status: "error",
                    });
                  });
              }}
              type="button"
            >
              Try again
            </Button>
            <LinkButton href="/app" variant="secondary">
              Back to workspace
            </LinkButton>
          </div>
        </div>
      </Card>
    );
  }

  const account = accountState.account;

  return (
    <div className="grid gap-5 lg:grid-cols-[minmax(0,1fr)_340px]">
      <div className="flex flex-col gap-5">
        <section className="rounded-lg border border-line bg-white/75 p-6 shadow-soft sm:p-8">
          <div className="flex flex-col gap-7">
            <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
              <div className="min-w-0 space-y-3">
                <span className="inline-flex items-center gap-2 font-mono text-[11px] font-semibold uppercase tracking-[0.16em] text-sage">
                  <UserRound className="h-4 w-4" aria-hidden="true" />
                  Account
                </span>
                <div>
                  <h1 className="break-words text-4xl sm:text-5xl">Your account</h1>
                  <p className="mt-3 max-w-2xl text-base text-ink/65">
                    Review your sign-in details and current rewrite allowance.
                  </p>
                </div>
              </div>
              <Button
                disabled={isSigningOut}
                onClick={signOut}
                type="button"
                variant="secondary"
              >
                <LogOut className="h-4 w-4" aria-hidden="true" />
                {isSigningOut ? "Signing out..." : "Sign out"}
              </Button>
            </div>

            <dl className="grid gap-3 sm:grid-cols-2">
              <div className="rounded-md border border-line bg-paper px-4 py-3">
                <dt className="font-mono text-[11px] font-semibold uppercase tracking-[0.14em] text-ink/45">
                  Email
                </dt>
                <dd className="mt-1 break-words text-sm font-semibold text-ink">
                  {email}
                </dd>
              </div>
              <div className="rounded-md border border-line bg-paper px-4 py-3">
                <dt className="font-mono text-[11px] font-semibold uppercase tracking-[0.14em] text-ink/45">
                  Subscription status
                </dt>
                <dd className="mt-1 text-sm font-semibold text-ink">
                  {formatStatus(account.subscriptionStatus)}
                </dd>
              </div>
              <div className="rounded-md border border-line bg-paper px-4 py-3">
                <dt className="font-mono text-[11px] font-semibold uppercase tracking-[0.14em] text-ink/45">
                  Remaining usage
                </dt>
                <dd className="mt-1 text-sm font-semibold text-sage">
                  {usageCopy(account)}
                </dd>
              </div>
              <div className="rounded-md border border-line bg-paper px-4 py-3">
                <dt className="font-mono text-[11px] font-semibold uppercase tracking-[0.14em] text-ink/45">
                  Period end
                </dt>
                <dd className="mt-1 text-sm font-semibold text-ink">
                  {formatDate(account.usage.periodEnd ?? account.currentPeriodEnd)}
                </dd>
              </div>
            </dl>

            <div className="rounded-md border border-sage/20 bg-sky px-4 py-3">
              <div className="flex items-start gap-3">
                <CheckCircle2 className="mt-0.5 h-5 w-5 text-sage" aria-hidden="true" />
                <p className="text-sm text-ink/70">
                  Successful rewrites count against your allowance. Validation or
                  service errors are not charged.
                </p>
              </div>
            </div>
          </div>
        </section>

        <section className="rounded-lg border border-line bg-white/80 p-6 shadow-soft sm:p-8">
          <div className="flex items-start gap-3">
            <LifeBuoy className="mt-0.5 h-5 w-5 text-clay" aria-hidden="true" />
            <div className="min-w-0">
              <h2 className="text-2xl">Billing support</h2>
              <p className="mt-2 max-w-2xl text-sm text-ink/65">
                Send a billing question or refund request for owner review.
              </p>
            </div>
          </div>

          <form className="mt-5 grid gap-4" onSubmit={submitSupportRequest}>
            <div className="grid gap-4 md:grid-cols-2">
              <label className="block text-sm font-semibold text-ink/70">
                Reason
                <select
                  className="mt-1 w-full rounded-md border border-line bg-white px-3 py-2 text-sm text-ink outline-none transition focus:border-clay focus:ring-2 focus:ring-clay/15"
                  onChange={(event) =>
                    setSupportType(event.target.value as SupportRequestType)
                  }
                  value={supportType}
                >
                  <option value="refund">Refund request</option>
                  <option value="billing-question">Billing question</option>
                </select>
              </label>

              <label className="block text-sm font-semibold text-ink/70">
                Purchase
                <select
                  className="mt-1 w-full rounded-md border border-line bg-white px-3 py-2 text-sm text-ink outline-none transition focus:border-clay focus:ring-2 focus:ring-clay/15"
                  onChange={(event) =>
                    setSupportPaymentIntentId(event.target.value)
                  }
                  value={supportPaymentIntentId}
                >
                  <option value="">No specific purchase</option>
                  {paymentOptions.map((payment) => (
                    <option
                      key={payment.paymentIntentId}
                      value={payment.paymentIntentId ?? ""}
                    >
                      {paymentOptionLabel(payment)}
                    </option>
                  ))}
                </select>
              </label>
            </div>

            <label className="block text-sm font-semibold text-ink/70">
              Message
              <Textarea
                className="mt-1 min-h-28"
                maxLength={2000}
                onChange={(event) => setSupportMessage(event.target.value)}
                placeholder="Tell us what happened and include any amount or date that matters."
                required
                value={supportMessage}
              />
            </label>

            {supportError ? (
              <p className="text-sm font-semibold text-rust">{supportError}</p>
            ) : null}
            {supportNotice ? (
              <p className="text-sm font-semibold text-sage">{supportNotice}</p>
            ) : null}

            <div className="flex justify-end">
              <Button disabled={isSubmittingSupport} type="submit">
                {isSubmittingSupport ? (
                  <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
                ) : (
                  <Send className="h-4 w-4" aria-hidden="true" />
                )}
                {isSubmittingSupport ? "Sending..." : "Send request"}
              </Button>
            </div>
          </form>

          <div className="mt-6 border-t border-line pt-5">
            <h3 className="text-sm font-semibold uppercase text-ink/45">
              Recent requests
            </h3>
            <div className="mt-3 grid gap-3">
              {supportRequests.map((request) => (
                <div
                  className="rounded-md border border-line bg-paper px-4 py-3"
                  key={request.id}
                >
                  <div className="flex flex-wrap items-center gap-2 text-xs font-semibold uppercase text-ink/45">
                    <span>{requestTypeLabel(request.type)}</span>
                    <span aria-hidden="true">·</span>
                    <span>{request.status}</span>
                    <span aria-hidden="true">·</span>
                    <span>{formatDateTime(request.createdAt)}</span>
                  </div>
                  {request.relatedPaymentIntentId ? (
                    <p className="mt-2 break-all font-mono text-xs text-ink/50">
                      {request.relatedPaymentIntentId}
                    </p>
                  ) : null}
                  <p className="mt-2 text-sm text-ink/70">{request.message}</p>
                </div>
              ))}
              {supportRequests.length === 0 ? (
                <p className="rounded-md border border-dashed border-line px-4 py-5 text-sm text-ink/55">
                  No billing support requests yet.
                </p>
              ) : null}
            </div>
          </div>
        </section>

        <PurchaseHistorySection payments={payments} />
      </div>

      <aside className="flex flex-col gap-5">
        <section className="rounded-lg border border-line bg-white/75 p-5 shadow-crisp">
          <h2 className="text-2xl">Workspace</h2>
          <p className="mt-2 text-sm text-ink/65">
            Return to the reply editor with your current allowance.
          </p>
          <LinkButton className="mt-5 w-full" href="/app">
            Open workspace
          </LinkButton>
        </section>

        <section className="rounded-lg border border-rust/25 bg-white/75 p-5 shadow-crisp">
          <h2 className="text-2xl">Delete account</h2>
          <p className="mt-2 text-sm text-ink/65">
            This removes your profile and cancels active account access. This
            action cannot be undone.
          </p>
          <Button
            className="mt-5 w-full border-rust/30 text-rust hover:bg-rust/5"
            onClick={() => setShowDeleteDialog(true)}
            type="button"
            variant="secondary"
          >
            <Trash2 className="h-4 w-4" aria-hidden="true" />
            Delete account
          </Button>
        </section>
      </aside>

      {showDeleteDialog ? (
        <div
          aria-labelledby="delete-account-title"
          aria-modal="true"
          className="fixed inset-0 z-[80] flex items-center justify-center bg-ink/35 p-4"
          role="dialog"
        >
          <div className="w-full max-w-lg rounded-lg border border-line bg-paper p-6 shadow-soft">
            <div className="flex items-start gap-3">
              <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-md bg-rust/10 text-rust">
                <Trash2 className="h-5 w-5" aria-hidden="true" />
              </div>
              <div className="min-w-0">
                <h2 id="delete-account-title" className="text-2xl">
                  Delete this account?
                </h2>
                <p className="mt-2 text-sm text-ink/65">
                  Type <strong>DELETE</strong> or your email address to confirm.
                </p>
              </div>
            </div>

            <label
              className="mt-5 block text-sm font-semibold text-ink"
              htmlFor="delete-account-confirmation"
            >
              Confirmation phrase
            </label>
            <Input
              autoComplete="off"
              className="mt-2"
              id="delete-account-confirmation"
              onChange={(event) => setConfirmation(event.target.value)}
              value={confirmation}
            />

            {deleteError ? (
              <p className="mt-3 text-sm font-semibold text-rust">{deleteError}</p>
            ) : null}

            <div className="mt-6 flex flex-col-reverse gap-3 sm:flex-row sm:justify-end">
              <Button
                disabled={isDeleting}
                onClick={closeDeleteDialog}
                type="button"
                variant="secondary"
              >
                Cancel
              </Button>
              <Button
                className="bg-rust text-white hover:bg-rust/90"
                disabled={!canConfirmDelete || isDeleting}
                onClick={() => void deleteAccount()}
                type="button"
              >
                {isDeleting ? (
                  <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
                ) : (
                  <Trash2 className="h-4 w-4" aria-hidden="true" />
                )}
                {isDeleting ? "Deleting..." : "Delete account"}
              </Button>
            </div>
          </div>
        </div>
      ) : null}
    </div>
  );
}
