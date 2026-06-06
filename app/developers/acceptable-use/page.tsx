import type { Metadata } from "next";

import { SiteHeader } from "../../../components/site-header";

export const metadata: Metadata = {
  title: "Acceptable Use Policy",
  description:
    "Draft Acceptable Use Policy for the Reply In My Voice developer API.",
};

const sections = [
  {
    title: "Status",
    text: "This Acceptable Use Policy is Draft — pending review. It applies to API access for Reply In My Voice, operated by TimeAwake Ltd at replyinmyvoice.com.",
  },
  {
    title: "Content you submit",
    text: "Do not use the API for illegal, deceptive, abusive, or harassing content. You are responsible for the content you submit to the API and for any message you decide to send after reviewing the output.",
  },
  {
    title: "Respectful communication",
    text: "The API is intended for practical reply-writing workflows that preserve facts and make communication clearer. Do not use it to threaten, intimidate, discriminate, or target people unfairly.",
  },
  {
    title: "System integrity",
    text: "Do not attempt to overload or reverse-engineer the service, interfere with rate limits, probe private implementation details, scrape internal behavior, or use the API in a way that harms availability for other customers.",
  },
  {
    title: "Access and resale",
    text: "Do not resell raw access to the API, share keys outside your organization, or present the API as a standalone pass-through service. Build integrations that add clear customer value and keep keys under your control.",
  },
  {
    title: "Your customers and users",
    text: "If you connect the API to your own product, you are responsible for your customer-facing workflow, consent notices, review steps, and any message your users choose to send.",
  },
  {
    title: "Enforcement",
    text: "TimeAwake Ltd may rate-limit, suspend, revoke keys, or close API access for policy violations, operational risk, unpaid access, or security concerns.",
  },
  {
    title: "Contact",
    text: "Questions about this draft policy can be sent to TimeAwake Ltd at info@timeawake.co.nz.",
  },
];

export default function AcceptableUsePage() {
  return (
    <main className="rimv">
      <SiteHeader />
      <section className="page dev-page">
        <div className="wrap" style={{ maxWidth: 980 }}>
          <div className="page-head">
            <div className="eyebrow">
              <span className="dot" />
              Draft — pending review
            </div>
            <h1>Acceptable Use Policy</h1>
            <p className="lede">
              Draft API use rules for responsible reply-writing integrations on
              replyinmyvoice.com.
            </p>
          </div>

          <div className="card-stack">
            {sections.map((section) => (
              <article className="v2card" key={section.title}>
                <h2 style={{ fontSize: 18 }}>{section.title}</h2>
                <p>{section.text}</p>
              </article>
            ))}
          </div>
        </div>
      </section>
    </main>
  );
}
