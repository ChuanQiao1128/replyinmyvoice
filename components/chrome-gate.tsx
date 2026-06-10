"use client";

import type { ReactNode } from "react";
import { usePathname } from "next/navigation";

const HIDDEN_PREFIXES = ["/app", "/admin"];

/**
 * Hides marketing chrome (the site footer) on the signed-in app shell and
 * admin backoffice, which provide their own layout.
 */
export function ChromeGate({ children }: { children: ReactNode }) {
  const pathname = usePathname() ?? "/";
  const hidden = HIDDEN_PREFIXES.some(
    (prefix) => pathname === prefix || pathname.startsWith(`${prefix}/`),
  );
  return hidden ? null : <>{children}</>;
}
