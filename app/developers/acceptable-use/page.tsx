import type { Metadata } from "next";

import { SiteHeader } from "../../../components/site-header";

export const metadata: Metadata = {
  title: "Acceptable Use Policy",
  description:
    "Acceptable Use Policy for the Reply In My Voice developer API.",
};

const sections = [
  {
    title: "Scope",
    text: "This Acceptable Use Policy applies to API access for Reply In My Voice, operated by TimeAwake Ltd at replyinmyvoice.com.",
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
    text: "Do not resell raw access to the API. Raw access means making Reply In My Voice API capacity, credentials, or endpoints available as the main thing you sell, instead of embedding it inside your own reviewed product workflow. Allowed example: a value-add integration that uses your server-side key inside your product, adds your own customer workflow, and keeps keys under your control. Disallowed example: a bare pass-through service or key sharing arrangement that lets others use Reply In My Voice directly through your account.",
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
    text: "Questions about this policy can be sent to TimeAwake Ltd at info@timeawake.co.nz.",
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
              Effective 6 June 2026
            </div>
            <h1>Acceptable Use Policy</h1>
            <p className="lede">
              API use rules for responsible reply-writing integrations on
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
