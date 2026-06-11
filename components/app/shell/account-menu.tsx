"use client";

import { useEffect, useId, useRef, useState } from "react";
import Link from "next/link";

import {
  clearAllLocalRewriteHistory,
  clearLocalRewriteHistory,
} from "../../../lib/rewrite-history";
import { ShellIcon } from "./shell-icons";
import styles from "./shell.module.css";

type Props = {
  email: string | null;
  isAdmin: boolean;
  planLabel: string;
  rewriteHistoryUserKey?: string;
};

export function AccountMenu({
  email,
  isAdmin,
  planLabel,
  rewriteHistoryUserKey,
}: Props) {
  const [open, setOpen] = useState(false);
  const wrapRef = useRef<HTMLDivElement>(null);
  const menuId = useId();
  const initial = (email?.trim()?.[0] ?? "U").toUpperCase();

  useEffect(() => {
    if (!open) {
      return;
    }
    function onPointer(event: MouseEvent) {
      if (wrapRef.current && !wrapRef.current.contains(event.target as Node)) {
        setOpen(false);
      }
    }
    function onKey(event: KeyboardEvent) {
      if (event.key === "Escape") {
        setOpen(false);
      }
    }
    document.addEventListener("mousedown", onPointer);
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("mousedown", onPointer);
      document.removeEventListener("keydown", onKey);
    };
  }, [open]);

  function signOut() {
    clearLocalRewriteHistory(rewriteHistoryUserKey);
    clearAllLocalRewriteHistory();
    window.location.assign("/api/auth/logout");
  }

  return (
    <div className={styles.accountWrap} ref={wrapRef}>
      <button
        type="button"
        className={styles.accountBtn}
        aria-haspopup="menu"
        aria-expanded={open}
        aria-controls={menuId}
        onClick={() => setOpen((value) => !value)}
      >
        <span className={styles.avatar} aria-hidden="true">
          {initial}
        </span>
        <ShellIcon name="chevron" size={15} />
      </button>

      {open ? (
        <div className={styles.menu} id={menuId} role="menu">
          <div className={styles.menuIdentity}>
            {email ? <div className={styles.menuEmail}>{email}</div> : null}
            <span className={styles.menuPlanBadge}>{planLabel}</span>
          </div>

          <Link
            href="/app/account"
            role="menuitem"
            className={styles.menuItem}
            onClick={() => setOpen(false)}
          >
            <ShellIcon name="user" /> Account &amp; billing
          </Link>
          {isAdmin ? (
            <Link
              href="/admin"
              role="menuitem"
              className={styles.menuItem}
              onClick={() => setOpen(false)}
            >
              <ShellIcon name="shield" /> Admin
            </Link>
          ) : null}

          <div className={styles.menuDivider} />

          <button
            type="button"
            role="menuitem"
            className={styles.menuItem}
            onClick={signOut}
          >
            <ShellIcon name="external" /> Sign out
          </button>
        </div>
      ) : null}
    </div>
  );
}
