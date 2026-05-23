"use client";

import { useEffect, useState } from "react";

type NatBarProps = {
  before: number;
  after: number;
  /** Animate the fills from 0 on mount / when the values change. */
  animate?: boolean;
};

/**
 * Naturalness Check meter — two stacked fills (draft vs rewrite) inside a
 * single track. Lower is better, so the rewrite fill sits in front and the
 * draft fill shows how far the signal dropped.
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
    <div className="nat-bar">
      <div className="nat-track" />
      <div className="nat-before" style={{ width: `${b}%` }}>
        <span className="lbl">Draft {before}%</span>
      </div>
      <div className="nat-after" style={{ width: `${a}%` }}>
        <span className="lbl">Rewrite {after}%</span>
      </div>
    </div>
  );
}
