import { ImageResponse } from "next/og";

export const runtime = "edge";
export const alt = "Reply In My Voice";
export const size = { width: 1200, height: 630 };
export const contentType = "image/png";

export default function OpengraphImage() {
  return new ImageResponse(
    (
      <div
        style={{
          width: "100%",
          height: "100%",
          display: "flex",
          flexDirection: "column",
          justifyContent: "center",
          padding: "80px",
          background: "#F7F4EE",
          color: "#1F2024",
          fontFamily: "system-ui, sans-serif",
        }}
      >
        <div style={{ fontSize: 32, letterSpacing: 4, textTransform: "uppercase", color: "#B4471F" }}>
          Reply In My Voice
        </div>
        <div style={{ fontSize: 76, fontWeight: 600, marginTop: 24, lineHeight: 1.1 }}>
          Clear, natural replies — without the typing.
        </div>
        <div style={{ fontSize: 30, marginTop: 28, color: "#4A4A4A" }}>
          Rewrite drafts for students, customers, colleagues, and clients.
        </div>
      </div>
    ),
    { ...size },
  );
}
