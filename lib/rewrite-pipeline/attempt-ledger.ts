import type { RewriteAttemptLedgerEntry } from "./types";

export function createRewriteAttemptLedgerEntry(
  entry: RewriteAttemptLedgerEntry,
): RewriteAttemptLedgerEntry {
  if (!Number.isInteger(entry.attemptNo) || entry.attemptNo < 1 || entry.attemptNo > 10) {
    throw new Error("attemptNo must be between 1 and 10.");
  }

  return {
    ...entry,
    failureKinds: [...entry.failureKinds],
  };
}
