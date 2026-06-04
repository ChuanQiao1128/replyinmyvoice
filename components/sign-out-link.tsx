"use client";

import type { MouseEvent } from "react";

import {
  clearAllLocalRewriteHistory,
  clearLocalRewriteHistory,
} from "../lib/rewrite-history";

type Props = {
  rewriteHistoryUserKey?: string;
};

export function SignOutLink({ rewriteHistoryUserKey }: Props) {
  function signOut(event: MouseEvent<HTMLAnchorElement>) {
    event.preventDefault();
    clearLocalRewriteHistory(rewriteHistoryUserKey);
    clearAllLocalRewriteHistory();
    window.location.assign("/api/auth/logout");
  }

  return (
    <a href="/api/auth/logout" onClick={signOut}>
      Sign out
    </a>
  );
}
