"use client";

import Link from "next/link";

export default function AppError({ reset }: { error: Error; reset: () => void }) {
  return (
    <main
      className="rimv"
      style={{
        minHeight: "68vh",
        display: "grid",
        placeItems: "center",
        padding: "64px 20px",
      }}
    >
      <div style={{ textAlign: "center", maxWidth: 460 }}>
        <p
          style={{
            fontSize: 13,
            fontWeight: 600,
            letterSpacing: "0.08em",
            textTransform: "uppercase",
            color: "var(--muted)",
            margin: 0,
          }}
        >
          Something went wrong
        </p>
        <h1 style={{ fontSize: 30, margin: "8px 0 10px" }}>We hit a snag</h1>
        <p style={{ color: "var(--ink-3)", marginBottom: 24 }}>
          That didn&apos;t load as expected. Try again, or head back home. Your
          account and balance are safe.
        </p>
        <div
          style={{
            display: "flex",
            gap: 10,
            justifyContent: "center",
            flexWrap: "wrap",
          }}
        >
          <button type="button" className="btn btn-primary" onClick={reset}>
            Try again
          </button>
          <Link href="/" className="btn btn-ghost">
            Back home
          </Link>
        </div>
      </div>
    </main>
  );
}
