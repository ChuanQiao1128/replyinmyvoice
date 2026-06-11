import type { Metadata } from "next";

import { SiteHeader } from "../../../components/site-header";

export const metadata: Metadata = {
  title: "Data & Retention",
  description:
    "Data & Retention notes for the Reply In My Voice developer API.",
};

const sections = [
  {
    title: "Scope",
    text: "This Data & Retention page describes API-originated data handling for Reply In My Voice, operated by TimeAwake Ltd at replyinmyvoice.com.",
  },
  {
    title: "What is stored",
    text: "For API rewrite jobs, Reply In My Voice stores request metadata plus the input and output on RewriteAttempt so async polling, idempotency, account history, support, and quota reconciliation can work.",
  },
  {
    title: "Retention window",
    text: "API-originated request and result content has a bounded 30-day retention window. Workspace history may be retained for up to 90 days, while API request and result records use a separate 30-day retention window. After that window, stored input and output content is purged while operational records needed for account, billing, security, and abuse review may be retained.",
  },
  {
    title: "Processing and residency",
    text: "API request and result content is processed by Reply In My Voice systems and the rewrite and naturalness providers needed to provide the service. Those providers may process data in the locations where they operate their managed services; TimeAwake Ltd does not promise a specific country, region, or single-region residency unless a separate written agreement says so.",
  },
  {
    title: "Deletion",
    text: "You can delete your account from /app/account or request deletion of API-originated content by contacting TimeAwake Ltd at info@timeawake.co.nz. TimeAwake Ltd will respond to deletion requests within 30 days. Deletion is available for retained request and result content, subject to account, security, billing, and legal obligations.",
  },
  {
    title: "Processors",
    text: "API content is processed by the rewrite and naturalness providers needed to provide the service. Hosting, database, authentication, billing, and operational providers may also process the minimum data needed for their role.",
  },
  {
    title: "API keys and account records",
    text: "The service stores API key metadata, masked key previews, revocation status, usage counts, rate-limit data, and account identifiers needed to authenticate requests and operate paid quota.",
  },
  {
    title: "Sensitive content",
    text: "Do not submit passwords, payment card details, government identifiers, or highly sensitive personal information unless your own workflow has a clear need and permission to do so.",
  },
  {
    title: "Contact",
    text: "Questions about this data page can be sent to TimeAwake Ltd at info@timeawake.co.nz.",
  },
];

export default function ApiDataRetentionPage() {
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
            <h1>Data & Retention</h1>
            <p className="lede">
              API data handling notes for Reply In My Voice request and
              result content on replyinmyvoice.com.
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
