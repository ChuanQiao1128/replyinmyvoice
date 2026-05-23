import type { Metadata } from "next";
import { Geist, Geist_Mono, Instrument_Serif } from "next/font/google";
import { SiteFooter } from "../components/site-footer";
import "./globals.css";

const geist = Geist({
  subsets: ["latin"],
  variable: "--font-geist",
  display: "swap",
});

const geistMono = Geist_Mono({
  subsets: ["latin"],
  variable: "--font-geist-mono",
  display: "swap",
});

const instrumentSerif = Instrument_Serif({
  subsets: ["latin"],
  weight: "400",
  style: ["normal", "italic"],
  variable: "--font-instrument",
  display: "swap",
});

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
  twitter: {
    card: "summary_large_image",
    title: "Reply In My Voice",
    description:
      "Rewrite everyday replies so they stay clear, specific, and natural.",
  },
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html
      lang="en"
      className={`${geist.variable} ${geistMono.variable} ${instrumentSerif.variable}`}
    >
      <body>
        {children}
        <SiteFooter />
      </body>
    </html>
  );
}
