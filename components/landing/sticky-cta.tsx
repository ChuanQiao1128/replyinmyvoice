"use client";

import { X } from "lucide-react";
import Link from "next/link";
import { useEffect, useState } from "react";

type StickyCtaProps = {
  signedIn?: boolean;
};

export function StickyCta({ signedIn = false }: StickyCtaProps) {
  const [visible, setVisible] = useState(false);
  const [dismissed, setDismissed] = useState(false);
  const href = signedIn ? "/app" : "/sign-up";
  const label = signedIn ? "Open workspace" : "Start rewriting";

  useEffect(() => {
    const hero = document.querySelector(".hero");

    if (!hero) {
      return;
    }

    const updateFromHeroPosition = () => {
      setVisible(hero.getBoundingClientRect().bottom <= 0);
    };

    if (typeof IntersectionObserver === "undefined") {
      updateFromHeroPosition();
      window.addEventListener("scroll", updateFromHeroPosition, {
        passive: true,
      });
      window.addEventListener("resize", updateFromHeroPosition);

      return () => {
        window.removeEventListener("scroll", updateFromHeroPosition);
        window.removeEventListener("resize", updateFromHeroPosition);
      };
    }

    const observer = new IntersectionObserver(([entry]) => {
      setVisible(!entry.isIntersecting && entry.boundingClientRect.bottom <= 0);
    });

    observer.observe(hero);

    return () => observer.disconnect();
  }, []);

  if (dismissed || !visible) {
    return null;
  }

  return (
    <aside
      className="sticky-mobile-cta visible"
      aria-label="Quick start"
    >
      <div className="sticky-mobile-cta-copy">
        <span className="sticky-mobile-cta-label">Ready to rewrite?</span>
        <span className="sticky-mobile-cta-note">Keep facts. Fix the feel.</span>
      </div>
      <div className="sticky-mobile-cta-actions">
        <Link href={href} className="btn btn-primary">
          {label}
        </Link>
        <button
          type="button"
          className="sticky-mobile-cta-close"
          aria-label="Dismiss quick start"
          onClick={() => setDismissed(true)}
        >
          <X size={16} strokeWidth={2} aria-hidden="true" />
        </button>
      </div>
    </aside>
  );
}
