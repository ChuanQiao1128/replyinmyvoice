"use client";

import {
  AlertCircle,
  CheckCircle2,
  ChevronLeft,
  ChevronRight,
  Loader2,
  Search,
} from "lucide-react";
import Link from "next/link";
import { useEffect, useMemo, useState } from "react";

import type {
  AdminBillingSupportRequest,
  AdminStatsResponse,
  AdminUserListItem,
  AdminUsersListResponse,
} from "../../lib/admin-types";
import { Button } from "../ui/button";
import { Card } from "../ui/card";
import { Input } from "../ui/input";

type AdminDashboardState =
  | { status: "loading" }
  | {
      status: "ready";
      stats: AdminStatsResponse;
      supportRequests: AdminBillingSupportRequest[];
      users: AdminUsersListResponse;
    }
  | { status: "error"; message: string };

const pageSize = 25;
const eraseConfirmationWord = "ERASE";

async function readJsonError(response: Response) {
  const payload = (await response.json().catch(() => null)) as {
    error?: string;
    detail?: string;
    title?: string;
  } | null;

  return payload?.error ?? payload?.detail ?? payload?.title;
}

async function loadJson<T>(url: string): Promise<T> {
  const response = await fetch(url, { cache: "no-store" });

  if (response.status === 401) {
    window.location.assign("/sign-in");
    throw new Error("Authentication required.");
  }

  if (!response.ok) {
    throw new Error((await readJsonError(response)) ?? "Admin request failed.");
  }

  return (await response.json()) as T;
}

async function postJson<T>(url: string): Promise<T> {
  return requestJson<T>(url, "POST");
}

async function deleteJson<T>(url: string): Promise<T> {
  return requestJson<T>(url, "DELETE");
}

async function requestJson<T>(url: string, method: "DELETE" | "POST"): Promise<T> {
  const response = await fetch(url, {
    cache: "no-store",
    method,
  });

  if (response.status === 401) {
    window.location.assign("/sign-in");
    throw new Error("Authentication required.");
  }

  if (!response.ok) {
    throw new Error((await readJsonError(response)) ?? "Admin request failed.");
  }

  const body = await response.text();
  if (!body) {
    return undefined as T;
  }

  return JSON.parse(body) as T;
}

function formatDate(value: string) {
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

function userMatchesSearch(user: AdminUserListItem, query: string) {
  const normalized = query.trim().toLowerCase();
  if (!normalized) {
    return true;
  }

  return [
    user.email,
    user.id,
    user.externalAuthUserId,
    user.subscriptionStatus,
  ].some((value) => value?.toLowerCase().includes(normalized));
}

function isErasedUser(user: AdminUserListItem) {
  return user.externalAuthUserId.startsWith("erased:");
}

function StatTile({
  label,
  value,
}: {
  label: string;
  value: string | number;
}) {
  return (
    <Card className="p-5">
      <p className="text-xs font-semibold uppercase text-ink/45">
        {label}
      </p>
      <p className="mt-3 text-2xl font-semibold tracking-normal text-ink">
        {value}
      </p>
    </Card>
  );
}

export function AdminDashboard() {
  const [page, setPage] = useState(1);
  const [query, setQuery] = useState("");
  const [state, setState] = useState<AdminDashboardState>({ status: "loading" });
  const [queueActionId, setQueueActionId] = useState<string | null>(null);
  const [userActionError, setUserActionError] = useState<string | null>(null);
  const [userActionId, setUserActionId] = useState<string | null>(null);
  const [eraseTargetUserId, setEraseTargetUserId] = useState<string | null>(null);
  const [eraseConfirmation, setEraseConfirmation] = useState("");

  useEffect(() => {
    let current = true;

    async function loadAdminData() {
      setState({ status: "loading" });
      try {
        const params = new URLSearchParams({
          page: String(page),
          pageSize: String(pageSize),
        });
        const [stats, users, supportRequests] = await Promise.all([
          loadJson<AdminStatsResponse>("/api/admin/stats"),
          loadJson<AdminUsersListResponse>(`/api/admin/users?${params}`),
          loadJson<AdminBillingSupportRequest[]>("/api/admin/billing-support-requests"),
        ]);

        if (current) {
          setState({ stats, status: "ready", supportRequests, users });
        }
      } catch (error) {
        if (current) {
          setState({
            message:
              error instanceof Error ? error.message : "Admin data is unavailable.",
            status: "error",
          });
        }
      }
    }

    void loadAdminData();

    return () => {
      current = false;
    };
  }, [page]);

  const filteredUsers = useMemo(() => {
    if (state.status !== "ready") {
      return [];
    }

    return state.users.users.filter(
      (user) => !isErasedUser(user) && userMatchesSearch(user, query),
    );
  }, [query, state]);

  const hiddenErasedUserCount = useMemo(() => {
    if (state.status !== "ready") {
      return 0;
    }

    return state.users.users.filter(isErasedUser).length;
  }, [state]);

  const totalPages = state.status === "ready" ? state.users.totalPages : 0;
  const canGoBack = page > 1;
  const canGoForward = totalPages > 0 && page < totalPages;
  const eraseTargetUser = useMemo(() => {
    if (state.status !== "ready" || !eraseTargetUserId) {
      return null;
    }

    return state.users.users.find((user) => user.id === eraseTargetUserId) ?? null;
  }, [eraseTargetUserId, state]);

  async function resolveSupportRequest(requestId: string) {
    if (state.status !== "ready" || queueActionId) {
      return;
    }

    setQueueActionId(requestId);
    try {
      const resolved = await postJson<AdminBillingSupportRequest>(
        `/api/admin/billing-support-requests/${requestId}/resolve`,
      );
      setState({
        ...state,
        supportRequests: state.supportRequests.filter(
          (request) => request.id !== resolved.id,
        ),
      });
    } catch (error) {
      setState({
        message:
          error instanceof Error
            ? error.message
            : "Admin billing support action failed.",
        status: "error",
      });
    } finally {
      setQueueActionId(null);
    }
  }

  function openEraseConfirmation(userId: string) {
    if (userActionId) {
      return;
    }

    setUserActionError(null);
    setEraseTargetUserId(userId);
    setEraseConfirmation("");
  }

  function closeEraseConfirmation() {
    if (userActionId) {
      return;
    }

    setEraseTargetUserId(null);
    setEraseConfirmation("");
  }

  async function deleteUser(userId: string) {
    if (
      state.status !== "ready" ||
      userActionId ||
      eraseConfirmation !== eraseConfirmationWord
    ) {
      return;
    }

    setUserActionError(null);
    setUserActionId(userId);
    try {
      await deleteJson<void>(`/api/admin/users/${encodeURIComponent(userId)}`);
      setState((current) => {
        if (current.status !== "ready") {
          return current;
        }

        return {
          ...current,
          users: {
            ...current.users,
            totalCount: Math.max(0, current.users.totalCount - 1),
            users: current.users.users.filter((user) => user.id !== userId),
          },
        };
      });
      setEraseTargetUserId(null);
      setEraseConfirmation("");
    } catch (error) {
      setUserActionError(
        error instanceof Error ? error.message : "Account erase failed.",
      );
    } finally {
      setUserActionId(null);
    }
  }

  return (
    <main className="wrap py-10">
      <div className="mb-8 flex flex-col gap-4 md:flex-row md:items-end md:justify-between">
        <div>
          <p className="eyebrow">
            <span className="dot" aria-hidden="true" />
            Operations
          </p>
          <h1 className="mt-3 text-4xl font-semibold tracking-normal text-ink">
            Admin
          </h1>
        </div>
        <div className="relative w-full md:max-w-sm">
          <Search
            className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-ink/40"
            aria-hidden="true"
          />
          <Input
            aria-label="Search users"
            className="pl-9"
            onChange={(event) => setQuery(event.target.value)}
            placeholder="Search email, id, or status"
            value={query}
          />
        </div>
      </div>

      {state.status === "loading" ? (
        <Card className="p-8">
          <div className="flex items-center gap-3 text-ink/65">
            <Loader2 className="h-5 w-5 animate-spin" aria-hidden="true" />
            <p>Loading admin data...</p>
          </div>
        </Card>
      ) : null}

      {state.status === "error" ? (
        <Card className="p-8">
          <div className="flex items-start gap-3">
            <AlertCircle className="mt-0.5 h-5 w-5 text-rust" aria-hidden="true" />
            <div>
              <h2 className="text-xl font-semibold tracking-normal">
                Admin data is unavailable.
              </h2>
              <p className="mt-2 text-sm text-ink/65">{state.message}</p>
            </div>
          </div>
        </Card>
      ) : null}

      {state.status === "ready" ? (
        <div className="space-y-6">
          <section className="grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
            <StatTile label="Total users" value={state.stats.totalUsers} />
            <StatTile label="Paid users" value={state.stats.paidUsers} />
            <StatTile label="Credits remaining" value={state.stats.creditRemaining} />
            <StatTile label="Refund review" value={state.stats.refundReview.flaggedUserCount} />
            <StatTile label="Cost to date" value={formatUsd(state.stats.costToDateUsd)} />
          </section>

          <Link
            aria-label="Promo codes"
            className="group flex flex-col gap-2 rounded-md border border-line bg-white/50 px-5 py-4 transition hover:bg-paper-deep/50 sm:flex-row sm:items-center sm:justify-between"
            href="/admin/promo-codes"
          >
            <span>
              <span className="block text-sm font-semibold text-ink">
                Promo codes
              </span>
              <span className="mt-1 block text-sm text-ink/55">
                Create & manage redeemable trial codes
              </span>
            </span>
            <ChevronRight
              className="h-4 w-4 text-ink/45 transition group-hover:translate-x-0.5"
              aria-hidden="true"
            />
          </Link>

          <Card className="overflow-hidden">
            <div className="border-b border-line px-5 py-4">
              <h2 className="text-xl font-semibold tracking-normal">
                Billing support queue
              </h2>
              <p className="text-sm text-ink/55">
                {state.supportRequests.length} open request
                {state.supportRequests.length === 1 ? "" : "s"}
              </p>
            </div>
            <div className="overflow-x-auto">
              <table className="min-w-full text-left text-sm">
                <thead className="border-b border-line bg-paper-deep/40 text-xs uppercase text-ink/50">
                  <tr>
                    <th className="px-5 py-3 font-semibold">Customer</th>
                    <th className="px-5 py-3 font-semibold">Reason</th>
                    <th className="px-5 py-3 font-semibold">Payment intent</th>
                    <th className="px-5 py-3 font-semibold">Message</th>
                    <th className="px-5 py-3 font-semibold">Actions</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-line">
                  {state.supportRequests.map((request) => (
                    <tr key={request.id} className="bg-white/45">
                      <td className="px-5 py-4 align-top">
                        <Link
                          className="font-semibold text-ink underline-offset-4 hover:underline"
                          href={`/admin/users/${request.userId}`}
                        >
                          {request.userEmail || request.userId}
                        </Link>
                        {request.externalAuthUserId ? (
                          <p className="mt-1 max-w-xs truncate font-mono text-xs text-ink/45">
                            {request.externalAuthUserId}
                          </p>
                        ) : null}
                      </td>
                      <td className="px-5 py-4 align-top text-ink/70">
                        {request.type === "refund"
                          ? "Refund request"
                          : "Billing question"}
                        <span className="mt-1 block text-xs text-ink/45">
                          {formatDate(request.createdAt)}
                        </span>
                      </td>
                      <td className="px-5 py-4 align-top font-mono text-xs text-ink/60">
                        {request.relatedPaymentIntentId ?? "—"}
                      </td>
                      <td className="max-w-md px-5 py-4 align-top text-ink/70">
                        {request.message}
                      </td>
                      <td className="px-5 py-4 align-top">
                        <div className="flex flex-col gap-2">
                          <Link
                            className="inline-flex min-h-9 items-center justify-center rounded-md border border-line bg-paper px-3 py-1.5 text-xs font-semibold text-ink hover:bg-paper-deep"
                            href={`/admin/users/${request.userId}${
                              request.relatedPaymentIntentId
                                ? `?paymentIntentId=${encodeURIComponent(
                                    request.relatedPaymentIntentId,
                                  )}#refund-action`
                                : ""
                            }`}
                          >
                            Refund action
                          </Link>
                          <Button
                            disabled={queueActionId !== null}
                            onClick={() => void resolveSupportRequest(request.id)}
                            type="button"
                            variant="secondary"
                          >
                            {queueActionId === request.id ? (
                              <Loader2
                                className="h-4 w-4 animate-spin"
                                aria-hidden="true"
                              />
                            ) : (
                              <CheckCircle2 className="h-4 w-4" aria-hidden="true" />
                            )}
                            Resolve
                          </Button>
                        </div>
                      </td>
                    </tr>
                  ))}
                  {state.supportRequests.length === 0 ? (
                    <tr>
                      <td className="px-5 py-8 text-center text-sm text-ink/55" colSpan={5}>
                        No open billing support requests.
                      </td>
                    </tr>
                  ) : null}
                </tbody>
              </table>
            </div>
          </Card>

          <Card className="overflow-hidden">
            <div className="flex flex-col gap-3 border-b border-line px-5 py-4 md:flex-row md:items-center md:justify-between">
              <div>
                <h2 className="text-xl font-semibold tracking-normal">Users</h2>
                <p className="text-sm text-ink/55">
                  {state.users.totalCount} total, page {state.users.page} of{" "}
                  {Math.max(state.users.totalPages, 1)}
                </p>
                <p className="mt-1 text-xs text-ink/55">
                  Inactive / Free = no active subscription (normal). Canceled =
                  account erased.
                </p>
                {hiddenErasedUserCount > 0 ? (
                  <p className="mt-1 text-xs text-ink/55">
                    {hiddenErasedUserCount} erased account
                    {hiddenErasedUserCount === 1 ? "" : "s"} hidden
                  </p>
                ) : null}
                {userActionError ? (
                  <p className="mt-2 text-xs font-semibold text-rust" role="alert">
                    {userActionError}
                  </p>
                ) : null}
              </div>
              <div className="flex items-center gap-2">
                <Button
                  aria-label="Previous page"
                  disabled={!canGoBack}
                  onClick={() => setPage((value) => Math.max(1, value - 1))}
                  type="button"
                  variant="secondary"
                >
                  <ChevronLeft className="h-4 w-4" aria-hidden="true" />
                </Button>
                <Button
                  aria-label="Next page"
                  disabled={!canGoForward}
                  onClick={() => setPage((value) => value + 1)}
                  type="button"
                  variant="secondary"
                >
                  <ChevronRight className="h-4 w-4" aria-hidden="true" />
                </Button>
              </div>
            </div>

            <div className="overflow-x-auto">
              <table className="min-w-full text-left text-sm">
                <thead className="border-b border-line bg-paper-deep/40 text-xs uppercase text-ink/50">
                  <tr>
                    <th className="px-5 py-3 font-semibold">User</th>
                    <th className="px-5 py-3 font-semibold">Status</th>
                    <th className="px-5 py-3 font-semibold">Usage</th>
                    <th className="px-5 py-3 font-semibold">Credits</th>
                    <th className="px-5 py-3 font-semibold">Cost</th>
                    <th className="px-5 py-3 font-semibold">Created</th>
                    <th className="px-5 py-3 font-semibold">Actions</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-line">
                  {filteredUsers.map((user) => (
                    <tr key={user.id} className="bg-white/45">
                      <td className="px-5 py-4 align-top">
                        <Link
                          className="font-semibold text-ink underline-offset-4 hover:underline"
                          href={`/admin/users/${user.id}`}
                        >
                          {user.email || user.id}
                        </Link>
                        <p className="mt-1 max-w-xs truncate font-mono text-xs text-ink/45">
                          {user.externalAuthUserId}
                        </p>
                      </td>
                      <td className="px-5 py-4 align-top">
                        <span className="inline-flex rounded-md border border-line bg-paper px-2.5 py-1 text-xs font-semibold text-ink/70">
                          {formatStatus(user.subscriptionStatus)}
                        </span>
                      </td>
                      <td className="px-5 py-4 align-top text-ink/70">
                        {user.usedRewrites} used
                        <span className="block text-xs text-ink/45">
                          {user.reservedRewrites} reserved
                        </span>
                      </td>
                      <td className="px-5 py-4 align-top text-ink/70">
                        {user.creditRemaining}
                      </td>
                      <td className="px-5 py-4 align-top text-ink/70">
                        {formatUsd(user.costToDateUsd)}
                      </td>
                      <td className="px-5 py-4 align-top text-ink/70">
                        {formatDate(user.createdAt)}
                      </td>
                      <td className="px-5 py-4 align-top">
                        <Button
                          className="min-h-8 px-3 py-1.5 text-xs text-rust hover:bg-rust/10"
                          disabled={userActionId !== null}
                          onClick={() => openEraseConfirmation(user.id)}
                          type="button"
                          variant="secondary"
                        >
                          {userActionId === user.id ? (
                            <Loader2
                              className="h-4 w-4 animate-spin"
                              aria-hidden="true"
                            />
                          ) : null}
                          Delete
                        </Button>
                      </td>
                    </tr>
                  ))}
                  {filteredUsers.length === 0 ? (
                    <tr>
                      <td className="px-5 py-8 text-center text-sm text-ink/55" colSpan={7}>
                        No users match this search.
                      </td>
                    </tr>
                  ) : null}
                </tbody>
              </table>
            </div>
          </Card>
        </div>
      ) : null}

      {eraseTargetUser ? (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-ink/35 px-4 py-8"
          role="presentation"
        >
          <Card
            aria-labelledby="erase-confirmation-title"
            aria-modal="true"
            className="w-full max-w-md border-rust/30 bg-white p-5 shadow-xl"
            role="dialog"
          >
            <div className="flex items-start gap-3">
              <AlertCircle
                className="mt-0.5 h-5 w-5 shrink-0 text-rust"
                aria-hidden="true"
              />
              <div>
                <h2
                  className="text-lg font-semibold tracking-normal text-ink"
                  id="erase-confirmation-title"
                >
                  Erase account
                </h2>
                <p className="mt-2 text-sm text-ink/65">
                  This anonymizes the user and removes their data. This cannot
                  be undone.
                </p>
                <p className="mt-3 break-words text-sm font-semibold text-ink">
                  {eraseTargetUser.email || eraseTargetUser.id}
                </p>
              </div>
            </div>
            <label className="mt-5 block text-sm font-semibold text-ink/70">
              Type {eraseConfirmationWord} to confirm account erase.
              <Input
                aria-label="Type ERASE to confirm account erase"
                className="mt-1"
                onChange={(event) => setEraseConfirmation(event.target.value)}
                value={eraseConfirmation}
              />
            </label>
            <div className="mt-5 flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
              <Button
                disabled={userActionId !== null}
                onClick={closeEraseConfirmation}
                type="button"
                variant="secondary"
              >
                Cancel
              </Button>
              <Button
                className="bg-rust text-white hover:bg-rust/90"
                disabled={
                  userActionId !== null ||
                  eraseConfirmation !== eraseConfirmationWord
                }
                onClick={() => void deleteUser(eraseTargetUser.id)}
                type="button"
              >
                {userActionId === eraseTargetUser.id ? (
                  <Loader2
                    className="h-4 w-4 animate-spin"
                    aria-hidden="true"
                  />
                ) : null}
                Erase account
              </Button>
            </div>
          </Card>
        </div>
      ) : null}
    </main>
  );
}
