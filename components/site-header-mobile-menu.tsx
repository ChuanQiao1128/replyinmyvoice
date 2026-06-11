"use client";

import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { Menu } from "lucide-react";

import { SignOutLink } from "./sign-out-link";

type Props = {
  isAdmin: boolean;
  primaryCta: {
    href: string;
    label: string;
  };
  signedIn: boolean;
  signOutUserKey?: string;
};

const panelId = "site-mobile-nav-panel";

export function SiteHeaderMobileMenu({
  isAdmin,
  primaryCta,
  signedIn,
  signOutUserKey,
}: Props) {
  const [open, setOpen] = useState(false);
  const panelRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    if (!open) {
      return;
    }

    const firstLink = panelRef.current?.querySelector<HTMLElement>("a[href]");
    firstLink?.focus();

    function onKeyDown(event: KeyboardEvent) {
      if (event.key !== "Escape") {
        return;
      }
      event.preventDefault();
      setOpen(false);
      triggerRef.current?.focus();
    }

    function onPointerDown(event: MouseEvent) {
      const target = event.target as Node;
      if (
        panelRef.current?.contains(target) ||
        triggerRef.current?.contains(target)
      ) {
        return;
      }
      setOpen(false);
    }

    document.addEventListener("keydown", onKeyDown);
    document.addEventListener("mousedown", onPointerDown);
    return () => {
      document.removeEventListener("keydown", onKeyDown);
      document.removeEventListener("mousedown", onPointerDown);
    };
  }, [open]);

  function closeMenu() {
    setOpen(false);
  }

  return (
    <div className="mobile-nav-menu" data-open={open ? "true" : undefined}>
      <button
        type="button"
        className="mobile-nav-trigger"
        aria-label={open ? "Close menu" : "Open menu"}
        aria-expanded={open}
        aria-controls={panelId}
        onClick={() => setOpen((value) => !value)}
        ref={triggerRef}
      >
        <span>Menu</span>
        <Menu
          aria-hidden="true"
          className="mobile-nav-trigger-icon"
          focusable="false"
          size={16}
          strokeWidth={2}
        />
      </button>
      <div
        className="mobile-nav-panel"
        hidden={!open}
        id={panelId}
        ref={panelRef}
      >
        <Link href="/pricing" onClick={closeMenu}>
          Pricing
        </Link>
        <Link href="/developers" onClick={closeMenu}>
          Developers
        </Link>
        {!signedIn ? (
          <>
            <Link href="/sign-in" onClick={closeMenu}>
              Sign in
            </Link>
            <Link
              href={primaryCta.href}
              className="mobile-nav-cta"
              onClick={closeMenu}
            >
              {primaryCta.label}
            </Link>
          </>
        ) : (
          <>
            <Link href="/developers/keys" onClick={closeMenu}>
              API keys
            </Link>
            {isAdmin ? (
              <Link href="/admin" onClick={closeMenu}>
                Admin
              </Link>
            ) : null}
            <SignOutLink rewriteHistoryUserKey={signOutUserKey} />
            <Link
              href={primaryCta.href}
              className="mobile-nav-cta"
              onClick={closeMenu}
            >
              {primaryCta.label}
            </Link>
          </>
        )}
      </div>
    </div>
  );
}
