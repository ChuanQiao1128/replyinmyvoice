import type { Metadata } from "next";
import Link from "next/link";

import { SiteHeader } from "../../components/site-header";

export const metadata: Metadata = {
  title: "Student email & message rewriter",
  description:
    "Rewrite real student messages — extension requests, lecturer emails, internship follow-ups — so they sound clear, polite, and natural while preserving the facts.",
};

const situations = [
  {
    title: "Ask for an assignment extension",
    before:
      "Hi, I can't submit. I had personal stuff. Can I get more time?",
    after:
      "Hi Dr Chen, I'm sorry for the late notice. I've had a personal issue this week and I'm worried I won't make tonight's deadline. Would it be possible to request a short extension? I'm happy to follow any formal process you need.",
  },
  {
    title: "Email your lecturer after missing class",
    before:
      "Sorry I missed class. I was sick. Did I miss anything important?",
    after:
      "Hi Dr Chen, I'm sorry I missed class on Tuesday. I was unwell and could not attend. Could you please let me know if there are notes or tasks I should catch up on before the next class?",
  },
  {
    title: "Follow up on an internship application",
    before:
      "Hi, just checking if you saw my internship application. I really want it.",
    after:
      "Hi, I hope you're well. I wanted to follow up on my internship application and ask whether there are any updates on the next step. I'm still very interested in the role and would be happy to provide anything else you need.",
  },
  {
    title: "Tell a group member they need to contribute",
    before:
      "You haven't done anything yet and we need this finished. Please do your part.",
    after:
      "Hi Alex, I wanted to check in because the group submission is coming up and we still need your section. Could you please send your part by Tuesday so we have time to put everything together?",
  },
  {
    title: "Ask student services / admin for help",
    before:
      "I can't find where to upload this form. Can someone help me?",
    after:
      "Hi, I'm trying to submit a student services form, but I can't find the correct place to upload it. Could you please point me to the right page or let me know the best way to send it through?",
  },
  {
    title: "Reply politely when you are stressed or upset",
    before:
      "I don't think this is fair and I'm really frustrated about it.",
    after:
      "Hi, I understand there may be a process I need to follow, but I'm feeling frustrated about the outcome and would like to understand it better. Could you please explain the next step or let me know who I should speak with?",
  },
];

const reasons = [
  "One click on your actual message and draft, without writing a careful prompt first.",
  "It keeps your supplied facts in view and should not add details you did not give it.",
  "Naturalness Check lets you compare the draft and rewrite before you send.",
];

export default function StudentsPage() {
  return (
    <main className="rimv">
      <SiteHeader />

      <header className="hero student-hero">
        <div className="wrap student-hero-grid">
          <div className="student-hero-copy">
            <div className="eyebrow">
              <span className="dot" />
              Extension requests · lecturer emails · internship follow-ups
            </div>
            <h1 style={{ marginTop: 28 }}>
              Sound like yourself when the message matters.
            </h1>
            <p className="hero-lead">
              Turn rough student replies into clear, polite messages for
              extension requests, lecturer emails, internship follow-ups,
              group-project messages, and awkward replies when you are stressed.
            </p>
            <div className="hero-cta">
              <Link href="/sign-up" className="btn btn-primary btn-lg">
                Try 3 free rewrites — no card{" "}
                <span className="btn-arrow">→</span>
              </Link>
              <Link href="/" className="btn btn-ghost btn-lg">
                Back to home
              </Link>
            </div>
          </div>

          <div className="student-preview" aria-label="Before and after preview">
            <div className="compare-cols">
              <div className="compare-col before">
                <h4>Before</h4>
                <p className="compare-body">
                  {
                    "Hi, I know this is late but I need more time. I don't know what to say."
                  }
                </p>
              </div>
              <div className="compare-col after">
                <h4>After</h4>
                <p className="compare-body">
                  {
                    "Hi Dr Chen, I'm sorry for the late notice. I'm having trouble finishing the assignment on time and wanted to ask whether a short extension is possible. I'm happy to follow the formal process if needed."
                  }
                </p>
              </div>
              <div className="compare-arrow" aria-hidden="true">
                →
              </div>
            </div>
          </div>
        </div>
      </header>

      <section className="block" id="student-situations">
        <div className="wrap">
          <div className="sec-head">
            <div>
              <span className="sec-num">01 · Student replies</span>
            </div>
            <div className="sec-head-lead">
              <h2>For messages you actually need to send.</h2>
              <p className="lede">
                These examples are illustrative. Paste your own message, rough
                draft, and facts before choosing what to send.
              </p>
            </div>
          </div>

          <div className="student-situations">
            {situations.map((situation, index) => (
              <article className="student-card" key={situation.title}>
                <div className="uc-tag">№ {String(index + 1).padStart(2, "0")}</div>
                <h3>{situation.title}</h3>
                <div className="student-example">
                  <span>Before</span>
                  <p>{situation.before}</p>
                </div>
                <div className="student-example student-example-after">
                  <span>After</span>
                  <p>{situation.after}</p>
                </div>
              </article>
            ))}
          </div>
        </div>
      </section>

      <section className="block">
        <div className="wrap">
          <div className="sec-head">
            <div>
              <span className="sec-num">02 · Why this</span>
            </div>
            <div className="sec-head-lead">
              <h2>Why not just use ChatGPT?</h2>
            </div>
          </div>

          <div className="student-reasons">
            {reasons.map((reason, index) => (
              <div className="student-reason" key={reason}>
                <span>{String(index + 1).padStart(2, "0")}</span>
                <p>{reason}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      <section className="final">
        <div className="wrap">
          <div className="final-card">
            <div>
              <div className="eyebrow" style={{ color: "var(--bg)" }}>
                <span
                  className="dot"
                  style={{ background: "var(--accent)" }}
                />
                Student messages
              </div>
              <h2 style={{ marginTop: 20 }}>
                Write the reply, then decide if it sounds right.
              </h2>
              <p>
                Start with three successful free rewrites after sign-up. No
                card is needed to try the workspace.
              </p>
            </div>
            <div className="cta-side">
              <Link href="/sign-up" className="btn btn-accent">
                Try 3 free rewrites — no card{" "}
                <span className="btn-arrow">→</span>
              </Link>
            </div>
          </div>
        </div>
      </section>
    </main>
  );
}
