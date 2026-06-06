import type { Metadata } from "next";

import { SiteHeader } from "../../../components/site-header";

export const metadata: Metadata = {
  title: "Data & Retention",
  description:
    "Draft Data & Retention notes for the Reply In My Voice developer API.",
};

const sections = [
  {
    title: "Status",
    text: "This Data & Retention page is Draft — pending review. It describes the intended API-originated data handling for Reply In My Voice, operated by TimeAwake Ltd at replyinmyvoice.com.",
  },
  {
    title: "What is stored",
    text: "For API rewrite jobs, Reply In My Voice stores request metadata plus the input and output on RewriteAttempt so async polling, idempotency, account history, support, and quota reconciliation can work.",
  },
  {
    title: "Retention window",
    text: "API-originated request and result content has a bounded 30-day retention window. After that window, stored input and output content is purged while operational records needed for account, billing, security, and abuse review may be retained.",
  },
  {
    title: "Deletion",
    text: "You can request deletion of API-originated content by contacting TimeAwake Ltd at info@timeawake.co.nz. Deletion is available for retained request and result content, subject to account, security, billing, and legal obligations.",
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
    text: "Questions about this draft data page can be sent to TimeAwake Ltd at info@timeawake.co.nz.",
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
              Draft — pending review
            </div>
            <h1>Data & Retention</h1>
            <p className="lede">
              Draft API data handling notes for Reply In My Voice request and
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
