"use client";

import { CheckCircle2, Loader2, ShieldCheck, Ticket, X } from "lucide-react";
import { useRouter } from "next/navigation";
import { FormEvent, useEffect, useRef, useState } from "react";

import { trialExpiryLabel } from "../../lib/promo-app-state";
import { Button } from "../ui/button";
import { Card } from "../ui/card";
import { Input } from "../ui/input";

const turnstileScriptSrc =
  "https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit";
const turnstileTestSiteKey = "1x00000000000000000000AA";

type TurnstileRenderOptions = {
  callback: (token: string) => void;
  "error-callback": () => void;
  "expired-callback": () => void;
  sitekey: string;
  size: "flexible";
};

type TurnstileApi = {
  remove: (widgetId: string) => void;
  render: (container: HTMLElement, options: TurnstileRenderOptions) => string;
  reset: (widgetId: string) => void;
};

type WindowWithTurnstile = Window &
  typeof globalThis & {
    turnstile?: TurnstileApi;
  };

type PromoRedeemSuccess = {
  creditsGranted?: number;
  expiresAt?: string | null;
};

type RedeemCodeCardProps = {
  open?: boolean;
  onClose?: () => void;
};

let turnstileScriptPromise: Promise<void> | null = null;

function browserWindow() {
  return window as WindowWithTurnstile;
}

function getTurnstileSiteKey() {
  const configuredSiteKey = (process.env.NEXT_PUBLIC_TURNSTILE_SITE_KEY ?? "0x4AAAAAADdY3Xy1e6vEJU8E").trim();
  if (configuredSiteKey) {
    return configuredSiteKey;
  }

  return process.env.NODE_ENV === "production" ? "" : turnstileTestSiteKey;
}

function loadTurnstileScript() {
  if (browserWindow().turnstile) {
    return Promise.resolve();
  }

  if (turnstileScriptPromise) {
    return turnstileScriptPromise;
  }

  turnstileScriptPromise = new Promise((resolve, reject) => {
    const existing = document.querySelector<HTMLScriptElement>(
      `script[src="${turnstileScriptSrc}"]`,
    );
    if (existing) {
      existing.addEventListener("load", () => resolve(), { once: true });
      existing.addEventListener("error", () => reject(new Error("load")), {
        once: true,
      });
      return;
    }

    const script = document.createElement("script");
    script.async = true;
    script.defer = true;
    script.src = turnstileScriptSrc;
    script.addEventListener("load", () => resolve(), { once: true });
    script.addEventListener("error", () => reject(new Error("load")), {
      once: true,
    });
    document.head.appendChild(script);
  });

  return turnstileScriptPromise;
}

function payloadString(payload: unknown, key: string) {
  if (payload === null || typeof payload !== "object" || Array.isArray(payload)) {
    return undefined;
  }

  const value = (payload as Record<string, unknown>)[key];
  return typeof value === "string" ? value : undefined;
}

function promoSuccessFromPayload(payload: unknown): PromoRedeemSuccess {
  if (payload === null || typeof payload !== "object" || Array.isArray(payload)) {
    return {};
  }

  const record = payload as Record<string, unknown>;
  return {
    creditsGranted:
      typeof record.creditsGranted === "number" ? record.creditsGranted : undefined,
    expiresAt: typeof record.expiresAt === "string" ? record.expiresAt : null,
  };
}

export function messageForPromoError(error: string | undefined) {
  switch (error) {
    case "invalid_code":
      return "That code is not valid. Check it and try again.";
    case "code_expired":
      return "That code has expired.";
    case "already_redeemed":
      return "This account has already redeemed a trial code.";
    case "code_exhausted":
      return "That code has no trial credits left.";
    case "ip_velocity":
      return "Too many redemption attempts from this network. Try again later.";
    case "invalid_captcha":
      return "Complete the verification check and try again.";
    case "server_config":
    case "server_error":
      return "Redeem is unavailable right now. Try again later.";
    default:
      return "Could not redeem that code. Try again.";
  }
}

async function readJsonPayload(response: Response) {
  try {
    return await response.json();
  } catch {
    return null;
  }
}

export function RedeemCodeCard({ open = true, onClose }: RedeemCodeCardProps = {}) {
  const router = useRouter();
  const [code, setCode] = useState("");
  const [turnstileToken, setTurnstileToken] = useState("");
  const [captchaError, setCaptchaError] = useState("");
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [loading, setLoading] = useState(false);
  const turnstileContainerRef = useRef<HTMLDivElement>(null);
  const turnstileWidgetIdRef = useRef<string | null>(null);
  const siteKey = getTurnstileSiteKey();

  useEffect(() => {
    if (!open) {
      return;
    }

    setCode("");
    setTurnstileToken("");
    setCaptchaError("");
    setError("");
    setSuccess("");
  }, [open]);

  useEffect(() => {
    if (!open) {
      return;
    }

    const container = turnstileContainerRef.current;
    if (!container) {
      return;
    }

    if (!siteKey) {
      setCaptchaError("Verification is not configured yet.");
      return;
    }

    let active = true;
    loadTurnstileScript()
      .then(() => {
        const turnstile = browserWindow().turnstile;
        if (!active || !turnstile || turnstileWidgetIdRef.current) {
          return;
        }

        turnstileWidgetIdRef.current = turnstile.render(container, {
          callback: (token) => {
            setTurnstileToken(token);
            setCaptchaError("");
          },
          "error-callback": () => {
            setTurnstileToken("");
            setCaptchaError("Verification failed. Try again.");
          },
          "expired-callback": () => {
            setTurnstileToken("");
            setCaptchaError("Verification expired. Complete it again.");
          },
          sitekey: siteKey,
          size: "flexible",
        });
      })
      .catch(() => {
        if (active) {
          setCaptchaError("Verification could not load. Refresh and try again.");
        }
      });

    return () => {
      active = false;
      const widgetId = turnstileWidgetIdRef.current;
      const turnstile = browserWindow().turnstile;
      if (widgetId && turnstile) {
        turnstile.remove(widgetId);
        turnstileWidgetIdRef.current = null;
      }
    };
  }, [open, siteKey]);

  function resetTurnstile() {
    const widgetId = turnstileWidgetIdRef.current;
    const turnstile = browserWindow().turnstile;
    if (widgetId && turnstile) {
      turnstile.reset(widgetId);
    }
    setTurnstileToken("");
  }

  async function refetchAccountSummary() {
    await fetch("/api/me", {
      cache: "no-store",
    }).catch(() => null);
    router.refresh();
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const trimmedCode = code.trim();
    if (!trimmedCode || loading) {
      return;
    }

    if (!turnstileToken) {
      setError(messageForPromoError("invalid_captcha"));
      return;
    }

    setLoading(true);
    setError("");
    setSuccess("");

    try {
      const response = await fetch("/api/promo/redeem", {
        body: JSON.stringify({
          code: trimmedCode,
          turnstileToken,
        }),
        headers: {
          "Content-Type": "application/json",
        },
        method: "POST",
      });
      const payload = await readJsonPayload(response);

      if (!response.ok) {
        const errorCode =
          payloadString(payload, "error") ?? payloadString(payload, "code");
        setError(messageForPromoError(errorCode));
        resetTurnstile();
        return;
      }

      const result = promoSuccessFromPayload(payload);
      const creditsGranted = result.creditsGranted ?? 3;
      const expiry = trialExpiryLabel(result.expiresAt ?? null);
      const successPrefix =
        creditsGranted === 3 ? "3 rewrites unlocked" : `${creditsGranted} rewrites unlocked`;
      setSuccess(
        `${successPrefix}${expiry ? ` — ${expiry}` : ""}`,
      );
      await refetchAccountSummary();
      onClose?.();
    } catch {
      setError("Could not redeem that code. Try again.");
      resetTurnstile();
    } finally {
      setLoading(false);
    }
  }

  const canSubmit = Boolean(code.trim()) && Boolean(turnstileToken) && !loading;

  if (!open) {
    return null;
  }

  return (
    <div
      aria-labelledby="promo-redeem-title"
      aria-modal="true"
      className="fixed inset-0 z-50 grid place-items-center overflow-y-auto bg-ink/35 px-4 py-6 text-ink"
      role="dialog"
    >
      <div className="w-full max-w-3xl">
        <Card className="grid overflow-hidden p-0 md:grid-cols-[minmax(0,1fr)_260px]">
          <div className="p-5 md:p-7">
            <div className="mb-5 flex items-start justify-between gap-4">
              <div className="flex h-12 w-12 items-center justify-center rounded-md bg-paper-deep text-clay">
                <Ticket className="h-5 w-5" aria-hidden="true" />
              </div>
              {onClose ? (
                <button
                  aria-label="Close"
                  className="rounded-md p-1.5 text-ink/45 transition hover:bg-paper hover:text-ink"
                  onClick={onClose}
                  type="button"
                >
                  <X className="h-4 w-4" aria-hidden="true" />
                </button>
              ) : null}
            </div>
            <p className="text-xs font-semibold uppercase tracking-[0.16em] text-clay">
              Trial code
            </p>
            <h1
              className="mt-3 text-2xl font-semibold md:text-3xl"
              id="promo-redeem-title"
            >
              Redeem your code
            </h1>
            <p className="mt-3 max-w-2xl leading-7 text-ink/65">
              Enter your trial code to unlock 3 rewrites for your workspace.
            </p>

            <form className="mt-6 max-w-xl space-y-4" onSubmit={submit}>
              <div>
                <label
                  className="text-sm font-semibold text-ink/70"
                  htmlFor="promo-code"
                >
                  Code
                </label>
                <Input
                  autoComplete="one-time-code"
                  autoFocus
                  className="mt-2 uppercase tracking-[0.08em]"
                  id="promo-code"
                  onChange={(event) => {
                    setCode(event.target.value);
                    setError("");
                    setSuccess("");
                  }}
                  placeholder="Enter your code"
                  type="text"
                  value={code}
                />
              </div>

              <div>
                <div
                  className="min-h-[70px] w-full max-w-[300px] overflow-hidden rounded-md border border-line bg-paper"
                  data-testid="turnstile-widget"
                  ref={turnstileContainerRef}
                />
                {captchaError ? (
                  <p className="mt-2 text-sm text-red-700">{captchaError}</p>
                ) : null}
              </div>

              <Button disabled={!canSubmit} type="submit" variant="clay">
                {loading ? (
                  <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
                ) : (
                  <CheckCircle2 className="h-4 w-4" aria-hidden="true" />
                )}
                Redeem
              </Button>

              <div aria-live="polite">
                {success ? (
                  <p className="text-sm font-semibold text-sage">{success}</p>
                ) : null}
                {error ? <p className="text-sm text-red-700">{error}</p> : null}
              </div>
            </form>
          </div>

          <aside className="border-t border-line bg-paper/70 p-5 md:border-l md:border-t-0">
            <div className="flex h-10 w-10 items-center justify-center rounded-md bg-mint text-sage">
              <ShieldCheck className="h-5 w-5" aria-hidden="true" />
            </div>
            <p className="mt-4 text-sm font-medium text-ink/60">
              Reply In My Voice
            </p>
            <p className="mt-2 text-2xl font-semibold">Trial rewrites</p>
            <p className="mt-2 text-sm leading-6 text-ink/60">
              Trial credits are available after a successful code redemption.
              Successful rewrites count against the trial balance.
            </p>
          </aside>
        </Card>
      </div>
    </div>
  );
}
