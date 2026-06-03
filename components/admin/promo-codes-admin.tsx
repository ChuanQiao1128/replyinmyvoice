"use client";

import {
  Activity,
  Archive,
  ArchiveRestore,
  ArrowLeft,
  Ban,
  BarChart3,
  CheckCircle2,
  Copy,
  Eye,
  EyeOff,
  Loader2,
  Pencil,
  Plus,
  RefreshCw,
  Save,
  Ticket,
  Users,
  X,
} from "lucide-react";
import Link from "next/link";
import type { ReactNode } from "react";
import { FormEvent, useCallback, useEffect, useMemo, useState } from "react";

import {
  adminPromoCodesFromPayload,
  adminPromoDetailFromPayload,
  derivePromoCodeStatus,
  editFormValuesFromCode,
  fieldErrorsFromAdminError,
  type AdminPromoCode,
  type AdminPromoCreateFieldErrors,
  type AdminPromoCreateFormValues,
  type AdminPromoDetail,
  type AdminPromoEditFieldErrors,
  type AdminPromoEditFormValues,
  type AdminPromoStatus,
  validatePromoCreateForm,
  validatePromoEditForm,
} from "../../lib/admin-promo-codes";
import { Button } from "../ui/button";
import { Input } from "../ui/input";

type PromoCodesAdminProps = {
  initialCodes: AdminPromoCode[];
  initialError?: string;
};

const statusClasses: Record<AdminPromoStatus, string> = {
  active: "border-clay/25 bg-mint text-clay",
  archived: "border-line bg-paper-deep text-ink/45",
  disabled: "border-line bg-paper-deep text-ink/70",
  exhausted: "border-rust/25 bg-rust/10 text-rust",
  expired: "border-line bg-white text-ink/60",
  pending: "border-gold/25 bg-gold/10 text-gold",
};

const statusLegendItems: Array<{
  description: string;
  status: AdminPromoStatus;
}> = [
  { description: "redeemable now", status: "active" },
  {
    description: "not yet active (valid-from is in the future)",
    status: "pending",
  },
  { description: "past valid-until", status: "expired" },
  { description: "global cap reached", status: "exhausted" },
  { description: "turned off by an admin", status: "disabled" },
  { description: "soft-deleted and hidden (can be restored).", status: "archived" },
];

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

function fieldErrorProps(error: string | undefined, errorId: string) {
  if (!error) {
    return {};
  }

  return {
    "aria-describedby": errorId,
    "aria-invalid": true,
  } as const;
}

function FieldError({ children, id }: { children?: string; id: string }) {
  if (!children) {
    return null;
  }

  return (
    <p className="mt-1 text-xs font-medium text-rust" id={id}>
      {children}
    </p>
  );
}

function StatTile({
  icon,
  label,
  testId,
  value,
}: {
  icon: ReactNode;
  label: string;
  testId: string;
  value: string;
}) {
  return (
    <div className="rounded-lg border border-line bg-white px-4 py-3">
      <div className="mb-2 flex items-center gap-2 text-xs font-semibold uppercase text-ink/55">
        {icon}
        {label}
      </div>
      <div className="text-xl font-semibold text-ink" data-testid={testId}>
        {value}
      </div>
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
  const activation = `${Math.round(detail.stats.activationRate * 100)}%`;

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
          testId="promo-stat-redemptions"
          value={`${detail.stats.totalRedemptions}`}
        />
        <StatTile
          icon={<Users className="h-4 w-4" aria-hidden="true" />}
          label="Distinct users"
          testId="promo-stat-users"
          value={`${detail.stats.distinctUsers}`}
        />
        <StatTile
          icon={<BarChart3 className="h-4 w-4" aria-hidden="true" />}
          label="Activation"
          testId="promo-stat-activation"
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
  const [formSuccessCode, setFormSuccessCode] = useState("");
  const [creating, setCreating] = useState(false);
  const [updatingId, setUpdatingId] = useState<string | null>(null);
  const [showArchived, setShowArchived] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editValues, setEditValues] = useState<AdminPromoEditFormValues | null>(null);
  const [editFieldErrors, setEditFieldErrors] = useState<AdminPromoEditFieldErrors>(
    {},
  );
  const [editError, setEditError] = useState("");
  const [editSaving, setEditSaving] = useState(false);
  const [cardErrors, setCardErrors] = useState<Record<string, string>>({});
  const [confirmArchiveId, setConfirmArchiveId] = useState<string | null>(null);
  const [copiedKey, setCopiedKey] = useState<string | null>(null);

  const visibleCodes = useMemo(
    () =>
      showArchived
        ? codes
        : codes.filter((code) => code.status !== "archived"),
    [codes, showArchived],
  );
  const archivedCount = useMemo(
    () => codes.filter((code) => code.status === "archived").length,
    [codes],
  );

  const loadDetail = useCallback(async (id: string, isCurrent: () => boolean = () => true) => {
    setDetailLoading(true);
    setDetailError("");
    try {
      const response = await fetch(`/api/admin/promo-codes/${id}`, {
        cache: "no-store",
      });
      const payload = await readJsonPayload(response);
      if (!response.ok) {
        throw new Error("Could not load promo code stats.");
      }

      const nextDetail = adminPromoDetailFromPayload(payload);
      if (!nextDetail) {
        throw new Error("Promo code stats were invalid.");
      }

      if (isCurrent()) {
        setDetail(nextDetail);
      }
    } catch (error: unknown) {
      if (isCurrent()) {
        setDetail(null);
        setDetailError(
          error instanceof Error ? error.message : "Could not load promo code stats.",
        );
      }
    } finally {
      if (isCurrent()) {
        setDetailLoading(false);
      }
    }
  }, []);

  useEffect(() => {
    if (!selectedId) {
      setDetail(null);
      return;
    }

    let active = true;
    void loadDetail(selectedId, () => active);
    return () => {
      active = false;
    };
  }, [loadDetail, selectedId]);

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
      const nextSelectedId =
        selectedId && nextCodes.some((code) => code.id === selectedId)
          ? selectedId
          : nextCodes[0]?.id ?? null;
      setCodes(nextCodes);
      setSelectedId(nextSelectedId);
      if (selectedId && nextSelectedId === selectedId) {
        await loadDetail(selectedId);
      } else if (!nextSelectedId) {
        setDetail(null);
      }
    } catch (error) {
      setListError(
        error instanceof Error ? error.message : "Could not load promo codes.",
      );
    } finally {
      setListLoading(false);
    }
  }

  function clearCardError(id: string) {
    setCardErrors((current) => {
      if (!current[id]) {
        return current;
      }

      const next = { ...current };
      delete next[id];
      return next;
    });
  }

  function setCardError(id: string, message: string) {
    setCardErrors((current) => ({ ...current, [id]: message }));
  }

  async function copyCodeValue(value: string, key: string) {
    if (
      typeof navigator === "undefined" ||
      !navigator.clipboard ||
      typeof navigator.clipboard.writeText !== "function"
    ) {
      return;
    }

    try {
      await navigator.clipboard.writeText(value);
      setCopiedKey(key);
      window.setTimeout(() => {
        setCopiedKey((current) => (current === key ? null : current));
      }, 1500);
    } catch {
      // Clipboard access can be unavailable in locked-down browsers.
    }
  }

  function updateField(field: keyof AdminPromoCreateFormValues, value: string) {
    setFormValues((current) => ({ ...current, [field]: value }));
    setFieldErrors((current) => ({ ...current, [field]: undefined }));
    setFormError("");
    setFormSuccess("");
    setFormSuccessCode("");
  }

  async function createCode(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const validation = validatePromoCreateForm(formValues);
    if (!validation.ok) {
      setFieldErrors(validation.fieldErrors);
      setFormError("Fix the highlighted fields and try again.");
      setFormSuccess("");
      setFormSuccessCode("");
      return;
    }

    setCreating(true);
    setFieldErrors({});
    setFormError("");
    setFormSuccess("");
    setFormSuccessCode("");

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
      const nextDisplayCode = displayCode(nextCode);
      setFormSuccess(`${nextDisplayCode} created.`);
      setFormSuccessCode(nextDisplayCode);
      void copyCodeValue(nextDisplayCode, `created:${nextDisplayCode}`);
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
      clearCardError(nextCode.id);
      if (selectedId === nextCode.id && detail) {
        setDetail({ ...detail, promoCode: nextCode });
      }
    } catch (error) {
      const message =
        error instanceof Error
          ? error.message
          : `Could not ${action} ${displayCode(code)}.`;
      setCodes(previousCodes);
      setCardError(code.id, message);
    } finally {
      setUpdatingId(null);
    }
  }

  function startEdit(code: AdminPromoCode) {
    setEditingId(code.id);
    setEditValues(editFormValuesFromCode(code, toLocalDateTimeInput));
    setEditFieldErrors({});
    setEditError("");
  }

  function cancelEdit() {
    setEditingId(null);
    setEditValues(null);
    setEditFieldErrors({});
    setEditError("");
  }

  function updateEditField(field: keyof AdminPromoEditFormValues, value: string) {
    setEditValues((current) => (current ? { ...current, [field]: value } : current));
    setEditFieldErrors((current) => ({ ...current, [field]: undefined }));
    setEditError("");
  }

  async function submitEdit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!editingId || !editValues) {
      return;
    }

    const validation = validatePromoEditForm(editValues);
    if (!validation.ok) {
      setEditFieldErrors(validation.fieldErrors);
      setEditError("Fix the highlighted fields and try again.");
      return;
    }

    const targetId = editingId;
    setEditSaving(true);
    setEditFieldErrors({});
    setEditError("");

    try {
      const response = await fetch(`/api/admin/promo-codes/${targetId}`, {
        body: JSON.stringify(validation.payload),
        headers: {
          "Content-Type": "application/json",
        },
        method: "PATCH",
      });
      const payload = await readJsonPayload(response);
      if (!response.ok) {
        const detailMessage =
          payload &&
          typeof payload === "object" &&
          "detail" in payload &&
          typeof (payload as { detail?: unknown }).detail === "string"
            ? (payload as { detail: string }).detail
            : "Could not save those changes.";
        setEditError(detailMessage);
        return;
      }

      const nextCode = codeFromPayload(payload);
      if (!nextCode) {
        throw new Error("Update response was invalid.");
      }

      setCodes((current) => replaceCode(current, nextCode));
      if (selectedId === nextCode.id && detail) {
        setDetail({ ...detail, promoCode: nextCode });
      }
      cancelEdit();
    } catch (error) {
      setEditError(
        error instanceof Error ? error.message : "Could not save those changes.",
      );
    } finally {
      setEditSaving(false);
    }
  }

  async function archiveCode(code: AdminPromoCode) {
    const previousCodes = codes;
    const optimisticCode: AdminPromoCode = {
      ...code,
      archivedAt: new Date().toISOString(),
      isActive: false,
      status: "archived",
    };

    if (editingId === code.id) {
      cancelEdit();
    }
    setConfirmArchiveId(null);
    setUpdatingId(code.id);
    setListError("");
    setCodes((current) => replaceCode(current, optimisticCode));

    try {
      const response = await fetch(`/api/admin/promo-codes/${code.id}/archive`, {
        method: "POST",
      });
      const payload = await readJsonPayload(response);
      if (!response.ok) {
        throw new Error(`Could not archive ${displayCode(code)}.`);
      }

      const nextCode = codeFromPayload(payload);
      if (!nextCode) {
        throw new Error("Archive response was invalid.");
      }

      setCodes((current) => replaceCode(current, nextCode));
      clearCardError(nextCode.id);
      if (selectedId === nextCode.id && detail) {
        setDetail({ ...detail, promoCode: nextCode });
      }
    } catch (error) {
      const message =
        error instanceof Error ? error.message : `Could not archive ${displayCode(code)}.`;
      setCodes(previousCodes);
      setCardError(code.id, message);
    } finally {
      setUpdatingId(null);
    }
  }

  async function restoreCode(code: AdminPromoCode) {
    const previousCodes = codes;
    const restored = { ...code, archivedAt: null };
    const optimisticCode: AdminPromoCode = {
      ...restored,
      status: derivePromoCodeStatus(restored),
    };

    setUpdatingId(code.id);
    setListError("");
    setCodes((current) => replaceCode(current, optimisticCode));

    try {
      const response = await fetch(`/api/admin/promo-codes/${code.id}/restore`, {
        method: "POST",
      });
      const payload = await readJsonPayload(response);
      if (!response.ok) {
        throw new Error(`Could not restore ${displayCode(code)}.`);
      }

      const nextCode = codeFromPayload(payload);
      if (!nextCode) {
        throw new Error("Restore response was invalid.");
      }

      setCodes((current) => replaceCode(current, nextCode));
      clearCardError(nextCode.id);
      if (selectedId === nextCode.id && detail) {
        setDetail({ ...detail, promoCode: nextCode });
      }
    } catch (error) {
      const message =
        error instanceof Error ? error.message : `Could not restore ${displayCode(code)}.`;
      setCodes(previousCodes);
      setCardError(code.id, message);
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
                <RefreshCw className="h-4 w-4" aria-hidden="true" />
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
                  {...fieldErrorProps(fieldErrors.code, "promo-code-error")}
                  autoComplete="off"
                  id="promo-code"
                  onChange={(event) => updateField("code", event.target.value)}
                  placeholder="SPRING2026"
                  value={formValues.code}
                />
                <FieldError id="promo-code-error">{fieldErrors.code}</FieldError>
              </div>

              <div>
                <label
                  className="text-sm font-semibold text-ink"
                  htmlFor="promo-display-code"
                >
                  Display code
                </label>
                <Input
                  {...fieldErrorProps(
                    fieldErrors.displayCode,
                    "promo-display-code-error",
                  )}
                  autoComplete="off"
                  id="promo-display-code"
                  onChange={(event) => updateField("displayCode", event.target.value)}
                  placeholder="SPRING-2026"
                  value={formValues.displayCode}
                />
                <FieldError id="promo-display-code-error">
                  {fieldErrors.displayCode}
                </FieldError>
                <p className="mt-1 text-xs text-ink/55">
                  Display code is the same code with optional spacing/hyphens for
                  sharing.
                </p>
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
                    {...fieldErrorProps(fieldErrors.credits, "promo-credits-error")}
                    id="promo-credits"
                    inputMode="numeric"
                    onChange={(event) => updateField("credits", event.target.value)}
                    value={formValues.credits}
                  />
                  <FieldError id="promo-credits-error">{fieldErrors.credits}</FieldError>
                </div>
                <div>
                  <label className="text-sm font-semibold text-ink" htmlFor="promo-ttl">
                    TTL days
                  </label>
                  <Input
                    {...fieldErrorProps(fieldErrors.ttlDays, "promo-ttl-error")}
                    id="promo-ttl"
                    inputMode="numeric"
                    onChange={(event) => updateField("ttlDays", event.target.value)}
                    value={formValues.ttlDays}
                  />
                  <FieldError id="promo-ttl-error">{fieldErrors.ttlDays}</FieldError>
                  <p className="mt-1 text-xs text-ink/55">
                    Days a redeemed code&apos;s credits stay valid.
                  </p>
                </div>
              </div>

              <div>
                <label className="text-sm font-semibold text-ink" htmlFor="promo-from">
                  Valid from
                </label>
                <Input
                  {...fieldErrorProps(fieldErrors.validFrom, "promo-from-error")}
                  id="promo-from"
                  onChange={(event) => updateField("validFrom", event.target.value)}
                  type="datetime-local"
                  value={formValues.validFrom}
                />
                <FieldError id="promo-from-error">{fieldErrors.validFrom}</FieldError>
              </div>

              <div>
                <label className="text-sm font-semibold text-ink" htmlFor="promo-until">
                  Valid until
                </label>
                <Input
                  {...fieldErrorProps(fieldErrors.validUntil, "promo-until-error")}
                  id="promo-until"
                  onChange={(event) => updateField("validUntil", event.target.value)}
                  type="datetime-local"
                  value={formValues.validUntil}
                />
                <FieldError id="promo-until-error">{fieldErrors.validUntil}</FieldError>
                <p className="mt-1 text-xs text-ink/55">
                  When the code stops being redeemable.
                </p>
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
                    {...fieldErrorProps(fieldErrors.globalCap, "promo-global-cap-error")}
                    id="promo-global-cap"
                    inputMode="numeric"
                    onChange={(event) => updateField("globalCap", event.target.value)}
                    placeholder="Unlimited"
                    value={formValues.globalCap}
                  />
                  <FieldError id="promo-global-cap-error">
                    {fieldErrors.globalCap}
                  </FieldError>
                </div>
                <div>
                  <label
                    className="text-sm font-semibold text-ink"
                    htmlFor="promo-per-user-cap"
                  >
                    Per-user cap
                  </label>
                  <Input
                    {...fieldErrorProps(
                      fieldErrors.perUserCap,
                      "promo-per-user-cap-error",
                    )}
                    id="promo-per-user-cap"
                    inputMode="numeric"
                    onChange={(event) => updateField("perUserCap", event.target.value)}
                    value={formValues.perUserCap}
                  />
                  <FieldError id="promo-per-user-cap-error">
                    {fieldErrors.perUserCap}
                  </FieldError>
                </div>
              </div>

              {formError ? (
                <p className="rounded-md border border-rust/25 bg-rust/10 px-3 py-2 text-sm font-medium text-rust">
                  {formError}
                </p>
              ) : null}
              {formSuccess ? (
                <div className="flex items-center justify-between gap-2 rounded-md border border-clay/25 bg-mint px-3 py-2 text-sm font-medium text-clay">
                  <span>{formSuccess}</span>
                  {formSuccessCode ? (
                    <Button
                      aria-label={`Copy code ${formSuccessCode}`}
                      className="min-h-8 shrink-0 px-2 py-1 text-xs"
                      onClick={() =>
                        copyCodeValue(formSuccessCode, `created:${formSuccessCode}`)
                      }
                      type="button"
                      variant="secondary"
                    >
                      {copiedKey === `created:${formSuccessCode}` ? (
                        <CheckCircle2 className="h-4 w-4" aria-hidden="true" />
                      ) : (
                        <Copy className="h-4 w-4" aria-hidden="true" />
                      )}
                      {copiedKey === `created:${formSuccessCode}` ? "Copied" : "Copy"}
                    </Button>
                  ) : null}
                </div>
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
            <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
              <div className="flex items-center gap-2">
                <Ticket className="h-5 w-5 text-clay" aria-hidden="true" />
                <h2 className="text-lg font-semibold text-ink">Codes</h2>
              </div>
              <div className="flex items-center gap-3">
                {archivedCount > 0 ? (
                  <button
                    aria-pressed={showArchived}
                    className="inline-flex items-center gap-1.5 text-xs font-semibold text-clay transition hover:text-ink focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-clay/35 focus-visible:ring-offset-2 focus-visible:ring-offset-paper"
                    onClick={() => setShowArchived((value) => !value)}
                    type="button"
                  >
                    {showArchived ? (
                      <EyeOff className="h-4 w-4" aria-hidden="true" />
                    ) : (
                      <Eye className="h-4 w-4" aria-hidden="true" />
                    )}
                    {showArchived ? "Hide archived" : `Show archived (${archivedCount})`}
                  </button>
                ) : null}
                <span className="text-sm text-ink/55">{visibleCodes.length} shown</span>
              </div>
            </div>
            <div
              aria-label="Promo code status legend"
              className="mb-4 space-y-2 text-xs text-ink/60"
            >
              {statusLegendItems.map((item) => (
                <div className="flex items-center gap-2" key={item.status}>
                  <span
                    className={`min-w-20 rounded-full border px-2 py-0.5 text-center font-semibold ${statusClasses[item.status]}`}
                  >
                    {statusLabel(item.status)}
                  </span>
                  <span>{item.description}</span>
                </div>
              ))}
            </div>

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
            ) : visibleCodes.length === 0 ? (
              <p className="rounded-lg border border-dashed border-line bg-paper/60 px-4 py-6 text-sm text-ink/60">
                Every code is archived. Use Show archived to view and restore them.
              </p>
            ) : (
              <div className="space-y-3">
                {visibleCodes.map((code) => {
                  const codeDisplay = displayCode(code);
                  const remaining =
                    code.maxRedemptionsGlobal === null
                      ? null
                      : Math.max(0, code.maxRedemptionsGlobal - code.redemptionCount);
                  const isArchived = code.status === "archived";
                  const isBusy = updatingId === code.id;
                  const isEditing = editingId === code.id;
                  const isConfirmingArchive = confirmArchiveId === code.id;
                  const cardCopyKey = `card:${code.id}`;
                  const isCopied = copiedKey === cardCopyKey;
                  const cardError = cardErrors[code.id];

                  return (
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
                            {codeDisplay}
                          </h3>
                          <p className="mt-1 text-xs text-ink/55">
                            {code.creditsGranted} credits · {code.grantTtlDays}-day TTL ·{" "}
                            {code.maxRedemptionsPerUser} per user
                          </p>
                        </div>
                        <div className="flex shrink-0 flex-col items-end gap-1">
                          <div className="flex items-center gap-2">
                            <Button
                              aria-label={`Copy code ${codeDisplay}`}
                              className="min-h-8 min-w-8 px-2 py-1 text-xs"
                              onClick={() => copyCodeValue(codeDisplay, cardCopyKey)}
                              type="button"
                              variant="secondary"
                            >
                              {isCopied ? (
                                <CheckCircle2 className="h-4 w-4" aria-hidden="true" />
                              ) : (
                                <Copy className="h-4 w-4" aria-hidden="true" />
                              )}
                            </Button>
                            <span
                              className={`rounded-full border px-2.5 py-1 text-xs font-semibold ${statusClasses[code.status]}`}
                            >
                              {statusLabel(code.status)}
                            </span>
                          </div>
                          {isCopied ? (
                            <span className="text-xs font-semibold text-clay">Copied</span>
                          ) : null}
                        </div>
                      </div>

                      <dl className="mt-3 grid grid-cols-2 gap-x-4 gap-y-2 text-xs text-ink/60">
                        <div>
                          <dt className="font-semibold text-ink/45">Redeemed</dt>
                          <dd>
                            {code.redemptionCount}
                            {code.maxRedemptionsGlobal === null
                              ? ""
                              : ` / ${code.maxRedemptionsGlobal}`}
                          </dd>
                        </div>
                        <div>
                          <dt className="font-semibold text-ink/45">Remaining</dt>
                          <dd>{remaining === null ? "Unlimited" : remaining}</dd>
                        </div>
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
                          aria-label={`View stats for ${codeDisplay}`}
                          className="min-h-9 px-3 py-1.5 text-xs"
                          onClick={() => setSelectedId(code.id)}
                          type="button"
                          variant="secondary"
                        >
                          <BarChart3 className="h-4 w-4" aria-hidden="true" />
                          Stats
                        </Button>
                        {isArchived ? (
                          <Button
                            aria-label={`Restore ${codeDisplay}`}
                            className="min-h-9 px-3 py-1.5 text-xs"
                            disabled={isBusy}
                            onClick={() => restoreCode(code)}
                            type="button"
                            variant="primary"
                          >
                            {isBusy ? (
                              <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
                            ) : (
                              <ArchiveRestore className="h-4 w-4" aria-hidden="true" />
                            )}
                            Restore
                          </Button>
                        ) : (
                          <>
                            <Button
                              aria-label={`Edit ${codeDisplay}`}
                              className="min-h-9 px-3 py-1.5 text-xs"
                              onClick={() => (isEditing ? cancelEdit() : startEdit(code))}
                              type="button"
                              variant={isEditing ? "primary" : "secondary"}
                            >
                              <Pencil className="h-4 w-4" aria-hidden="true" />
                              Edit
                            </Button>
                            <Button
                              aria-label={`${code.isActive ? "Disable" : "Enable"} ${codeDisplay}`}
                              className="min-h-9 px-3 py-1.5 text-xs"
                              disabled={isBusy}
                              onClick={() => setCodeActive(code, !code.isActive)}
                              type="button"
                              variant={code.isActive ? "secondary" : "primary"}
                            >
                              {isBusy ? (
                                <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
                              ) : code.isActive ? (
                                <Ban className="h-4 w-4" aria-hidden="true" />
                              ) : (
                                <CheckCircle2 className="h-4 w-4" aria-hidden="true" />
                              )}
                              {code.isActive ? "Disable" : "Enable"}
                            </Button>
                            {isConfirmingArchive ? (
                              <div className="col-span-2 grid grid-cols-[1fr_auto] gap-2">
                                <Button
                                  aria-label={`Archive ${codeDisplay}`}
                                  className="min-h-9 px-3 py-1.5 text-xs"
                                  disabled={isBusy}
                                  onClick={() => archiveCode(code)}
                                  type="button"
                                  variant="secondary"
                                >
                                  {isBusy ? (
                                    <Loader2
                                      className="h-4 w-4 animate-spin"
                                      aria-hidden="true"
                                    />
                                  ) : (
                                    <Archive className="h-4 w-4" aria-hidden="true" />
                                  )}
                                  Archive?
                                </Button>
                                <Button
                                  aria-label={`Cancel archive ${codeDisplay}`}
                                  className="min-h-9 px-3 py-1.5 text-xs"
                                  disabled={isBusy}
                                  onClick={() => setConfirmArchiveId(null)}
                                  type="button"
                                  variant="ghost"
                                >
                                  Cancel
                                </Button>
                              </div>
                            ) : (
                              <Button
                                aria-label={`Archive ${codeDisplay}`}
                                className="col-span-2 min-h-9 px-3 py-1.5 text-xs"
                                disabled={isBusy}
                                onClick={() => setConfirmArchiveId(code.id)}
                                type="button"
                                variant="secondary"
                              >
                                <Archive className="h-4 w-4" aria-hidden="true" />
                                Archive
                              </Button>
                            )}
                          </>
                        )}
                      </div>

                      {cardError ? (
                        <p className="mt-3 rounded-md border border-rust/25 bg-rust/10 px-3 py-2 text-xs font-medium text-rust">
                          {cardError}
                        </p>
                      ) : null}

                      {isEditing && editValues ? (
                        <form
                          className="mt-4 space-y-3 rounded-lg border border-clay/25 bg-paper/60 p-4"
                          onSubmit={submitEdit}
                        >
                          <p className="text-xs font-semibold uppercase text-clay">
                            Edit code
                          </p>
                          <div className="grid grid-cols-2 gap-3">
                            <div>
                              <label
                                className="text-xs font-semibold text-ink"
                                htmlFor="promo-edit-credits"
                              >
                                Credits
                              </label>
                              <Input
                                {...fieldErrorProps(
                                  editFieldErrors.credits,
                                  "promo-edit-credits-error",
                                )}
                                id="promo-edit-credits"
                                inputMode="numeric"
                                onChange={(event) =>
                                  updateEditField("credits", event.target.value)
                                }
                                value={editValues.credits}
                              />
                              <FieldError id="promo-edit-credits-error">
                                {editFieldErrors.credits}
                              </FieldError>
                            </div>
                            <div>
                              <label
                                className="text-xs font-semibold text-ink"
                                htmlFor="promo-edit-ttl"
                              >
                                TTL days
                              </label>
                              <Input
                                {...fieldErrorProps(
                                  editFieldErrors.ttlDays,
                                  "promo-edit-ttl-error",
                                )}
                                id="promo-edit-ttl"
                                inputMode="numeric"
                                onChange={(event) =>
                                  updateEditField("ttlDays", event.target.value)
                                }
                                value={editValues.ttlDays}
                              />
                              <FieldError id="promo-edit-ttl-error">
                                {editFieldErrors.ttlDays}
                              </FieldError>
                            </div>
                          </div>
                          <div>
                            <label
                              className="text-xs font-semibold text-ink"
                              htmlFor="promo-edit-from"
                            >
                              Valid from
                            </label>
                            <Input
                              {...fieldErrorProps(
                                editFieldErrors.validFrom,
                                "promo-edit-from-error",
                              )}
                              id="promo-edit-from"
                              onChange={(event) =>
                                updateEditField("validFrom", event.target.value)
                              }
                              type="datetime-local"
                              value={editValues.validFrom}
                            />
                            <FieldError id="promo-edit-from-error">
                              {editFieldErrors.validFrom}
                            </FieldError>
                          </div>
                          <div>
                            <label
                              className="text-xs font-semibold text-ink"
                              htmlFor="promo-edit-until"
                            >
                              Valid until
                            </label>
                            <Input
                              {...fieldErrorProps(
                                editFieldErrors.validUntil,
                                "promo-edit-until-error",
                              )}
                              id="promo-edit-until"
                              onChange={(event) =>
                                updateEditField("validUntil", event.target.value)
                              }
                              type="datetime-local"
                              value={editValues.validUntil}
                            />
                            <FieldError id="promo-edit-until-error">
                              {editFieldErrors.validUntil}
                            </FieldError>
                          </div>
                          <div className="grid grid-cols-2 gap-3">
                            <div>
                              <label
                                className="text-xs font-semibold text-ink"
                                htmlFor="promo-edit-global-cap"
                              >
                                Global cap
                              </label>
                              <Input
                                {...fieldErrorProps(
                                  editFieldErrors.globalCap,
                                  "promo-edit-global-cap-error",
                                )}
                                id="promo-edit-global-cap"
                                inputMode="numeric"
                                onChange={(event) =>
                                  updateEditField("globalCap", event.target.value)
                                }
                                placeholder="Unlimited"
                                value={editValues.globalCap}
                              />
                              <FieldError id="promo-edit-global-cap-error">
                                {editFieldErrors.globalCap}
                              </FieldError>
                            </div>
                            <div>
                              <label
                                className="text-xs font-semibold text-ink"
                                htmlFor="promo-edit-per-user-cap"
                              >
                                Per-user cap
                              </label>
                              <Input
                                {...fieldErrorProps(
                                  editFieldErrors.perUserCap,
                                  "promo-edit-per-user-cap-error",
                                )}
                                id="promo-edit-per-user-cap"
                                inputMode="numeric"
                                onChange={(event) =>
                                  updateEditField("perUserCap", event.target.value)
                                }
                                value={editValues.perUserCap}
                              />
                              <FieldError id="promo-edit-per-user-cap-error">
                                {editFieldErrors.perUserCap}
                              </FieldError>
                            </div>
                          </div>
                          <div>
                            <label
                              className="text-xs font-semibold text-ink"
                              htmlFor="promo-edit-description"
                            >
                              Description
                            </label>
                            <Input
                              {...fieldErrorProps(
                                editFieldErrors.description,
                                "promo-edit-description-error",
                              )}
                              id="promo-edit-description"
                              onChange={(event) =>
                                updateEditField("description", event.target.value)
                              }
                              placeholder="Optional internal note"
                              value={editValues.description}
                            />
                            <FieldError id="promo-edit-description-error">
                              {editFieldErrors.description}
                            </FieldError>
                          </div>

                          {editError ? (
                            <p className="rounded-md border border-rust/25 bg-rust/10 px-3 py-2 text-xs font-medium text-rust">
                              {editError}
                            </p>
                          ) : null}

                          <div className="flex gap-2">
                            <Button
                              className="min-h-9 flex-1 px-3 py-1.5 text-xs"
                              disabled={editSaving}
                              type="submit"
                            >
                              {editSaving ? (
                                <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
                              ) : (
                                <Save className="h-4 w-4" aria-hidden="true" />
                              )}
                              Save changes
                            </Button>
                            <Button
                              className="min-h-9 px-3 py-1.5 text-xs"
                              onClick={cancelEdit}
                              type="button"
                              variant="secondary"
                            >
                              <X className="h-4 w-4" aria-hidden="true" />
                              Cancel
                            </Button>
                          </div>
                        </form>
                      ) : null}
                    </article>
                  );
                })}
              </div>
            )}
          </section>
        </aside>

        <div className="space-y-6">
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
