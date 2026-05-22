const faqs = [
  {
    question: "What does this do?",
    answer:
      "It turns rough or too-generic reply drafts into clearer, more natural messages for everyday communication.",
  },
  {
    question: "Does it invent new facts?",
    answer:
      "No. It is designed to preserve facts and use only the context you provide.",
  },
  {
    question: "Who is it for?",
    answer:
      "Teachers, sales teams, workplace communicators, and anyone replying to students, customers, colleagues, or clients.",
  },
  {
    question: "Can I cancel?",
    answer: "Yes. The monthly plan can be managed through Stripe billing.",
  },
  {
    question: "Is the Naturalness Check a guarantee?",
    answer:
      "No. It is a reference writing signal that helps compare drafts. You should still review the reply before sending.",
  },
  {
    question: "Do you save my reply content?",
    answer:
      "The app processes reply content for the request. It does not save pasted messages or rewritten replies to the database.",
  },
  {
    question: "Who operates the product?",
    answer:
      "Reply In My Voice is operated by TimeAwake Ltd. for practical email and message workflows.",
  },
];

export function FAQ() {
  return (
    <section className="mx-auto max-w-3xl px-4 py-16 sm:px-6">
      <h2 className="text-3xl font-semibold md:text-4xl">FAQ</h2>
      <div className="mt-8 divide-y divide-line border-y border-line">
        {faqs.map((item, index) => (
          <details className="group py-5" key={item.question} open={index === 0}>
            <summary className="flex cursor-pointer list-none items-center justify-between gap-4 text-left">
              <span className="font-semibold">{item.question}</span>
              <span className="rounded-md border border-line px-2 py-0.5 text-sm text-ink/55 transition group-open:rotate-45">
                +
              </span>
            </summary>
            <p className="mt-3 max-w-3xl text-sm leading-6 text-ink/65">
              {item.answer}
            </p>
          </details>
        ))}
      </div>
    </section>
  );
}
