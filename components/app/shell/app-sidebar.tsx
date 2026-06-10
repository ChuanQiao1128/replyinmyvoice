"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";

import { ShellIcon } from "./shell-icons";
import { isNavItemActive, SHELL_NAV } from "./shell-types";
import styles from "./shell.module.css";

type NavProps = {
  onNavigate?: () => void;
};

/** Shared nav rendering for both the desktop sidebar and the mobile drawer. */
export function ShellNavGroups({ onNavigate }: NavProps) {
  const pathname = usePathname() ?? "/app";

  return (
    <>
      {SHELL_NAV.map((group) => (
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

export function AppSidebar() {
  return (
    <nav className={styles.sidebar} aria-label="Workspace navigation">
      <ShellNavGroups />
    </nav>
  );
}
