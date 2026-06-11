import type { ReactNode } from "react";
import Link from "next/link";

import { ShellIcon } from "./shell-icons";
import type { ShellIconName } from "./shell-types";
import styles from "./shell.module.css";

/** Page heading used at the top of each shell route, with an optional action slot. */
export function PageHeader({
  title,
  description,
  actions,
}: {
  title: string;
  description?: string;
  actions?: ReactNode;
}) {
  const heading = (
    <header className={styles.pageHeader}>
      <h1 className={styles.pageTitle}>{title}</h1>
      {description ? <p className={styles.pageDesc}>{description}</p> : null}
    </header>
  );

  if (!actions) {
    return heading;
  }

  return (
    <div className={styles.pageHeaderRow}>
      {heading}
      <div>{actions}</div>
    </div>
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

/**
 * Shared gate for the developer pages (Keys / Usage / Connect): the features
 * stay visible to everyone, and accounts without API access are invited to
 * subscribe — never shown an error.
 */
export function DeveloperUpsell() {
  return (
    <UpsellCard
      title="API & MCP access comes with Pro/API"
      body="One key powers both the REST API and the MCP server — use Reply In My Voice from your own product or inside Claude Code, Claude Desktop, Codex, and Cursor. API calls share the same balance as your web rewrites."
    >
      <ul
        style={{
          margin: "0 0 16px",
          padding: 0,
          listStyle: "none",
          display: "grid",
          gap: 8,
          fontSize: 14,
          color: "var(--ink-2)",
        }}
      >
        <li>• 90 rewrites per month, shared across web + API</li>
        <li>• REST API with async jobs and an OpenAPI spec</li>
        <li>• MCP server for Claude Code, Claude Desktop, Codex, Cursor</li>
        <li>• Usage dashboard, key rotation, and webhooks</li>
      </ul>
      <div style={{ display: "flex", gap: 10, flexWrap: "wrap" }}>
        <Link href="/pricing" className="btn btn-primary">
          Get Pro/API — NZ$19.90/mo
        </Link>
        <Link href="/developers/api" className="btn btn-ghost">
          Read API docs
        </Link>
      </div>
    </UpsellCard>
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
