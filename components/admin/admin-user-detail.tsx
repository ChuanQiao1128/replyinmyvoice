"use client";

import {
  AlertCircle,
  ArrowLeft,
  CheckCircle2,
  CreditCard,
  Loader2,
  ShieldOff,
  ShieldCheck,
} from "lucide-react";
import Link from "next/link";
import { FormEvent, useCallback, useEffect, useMemo, useState } from "react";

import type {
  AdminCredit,
  AdminPayment,
  AdminUserDetailResponse,
  AdminUsagePeriod,
} from "../../lib/admin-types";
import { Button } from "../ui/button";
import { Card } from "../ui/card";
import { Input } from "../ui/input";
import { Textarea } from "../ui/textarea";

type DetailState =
  | { status: "loading" }
  | { status: "ready"; detail: AdminUserDetailResponse }
  | { status: "error"; message: string };

type ActionOutcome = {
  title: string;
  payload: unknown;
};

async function readJsonError(response: Response) {
  const payload = (await response.json().catch(() => null)) as {
    error?: string;
    detail?: string;
    title?: string;
  } | null;

  return payload?.error ?? payload?.detail ?? payload?.title;
}

async function fetchDetail(userId: string) {
  const response = await fetch(`/api/admin/users/${userId}`, {
    cache: "no-store",
  });

  if (response.status === 401) {
    window.location.assign("/sign-in");
    throw new Error("Authentication required.");
  }

  if (!response.ok) {
    throw new Error((await readJsonError(response)) ?? "Could not load user.");
  }

  return (await response.json()) as AdminUserDetailResponse;
}

async function postAction(path: string, body: unknown) {
  const response = await fetch(path, {
    body: JSON.stringify(body),
    cache: "no-store",
    headers: {
      "Content-Type": "application/json",
    },
    method: "POST",
  });

  const payload = await response.json().catch(() => null);
  if (!response.ok) {
    const message =
      payload && typeof payload === "object"
        ? ((payload as { error?: string; detail?: string; title?: string }).error ??
          (payload as { error?: string; detail?: string; title?: string }).detail ??
          (payload as { error?: string; detail?: string; title?: string }).title)
        : null;
    throw new Error(message ?? "Admin action failed.");
  }

  return payload;
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
    dateStyle: "medium",
    timeStyle: "short",
  }).format(date);
}

function formatUsd(value: number) {
  return new Intl.NumberFormat(undefined, {
    currency: "USD",
    maximumFractionDigits: 2,
    minimumFractionDigits: 2,
    style: "currency",
  }).format(value);
}

function formatStatus(value: string) {
  return value
    .trim()
    .replace(/[-_]+/g, " ")
    .replace(/\b\w/g, (match) => match.toUpperCase());
}

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || value === "" ? "—" : value;
}

function paymentLabel(payment: AdminPayment) {
  const intent = payment.paymentIntentId ?? "No payment intent";
  const amount =
    payment.amountTotal !== null && payment.currency
      ? `${payment.amountTotal} ${payment.currency.toUpperCase()}`
      : "amount not stored";
  return `${intent} · ${amount}`;
}

function SummaryItem({
  label,
  value,
}: {
  label: string;
  value: string | number | null | undefined;
}) {
  return (
    <div>
      <p className="text-xs font-semibold uppercase text-ink/45">{label}</p>
      <p className="mt-1 break-words text-sm font-medium text-ink">
        {valueOrDash(value)}
      </p>
    </div>
  );
}

function UsageTable({ usage }: { usage: AdminUsagePeriod[] }) {
  return (
    <Card className="overflow-hidden">
      <div className="border-b border-line px-5 py-4">
        <h2 className="text-xl font-semibold tracking-normal">Usage periods</h2>
      </div>
      <div className="overflow-x-auto">
        <table className="min-w-full text-left text-sm">
          <thead className="border-b border-line bg-paper-deep/40 text-xs uppercase text-ink/50">
            <tr>
              <th className="px-5 py-3 font-semibold">Period</th>
              <th className="px-5 py-3 font-semibold">Quota</th>
              <th className="px-5 py-3 font-semibold">Used</th>
              <th className="px-5 py-3 font-semibold">Reserved</th>
              <th className="px-5 py-3 font-semibold">Ends</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-line">
            {usage.map((period) => (
              <tr key={period.id} className="bg-white/45">
                <td className="px-5 py-4">{period.periodKey}</td>
                <td className="px-5 py-4">{period.quota}</td>
                <td className="px-5 py-4">{period.used}</td>
                <td className="px-5 py-4">{period.reserved}</td>
                <td className="px-5 py-4">{formatDateTime(period.periodEnd)}</td>
              </tr>
            ))}
            {usage.length === 0 ? (
              <tr>
                <td className="px-5 py-8 text-center text-ink/55" colSpan={5}>
                  No usage periods found.
                </td>
              </tr>
            ) : null}
          </tbody>
        </table>
      </div>
    </Card>
  );
}

function CreditTable({ credits }: { credits: AdminCredit[] }) {
  return (
    <Card className="overflow-hidden">
      <div className="border-b border-line px-5 py-4">
        <h2 className="text-xl font-semibold tracking-normal">Credit ledger</h2>
      </div>
      <div className="overflow-x-auto">
        <table className="min-w-full text-left text-sm">
          <thead className="border-b border-line bg-paper-deep/40 text-xs uppercase text-ink/50">
            <tr>
              <th className="px-5 py-3 font-semibold">Source</th>
              <th className="px-5 py-3 font-semibold">Granted</th>
              <th className="px-5 py-3 font-semibold">Consumed</th>
              <th className="px-5 py-3 font-semibold">Remaining</th>
              <th className="px-5 py-3 font-semibold">Payment intent</th>
              <th className="px-5 py-3 font-semibold">Expires</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-line">
            {credits.map((credit) => (
              <tr key={credit.id} className="bg-white/45">
                <td className="px-5 py-4">{credit.source}</td>
                <td className="px-5 py-4">{credit.amountGranted}</td>
                <td className="px-5 py-4">{credit.amountConsumed}</td>
                <td className="px-5 py-4">{credit.remaining}</td>
                <td className="px-5 py-4 font-mono text-xs">
                  {valueOrDash(credit.paymentIntentId)}
                </td>
                <td className="px-5 py-4">{formatDateTime(credit.expiresAt)}</td>
              </tr>
            ))}
            {credits.length === 0 ? (
              <tr>
                <td className="px-5 py-8 text-center text-ink/55" colSpan={6}>
                  No credit ledger rows found.
                </td>
              </tr>
            ) : null}
          </tbody>
        </table>
      </div>
    </Card>
  );
}

function PaymentTable({ payments }: { payments: AdminPayment[] }) {
  return (
    <Card className="overflow-hidden">
      <div className="border-b border-line px-5 py-4">
        <h2 className="text-xl font-semibold tracking-normal">Payments</h2>
      </div>
      <div className="overflow-x-auto">
        <table className="min-w-full text-left text-sm">
          <thead className="border-b border-line bg-paper-deep/40 text-xs uppercase text-ink/50">
            <tr>
              <th className="px-5 py-3 font-semibold">Payment intent</th>
              <th className="px-5 py-3 font-semibold">SKU</th>
              <th className="px-5 py-3 font-semibold">Amount</th>
              <th className="px-5 py-3 font-semibold">Credits</th>
              <th className="px-5 py-3 font-semibold">Receipt</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-line">
            {payments.map((payment) => (
              <tr key={payment.creditId} className="bg-white/45">
                <td className="px-5 py-4 font-mono text-xs">
                  {valueOrDash(payment.paymentIntentId)}
                </td>
                <td className="px-5 py-4">{valueOrDash(payment.sku)}</td>
                <td className="px-5 py-4">
                  {payment.amountTotal ?? "—"}{" "}
                  {payment.currency ? payment.currency.toUpperCase() : ""}
                </td>
                <td className="px-5 py-4">
                  {payment.creditsRemaining} of {payment.creditsGranted}
                </td>
                <td className="px-5 py-4">
                  {payment.receiptUrl ? (
                    <a
                      className="font-semibold text-clay underline-offset-4 hover:underline"
                      href={payment.receiptUrl}
                      rel="noreferrer"
                      target="_blank"
                    >
                      Receipt
                    </a>
                  ) : (
                    "—"
                  )}
                </td>
              </tr>
            ))}
            {payments.length === 0 ? (
              <tr>
                <td className="px-5 py-8 text-center text-ink/55" colSpan={5}>
                  No payment rows found.
                </td>
              </tr>
            ) : null}
          </tbody>
        </table>
      </div>
    </Card>
  );
}

export function AdminUserDetail({ userId }: { userId: string }) {
  const [state, setState] = useState<DetailState>({ status: "loading" });
  const [creditAmount, setCreditAmount] = useState("5");
  const [creditReason, setCreditReason] = useState("");
  const [refundPaymentIntentId, setRefundPaymentIntentId] = useState("");
  const [refundAmount, setRefundAmount] = useState("");
  const [refundCurrency, setRefundCurrency] = useState("");
  const [refundReason, setRefundReason] = useState("");
  const [outcome, setOutcome] = useState<ActionOutcome | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [actionLoading, setActionLoading] = useState<string | null>(null);

  const refreshDetail = useCallback(async () => {
    setState({ status: "loading" });
    try {
      const detail = await fetchDetail(userId);
      setState({ detail, status: "ready" });
    } catch (error) {
      setState({
        message: error instanceof Error ? error.message : "Could not load user.",
        status: "error",
      });
    }
  }, [userId]);

  useEffect(() => {
    void refreshDetail();
  }, [refreshDetail]);

  const detail = state.status === "ready" ? state.detail : null;
  const selectablePayments = useMemo(
    () => detail?.payments.filter((payment) => payment.paymentIntentId) ?? [],
    [detail],
  );

  useEffect(() => {
    if (!refundPaymentIntentId && selectablePayments[0]?.paymentIntentId) {
      setRefundPaymentIntentId(selectablePayments[0].paymentIntentId);
    }
  }, [refundPaymentIntentId, selectablePayments]);

  useEffect(() => {
    const selected = selectablePayments.find(
      (payment) => payment.paymentIntentId === refundPaymentIntentId,
    );
    if (selected?.amountTotal !== null && selected?.amountTotal !== undefined) {
      setRefundAmount(String(selected.amountTotal));
    }
    if (selected?.currency) {
      setRefundCurrency(selected.currency);
    }
  }, [refundPaymentIntentId, selectablePayments]);

  async function runAction(key: string, title: string, action: () => Promise<unknown>) {
    setActionError(null);
    setOutcome(null);
    setActionLoading(key);
    try {
      const payload = await action();
      setOutcome({ payload, title });
      await refreshDetail();
    } catch (error) {
      setActionError(
        error instanceof Error ? error.message : "The admin action failed.",
      );
    } finally {
      setActionLoading(null);
    }
  }

  async function submitCredits(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const amount = Number.parseInt(creditAmount, 10);
    if (!Number.isInteger(amount) || amount <= 0) {
      setActionError("Credit amount must be greater than zero.");
      return;
    }

    if (!window.confirm(`Grant ${amount} credits to this user?`)) {
      return;
    }

    await runAction("credits", "Audit outcome: credit grant recorded", () =>
      postAction(`/api/admin/users/${userId}/credits`, {
        amount,
        reason: creditReason,
      }),
    );
  }

  async function submitRefund(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const amount = Number.parseInt(refundAmount, 10);
    if (!refundPaymentIntentId.trim()) {
      setActionError("Select a payment intent before issuing a refund.");
      return;
    }
    if (!Number.isInteger(amount) || amount <= 0) {
      setActionError("Refund amount must be greater than zero.");
      return;
    }

    if (!window.confirm(`Issue a refund for ${amount} ${refundCurrency || ""}?`)) {
      return;
    }

    await runAction("refund", "Audit outcome: refund recorded", () =>
      postAction(`/api/admin/users/${userId}/refund`, {
        amount,
        currency: refundCurrency,
        paymentIntentId: refundPaymentIntentId,
        reason: refundReason,
      }),
    );
  }

  async function submitSuspension(suspended: boolean) {
    const label = suspended ? "suspend" : "unsuspend";
    if (!window.confirm(`Confirm ${label} for this user?`)) {
      return;
    }

    await runAction(
      suspended ? "suspend" : "unsuspend",
      suspended
        ? "Audit outcome: suspension recorded"
        : "Audit outcome: unsuspension recorded",
      () =>
        postAction(`/api/admin/users/${userId}/suspension`, {
          suspended,
        }),
    );
  }

  return (
    <main className="wrap py-10">
      <Link
        className="mb-6 inline-flex items-center gap-2 text-sm font-semibold text-ink/65 hover:text-ink"
        href="/admin"
      >
        <ArrowLeft className="h-4 w-4" aria-hidden="true" />
        Back to admin
      </Link>

      {state.status === "loading" ? (
        <Card className="p-8">
          <div className="flex items-center gap-3 text-ink/65">
            <Loader2 className="h-5 w-5 animate-spin" aria-hidden="true" />
            <p>Loading user detail...</p>
          </div>
        </Card>
      ) : null}

      {state.status === "error" ? (
        <Card className="p-8">
          <div className="flex items-start gap-3">
            <AlertCircle className="mt-0.5 h-5 w-5 text-rust" aria-hidden="true" />
            <div>
              <h1 className="text-2xl font-semibold tracking-normal">
                User detail is unavailable.
              </h1>
              <p className="mt-2 text-sm text-ink/65">{state.message}</p>
            </div>
          </div>
        </Card>
      ) : null}

      {detail ? (
        <div className="space-y-6">
          <section className="flex flex-col gap-4 md:flex-row md:items-end md:justify-between">
            <div>
              <p className="eyebrow">
                <span className="dot" aria-hidden="true" />
                User detail
              </p>
              <h1 className="mt-3 break-words text-4xl font-semibold tracking-normal text-ink">
                {detail.email || detail.id}
              </h1>
              <p className="mt-2 font-mono text-xs text-ink/45">
                {detail.externalAuthUserId}
              </p>
            </div>
            <Card className="grid gap-4 p-5 sm:grid-cols-3">
              <SummaryItem
                label="Subscription"
                value={formatStatus(detail.subscription.status)}
              />
              <SummaryItem label="Cost to date" value={formatUsd(detail.costToDateUsd)} />
              <SummaryItem label="Created" value={formatDateTime(detail.createdAt)} />
            </Card>
          </section>

          <section className="grid gap-4 lg:grid-cols-[1fr_360px]">
            <div className="space-y-6">
              <Card className="p-5">
                <h2 className="text-xl font-semibold tracking-normal">Subscription</h2>
                <div className="mt-5 grid gap-4 sm:grid-cols-2">
                  <SummaryItem
                    label="Customer"
                    value={detail.subscription.stripeCustomerId}
                  />
                  <SummaryItem
                    label="Subscription id"
                    value={detail.subscription.stripeSubscriptionId}
                  />
                  <SummaryItem
                    label="Current period end"
                    value={formatDateTime(detail.subscription.currentPeriodEnd)}
                  />
                  <SummaryItem label="Updated" value={formatDateTime(detail.updatedAt)} />
                </div>
              </Card>

              <PaymentTable payments={detail.payments} />
              <CreditTable credits={detail.credits} />
              <UsageTable usage={detail.usage} />
            </div>

            <aside className="space-y-4">
              <Card className="p-5">
                <div className="flex items-center gap-2">
                  <CreditCard className="h-5 w-5 text-clay" aria-hidden="true" />
                  <h2 className="text-lg font-semibold tracking-normal">
                    Grant credits
                  </h2>
                </div>
                <form className="mt-4 space-y-3" onSubmit={submitCredits}>
                  <label className="block text-sm font-semibold text-ink/70">
                    Amount
                    <Input
                      min={1}
                      onChange={(event) => setCreditAmount(event.target.value)}
                      required
                      type="number"
                      value={creditAmount}
                    />
                  </label>
                  <label className="block text-sm font-semibold text-ink/70">
                    Reason
                    <Textarea
                      className="mt-1 min-h-24"
                      onChange={(event) => setCreditReason(event.target.value)}
                      required
                      value={creditReason}
                    />
                  </label>
                  <Button
                    className="w-full"
                    disabled={actionLoading !== null}
                    type="submit"
                  >
                    {actionLoading === "credits" ? "Granting..." : "Grant credits"}
                  </Button>
                </form>
              </Card>

              <Card className="p-5">
                <div className="flex items-center gap-2">
                  <CreditCard className="h-5 w-5 text-rust" aria-hidden="true" />
                  <h2 className="text-lg font-semibold tracking-normal">
                    Issue refund
                  </h2>
                </div>
                <form className="mt-4 space-y-3" onSubmit={submitRefund}>
                  <label className="block text-sm font-semibold text-ink/70">
                    Payment
                    <select
                      className="mt-1 w-full rounded-md border border-line bg-white px-3 py-2 text-sm text-ink outline-none transition focus:border-clay focus:ring-2 focus:ring-clay/15"
                      onChange={(event) => setRefundPaymentIntentId(event.target.value)}
                      required
                      value={refundPaymentIntentId}
                    >
                      <option value="">Select payment</option>
                      {selectablePayments.map((payment) => (
                        <option
                          key={payment.creditId}
                          value={payment.paymentIntentId ?? ""}
                        >
                          {paymentLabel(payment)}
                        </option>
                      ))}
                    </select>
                  </label>
                  <label className="block text-sm font-semibold text-ink/70">
                    Amount
                    <Input
                      min={1}
                      onChange={(event) => setRefundAmount(event.target.value)}
                      required
                      type="number"
                      value={refundAmount}
                    />
                  </label>
                  <label className="block text-sm font-semibold text-ink/70">
                    Currency
                    <Input
                      onChange={(event) => setRefundCurrency(event.target.value)}
                      placeholder="nzd"
                      value={refundCurrency}
                    />
                  </label>
                  <label className="block text-sm font-semibold text-ink/70">
                    Reason
                    <Textarea
                      className="mt-1 min-h-24"
                      onChange={(event) => setRefundReason(event.target.value)}
                      required
                      value={refundReason}
                    />
                  </label>
                  <Button
                    className="w-full"
                    disabled={actionLoading !== null || selectablePayments.length === 0}
                    type="submit"
                    variant="clay"
                  >
                    {actionLoading === "refund" ? "Issuing..." : "Issue refund"}
                  </Button>
                </form>
              </Card>

              <Card className="p-5">
                <h2 className="text-lg font-semibold tracking-normal">
                  Account access
                </h2>
                <div className="mt-4 grid gap-2 sm:grid-cols-2 lg:grid-cols-1">
                  <Button
                    disabled={actionLoading !== null}
                    onClick={() => void submitSuspension(true)}
                    type="button"
                    variant="secondary"
                  >
                    <ShieldOff className="h-4 w-4" aria-hidden="true" />
                    Suspend
                  </Button>
                  <Button
                    disabled={actionLoading !== null}
                    onClick={() => void submitSuspension(false)}
                    type="button"
                    variant="secondary"
                  >
                    <ShieldCheck className="h-4 w-4" aria-hidden="true" />
                    Unsuspend
                  </Button>
                </div>
              </Card>

              {actionError ? (
                <Card className="border-rust/40 p-5">
                  <div className="flex items-start gap-3 text-rust">
                    <AlertCircle className="mt-0.5 h-5 w-5" aria-hidden="true" />
                    <p className="text-sm font-semibold">{actionError}</p>
                  </div>
                </Card>
              ) : null}

              {outcome ? (
                <Card className="p-5">
                  <div className="flex items-start gap-3">
                    <CheckCircle2
                      className="mt-0.5 h-5 w-5 text-clay"
                      aria-hidden="true"
                    />
                    <div className="min-w-0">
                      <h2 className="text-sm font-semibold text-ink">
                        {outcome.title}
                      </h2>
                      <pre className="mt-3 max-h-56 overflow-auto rounded-md bg-paper-deep/60 p-3 text-xs text-ink/70">
                        {JSON.stringify(outcome.payload, null, 2)}
                      </pre>
                    </div>
                  </div>
                </Card>
              ) : null}
            </aside>
          </section>
        </div>
      ) : null}
    </main>
  );
}
