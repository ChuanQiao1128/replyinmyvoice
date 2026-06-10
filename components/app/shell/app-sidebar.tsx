"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";

import { ShellIcon } from "./shell-icons";
import { isNavItemActive, visibleNavGroups } from "./shell-types";
import styles from "./shell.module.css";

type NavProps = {
  isDeveloperTier: boolean;
  devMode: boolean;
  onNavigate?: () => void;
};

/** Shared nav rendering for both the desktop sidebar and the mobile drawer. */
export function ShellNavGroups({
  isDeveloperTier,
  devMode,
  onNavigate,
}: NavProps) {
  const pathname = usePathname() ?? "/app";
  const groups = visibleNavGroups(isDeveloperTier, devMode);

  return (
    <>
      {groups.map((group) => (
        <div key={group.id} className={styles.group}>
          <div className={styles.groupLabel}>{group.label}</div>
          {group.items.map((item) => {
            const active = isNavItemActive(pathname, item.href);
            return (
              <Link
                key={item.href}
                href={item.href}
                className={`${styles.navItem} ${active ? styles.navItemActive : ""}`}
                aria-current={active ? "page" : undefined}
                onClick={onNavigate}
              >
                <span className={styles.navIcon}>
                  <ShellIcon name={item.icon} />
                </span>
                <span>{item.label}</span>
              </Link>
            );
          })}
        </div>
      ))}
    </>
  );
}

export function AppSidebar({ isDeveloperTier, devMode }: NavProps) {
  return (
    <nav className={styles.sidebar} aria-label="Workspace navigation">
      <ShellNavGroups isDeveloperTier={isDeveloperTier} devMode={devMode} />
    </nav>
  );
}
