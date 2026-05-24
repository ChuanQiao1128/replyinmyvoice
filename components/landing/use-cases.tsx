const useCases = [
  {
    icon: "E",
    title: "Extension requests",
    k: "01",
    body: "Ask for more time without sounding careless, vague, or over-apologetic.",
    snippet:
      "I know this is late, but I wanted to ask whether a short extension is possible and explain what happened clearly.",
  },
  {
    icon: "L",
    title: "Lecturer emails",
    k: "02",
    body: "Turn nervous drafts into polite, specific messages that keep the actual situation intact.",
    snippet:
      "I missed class because I was unwell. Could you please let me know what I should catch up on before next week?",
  },
  {
    icon: "C",
    title: "Client replies",
    k: "03",
    body: "Reply to delays, scope questions, and awkward updates with care and precision.",
    snippet:
      "I need until Tuesday rather than Friday, and I want to explain why without sounding defensive.",
  },
  {
    icon: "G",
    title: "Group-project messages",
    k: "04",
    body: "Ask someone to contribute without making the message sharper than it needs to be.",
    snippet:
      "We still need your section by Tuesday so the group has time to put everything together.",
  },
  {
    icon: "!",
    title: "Make this less rude",
    k: "05",
    body: "Keep the point, lower the heat, and make the next action clear before you send.",
    snippet:
      "I'm frustrated about the outcome and want to understand the next step without escalating the conversation.",
  },
];

export function UseCases() {
  const [feature, ...rest] = useCases;

  return (
    <section className="block" id="cases">
      <div className="wrap">
        <div className="sec-head">
          <div>
            <span className="sec-num">02 · Built for</span>
          </div>
          <div className="sec-head-lead">
            <h2>
              Real reply moments.
              <br />
              Not blank-box rewriting.
            </h2>
            <p className="lede">
              Reply In My Voice is built for the last step before you send:
              extension requests, lecturer emails, client replies,
              group-project messages, and drafts that need to sound less sharp.
            </p>
          </div>
        </div>

        <div className="usecases">
          <article className="uc uc-feature">
            <div className="uc-top">
              <div className="uc-icon" aria-hidden="true">
                {feature.icon}
              </div>
              <div className="uc-tag">№ {feature.k}</div>
            </div>
            <h3>{feature.title}</h3>
            <p className="uc-body">{feature.body}</p>
            <ul className="uc-points">
              <li>Keeps your reason and dates intact</li>
              <li>Warm or Direct — your call</li>
              <li>Copy-ready in a single pass</li>
            </ul>
            <div className="uc-snippet">
              <span className="uc-snippet-tag">Example draft</span>
              {feature.snippet}
            </div>
          </article>

          {rest.map((item) => (
            <article className="uc" key={item.title}>
              <div className="uc-top">
                <div className="uc-icon" aria-hidden="true">
                  {item.icon}
                </div>
                <div className="uc-tag">№ {item.k}</div>
              </div>
              <h3>{item.title}</h3>
              <p className="uc-body">{item.body}</p>
              <div className="uc-snippet">{item.snippet}</div>
            </article>
          ))}
        </div>
      </div>
    </section>
  );
}
