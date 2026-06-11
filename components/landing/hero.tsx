import Link from "next/link";

import { InteractiveDemo } from "./interactive-demo";

const stats = [
  { v: "Trial code", l: "redeem for 3 rewrites" },
  { v: "Any draft", l: "emails, replies, notes, messages" },
  { v: "Facts intact", l: "no invented details" },
  { v: "No bad result", l: "or it doesn't count" },
];

export function Hero({ signedIn = false }: { signedIn?: boolean }) {
  return (
    <header className="hero">
      <div className="wrap">
        <div className="eyebrow">
          <span className="dot" />
          Redeem a trial code · Buy rewrites from NZ$2.50 · Pro/API for developers
        </div>
        <h1 style={{ marginTop: 28 }}>
          Send it like
          <br />
          you <span className="alt">wrote it.</span>
        </h1>
        <p className="hero-lead">
          Paste a draft that sounds stiff, generic, or too polished. Reply In
          My Voice rewrites it into a clear, natural message — your meaning and
          facts intact.
        </p>
        <div className="hero-cta">
          {signedIn ? (
            <Link href="/app" className="btn btn-primary btn-lg">
              Open your workspace <span className="btn-arrow">→</span>
            </Link>
          ) : (
            <Link href="/sign-up" className="btn btn-primary btn-lg">
              Start rewriting <span className="btn-arrow">→</span>
            </Link>
          )}
          <a href="#workflow" className="btn btn-ghost btn-lg">
            See an example — no sign-up
          </a>
        </div>
        <div className="hero-stats">
          {stats.map((stat) => (
            <div className="hero-stat" key={stat.v}>
              <div className="v">{stat.v}</div>
              <div className="l">{stat.l}</div>
            </div>
          ))}
        </div>

        <InteractiveDemo />
      </div>
    </header>
  );
}
