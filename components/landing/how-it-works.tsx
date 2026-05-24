export function HowItWorks() {
  return (
    <section className="block" id="how">
      <div className="wrap">
        <div className="sec-head">
          <div>
            <span className="sec-num">03 · Workflow</span>
          </div>
          <div className="sec-head-lead">
            <h2>
              Four quick steps,
              <br />
              then a reply you&apos;d actually send.
            </h2>
            <p className="lede">
              Paste the thread. Add the facts that must stay. Pick a tone.
              Compare the signal. Copy it into your email. Under a minute.
            </p>
          </div>
        </div>

        <div className="steps">
          <div className="step">
            <div className="step-head">
              <span className="step-node">01</span>
              <span className="step-line" aria-hidden="true" />
            </div>
            <h3>Paste the thread</h3>
            <p>
              Paste the message you&apos;re answering plus the rough draft you
              already have.
            </p>
            <div className="step-figure">
              <div style={{ color: "var(--muted)" }}>{"// thread.txt"}</div>
              <div style={{ paddingTop: 6 }}>From: Daniel</div>
              <div>Subject: Missed Friday&apos;s quiz</div>
              <div style={{ color: "var(--muted)", paddingTop: 4 }}>
                — rough reply —
              </div>
              <div>
                <span className="caret">▍</span>Dear Daniel, thank you very…
              </div>
            </div>
          </div>

          <div className="step">
            <div className="step-head">
              <span className="step-node">02</span>
              <span className="step-line" aria-hidden="true" />
            </div>
            <h3>Pick quick context</h3>
            <p>
              Choose audience, purpose, and anything that must stay unchanged.
              Most fields are optional.
            </p>
            <div className="step-figure">
              <div className="field-line">
                <span className="k">audience</span>
                <span className="v">student</span>
              </div>
              <div className="field-line">
                <span className="k">purpose</span>
                <span className="v">reply + next step</span>
              </div>
              <div className="field-line">
                <span className="k">keep</span>
                <span className="v">
                  <span className="chip">Room 204</span>
                  <span className="chip">Wed deadline</span>
                </span>
              </div>
            </div>
          </div>

          <div className="step">
            <div className="step-head">
              <span className="step-node">03</span>
              <span className="step-line" aria-hidden="true" />
            </div>
            <h3>Choose a tone preset</h3>
            <p>
              Choose Warm or Direct. The app shapes the reply around your
              context — never inventing.
            </p>
            <div className="step-figure">
              <div style={{ display: "flex", gap: 6, marginBottom: 8 }}>
                <span
                  className="chip"
                  style={{ background: "var(--ink)", color: "var(--bg)" }}
                >
                  ● Warm
                </span>
                <span
                  className="chip"
                  style={{
                    background: "transparent",
                    border: "1px solid var(--rule-2)",
                    color: "var(--ink-2)",
                  }}
                >
                  Direct
                </span>
              </div>
              <div style={{ color: "var(--muted)" }}>{"// preview"}</div>
              <div>
                Hi Daniel — sorry you were out
                <br />
                sick, hope you&apos;re on the mend…
              </div>
            </div>
          </div>

          <div className="step">
            <div className="step-head">
              <span className="step-node">04</span>
              <span className="step-line" aria-hidden="true" />
            </div>
            <h3>Review the signal</h3>
            <p>
              Compare the before/after AI-like signal, then copy the reply when
              it feels right.
            </p>
            <div className="step-figure">
              <div className="field-line">
                <span className="k">before</span>
                <span className="v" style={{ color: "var(--warn)" }}>
                  74%
                </span>
              </div>
              <div className="field-line">
                <span className="k">after</span>
                <span className="v" style={{ color: "var(--accent)" }}>
                  10%
                </span>
              </div>
              <div className="field-line">
                <span className="k">delta</span>
                <span className="v">−64 pts</span>
              </div>
              <div style={{ marginTop: 6, color: "var(--accent)" }}>
                ↓ ⌘ Copy reply
              </div>
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}
