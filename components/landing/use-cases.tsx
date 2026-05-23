const useCases = [
  {
    icon: "T",
    title: "Teacher messages",
    k: "01",
    body: "Reply to students with warmth and clarity while keeping the class policy and facts intact.",
    snippet:
      "“I can look at this with you tomorrow and check it against the late-work policy before deciding the next step.”",
  },
  {
    icon: "S",
    title: "Sales follow-ups",
    k: "02",
    body: "Turn stiff follow-ups into relationship-aware notes — without inventing promises that aren't yours to make.",
    snippet:
      "“Just checking in after our demo on the 8th. No pressure either way — if the timing's wrong, I'd rather know than keep nudging.”",
  },
  {
    icon: "W",
    title: "Workplace email",
    k: "03",
    body: "Make internal updates easier to read and less formulaic. The team stops skimming.",
    snippet:
      "“Quick one — do we actually need a meeting for this, or can I send a short doc by Friday and we comment async?”",
  },
  {
    icon: "C",
    title: "Client replies",
    k: "04",
    body: "Respond to customers and clients with the right balance of care and precision. No corporate hedging.",
    snippet:
      "“I'm going to need until Tuesday rather than Friday — the data took two extra days and I'd rather hand you something I trust.”",
  },
];

export function UseCases() {
  return (
    <section className="block" id="cases">
      <div className="wrap">
        <div className="sec-head">
          <div>
            <span className="sec-num">02 · Built for</span>
          </div>
          <div className="sec-head-lead">
            <h2>
              Better everyday messages.
              <br />
              Not generic rewrites.
            </h2>
            <p className="lede">
              Reply In My Voice is designed around the moments where people
              already use draft assistance — and tuned so the output sounds like
              a person wrote it, not a template engine.
            </p>
          </div>
        </div>

        <div className="usecases">
          {useCases.map((item) => (
            <div className="uc" key={item.title}>
              <div className="uc-icon" aria-hidden="true">
                {item.icon}
              </div>
              <h3>{item.title}</h3>
              <p className="uc-body">{item.body}</p>
              <div className="uc-snippet">{item.snippet}</div>
              <div className="uc-tag">№ {item.k}</div>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
