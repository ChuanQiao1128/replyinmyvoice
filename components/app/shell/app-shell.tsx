"use client";

import type { ReactNode } from "react";
import { useState } from "react";
import Link from "next/link";

import { AccountMenu } from "./account-menu";
import { AppDrawer } from "./app-drawer";
import { AppSidebar } from "./app-sidebar";
import { QuotaPill } from "./quota-pill";
import { ShellIcon } from "./shell-icons";
import type { ShellAccount } from "./shell-types";
import styles from "./shell.module.css";

type Props = {
  account: ShellAccount;
  children: ReactNode;
};

export function AppShell({ account, children }: Props) {
  const [drawerOpen, setDrawerOpen] = useState(false);

  return (
    <div className={styles.shell}>
      <header className={styles.topbar}>
        <div className={styles.topbarInner}>
          <button
            type="button"
            className={styles.hamburger}
            aria-label="Open menu"
            aria-expanded={drawerOpen}
            onClick={() => setDrawerOpen(true)}
          >
            <ShellIcon name="menu" size={20} />
          </button>
          <Link href="/app" className={styles.brand}>
            <span className={styles.brandMark} aria-hidden="true">
              R
            </span>
            <span className={styles.brandText}>Reply In My Voice</span>
          </Link>
          <div className={styles.topbarRight}>
            <QuotaPill paid={account.isDeveloperTier} quota={account.quota} />
            <Link href="/developers" className={styles.docsLink}>
              Docs
              <ShellIcon name="external" size={14} />
            </Link>
            <AccountMenu
              email={account.email}
              isAdmin={account.isAdmin}
              rewriteHistoryUserKey={account.rewriteHistoryUserKey}
            />
          </div>
        </div>
      </header>

      <div className={styles.body}>
        <AppSidebar />
        <main className={styles.main}>
          <div className={styles.mainInner}>{children}</div>
        </main>
      </div>

      <AppDrawer open={drawerOpen} onClose={() => setDrawerOpen(false)} />
    </div>
  );
}
