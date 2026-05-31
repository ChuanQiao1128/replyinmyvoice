"use client";

import Link from "next/link";
import { type CSSProperties, type FormEvent, useEffect, useMemo, useState } from "react";

import styles from "./auth-panels.module.css";

type AuthMode = "sign-in" | "sign-up" | "reset";
type FieldErrors = Partial<Record<"email" | "password" | "displayName" | "code" | "newEntry" | "confirmEntry", string>>;
type JsonBody = Record<string, unknown> | null;

type SignInAuthPageProps = {
  callbackError?: string;
  initialEmail?: string;
  redirectTo?: string;
  resetSuccess?: boolean;
};

type SignUpAuthPageProps = {
  initialEmail?: string;
  redirectTo?: string;
};

type ResetAuthPageProps = {
  initialEmail?: string;
};

const minEntryLength = 8;
const defaultRedirectTo = "/app";
const defaultCodeLength = 6;
const defaultCooldownSeconds = 30;
const panelVisualStyle = {
  background: "var(--card)",
  borderColor: "var(--rule)",
  boxShadow: "var(--shadow-lg)",
} satisfies CSSProperties;

const sideHighlights = {
  "sign-in": [
    "Open your draft workspace",
    "Keep your remaining rewrites in view",
    "Use Google when you prefer browser sign-in",
  ],
  "sign-up": [
    "Start with three free rewrites",
    "Verify your email before the workspace opens",
    "Keep facts and tone checks tied to your account",
  ],
  reset: [
    "Reset access without leaving the app",
    "Use the code sent to your email",
    "Return to sign in when the reset is complete",
  ],
} satisfies Record<AuthMode, string[]>;

export function SignInAuthPage({
  callbackError,
  initialEmail = "",
  redirectTo = defaultRedirectTo,
  resetSuccess = false,
}: SignInAuthPageProps) {
  const safeRedirect = safeRedirectTo(redirectTo);
  const [email, setEmail] = useState(initialEmail);
  const [entry, setEntry] = useState("");
  const [showEntry, setShowEntry] = useState(false);
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [formError, setFormError] = useState<string | null>(callbackError ? callbackErrorMessage(callbackError) : null);
  const [fallbackRedirect, setFallbackRedirect] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const googleHref = useMemo(() => buildGoogleHref(safeRedirect, email), [email, safeRedirect]);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const nextErrors = validateEmailEntry(email, entry);
    setFieldErrors(nextErrors);
    setFallbackRedirect(null);

    if (hasErrors(nextErrors)) {
      setFormError("Check the highlighted fields and try again.");
      return;
    }

    setIsSubmitting(true);
    setFormError(null);

    try {
      const response = await fetch("/api/auth/signin", {
        body: JSON.stringify({ email, password: entry, redirectTo: safeRedirect }),
        headers: { "Content-Type": "application/json" },
        method: "POST",
      });

      if (response.redirected) {
        moveToRedirect(response.url, safeRedirect);
        return;
      }

      const json = await readJsonBody(response);
      if (response.ok) {
        window.location.assign(safeRedirect);
        return;
      }

      const fallback = textValue(json?.fallbackRedirect);
      if (fallback) {
        setFallbackRedirect(fallback);
      }
      setFormError(signInErrorMessage(textValue(json?.error)));
    } catch {
      setFormError("We could not sign you in. Please try again.");
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <AuthShell
      eyebrow="Welcome back"
      heading="Sign in to your reply workspace."
      lead="Use the in-app email path, or continue with Google when you want browser sign-in."
      mode="sign-in"
    >
      <section aria-labelledby="sign-in-title" className={styles.panel} style={panelVisualStyle}>
        <PanelHeader
          id="sign-in-title"
          eyebrow="Entra OAuth sign-in"
          title="Email and password"
          body="Enter the account details you used for Reply In My Voice."
        />

        {resetSuccess ? (
          <StatusMessage tone="success">Your sign-in value has been reset. Sign in with the new value.</StatusMessage>
        ) : null}
        {formError ? <StatusMessage tone="error">{formError}</StatusMessage> : null}

        <form className={styles.form} noValidate onSubmit={handleSubmit}>
          <TextField
            autoComplete="email"
            error={fieldErrors.email}
            inputMode="email"
            label="Email address"
            maxLength={320}
            name="email"
            onChange={setEmail}
            placeholder="you@example.com"
            type="email"
            value={email}
          />

          <EntryField
            autoComplete="current-password"
            error={fieldErrors.password}
            label="Password"
            onChange={setEntry}
            showEntry={showEntry}
            toggleShowEntry={() => setShowEntry((value) => !value)}
            value={entry}
          />

          <div className={styles.formRow}>
            <span className={styles.hint}>Use at least {minEntryLength} characters.</span>
            <Link className={styles.textLink} href="/forgot-password">
              Forgot password?
            </Link>
          </div>

          <button className="btn btn-primary btn-lg" disabled={isSubmitting} type="submit">
            {isSubmitting ? "Signing in..." : "Continue with email"}
          </button>
        </form>

        {fallbackRedirect ? (
          <a className={`${styles.browserFallback} btn btn-ghost btn-lg`} href={fallbackRedirect}>
            Continue in browser
          </a>
        ) : null}

        <Divider label="or" />
        <GoogleButton href={googleHref} />

        <footer className={styles.panelFooter}>
          <span>New here?</span>
          <Link className={styles.textLink} href={withEmail("/sign-up", email)}>
            Create an account
          </Link>
        </footer>
      </section>
    </AuthShell>
  );
}

export function ResetAuthPage({
  initialEmail = "",
}: ResetAuthPageProps) {
  const [step, setStep] = useState<"email" | "code">("email");
  const [email, setEmail] = useState(initialEmail);
  const [code, setCode] = useState("");
  const [newEntry, setNewEntry] = useState("");
  const [confirmEntry, setConfirmEntry] = useState("");
  const [showNewEntry, setShowNewEntry] = useState(false);
  const [codeLength, setCodeLength] = useState(defaultCodeLength);
  const [channelLabel, setChannelLabel] = useState("");
  const [cooldownSeconds, setCooldownSeconds] = useState(0);
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isResending, setIsResending] = useState(false);

  useEffect(() => {
    if (step !== "code" || cooldownSeconds <= 0) {
      return;
    }

    const timer = window.setTimeout(() => {
      setCooldownSeconds((seconds) => Math.max(0, seconds - 1));
    }, 1000);

    return () => window.clearTimeout(timer);
  }, [cooldownSeconds, step]);

  async function handleStart(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const nextErrors = validateResetEmail(email);
    setFieldErrors(nextErrors);

    if (hasErrors(nextErrors)) {
      setFormError("Enter a valid email to receive a reset code.");
      return;
    }

    setIsSubmitting(true);
    setFormError(null);

    try {
      const response = await fetch("/api/auth/reset/start", {
        body: JSON.stringify({ email }),
        headers: { "Content-Type": "application/json" },
        method: "POST",
      });
      const json = await readJsonBody(response);

      if (response.ok && json?.ok === true) {
        setCode("");
        setNewEntry("");
        setConfirmEntry("");
        setCodeLength(numberValue(json.codeLength) ?? defaultCodeLength);
        setChannelLabel(textValue(json.channelLabel) ?? email);
        setCooldownSeconds(defaultCooldownSeconds);
        setStep("code");
        setFieldErrors({});
        return;
      }

      setFormError(textValue(json?.error) ?? "We could not start the reset. Please try again.");
    } catch {
      setFormError("We could not start the reset. Please try again.");
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleVerify(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const trimmedCode = code.trim();
    const nextErrors = validateResetCredentials(trimmedCode, newEntry, confirmEntry);
    setFieldErrors(nextErrors);

    if (hasErrors(nextErrors)) {
      setFormError("Check the highlighted fields and try again.");
      return;
    }

    setIsSubmitting(true);
    setFormError(null);

    try {
      const response = await fetch("/api/auth/reset/verify", {
        body: JSON.stringify({ code: trimmedCode, newPassword: newEntry }),
        headers: { "Content-Type": "application/json" },
        method: "POST",
      });
      const json = await readJsonBody(response);

      if (response.ok && json?.ok === true) {
        window.location.assign(textValue(json.next) ?? "/sign-in?reset=success");
        return;
      }

      setFormError(textValue(json?.error) ?? "We could not finish the reset. Please try again.");
    } catch {
      setFormError("We could not finish the reset. Please try again.");
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleResend() {
    if (cooldownSeconds > 0 || isResending) {
      return;
    }

    setIsResending(true);
    setFormError(null);

    try {
      const response = await fetch("/api/auth/reset/resend", {
        body: JSON.stringify({}),
        headers: { "Content-Type": "application/json" },
        method: "POST",
      });
      const json = await readJsonBody(response);

      if (response.ok && json?.ok === true) {
        setCooldownSeconds(numberValue(json.cooldownSeconds) ?? defaultCooldownSeconds);
        return;
      }

      setCooldownSeconds(numberValue(json?.cooldownSeconds) ?? 0);
      setFormError(textValue(json?.error) ?? "We could not resend the code. Please try again.");
    } catch {
      setFormError("We could not resend the code. Please try again.");
    } finally {
      setIsResending(false);
    }
  }

  return (
    <AuthShell
      eyebrow="Account reset"
      heading="Reset your sign-in value."
      lead="Enter your email, then use the code we send to choose a new sign-in value."
      mode="reset"
    >
      <section aria-labelledby="reset-title" className={styles.panel} style={panelVisualStyle}>
        {step === "email" ? (
          <>
            <PanelHeader
              id="reset-title"
              eyebrow="Step 1 of 2"
              title="Reset your sign-in value"
              body="Use the email tied to your Reply In My Voice account. We will send a reset code next."
            />

            {formError ? <StatusMessage tone="error">{formError}</StatusMessage> : null}

            <form className={styles.form} noValidate onSubmit={handleStart}>
              <TextField
                autoComplete="email"
                error={fieldErrors.email}
                inputMode="email"
                label="Email address"
                maxLength={320}
                name="email"
                onChange={setEmail}
                placeholder="you@example.com"
                type="email"
                value={email}
              />

              <button className="btn btn-primary btn-lg" disabled={isSubmitting} type="submit">
                {isSubmitting ? "Sending reset code..." : "Send reset code"}
              </button>
            </form>

            <footer className={styles.panelFooter}>
              <span>Remembered it?</span>
              <Link className={styles.textLink} href={withEmail("/sign-in", email)}>
                Sign in
              </Link>
            </footer>
          </>
        ) : (
          <>
            <PanelHeader
              id="reset-title"
              eyebrow="Step 2 of 2"
              title="Enter the reset code"
              body={`We sent a ${codeLength}-character code to ${channelLabel || email}.`}
            />

            {formError ? <StatusMessage tone="error">{formError}</StatusMessage> : null}

            <form className={styles.form} noValidate onSubmit={handleVerify}>
              <TextField
                autoComplete="one-time-code"
                error={fieldErrors.code}
                inputMode="numeric"
                label="Verification code"
                maxLength={32}
                name="code"
                onChange={setCode}
                placeholder={"0".repeat(Math.min(codeLength, 6))}
                type="text"
                value={code}
              />

              <EntryField
                autoComplete="new-password"
                error={fieldErrors.newEntry}
                label="New sign-in value"
                name="newEntry"
                onChange={setNewEntry}
                showEntry={showNewEntry}
                toggleShowEntry={() => setShowNewEntry((value) => !value)}
                value={newEntry}
              />

              <EntryField
                autoComplete="new-password"
                error={fieldErrors.confirmEntry}
                label="Confirm sign-in value"
                name="confirmEntry"
                onChange={setConfirmEntry}
                showEntry={showNewEntry}
                toggleShowEntry={() => setShowNewEntry((value) => !value)}
                value={confirmEntry}
              />

              <p className={styles.hint}>Use at least {minEntryLength} characters.</p>

              <div className={styles.codeActions}>
                <button className={styles.inlineButton} onClick={() => setStep("email")} type="button">
                  Change email
                </button>
                <button
                  className={styles.inlineButton}
                  disabled={cooldownSeconds > 0 || isResending}
                  onClick={handleResend}
                  type="button"
                >
                  {cooldownSeconds > 0
                    ? `Resend in ${cooldownSeconds}s`
                    : isResending
                      ? "Resending..."
                      : "Resend code"}
                </button>
              </div>

              <button className="btn btn-primary btn-lg" disabled={isSubmitting} type="submit">
                {isSubmitting ? "Resetting..." : "Reset and return to sign in"}
              </button>
            </form>
          </>
        )}
      </section>
    </AuthShell>
  );
}

export function SignUpAuthPage({
  initialEmail = "",
  redirectTo = defaultRedirectTo,
}: SignUpAuthPageProps) {
  const safeRedirect = safeRedirectTo(redirectTo);
  const [step, setStep] = useState<"details" | "code">("details");
  const [email, setEmail] = useState(initialEmail);
  const [displayName, setDisplayName] = useState("");
  const [entry, setEntry] = useState("");
  const [showEntry, setShowEntry] = useState(false);
  const [code, setCode] = useState("");
  const [codeLength, setCodeLength] = useState(defaultCodeLength);
  const [channelLabel, setChannelLabel] = useState("");
  const [cooldownSeconds, setCooldownSeconds] = useState(0);
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [fallbackRedirect, setFallbackRedirect] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isResending, setIsResending] = useState(false);

  const googleHref = useMemo(() => buildGoogleHref(safeRedirect, email), [email, safeRedirect]);

  useEffect(() => {
    if (step !== "code" || cooldownSeconds <= 0) {
      return;
    }

    const timer = window.setTimeout(() => {
      setCooldownSeconds((seconds) => Math.max(0, seconds - 1));
    }, 1000);

    return () => window.clearTimeout(timer);
  }, [cooldownSeconds, step]);

  async function handleStart(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const nextErrors = validateEmailEntry(email, entry);
    if (displayName.trim().length > 160) {
      nextErrors.displayName = "Use 160 characters or fewer.";
    }
    setFieldErrors(nextErrors);
    setFallbackRedirect(null);

    if (hasErrors(nextErrors)) {
      setFormError("Check the highlighted fields and try again.");
      return;
    }

    setIsSubmitting(true);
    setFormError(null);

    try {
      const response = await fetch("/api/auth/signup/start", {
        body: JSON.stringify({
          displayName: displayName.trim() || undefined,
          email,
          password: entry,
        }),
        headers: { "Content-Type": "application/json" },
        method: "POST",
      });
      const json = await readJsonBody(response);

      if (response.ok && json?.ok === true) {
        setCode("");
        setCodeLength(numberValue(json.codeLength) ?? defaultCodeLength);
        setChannelLabel(textValue(json.channelLabel) ?? email);
        setCooldownSeconds(defaultCooldownSeconds);
        setEntry("");
        setStep("code");
        setFieldErrors({});
        return;
      }

      const fallback = textValue(json?.fallbackRedirect);
      if (fallback) {
        setFallbackRedirect(fallback);
      }
      setFormError(textValue(json?.error) ?? "We could not start sign-up. Please try again.");
    } catch {
      setFormError("We could not start sign-up. Please try again.");
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleVerify(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const trimmedCode = code.trim();
    const nextErrors: FieldErrors = {};
    setFallbackRedirect(null);

    if (!trimmedCode) {
      nextErrors.code = "Enter the verification code.";
    }
    setFieldErrors(nextErrors);

    if (hasErrors(nextErrors)) {
      setFormError("Enter the code from your email.");
      return;
    }

    setIsSubmitting(true);
    setFormError(null);

    try {
      const response = await fetch("/api/auth/signup/verify", {
        body: JSON.stringify({ code: trimmedCode, redirectTo: safeRedirect }),
        headers: { "Content-Type": "application/json" },
        method: "POST",
      });

      if (response.redirected) {
        moveToRedirect(response.url, safeRedirect);
        return;
      }

      const json = await readJsonBody(response);
      if (response.ok) {
        window.location.assign(safeRedirect);
        return;
      }

      setFormError(textValue(json?.error) ?? "We could not verify the code. Please try again.");
    } catch {
      setFormError("We could not verify the code. Please try again.");
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleResend() {
    if (cooldownSeconds > 0 || isResending) {
      return;
    }

    setIsResending(true);
    setFormError(null);

    try {
      const response = await fetch("/api/auth/signup/resend", {
        body: JSON.stringify({}),
        headers: { "Content-Type": "application/json" },
        method: "POST",
      });
      const json = await readJsonBody(response);

      if (response.ok && json?.ok === true) {
        setCooldownSeconds(numberValue(json.cooldownSeconds) ?? defaultCooldownSeconds);
        return;
      }

      setCooldownSeconds(numberValue(json?.cooldownSeconds) ?? 0);
      setFormError(textValue(json?.error) ?? "We could not resend the code. Please try again.");
    } catch {
      setFormError("We could not resend the code. Please try again.");
    } finally {
      setIsResending(false);
    }
  }

  return (
    <AuthShell
      eyebrow="Create your account"
      heading="Start with email, a sign-in value, and a quick verification."
      lead="Create the account in-app, then enter the code sent to your email before opening the workspace."
      mode="sign-up"
    >
      <section aria-labelledby="sign-up-title" className={styles.panel} style={panelVisualStyle}>
        {step === "details" ? (
          <>
            <PanelHeader
              id="sign-up-title"
              eyebrow="Step 1 of 2"
              title="Create your account"
              body="Use an email you can access. We will send a verification code next."
            />

            {formError ? <StatusMessage tone="error">{formError}</StatusMessage> : null}

            <form className={styles.form} noValidate onSubmit={handleStart}>
              <TextField
                autoComplete="email"
                error={fieldErrors.email}
                inputMode="email"
                label="Email address"
                maxLength={320}
                name="email"
                onChange={setEmail}
                placeholder="you@example.com"
                type="email"
                value={email}
              />

              <TextField
                autoComplete="name"
                error={fieldErrors.displayName}
                label="Display name"
                maxLength={160}
                name="displayName"
                onChange={setDisplayName}
                optionalLabel="Optional"
                placeholder="Your name"
                type="text"
                value={displayName}
              />

              <EntryField
                autoComplete="new-password"
                error={fieldErrors.password}
                label="Password"
                onChange={setEntry}
                showEntry={showEntry}
                toggleShowEntry={() => setShowEntry((value) => !value)}
                value={entry}
              />

              <p className={styles.hint} id="sign-up-entry-hint">
                Use at least {minEntryLength} characters. Keep it unique to this account.
              </p>

              <button className="btn btn-primary btn-lg" disabled={isSubmitting} type="submit">
                {isSubmitting ? "Sending code..." : "Create account"}
              </button>
            </form>

            {fallbackRedirect ? (
              <a className={`${styles.browserFallback} btn btn-ghost btn-lg`} href={fallbackRedirect}>
                {fallbackRedirect.startsWith("/sign-in") ? "Go to sign in" : "Continue in browser"}
              </a>
            ) : null}

            <Divider label="or" />
            <GoogleButton href={googleHref} />

            <footer className={styles.panelFooter}>
              <span>Already have an account?</span>
              <Link className={styles.textLink} href={withEmail("/sign-in", email)}>
                Sign in
              </Link>
            </footer>
          </>
        ) : (
          <>
            <PanelHeader
              id="sign-up-title"
              eyebrow="Step 2 of 2"
              title="Enter the verification code"
              body={`We sent a ${codeLength}-character code to ${channelLabel || email}.`}
            />

            {formError ? <StatusMessage tone="error">{formError}</StatusMessage> : null}

            <form className={styles.form} noValidate onSubmit={handleVerify}>
              <TextField
                autoComplete="one-time-code"
                error={fieldErrors.code}
                inputMode="numeric"
                label="Verification code"
                maxLength={32}
                name="code"
                onChange={setCode}
                placeholder={"0".repeat(Math.min(codeLength, 6))}
                type="text"
                value={code}
              />

              <div className={styles.codeActions}>
                <button className={styles.inlineButton} onClick={() => setStep("details")} type="button">
                  Edit details
                </button>
                <button
                  className={styles.inlineButton}
                  disabled={cooldownSeconds > 0 || isResending}
                  onClick={handleResend}
                  type="button"
                >
                  {cooldownSeconds > 0
                    ? `Resend in ${cooldownSeconds}s`
                    : isResending
                      ? "Resending..."
                      : "Resend code"}
                </button>
              </div>

              <button className="btn btn-primary btn-lg" disabled={isSubmitting} type="submit">
                {isSubmitting ? "Verifying..." : "Verify and continue"}
              </button>
            </form>
          </>
        )}
      </section>
    </AuthShell>
  );
}

function AuthShell({
  children,
  eyebrow,
  heading,
  lead,
  mode,
}: {
  children: React.ReactNode;
  eyebrow: string;
  heading: string;
  lead: string;
  mode: AuthMode;
}) {
  return (
    <main className="rimv">
      <div className={styles.authRoot}>
        <div className={`wrap ${styles.authWrap}`}>
          <section className={styles.authIntro} aria-labelledby={`${mode}-hero-title`}>
            <Link href="/" className={styles.backLink}>
              &larr; Back to home
            </Link>
            <Link href="/" className="brand" aria-label="Reply In My Voice home">
              <span className="brand-mark" aria-hidden="true">
                R
              </span>
              <span>Reply In My Voice</span>
            </Link>

            <div className="eyebrow">
              <span className="dot" />
              {eyebrow}
            </div>
            <h1 id={`${mode}-hero-title`}>{heading}</h1>
            <p className={styles.lead}>{lead}</p>

            <ul className={styles.highlights} aria-label="Account benefits">
              {sideHighlights[mode].map((highlight) => (
                <li key={highlight}>
                  <span aria-hidden="true" className={styles.checkMark} />
                  {highlight}
                </li>
              ))}
            </ul>
          </section>

          <div className={styles.authPanelColumn}>{children}</div>
        </div>
      </div>
    </main>
  );
}

function PanelHeader({
  body,
  eyebrow,
  id,
  title,
}: {
  body: string;
  eyebrow: string;
  id: string;
  title: string;
}) {
  return (
    <header className={styles.panelHeader}>
      <div className="eyebrow">{eyebrow}</div>
      <h2 id={id}>{title}</h2>
      <p>{body}</p>
    </header>
  );
}

function TextField({
  error,
  label,
  name,
  onChange,
  optionalLabel,
  value,
  ...props
}: {
  error?: string;
  label: string;
  name: string;
  onChange: (value: string) => void;
  optionalLabel?: string;
  value: string;
} & Omit<React.InputHTMLAttributes<HTMLInputElement>, "className" | "id" | "name" | "onChange" | "value">) {
  const errorId = `${name}-error`;

  return (
    <div className={styles.field}>
      <label className={styles.label} htmlFor={name}>
        <span>{label}</span>
        {optionalLabel ? <span className={styles.optional}>{optionalLabel}</span> : null}
      </label>
      <input
        aria-describedby={error ? errorId : undefined}
        aria-invalid={Boolean(error)}
        className={`${styles.input} ${error ? styles.inputError : ""}`}
        id={name}
        name={name}
        onChange={(event) => onChange(event.currentTarget.value)}
        value={value}
        {...props}
      />
      {error ? (
        <p className={styles.fieldError} id={errorId}>
          {error}
        </p>
      ) : null}
    </div>
  );
}

function EntryField({
  autoComplete,
  error,
  label,
  name = "password",
  onChange,
  showEntry,
  toggleShowEntry,
  value,
}: {
  autoComplete: string;
  error?: string;
  label: string;
  name?: string;
  onChange: (value: string) => void;
  showEntry: boolean;
  toggleShowEntry: () => void;
  value: string;
}) {
  const errorId = `${name}-error`;

  return (
    <div className={styles.field}>
      <label className={styles.label} htmlFor={name}>
        <span>{label}</span>
      </label>
      <div className={`${styles.entryWrap} ${error ? styles.inputError : ""}`}>
        <input
          aria-describedby={error ? errorId : undefined}
          aria-invalid={Boolean(error)}
          autoComplete={autoComplete}
          className={styles.entryInput}
          id={name}
          maxLength={128}
          name={name}
          onChange={(event) => onChange(event.currentTarget.value)}
          type={showEntry ? "text" : "password"}
          value={value}
        />
        <button
          aria-label={`${showEntry ? "Hide" : "Show"} ${label.toLowerCase()}`}
          className={styles.visibilityToggle}
          onClick={toggleShowEntry}
          type="button"
        >
          {showEntry ? "Hide" : "Show"}
        </button>
      </div>
      {error ? (
        <p className={styles.fieldError} id={errorId}>
          {error}
        </p>
      ) : null}
    </div>
  );
}

function StatusMessage({
  children,
  tone,
}: {
  children: React.ReactNode;
  tone: "error" | "success";
}) {
  return (
    <div
      className={`${styles.status} ${tone === "error" ? styles.statusError : styles.statusSuccess}`}
      role={tone === "error" ? "alert" : "status"}
    >
      {children}
    </div>
  );
}

function Divider({ label }: { label: string }) {
  return (
    <div className={styles.divider}>
      <span>{label}</span>
    </div>
  );
}

function GoogleButton({ href }: { href: string }) {
  return (
    <a className={styles.googleButton} href={href}>
      <GoogleMark />
      <span>Continue with Google</span>
    </a>
  );
}

function GoogleMark() {
  return (
    <svg aria-hidden="true" className={styles.googleMark} viewBox="0 0 24 24">
      <path
        d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92c-.26 1.37-1.04 2.53-2.21 3.31v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.09z"
        fill="#4285F4"
      />
      <path
        d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.24 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z"
        fill="#34A853"
      />
      <path
        d="M5.84 14.1c-.22-.66-.35-1.36-.35-2.1s.13-1.44.35-2.1V7.06H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.94l3.66-2.84z"
        fill="#FBBC05"
      />
      <path
        d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.06L5.84 9.9C6.71 7.31 9.14 5.38 12 5.38z"
        fill="#EA4335"
      />
    </svg>
  );
}

function validateEmailEntry(email: string, entry: string) {
  const errors: FieldErrors = {};
  if (!isEmail(email)) {
    errors.email = "Enter a valid email.";
  }
  if (entry.length < minEntryLength) {
    errors.password = `Use at least ${minEntryLength} characters.`;
  }
  return errors;
}

function validateResetEmail(email: string) {
  const errors: FieldErrors = {};
  if (!isEmail(email)) {
    errors.email = "Enter a valid email.";
  }
  return errors;
}

function validateResetCredentials(code: string, newEntry: string, confirmEntry: string) {
  const errors: FieldErrors = {};
  if (!code) {
    errors.code = "Enter the verification code.";
  }
  if (newEntry.length < minEntryLength) {
    errors.newEntry = `Use at least ${minEntryLength} characters.`;
  }
  if (confirmEntry !== newEntry) {
    errors.confirmEntry = "The two values need to match.";
  }
  return errors;
}

function hasErrors(errors: FieldErrors) {
  return Object.values(errors).some(Boolean);
}

function isEmail(value: string) {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value.trim());
}

function safeRedirectTo(value: string) {
  if (!value.startsWith("/") || value.startsWith("//")) {
    return defaultRedirectTo;
  }
  return value;
}

function buildGoogleHref(redirectTo: string, email: string) {
  const params = new URLSearchParams({ redirectTo });
  const normalizedEmail = email.trim();
  if (isEmail(normalizedEmail)) {
    params.set("loginHint", normalizedEmail);
  }
  return `/api/auth/login?${params.toString()}`;
}

function withEmail(pathname: string, email: string) {
  const normalizedEmail = email.trim();
  if (!isEmail(normalizedEmail)) {
    return pathname;
  }
  return `${pathname}?email=${encodeURIComponent(normalizedEmail)}`;
}

function moveToRedirect(url: string, fallbackPath: string) {
  try {
    const destination = new URL(url, window.location.origin);
    if (destination.origin === window.location.origin) {
      window.location.assign(`${destination.pathname}${destination.search}${destination.hash}`);
      return;
    }
  } catch {
    // Fall through to the local fallback.
  }
  window.location.assign(fallbackPath);
}

async function readJsonBody(response: Response): Promise<JsonBody> {
  const contentType = response.headers.get("content-type") ?? "";
  if (!contentType.includes("application/json")) {
    return null;
  }

  try {
    const parsed = await response.json() as unknown;
    return parsed && typeof parsed === "object" && !Array.isArray(parsed)
      ? parsed as Record<string, unknown>
      : null;
  } catch {
    return null;
  }
}

function textValue(value: unknown) {
  return typeof value === "string" && value.trim() ? value.trim() : null;
}

function numberValue(value: unknown) {
  return typeof value === "number" && Number.isFinite(value) ? value : null;
}

function signInErrorMessage(error: string | null) {
  switch (error) {
    case "invalid_credentials":
      return "Email or sign-in value is incorrect.";
    case "user_not_found":
      return "No account found for this email. Create one to continue.";
    case "redirect_required":
      return "This account needs browser sign-in.";
    case "rate_limited":
      return "Too many attempts. Please try again later.";
    default:
      return "We could not sign you in. Please try again.";
  }
}

function callbackErrorMessage(error: string) {
  switch (error) {
    case "callback":
    case "callback_failed":
      return "The browser sign-in could not be completed. Please try again.";
    case "access_denied":
      return "The browser sign-in was cancelled.";
    default:
      return "The browser sign-in could not be completed. Please try again.";
  }
}
