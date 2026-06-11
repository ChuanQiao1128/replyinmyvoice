"use client";

import Link from "next/link";

import styles from "../../components/app/shell/shell.module.css";

export default function AppError({
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  return (
    <div className={styles.errorBox} role="alert">
      <div>
        <p className={styles.emptyTitle}>Reply In My Voice could not load.</p>
        <p className={styles.emptyBody} style={{ marginTop: 6 }}>
          The workspace hit a temporary account loading problem. Try again, or
          return to the workspace start.
        </p>
      </div>
      <div className={styles.emptyActions} style={{ justifyContent: "flex-start" }}>
        <button type="button" className="btn btn-primary" onClick={reset}>
          Try again
        </button>
        <Link href="/app" className="btn btn-ghost">
          Back to workspace
        </Link>
      </div>
    </div>
  );
}
