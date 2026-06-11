"use client";

import { Check, Clipboard } from "lucide-react";
import { useEffect, useRef, useState } from "react";

type CopyState = "idle" | "copied" | "failed";

type McpConfigCopyButtonProps = {
  label: string;
  text: string;
};

export function McpConfigCopyButton({ label, text }: McpConfigCopyButtonProps) {
  const [copyState, setCopyState] = useState<CopyState>("idle");
  const resetTimerRef = useRef<number | null>(null);

  useEffect(() => {
    return () => {
      if (resetTimerRef.current !== null) {
        window.clearTimeout(resetTimerRef.current);
      }
    };
  }, []);

  async function copyConfig() {
    if (
      !navigator.clipboard ||
      typeof navigator.clipboard.writeText !== "function"
    ) {
      setTemporaryState("failed");
      return;
    }

    try {
      await navigator.clipboard.writeText(text);
      setTemporaryState("copied");
    } catch {
      setTemporaryState("failed");
    }
  }

  function setTemporaryState(nextState: CopyState) {
    setCopyState(nextState);

    if (resetTimerRef.current !== null) {
      window.clearTimeout(resetTimerRef.current);
    }

    resetTimerRef.current = window.setTimeout(() => {
      setCopyState("idle");
      resetTimerRef.current = null;
    }, 1800);
  }

  const Icon = copyState === "copied" ? Check : Clipboard;
  const labelText =
    copyState === "copied"
      ? "Copied"
      : copyState === "failed"
        ? "Copy failed"
        : "Copy";

  return (
    <button
      aria-label={label}
      className={`api-copy-button${copyState === "copied" ? " is-copied" : ""}`}
      onClick={() => void copyConfig()}
      type="button"
    >
      <Icon aria-hidden="true" size={14} strokeWidth={2} />
      <span aria-live="polite">{labelText}</span>
    </button>
  );
}
