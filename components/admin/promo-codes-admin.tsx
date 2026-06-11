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
  Download,
  Eye,
  EyeOff,
  Loader2,
  Pencil,
  Plus,
  RefreshCw,
  Save,
  Search,
  Ticket,
  Users,
  X,
} from "lucide-react";
import Link from "next/link";
import type { ReactNode, RefObject } from "react";
import { FormEvent, useCallback, useEffect, useMemo, useRef, useState } from "react";

import {
  adminPromoCodesFromPayload,
  adminPromoDetailFromPayload,
  derivePromoCodeStatus,
  editFormValuesFromCode,
  fieldErrorsFromAdminError,
  normalizePromoCode,
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
import { serializeCsv } from "../../lib/csv-export";
import { Button } from "../ui/button";
import { Input } from "../ui/input";
import { Textarea } from "../ui/textarea";

type PromoCodesAdminProps = {
  initialCodes: AdminPromoCode[];
  initialError?: string;
};

type PromoStatusFilter = "all" | AdminPromoStatus;
type PromoSortOrder = "newest" | "most-redeemed";
type BulkActionState = "creating" | "disabling" | "exporting" | null;

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

export function initialFormValues(): AdminPromoCreateFormValues {
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

function initialBulkFormValues(): AdminPromoCreateFormValues {
  return {
    ...initialFormValues(),
    code: "",
    displayCode: "",
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

function dateTimeValue(value: string) {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? 0 : date.getTime();
}

function pluralize(count: number, singular: string, plural = `${singular}s`) {
  return `${count} ${count === 1 ? singular : plural}`;
}

function parsedBulkCodes(value: string) {
  return value
    .split(/[\n,]+/)
    .map((code) => code.trim())
    .filter(Boolean);
}

function downloadCsv(csv: string, filename: string) {
  const blob = new Blob([csv], { type: "text/csv;charset=utf-8" });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = filename;
  link.style.display = "none";
  document.body.appendChild(link);
  try {
    link.click();
  } finally {
    link.remove();
    URL.revokeObjectURL(url);
  }
}

function promoCodeCsvRows(codes: AdminPromoCode[]) {
  return codes.map((code) => {
    const remaining = code.maxRedemptionsGlobal === null
      ? "unlimited"
      : Math.max(0, code.maxRedemptionsGlobal - code.redemptionCount);

    return {
      archivedAt: code.archivedAt,
      code: code.code,
      createdAt: code.createdAt,
      creditsGranted: code.creditsGranted,
      displayCode: code.displayCode,
      grantTtlDays: code.grantTtlDays,
      isActive: code.isActive,
      maxRedemptionsGlobal: code.maxRedemptionsGlobal,
      maxRedemptionsPerUser: code.maxRedemptionsPerUser,
      redemptionCount: code.redemptionCount,
      remainingRedemptions: remaining,
      status: code.status,
      updatedAt: code.updatedAt,
      validFrom: code.validFrom,
      validUntil: code.validUntil,
    };
  });
}

function focusableElements(container: HTMLElement) {
  return Array.from(
    container.querySelectorAll<HTMLElement>(
      [
        "a[href]",
        "button:not([disabled])",
        "input:not([disabled])",
        "select:not([disabled])",
        "textarea:not([disabled])",
        '[tabindex]:not([tabindex="-1"])',
      ].join(", "),
    ),
  );
}

function useDialogFocus(
  isOpen: boolean,
  dialogRef: RefObject<HTMLElement | null>,
  onClose: () => void,
) {
  useEffect(() => {
    if (!isOpen || typeof document === "undefined") {
      return;
    }

    const dialog = dialogRef.current;
    const previouslyFocused = document.activeElement instanceof HTMLElement
      ? document.activeElement
      : null;

    window.setTimeout(() => {
      const firstFocusable = dialog ? focusableElements(dialog)[0] : null;
      (firstFocusable ?? dialog)?.focus();
    }, 0);

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") {
        event.preventDefault();
        onClose();
        return;
      }

      if (event.key !== "Tab" || !dialog) {
        return;
      }

      const focusable = focusableElements(dialog);
      if (focusable.length === 0) {
        event.preventDefault();
        dialog.focus();
        return;
      }

      const first = focusable[0];
      const last = focusable[focusable.length - 1];
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first.focus();
      }
    }

    document.addEventListener("keydown", handleKeyDown);
    return () => {
      document.removeEventListener("keydown", handleKeyDown);
      previouslyFocused?.focus();
    };
  }, [dialogRef, isOpen, onClose]);
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
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [detail, setDetail] = useState<AdminPromoDetail | null>(null);
  const [detailError, setDetailError] = useState("");
  const [detailLoading, setDetailLoading] = useState(false);
  const [createModalOpen, setCreateModalOpen] = useState(false);
  const [statsDrawerOpen, setStatsDrawerOpen] = useState(false);
  const [formValues, setFormValues] = useState<AdminPromoCreateFormValues>(() =>
    initialFormValues(),
  );
  const [fieldErrors, setFieldErrors] = useState<AdminPromoCreateFieldErrors>({});
  const [formError, setFormError] = useState("");
  const [formSuccess, setFormSuccess] = useState("");
  const [formSuccessCode, setFormSuccessCode] = useState("");
  const [creating, setCreating] = useState(false);
  const [bulkCreateModalOpen, setBulkCreateModalOpen] = useState(false);
  const [bulkCodes, setBulkCodes] = useState("");
  const [bulkValues, setBulkValues] = useState<AdminPromoCreateFormValues>(() =>
    initialBulkFormValues(),
  );
  const [bulkFieldErrors, setBulkFieldErrors] =
    useState<AdminPromoCreateFieldErrors>({});
  const [bulkError, setBulkError] = useState("");
  const [bulkNotice, setBulkNotice] = useState("");
  const [bulkAction, setBulkAction] = useState<BulkActionState>(null);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(() => new Set());
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
  const [searchTerm, setSearchTerm] = useState("");
  const [statusFilter, setStatusFilter] = useState<PromoStatusFilter>("all");
  const [sortOrder, setSortOrder] = useState<PromoSortOrder>("newest");
  const createDialogRef = useRef<HTMLDivElement>(null);
  const bulkCreateDialogRef = useRef<HTMLDivElement>(null);
  const editDialogRef = useRef<HTMLDivElement>(null);
  const statsDrawerRef = useRef<HTMLDivElement>(null);

  const baseVisibleCodes = useMemo(
    () =>
      showArchived
        ? codes
        : codes.filter((code) => code.status !== "archived"),
    [codes, showArchived],
  );
  const visibleCodes = useMemo(() => {
    const normalizedSearch = searchTerm.trim().toLowerCase();
    return [...baseVisibleCodes]
      .filter((code) => {
        if (statusFilter !== "all" && code.status !== statusFilter) {
          return false;
        }

        if (!normalizedSearch) {
          return true;
        }

        return [code.code, code.displayCode ?? ""].some((value) =>
          value.toLowerCase().includes(normalizedSearch),
        );
      })
      .sort((left, right) => {
        if (sortOrder === "most-redeemed") {
          const redeemed = right.redemptionCount - left.redemptionCount;
          return redeemed !== 0
            ? redeemed
            : dateTimeValue(right.createdAt) - dateTimeValue(left.createdAt);
        }

        return dateTimeValue(right.createdAt) - dateTimeValue(left.createdAt);
      });
  }, [baseVisibleCodes, searchTerm, sortOrder, statusFilter]);
  const selectedVisibleCodes = useMemo(
    () => visibleCodes.filter((code) => selectedIds.has(code.id)),
    [selectedIds, visibleCodes],
  );
  const selectedActiveCodes = useMemo(
    () =>
      selectedVisibleCodes.filter(
        (code) => code.isActive && code.status !== "archived",
      ),
    [selectedVisibleCodes],
  );
  const allVisibleSelected = visibleCodes.length > 0 &&
    visibleCodes.every((code) => selectedIds.has(code.id));
  const someVisibleSelected = visibleCodes.some((code) => selectedIds.has(code.id));
  const archivedCount = useMemo(
    () => codes.filter((code) => code.status === "archived").length,
    [codes],
  );
  const isEditModalOpen = Boolean(editingId && editValues);
  const editingCode = editingId
    ? codes.find((code) => code.id === editingId) ?? null
    : null;

  useEffect(() => {
    setSelectedIds((current) => {
      const knownIds = new Set(codes.map((code) => code.id));
      const next = new Set(
        Array.from(current).filter((id) => knownIds.has(id)),
      );
      return next.size === current.size ? current : next;
    });
  }, [codes]);

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
    if (!statsDrawerOpen || !selectedId) {
      if (!selectedId) {
        setDetail(null);
      }
      setDetail(null);
      return;
    }

    let active = true;
    void loadDetail(selectedId, () => active);
    return () => {
      active = false;
    };
  }, [loadDetail, selectedId, statsDrawerOpen]);

  async function refreshList(preferredSelectedId: string | null = selectedId) {
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
        preferredSelectedId && nextCodes.some((code) => code.id === preferredSelectedId)
          ? preferredSelectedId
          : null;
      setCodes(nextCodes);
      setSelectedId(nextSelectedId);
      if (statsDrawerOpen && nextSelectedId) {
        await loadDetail(nextSelectedId);
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

  const closeCreateModal = useCallback(() => {
    setCreateModalOpen(false);
    setFieldErrors({});
    setFormError("");
  }, []);

  const closeBulkCreateModal = useCallback(() => {
    if (bulkAction === "creating") {
      return;
    }

    setBulkCreateModalOpen(false);
    setBulkFieldErrors({});
    setBulkError("");
  }, [bulkAction]);

  const closeStatsDrawer = useCallback(() => {
    setStatsDrawerOpen(false);
    setDetailError("");
  }, []);

  function openCreateModal() {
    cancelEdit();
    setBulkCreateModalOpen(false);
    setStatsDrawerOpen(false);
    setFormValues((current) => (current.code.trim() ? current : initialFormValues()));
    setFieldErrors({});
    setFormError("");
    setCreateModalOpen(true);
  }

  function openBulkCreateModal() {
    cancelEdit();
    setCreateModalOpen(false);
    setStatsDrawerOpen(false);
    setBulkValues((current) => current);
    setBulkFieldErrors({});
    setBulkError("");
    setBulkNotice("");
    setBulkCreateModalOpen(true);
  }

  function openStatsDrawer(code: AdminPromoCode) {
    cancelEdit();
    setCreateModalOpen(false);
    setBulkCreateModalOpen(false);
    setSelectedId(code.id);
    setDetailError("");
    if (detail?.promoCode.id !== code.id) {
      setDetail(null);
    }
    setStatsDrawerOpen(true);
  }

  function updateField(field: keyof AdminPromoCreateFormValues, value: string) {
    setFormValues((current) => ({ ...current, [field]: value }));
    setFieldErrors((current) => ({ ...current, [field]: undefined }));
    setFormError("");
    setFormSuccess("");
    setFormSuccessCode("");
  }

  function updateBulkField(field: keyof AdminPromoCreateFormValues, value: string) {
    setBulkValues((current) => ({ ...current, [field]: value }));
    setBulkFieldErrors((current) => ({ ...current, [field]: undefined }));
    setBulkError("");
    setBulkNotice("");
  }

  function toggleCodeSelection(id: string) {
    setSelectedIds((current) => {
      const next = new Set(current);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  }

  function toggleVisibleSelection() {
    setSelectedIds((current) => {
      const next = new Set(current);
      if (allVisibleSelected) {
        visibleCodes.forEach((code) => next.delete(code.id));
      } else {
        visibleCodes.forEach((code) => next.add(code.id));
      }
      return next;
    });
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
      setCreateModalOpen(false);
      await refreshList(nextCode.id);
      void copyCodeValue(nextDisplayCode, `created:${nextDisplayCode}`);
    } catch (error) {
      setFormError(
        error instanceof Error ? error.message : "Could not create that promo code.",
      );
    } finally {
      setCreating(false);
    }
  }

  async function createBulkCodes(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const requestedCodes = parsedBulkCodes(bulkCodes);
    if (requestedCodes.length === 0) {
      setBulkError("Enter at least one promo code.");
      return;
    }

    const seen = new Set<string>();
    const validations = requestedCodes.map((rawCode) => {
      const normalized = normalizePromoCode(rawCode);
      if (!normalized) {
        return {
          error: `${rawCode} is not a valid code.`,
          payload: null,
          rawCode,
        };
      }

      if (seen.has(normalized)) {
        return {
          error: `${rawCode} is listed more than once.`,
          payload: null,
          rawCode,
        };
      }
      seen.add(normalized);

      const validation = validatePromoCreateForm({
        ...bulkValues,
        code: rawCode,
        displayCode: rawCode,
      });

      return validation.ok
        ? { error: null, payload: validation.payload, rawCode }
        : {
            error: Object.values(validation.fieldErrors)[0] ??
              `${rawCode} is not ready to create.`,
            payload: null,
            rawCode,
          };
    });
    const firstValidationError = validations.find((validation) => validation.error);
    if (firstValidationError) {
      setBulkError(firstValidationError.error ?? "Fix the bulk create fields.");
      setBulkFieldErrors({});
      return;
    }

    setBulkAction("creating");
    setBulkError("");
    setBulkNotice("");
    setBulkFieldErrors({});
    const createdCodes: AdminPromoCode[] = [];
    const failures: string[] = [];

    try {
      for (const validation of validations) {
        if (!validation.payload) {
          continue;
        }

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
          failures.push(
            `${validation.rawCode}: ${
              Object.values(nextFieldErrors)[0] ?? "could not be created"
            }`,
          );
          continue;
        }

        const nextCode = codeFromPayload(payload);
        if (!nextCode) {
          failures.push(`${validation.rawCode}: create response was invalid`);
          continue;
        }

        createdCodes.push(nextCode);
      }

      if (createdCodes.length > 0) {
        setCodes((current) => [
          ...createdCodes,
          ...current.filter(
            (code) => !createdCodes.some((created) => created.id === code.id),
          ),
        ]);
        setSelectedId(createdCodes[0]?.id ?? selectedId);
        await refreshList(createdCodes[0]?.id ?? selectedId);
      }

      if (failures.length > 0) {
        setBulkError(failures.slice(0, 3).join(" "));
      }

      if (createdCodes.length > 0) {
        setBulkNotice(
          `${pluralize(createdCodes.length, "code")} created${
            failures.length > 0 ? `, ${pluralize(failures.length, "failure")}.` : "."
          }`,
        );
      }

      if (createdCodes.length > 0 && failures.length === 0) {
        setBulkCodes("");
        setBulkValues(initialBulkFormValues());
        setBulkCreateModalOpen(false);
      }
    } finally {
      setBulkAction(null);
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

  async function bulkDisableSelected() {
    if (selectedActiveCodes.length === 0 || bulkAction) {
      setListError("Select at least one active promo code to disable.");
      return;
    }

    setBulkAction("disabling");
    setBulkNotice("");
    setListError("");
    const failures: string[] = [];
    let disabledCount = 0;

    try {
      for (const code of selectedActiveCodes) {
        setUpdatingId(code.id);
        const response = await fetch(`/api/admin/promo-codes/${code.id}/disable`, {
          method: "POST",
        });
        const payload = await readJsonPayload(response);
        if (!response.ok) {
          const message = `Could not disable ${displayCode(code)}.`;
          failures.push(message);
          setCardError(code.id, message);
          continue;
        }

        const nextCode = codeFromPayload(payload);
        if (!nextCode) {
          const message = `Disable response for ${displayCode(code)} was invalid.`;
          failures.push(message);
          setCardError(code.id, message);
          continue;
        }

        disabledCount += 1;
        clearCardError(nextCode.id);
        setCodes((current) => replaceCode(current, nextCode));
        setSelectedIds((current) => {
          const next = new Set(current);
          next.delete(nextCode.id);
          return next;
        });
        if (selectedId === nextCode.id && detail) {
          setDetail({ ...detail, promoCode: nextCode });
        }
      }

      if (disabledCount > 0) {
        setBulkNotice(`${pluralize(disabledCount, "code")} disabled.`);
      }
      if (failures.length > 0) {
        setListError(failures.slice(0, 3).join(" "));
      }
    } finally {
      setUpdatingId(null);
      setBulkAction(null);
    }
  }

  function exportVisibleCodesCsv() {
    if (visibleCodes.length === 0 || bulkAction) {
      setListError("There are no visible promo codes to export.");
      return;
    }

    setBulkAction("exporting");
    setListError("");
    setBulkNotice("");
    try {
      const columns = [
        "code",
        "displayCode",
        "status",
        "creditsGranted",
        "grantTtlDays",
        "redemptionCount",
        "remainingRedemptions",
        "maxRedemptionsGlobal",
        "maxRedemptionsPerUser",
        "validFrom",
        "validUntil",
        "isActive",
        "createdAt",
        "updatedAt",
        "archivedAt",
      ];
      const csv = serializeCsv(columns, promoCodeCsvRows(visibleCodes));
      downloadCsv(csv, "promo-codes.csv");
      setBulkNotice(`${pluralize(visibleCodes.length, "code")} exported.`);
    } catch (error) {
      setListError(error instanceof Error ? error.message : "Could not export CSV.");
    } finally {
      setBulkAction(null);
    }
  }

  function startEdit(code: AdminPromoCode) {
    setCreateModalOpen(false);
    setStatsDrawerOpen(false);
    setSelectedId(code.id);
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

  useDialogFocus(createModalOpen, createDialogRef, closeCreateModal);
  useDialogFocus(bulkCreateModalOpen, bulkCreateDialogRef, closeBulkCreateModal);
  useDialogFocus(isEditModalOpen, editDialogRef, cancelEdit);
  useDialogFocus(statsDrawerOpen, statsDrawerRef, closeStatsDrawer);

  function renderCreateForm() {
    return (
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
          <label className="text-sm font-semibold text-ink" htmlFor="promo-display-code">
            Display code
          </label>
          <Input
            {...fieldErrorProps(fieldErrors.displayCode, "promo-display-code-error")}
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
            Display code is the same code with optional spacing/hyphens for sharing.
          </p>
        </div>

        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className="text-sm font-semibold text-ink" htmlFor="promo-credits">
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
            <label className="text-sm font-semibold text-ink" htmlFor="promo-global-cap">
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
            <FieldError id="promo-global-cap-error">{fieldErrors.globalCap}</FieldError>
          </div>
          <div>
            <label
              className="text-sm font-semibold text-ink"
              htmlFor="promo-per-user-cap"
            >
              Per-user cap
            </label>
            <Input
              {...fieldErrorProps(fieldErrors.perUserCap, "promo-per-user-cap-error")}
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

        <Button className="w-full" disabled={creating} type="submit">
          {creating ? (
            <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
          ) : (
            <Plus className="h-4 w-4" aria-hidden="true" />
          )}
          Create code
        </Button>
      </form>
    );
  }

  function renderBulkCreateForm() {
    return (
      <form className="space-y-4" onSubmit={createBulkCodes}>
        <div>
          <label className="text-sm font-semibold text-ink" htmlFor="promo-bulk-codes">
            Codes
          </label>
          <Textarea
            className="mt-1 min-h-36 font-mono"
            id="promo-bulk-codes"
            onChange={(event) => {
              setBulkCodes(event.target.value);
              setBulkError("");
              setBulkNotice("");
            }}
            placeholder={"SPRING-2026\nSUMMER-2026\nTEACHERS-2026"}
            value={bulkCodes}
          />
          <p className="mt-1 text-xs text-ink/55">
            Enter one code per line or separate codes with commas.
          </p>
        </div>

        <div className="grid grid-cols-2 gap-3">
          <div>
            <label
              className="text-sm font-semibold text-ink"
              htmlFor="promo-bulk-credits"
            >
              Credits
            </label>
            <Input
              {...fieldErrorProps(bulkFieldErrors.credits, "promo-bulk-credits-error")}
              id="promo-bulk-credits"
              inputMode="numeric"
              onChange={(event) => updateBulkField("credits", event.target.value)}
              value={bulkValues.credits}
            />
            <FieldError id="promo-bulk-credits-error">
              {bulkFieldErrors.credits}
            </FieldError>
          </div>
          <div>
            <label className="text-sm font-semibold text-ink" htmlFor="promo-bulk-ttl">
              TTL days
            </label>
            <Input
              {...fieldErrorProps(bulkFieldErrors.ttlDays, "promo-bulk-ttl-error")}
              id="promo-bulk-ttl"
              inputMode="numeric"
              onChange={(event) => updateBulkField("ttlDays", event.target.value)}
              value={bulkValues.ttlDays}
            />
            <FieldError id="promo-bulk-ttl-error">
              {bulkFieldErrors.ttlDays}
            </FieldError>
          </div>
        </div>

        <div className="grid grid-cols-2 gap-3">
          <div>
            <label
              className="text-sm font-semibold text-ink"
              htmlFor="promo-bulk-global-cap"
            >
              Global cap
            </label>
            <Input
              {...fieldErrorProps(
                bulkFieldErrors.globalCap,
                "promo-bulk-global-cap-error",
              )}
              id="promo-bulk-global-cap"
              inputMode="numeric"
              onChange={(event) => updateBulkField("globalCap", event.target.value)}
              placeholder="Unlimited"
              value={bulkValues.globalCap}
            />
            <FieldError id="promo-bulk-global-cap-error">
              {bulkFieldErrors.globalCap}
            </FieldError>
          </div>
          <div>
            <label
              className="text-sm font-semibold text-ink"
              htmlFor="promo-bulk-per-user-cap"
            >
              Per-user cap
            </label>
            <Input
              {...fieldErrorProps(
                bulkFieldErrors.perUserCap,
                "promo-bulk-per-user-cap-error",
              )}
              id="promo-bulk-per-user-cap"
              inputMode="numeric"
              onChange={(event) => updateBulkField("perUserCap", event.target.value)}
              value={bulkValues.perUserCap}
            />
            <FieldError id="promo-bulk-per-user-cap-error">
              {bulkFieldErrors.perUserCap}
            </FieldError>
          </div>
        </div>

        <div className="grid gap-3 sm:grid-cols-2">
          <div>
            <label className="text-sm font-semibold text-ink" htmlFor="promo-bulk-from">
              Valid from
            </label>
            <Input
              {...fieldErrorProps(bulkFieldErrors.validFrom, "promo-bulk-from-error")}
              id="promo-bulk-from"
              onChange={(event) => updateBulkField("validFrom", event.target.value)}
              type="datetime-local"
              value={bulkValues.validFrom}
            />
            <FieldError id="promo-bulk-from-error">
              {bulkFieldErrors.validFrom}
            </FieldError>
          </div>
          <div>
            <label className="text-sm font-semibold text-ink" htmlFor="promo-bulk-until">
              Valid until
            </label>
            <Input
              {...fieldErrorProps(
                bulkFieldErrors.validUntil,
                "promo-bulk-until-error",
              )}
              id="promo-bulk-until"
              onChange={(event) => updateBulkField("validUntil", event.target.value)}
              type="datetime-local"
              value={bulkValues.validUntil}
            />
            <FieldError id="promo-bulk-until-error">
              {bulkFieldErrors.validUntil}
            </FieldError>
          </div>
        </div>

        {bulkError ? (
          <p className="rounded-md border border-rust/25 bg-rust/10 px-3 py-2 text-sm font-medium text-rust">
            {bulkError}
          </p>
        ) : null}

        <Button
          className="w-full"
          disabled={bulkAction === "creating"}
          type="submit"
        >
          {bulkAction === "creating" ? (
            <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
          ) : (
            <Plus className="h-4 w-4" aria-hidden="true" />
          )}
          {bulkAction === "creating" ? "Creating..." : "Create codes"}
        </Button>
      </form>
    );
  }

  function renderEditForm() {
    if (!editValues) {
      return null;
    }

    return (
      <form className="space-y-4" onSubmit={submitEdit}>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className="text-sm font-semibold text-ink" htmlFor="promo-edit-credits">
              Credits
            </label>
            <Input
              {...fieldErrorProps(editFieldErrors.credits, "promo-edit-credits-error")}
              id="promo-edit-credits"
              inputMode="numeric"
              onChange={(event) => updateEditField("credits", event.target.value)}
              value={editValues.credits}
            />
            <FieldError id="promo-edit-credits-error">
              {editFieldErrors.credits}
            </FieldError>
          </div>
          <div>
            <label className="text-sm font-semibold text-ink" htmlFor="promo-edit-ttl">
              TTL days
            </label>
            <Input
              {...fieldErrorProps(editFieldErrors.ttlDays, "promo-edit-ttl-error")}
              id="promo-edit-ttl"
              inputMode="numeric"
              onChange={(event) => updateEditField("ttlDays", event.target.value)}
              value={editValues.ttlDays}
            />
            <FieldError id="promo-edit-ttl-error">{editFieldErrors.ttlDays}</FieldError>
          </div>
        </div>

        <div>
          <label className="text-sm font-semibold text-ink" htmlFor="promo-edit-from">
            Valid from
          </label>
          <Input
            {...fieldErrorProps(editFieldErrors.validFrom, "promo-edit-from-error")}
            id="promo-edit-from"
            onChange={(event) => updateEditField("validFrom", event.target.value)}
            type="datetime-local"
            value={editValues.validFrom}
          />
          <FieldError id="promo-edit-from-error">{editFieldErrors.validFrom}</FieldError>
        </div>

        <div>
          <label className="text-sm font-semibold text-ink" htmlFor="promo-edit-until">
            Valid until
          </label>
          <Input
            {...fieldErrorProps(editFieldErrors.validUntil, "promo-edit-until-error")}
            id="promo-edit-until"
            onChange={(event) => updateEditField("validUntil", event.target.value)}
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
              className="text-sm font-semibold text-ink"
              htmlFor="promo-edit-global-cap"
            >
              Global cap
            </label>
            <Input
              {...fieldErrorProps(editFieldErrors.globalCap, "promo-edit-global-cap-error")}
              id="promo-edit-global-cap"
              inputMode="numeric"
              onChange={(event) => updateEditField("globalCap", event.target.value)}
              placeholder="Unlimited"
              value={editValues.globalCap}
            />
            <FieldError id="promo-edit-global-cap-error">
              {editFieldErrors.globalCap}
            </FieldError>
          </div>
          <div>
            <label
              className="text-sm font-semibold text-ink"
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
              onChange={(event) => updateEditField("perUserCap", event.target.value)}
              value={editValues.perUserCap}
            />
            <FieldError id="promo-edit-per-user-cap-error">
              {editFieldErrors.perUserCap}
            </FieldError>
          </div>
        </div>

        <div>
          <label
            className="text-sm font-semibold text-ink"
            htmlFor="promo-edit-description"
          >
            Description
          </label>
          <Input
            {...fieldErrorProps(editFieldErrors.description, "promo-edit-description-error")}
            id="promo-edit-description"
            onChange={(event) => updateEditField("description", event.target.value)}
            placeholder="Optional internal note"
            value={editValues.description}
          />
          <FieldError id="promo-edit-description-error">
            {editFieldErrors.description}
          </FieldError>
        </div>

        {editError ? (
          <p className="rounded-md border border-rust/25 bg-rust/10 px-3 py-2 text-sm font-medium text-rust">
            {editError}
          </p>
        ) : null}

        <div className="flex gap-2">
          <Button className="flex-1" disabled={editSaving} type="submit">
            {editSaving ? (
              <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
            ) : (
              <Save className="h-4 w-4" aria-hidden="true" />
            )}
            Save changes
          </Button>
          <Button onClick={cancelEdit} type="button" variant="secondary">
            <X className="h-4 w-4" aria-hidden="true" />
            Cancel
          </Button>
        </div>
      </form>
    );
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
              <p className="text-xs font-semibold uppercase text-clay">Admin</p>
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
              onClick={() => refreshList()}
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

      <div className="wrap py-6">
        <section className="space-y-5 rounded-lg border border-line bg-white/80 p-5 shadow-crisp">
          <div className="flex flex-wrap items-center justify-between gap-4">
            <div className="flex items-center gap-2">
              <Ticket className="h-5 w-5 text-clay" aria-hidden="true" />
              <h2 className="text-lg font-semibold text-ink">Codes</h2>
            </div>
            <div className="flex flex-wrap gap-2">
              <Button onClick={openCreateModal} type="button">
                <Plus className="h-4 w-4" aria-hidden="true" />
                New code
              </Button>
              <Button onClick={openBulkCreateModal} type="button" variant="secondary">
                <Plus className="h-4 w-4" aria-hidden="true" />
                Bulk create
              </Button>
              <Button
                disabled={selectedActiveCodes.length === 0 || bulkAction !== null}
                onClick={() => void bulkDisableSelected()}
                type="button"
                variant="secondary"
              >
                {bulkAction === "disabling" ? (
                  <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
                ) : (
                  <Ban className="h-4 w-4" aria-hidden="true" />
                )}
                Bulk disable
              </Button>
              <Button
                disabled={visibleCodes.length === 0 || bulkAction !== null}
                onClick={exportVisibleCodesCsv}
                type="button"
                variant="secondary"
              >
                {bulkAction === "exporting" ? (
                  <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
                ) : (
                  <Download className="h-4 w-4" aria-hidden="true" />
                )}
                Export CSV
              </Button>
            </div>
          </div>

          <div className="grid gap-3 lg:grid-cols-[minmax(16rem,1fr)_12rem_12rem_auto] lg:items-end">
            <div>
              <label className="text-sm font-semibold text-ink" htmlFor="promo-code-search">
                Search
              </label>
              <div className="relative mt-1">
                <Search
                  className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-ink/40"
                  aria-hidden="true"
                />
                <Input
                  className="pl-9"
                  id="promo-code-search"
                  onChange={(event) => setSearchTerm(event.target.value)}
                  placeholder="Code or display code"
                  type="search"
                  value={searchTerm}
                />
              </div>
            </div>

            <div>
              <label className="text-sm font-semibold text-ink" htmlFor="promo-status-filter">
                Status
              </label>
              <select
                className="mt-1 min-h-10 w-full rounded-md border border-line bg-white px-3 py-2 text-sm font-semibold text-ink outline-none transition focus:border-clay focus:ring-2 focus:ring-clay/15"
                id="promo-status-filter"
                onChange={(event) => setStatusFilter(event.target.value as PromoStatusFilter)}
                value={statusFilter}
              >
                <option value="all">All statuses</option>
                <option value="active">Active</option>
                <option value="pending">Pending</option>
                <option value="expired">Expired</option>
                <option value="exhausted">Exhausted</option>
                <option value="disabled">Disabled</option>
                <option value="archived">Archived</option>
              </select>
            </div>

            <div>
              <label className="text-sm font-semibold text-ink" htmlFor="promo-sort-order">
                Sort
              </label>
              <select
                className="mt-1 min-h-10 w-full rounded-md border border-line bg-white px-3 py-2 text-sm font-semibold text-ink outline-none transition focus:border-clay focus:ring-2 focus:ring-clay/15"
                id="promo-sort-order"
                onChange={(event) => setSortOrder(event.target.value as PromoSortOrder)}
                value={sortOrder}
              >
                <option value="newest">Newest</option>
                <option value="most-redeemed">Most redeemed</option>
              </select>
            </div>

            <div className="flex items-center justify-between gap-3 lg:justify-end">
              {archivedCount > 0 ? (
                <button
                  aria-pressed={showArchived}
                  className="inline-flex min-h-10 items-center gap-1.5 rounded-md border border-line bg-paper px-3 py-2 text-xs font-semibold text-clay transition hover:bg-paper-deep hover:text-ink focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-clay/35 focus-visible:ring-offset-2 focus-visible:ring-offset-paper"
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
              <span className="whitespace-nowrap text-sm text-ink/55">
                {visibleCodes.length} shown
              </span>
            </div>
          </div>

          <div
            aria-label="Promo code status legend"
            className="grid gap-2 text-xs text-ink/60 md:grid-cols-2 lg:grid-cols-3"
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

          {formSuccess ? (
            <div className="flex flex-wrap items-center justify-between gap-2 rounded-md border border-clay/25 bg-mint px-3 py-2 text-sm font-medium text-clay">
              <span>{formSuccess}</span>
              {formSuccessCode ? (
                <Button
                  aria-label={`Copy code ${formSuccessCode}`}
                  className="min-h-8 shrink-0 px-2 py-1 text-xs"
                  onClick={() => copyCodeValue(formSuccessCode, `created:${formSuccessCode}`)}
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

          {bulkNotice ? (
            <div className="rounded-md border border-clay/25 bg-mint px-3 py-2 text-sm font-medium text-clay">
              {bulkNotice}
            </div>
          ) : null}

          {listError ? (
            <p className="rounded-md border border-rust/25 bg-rust/10 px-3 py-2 text-sm font-medium text-rust">
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
          ) : baseVisibleCodes.length === 0 ? (
            <p className="rounded-lg border border-dashed border-line bg-paper/60 px-4 py-6 text-sm text-ink/60">
              Every code is archived. Use Show archived to view and restore them.
            </p>
          ) : visibleCodes.length === 0 ? (
            <p className="rounded-lg border border-dashed border-line bg-paper/60 px-4 py-6 text-sm text-ink/60">
              No promo codes match the current filters.
            </p>
          ) : (
            <div className="overflow-x-auto rounded-lg border border-line">
              <table role="table" className="min-w-[68rem] w-full divide-y divide-line text-left text-sm">
                <thead className="bg-paper text-xs uppercase text-ink/55">
                  <tr>
                    <th className="w-12 px-4 py-3 font-semibold" scope="col">
                      <input
                        aria-label="Select visible promo codes"
                        checked={allVisibleSelected}
                        className="h-4 w-4 rounded border-line text-clay focus:ring-clay/30"
                        onChange={toggleVisibleSelection}
                        ref={(input) => {
                          if (input) {
                            input.indeterminate = someVisibleSelected && !allVisibleSelected;
                          }
                        }}
                        type="checkbox"
                      />
                    </th>
                    <th className="px-4 py-3 font-semibold" scope="col">Code</th>
                    <th className="px-4 py-3 font-semibold" scope="col">Status</th>
                    <th className="px-4 py-3 font-semibold" scope="col">Credits / TTL</th>
                    <th className="px-4 py-3 font-semibold" scope="col">Redeemed / Cap</th>
                    <th className="px-4 py-3 font-semibold" scope="col">Valid window</th>
                    <th className="px-4 py-3 font-semibold" scope="col">Actions</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-line bg-white">
                  {visibleCodes.map((code) => {
                    const codeDisplay = displayCode(code);
                    const remaining = code.maxRedemptionsGlobal === null
                      ? null
                      : Math.max(0, code.maxRedemptionsGlobal - code.redemptionCount);
                    const isArchived = code.status === "archived";
                    const isBusy = updatingId === code.id;
                    const isSelected = selectedId === code.id;
                    const isChecked = selectedIds.has(code.id);
                    const isConfirmingArchive = confirmArchiveId === code.id;
                    const rowCopyKey = `row:${code.id}`;
                    const isCopied = copiedKey === rowCopyKey;
                    const cardError = cardErrors[code.id];

                    return (
                      <tr
                        aria-selected={isSelected}
                        className={isSelected ? "bg-mint/45" : "transition hover:bg-paper/60"}
                        key={code.id}
                      >
                        <td className="px-4 py-4 align-top">
                          <input
                            aria-label={`Select ${codeDisplay}`}
                            checked={isChecked}
                            className="h-4 w-4 rounded border-line text-clay focus:ring-clay/30"
                            onChange={() => toggleCodeSelection(code.id)}
                            type="checkbox"
                          />
                        </td>
                        <td className="px-4 py-4 align-top">
                          <div className="flex min-w-0 items-start gap-2">
                            <div className="min-w-0">
                              <div className="truncate font-mono font-semibold text-ink">
                                {codeDisplay}
                              </div>
                              <div className="mt-1 truncate font-mono text-xs text-ink/45">
                                {code.code}
                              </div>
                            </div>
                            <Button
                              aria-label={`Copy code ${codeDisplay}`}
                              className="min-h-8 min-w-8 shrink-0 px-2 py-1 text-xs"
                              onClick={() => copyCodeValue(codeDisplay, rowCopyKey)}
                              type="button"
                              variant="secondary"
                            >
                              {isCopied ? (
                                <CheckCircle2 className="h-4 w-4" aria-hidden="true" />
                              ) : (
                                <Copy className="h-4 w-4" aria-hidden="true" />
                              )}
                            </Button>
                          </div>
                          {isCopied ? (
                            <p className="mt-1 text-xs font-semibold text-clay">Copied</p>
                          ) : null}
                        </td>
                        <td className="px-4 py-4 align-top">
                          <span className={`rounded-full border px-2.5 py-1 text-xs font-semibold ${statusClasses[code.status]}`}>
                            {statusLabel(code.status)}
                          </span>
                        </td>
                        <td className="px-4 py-4 align-top text-ink/70">
                          <div className="font-semibold text-ink">{code.creditsGranted} credits</div>
                          <div className="mt-1 text-xs">{code.grantTtlDays}-day TTL</div>
                        </td>
                        <td className="px-4 py-4 align-top text-ink/70">
                          <div className="font-semibold text-ink">
                            {code.redemptionCount}
                            {code.maxRedemptionsGlobal === null ? "" : ` / ${code.maxRedemptionsGlobal}`}
                          </div>
                          <div className="mt-1 text-xs">
                            Remaining: {remaining === null ? "Unlimited" : remaining}
                          </div>
                        </td>
                        <td className="px-4 py-4 align-top text-xs text-ink/65">
                          <div>{formatDate(code.validFrom)}</div>
                          <div className="mt-1">to {formatDate(code.validUntil)}</div>
                        </td>
                        <td className="px-4 py-4 align-top">
                          <div className="flex flex-wrap gap-2">
                            <Button
                              aria-label={`View stats for ${codeDisplay}`}
                              className="min-h-9 px-3 py-1.5 text-xs"
                              onClick={() => openStatsDrawer(code)}
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
                                  onClick={() => startEdit(code)}
                                  type="button"
                                  variant="secondary"
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
                                  <>
                                    <Button
                                      aria-label={`Archive ${codeDisplay}`}
                                      className="min-h-9 px-3 py-1.5 text-xs"
                                      disabled={isBusy}
                                      onClick={() => archiveCode(code)}
                                      type="button"
                                      variant="secondary"
                                    >
                                      {isBusy ? (
                                        <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
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
                                  </>
                                ) : (
                                  <Button
                                    aria-label={`Archive ${codeDisplay}`}
                                    className="min-h-9 px-3 py-1.5 text-xs"
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
                            <p className="mt-2 rounded-md border border-rust/25 bg-rust/10 px-3 py-2 text-xs font-medium text-rust">
                              {cardError}
                            </p>
                          ) : null}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </section>
      </div>

      {createModalOpen ? (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-ink/35 p-4"
          onMouseDown={(event) => {
            if (event.target === event.currentTarget) {
              closeCreateModal();
            }
          }}
          role="presentation"
        >
          <section
            aria-labelledby="promo-create-title"
            aria-modal="true"
            className="max-h-[calc(100vh-2rem)] w-full max-w-2xl overflow-y-auto rounded-lg border border-line bg-white p-5 shadow-crisp focus:outline-none md:p-6"
            ref={createDialogRef}
            role="dialog"
            tabIndex={-1}
          >
            <div className="mb-5 flex items-start justify-between gap-4">
              <div className="flex items-center gap-2">
                <Plus className="h-5 w-5 text-clay" aria-hidden="true" />
                <h2 className="text-lg font-semibold text-ink" id="promo-create-title">
                  New code
                </h2>
              </div>
              <Button
                aria-label="Close new code form"
                className="min-h-8 min-w-8 px-2 py-1"
                onClick={closeCreateModal}
                type="button"
                variant="ghost"
              >
                <X className="h-4 w-4" aria-hidden="true" />
              </Button>
            </div>
            {renderCreateForm()}
          </section>
        </div>
      ) : null}

      {bulkCreateModalOpen ? (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-ink/35 p-4"
          onMouseDown={(event) => {
            if (event.target === event.currentTarget) {
              closeBulkCreateModal();
            }
          }}
          role="presentation"
        >
          <section
            aria-labelledby="promo-bulk-create-title"
            aria-modal="true"
            className="max-h-[calc(100vh-2rem)] w-full max-w-2xl overflow-y-auto rounded-lg border border-line bg-white p-5 shadow-crisp focus:outline-none md:p-6"
            ref={bulkCreateDialogRef}
            role="dialog"
            tabIndex={-1}
          >
            <div className="mb-5 flex items-start justify-between gap-4">
              <div className="flex items-center gap-2">
                <Plus className="h-5 w-5 text-clay" aria-hidden="true" />
                <h2
                  className="text-lg font-semibold text-ink"
                  id="promo-bulk-create-title"
                >
                  Bulk create
                </h2>
              </div>
              <Button
                aria-label="Close bulk create form"
                className="min-h-8 min-w-8 px-2 py-1"
                disabled={bulkAction === "creating"}
                onClick={closeBulkCreateModal}
                type="button"
                variant="ghost"
              >
                <X className="h-4 w-4" aria-hidden="true" />
              </Button>
            </div>
            {renderBulkCreateForm()}
          </section>
        </div>
      ) : null}

      {isEditModalOpen ? (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-ink/35 p-4"
          onMouseDown={(event) => {
            if (event.target === event.currentTarget) {
              cancelEdit();
            }
          }}
          role="presentation"
        >
          <section
            aria-labelledby="promo-edit-title"
            aria-modal="true"
            className="max-h-[calc(100vh-2rem)] w-full max-w-2xl overflow-y-auto rounded-lg border border-line bg-white p-5 shadow-crisp focus:outline-none md:p-6"
            ref={editDialogRef}
            role="dialog"
            tabIndex={-1}
          >
            <div className="mb-5 flex items-start justify-between gap-4">
              <div className="flex items-center gap-2">
                <Pencil className="h-5 w-5 text-clay" aria-hidden="true" />
                <h2 className="text-lg font-semibold text-ink" id="promo-edit-title">
                  Edit {editingCode ? displayCode(editingCode) : "promo code"}
                </h2>
              </div>
              <Button
                aria-label="Close edit form"
                className="min-h-8 min-w-8 px-2 py-1"
                onClick={cancelEdit}
                type="button"
                variant="ghost"
              >
                <X className="h-4 w-4" aria-hidden="true" />
              </Button>
            </div>
            {renderEditForm()}
          </section>
        </div>
      ) : null}

      {statsDrawerOpen ? (
        <div
          className="fixed inset-0 z-50 flex justify-end bg-ink/25"
          onMouseDown={(event) => {
            if (event.target === event.currentTarget) {
              closeStatsDrawer();
            }
          }}
          role="presentation"
        >
          <aside
            aria-labelledby="promo-stats-drawer-title"
            aria-modal="true"
            className="h-full w-full overflow-y-auto border-l border-line bg-paper p-4 shadow-crisp focus:outline-none sm:max-w-2xl md:p-6"
            ref={statsDrawerRef}
            role="dialog"
            tabIndex={-1}
          >
            <div className="mb-4 flex items-start justify-between gap-4">
              <div>
                <p className="text-xs font-semibold uppercase text-clay">Promo code</p>
                <h2 className="mt-1 text-xl font-semibold text-ink" id="promo-stats-drawer-title">
                  Stats
                </h2>
              </div>
              <Button
                aria-label="Close stats drawer"
                className="min-h-8 min-w-8 px-2 py-1"
                onClick={closeStatsDrawer}
                type="button"
                variant="ghost"
              >
                <X className="h-4 w-4" aria-hidden="true" />
              </Button>
            </div>
            <PromoStatsPanel detail={detail} error={detailError} loading={detailLoading} />
          </aside>
        </div>
      ) : null}
    </main>
  );
}
