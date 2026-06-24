"use client";

import { useState } from "react";

import { homepageSampleCases } from "./sample-cases";
import { NatBar } from "./nat-bar";

/**
 * Hero centerpiece — a tabbed rough-draft → in-your-voice comparison for each
 * real use case, with the Naturalness Check meter and a copy button. Samples
 * come from the documented, test-aligned fixtures.
 */
export function InteractiveDemo() {
  const [index, setIndex] = useState(0);
  const [copied, setCopied] = useState(false);
  const [expanded, setExpanded] = useState(false);

  const sample = homepageSampleCases[index];
  const delta = sample.before - sample.after;

  async function copyReply() {
    try {
      await navigator.clipboard.writeText(sample.rewrite);
      setCopied(true);
      window.setTimeout(() => setCopied(false), 1600);
    } catch {
      // Clipboard unavailable (e.g. insecure context) — leave the label as-is.
    }
  }

  return (
    <div className="compare" id="workflow">
      <div className="compare-tabs">
        {homepageSampleCases.map((item, itemIndex) => (
          <button
            key={item.label}
            type="button"
            className={"compare-tab " + (itemIndex === index ? "active" : "")}
            onClick={() => {
              setIndex(itemIndex);
              setExpanded(false);
            }}
            aria-pressed={itemIndex === index}
          >
            <span className="ico" aria-hidden="true">
              {item.icon}
            </span>
            <span>{item.label}</span>
          </button>
        ))}
        <div className="compare-context">CTX · {sample.context}</div>
      </div>

      <div className="compare-cols">
        <div className="compare-col before">
          <h4>
            Rough draft
            <span className="pill">rough · {sample.before}%</span>
          </h4>
          <div className={"compare-body" + (expanded ? "" : " clamped")}>
            {sample.draft}
          </div>
        </div>
        <div className="compare-arrow" aria-hidden="true">
          →
        </div>
        <div className="compare-col after">
          <h4>
            In your voice
            <span className="pill">rewrite · {sample.after}%</span>
          </h4>
          <div className={"compare-body" + (expanded ? "" : " clamped")}>
            {sample.rewrite}
          </div>
        </div>
      </div>

      <button
        type="button"
        className="compare-expand"
        onClick={() => setExpanded((value) => !value)}
        aria-expanded={expanded}
      >
        {expanded ? "Show less ↑" : "Show full reply ↓"}
      </button>

      <div className="compare-foot">
        <div className="nat">
          <div className="nat-label">
            AI Signal
            <span
              className="q"
              title="Naturalness reference, not a guarantee"
              aria-hidden="true"
            >
              ?
            </span>
          </div>
          <NatBar
            key={index}
            before={sample.before}
            after={sample.after}
            animate
          />
          <div className="nat-delta">−{delta} pts</div>
        </div>
        <button type="button" className="copy-btn" onClick={copyReply}>
          {copied ? "✓ Copied" : "⌘ Copy reply"}
        </button>
      </div>
    </div>
  );
}
