import Link from "next/link";
import type { Metadata } from "next";

import { SiteHeader } from "../../../components/site-header";

export const metadata: Metadata = {
  title: "API Terms of Use",
  description:
    "API Terms of Use for Reply In My Voice by TimeAwake Ltd.",
};

type ApiTermsSection = {
  title: string;
  text: string;
  href?: string;
  linkText?: string;
};

const sections: ApiTermsSection[] = [
  {
    title: "Operator and status",
    text: "Reply In My Voice is operated by TimeAwake Ltd at replyinmyvoice.com. These terms set out the service boundaries for API customers.",
  },
  {
    title: "Eligibility",
    text: "You may use the API only if you can form a binding agreement for your organization or for yourself. You must keep account details accurate and use the API for lawful reply-writing workflows.",
  },
  {
    title: "API key responsibility",
    text: "API keys are credentials for your account. Store them server-side, restrict access, rotate keys when staff or systems change, and revoke any key that is no longer needed or may have been exposed.",
  },
  {
    title: "Metering and quota",
    text: "API metering is per succeeded rewrite. API calls and website rewrites use one shared quota pool on the account. There is no free tier for API access; paid quota must be available before an API key can process rewrites.",
  },
  {
    title: "Acceptable use",
    text: "Your use of the API must follow the Acceptable Use Policy. If there is a conflict between this page and that policy for content or operational conduct, the Acceptable Use Policy controls for that topic.",
    href: "/developers/acceptable-use",
    linkText: "Read the Acceptable Use Policy",
  },
  {
    title: "signal",
    text: "The signal field is an informational naturalness reference for comparing draft and rewritten text. It may change as providers, scoring, and product behavior evolve, and it is not a guarantee of how any person or system will respond to a message.",
  },
  {
    title: "Service changes and termination",
    text: "We may update the API, documentation, quotas, limits, pricing, or these terms as the service matures. We may suspend or terminate API access for unpaid accounts, security risk, harmful use, or repeated policy violations.",
  },
  {
    title: "Limitations",
    text: "The API produces assisted writing output that should be reviewed before use. To the maximum extent allowed by applicable law, TimeAwake Ltd is not liable for indirect, incidental, special, consequential, or lost-profit damages arising from API use.",
  },
  {
    title: "Contact",
    text: "Questions about these API terms can be sent to TimeAwake Ltd at info@timeawake.co.nz.",
  },
];

export default function ApiTermsPage() {
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
            <h1>API Terms of Use</h1>
            <p className="lede">
              Service terms for the Reply In My Voice API operated by
              TimeAwake Ltd at replyinmyvoice.com.
            </p>
          </div>

          <div className="card-stack">
            {sections.map((section) => (
              <article className="v2card" key={section.title}>
                <h2 style={{ fontSize: 18 }}>{section.title}</h2>
                <p>{section.text}</p>
                {section.href ? (
                  <p>
                    <Link href={section.href} className="dev-text-link">
                      {section.linkText ?? section.title}
                    </Link>
                  </p>
                ) : null}
              </article>
            ))}
          </div>
        </div>
      </section>
    </main>
  );
}
