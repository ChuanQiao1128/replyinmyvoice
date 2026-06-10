"use client";

import { useEffect, useRef } from "react";

import { ShellNavGroups } from "./app-sidebar";
import { ShellIcon } from "./shell-icons";
import styles from "./shell.module.css";

type Props = {
  open: boolean;
  onClose: () => void;
};

export function AppDrawer({ open, onClose }: Props) {
  const panelRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) {
      return;
    }
    const previouslyFocused = document.activeElement as HTMLElement | null;
    panelRef.current?.focus();

    function onKey(event: KeyboardEvent) {
      if (event.key === "Escape") {
        onClose();
        return;
      }
      if (event.key !== "Tab" || !panelRef.current) {
        return;
      }
      const focusables = panelRef.current.querySelectorAll<HTMLElement>(
        'a[href], button:not([disabled])',
      );
      if (focusables.length === 0) {
        return;
      }
      const first = focusables[0];
      const last = focusables[focusables.length - 1];
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first.focus();
      }
    }

    document.addEventListener("keydown", onKey);
    const { overflow } = document.body.style;
    document.body.style.overflow = "hidden";
    return () => {
      document.removeEventListener("keydown", onKey);
      document.body.style.overflow = overflow;
      previouslyFocused?.focus?.();
    };
  }, [open, onClose]);

  if (!open) {
    return null;
  }

  return (
    <>
      <div
        className={styles.drawerBackdrop}
        onClick={onClose}
        aria-hidden="true"
      />
      <div
        className={styles.drawerPanel}
        ref={panelRef}
        role="dialog"
        aria-modal="true"
        aria-label="Workspace navigation"
        tabIndex={-1}
      >
        <div className={styles.drawerHead}>
          <span className={styles.brand}>
            <span className={styles.brandMark} aria-hidden="true">
              R
            </span>
            <span className={styles.brandText}>Reply In My Voice</span>
          </span>
          <button
            type="button"
            className={styles.drawerClose}
            aria-label="Close menu"
            onClick={onClose}
          >
            <ShellIcon name="close" size={18} />
          </button>
        </div>
        <ShellNavGroups onNavigate={onClose} />
      </div>
    </>
  );
}
