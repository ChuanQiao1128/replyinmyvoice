"use client";

import { AlertCircle, ChevronLeft, ChevronRight, Loader2, Search } from "lucide-react";
import Link from "next/link";
import { useEffect, useMemo, useState } from "react";

import type {
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
      users: AdminUsersListResponse;
    }
  | { status: "error"; message: string };

const pageSize = 25;

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

  useEffect(() => {
    let current = true;

    async function loadAdminData() {
      setState({ status: "loading" });
      try {
        const params = new URLSearchParams({
          page: String(page),
          pageSize: String(pageSize),
        });
        const [stats, users] = await Promise.all([
          loadJson<AdminStatsResponse>("/api/admin/stats"),
          loadJson<AdminUsersListResponse>(`/api/admin/users?${params}`),
        ]);

        if (current) {
          setState({ stats, status: "ready", users });
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

    return state.users.users.filter((user) => userMatchesSearch(user, query));
  }, [query, state]);

  const totalPages = state.status === "ready" ? state.users.totalPages : 0;
  const canGoBack = page > 1;
  const canGoForward = totalPages > 0 && page < totalPages;

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

          <Card className="overflow-hidden">
            <div className="flex flex-col gap-3 border-b border-line px-5 py-4 md:flex-row md:items-center md:justify-between">
              <div>
                <h2 className="text-xl font-semibold tracking-normal">Users</h2>
                <p className="text-sm text-ink/55">
                  {state.users.totalCount} total, page {state.users.page} of{" "}
                  {Math.max(state.users.totalPages, 1)}
                </p>
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
                    </tr>
                  ))}
                  {filteredUsers.length === 0 ? (
                    <tr>
                      <td className="px-5 py-8 text-center text-sm text-ink/55" colSpan={6}>
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
    </main>
  );
}
