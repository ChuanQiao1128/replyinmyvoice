"use client";

import { useEffect } from "react";

const apiReferenceHashes = new Set([
  "#quickstart",
  "#auth",
  "#api",
  "#errors",
  "#guides",
  "#pricing",
]);

export function DevelopersAnchorRedirect() {
  useEffect(() => {
    const hash = window.location.hash;

    if (apiReferenceHashes.has(hash)) {
      window.location.replace(`/developers/api${hash}`);
    }
  }, []);

  return null;
}
