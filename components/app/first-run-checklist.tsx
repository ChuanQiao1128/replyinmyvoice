"use client";

import {
  CheckCircle2,
  ChevronDown,
  Circle,
  Code2,
  Ticket,
  X,
} from "lucide-react";
import { useEffect, useState } from "react";

import { readLocalRewriteHistory } from "../../lib/rewrite-history";
import { Button, LinkButton } from "../ui/button";
import { SectionCard } from "./shell/shell-primitives";

export const FIRST_RUN_DISMISSAL_STORAGE_KEY = "rimv.firstrun.dismissed.v1";

type FirstRunChecklistProps = {
  canRedeem: boolean;
  onRedeemClick: () => void;
  rewriteBalance: number;
  rewriteHistoryUserKey: string;
  rewriteSucceededThisSession: boolean;
};

function storageDismissed() {
  try {
    return (
      window.localStorage.getItem(FIRST_RUN_DISMISSAL_STORAGE_KEY) === "true"
    );
  } catch {
    return false;
  }
}

function hasStoredRewriteHistory(userKey: string) {
  try {
    const storedHistory = readLocalRewriteHistory(userKey);
    if (!storedHistory?.trim()) {
      return false;
    }

    try {
      const parsed = JSON.parse(storedHistory) as unknown;
      if (Array.isArray(parsed)) {
        return parsed.length > 0;
      }
      if (parsed && typeof parsed === "object") {
        const items = (parsed as { items?: unknown }).items;
        return Array.isArray(items) ? items.length > 0 : true;
      }
    } catch {
      return true;
    }

    return true;
  } catch {
    return false;
  }
}

export function FirstRunChecklist({
  canRedeem,
  onRedeemClick,
  rewriteBalance,
  rewriteHistoryUserKey,
  rewriteSucceededThisSession,
}: FirstRunChecklistProps) {
  const [dismissed, setDismissed] = useState(false);
  const [dismissalChecked, setDismissalChecked] = useState(false);
  const [developerOpen, setDeveloperOpen] = useState(false);
  const [hasRewriteHistory, setHasRewriteHistory] = useState(false);

  useEffect(() => {
    setDismissed(storageDismissed());
    setDismissalChecked(true);
  }, []);

  useEffect(() => {
    setHasRewriteHistory(hasStoredRewriteHistory(rewriteHistoryUserKey));
  }, [rewriteHistoryUserKey]);

  const hasRewriteBalance = rewriteBalance > 0;
  const firstRewriteDone = hasRewriteHistory || rewriteSucceededThisSession;
  const allRequiredStepsDone = hasRewriteBalance && firstRewriteDone;

  function dismissChecklist() {
    try {
      window.localStorage.setItem(FIRST_RUN_DISMISSAL_STORAGE_KEY, "true");
    } catch {
      // Non-critical; this checklist must never block the workspace.
    }
    setDismissed(true);
  }

  if (!dismissalChecked || dismissed || allRequiredStepsDone) {
    return null;
  }

  const checklistSteps = [
    {
      body: "Start by choosing how to fund your workspace: redeem a trial code (or buy a pack).",
      done: hasRewriteBalance,
      title: "Get rewrites",
    },
    {
      body: "Paste a rough reply, run Rewrite, and copy the version you want to send.",
      done: firstRewriteDone,
      title: "First rewrite",
    },
  ];

  return (
    <div className="mb-6">
      <SectionCard>
        <div className="flex flex-col gap-5 md:flex-row md:items-start md:justify-between">
          <div className="min-w-0">
            <p className="font-mono text-[11px] font-semibold uppercase tracking-[0.18em] text-sage">
              First run
            </p>
            <h2 className="mt-2 text-lg font-semibold text-ink">
              Set up your writing desk
            </h2>
            <p className="mt-1 max-w-2xl text-sm leading-6 text-ink/60">
              Two steps get the workspace ready for your first sendable reply.
            </p>
          </div>
          <Button
            aria-label="Dismiss first-run checklist"
            className="min-h-11 w-11 shrink-0 p-0 text-ink/45 hover:bg-paper hover:text-ink"
            onClick={dismissChecklist}
            type="button"
            variant="ghost"
          >
            <X className="h-4 w-4" aria-hidden="true" />
          </Button>
        </div>

        <ol className="mt-5 grid gap-3 md:grid-cols-2">
          {checklistSteps.map((step) => (
            <li
              className="flex min-w-0 gap-3 rounded-lg border border-line bg-paper/60 p-4"
              key={step.title}
            >
              <span
                className={`mt-0.5 flex h-6 w-6 shrink-0 items-center justify-center rounded-lg ${
                  step.done ? "bg-mint text-sage" : "bg-white text-ink/35"
                }`}
              >
                {step.done ? (
                  <CheckCircle2 className="h-4 w-4" aria-hidden="true" />
                ) : (
                  <Circle className="h-4 w-4" aria-hidden="true" />
                )}
              </span>
              <span className="min-w-0">
                <span className="block font-semibold text-ink">
                  {step.title}
                </span>
                <span className="mt-1 block text-sm leading-6 text-ink/60">
                  {step.body}
                </span>
              </span>
            </li>
          ))}
        </ol>

        <div className="mt-5 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex flex-col gap-2 sm:flex-row">
            {canRedeem ? (
              <Button
                className="w-full sm:w-auto"
                onClick={onRedeemClick}
                type="button"
                variant="secondary"
              >
                <Ticket className="h-4 w-4" aria-hidden="true" />
                Enter trial code
              </Button>
            ) : null}
            <LinkButton
              className="w-full sm:w-auto"
              href="/pricing"
              variant="primary"
            >
              Buy a pack
            </LinkButton>
          </div>
          <Button
            aria-controls="first-run-developer-links"
            aria-expanded={developerOpen}
            className="justify-between px-0 text-ink/65 hover:bg-transparent hover:text-ink sm:px-3"
            onClick={() => setDeveloperOpen((open) => !open)}
            type="button"
            variant="ghost"
          >
            <span className="inline-flex items-center gap-2">
              <Code2 className="h-4 w-4" aria-hidden="true" />
              For developers
            </span>
            <ChevronDown
              className={`h-4 w-4 transition ${
                developerOpen ? "rotate-180" : ""
              }`}
              aria-hidden="true"
            />
          </Button>
        </div>

        {developerOpen ? (
          <div
            className="mt-3 flex flex-col gap-2 rounded-lg border border-line bg-white/70 p-4 text-sm text-ink/60 sm:flex-row sm:items-center sm:justify-between"
            id="first-run-developer-links"
          >
            <span>Optional API setup for product integrations.</span>
            <span className="flex flex-col gap-2 sm:flex-row">
              <LinkButton href="/app/keys" variant="secondary">
                API keys
              </LinkButton>
              <LinkButton href="/app/connect" variant="secondary">
                Connect
              </LinkButton>
            </span>
          </div>
        ) : null}
      </SectionCard>
    </div>
  );
}
