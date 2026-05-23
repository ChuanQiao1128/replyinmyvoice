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
              A reference signal,
              <br />
              not a verdict.
            </h2>
            <p className="lede">
              The Naturalness Check compares how AI-like the draft and rewrite
              feel using a third-party writing signal. It&apos;s a reference — not a
              guarantee. You always review before sending.
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
              The Naturalness Check is a reference writing signal that helps you
              compare how natural the draft and the rewrite feel. It&apos;s not a
              guarantee of any outcome. Always review the reply before sending
              it.
            </div>
          </div>

          <div className="nat-aside">
            <h3>We measure the change, you make the call.</h3>
            <p>
              The score is a useful sanity check — not a green light. A lower
              number generally means the rewrite reads less like generic AI
              output, which usually means more like a person.
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
