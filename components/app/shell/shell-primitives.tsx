import type { ReactNode } from "react";
import Link from "next/link";

import { ShellIcon } from "./shell-icons";
import type { ShellIconName } from "./shell-types";
import styles from "./shell.module.css";

/** Page heading used at the top of each shell route. */
export function PageHeader({
  title,
  description,
}: {
  title: string;
  description?: string;
}) {
  return (
    <header className={styles.pageHeader}>
      <h1 className={styles.pageTitle}>{title}</h1>
      {description ? <p className={styles.pageDesc}>{description}</p> : null}
    </header>
  );
}

export function SectionCard({ children }: { children: ReactNode }) {
  return <section className={styles.sectionCard}>{children}</section>;
}

type EmptyAction = { label: string; href: string; primary?: boolean };

/** Explicit empty state — every shell surface gets one instead of a blank screen. */
export function EmptyState({
  icon = "sparkle",
  title,
  body,
  actions = [],
}: {
  icon?: ShellIconName;
  title: string;
  body?: string;
  actions?: EmptyAction[];
}) {
  return (
    <div className={styles.emptyState}>
      <span className={styles.emptyIcon}>
        <ShellIcon name={icon} size={22} />
      </span>
      <h2 className={styles.emptyTitle}>{title}</h2>
      {body ? <p className={styles.emptyBody}>{body}</p> : null}
      {actions.length > 0 ? (
        <div className={styles.emptyActions}>
          {actions.map((action) => (
            <Link
              key={action.href}
              href={action.href}
              className={`btn ${action.primary ? "btn-primary" : "btn-ghost"}`}
            >
              {action.label}
            </Link>
          ))}
        </div>
      ) : null}
    </div>
  );
}

/** Upsell surface for feature-gated developer pages — never an error. */
export function UpsellCard({
  title,
  body,
  children,
}: {
  title: string;
  body?: string;
  children?: ReactNode;
}) {
  return (
    <div className={styles.upsell}>
      <span className={styles.upsellBadge}>
        <ShellIcon name="sparkle" size={14} /> Pro/API
      </span>
      <h2 className={styles.emptyTitle} style={{ marginTop: 12 }}>
        {title}
      </h2>
      {body ? (
        <p className={styles.emptyBody} style={{ marginTop: 6 }}>
          {body}
        </p>
      ) : null}
      <div style={{ marginTop: 16 }}>
        {children ?? (
          <Link href="/pricing" className="btn btn-primary">
            See Pro/API — NZ$19.90/mo
          </Link>
        )}
      </div>
    </div>
  );
}

export function Skeleton({ lines = 3 }: { lines?: number }) {
  return (
    <div className={styles.skeleton} aria-hidden="true">
      {Array.from({ length: lines }).map((_, index) => (
        <div
          key={index}
          className={styles.skelLine}
          style={{ width: `${92 - index * 12}%` }}
        />
      ))}
    </div>
  );
}
