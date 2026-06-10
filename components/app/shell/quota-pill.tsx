import Link from "next/link";

import type { ShellQuota } from "./shell-types";
import styles from "./shell.module.css";

export function QuotaPill({ quota }: { quota: ShellQuota }) {
  const total = Math.max(quota.quota, 0);
  const remaining = Math.max(quota.remaining, 0);
  const pct = total > 0 ? Math.min(100, Math.round((remaining / total) * 100)) : 0;
  const low = total > 0 && remaining / total <= 0.15;

  return (
    <Link href="/app/usage" className={styles.quotaPill} aria-label="View usage">
      <span className={styles.quotaPillTop}>
        <span className={styles.quotaNum}>
          {remaining} of {total || "—"} left
        </span>
        <span className={styles.quotaScope}>{quota.scopeLabel}</span>
      </span>
      <span className={styles.quotaTrack}>
        <span
          className={`${styles.quotaFill} ${low ? styles.quotaFillLow : ""}`}
          style={{ width: `${pct}%` }}
        />
      </span>
    </Link>
  );
}
