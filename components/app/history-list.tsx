"use client";

import { useCallback, useEffect, useRef, useState } from "react";

import { NatBar } from "../landing/nat-bar";
import { ShellIcon } from "./shell/shell-icons";
import { EmptyState, Skeleton } from "./shell/shell-primitives";
import styles from "./shell/shell.module.css";

type HistoryItem = {
  attemptId: string;
  status: string;
  preview: string;
  createdAt: string | null;
};

const HISTORY_PAGE_SIZE = 20;
const DELETE_UNDO_MS = 5000;

export type HistoryDetail = {
  draft: string;
  rewrite: string;
  draftSignal: number | null;
  rewriteSignal: number | null;
};

type DetailState =
  | { status: "loading" }
  | { status: "error" }
  | ({ status: "ready" } & HistoryDetail);

type PendingDelete = {
  item: HistoryItem;
  index: number;
};

function readString(record: Record<string, unknown>, ...keys: string[]) {
  for (const key of keys) {
    const value = record[key];
    if (typeof value === "string" && value.length > 0) {
      return value;
    }
  }
  return null;
}

function readNumber(record: Record<string, unknown>, ...keys: string[]) {
  for (const key of keys) {
    const value = record[key];
    if (typeof value === "number" && Number.isFinite(value)) {
      return value;
    }
  }
  return null;
}

function parseJsonObject(value: unknown): Record<string, unknown> | null {
  if (typeof value !== "string" || value.length === 0) {
    return null;
  }
  try {
    const parsed = JSON.parse(value) as unknown;
    if (parsed && typeof parsed === "object" && !Array.isArray(parsed)) {
      return parsed as Record<string, unknown>;
    }
  } catch {
    /* tolerated — raw content may already be scrubbed by retention */
  }
  return null;
}

function rewriteTextFrom(resultJson: unknown): string {
  const parsed = parseJsonObject(resultJson);
  if (!parsed) {
    return "";
  }
  return (
    readString(parsed, "rewrittenText", "RewrittenText", "rewrite", "text") ?? ""
  );
}

function detailFromPayload(payload: Record<string, unknown>): HistoryDetail {
  const request = parseJsonObject(payload.requestJson ?? payload.RequestJson);
  const result = parseJsonObject(payload.resultJson ?? payload.ResultJson);
  const rawNaturalness = result?.naturalness ?? result?.Naturalness;
  const naturalness =
    rawNaturalness &&
    typeof rawNaturalness === "object" &&
    !Array.isArray(rawNaturalness)
      ? (rawNaturalness as Record<string, unknown>)
      : null;

  return {
    draft: request
      ? (readString(request, "roughDraftReply", "RoughDraftReply", "draft", "Draft") ?? "")
      : "",
    rewrite: result
      ? (readString(result, "rewrittenText", "RewrittenText", "rewrite", "text") ?? "")
      : "",
    draftSignal: naturalness
      ? readNumber(naturalness, "draftAiLikePercent", "DraftAiLikePercent")
      : null,
    rewriteSignal: naturalness
      ? readNumber(naturalness, "rewriteAiLikePercent", "RewriteAiLikePercent")
      : null,
  };
}

function normalizeItem(raw: Record<string, unknown>): HistoryItem | null {
  const attemptId = readString(raw, "attemptId", "AttemptId");
  if (!attemptId) {
    return null;
  }
  const status = readString(raw, "status", "Status") ?? "unknown";
  const preview = rewriteTextFrom(raw.resultJson ?? raw.ResultJson)
    .replace(/\s+/g, " ")
    .trim();
  const createdAt = readString(raw, "createdAt", "CreatedAt");
  return { attemptId, status, preview, createdAt };
}

function formatDate(value: string | null): string {
  if (!value) {
    return "";
  }
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return "";
  }
  return date.toLocaleDateString(undefined, {
    year: "numeric",
    month: "short",
    day: "numeric",
  });
}

function isSuccess(status: string): boolean {
  return /succeed|success|complete|completed/i.test(status);
}

type Props = {
  /** Preview/test hook: render with provided data and skip network fetches. */
  demoItems?: HistoryItem[];
  demoDetail?: HistoryDetail;
};

export function HistoryList({ demoItems, demoDetail }: Props = {}) {
  const [items, setItems] = useState<HistoryItem[] | null>(demoItems ?? null);
  const [error, setError] = useState(false);
  const [page, setPage] = useState(1);
  const [hasMore, setHasMore] = useState(false);
  const [loadingMore, setLoadingMore] = useState(false);
  const [loadMoreError, setLoadMoreError] = useState(false);
  const [openId, setOpenId] = useState<string | null>(null);
  const [details, setDetails] = useState<Record<string, DetailState>>({});
  const [copiedId, setCopiedId] = useState<string | null>(null);
  const [pendingDeletes, setPendingDeletes] = useState<PendingDelete[]>([]);
  const [deleteNotice, setDeleteNotice] = useState<string | null>(null);
  const deleteTimersRef = useRef<Map<string, number>>(new Map());

  const restorePendingDelete = useCallback((pending: PendingDelete) => {
    setItems((current) => {
      if (current?.some((item) => item.attemptId === pending.item.attemptId)) {
        return current;
      }
      const next = [...(current ?? [])];
      next.splice(Math.min(pending.index, next.length), 0, pending.item);
      return next;
    });
  }, []);

  const clearDeleteTimer = useCallback((attemptId: string) => {
    const timer = deleteTimersRef.current.get(attemptId);
    if (timer !== undefined) {
      window.clearTimeout(timer);
      deleteTimersRef.current.delete(attemptId);
    }
  }, []);

  const finalizeDelete = useCallback(
    async (pending: PendingDelete) => {
      const attemptId = pending.item.attemptId;
      deleteTimersRef.current.delete(attemptId);
      try {
        const response = await fetch(`/api/me/rewrites/${attemptId}`, {
          method: "DELETE",
        });
        if (!response.ok && response.status !== 204) {
          throw new Error(`status ${response.status}`);
        }
        setPendingDeletes((current) =>
          current.filter((entry) => entry.item.attemptId !== attemptId),
        );
      } catch {
        setPendingDeletes((current) =>
          current.filter((entry) => entry.item.attemptId !== attemptId),
        );
        restorePendingDelete(pending);
        setDeleteNotice("Couldn’t delete that rewrite, so it was restored.");
      }
    },
    [restorePendingDelete],
  );

  const loadPage = useCallback(
    async (targetPage: number, mode: "replace" | "append") => {
      if (demoItems) {
        return;
      }
      setError(false);
      setLoadMoreError(false);
      if (mode === "replace") {
        setItems(null);
        setPage(1);
        setHasMore(false);
      } else {
        setLoadingMore(true);
      }

      try {
        const response = await fetch(
          `/api/me/rewrites?page=${targetPage}&pageSize=${HISTORY_PAGE_SIZE}`,
          {
            cache: "no-store",
          },
        );
        if (!response.ok) {
          throw new Error(`status ${response.status}`);
        }
        const payload = (await response.json()) as Record<string, unknown>;
        const rawItems = (payload.items ?? payload.Items ?? []) as Record<
          string,
          unknown
        >[];
        const nextItems = rawItems
          .map(normalizeItem)
          .filter((item): item is HistoryItem => item !== null);
        const responsePageSize =
          readNumber(payload, "pageSize", "PageSize") ?? HISTORY_PAGE_SIZE;
        const totalCount = readNumber(payload, "totalCount", "TotalCount");

        setItems((current) =>
          mode === "append" && current ? [...current, ...nextItems] : nextItems,
        );
        setPage(targetPage);
        setHasMore(
          totalCount !== null
            ? targetPage * responsePageSize < totalCount
            : rawItems.length >= responsePageSize,
        );
      } catch {
        if (mode === "replace") {
          setError(true);
        } else {
          setLoadMoreError(true);
        }
      } finally {
        if (mode === "append") {
          setLoadingMore(false);
        }
      }
    },
    [demoItems],
  );

  const load = useCallback(async () => {
    await loadPage(1, "replace");
  }, [loadPage]);

  const loadMore = useCallback(async () => {
    if (loadingMore || !hasMore) {
      return;
    }
    await loadPage(page + 1, "append");
  }, [hasMore, loadPage, loadingMore, page]);

  useEffect(() => {
    const deleteTimers = deleteTimersRef.current;
    return () => {
      for (const timer of deleteTimers.values()) {
        window.clearTimeout(timer);
      }
      deleteTimers.clear();
    };
  }, []);

  const remove = useCallback(
    (attemptId: string) => {
      if (!items) {
        return;
      }
      const index = items.findIndex((item) => item.attemptId === attemptId);
      if (index === -1) {
        return;
      }
      const pending = { item: items[index], index };
      clearDeleteTimer(attemptId);
      setDeleteNotice(null);
      setItems(items.filter((item) => item.attemptId !== attemptId));
      setOpenId((current) => (current === attemptId ? null : current));
      setPendingDeletes((current) => [
        ...current.filter((entry) => entry.item.attemptId !== attemptId),
        pending,
      ]);
      const timer = window.setTimeout(() => {
        void finalizeDelete(pending);
      }, DELETE_UNDO_MS);
      deleteTimersRef.current.set(attemptId, timer);
    },
    [clearDeleteTimer, finalizeDelete, items],
  );

  const undoDelete = useCallback(
    (attemptId: string) => {
      const pending = pendingDeletes.find(
        (entry) => entry.item.attemptId === attemptId,
      );
      if (!pending) {
        return;
      }
      clearDeleteTimer(attemptId);
      setPendingDeletes((current) =>
        current.filter((entry) => entry.item.attemptId !== attemptId),
      );
      setDeleteNotice(null);
      restorePendingDelete(pending);
    },
    [clearDeleteTimer, pendingDeletes, restorePendingDelete],
  );

  const undoRegion =
    pendingDeletes.length > 0 || deleteNotice ? (
      <div className={styles.historyToastStack} aria-live="polite">
        {pendingDeletes.map((pending) => (
          <div
            key={pending.item.attemptId}
            className={styles.historyToast}
            role="status"
          >
            <span>Rewrite removed. Undo in the next 5 seconds.</span>
            <button
              type="button"
              className="btn btn-ghost"
              onClick={() => undoDelete(pending.item.attemptId)}
            >
              Undo
            </button>
          </div>
        ))}
        {deleteNotice ? (
          <div className={styles.historyNotice} role="status">
            {deleteNotice}
          </div>
        ) : null}
      </div>
    ) : null;

  useEffect(() => {
    void load();
  }, [load]);

  const openDetail = useCallback(
    async (attemptId: string) => {
      if (openId === attemptId) {
        setOpenId(null);
        return;
      }
      setOpenId(attemptId);
      if (details[attemptId]?.status === "ready") {
        return;
      }
      if (demoDetail) {
        setDetails((current) => ({
          ...current,
          [attemptId]: { status: "ready", ...demoDetail },
        }));
        return;
      }
      setDetails((current) => ({
        ...current,
        [attemptId]: { status: "loading" },
      }));
      try {
        const response = await fetch(`/api/me/rewrites/${attemptId}`, {
          cache: "no-store",
        });
        if (!response.ok) {
          throw new Error(`status ${response.status}`);
        }
        const payload = (await response.json()) as Record<string, unknown>;
        setDetails((current) => ({
          ...current,
          [attemptId]: { status: "ready", ...detailFromPayload(payload) },
        }));
      } catch {
        setDetails((current) => ({
          ...current,
          [attemptId]: { status: "error" },
        }));
      }
    },
    [demoDetail, details, openId],
  );

  const copyRewrite = useCallback(async (attemptId: string, text: string) => {
    if (!text) {
      return;
    }
    await navigator.clipboard.writeText(text);
    setCopiedId(attemptId);
    window.setTimeout(() => {
      setCopiedId((current) => (current === attemptId ? null : current));
    }, 1600);
  }, []);

  if (error) {
    return (
      <div className={styles.errorBox}>
        <p style={{ margin: 0 }}>We couldn&apos;t load your history just now.</p>
        <button
          type="button"
          className="btn btn-ghost"
          onClick={() => void load()}
        >
          Try again
        </button>
      </div>
    );
  }

  if (items === null) {
    return (
      <div className={styles.list}>
        <Skeleton lines={2} />
        <Skeleton lines={2} />
        <Skeleton lines={2} />
      </div>
    );
  }

  if (items.length === 0) {
    return (
      <>
        <EmptyState
          icon="history"
          title="Welcome to your history"
          body="Completed rewrites you create appear here for up to 90 days. After that, raw content is removed. Start from the workspace when you’re ready."
          actions={[{ label: "Start a rewrite", href: "/app", primary: true }]}
        />
        {undoRegion}
      </>
    );
  }

  return (
    <div className={styles.list}>
      <p className={styles.historyWelcome}>
        Welcome back. Your newest saved rewrites are shown first.
      </p>
      {items.map((item) => {
        const open = openId === item.attemptId;
        const detail = details[item.attemptId];
        const delta =
          detail?.status === "ready" &&
          detail.draftSignal !== null &&
          detail.rewriteSignal !== null
            ? detail.draftSignal - detail.rewriteSignal
            : null;

        return (
          <article key={item.attemptId} className={styles.listRow}>
            <div className={styles.listMain}>
              <button
                type="button"
                className={styles.histRowBtn}
                aria-expanded={open}
                onClick={() => void openDetail(item.attemptId)}
              >
                <div className={styles.listMain}>
                  <p className={styles.listPreview}>
                    {item.preview || "Rewrite result"}
                  </p>
                  <div className={styles.listMeta}>
                    <span
                      className={`${styles.badge} ${isSuccess(item.status) ? "" : styles.badgeMuted}`}
                    >
                      {isSuccess(item.status) ? "Completed" : item.status}
                    </span>
                    {formatDate(item.createdAt) ? (
                      <span>{formatDate(item.createdAt)}</span>
                    ) : null}
                    <span>{open ? "Hide details" : "View draft vs rewrite"}</span>
                  </div>
                </div>
                <span
                  className={`${styles.histChevron} ${open ? styles.histChevronOpen : ""}`}
                  aria-hidden="true"
                >
                  <ShellIcon name="chevron" size={16} />
                </span>
              </button>

              {open ? (
                <div className={styles.histPanel}>
                  {!detail || detail.status === "loading" ? (
                    <>
                      <Skeleton lines={4} />
                      <Skeleton lines={4} />
                    </>
                  ) : detail.status === "error" ? (
                    <div className={`${styles.errorBox} ${styles.histPanelFoot}`}>
                      <p style={{ margin: 0 }}>
                        Couldn&apos;t load this rewrite&apos;s details.
                      </p>
                    </div>
                  ) : (
                    <>
                      {detail.draftSignal !== null &&
                      detail.rewriteSignal !== null ? (
                        <div className={styles.histSignal}>
                          <div className={styles.histSignalHead}>
                            <span className={styles.histBlockLabel}>
                              AI Signal (naturalness) · before vs after
                            </span>
                            {delta !== null ? (
                              <span
                                className={`${styles.badge} ${
                                  delta > 0
                                    ? ""
                                    : delta < 0
                                      ? styles.badgeWarn
                                      : styles.badgeMuted
                                }`}
                              >
                                {delta > 0
                                  ? `−${delta} pts more natural`
                                  : delta < 0
                                    ? `+${Math.abs(delta)} pts`
                                    : "No change"}
                              </span>
                            ) : null}
                          </div>
                          <div className={styles.histSignalBar}>
                            <NatBar
                              after={detail.rewriteSignal}
                              before={detail.draftSignal}
                            />
                          </div>
                          <div className={styles.histSignalLegend}>
                            <span className={styles.histSignalPill}>
                              Draft {detail.draftSignal}%
                            </span>
                            <span
                              className={`${styles.histSignalPill} ${styles.histSignalPillAccent}`}
                            >
                              Rewrite {detail.rewriteSignal}%
                            </span>
                          </div>
                          <p className={styles.histSignalNote}>
                            A third-party reference signal — lower reads more natural.
                            It is not a guarantee; review before sending.
                          </p>
                        </div>
                      ) : null}
                      <div className={styles.histBlock}>
                        <span className={styles.histBlockLabel}>
                          Your draft
                          {detail.draftSignal !== null
                            ? ` · AI Signal ${detail.draftSignal}%`
                            : ""}
                        </span>
                        <p className={styles.histText}>
                          {detail.draft ||
                            "The original draft is no longer stored for this rewrite (retention window passed)."}
                        </p>
                      </div>
                      <div className={styles.histBlock}>
                        <span
                          className={`${styles.histBlockLabel} ${styles.histBlockAccent}`}
                        >
                          In your voice
                          {detail.rewriteSignal !== null
                            ? ` · AI Signal ${detail.rewriteSignal}%`
                            : ""}
                        </span>
                        <p className={styles.histText}>
                          {detail.rewrite ||
                            "The rewrite text is no longer stored for this item."}
                        </p>
                      </div>
                      <div className={styles.histPanelFoot}>
                        <button
                          type="button"
                          className="btn btn-primary"
                          disabled={!detail.rewrite}
                          onClick={() =>
                            void copyRewrite(item.attemptId, detail.rewrite)
                          }
                        >
                          {copiedId === item.attemptId
                            ? "Copied"
                            : "Copy rewrite"}
                        </button>
                        <button
                          type="button"
                          className={styles.iconBtn}
                          onClick={() => remove(item.attemptId)}
                        >
                          Delete
                        </button>
                      </div>
                    </>
                  )}
                </div>
              ) : null}
            </div>
          </article>
        );
      })}
      {hasMore || loadingMore || loadMoreError ? (
        <div className={styles.historyPager}>
          <button
            type="button"
            className="btn btn-ghost"
            disabled={loadingMore || !hasMore}
            onClick={() => void loadMore()}
          >
            {loadingMore ? "Loading…" : "Load more"}
          </button>
          {loadMoreError ? (
            <p className={styles.historyPagerNote}>
              Couldn’t load more history. Try again.
            </p>
          ) : null}
        </div>
      ) : null}
      {undoRegion}
    </div>
  );
}
