"use client";

// Replaces the root layout when an error escapes it, so it must render its own
// <html>/<body> and cannot rely on globals.css being loaded.
export default function GlobalError({ reset }: { error: Error; reset: () => void }) {
  return (
    <html lang="en">
      <body
        style={{
          margin: 0,
          minHeight: "100vh",
          display: "grid",
          placeItems: "center",
          background: "#f7f5ef",
          color: "#12160e",
          fontFamily:
            "ui-sans-serif, system-ui, -apple-system, 'Helvetica Neue', sans-serif",
          padding: "64px 20px",
        }}
      >
        <div style={{ textAlign: "center", maxWidth: 440 }}>
          <h1 style={{ fontSize: 28, margin: "0 0 10px" }}>Something went wrong</h1>
          <p style={{ color: "#5b6253", margin: "0 0 24px" }}>
            The page failed to load. Please try again.
          </p>
          <button
            type="button"
            onClick={reset}
            style={{
              display: "inline-flex",
              alignItems: "center",
              padding: "10px 18px",
              borderRadius: 12,
              border: "none",
              background: "#1a6b48",
              color: "#fff",
              fontSize: 15,
              fontWeight: 600,
              cursor: "pointer",
            }}
          >
            Try again
          </button>
        </div>
      </body>
    </html>
  );
}
