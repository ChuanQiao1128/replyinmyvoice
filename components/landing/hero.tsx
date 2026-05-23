import Link from "next/link";

import { InteractiveDemo } from "./interactive-demo";

const stats = [
  { v: "3 free", l: "rewrites after sign-up" },
  { v: "40 / mo", l: "on the NZD $9 plan" },
  { v: "Warm · Direct", l: "simple tone presets" },
  { v: "Fact-aware", l: "preserves what must stay intact" },
];

export function Hero() {
  return (
    <header className="hero">
      <div className="wrap">
        <div className="eyebrow">
          <span className="dot" />
          Now available · NZD $9 / month · 40 rewrites
        </div>
        <h1 style={{ marginTop: 28 }}>
          Send the message
          <br />
          you&apos;ve been <span className="alt">avoiding.</span>
        </h1>
        <p className="hero-lead">
          Turn rough, awkward, or too-stiff drafts into clear, natural replies
          for extension requests, lecturer emails, client replies, and
          group-project messages while keeping your facts intact.
        </p>
        <div className="hero-cta">
          <Link href="/sign-up" className="btn btn-primary btn-lg">
            Start rewriting <span className="btn-arrow">→</span>
          </Link>
          <a href="#workflow" className="btn btn-ghost btn-lg">
            See examples
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
