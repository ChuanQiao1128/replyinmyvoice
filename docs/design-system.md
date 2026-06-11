# Design System Token Reference

This reference documents the current visual token system without changing any
rendered output. Source files reviewed:

- `app/globals.css`
- `components/app/shell/shell.module.css`
- `tailwind.config.ts`

## Token Inventory

The canonical CSS custom properties live in the `:root` block of
`app/globals.css`.

### Color Tokens

| Token | Value | Purpose | Current use |
| --- | --- | --- | --- |
| `--bg` | `#f7f5ef` | Main warm page surface. | `html`, `body`, landing dark-on-light inversions, shell root. |
| `--bg-2` | `#efece2` | Secondary warm surface and subtle hover fill. | Landing mobile menu hover, signal meter track, shell skeleton gradients. |
| `--bg-3` | `#e7e3d5` | Deeper warm surface. | Shell quota track, muted badges, skeleton gradients. |
| `--card` | `#ffffff` | Primary card and panel surface. | Landing cards, compare panel, pricing cards, shell topbar/cards/menus. |
| `--card-2` | `#fbfaf4` | Secondary card surface. | Landing nested snippets, shell sidebar, shell table headers. |
| `--ink` | `#12160e` | Primary text and dark filled controls. | Base text, headings, landing buttons, dark pricing panels, shell primary text. |
| `--ink-2` | `#3d453a` | Secondary text. | Navigation links, body copy, table captions, shell secondary labels. |
| `--ink-3` | `#5b6253` | Tertiary text. | Pricing subtitles, shell page descriptions, muted card copy. |
| `--muted` | `#797f70` | Low-emphasis text. | Metadata, small labels, section numbers, shell group labels. |
| `--rule` | `#ddd8c8` | Default border and divider. | Nav border, cards, tables, shell panels, page separators. |
| `--rule-2` | `#c0baa6` | Stronger border. | Ghost buttons, mobile trigger, quota pill, dashed empty states. |
| `--accent` | `#1a6b48` | Primary brand green. | Eyebrows, dots, primary accents, checks, selected landing states. |
| `--accent-2` | `#2c9468` | Brighter green for stronger state. | Recommended pricing states, shell quota fill, shell switch-on state. |
| `--accent-deep` | `#11432e` | Deep green for dark accent surfaces and readable text on green tints. | Shell hover states, badges, code blocks. |
| `--accent-soft` | `#d6e6dd` | Green tint chip background and soft accent border. | Chips, after-state panels, shell avatars, selected nav items. |
| `--accent-tint` | `#eef4ef` | Faint green wash. | Landing feature cards, shell hover fills, upsell and callout backgrounds. |
| `--warn` | `#c2571c` | Warm warning or before-state color. | Signal before bar, warning badges, shell low-quota and warning states. |
| `--warn-2` | `#d98a4a` | Softer warning border/accent. | Shell error and danger cards. |
| `--warn-soft` | `#f4e6d6` | Warning background tint. | Developer status badges, shell warning badges and error boxes. |
| `--good` | `#1a6b48` | Success alias matching the main green. | Referenced outside the landing and shell token paths, mostly auth styling. |

### Elevation Tokens

| Token | Value | Purpose | Current use |
| --- | --- | --- | --- |
| `--shadow-sm` | `0 1px 2px rgba(17, 21, 15, 0.04), 0 3px 10px -5px rgba(17, 21, 15, 0.09)` | Small card lift. | Landing step hover, shell cards, rows, skeleton containers. |
| `--shadow-md` | `0 14px 34px -20px rgba(17, 21, 15, 0.2), 0 5px 14px -8px rgba(17, 21, 15, 0.1)` | Floating menu/card lift. | Mobile nav panel, landing callout hover, shell menus and notices. |
| `--shadow-lg` | `0 1px 0 rgba(255, 255, 255, 1) inset, 0 50px 100px -50px rgba(17, 21, 15, 0.2), 0 22px 44px -32px rgba(17, 21, 15, 0.14)` | Large drawer/modal lift. | Shell mobile drawer. |

### Radius Tokens

| Token | Value | Purpose | Current use |
| --- | --- | --- | --- |
| `--radius` | `14px` | Default card radius. | Landing use-case and workflow cards, shell list rows, stat/config cards. |
| `--radius-lg` | `20px` | Larger container radius. | Landing naturalness callout, shell section cards, empty states, danger cards. |

### Typography Tokens

| Token | Value | Purpose | Current use |
| --- | --- | --- | --- |
| `--sans` | `var(--font-geist), ui-sans-serif, system-ui, -apple-system, "Helvetica Neue", sans-serif` | Base product sans stack. | `body`, buttons, compare tabs, compare body. |
| `--mono` | `var(--font-geist-mono), "JetBrains Mono", ui-monospace, Menlo, monospace` | Metadata, code, labels, badges. | Eyebrows, stats, pricing metadata, API blocks, shell stats and code blocks. |
| `--display` | `var(--font-geist), ui-sans-serif, system-ui, sans-serif` | Display headings and prices. | `.rimv` headings, plan prices, pack prices. |
| `--serif` | `var(--font-instrument), Georgia, "Times New Roman", serif` | Editorial accent text. | Hero alternate word. |

### Spacing

There are no root spacing custom properties today. Spacing is defined directly
in landing CSS (`.wrap`, `.hero`, `section.block`, cards, pricing panels) and
through Tailwind utility classes in dashboard/admin/app surfaces.

## Landing Custom Classes

Landing and public marketing pages use custom global classes from
`app/globals.css`. These classes are generally scoped by the `.rimv` page root
and share the root token palette.

| Area | Main classes | Token mapping |
| --- | --- | --- |
| Page shell | `.rimv`, `.wrap`, `.page`, `.page-head`, `.lede` | Uses `--bg`, `--ink`, `--ink-2`, `--muted`, `--sans`, `--display`. |
| Navigation | `.nav`, `.nav-inner`, `.brand`, `.brand-mark`, `.nav-links`, `.mobile-nav-*` | Uses `--bg`, `--card`, `--ink`, `--ink-2`, `--rule`, `--rule-2`, `--accent`, `--shadow-md`. |
| Buttons | `.btn`, `.btn-primary`, `.btn-accent`, `.btn-ghost`, `.btn-lg`, `.btn-arrow` | Uses `--sans`, `--ink`, `--bg`, `--accent`, `--rule-2`. |
| Hero | `.hero`, `.hero-lead`, `.hero-cta`, `.hero-stats`, `.hero-stat` | Uses `--display`, `--serif`, `--mono`, `--accent`, `--ink`, `--ink-2`, `--muted`. |
| Comparison demo | `.compare`, `.compare-tabs`, `.compare-col`, `.compare-body`, `.compare-arrow`, `.copy-btn` | Uses `--card`, `--rule`, `--shadow-md`, `--bg`, `--bg-2`, `--ink`, `--ink-2`, `--accent`, `--accent-soft`, `--mono`. |
| Signal meter | `.nat`, `.nat-bar`, `.nat-track`, `.nat-before`, `.nat-after`, `.nat-delta`, `.nat-callout` | Uses `--bg-2`, `--rule`, `--warn`, `--accent`, `--accent-soft`, `--card`, `--shadow-md`, `--radius-lg`. |
| Shared sections | `section.block`, `.sec-head`, `.sec-num`, `.sec-head-lead` | Uses `--rule`, `--ink`, `--ink-2`, `--muted`, `--mono`. |
| Use cases | `.usecases`, `.uc`, `.uc-feature`, `.uc-icon`, `.uc-snippet`, `.uc-tag` | Uses `--card`, `--card-2`, `--bg`, `--rule`, `--radius`, `--accent`, `--accent-soft`, `--accent-tint`, `--shadow-md`. |
| Workflow cards | `.steps`, `.step`, `.step-node`, `.step-line`, `.step-figure`, `.chip` | Uses `--card`, `--card-2`, `--rule`, `--rule-2`, `--radius`, `--shadow-sm`, `--ink`, `--bg`, `--accent`, `--accent-soft`, `--mono`. |
| Trust and FAQ | `.trust`, `.trust-item`, `.faq-list`, `.faq-item`, `.faq-toggle` | Uses `--rule`, `--ink`, `--ink-2`, `--muted`, `--accent`, `--bg`, `--mono`. |
| Pricing | `.pricing-wrap`, `.plan-*`, `.pack-*`, `.pp-*`, `.pricing-*` | Uses `--card`, `--bg`, `--ink`, `--ink-2`, `--ink-3`, `--muted`, `--rule`, `--accent`, `--accent-2`, `--accent-soft`, `--accent-tint`, `--display`, `--mono`. |
| Developers pages | `.dev-*`, `.api-*`, `.method-badge`, `.code-block` | Uses `--card`, `--bg`, `--ink`, `--ink-2`, `--muted`, `--rule`, `--accent`, `--accent-soft`, `--accent-tint`, `--warn`, `--warn-soft`, `--mono`. |
| Final CTA and footer | `.final`, `.final-card`, `.site-footer`, `.footer-*` | Uses `--ink`, `--bg`, `--rule`, `--accent`, `--ink-2`, `--muted`, `--mono`. |

Landing CSS also contains local literals for specialized visual treatments:
comparison gradients, dark pricing-panel translucent fills, API syntax colors,
and several one-off shadows. Those are existing values and are not normalized in
this issue.

## App Shell CSS Module

Signed-in pages use `components/app/shell/shell.module.css`. The module is
mostly token-based and provides reusable layout primitives for app surfaces.

| Area | Main classes | Token mapping |
| --- | --- | --- |
| Shell frame | `.shell`, `.topbar`, `.topbarInner`, `.body`, `.sidebar`, `.main`, `.mainInner` | Uses `--bg`, `--card`, `--card-2`, `--ink`, `--rule`. |
| Brand and nav | `.brand`, `.brandMark`, `.docsLink`, `.hamburger`, `.navItem`, `.navItemActive`, `.navIcon` | Uses `--ink`, `--ink-2`, `--ink-3`, `--accent`, `--accent-deep`, `--accent-soft`, `--accent-tint`, `--card`, `--rule`. |
| Quota/account controls | `.quotaPill`, `.quotaTrack`, `.quotaFill`, `.quotaFillLow`, `.accountBtn`, `.avatar`, `.menu`, `.menuItem`, `.switch*` | Uses `--card`, `--rule`, `--rule-2`, `--bg-3`, `--accent-2`, `--warn`, `--accent-soft`, `--accent-deep`, `--shadow-sm`, `--shadow-md`. |
| Drawer | `.drawerBackdrop`, `.drawerPanel`, `.drawerHead`, `.drawerClose` | Uses `--card`, `--rule`, `--ink`, `--shadow-lg`; backdrop uses an existing rgba value. |
| Reusable primitives | `.sectionCard`, `.emptyState`, `.emptyIcon`, `.upsell`, `.badge`, `.iconBtn`, `.calloutRow` | Uses `--card`, `--rule`, `--rule-2`, `--radius`, `--radius-lg`, `--shadow-sm`, `--accent-*`, `--ink-*`, `--muted`. |
| Loading states | `.skeletonBlock`, `.skelLine`, `.loading*` | Uses `--bg-2`, `--bg-3`, `--card`, `--card-2`, `--rule`, `--accent-tint`, `--accent-deep`, `--shadow-sm`. |
| History and data cards | `.list*`, `.stat*`, `.hist*`, `.configCard`, `.codeBlock` | Uses `--card`, `--card-2`, `--rule`, `--radius`, `--shadow-sm`, `--ink-*`, `--muted`, `--mono`, `--accent-*`. |
| Warning states | `.errorBox`, `.historyPagerNote`, `.badgeWarn`, `.dangerCard` | Uses `--warn`, `--warn-2`, `--warn-soft`, `--ink`, `--card`, `--radius`, `--radius-lg`. |

The shell module does not define its own root variables; it consumes the global
tokens. It still uses a few local literals for white text, drawer backdrop,
loading code text, and gradient composition.

## Tailwind Utility Mapping

Dashboard, admin, and some app components use Tailwind utilities backed by
`tailwind.config.ts`. These are separate from the CSS custom properties and do
not currently read `var(--*)`.

### Tailwind Colors

| Tailwind key | Value | Closest CSS token | Status |
| --- | --- | --- | --- |
| `ink` | `#11150f` | `--ink` `#12160e` | Near match, not exact. |
| `paper` | `#f6f4ee` | `--bg` `#f7f5ef` | Near match, not exact. |
| `paper-deep` | `#e7e3d5` | `--bg-3` `#e7e3d5` | Exact match. |
| `mint` | `#d6e6dd` | `--accent-soft` `#d6e6dd` | Exact match. |
| `sky` | `#e8f1ec` | No root token | Used as a standalone tint. |
| `line` | `#d8d4c4` | `--rule` `#ddd8c8` | Near match, not exact. |
| `clay` | `#1e6b4a` | `--accent` `#1a6b48` | Near match, not exact. |
| `sage` | `#1e6b4a` | `--accent` `#1a6b48` | Same Tailwind value as `clay`, near root accent. |
| `rust` | `#c45a1a` | `--warn` `#c2571c` | Near match, not exact. |
| `gold` | `#9a7b2e` | No root token | Standalone Tailwind color. |

### Tailwind Typography

| Tailwind key | Value | Closest CSS token | Status |
| --- | --- | --- | --- |
| `font-serif` | `var(--font-instrument), Georgia, serif` | `--serif` | Near match; root adds `"Times New Roman"` fallback. |
| `font-mono` | `var(--font-geist-mono), ui-monospace, SFMono-Regular, Menlo, monospace` | `--mono` | Near match; root adds `"JetBrains Mono"` before system monospace. |

There is no custom Tailwind `font-sans`; default utilities use Tailwind's base
stack while global CSS sets `body` to `--sans`.

### Tailwind Shadows

| Tailwind key | Value | Closest CSS token | Status |
| --- | --- | --- | --- |
| `shadow-soft` | `0 18px 45px rgba(17, 21, 15, 0.10)` | `--shadow-md` | Separate landing/admin shadow value. |
| `shadow-crisp` | `0 12px 28px rgba(17, 21, 15, 0.08)` | `--shadow-sm` or `--shadow-md` | Separate compact shadow value. |

### Tailwind Usage Areas

| Area | Utility examples | Token relationship |
| --- | --- | --- |
| Admin screens | `bg-paper`, `bg-paper-deep/40`, `text-ink`, `border-line`, `text-clay`, `text-rust`, `shadow-soft`, `shadow-crisp` | Uses Tailwind theme aliases, some exact and some near root token matches. |
| App workspace components | `text-ink`, `text-sage`, `bg-paper`, `border-line`, `bg-white/70`, `rounded-lg`, `shadow-soft` | Mixes Tailwind aliases with default Tailwind spacing, radius, opacity, and white utilities. |
| UI primitives | `bg-white`, `border-line`, `focus:border-clay`, `focus:ring-clay/15`, `rounded-md` | Uses Tailwind aliases for color but Tailwind defaults for radius and spacing. |
| Developer charts | literal SVG fills plus Tailwind utility classes | Has local chart colors separate from both root variables and Tailwind aliases. |

## Follow-Ups

These are documentation findings only. No values were changed for this issue.

- `app/globals.css` references `var(--paper)` in `.pp-reassurance-item`, but
  `--paper` is not defined in the root token block.
- `tailwind.config.ts` duplicates the visual palette instead of pointing at
  CSS variables. Several aliases are near matches rather than exact matches:
  `ink`, `paper`, `line`, `clay`/`sage`, and `rust`.
- The root comment says `--accent` matches Tailwind `clay`, but the values are
  different: `--accent` is `#1a6b48`; Tailwind `clay` is `#1e6b4a`.
- There is no shared spacing token scale. Landing CSS uses direct pixel/clamp
  values while app/admin surfaces use Tailwind spacing utilities.
- Radius values are mixed: root has `14px` and `20px`, while landing, shell,
  UI primitives, and Tailwind utilities also use direct values such as `6px`,
  `8px`, `10px`, `12px`, `16px`, `18px`, `24px`, and `999px`.
- Shadows are duplicated across root CSS variables, Tailwind aliases, and
  several local landing shadows.
- `components/auth/auth-panels.module.css` has a scoped dark token override and
  provider-specific literal colors. That module is outside the W4-D mapping
  scope but should be considered in a future full design-system pass.
