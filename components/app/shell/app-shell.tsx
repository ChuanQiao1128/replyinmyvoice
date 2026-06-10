"use client";

import type { ReactNode } from "react";
import { useCallback, useState } from "react";
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
  devModeDefault: boolean;
  children: ReactNode;
};

export function AppShell({ account, devModeDefault, children }: Props) {
  const [devMode, setDevMode] = useState(devModeDefault);
  const [drawerOpen, setDrawerOpen] = useState(false);

  const toggleDevMode = useCallback(() => {
    setDevMode((prev) => {
      const next = !prev;
      try {
        document.cookie = `rimv_devmode=${next ? "1" : "0"}; path=/; max-age=31536000; samesite=lax`;
        window.localStorage.setItem("rimv.devmode", next ? "1" : "0");
      } catch {
        /* storage unavailable — in-memory toggle still works for this session */
      }
      return next;
    });
  }, []);

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
            <QuotaPill quota={account.quota} />
            <Link href="/developers" className={styles.docsLink}>
              Docs
              <ShellIcon name="external" size={14} />
            </Link>
            <AccountMenu
              email={account.email}
              isAdmin={account.isAdmin}
              isDeveloperTier={account.isDeveloperTier}
              devMode={devMode}
              onToggleDevMode={toggleDevMode}
              rewriteHistoryUserKey={account.rewriteHistoryUserKey}
            />
          </div>
        </div>
      </header>

      <div className={styles.body}>
        <AppSidebar
          isDeveloperTier={account.isDeveloperTier}
          devMode={devMode}
        />
        <main className={styles.main}>
          <div className={styles.mainInner}>{children}</div>
        </main>
      </div>

      <AppDrawer
        open={drawerOpen}
        onClose={() => setDrawerOpen(false)}
        isDeveloperTier={account.isDeveloperTier}
        devMode={devMode}
      />
    </div>
  );
}
