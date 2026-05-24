"use client";

import { useEffect, useState } from "react";

type NatBarProps = {
  before: number;
  after: number;
  /** Animate the fills from 0 on mount / when the values change. */
  animate?: boolean;
};

/**
 * Naturalness Check meter — a draft fill (warn) behind a shorter rewrite fill
 * (accent) in one track, so the visible gap shows how far the signal dropped.
 * Purely visual: the numbers live in the adjacent pills / legend, and the
 * values are exposed to assistive tech via the bar's aria-label.
 */
export function NatBar({ before, after, animate = false }: NatBarProps) {
  const [b, setB] = useState(animate ? 0 : before);
  const [a, setA] = useState(animate ? 0 : after);

  useEffect(() => {
    if (!animate) {
      setB(before);
      setA(after);
      return;
    }
    setB(0);
    setA(0);
    const t1 = setTimeout(() => setB(before), 80);
    const t2 = setTimeout(() => setA(after), 480);
    return () => {
      clearTimeout(t1);
      clearTimeout(t2);
    };
  }, [before, after, animate]);

  return (
    <div
      className="nat-bar"
      role="img"
      aria-label={`Draft signal ${before} percent, rewrite signal ${after} percent`}
    >
      <div className="nat-track" />
      <div className="nat-before" style={{ width: `${b}%` }} />
      <div className="nat-after" style={{ width: `${a}%` }} />
    </div>
  );
}
