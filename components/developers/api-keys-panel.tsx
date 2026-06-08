"use client";

import {
  AlertCircle,
  CheckCircle2,
  Clipboard,
  CreditCard,
  FlaskConical,
  KeyRound,
  Link2,
  Loader2,
  Plus,
  RefreshCw,
  Save,
  ShieldCheck,
  Trash2,
  X,
} from "lucide-react";
import React, {
  type FormEvent,
  useCallback,
  useEffect,
  useMemo,
  useState,
} from "react";

import { Button, LinkButton } from "../ui/button";
import { Card } from "../ui/card";
import { Input } from "../ui/input";

type ApiKey = {
  createdAt: string;
  id: string;
  isTest: boolean;
  lastUsedAt: string | null;
  last30dUsage: ApiUsageCount;
  maskedKey: string;
  name: string;
  revokedAt: string | null;
  webhookUrl: string | null;
};

type ApiUsageCount = {
  calls: number;
  failed: number;
  succeeded: number;
};

type CreatedApiKey = {
  createdAt: string;
  id: string;
  isTest: boolean;
  key: string;
  name: string;
};

type RevealedWebhookSecret = {
  keyId: string;
  keyName: string;
  secret: string;
  webhookUrl: string;
};

type KeysState =
  | { status: "loading" }
  | { status: "ready"; keys: ApiKey[] }
  | { status: "error"; message: string };

type UsageBalanceSummary = {
  periodEnd?: string | null;
  quota: number;
  remaining: number;
  used: number;
};

type UsageBalanceState =
  | { status: "loading" }
  | { status: "ready"; summary: UsageBalanceSummary }
  | { status: "error"; message: string };

const emptyUsageBalanceSummary: UsageBalanceSummary = {
  periodEnd: null,
  quota: 0,
  remaining: 0,
  used: 0,
};

function formatDateTime(value: string | null, emptyLabel = "Not recorded") {
  if (!value) {
    return emptyLabel;
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return emptyLabel;
  }

  return new Intl.DateTimeFormat(undefined, {
    day: "numeric",
    hour: "numeric",
    minute: "2-digit",
    month: "short",
    year: "numeric",
  }).format(date);
}

function formatPeriodEnd(value: string | null | undefined) {
  if (!value) {
    return "No period end recorded";
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return "No period end recorded";
  }

  return new Intl.DateTimeFormat(undefined, {
    day: "numeric",
    month: "long",
    year: "numeric",
  }).format(date);
}

function fallbackMaskedKey(value: string) {
  const trimmed = value.trim();
  if (trimmed.startsWith("rmv_test_") && trimmed.length >= 13) {
    return `rmv_test_\u2022\u2022\u2022\u2022${trimmed.slice(-4)}`;
  }

  if (trimmed.startsWith("rmv_live_") && trimmed.length >= 13) {
    return `rmv_live_\u2022\u2022\u2022\u2022${trimmed.slice(-4)}`;
  }

  if (trimmed.length <= 12) {
    return "****";
  }

  return `${trimmed.slice(0, 8)}...${trimmed.slice(-4)}`;
}

function safeNumber(value: number | undefined | null) {
  return Number.isFinite(value) && value && value > 0 ? value : 0;
}

function normalizeUsageBalanceSummary(
  summary: Partial<UsageBalanceSummary> | null,
): UsageBalanceSummary {
  if (!summary) {
    return emptyUsageBalanceSummary;
  }

  return {
    periodEnd: summary.periodEnd ?? null,
    quota: safeNumber(summary.quota),
    remaining: safeNumber(summary.remaining),
    used: safeNumber(summary.used),
  };
}

async function readJsonError(response: Response) {
  const payload = (await response.json().catch(() => null)) as {
    detail?: string;
    error?: string;
    title?: string;
  } | null;
  return payload?.error ?? payload?.detail ?? payload?.title;
}

async function loadKeys() {
  const response = await fetch("/api/keys", {
    cache: "no-store",
  });

  if (response.status === 401) {
    window.location.assign("/sign-in");
    return [];
  }

  if (!response.ok) {
    throw new Error((await readJsonError(response)) ?? "Could not load API keys.");
  }

  return (await response.json()) as ApiKey[];
}

async function loadUsageBalance() {
  const response = await fetch("/api/me/api-usage/summary", {
    cache: "no-store",
  });

  if (response.status === 401) {
    window.location.assign("/sign-in");
    return emptyUsageBalanceSummary;
  }

  if (!response.ok) {
    throw new Error(
      (await readJsonError(response)) ?? "Could not load credit balance.",
    );
  }

  return normalizeUsageBalanceSummary(
    (await response.json()) as Partial<UsageBalanceSummary> | null,
  );
}

function statusLabel(key: ApiKey) {
  return key.revokedAt ? "Revoked" : "Active";
}

function statusClassName(key: ApiKey) {
  return key.revokedAt
    ? "border-rust/25 bg-rust/10 text-rust"
    : "border-sage/25 bg-sky text-sage";
}

function emptyUsage(): ApiUsageCount {
  return {
    calls: 0,
    failed: 0,
    succeeded: 0,
  };
}

function usageDetailLabel(usage: ApiUsageCount) {
  if (usage.calls === 0) {
    return "No calls";
  }

  return `${usage.succeeded} ok / ${usage.failed} failed`;
}

export function createdKeyListItem(created: CreatedApiKey): ApiKey {
  return {
    createdAt: created.createdAt,
    id: created.id,
    isTest: created.isTest,
    lastUsedAt: null,
    last30dUsage: emptyUsage(),
    maskedKey: fallbackMaskedKey(created.key),
    name: created.name,
    revokedAt: null,
    webhookUrl: null,
  };
}

export function CreatedKeyReveal({
  copyNotice,
  onCopy,
  onDone,
  revealedKey,
}: {
  copyNotice: string | null;
  onCopy: () => void;
  onDone: () => void;
  revealedKey: CreatedApiKey;
}) {
  return (
    <section
      aria-labelledby="new-api-key-title"
      className="rounded-lg border border-sage/25 bg-white p-6 shadow-soft sm:p-8"
    >
      <div className="flex items-start gap-3">
        <CheckCircle2 className="mt-1 h-5 w-5 text-sage" aria-hidden="true" />
        <div className="min-w-0 flex-1">
          <h2 id="new-api-key-title" className="text-2xl">
            New key created
          </h2>
          <p className="mt-2 text-sm text-ink/65">
            {"Copy this plaintext key now. For account safety, you won't see this again."}
          </p>
          <div className="mt-4 rounded-md border border-line bg-paper px-4 py-3">
            <code className="block break-all font-mono text-sm text-ink">
              {revealedKey.key}
            </code>
          </div>
          {revealedKey.isTest ? (
            <span
              aria-label="Test key"
              className="mt-3 inline-flex rounded-full border border-gold/30 bg-gold/10 px-2.5 py-1 text-xs font-semibold text-ink"
            >
              Test
            </span>
          ) : null}
          <div className="mt-4 flex flex-col gap-3 sm:flex-row sm:items-center">
            <Button onClick={onCopy} type="button">
              <Clipboard className="h-4 w-4" aria-hidden="true" />
              Copy key
            </Button>
            <Button onClick={onDone} type="button" variant="secondary">
              Done
            </Button>
            {copyNotice ? (
              <p className="text-sm font-semibold text-sage">{copyNotice}</p>
            ) : null}
          </div>
        </div>
      </div>
    </section>
  );
}

export function MaskedApiKeyValue({ apiKey }: { apiKey: ApiKey }) {
  return (
    <div className="flex items-center gap-2">
      <span className="font-mono">{apiKey.maskedKey}</span>
      {apiKey.isTest ? (
        <span
          aria-label="Test key"
          className="inline-flex rounded-full border border-gold/30 bg-gold/10 px-2 py-0.5 font-sans text-[11px] font-semibold text-ink"
        >
          Test
        </span>
      ) : null}
    </div>
  );
}

function CreditBalanceCard({
  balanceState,
  onRefresh,
}: {
  balanceState: UsageBalanceState;
  onRefresh: () => void;
}) {
  const summary =
    balanceState.status === "ready"
      ? balanceState.summary
      : emptyUsageBalanceSummary;
  const exhausted =
    balanceState.status === "ready" && summary.remaining <= 0 && summary.quota > 0;

  return (
    <Card className="p-5">
      <div className="flex items-start gap-3">
        <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-md bg-sky text-sage">
          <CreditCard className="h-5 w-5" aria-hidden="true" />
        </div>
        <div className="min-w-0 flex-1">
          <p className="font-mono text-[11px] font-semibold uppercase tracking-[0.16em] text-sage">
            Credit balance
          </p>
          <h2 className="mt-3 text-2xl">Remaining credits</h2>
        </div>
      </div>

      {balanceState.status === "loading" ? (
        <div className="mt-5 flex items-center gap-3 rounded-md border border-line bg-paper px-4 py-4 text-sm text-ink/65">
          <Loader2 className="h-5 w-5 animate-spin" aria-hidden="true" />
          Loading credits...
        </div>
      ) : null}

      {balanceState.status === "error" ? (
        <div className="mt-5 rounded-md border border-rust/25 bg-rust/5 p-4">
          <p className="text-sm font-semibold text-rust">
            Could not load credits.
          </p>
          <p className="mt-1 text-sm text-ink/65">{balanceState.message}</p>
          <Button
            className="mt-4 w-full"
            onClick={onRefresh}
            type="button"
            variant="secondary"
          >
            <RefreshCw className="h-4 w-4" aria-hidden="true" />
            Retry
          </Button>
        </div>
      ) : null}

      {balanceState.status === "ready" ? (
        <div className="mt-5">
          <p className="text-4xl font-semibold tracking-normal text-ink">
            {summary.remaining}
            <span className="text-base font-medium text-ink/50">
              {" "}
              of {summary.quota}
            </span>
          </p>
          <p className="mt-2 text-sm text-ink/60">
            {summary.used} used this period. Period ends{" "}
            {formatPeriodEnd(summary.periodEnd)}.
          </p>
          {exhausted ? (
            <p className="mt-3 rounded-md border border-rust/25 bg-rust/5 px-3 py-2 text-sm font-semibold text-rust">
              No credits remaining.
            </p>
          ) : null}
        </div>
      ) : null}

      <LinkButton
        className="mt-5 w-full"
        href="/pricing"
        variant={exhausted ? "clay" : "secondary"}
      >
        Buy credits
      </LinkButton>
    </Card>
  );
}

export function ApiKeysPanel() {
  const [keysState, setKeysState] = useState<KeysState>({ status: "loading" });
  const [balanceState, setBalanceState] = useState<UsageBalanceState>({
    status: "loading",
  });
  const [name, setName] = useState("");
  const [createError, setCreateError] = useState<string | null>(null);
  const [createNotice, setCreateNotice] = useState<string | null>(null);
  const [isCreating, setIsCreating] = useState(false);
  const [revealedKey, setRevealedKey] = useState<CreatedApiKey | null>(null);
  const [copyNotice, setCopyNotice] = useState<string | null>(null);
  const [keyToRevoke, setKeyToRevoke] = useState<ApiKey | null>(null);
  const [revokeError, setRevokeError] = useState<string | null>(null);
  const [isRevoking, setIsRevoking] = useState(false);
  const [keyToRotate, setKeyToRotate] = useState<ApiKey | null>(null);
  const [rotateError, setRotateError] = useState<string | null>(null);
  const [isRotating, setIsRotating] = useState(false);
  const [webhookDrafts, setWebhookDrafts] = useState<Record<string, string>>({});
  const [webhookErrors, setWebhookErrors] = useState<Record<string, string>>({});
  const [webhookSavingId, setWebhookSavingId] = useState<string | null>(null);
  const [webhookClearingId, setWebhookClearingId] = useState<string | null>(null);
  const [revealedWebhookSecret, setRevealedWebhookSecret] =
    useState<RevealedWebhookSecret | null>(null);
  const [webhookCopyNotice, setWebhookCopyNotice] = useState<string | null>(null);

  const refreshKeys = useCallback(async () => {
    setKeysState({ status: "loading" });
    try {
      const keys = await loadKeys();
      setKeysState({ keys, status: "ready" });
    } catch (error) {
      setKeysState({
        message: error instanceof Error ? error.message : "Could not load API keys.",
        status: "error",
      });
    }
  }, []);

  const refreshBalance = useCallback(async () => {
    setBalanceState({ status: "loading" });
    try {
      const summary = await loadUsageBalance();
      setBalanceState({ status: "ready", summary });
    } catch (error) {
      setBalanceState({
        message:
          error instanceof Error ? error.message : "Could not load credit balance.",
        status: "error",
      });
    }
  }, []);

  useEffect(() => {
    let isCurrent = true;

    async function loadInitialKeys() {
      try {
        const keys = await loadKeys();
        if (isCurrent) {
          setKeysState({ keys, status: "ready" });
        }
      } catch (error) {
        if (isCurrent) {
          setKeysState({
            message:
              error instanceof Error ? error.message : "Could not load API keys.",
            status: "error",
          });
        }
      }
    }

    void loadInitialKeys();

    return () => {
      isCurrent = false;
    };
  }, []);

  useEffect(() => {
    let isCurrent = true;

    async function loadInitialBalance() {
      try {
        const summary = await loadUsageBalance();
        if (isCurrent) {
          setBalanceState({ status: "ready", summary });
        }
      } catch (error) {
        if (isCurrent) {
          setBalanceState({
            message:
              error instanceof Error
                ? error.message
                : "Could not load credit balance.",
            status: "error",
          });
        }
      }
    }

    void loadInitialBalance();

    return () => {
      isCurrent = false;
    };
  }, []);

  const activeCount = useMemo(() => {
    if (keysState.status !== "ready") {
      return 0;
    }

    return keysState.keys.filter((key) => !key.revokedAt).length;
  }, [keysState]);

  async function submitCreateKey(isTest: boolean) {
    if (isCreating) {
      return;
    }

    const trimmedName = name.trim();
    if (!trimmedName) {
      setCreateError("Add a name for this key.");
      return;
    }

    setRevealedKey(null);
    setCopyNotice(null);
    setCreateError(null);
    setCreateNotice(null);
    setIsCreating(true);

    let created: CreatedApiKey | null = null;

    try {
      const response = await fetch("/api/keys", {
        body: JSON.stringify({ name: trimmedName, test: isTest }),
        cache: "no-store",
        headers: {
          "Content-Type": "application/json",
        },
        method: "POST",
      });

      if (response.status === 401) {
        window.location.assign("/sign-in");
        return;
      }

      if (!response.ok) {
        throw new Error(
          (await readJsonError(response)) ?? "Could not create this key.",
        );
      }

      created = (await response.json()) as CreatedApiKey;
      setRevealedKey(created);
      setName("");
      setCreateNotice(
        isTest
          ? "Test key created. Copy it before leaving this page."
          : "Key created. Copy it before leaving this page.",
      );
      setKeysState((current) => {
        if (current.status !== "ready" || !created) {
          return current;
        }

        return {
          keys: [
            createdKeyListItem(created),
            ...current.keys.filter((key) => key.id !== created?.id),
          ],
          status: "ready",
        };
      });

      try {
        const keys = await loadKeys();
        setKeysState({ keys, status: "ready" });
      } catch {
        setCreateError(
          "Key created, but the list did not refresh. Copy the key now, then reload.",
        );
      }
    } catch (error) {
      if (!created) {
        setCreateError(
          error instanceof Error ? error.message : "Could not create this key.",
        );
      }
    } finally {
      setIsCreating(false);
    }
  }

  function createKey(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    void submitCreateKey(false);
  }

  async function copyKey() {
    if (!revealedKey) {
      return;
    }

    try {
      await navigator.clipboard.writeText(revealedKey.key);
      setCopyNotice("Copied.");
    } catch {
      setCopyNotice("Copy failed. Select the key and copy it manually.");
    }
  }

  async function copyWebhookSecret() {
    if (!revealedWebhookSecret) {
      return;
    }

    try {
      await navigator.clipboard.writeText(revealedWebhookSecret.secret);
      setWebhookCopyNotice("Copied.");
    } catch {
      setWebhookCopyNotice("Copy failed. Select the value and copy it manually.");
    }
  }

  function webhookDraftFor(key: ApiKey) {
    return webhookDrafts[key.id] ?? key.webhookUrl ?? "";
  }

  function updateWebhookDraft(keyId: string, value: string) {
    setWebhookDrafts((current) => ({
      ...current,
      [keyId]: value,
    }));
    setWebhookErrors((current) => {
      const next = { ...current };
      delete next[keyId];
      return next;
    });
  }

  function updateKeyWebhook(keyId: string, webhookUrl: string | null) {
    setKeysState((current) => {
      if (current.status !== "ready") {
        return current;
      }

      return {
        keys: current.keys.map((key) =>
          key.id === keyId ? { ...key, webhookUrl } : key,
        ),
        status: "ready",
      };
    });
  }

  function validateWebhookUrl(value: string) {
    const trimmed = value.trim();
    if (!trimmed) {
      return null;
    }

    try {
      const parsed = new URL(trimmed);
      if (parsed.protocol !== "https:" && parsed.protocol !== "http:") {
        return null;
      }

      return parsed.toString();
    } catch {
      return null;
    }
  }

  async function saveWebhook(key: ApiKey) {
    if (webhookSavingId || webhookClearingId) {
      return;
    }

    const webhookUrl = validateWebhookUrl(webhookDraftFor(key));
    if (!webhookUrl) {
      setWebhookErrors((current) => ({
        ...current,
        [key.id]: "Enter a valid webhook URL.",
      }));
      return;
    }

    setWebhookSavingId(key.id);
    setWebhookCopyNotice(null);
    setRevealedWebhookSecret(null);
    setWebhookErrors((current) => {
      const next = { ...current };
      delete next[key.id];
      return next;
    });

    try {
      const response = await fetch(
        `/api/keys/${encodeURIComponent(key.id)}/webhook`,
        {
          body: JSON.stringify({ webhookUrl }),
          cache: "no-store",
          headers: {
            "Content-Type": "application/json",
          },
          method: "POST",
        },
      );

      if (response.status === 401) {
        window.location.assign("/sign-in");
        return;
      }

      if (!response.ok) {
        throw new Error(
          (await readJsonError(response)) ?? "Could not save this webhook.",
        );
      }

      const body = (await response.json()) as {
        webhookSecret: string;
        webhookUrl: string;
      };
      updateKeyWebhook(key.id, body.webhookUrl);
      setWebhookDrafts((current) => ({
        ...current,
        [key.id]: body.webhookUrl,
      }));
      setRevealedWebhookSecret({
        keyId: key.id,
        keyName: key.name,
        secret: body.webhookSecret,
        webhookUrl: body.webhookUrl,
      });

      try {
        const keys = await loadKeys();
        setKeysState({ keys, status: "ready" });
      } catch {
        setWebhookErrors((current) => ({
          ...current,
          [key.id]: "Webhook saved, but the list did not refresh. Reload if it looks stale.",
        }));
      }
    } catch (error) {
      setWebhookErrors((current) => ({
        ...current,
        [key.id]:
          error instanceof Error ? error.message : "Could not save this webhook.",
      }));
    } finally {
      setWebhookSavingId(null);
    }
  }

  async function clearWebhook(key: ApiKey) {
    if (webhookSavingId || webhookClearingId) {
      return;
    }

    setWebhookClearingId(key.id);
    setWebhookCopyNotice(null);
    setWebhookErrors((current) => {
      const next = { ...current };
      delete next[key.id];
      return next;
    });

    try {
      const response = await fetch(
        `/api/keys/${encodeURIComponent(key.id)}/webhook`,
        {
          cache: "no-store",
          method: "DELETE",
        },
      );

      if (response.status === 401) {
        window.location.assign("/sign-in");
        return;
      }

      if (!response.ok) {
        throw new Error(
          (await readJsonError(response)) ?? "Could not clear this webhook.",
        );
      }

      updateKeyWebhook(key.id, null);
      setWebhookDrafts((current) => ({
        ...current,
        [key.id]: "",
      }));
      setRevealedWebhookSecret((current) =>
        current?.keyId === key.id ? null : current,
      );

      try {
        const keys = await loadKeys();
        setKeysState({ keys, status: "ready" });
      } catch {
        setWebhookErrors((current) => ({
          ...current,
          [key.id]: "Webhook cleared. Reload if the list looks stale.",
        }));
      }
    } catch (error) {
      setWebhookErrors((current) => ({
        ...current,
        [key.id]:
          error instanceof Error ? error.message : "Could not clear this webhook.",
      }));
    } finally {
      setWebhookClearingId(null);
    }
  }

  function closeRevokeDialog() {
    if (isRevoking) {
      return;
    }

    setKeyToRevoke(null);
    setRevokeError(null);
  }

  function closeRotateDialog() {
    if (isRotating) {
      return;
    }

    setKeyToRotate(null);
    setRotateError(null);
  }

  async function rotateKey() {
    if (!keyToRotate || isRotating) {
      return;
    }

    const key = keyToRotate;
    setIsRotating(true);
    setRotateError(null);
    setCreateNotice(null);
    setCreateError(null);
    setCopyNotice(null);

    let rotated: CreatedApiKey | null = null;

    try {
      const response = await fetch(
        `/api/keys/${encodeURIComponent(key.id)}/rotate`,
        {
          cache: "no-store",
          method: "POST",
        },
      );

      if (response.status === 401) {
        window.location.assign("/sign-in");
        return;
      }

      if (!response.ok) {
        throw new Error(
          (await readJsonError(response)) ?? "Could not rotate this key.",
        );
      }

      rotated = (await response.json()) as CreatedApiKey;
      setRevealedKey(rotated);
      setCreateNotice("Key rotated. Copy the new key before leaving this page.");
      setKeyToRotate(null);
      setRotateError(null);

      const revokedAt = new Date().toISOString();
      setKeysState((current) => {
        if (current.status !== "ready" || !rotated) {
          return current;
        }

        return {
          keys: [
            createdKeyListItem(rotated),
            ...current.keys.map((currentKey) =>
              currentKey.id === key.id
                ? { ...currentKey, revokedAt }
                : currentKey,
            ),
          ],
          status: "ready",
        };
      });

      try {
        const keys = await loadKeys();
        setKeysState({ keys, status: "ready" });
      } catch {
        setCreateError(
          "Key rotated, but the list did not refresh. Copy the key now, then reload.",
        );
      }
    } catch (error) {
      if (!rotated) {
        setRotateError(
          error instanceof Error ? error.message : "Could not rotate this key.",
        );
      }
    } finally {
      setIsRotating(false);
    }
  }

  async function revokeKey() {
    if (!keyToRevoke || isRevoking) {
      return;
    }

    const key = keyToRevoke;
    setIsRevoking(true);
    setRevokeError(null);

    try {
      const response = await fetch(
        `/api/keys/${encodeURIComponent(key.id)}`,
        {
          cache: "no-store",
          method: "DELETE",
        },
      );

      if (response.status === 401) {
        window.location.assign("/sign-in");
        return;
      }

      if (!response.ok) {
        throw new Error(
          (await readJsonError(response)) ?? "Could not revoke this key.",
        );
      }

      const revokedAt = new Date().toISOString();
      setKeysState((current) => {
        if (current.status !== "ready") {
          return current;
        }

        return {
          keys: current.keys.map((key) =>
            key.id === keyToRevoke.id ? { ...key, revokedAt } : key,
          ),
          status: "ready",
        };
      });
      setKeyToRevoke(null);
      setRevokeError(null);

      try {
        const keys = await loadKeys();
        setKeysState({ keys, status: "ready" });
      } catch {
        setCreateNotice("Key revoked. Reload if the list looks stale.");
      }
    } catch (error) {
      setRevokeError(
        error instanceof Error ? error.message : "Could not revoke this key.",
      );
    } finally {
      setIsRevoking(false);
    }
  }

  return (
    <div className="grid gap-5 lg:grid-cols-[minmax(0,1fr)_360px]">
      <div className="flex min-w-0 flex-col gap-5">
        <section className="rounded-lg border border-line bg-white/80 p-6 shadow-soft sm:p-8">
          <div className="flex flex-col gap-5 sm:flex-row sm:items-start sm:justify-between">
            <div className="min-w-0 space-y-3">
              <span className="inline-flex items-center gap-2 font-mono text-[11px] font-semibold uppercase tracking-[0.16em] text-sage">
                <KeyRound className="h-4 w-4" aria-hidden="true" />
                API access
              </span>
              <div>
                <h1 className="break-words text-4xl sm:text-5xl">API keys</h1>
                <p className="mt-3 max-w-2xl text-base text-ink/65">
                  Create and revoke keys for API requests tied to your account.
                  Keys are shown masked after creation.
                </p>
              </div>
            </div>
            <Button
              disabled={keysState.status === "loading"}
              onClick={() => void refreshKeys()}
              type="button"
              variant="secondary"
            >
              <RefreshCw className="h-4 w-4" aria-hidden="true" />
              Refresh
            </Button>
          </div>

          <div className="mt-6 rounded-md border border-sage/20 bg-sky px-4 py-3">
            <div className="flex items-start gap-3">
              <ShieldCheck
                className="mt-0.5 h-5 w-5 text-sage"
                aria-hidden="true"
              />
              <p className="text-sm text-ink/70">
                {
                  "Store API keys like passwords. We only show a new key once, and you won't see this again after leaving the reveal."
                }
              </p>
            </div>
          </div>
        </section>

        {revealedKey ? (
          <CreatedKeyReveal
            copyNotice={copyNotice}
            onCopy={() => void copyKey()}
            onDone={() => {
              setRevealedKey(null);
              setCopyNotice(null);
            }}
            revealedKey={revealedKey}
          />
        ) : null}

        {revealedWebhookSecret ? (
          <section
            aria-labelledby="webhook-secret-title"
            className="rounded-lg border border-sage/25 bg-white p-6 shadow-soft sm:p-8"
          >
            <div className="flex items-start gap-3">
              <CheckCircle2 className="mt-1 h-5 w-5 text-sage" aria-hidden="true" />
              <div className="min-w-0 flex-1">
                <h2 id="webhook-secret-title" className="text-2xl">
                  Webhook saved
                </h2>
                <p className="mt-2 text-sm text-ink/65">
                  Copy this signing secret for {revealedWebhookSecret.keyName}.
                  It is shown once.
                </p>
                <div className="mt-4 rounded-md border border-line bg-paper px-4 py-3">
                  <code className="block break-all font-mono text-sm text-ink">
                    {revealedWebhookSecret.secret}
                  </code>
                </div>
                <p className="mt-2 break-all text-xs text-ink/55">
                  {revealedWebhookSecret.webhookUrl}
                </p>
                <div className="mt-4 flex flex-col gap-3 sm:flex-row sm:items-center">
                  <Button onClick={() => void copyWebhookSecret()} type="button">
                    <Clipboard className="h-4 w-4" aria-hidden="true" />
                    Copy signing secret
                  </Button>
                  <Button
                    onClick={() => {
                      setRevealedWebhookSecret(null);
                      setWebhookCopyNotice(null);
                    }}
                    type="button"
                    variant="secondary"
                  >
                    Done
                  </Button>
                  {webhookCopyNotice ? (
                    <p className="text-sm font-semibold text-sage">
                      {webhookCopyNotice}
                    </p>
                  ) : null}
                </div>
              </div>
            </div>
          </section>
        ) : null}

        <section
          aria-labelledby="api-key-list-title"
          className="rounded-lg border border-line bg-white/80 p-6 shadow-soft sm:p-8"
        >
          <div className="flex flex-col gap-2 sm:flex-row sm:items-end sm:justify-between">
            <div>
              <h2 id="api-key-list-title" className="text-2xl">
                Existing keys
              </h2>
              <p className="mt-2 text-sm text-ink/65">
                {keysState.status === "ready"
                  ? `${activeCount} active key${activeCount === 1 ? "" : "s"}`
                  : "Loading keys"}
              </p>
            </div>
          </div>

          {keysState.status === "loading" ? (
            <div className="mt-6 flex items-center gap-3 rounded-md border border-line bg-paper px-4 py-6 text-sm text-ink/65">
              <Loader2 className="h-5 w-5 animate-spin" aria-hidden="true" />
              Loading API keys...
            </div>
          ) : null}

          {keysState.status === "error" ? (
            <div className="mt-6 rounded-md border border-rust/25 bg-rust/5 p-4">
              <div className="flex items-start gap-3">
                <AlertCircle
                  className="mt-0.5 h-5 w-5 text-rust"
                  aria-hidden="true"
                />
                <div className="min-w-0">
                  <p className="font-semibold text-rust">Could not load keys.</p>
                  <p className="mt-1 text-sm text-ink/65">{keysState.message}</p>
                  <Button
                    className="mt-4"
                    onClick={() => void refreshKeys()}
                    type="button"
                    variant="secondary"
                  >
                    Try again
                  </Button>
                </div>
              </div>
            </div>
          ) : null}

          {keysState.status === "ready" && keysState.keys.length === 0 ? (
            <div className="mt-6 rounded-md border border-dashed border-line bg-paper px-4 py-8 text-sm text-ink/55">
              No API keys yet. Create one when you are ready to connect another
              app.
            </div>
          ) : null}

          {keysState.status === "ready" && keysState.keys.length > 0 ? (
            <div className="mt-6 overflow-x-auto">
              <table className="min-w-full text-left text-sm">
                <thead className="border-b border-line bg-paper-deep/40 text-xs uppercase text-ink/50">
                  <tr>
                    <th className="px-4 py-3 font-semibold">Name</th>
                    <th className="px-4 py-3 font-semibold">Key</th>
                    <th className="px-4 py-3 font-semibold">Status</th>
                    <th className="px-4 py-3 font-semibold">30-day calls</th>
                    <th className="px-4 py-3 font-semibold">Last used</th>
                    <th className="px-4 py-3 font-semibold">Created</th>
                    <th className="px-4 py-3 font-semibold">Webhook</th>
                    <th className="px-4 py-3 font-semibold">Actions</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-line">
                  {keysState.keys.map((key) => (
                    <tr className="bg-white/45" key={key.id}>
                      <td className="max-w-[220px] px-4 py-4">
                        <div className="break-words font-semibold text-ink">
                          {key.name}
                        </div>
                      </td>
                      <td className="whitespace-nowrap px-4 py-4 text-xs text-ink/65">
                        <MaskedApiKeyValue apiKey={key} />
                      </td>
                      <td className="whitespace-nowrap px-4 py-4">
                        <span
                          className={`inline-flex rounded-full border px-2.5 py-1 text-xs font-semibold ${statusClassName(
                            key,
                          )}`}
                        >
                          {statusLabel(key)}
                        </span>
                      </td>
                      <td className="whitespace-nowrap px-4 py-4">
                        <div className="inline-flex min-w-20 flex-col rounded-md border border-line bg-paper px-3 py-2">
                          <span className="font-semibold text-ink">
                            {key.last30dUsage.calls}
                          </span>
                          <span className="text-xs text-ink/55">
                            {usageDetailLabel(key.last30dUsage)}
                          </span>
                        </div>
                      </td>
                      <td className="whitespace-nowrap px-4 py-4">
                        {formatDateTime(key.lastUsedAt, "Never")}
                      </td>
                      <td className="whitespace-nowrap px-4 py-4">
                        {formatDateTime(key.createdAt)}
                      </td>
                      <td className="min-w-[360px] px-4 py-4">
                        <div className="grid gap-2">
                          <label
                            className="sr-only"
                            htmlFor={`webhook-url-${key.id}`}
                          >
                            Webhook URL
                          </label>
                          <div className="flex items-center gap-2">
                            <Input
                              autoComplete="off"
                              className="min-w-[210px]"
                              disabled={Boolean(key.revokedAt)}
                              id={`webhook-url-${key.id}`}
                              maxLength={2048}
                              onChange={(event) =>
                                updateWebhookDraft(key.id, event.target.value)
                              }
                              placeholder="https://example.com/rewrite"
                              value={webhookDraftFor(key)}
                            />
                            <Button
                              disabled={
                                Boolean(key.revokedAt) ||
                                webhookSavingId === key.id ||
                                webhookClearingId === key.id
                              }
                              onClick={() => void saveWebhook(key)}
                              type="button"
                              variant="secondary"
                            >
                              {webhookSavingId === key.id ? (
                                <Loader2
                                  className="h-4 w-4 animate-spin"
                                  aria-hidden="true"
                                />
                              ) : (
                                <Save className="h-4 w-4" aria-hidden="true" />
                              )}
                              Save
                            </Button>
                            <Button
                              disabled={
                                Boolean(key.revokedAt) ||
                                !key.webhookUrl ||
                                webhookSavingId === key.id ||
                                webhookClearingId === key.id
                              }
                              onClick={() => void clearWebhook(key)}
                              type="button"
                              variant="secondary"
                            >
                              {webhookClearingId === key.id ? (
                                <Loader2
                                  className="h-4 w-4 animate-spin"
                                  aria-hidden="true"
                                />
                              ) : (
                                <X className="h-4 w-4" aria-hidden="true" />
                              )}
                              Clear webhook
                            </Button>
                          </div>
                          <p className="flex items-center gap-1 text-xs text-ink/55">
                            <Link2 className="h-3.5 w-3.5" aria-hidden="true" />
                            {key.webhookUrl ? "Webhook configured" : "No webhook URL"}
                          </p>
                          {webhookErrors[key.id] ? (
                            <p className="text-xs font-semibold text-rust">
                              {webhookErrors[key.id]}
                            </p>
                          ) : null}
                        </div>
                      </td>
                      <td className="whitespace-nowrap px-4 py-4">
                        <div className="flex gap-2">
                          <Button
                            disabled={Boolean(key.revokedAt)}
                            onClick={() => {
                              setKeyToRotate(key);
                              setRotateError(null);
                            }}
                            type="button"
                            variant="secondary"
                          >
                            <RefreshCw className="h-4 w-4" aria-hidden="true" />
                            Rotate
                          </Button>
                          <Button
                            className="border-rust/30 text-rust hover:bg-rust/5"
                            disabled={Boolean(key.revokedAt)}
                            onClick={() => {
                              setKeyToRevoke(key);
                              setRevokeError(null);
                            }}
                            type="button"
                            variant="secondary"
                          >
                            <Trash2 className="h-4 w-4" aria-hidden="true" />
                            Revoke
                          </Button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : null}
        </section>
      </div>

      <aside className="flex flex-col gap-5">
        <CreditBalanceCard
          balanceState={balanceState}
          onRefresh={() => void refreshBalance()}
        />

        <Card className="p-5">
          <h2 className="text-2xl">Create key</h2>
          <p className="mt-2 text-sm text-ink/65">
            Name keys by where they will be used, such as production server or
            internal tool.
          </p>
          <form className="mt-5 grid gap-4" onSubmit={createKey}>
            <label className="block text-sm font-semibold text-ink/70">
              Key name
              <Input
                autoComplete="off"
                className="mt-1"
                maxLength={80}
                onChange={(event) => setName(event.target.value)}
                placeholder="Production server"
                required
                value={name}
              />
            </label>

            {createError ? (
              <p className="text-sm font-semibold text-rust">{createError}</p>
            ) : null}
            {createNotice ? (
              <p className="text-sm font-semibold text-sage">{createNotice}</p>
            ) : null}

            <div className="grid gap-3">
              <Button disabled={isCreating} type="submit">
                {isCreating ? (
                  <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
                ) : (
                  <Plus className="h-4 w-4" aria-hidden="true" />
                )}
                {isCreating ? "Creating..." : "Create key"}
              </Button>
              <Button
                disabled={isCreating}
                onClick={() => void submitCreateKey(true)}
                type="button"
                variant="secondary"
              >
                {isCreating ? (
                  <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
                ) : (
                  <FlaskConical className="h-4 w-4" aria-hidden="true" />
                )}
                Create test key
              </Button>
            </div>
          </form>
        </Card>

        <Card className="p-5">
          <h2 className="text-2xl">API reference</h2>
          <p className="mt-2 text-sm text-ink/65">
            Review request shapes and response examples before connecting your
            product.
          </p>
          <LinkButton className="mt-5 w-full" href="/developers" variant="secondary">
            View developer page
          </LinkButton>
        </Card>
      </aside>

      {keyToRotate ? (
        <div
          aria-labelledby="rotate-api-key-title"
          aria-modal="true"
          className="fixed inset-0 z-[80] flex items-center justify-center bg-ink/35 p-4"
          role="dialog"
        >
          <div className="w-full max-w-lg rounded-lg border border-line bg-paper p-6 shadow-soft">
            <div className="flex items-start gap-3">
              <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-md bg-sky text-sage">
                <RefreshCw className="h-5 w-5" aria-hidden="true" />
              </div>
              <div className="min-w-0">
                <h2 id="rotate-api-key-title" className="text-2xl">
                  Rotate {keyToRotate.name}?
                </h2>
                <p className="mt-2 text-sm text-ink/65">
                  This creates a replacement key and revokes the current key.
                  Copy the new key before leaving this page.
                </p>
              </div>
            </div>

            {rotateError ? (
              <p className="mt-4 text-sm font-semibold text-rust">
                {rotateError}
              </p>
            ) : null}

            <div className="mt-6 flex flex-col-reverse gap-3 sm:flex-row sm:justify-end">
              <Button
                disabled={isRotating}
                onClick={closeRotateDialog}
                type="button"
                variant="secondary"
              >
                Cancel
              </Button>
              <Button disabled={isRotating} onClick={() => void rotateKey()} type="button">
                {isRotating ? (
                  <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
                ) : (
                  <RefreshCw className="h-4 w-4" aria-hidden="true" />
                )}
                {isRotating ? "Rotating..." : "Confirm rotate"}
              </Button>
            </div>
          </div>
        </div>
      ) : null}

      {keyToRevoke ? (
        <div
          aria-labelledby="revoke-api-key-title"
          aria-modal="true"
          className="fixed inset-0 z-[80] flex items-center justify-center bg-ink/35 p-4"
          role="dialog"
        >
          <div className="w-full max-w-lg rounded-lg border border-line bg-paper p-6 shadow-soft">
            <div className="flex items-start gap-3">
              <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-md bg-rust/10 text-rust">
                <Trash2 className="h-5 w-5" aria-hidden="true" />
              </div>
              <div className="min-w-0">
                <h2 id="revoke-api-key-title" className="text-2xl">
                  Revoke {keyToRevoke.name}?
                </h2>
                <p className="mt-2 text-sm text-ink/65">
                  This stops future requests using this key. Create a new key if
                  the connected app should keep sending requests.
                </p>
              </div>
            </div>

            {revokeError ? (
              <p className="mt-4 text-sm font-semibold text-rust">
                {revokeError}
              </p>
            ) : null}

            <div className="mt-6 flex flex-col-reverse gap-3 sm:flex-row sm:justify-end">
              <Button
                disabled={isRevoking}
                onClick={closeRevokeDialog}
                type="button"
                variant="secondary"
              >
                Cancel
              </Button>
              <Button
                className="bg-rust text-white hover:bg-rust/90"
                disabled={isRevoking}
                onClick={() => void revokeKey()}
                type="button"
              >
                {isRevoking ? (
                  <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
                ) : (
                  <Trash2 className="h-4 w-4" aria-hidden="true" />
                )}
                {isRevoking ? "Revoking..." : "Confirm revoke"}
              </Button>
            </div>
          </div>
        </div>
      ) : null}
    </div>
  );
}
