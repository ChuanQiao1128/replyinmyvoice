import { describe, expect, it } from "vitest";

import {
  clearAllLocalRewriteHistory,
  clearLocalRewriteHistory,
  readLocalRewriteHistory,
  REWRITE_HISTORY_STORAGE_KEY_PREFIX,
  rewriteHistoryStorageKey,
  writeLocalRewriteHistory,
} from "../../lib/rewrite-history";

class MemoryStorage {
  private readonly values = new Map<string, string>();

  get length() {
    return this.values.size;
  }

  getItem(key: string) {
    return this.values.get(key) ?? null;
  }

  key(index: number) {
    return [...this.values.keys()][index] ?? null;
  }

  removeItem(key: string) {
    this.values.delete(key);
  }

  setItem(key: string, value: string) {
    this.values.set(key, value);
  }
}

describe("rewrite history storage", () => {
  it("keeps local rewrite history scoped to the supplied user key", () => {
    const storage = new MemoryStorage();

    writeLocalRewriteHistory("user-a", "[\"first\"]", storage);

    expect(rewriteHistoryStorageKey("user-a")).toBe(
      `${REWRITE_HISTORY_STORAGE_KEY_PREFIX}:user-a`,
    );
    expect(readLocalRewriteHistory("user-a", storage)).toBe("[\"first\"]");
    expect(readLocalRewriteHistory("user-b", storage)).toBeNull();
    expect(storage.getItem(REWRITE_HISTORY_STORAGE_KEY_PREFIX)).toBeNull();
  });

  it("clears the current user's namespaced key and the legacy key only", () => {
    const storage = new MemoryStorage();
    storage.setItem(REWRITE_HISTORY_STORAGE_KEY_PREFIX, "[\"legacy\"]");
    writeLocalRewriteHistory("user-a", "[\"first\"]", storage);
    writeLocalRewriteHistory("user-b", "[\"second\"]", storage);

    clearLocalRewriteHistory("user-a", storage);

    expect(readLocalRewriteHistory("user-a", storage)).toBeNull();
    expect(readLocalRewriteHistory("user-b", storage)).toBe("[\"second\"]");
    expect(storage.getItem(REWRITE_HISTORY_STORAGE_KEY_PREFIX)).toBeNull();
  });

  it("clears every local rewrite history bucket for logout", () => {
    const storage = new MemoryStorage();
    storage.setItem(REWRITE_HISTORY_STORAGE_KEY_PREFIX, "[\"legacy\"]");
    writeLocalRewriteHistory("user-a", "[\"first\"]", storage);
    writeLocalRewriteHistory("user-b", "[\"second\"]", storage);
    storage.setItem("other.local.key", "kept");

    clearAllLocalRewriteHistory(storage);

    expect(readLocalRewriteHistory("user-a", storage)).toBeNull();
    expect(readLocalRewriteHistory("user-b", storage)).toBeNull();
    expect(storage.getItem(REWRITE_HISTORY_STORAGE_KEY_PREFIX)).toBeNull();
    expect(storage.getItem("other.local.key")).toBe("kept");
  });
});
