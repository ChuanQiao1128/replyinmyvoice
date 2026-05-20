import type { Metadata } from "next";
import { ClerkProvider } from "@clerk/nextjs";
import "./globals.css";

export const metadata: Metadata = {
  metadataBase: new URL("https://replyinmyvoice.com"),
  title: {
    default: "Reply In My Voice",
    template: "%s | Reply In My Voice",
  },
  description:
    "Turn rough drafts into clear, natural replies for students, customers, colleagues, and clients.",
  openGraph: {
    title: "Reply In My Voice",
    description:
      "Rewrite everyday replies so they stay clear, specific, and natural.",
    siteName: "Reply In My Voice",
    type: "website",
    url: "https://replyinmyvoice.com",
  },
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  const clerkProxyUrl = process.env.NEXT_PUBLIC_CLERK_PROXY_URL || undefined;
  const clerkJsUrl = process.env.NEXT_PUBLIC_CLERK_JS_URL || undefined;

  return (
    <ClerkProvider clerkJSUrl={clerkJsUrl} proxyUrl={clerkProxyUrl}>
      <html lang="en">
        <body>{children}</body>
      </html>
    </ClerkProvider>
  );
}
