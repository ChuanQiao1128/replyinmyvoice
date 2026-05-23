import { NatBar } from "./nat-bar";

export function Naturalness() {
  return (
    <section className="block" id="naturalness" style={{ background: "var(--bg-2)" }}>
      <div className="wrap">
        <div className="sec-head">
          <div>
            <span className="sec-num">04 · Signal</span>
          </div>
          <div className="sec-head-lead">
            <h2>
              Does this
              <br />
              sound natural?
            </h2>
            <p className="lede">
              Tone check compares the draft and rewrite using a third-party
              writing signal. It&apos;s a reference — not a guarantee. You always
              review before sending.
            </p>
          </div>
        </div>

        <div className="nat-section">
          <div className="nat-callout">
            <h3>Example · Teacher reply</h3>
            <NatBar before={81} after={39} animate />
            <div className="nat-legend">
              <span>
                <span className="sw b" />
                Draft signal · 81%
              </span>
              <span>
                <span className="sw a" />
                Rewrite signal · 39%
              </span>
            </div>
            <div className="nat-disclaimer">
              Tone check is a reference writing signal that helps you compare
              how natural the draft and the rewrite feel. It&apos;s not a guarantee
              of any outcome. Always review the reply before sending it.
            </div>
          </div>

          <div className="nat-aside">
            <h3>Use the signal, then make the call.</h3>
            <p>
              The score is a useful sanity check — not a green light. A lower
              number generally means the rewrite reads less stiff or generic.
            </p>
            <p>
              But you&apos;re the one sending the reply. The signal is there to help
              you decide, not to decide for you.
            </p>
          </div>
        </div>
      </div>
    </section>
  );
}
