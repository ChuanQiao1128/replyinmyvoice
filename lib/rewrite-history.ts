export const REWRITE_HISTORY_STORAGE_KEY_PREFIX = "rimv.rewrite.history.v1";

type RewriteHistoryStorage = Pick<Storage, "getItem" | "removeItem" | "setItem">;
type IterableRewriteHistoryStorage = RewriteHistoryStorage &
  Pick<Storage, "key" | "length">;

function browserStorage(): Storage | null {
  if (typeof window === "undefined") {
    return null;
  }

  try {
    return window.localStorage;
  } catch {
    return null;
  }
}

function normalizedUserKey(userKey: string) {
  const trimmed = userKey.trim();
  if (!trimmed) {
    throw new Error("Rewrite history user key is required.");
  }

  return encodeURIComponent(trimmed);
}

function canListKeys(
  storage: RewriteHistoryStorage,
): storage is IterableRewriteHistoryStorage {
  return (
    typeof (storage as { key?: unknown }).key === "function" &&
    typeof (storage as { length?: unknown }).length === "number"
  );
}

export function rewriteHistoryStorageKey(userKey: string) {
  return `${REWRITE_HISTORY_STORAGE_KEY_PREFIX}:${normalizedUserKey(userKey)}`;
}

export function readLocalRewriteHistory(
  userKey: string,
  storage: RewriteHistoryStorage | null = browserStorage(),
) {
  return storage?.getItem(rewriteHistoryStorageKey(userKey)) ?? null;
}

export function writeLocalRewriteHistory(
  userKey: string,
  value: string,
  storage: RewriteHistoryStorage | null = browserStorage(),
) {
  storage?.setItem(rewriteHistoryStorageKey(userKey), value);
}

export function clearLegacyRewriteHistory(
  storage: RewriteHistoryStorage | null = browserStorage(),
) {
  storage?.removeItem(REWRITE_HISTORY_STORAGE_KEY_PREFIX);
}

export function clearLocalRewriteHistory(
  userKey: string | null | undefined,
  storage: RewriteHistoryStorage | null = browserStorage(),
) {
  if (!storage) {
    return;
  }

  if (userKey?.trim()) {
    storage.removeItem(rewriteHistoryStorageKey(userKey));
  }
  storage.removeItem(REWRITE_HISTORY_STORAGE_KEY_PREFIX);
}

export function clearAllLocalRewriteHistory(
  storage: RewriteHistoryStorage | null = browserStorage(),
) {
  if (!storage) {
    return;
  }

  const keysToRemove = [REWRITE_HISTORY_STORAGE_KEY_PREFIX];
  if (canListKeys(storage)) {
    const prefix = `${REWRITE_HISTORY_STORAGE_KEY_PREFIX}:`;
    for (let index = 0; index < storage.length; index += 1) {
      const key = storage.key(index);
      if (key?.startsWith(prefix)) {
        keysToRemove.push(key);
      }
    }
  }

  keysToRemove.forEach((key) => storage.removeItem(key));
}
