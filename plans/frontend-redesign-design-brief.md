# Frontend Redesign Design Brief

Issue: M4-011
Date: 2026-05-22

## Current-State Critique

- Philosophy alignment: 6 / 10. The product is positioned as a calm communication tool, but the current pages still read like a generic SaaS template with repeated card grids and thin product specificity.
- Visual hierarchy: 6 / 10. The hero message is clear, yet many sections use similar card scale, label styling, and spacing, so the page rhythm flattens after the first viewport.
- Craft quality: 6 / 10. The Tailwind tokens are compact, but the palette is mostly warm neutrals and clay, radius usage is inconsistent on auth, and several label treatments rely on wide letter spacing.
- Functionality: 7 / 10. The routes expose the right actions and trust copy, though the landing/pricing pages duplicate framing and the workspace has less scan-friendly structure than a repeated-use tool needs.
- Originality: 5 / 10. The interactive demo helps, but the surrounding surface is still a familiar card stack instead of a product-specific writing workflow.

Average: 6.0 / 10

## Design Decisions

- Palette: Warm paper and ink remain the base, with muted evergreen, brick, soft blue-gray, and gold accents added as structured tokens. Color is used for workflow state and emphasis, not decoration.
- Typography: Keep the current system sans-serif for speed and reliability. Use stronger type contrast through size, weight, and line height instead of letter spacing treatments.
- Layout: Replace the split hero with a full-width writing-desk/workspace scene behind the foreground message. Use full-width bands for page sections, constrained content widths, and denser app workspace columns.
- Shape and surfaces: Radius stays at 8px or less. Use hairline borders, low shadows, and paper-like panels. Avoid nested cards by making pricing and landing sections unframed layouts with cards only for repeated items or focused panels.
- Motion: Keep motion subtle: hover color shifts, small lift on actionable panels, and the existing loading step animation. No large decorative animation.
- Assets: Use an in-product correspondence/workspace scene built from real UI content and sample messages instead of stock imagery. No fabricated testimonials, logos, metrics, or screenshots.

## Implementation Notes

- Preserve auth, rewrite, quota, billing, API, telemetry, and webhook behavior.
- Keep the public product line present: "Replies that still sound like you."
- Improve `/app` as a working surface rather than a marketing page.
- Browser verification is required, but the local sandbox currently blocks binding a dev server to localhost; this will be retried after implementation and documented if still blocked.

## Final Critique

Pending implementation and browser/static verification.
