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
    <section className="mx-auto max-w-6xl px-6 py-16">
      <h2 className="text-3xl font-semibold md:text-4xl">FAQ</h2>
      <div className="mt-8 grid gap-4 md:grid-cols-2">
        {faqs.map((item) => (
          <div key={item.question} className="rounded-lg border border-line bg-white/65 p-5">
            <h3 className="font-semibold">{item.question}</h3>
            <p className="mt-2 text-sm leading-6 text-ink/65">{item.answer}</p>
          </div>
        ))}
      </div>
    </section>
  );
}
