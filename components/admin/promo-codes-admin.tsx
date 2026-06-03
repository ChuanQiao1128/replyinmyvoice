"use client";

import {
  Activity,
  ArrowLeft,
  Ban,
  BarChart3,
  CheckCircle2,
  Loader2,
  Plus,
  RotateCcw,
  Ticket,
  Users,
} from "lucide-react";
import Link from "next/link";
import type { ReactNode } from "react";
import { FormEvent, useEffect, useMemo, useState } from "react";

import {
  adminPromoCodesFromPayload,
  adminPromoDetailFromPayload,
  derivePromoCodeStatus,
  fieldErrorsFromAdminError,
  type AdminPromoCode,
  type AdminPromoCreateFieldErrors,
  type AdminPromoCreateFormValues,
  type AdminPromoDetail,
  type AdminPromoStatus,
  validatePromoCreateForm,
} from "../../lib/admin-promo-codes";
import { Button } from "../ui/button";
import { Input } from "../ui/input";

type PromoCodesAdminProps = {
  initialCodes: AdminPromoCode[];
  initialError?: string;
};

const statusClasses: Record<AdminPromoStatus, string> = {
  active: "border-clay/25 bg-mint text-clay",
  disabled: "border-line bg-paper-deep text-ink/70",
  exhausted: "border-rust/25 bg-rust/10 text-rust",
  expired: "border-line bg-white text-ink/60",
  pending: "border-gold/25 bg-gold/10 text-gold",
};

function addDays(date: Date, days: number) {
  const next = new Date(date);
  next.setDate(next.getDate() + days);
  return next;
}

function toLocalDateTimeInput(date: Date) {
  const pad = (value: number) => value.toString().padStart(2, "0");
  return [
    date.getFullYear(),
    "-",
    pad(date.getMonth() + 1),
    "-",
    pad(date.getDate()),
    "T",
    pad(date.getHours()),
    ":",
    pad(date.getMinutes()),
  ].join("");
}

function initialFormValues(): AdminPromoCreateFormValues {
  const now = new Date();
  return {
    code: "",
    credits: "3",
    displayCode: "",
    globalCap: "1000",
    perUserCap: "1",
    ttlDays: "90",
    validFrom: toLocalDateTimeInput(now),
    validUntil: toLocalDateTimeInput(addDays(now, 90)),
  };
}

function displayCode(code: AdminPromoCode) {
  return code.displayCode || code.code;
}

function statusLabel(status: AdminPromoStatus) {
  return status[0]?.toUpperCase() + status.slice(1);
}

function formatDate(value: string) {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return "Invalid date";
  }

  return new Intl.DateTimeFormat("en", {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(date);
}

async function readJsonPayload(response: Response) {
  try {
    return await response.json();
  } catch {
    return null;
  }
}

function replaceCode(codes: AdminPromoCode[], nextCode: AdminPromoCode) {
  return codes.map((code) => (code.id === nextCode.id ? nextCode : code));
}

function codeFromPayload(payload: unknown) {
  return adminPromoCodesFromPayload({ promoCodes: [payload] })[0] ?? null;
}

function FieldError({ children }: { children?: string }) {
  if (!children) {
    return null;
  }

  return <p className="mt-1 text-xs font-medium text-rust">{children}</p>;
}

function StatTile({
  icon,
  label,
  value,
}: {
  icon: ReactNode;
  label: string;
  value: string;
}) {
  return (
    <div className="rounded-lg border border-line bg-white px-4 py-3">
      <div className="mb-2 flex items-center gap-2 text-xs font-semibold uppercase text-ink/55">
        {icon}
        {label}
      </div>
      <div className="text-xl font-semibold text-ink">{value}</div>
    </div>
  );
}

function PromoStatsPanel({
  detail,
  error,
  loading,
}: {
  detail: AdminPromoDetail | null;
  error: string;
  loading: boolean;
}) {
  if (loading) {
    return (
      <section className="rounded-lg border border-line bg-white/80 p-5">
        <div className="flex items-center gap-2 text-sm font-semibold text-ink/70">
          <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
          Loading stats
        </div>
      </section>
    );
  }

  if (error) {
    return (
      <section className="rounded-lg border border-rust/25 bg-rust/10 p-5 text-sm text-rust">
        {error}
      </section>
    );
  }

  if (!detail) {
    return (
      <section className="rounded-lg border border-line bg-white/70 p-5 text-sm text-ink/60">
        Select a promo code to view redemptions, activation, and hash clusters.
      </section>
    );
  }

  const maxDaily = Math.max(
    1,
    ...detail.stats.dailyCurve.map((day) => day.redemptions),
  );
  const activation = `${Math.round(detail.stats.activationRate * 100)}% activation`;

  return (
    <section className="space-y-4 rounded-lg border border-line bg-white/80 p-5 shadow-crisp">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <p className="text-xs font-semibold uppercase text-clay">
            Per-code stats
          </p>
          <h2 className="mt-1 text-2xl font-semibold text-ink">
            {displayCode(detail.promoCode)}
          </h2>
        </div>
        <span
          className={`rounded-full border px-3 py-1 text-xs font-semibold ${statusClasses[detail.promoCode.status]}`}
        >
          {statusLabel(detail.promoCode.status)}
        </span>
      </div>

      <div className="grid gap-3 sm:grid-cols-3">
        <StatTile
          icon={<Activity className="h-4 w-4" aria-hidden="true" />}
          label="Redemptions"
          value={`${detail.stats.totalRedemptions} redemptions`}
        />
        <StatTile
          icon={<Users className="h-4 w-4" aria-hidden="true" />}
          label="Users"
          value={`${detail.stats.distinctUsers} distinct users`}
        />
        <StatTile
          icon={<BarChart3 className="h-4 w-4" aria-hidden="true" />}
          label="Activation"
          value={activation}
        />
      </div>

      <div className="grid gap-5 lg:grid-cols-[1fr_1.1fr]">
        <div>
          <h3 className="mb-3 text-sm font-semibold text-ink">Daily curve</h3>
          {detail.stats.dailyCurve.length ? (
            <div className="space-y-3">
              {detail.stats.dailyCurve.map((day) => (
                <div
                  className="grid grid-cols-[6.5rem_1fr_2rem] items-center gap-3 text-sm"
                  key={day.date}
                >
                  <span className="font-mono text-xs text-ink/60">{day.date}</span>
                  <span className="h-2 rounded-full bg-paper-deep">
                    <span
                      className="block h-2 rounded-full bg-clay"
                      style={{ width: `${Math.max(8, (day.redemptions / maxDaily) * 100)}%` }}
                    />
                  </span>
                  <span className="text-right font-semibold">{day.redemptions}</span>
                </div>
              ))}
            </div>
          ) : (
            <p className="text-sm text-ink/60">No redemptions recorded.</p>
          )}
        </div>

        <div>
          <h3 className="mb-3 text-sm font-semibold text-ink">IP-hash clusters</h3>
          {detail.stats.ipHashClusters.length ? (
            <div className="overflow-x-auto rounded-lg border border-line">
              <table className="min-w-full divide-y divide-line text-left text-sm">
                <thead className="bg-paper text-xs uppercase text-ink/55">
                  <tr>
                    <th className="px-3 py-2 font-semibold">Hash</th>
                    <th className="px-3 py-2 font-semibold">Uses</th>
                    <th className="px-3 py-2 font-semibold">Users</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-line bg-white">
                  {detail.stats.ipHashClusters.map((cluster) => (
                    <tr key={cluster.ipHash}>
                      <td className="max-w-[14rem] truncate px-3 py-2 font-mono text-xs">
                        {cluster.ipHash}
                      </td>
                      <td className="px-3 py-2 font-semibold">{cluster.redemptions}</td>
                      <td className="px-3 py-2">{cluster.distinctUsers}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <p className="text-sm text-ink/60">No hash clusters recorded.</p>
          )}
        </div>
      </div>
    </section>
  );
}

export function PromoCodesAdmin({
  initialCodes,
  initialError = "",
}: PromoCodesAdminProps) {
  const [codes, setCodes] = useState(initialCodes);
  const [listError, setListError] = useState(initialError);
  const [listLoading, setListLoading] = useState(false);
  const [selectedId, setSelectedId] = useState<string | null>(
    initialCodes[0]?.id ?? null,
  );
  const [detail, setDetail] = useState<AdminPromoDetail | null>(null);
  const [detailError, setDetailError] = useState("");
  const [detailLoading, setDetailLoading] = useState(false);
  const [formValues, setFormValues] = useState<AdminPromoCreateFormValues>(() =>
    initialFormValues(),
  );
  const [fieldErrors, setFieldErrors] = useState<AdminPromoCreateFieldErrors>({});
  const [formError, setFormError] = useState("");
  const [formSuccess, setFormSuccess] = useState("");
  const [creating, setCreating] = useState(false);
  const [updatingId, setUpdatingId] = useState<string | null>(null);

  const selectedCode = useMemo(
    () => codes.find((code) => code.id === selectedId) ?? null,
    [codes, selectedId],
  );

  useEffect(() => {
    if (!selectedId) {
      setDetail(null);
      return;
    }

    let active = true;
    setDetailLoading(true);
    setDetailError("");
    fetch(`/api/admin/promo-codes/${selectedId}`, { cache: "no-store" })
      .then(async (response) => {
        const payload = await readJsonPayload(response);
        if (!response.ok) {
          throw new Error("Could not load promo code stats.");
        }

        const nextDetail = adminPromoDetailFromPayload(payload);
        if (!nextDetail) {
          throw new Error("Promo code stats were invalid.");
        }

        if (active) {
          setDetail(nextDetail);
        }
      })
      .catch((error: unknown) => {
        if (active) {
          setDetail(null);
          setDetailError(
            error instanceof Error ? error.message : "Could not load promo code stats.",
          );
        }
      })
      .finally(() => {
        if (active) {
          setDetailLoading(false);
        }
      });

    return () => {
      active = false;
    };
  }, [selectedId]);

  async function refreshList() {
    setListLoading(true);
    setListError("");
    try {
      const response = await fetch("/api/admin/promo-codes", {
        cache: "no-store",
      });
      const payload = await readJsonPayload(response);
      if (!response.ok) {
        throw new Error("Could not load promo codes.");
      }
      const nextCodes = adminPromoCodesFromPayload(payload);
      setCodes(nextCodes);
      setSelectedId((current) =>
        current && nextCodes.some((code) => code.id === current)
          ? current
          : nextCodes[0]?.id ?? null,
      );
    } catch (error) {
      setListError(
        error instanceof Error ? error.message : "Could not load promo codes.",
      );
    } finally {
      setListLoading(false);
    }
  }

  function updateField(field: keyof AdminPromoCreateFormValues, value: string) {
    setFormValues((current) => ({ ...current, [field]: value }));
    setFieldErrors((current) => ({ ...current, [field]: undefined }));
    setFormError("");
    setFormSuccess("");
  }

  async function createCode(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const validation = validatePromoCreateForm(formValues);
    if (!validation.ok) {
      setFieldErrors(validation.fieldErrors);
      setFormError("Fix the highlighted fields and try again.");
      setFormSuccess("");
      return;
    }

    setCreating(true);
    setFieldErrors({});
    setFormError("");
    setFormSuccess("");

    try {
      const response = await fetch("/api/admin/promo-codes", {
        body: JSON.stringify(validation.payload),
        headers: {
          "Content-Type": "application/json",
        },
        method: "POST",
      });
      const payload = await readJsonPayload(response);
      if (!response.ok) {
        const nextFieldErrors = fieldErrorsFromAdminError(response.status, payload);
        setFieldErrors(nextFieldErrors);
        setFormError(
          Object.keys(nextFieldErrors).length
            ? "Fix the highlighted fields and try again."
            : "Could not create that promo code.",
        );
        return;
      }

      const nextCode = codeFromPayload(payload);
      if (!nextCode) {
        throw new Error("Create response was invalid.");
      }

      setCodes((current) => [
        nextCode,
        ...current.filter((code) => code.id !== nextCode.id),
      ]);
      setSelectedId(nextCode.id);
      setFormValues(initialFormValues());
      setFormSuccess(`${displayCode(nextCode)} created.`);
    } catch (error) {
      setFormError(
        error instanceof Error ? error.message : "Could not create that promo code.",
      );
    } finally {
      setCreating(false);
    }
  }

  async function setCodeActive(code: AdminPromoCode, active: boolean) {
    const action = active ? "enable" : "disable";
    const previousCodes = codes;
    const optimisticCode = {
      ...code,
      isActive: active,
      status: derivePromoCodeStatus({ ...code, isActive: active }),
    };

    setUpdatingId(code.id);
    setListError("");
    setCodes((current) => replaceCode(current, optimisticCode));

    try {
      const response = await fetch(`/api/admin/promo-codes/${code.id}/${action}`, {
        method: "POST",
      });
      const payload = await readJsonPayload(response);
      if (!response.ok) {
        throw new Error(`Could not ${action} ${displayCode(code)}.`);
      }

      const nextCode = codeFromPayload(payload);
      if (!nextCode) {
        throw new Error("Active-state response was invalid.");
      }

      setCodes((current) => replaceCode(current, nextCode));
      if (selectedId === nextCode.id && detail) {
        setDetail({ ...detail, promoCode: nextCode });
      }
    } catch (error) {
      setCodes(previousCodes);
      setListError(
        error instanceof Error
          ? error.message
          : `Could not ${action} ${displayCode(code)}.`,
      );
    } finally {
      setUpdatingId(null);
    }
  }

  return (
    <main className="min-h-screen bg-paper">
      <section className="border-b border-line bg-white/65">
        <div className="wrap py-8">
          <div className="flex flex-wrap items-end justify-between gap-4">
            <div>
              <Link
                className="mb-4 inline-flex items-center gap-2 text-sm font-semibold text-clay transition hover:text-ink focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-clay/35 focus-visible:ring-offset-2 focus-visible:ring-offset-paper"
                href="/admin"
              >
                <ArrowLeft className="h-4 w-4" aria-hidden="true" />
                Back to Admin
              </Link>
              <p className="text-xs font-semibold uppercase text-clay">
                Admin
              </p>
              <h1 className="mt-2 text-4xl font-semibold tracking-normal text-ink">
                Promo codes
              </h1>
              <p className="mt-2 max-w-2xl text-sm text-ink/65">
                Create trial-credit codes, control active state, and review code-level
                redemption signals.
              </p>
            </div>
            <Button
              className="min-w-28"
              disabled={listLoading}
              onClick={refreshList}
              type="button"
              variant="secondary"
            >
              {listLoading ? (
                <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
              ) : (
                <RotateCcw className="h-4 w-4" aria-hidden="true" />
              )}
              Refresh
            </Button>
          </div>
        </div>
      </section>

      <div className="wrap grid gap-6 py-6 xl:grid-cols-[minmax(20rem,24rem)_1fr]">
        <aside className="space-y-6">
          <section className="rounded-lg border border-line bg-white/80 p-5 shadow-crisp">
            <div className="mb-4 flex items-center gap-2">
              <Plus className="h-5 w-5 text-clay" aria-hidden="true" />
              <h2 className="text-lg font-semibold text-ink">New code</h2>
            </div>

            <form className="space-y-4" onSubmit={createCode}>
              <div>
                <label className="text-sm font-semibold text-ink" htmlFor="promo-code">
                  Code
                </label>
                <Input
                  autoComplete="off"
                  id="promo-code"
                  onChange={(event) => updateField("code", event.target.value)}
                  placeholder="SPRING2026"
                  value={formValues.code}
                />
                <FieldError>{fieldErrors.code}</FieldError>
              </div>

              <div>
                <label
                  className="text-sm font-semibold text-ink"
                  htmlFor="promo-display-code"
                >
                  Display code
                </label>
                <Input
                  autoComplete="off"
                  id="promo-display-code"
                  onChange={(event) => updateField("displayCode", event.target.value)}
                  placeholder="SPRING-2026"
                  value={formValues.displayCode}
                />
                <FieldError>{fieldErrors.displayCode}</FieldError>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label
                    className="text-sm font-semibold text-ink"
                    htmlFor="promo-credits"
                  >
                    Credits
                  </label>
                  <Input
                    id="promo-credits"
                    inputMode="numeric"
                    onChange={(event) => updateField("credits", event.target.value)}
                    value={formValues.credits}
                  />
                  <FieldError>{fieldErrors.credits}</FieldError>
                </div>
                <div>
                  <label className="text-sm font-semibold text-ink" htmlFor="promo-ttl">
                    TTL days
                  </label>
                  <Input
                    id="promo-ttl"
                    inputMode="numeric"
                    onChange={(event) => updateField("ttlDays", event.target.value)}
                    value={formValues.ttlDays}
                  />
                  <FieldError>{fieldErrors.ttlDays}</FieldError>
                </div>
              </div>

              <div>
                <label className="text-sm font-semibold text-ink" htmlFor="promo-from">
                  Valid from
                </label>
                <Input
                  id="promo-from"
                  onChange={(event) => updateField("validFrom", event.target.value)}
                  type="datetime-local"
                  value={formValues.validFrom}
                />
                <FieldError>{fieldErrors.validFrom}</FieldError>
              </div>

              <div>
                <label className="text-sm font-semibold text-ink" htmlFor="promo-until">
                  Valid until
                </label>
                <Input
                  id="promo-until"
                  onChange={(event) => updateField("validUntil", event.target.value)}
                  type="datetime-local"
                  value={formValues.validUntil}
                />
                <FieldError>{fieldErrors.validUntil}</FieldError>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label
                    className="text-sm font-semibold text-ink"
                    htmlFor="promo-global-cap"
                  >
                    Global cap
                  </label>
                  <Input
                    id="promo-global-cap"
                    inputMode="numeric"
                    onChange={(event) => updateField("globalCap", event.target.value)}
                    value={formValues.globalCap}
                  />
                  <FieldError>{fieldErrors.globalCap}</FieldError>
                </div>
                <div>
                  <label
                    className="text-sm font-semibold text-ink"
                    htmlFor="promo-per-user-cap"
                  >
                    Per-user cap
                  </label>
                  <Input
                    id="promo-per-user-cap"
                    inputMode="numeric"
                    onChange={(event) => updateField("perUserCap", event.target.value)}
                    value={formValues.perUserCap}
                  />
                  <FieldError>{fieldErrors.perUserCap}</FieldError>
                </div>
              </div>

              {formError ? (
                <p className="rounded-md border border-rust/25 bg-rust/10 px-3 py-2 text-sm font-medium text-rust">
                  {formError}
                </p>
              ) : null}
              {formSuccess ? (
                <p className="rounded-md border border-clay/25 bg-mint px-3 py-2 text-sm font-medium text-clay">
                  {formSuccess}
                </p>
              ) : null}

              <Button className="w-full" disabled={creating} type="submit">
                {creating ? (
                  <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
                ) : (
                  <Plus className="h-4 w-4" aria-hidden="true" />
                )}
                Create code
              </Button>
            </form>
          </section>

          <section className="rounded-lg border border-line bg-white/80 p-5 shadow-crisp">
            <div className="mb-4 flex items-center justify-between gap-3">
              <div className="flex items-center gap-2">
                <Ticket className="h-5 w-5 text-clay" aria-hidden="true" />
                <h2 className="text-lg font-semibold text-ink">Codes</h2>
              </div>
              <span className="text-sm text-ink/55">{codes.length} total</span>
            </div>
            <p
              aria-label="Promo code status legend"
              className="mb-4 text-xs leading-5 text-ink/55"
            >
              Active = redeemable now · Pending = not yet active (valid-from is in the
              future) · Expired = past valid-until · Exhausted = global cap reached ·
              Disabled = turned off by an admin.
            </p>

            {listError ? (
              <p className="mb-3 rounded-md border border-rust/25 bg-rust/10 px-3 py-2 text-sm font-medium text-rust">
                {listError}
              </p>
            ) : null}

            {listLoading ? (
              <div className="flex items-center gap-2 text-sm text-ink/60">
                <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
                Loading promo codes
              </div>
            ) : codes.length === 0 ? (
              <p className="rounded-lg border border-dashed border-line bg-paper/60 px-4 py-6 text-sm text-ink/60">
                No promo codes yet.
              </p>
            ) : (
              <div className="space-y-3">
                {codes.map((code) => (
                  <article
                    className={`rounded-lg border p-4 transition ${
                      code.id === selectedId
                        ? "border-clay/35 bg-mint/55"
                        : "border-line bg-white"
                    }`}
                    key={code.id}
                  >
                    <div className="flex items-start justify-between gap-3">
                      <div className="min-w-0">
                        <h3 className="truncate font-mono text-sm font-semibold text-ink">
                          {displayCode(code)}
                        </h3>
                        <p className="mt-1 text-xs text-ink/55">
                          {code.redemptionCount} redeemed · {code.creditsGranted} credits ·{" "}
                          {code.grantTtlDays} days
                        </p>
                      </div>
                      <span
                        className={`shrink-0 rounded-full border px-2.5 py-1 text-xs font-semibold ${statusClasses[code.status]}`}
                      >
                        {statusLabel(code.status)}
                      </span>
                    </div>

                    <dl className="mt-3 grid grid-cols-2 gap-2 text-xs text-ink/60">
                      <div>
                        <dt className="font-semibold text-ink/45">Valid from</dt>
                        <dd>{formatDate(code.validFrom)}</dd>
                      </div>
                      <div>
                        <dt className="font-semibold text-ink/45">Valid until</dt>
                        <dd>{formatDate(code.validUntil)}</dd>
                      </div>
                    </dl>

                    <div className="mt-4 grid grid-cols-2 gap-2">
                      <Button
                        aria-label={`View stats for ${displayCode(code)}`}
                        className="min-h-9 px-3 py-1.5 text-xs"
                        onClick={() => setSelectedId(code.id)}
                        type="button"
                        variant="secondary"
                      >
                        <BarChart3 className="h-4 w-4" aria-hidden="true" />
                        Stats
                      </Button>
                      <Button
                        aria-label={`${code.isActive ? "Disable" : "Enable"} ${displayCode(code)}`}
                        className="min-h-9 px-3 py-1.5 text-xs"
                        disabled={updatingId === code.id}
                        onClick={() => setCodeActive(code, !code.isActive)}
                        type="button"
                        variant={code.isActive ? "secondary" : "primary"}
                      >
                        {updatingId === code.id ? (
                          <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
                        ) : code.isActive ? (
                          <Ban className="h-4 w-4" aria-hidden="true" />
                        ) : (
                          <CheckCircle2 className="h-4 w-4" aria-hidden="true" />
                        )}
                        {code.isActive ? "Disable" : "Enable"}
                      </Button>
                    </div>
                  </article>
                ))}
              </div>
            )}
          </section>
        </aside>

        <div className="space-y-6">
          <section className="rounded-lg border border-line bg-white/80 p-5 shadow-crisp">
            <div className="flex flex-wrap items-center justify-between gap-4">
              <div>
                <h2 className="text-lg font-semibold text-ink">Selected code</h2>
                <p className="mt-1 text-sm text-ink/60">
                  {selectedCode
                    ? `${displayCode(selectedCode)} · ${selectedCode.redemptionCount} redemptions`
                    : "No code selected."}
                </p>
              </div>
              {selectedCode ? (
                <span
                  className={`rounded-full border px-3 py-1 text-xs font-semibold ${statusClasses[selectedCode.status]}`}
                >
                  {statusLabel(selectedCode.status)}
                </span>
              ) : null}
            </div>
          </section>

          <PromoStatsPanel
            detail={detail}
            error={detailError}
            loading={detailLoading}
          />
        </div>
      </div>
    </main>
  );
}
