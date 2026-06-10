"use client";

import { useCallback, useEffect, useState } from "react";

import { EmptyState, Skeleton } from "./shell/shell-primitives";
import styles from "./shell/shell.module.css";

type HistoryItem = {
  attemptId: string;
  status: string;
  preview: string;
  createdAt: string | null;
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

function previewFromResult(resultJson: unknown): string {
  if (typeof resultJson !== "string" || resultJson.length === 0) {
    return "";
  }
  try {
    const parsed = JSON.parse(resultJson) as Record<string, unknown>;
    const text =
      readString(parsed, "rewrittenText", "RewrittenText", "rewrite", "text") ??
      "";
    return text.replace(/\s+/g, " ").trim();
  } catch {
    return "";
  }
}

function normalizeItem(raw: Record<string, unknown>): HistoryItem | null {
  const attemptId = readString(raw, "attemptId", "AttemptId");
  if (!attemptId) {
    return null;
  }
  const status = readString(raw, "status", "Status") ?? "unknown";
  const resultJson = raw.resultJson ?? raw.ResultJson;
  const createdAt = readString(raw, "createdAt", "CreatedAt");
  const preview = previewFromResult(resultJson);
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

export function HistoryList() {
  const [items, setItems] = useState<HistoryItem[] | null>(null);
  const [error, setError] = useState(false);
  const [deletingId, setDeletingId] = useState<string | null>(null);

  const load = useCallback(async () => {
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
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const remove = useCallback(async (attemptId: string) => {
    setDeletingId(attemptId);
    try {
      const response = await fetch(`/api/me/rewrites/${attemptId}`, {
        method: "DELETE",
      });
      if (response.ok || response.status === 204) {
        setItems((current) =>
          current ? current.filter((item) => item.attemptId !== attemptId) : current,
        );
      }
    } finally {
      setDeletingId(null);
    }
  }, []);

  if (error) {
    return (
      <div className={styles.errorBox}>
        <p style={{ margin: 0 }}>We couldn&apos;t load your history just now.</p>
        <button type="button" className="btn btn-ghost" onClick={() => void load()}>
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
      {items.map((item) => (
        <article key={item.attemptId} className={styles.listRow}>
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
            </div>
          </div>
          <button
            type="button"
            className={styles.iconBtn}
            disabled={deletingId === item.attemptId}
            onClick={() => void remove(item.attemptId)}
          >
            {deletingId === item.attemptId ? "Deleting…" : "Delete"}
          </button>
        </article>
      ))}
    </div>
  );
}
