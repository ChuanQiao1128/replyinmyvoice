import Link from "next/link";

export default function NotFound() {
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
          404
        </p>
        <h1 style={{ fontSize: 30, margin: "8px 0 10px" }}>
          This page wandered off
        </h1>
        <p style={{ color: "var(--ink-3)", marginBottom: 24 }}>
          The page you&apos;re looking for doesn&apos;t exist or has moved.
        </p>
        <div
          style={{
            display: "flex",
            gap: 10,
            justifyContent: "center",
            flexWrap: "wrap",
          }}
        >
          <Link href="/" className="btn btn-primary">
            Back home
          </Link>
          <Link href="/app" className="btn btn-ghost">
            Open the app
          </Link>
        </div>
      </div>
    </main>
  );
}
