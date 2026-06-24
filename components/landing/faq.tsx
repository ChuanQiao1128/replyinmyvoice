"use client";

import { useState, type KeyboardEvent } from "react";

import { buildFaqPageJsonLd } from "../seo/json-ld";

const faqs = [
  {
    q: "What does this do?",
    a: "It is the last step before you send a message that matters: paste the message, your rough reply, and the facts that must stay true, then review a clearer version.",
  },
  {
    q: "Does it invent new facts?",
    a: "No. It's designed to preserve facts and use only the context you provide. You can mark specific details as 'must stay intact' before the rewrite.",
  },
  {
    q: "Who is it for?",
    a: "Teachers replying to students and parents, people answering clients, teams handling awkward work messages, and anyone trying to make a real reply clearer before sending.",
  },
  {
    q: "Is this for assignments?",
    a: "No. It is built for real messages and replies you are responsible for sending, not writing assignments for you.",
  },
  {
    q: "Can I cancel?",
    a: "Rewrite packs are one-time with nothing to cancel. The Pro/API subscription can be paused or cancelled any time through Stripe billing from your account.",
  },
  {
    q: "Is the AI Signal a guarantee?",
    a: "No. It is a reference writing signal that helps you compare drafts. You should always review the reply before sending it.",
  },
  {
    q: "Do you save my reply content?",
    a: "The app processes reply content for the request only. It doesn't save your pasted messages or rewritten replies to the database.",
  },
  {
    q: "Who operates the product?",
    a: "Reply In My Voice is operated by TimeAwake Ltd. for practical email and message workflows. Billing runs through Stripe.",
  },
];

const faqJsonLd = buildFaqPageJsonLd(faqs);

export function FAQ() {
  const [open, setOpen] = useState(0);

  const toggle = (index: number) => setOpen(index === open ? -1 : index);
  const onKeyDown = (event: KeyboardEvent<HTMLDivElement>, index: number) => {
    if (event.key === "Enter" || event.key === " ") {
      event.preventDefault();
      toggle(index);
    }
  };

  return (
    <section className="block" id="faq">
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(faqJsonLd) }}
      />
      <div className="wrap">
        <div className="sec-head">
          <div>
            <span className="sec-num">07 · Questions</span>
          </div>
          <div className="sec-head-lead">
            <h2>FAQ.</h2>
            <p className="lede">
              The short answers people usually want before they sign up.
            </p>
          </div>
        </div>

        <div className="faq-list">
          {faqs.map((item, index) => {
            const isOpen = index === open;
            return (
              <div
                key={item.q}
                className={"faq-item" + (isOpen ? " open" : "")}
                role="button"
                tabIndex={0}
                aria-expanded={isOpen}
                onClick={() => toggle(index)}
                onKeyDown={(event) => onKeyDown(event, index)}
              >
                <div className="faq-q">{item.q}</div>
                <div className="faq-toggle" aria-hidden="true" />
                <div className="faq-a">{item.a}</div>
              </div>
            );
          })}
        </div>
      </div>
    </section>
  );
}
