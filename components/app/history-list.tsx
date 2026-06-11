"use client";

import { useCallback, useEffect, useState } from "react";

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
  const [openId, setOpenId] = useState<string | null>(null);
  const [details, setDetails] = useState<Record<string, DetailState>>({});
  const [deletingId, setDeletingId] = useState<string | null>(null);
  const [copiedId, setCopiedId] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (demoItems) {
      return;
    }
    setError(false);
    setItems(null);
    try {
      const response = await fetch("/api/me/rewrites?page=1&pageSize=20", {
        cache: "no-store",
      });
      if (!response.ok) {
        throw new Error(`status ${response.status}`);
      }
      const payload = (await response.json()) as Record<string, unknown>;
      const rawItems = (payload.items ?? payload.Items ?? []) as Record<
        string,
        unknown
      >[];
      setItems(
        rawItems
          .map(normalizeItem)
          .filter((item): item is HistoryItem => item !== null),
      );
    } catch {
      setError(true);
    }
  }, [demoItems]);

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

  const remove = useCallback(async (attemptId: string) => {
    setDeletingId(attemptId);
    try {
      const response = await fetch(`/api/me/rewrites/${attemptId}`, {
        method: "DELETE",
      });
      if (response.ok || response.status === 204) {
        setItems((current) =>
          current
            ? current.filter((item) => item.attemptId !== attemptId)
            : current,
        );
        setOpenId((current) => (current === attemptId ? null : current));
      }
    } finally {
      setDeletingId(null);
    }
  }, []);

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
      <EmptyState
        icon="history"
        title="No rewrites yet"
        body="Rewrites you create are saved here so you can reopen, copy, or delete them across your devices."
        actions={[{ label: "Start a rewrite", href: "/app", primary: true }]}
      />
    );
  }

  return (
    <div className={styles.list}>
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
                              AI Signal · before vs after
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
                                    : "No signal change"}
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
                          disabled={deletingId === item.attemptId}
                          onClick={() => void remove(item.attemptId)}
                        >
                          {deletingId === item.attemptId
                            ? "Deleting…"
                            : "Delete"}
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
    </div>
  );
}
