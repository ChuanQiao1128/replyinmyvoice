# ReplyInMyVoice.com MVP — Autonomous Build Requirements

## Mission
Build and ship the production-ready MVP for ReplyInMyVoice.com on Cloudflare.

Final MVP goal:

- A polished English-language web app at `https://replyinmyvoice.com`.
- Landing page with a warm writing-desk feel and a preset interactive demo.
- Authenticated rewrite workspace with two primary inputs: `Message to reply to` and `Rough draft reply`.
- OpenAI-powered rewrite output that preserves facts and produces a warmer or more direct reply while materially lowering the third-party AI-like signal whenever possible.
- Sapling-powered `Naturalness Check` showing before/after AI-like signal percentages as a reference signal.
- Clerk authentication.
- Neon/Postgres subscription and usage tracking.
- Stripe sandbox-ready checkout, billing portal, and webhook subscription updates.
- Free signed-in users get 3 lifetime rewrite attempts.
- Paid users get 100 rewrite attempts per billing month for `NZD $9/month` in the current sandbox/MVP configuration.
- Cloudflare-compatible deployment using Next.js App Router with the appropriate Cloudflare/OpenNext runtime, not a static export.

ReplyInMyVoice is a web app that helps users turn rough, AI-assisted, or too-generic drafts into replies that sound like they personally wrote them. The initial focus is everyday communication: teacher messages, sales follow-ups, workplace email, student/customer/client replies, and other high-context responses.

The current domain `replyinmyvoice.com` already has a simple deployed welcome/holding page. Preserve the brand direction and expand it into the complete MVP. Do not wipe the app and start over unless the existing codebase is unusable; inspect first, then extend.

## Background story and product context

This product exists because AI-written replies are becoming common in real communication, but the raw output often feels stiff, generic, or obviously not written by the sender.

Primary real-world story:

- A teacher needs to reply to students by email.
- The teacher may already use AI to help draft the response.
- The AI draft can sound too polished, too generic, or unlike how that teacher normally speaks.
- Students may notice the reply feels artificial or impersonal, which can create frustration or reduce trust.
- The teacher's need is not to hide misconduct or avoid academic review. The need is to send a clearer, warmer, more personal reply that still preserves the facts and intent.

Second real-world story:

- A salesperson needs to reply to customers, leads, or clients.
- They may use AI to summarize context or draft a response.
- The raw draft can sound mechanical, corporate, or detached.
- The salesperson needs the reply to feel more natural, friendly, and relationship-aware without inventing promises, discounts, timelines, meetings, or outcomes.

Broader pattern:

- Users are not asking the product to write essays, bypass review systems, or optimize against detection tools.
- Users are asking for practical reply help in normal communication where the message should still feel like it came from them.
- The product should emphasize context, personal tone, fact preservation, and appropriate warmth/directness.
- The product should help users review and improve AI-assisted drafts before sending emails or messages to real people.

Product implication:

- The core workflow is "draft + context -> reply in my voice."
- The product should ask for enough context to avoid making things up.
- The output should be useful for email/message replies, not long-form academic writing.
- The app should feel safe, professional, and communication-focused.
- The app should avoid any framing that suggests detector evasion, student cheating, hidden AI use, or guaranteed human-likeness.

## Confirmed MVP decisions

Main app workflow:

- Use two primary textareas:
  - Message to reply to
  - Rough draft reply
- Keep context fields:
  - Audience
  - Purpose
  - What actually happened
  - Facts to preserve
- Tone selector remains exactly:
  - Warm
  - Direct

Usage limits:

- Signed-in free users get 3 lifetime rewrite attempts.
- Paid users get 100 rewrite attempts per billing month.
- Price is `NZD $9/month` for MVP/sandbox copy because the current Stripe price is `unit_amount=900`, `currency=nzd`, `interval=month`.
- One user click on `Rewrite` counts as one usage attempt, even if the server internally tries more than one rewrite strategy to improve quality.
- The server may run bounded internal optimization attempts for the same user request when the AI-like signal does not improve enough.
- If the user manually clicks `Try again` after seeing the result, that is a new user request and consumes another usage attempt.
- Validation errors, auth failures, payment failures, and provider/server errors should not count.
- When free users exhaust their 3 lifetime attempts, show a hard paywall.
- When paid users exhaust their monthly quota, show a hard quota/paywall state.

Sapling / Naturalness Check:

- Use Sapling as the third-party writing signal provider.
- Run the signal for free users and paid users.
- Show before/after signal to users.
- User-facing label: `Naturalness Check`.
- Explainer copy:

```text
A third-party reference signal that helps compare how natural the draft and rewrite feel. It is not a guarantee; review the reply before sending.
```

- The signal is a reference only, not a guarantee.
- The signal must not be the sole success metric.
- If Sapling is unavailable or fails, the rewrite should still return and the UI should show a neutral unavailable state.
- Lowering the AI-like signal is a core product goal, similar to how a compression tool should noticeably reduce file size.
- During development, if the AI-like signal reduction is not satisfactory, keep iterating on prompts, rewrite strategies, scoring, and sample tests until the before/after reduction is meaningfully improved.

Naturalness optimization:

- Build the rewrite API as a bounded optimization loop, not a single naive prompt.
- For one user request, first measure the draft signal, generate a candidate, measure the candidate, then decide whether another strategy should be tried.
- The server may try multiple internal strategies under one user-visible request, subject to a strict cap and timeout.
- Internal strategies may include:
  - preserving concrete facts while reducing generic AI phrasing
  - shortening overly uniform sentences
  - removing template-like openings and endings
  - adding context-specific phrasing from the user's fields
  - making the reply less polished and more send-ready
  - using separate strategy prompts for `warm` and `direct`
  - running a refinement pass when the first candidate remains too AI-like
- Select the final result using a composite score:
  - lower Sapling AI-like signal than the draft
  - meaningful signal drop when possible
  - facts preserved
  - tone matches `Warm` or `Direct`
  - concise, natural, send-ready reply
  - no invented promises, names, timelines, policies, discounts, or outcomes
- Do not create an unbounded loop chasing a third-party score.
- Do not promise a specific percentage reduction to users.
- Production MVP runtime cap: up to 2 internal rewrite strategies per user request, charged as 1 user usage attempt.
- Development and evaluation scripts may try more than 2 strategies per sample while searching for stronger prompts and scoring approaches.

Development optimization requirement:

- Create a local evaluation set with representative teacher, sales, workplace, and client/customer reply samples.
- Use 8-12 representative samples for development evaluation.
- Try at most 3 prompt/strategy variants during evaluation.
- Honor `EVAL_MAX_PROMPT_ITERATIONS=5` and `EVAL_MAX_WALLCLOCK_MINUTES=60` as hard upper bounds if present.
- For each sample, record draft signal, rewrite signal, signal delta, fact preservation notes, tone quality, and failure notes.
- Use this evaluation set during development to compare strategies.
- Internal development target, not a user-facing promise:
  - average signal reduction of at least 30 points across the representative sample set
  - most rewritten samples should be below 50% AI-like signal
- If the target is not met within the evaluation budget, keep the best measured strategy, document measured results in `docs/optimization-notes.md`, and continue building the product.
- Treat this as core product R&D, not optional polishing.

Product focus:

- The MVP is a general reply assistant.
- Landing page and examples should explicitly include teacher and sales scenarios.
- Also include workplace email and customer/client replies.
- All user-facing website and app UI copy should be in English.

UI/UX decisions:

- Visual direction: `Warm Writing Desk`.
- The interface should feel warm, calm, professional, and writing-focused.
- Prefer off-white paper-like backgrounds, dark readable text, soft borders, subtle note/paper metaphors, and restrained warm accents.
- Avoid a heavy futuristic AI-tool look, large purple/blue gradients, and aggressive marketing visuals.
- Landing page should include a preset interactive demo that does not call OpenAI or Sapling and does not consume usage.
- Demo scenarios should include teacher message, sales follow-up, workplace email, and client/customer reply.
- `/app` workspace should use a two-column desktop layout:
  - Left: inputs and rewrite controls.
  - Right: rewritten output, Naturalness Check, summary, risk notes, copy, try again, and history.
- Mobile layout should stack the same content vertically.
- Local history must include a `Clear history` button.
- Add a privacy reminder near the input area:

```text
Avoid pasting passwords, payment details, or highly sensitive personal information.
```

Naturalness Check display:

- Show Sapling before/after as an AI-like signal percentage.
- Lower is better.
- Example shape:

```text
Naturalness Check
AI-like signal
Draft: 78%
Rewrite: 32%
Change: -46 pts
Reference signal only. Review the reply before sending.
```

- Use soft progress bars and labels such as:
  - High AI-like signal
  - Lower AI-like signal
  - Signal unavailable
- Do not display pass/fail.
- Do not imply that 0% means guaranteed human writing.
- Do not use the phrase `detector score` in user-facing UI.

Character limits:

- Message to reply to: max 5000 characters.
- Rough draft reply: 10 to 5000 characters.
- Audience: max 300 characters.
- Purpose: max 500 characters.
- What actually happened: max 1000 characters.
- Facts to preserve: max 1000 characters.
- Combined request cap: 10000 characters.
- Show remaining characters near long textareas.

Account/settings scope:

- Do not create a separate `/account` page for MVP.
- Show account-adjacent controls inside `/app`:
  - Subscription status
  - Usage remaining
  - Manage billing
  - Clear history

## Non-negotiable outcome
Keep working autonomously until the app is code-complete, locally buildable, type-safe, and ready for Cloudflare production deployment.

Do not stop to ask the user product questions. Make reasonable MVP decisions from this document. If an action requires an external dashboard or secret that the code agent cannot access, implement the code path fully, add a clear item to `docs/manual-setup.md`, and continue with all remaining work.

## Product positioning
Use this positioning everywhere:

> Replies that still sound like you.

Sub-positioning:

> A writing assistant for everyday communication: teacher messages, sales follow-ups, workplace email, and drafts that need your tone.

The product must feel like a practical writing workflow tool, not a detector-bypass tool.

## Strict copy and policy constraints
Never use these words or concepts in user-facing copy, metadata, routes, docs shown in the UI, or marketing sections:

- AI detection bypass
- detector bypass
- undetectable
- humanizer
- evade detection
- bypass filters
- trick detectors

Allowed wording:

- natural writing
- personal tone
- context-aware replies
- sounds like you
- preserve facts
- clearer replies
- warmer replies
- direct replies

Before finishing, grep the user-facing app code for:

```bash
grep -RniE "humanizer|bypass|undetect|detector|evade" app components public lib || true
```

Remove or rewrite any user-facing use. Internal comments may mention this grep requirement only if needed.

## Tech stack
Use this stack unless the existing repo already has a compatible equivalent that should be preserved:

- Next.js 15 App Router pinned to v15.x; do not install `next@latest` if it resolves to Next.js 16.
- TypeScript
- Tailwind CSS v3
- Clerk for auth
- Neon Postgres
- Prisma ORM with a Cloudflare Workers-compatible Neon adapter/driver strategy
- Stripe Checkout + Stripe Billing Portal
- OpenAI API using `gpt-4o-mini` by default
- Cloudflare Workers/OpenNext deployment compatibility using `@opennextjs/cloudflare`, not `@cloudflare/next-on-pages`

## Required environment variables
All secrets must be read from `process.env`. Never hardcode keys.

Create `.env.example` with these variables:

```env
# App
NEXT_PUBLIC_APP_URL=https://replyinmyvoice.com
NODE_ENV=development

# Clerk
NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY=
CLERK_SECRET_KEY=
NEXT_PUBLIC_CLERK_SIGN_IN_URL=/sign-in
NEXT_PUBLIC_CLERK_SIGN_UP_URL=/sign-up
NEXT_PUBLIC_CLERK_AFTER_SIGN_IN_URL=/app
NEXT_PUBLIC_CLERK_AFTER_SIGN_UP_URL=/app

# Database
DATABASE_URL=
DIRECT_URL=

# OpenAI
OPENAI_API_KEY=
OPENAI_MODEL=gpt-4o-mini
OPENAI_TIMEOUT_SEC=25

# Stripe
STRIPE_SECRET_KEY=
NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY=
STRIPE_PRICE_ID=
STRIPE_WEBHOOK_SECRET=

# Optional local/dev convenience only
ALLOW_DEV_SUBSCRIPTION_BYPASS=false

# Launch/cutover guardrail
LAUNCH_CONFIRMED=false

# Third-party writing signal
WRITING_SIGNAL_PROVIDER=sapling
SAPLING_API_KEY=
WRITING_SIGNAL_TIMEOUT_SEC=10
EVAL_MAX_PROMPT_ITERATIONS=5
EVAL_MAX_WALLCLOCK_MINUTES=60
```

Important environment rules:

- The app must not fail at build time just because production secrets are absent. Validate required secrets at runtime inside the route or server action that uses them.
- Client-side code may only read `NEXT_PUBLIC_*` variables.
- In production, Stripe webhook signature verification must be required. In local development only, if `STRIPE_WEBHOOK_SECRET` is missing, allow a clearly logged fallback path for manual testing.
- `ALLOW_DEV_SUBSCRIPTION_BYPASS` may work only when `NODE_ENV !== "production"`.
- `LAUNCH_CONFIRMED=false` means do not modify `replyinmyvoice.com` DNS records or the existing Pages custom domain. Verify only the independent Worker deployment and document final cutover steps.
- Only if `LAUNCH_CONFIRMED=true` may the code agent perform the final production-domain cutover after Worker verification passes.
- Use `.env.local` for local Next/OpenNext development.
- Do not create `.dev.vars` with duplicated secrets unless absolutely necessary. If `.dev.vars` is created, it should normally contain only `NEXTJS_ENV=development`.
- Production secrets must be configured in Cloudflare as secrets/runtime variables.
- Do not print secret values while setting Cloudflare secrets. Names only.

## MVP routes and pages

### `/`
Create a polished landing page using the existing brand direction.

Required sections:

1. Hero
   - Headline: `Replies that still sound like you.`
   - Subheadline: `Turn rough drafts into clear, natural replies for students, customers, colleagues, and clients — without losing your voice.`
   - Primary CTA: `Start rewriting`
   - Secondary CTA: `See examples`

2. Use cases
   - Teacher messages
   - Sales follow-ups
   - Workplace email
   - Client/customer replies

3. How it works
   - Paste your rough draft
   - Add the context that matters
   - Get a reply that preserves the facts and sounds like you

4. Example before/after panel
   - Show a generic original draft
   - Show a warmer, more personal reply
   - Make clear that facts are preserved and the tone is improved

5. Pricing
   - Single plan: `NZD $9/month`
   - CTA: `Start with the NZD $9 plan`
   - Mention: `Cancel anytime.`

6. FAQ
   - What does this do?
   - Does it invent new facts? Answer: no; it is designed to preserve facts and use only the context provided.
   - Who is it for?
   - Can I cancel?

7. Footer
   - Product name
   - Simple links: Home, App, Pricing

### `/sign-in/[[...sign-in]]`
Use Clerk sign-in component.

### `/sign-up/[[...sign-up]]`
Use Clerk sign-up component.

### `/app`
Authenticated app page.

If the user is not signed in, redirect to Clerk sign-in.

If the user is signed in, does not have an active/trialing subscription, and still has free lifetime rewrites remaining, show the rewrite workspace with remaining free usage.

If the user is signed in, does not have an active/trialing subscription, and has exhausted the 3 lifetime successful free rewrites, show a hard paywall card with:

- Current status
- `NZD $9/month` plan
- Button: `Subscribe and continue`
- Button calls Stripe Checkout route

If the user has active or trialing subscription status, show the rewrite workspace with paid billing-period usage remaining.

Workspace requirements:

- Message to reply to textarea
- Rough draft reply textarea
- Context fields:
  - Audience
  - Purpose
  - What actually happened
  - Facts to preserve
- Tone selector with exactly two options:
  - Warm
  - Direct
- Rewrite button
- Before panel
- After panel
- Change summary panel
- Copy button for rewritten text
- Local history of last 5 rewrites using `localStorage`, not database

LocalStorage key:

```txt
rimv.rewrite.history.v1
```

### `/pricing`
Optional but preferred. May reuse pricing section from homepage. CTA starts checkout if signed in; otherwise routes to sign-up.

## API routes

### `POST /api/rewrite`
Authenticated route.

Requires either:

- signed-in inactive/free user with remaining free lifetime quota
- active or trialing paid subscription with remaining billing-period quota
- local development with `ALLOW_DEV_SUBSCRIPTION_BYPASS=true`

Request JSON:

```ts
type RewriteRequest = {
  messageToReplyTo?: string;
  roughDraftReply: string;
  audience?: string;
  purpose?: string;
  whatHappened?: string;
  factsToPreserve?: string;
  tone: "warm" | "direct";
};
```

Validation:

- `messageToReplyTo` optional, max 5000 characters
- `roughDraftReply` required, 10 to 5000 characters
- `audience` optional, max 300 characters
- `purpose` optional, max 500 characters
- `whatHappened` optional, max 1000 characters
- `factsToPreserve` optional, max 1000 characters
- combined request cap 10000 characters
- `tone` must be `warm` or `direct`

Response JSON:

```ts
type RewriteResponse = {
  rewrittenText: string;
  changeSummary: string[];
  riskNotes: string[];
  naturalness?: {
    draftAiLikePercent: number | null;
    rewriteAiLikePercent: number | null;
    changePoints: number | null;
    label: "lower" | "still_high" | "unavailable";
  };
  optimization?: {
    internalStrategiesTried: number;
    userUsageCharged: 1;
  };
};
```

Error responses:

- `401` unauthenticated
- `402` no active subscription
- `400` invalid input
- `500` provider or server error

OpenAI behavior:

- Use `OPENAI_MODEL`, defaulting to `gpt-4o-mini`.
- Ask the model to return strict JSON only.
- Parse and validate the JSON before returning it.
- If JSON parsing fails, return a safe server error rather than raw model text.
- Run Sapling before and after candidate generation when the provider is configured.
- If the first candidate does not lower the AI-like signal enough, try another internal rewrite strategy under the same user request, within the configured cap.
- Charge only one user usage attempt per successful `POST /api/rewrite` request, regardless of how many bounded internal strategies were tried.
- If Sapling fails, still return the best rewrite candidate and mark the signal unavailable.

Use this system prompt, or a semantically equivalent stricter version:

```txt
You are ReplyInMyVoice, a writing assistant for everyday replies.
Your job is to rewrite the user's draft so it sounds natural, personal, and context-aware while preserving the user's facts.

Rules:
- Do not invent facts, promises, timelines, apologies, policies, discounts, meetings, names, or outcomes.
- Use only information from the draft and context fields.
- Keep the user's intent.
- Preserve concrete facts and constraints.
- If important context is missing, keep the reply neutral rather than adding details.
- Avoid sounding overly polished, generic, corporate, or robotic.
- Do not discuss hiddenness, evasion, or whether the reply will pass automated reviews.
- Return strict JSON only with keys: rewrittenText, changeSummary, riskNotes.

Tone guidance:
- warm: friendly, clear, kind, human, concise.
- direct: concise, professional, clear, low-fluff.
```

### `POST /api/stripe/checkout`
Authenticated route.

Behavior:

- Get or create local user by Clerk user ID.
- Get or create Stripe customer.
- Create Stripe Checkout session in subscription mode.
- Use `STRIPE_PRICE_ID`.
- Add `client_reference_id` and metadata with Clerk user ID.
- Success URL: `${NEXT_PUBLIC_APP_URL}/app?checkout=success`
- Cancel URL: `${NEXT_PUBLIC_APP_URL}/app?checkout=cancelled`
- Return `{ url: string }`.

### `POST /api/stripe/portal`
Authenticated route.

Behavior:

- Require local user with Stripe customer ID.
- Create Stripe Billing Portal session.
- Return URL.

### `POST /api/stripe/webhook`
Public route, but must verify Stripe signature in production.

Handle events:

- `checkout.session.completed`
- `customer.subscription.created`
- `customer.subscription.updated`
- `customer.subscription.deleted`
- `invoice.paid`
- `invoice.payment_failed`

If the Stripe dashboard currently lacks the invoice events, do not stop coding. Implement handlers and document adding those events in `docs/manual-setup.md`.

Behavior:

- Update local user subscription fields.
- Treat `active` and `trialing` as allowed states.
- Treat `canceled`, `unpaid`, `incomplete_expired`, and deleted subscriptions as inactive.
- Store Stripe customer ID, subscription ID, price ID, status, and current period end when available.
- When reading Stripe subscription billing periods, prefer `subscription.items.data[0].current_period_end` and fall back to legacy `subscription.current_period_end` only if present.
- Webhook handling must be idempotent. Store processed Stripe event IDs and skip repeats.

Use raw request body for signature verification.
In Cloudflare Workers/OpenNext, use an async-compatible signature verification path if synchronous `constructEvent` fails.

## Database requirements
Use Prisma with Postgres, but do not assume a standard Node/TCP Prisma Client will work in Cloudflare Workers.

Implement a Cloudflare-compatible database strategy, preferably Prisma with an edge-compatible Neon adapter/driver such as `@prisma/adapter-neon` and `@neondatabase/serverless`. Prisma Accelerate or another documented Cloudflare-compatible Prisma approach is acceptable if implemented and verified.

`DATABASE_URL` is for runtime database access. `DIRECT_URL` is for migrations only.

Minimum Prisma models:

```prisma
model User {
  id                 String   @id @default(cuid())
  clerkUserId        String   @unique
  email              String?
  stripeCustomerId   String?  @unique
  stripeSubscriptionId String?
  stripePriceId      String?
  subscriptionStatus String   @default("inactive")
  currentPeriodEnd   DateTime?
  createdAt          DateTime @default(now())
  updatedAt          DateTime @updatedAt
}

model RewriteUsage {
  id          String   @id @default(cuid())
  userId      String
  periodKey   String
  periodStart DateTime?
  periodEnd   DateTime?
  count       Int      @default(0)
  createdAt   DateTime @default(now())
  updatedAt   DateTime @updatedAt

  user        User     @relation(fields: [userId], references: [id], onDelete: Cascade)

  @@unique([userId, periodKey])
}

model StripeEvent {
  id        String   @id
  type      String
  createdAt DateTime @default(now())
}
```

Usage period keys:

- `lifetime` for signed-in inactive/free users.
- `paid:<stripeSubscriptionId>:<billingPeriodEnd>` for active/trialing paid users.

Usage quota must be enforced server-side only. Check quota before provider calls. Charge usage only after a successful rewrite result is ready. Increment exactly once per successful user-visible rewrite request inside a transaction.

Quota concurrency:

- Do not implement quota as read-count-then-write-count without a guard.
- Use an atomic database transaction that creates the usage row if missing, then conditionally increments only when `count < quota`, or otherwise locks/serializes the usage row before provider calls.
- If two requests race when only one attempt remains, exactly one may succeed and the other must return `402`.
- Never decrement quota before provider calls unless the code also rolls back on provider failure.

Do not count validation errors, auth failures, payment failures, provider failures, Sapling failures, OpenAI failures, or server errors. Never let localStorage decide usage quota.

Do not persist rewrite history in the database for MVP. Use localStorage only.

Local history privacy:

- Store only rough draft, rewritten text, tone, change summary, risk notes, naturalness result, and timestamp.
- Do not store the full `messageToReplyTo` field in localStorage.
- If a before panel needs the incoming message, keep it in React state for the current session only.

Add Prisma helper utilities:

- `lib/db.ts`
- `lib/users.ts`
- `lib/subscription.ts`
- `lib/stripe.ts`
- `lib/openai.ts`

Ensure Prisma client is safe for Next.js development hot reload and verified in Cloudflare Worker preview/runtime after OpenNext build.

## Auth and subscription gating

Use Clerk middleware and server helpers.

Rules:

- Public: `/`, `/pricing`, Clerk auth pages, Stripe webhook.
- Protected: `/app`, `/api/rewrite`, `/api/stripe/checkout`, `/api/stripe/portal`.
- `/app` may expose the rewrite workspace to signed-in inactive/free users only while they still have free lifetime rewrites remaining.
- `/app` must show a hard paywall to signed-in inactive/free users only after the 3 free lifetime successful rewrites are exhausted.
- API rewrite route must enforce auth, subscription/free-quota state, and usage quota on the server even if the UI hides the form.
- For Next.js 15, use `middleware.ts`. If a dependency forces Next.js 16, adapt to `proxy.ts` and document the version change.
- Use `.nvmrc` value `22`. When creating `package.json`, set `engines.node` to `>=22 <23`.
- Do not put database queries, subscription checks, Stripe calls, OpenAI calls, Sapling calls, or heavy logic in middleware/proxy. Middleware is only for lightweight auth route protection.
- Mark auth/subscription-dependent pages and routes as dynamic where needed: `/app`, `/api/rewrite`, `/api/stripe/checkout`, `/api/stripe/portal`, `/api/stripe/webhook`.
- Do not read secret env vars at module import time in a way that breaks build. Validate required secrets inside the handler that uses them.
- Do not add `export const runtime = "edge"` to Prisma/Stripe/OpenAI routes unless the implementation is verified in Cloudflare Worker preview.
- Protected POST routes must enforce same-origin requests for `/api/rewrite`, `/api/stripe/checkout`, and `/api/stripe/portal`.
- Check `Origin` when present. Allow only `NEXT_PUBLIC_APP_URL` and localhost development origins.
- If `Origin` is missing in development, allow with a warning. In production, reject suspicious cross-origin POST requests with `403`.
- Do not apply same-origin checks to `/api/stripe/webhook`; Stripe webhook uses signature verification.

Subscription helper should return:

```ts
type SubscriptionState = {
  isActive: boolean;
  status: string;
  currentPeriodEnd?: Date | null;
};
```

Allowed active statuses:

```ts
["active", "trialing"]
```

## UI requirements

Use Tailwind v3.

Visual direction:

- Clean SaaS landing page
- White/off-white background
- Dark readable text
- Subtle cards and borders
- Friendly but professional
- Mobile responsive

Components to create as needed:

- `components/site-header.tsx`
- `components/site-footer.tsx`
- `components/landing/hero.tsx`
- `components/landing/use-cases.tsx`
- `components/landing/how-it-works.tsx`
- `components/landing/example-panel.tsx`
- `components/landing/pricing.tsx`
- `components/app/rewrite-workspace.tsx`
- `components/app/paywall-card.tsx`
- `components/app/subscription-status.tsx`
- `components/ui/button.tsx`
- `components/ui/card.tsx`
- `components/ui/textarea.tsx`
- `components/ui/input.tsx`

Do not add a large component library unless already present. Keep dependencies minimal.

## User flows

### Visitor flow
1. User lands on `/`.
2. User clicks `Start rewriting`.
3. If signed out, route to `/sign-up`.
4. After sign-up, route to `/app`.
5. If signed in and free lifetime quota remains, show the rewrite workspace.
6. If signed in, not subscribed, and free lifetime quota is exhausted, show the hard paywall.
7. User clicks subscribe and goes to Stripe Checkout.

### Paid user flow
1. User signs in.
2. User opens `/app`.
3. App verifies active subscription or remaining free lifetime quota.
4. User submits draft and context.
5. API rewrites text.
6. UI shows before/after, summary, risk notes, and copy button.
7. Rewrite is saved to localStorage last 5.

### Checkout success flow
1. Stripe redirects to `/app?checkout=success`.
2. App shows a small success/processing banner.
3. If webhook has already updated the DB, show workspace.
4. If not yet updated, show refresh/retry state without breaking.

## Error handling

Add friendly UI states for:

- Not signed in
- Free quota remaining
- Free quota exhausted / not subscribed
- Paid quota exhausted
- Checkout creation failed
- OpenAI request failed
- Invalid input
- Rewrite takes too long
- Missing runtime env var

Do not expose secret values in logs or UI.

## Files to add/update

Expected scope paths:

```txt
app/**
components/**
lib/**
prisma/**
public/**
docs/**
.env.example
README.md
package.json
```

Create `docs/manual-setup.md` with clear instructions for dashboard-only tasks:

- Clerk allowed origins and redirect URLs
- Cloudflare production environment variables
- Stripe product/price setup
- Stripe webhook endpoint and events
- Stripe live mode checklist
- DNS checklist for `replyinmyvoice.com`

Create or update `README.md` with:

- Local setup
- Required env vars
- Prisma commands
- Running dev server
- Stripe webhook local testing note
- Build/typecheck commands

## Commands and scripts

Ensure these scripts exist and pass:

```json
{
  "scripts": {
    "dev": "next dev",
    "build": "next build",
    "cf:build": "opennextjs-cloudflare build",
    "cf:preview": "opennextjs-cloudflare build && opennextjs-cloudflare preview",
    "cf:deploy": "opennextjs-cloudflare build && opennextjs-cloudflare deploy -- --keep-vars",
    "typecheck": "tsc --noEmit",
    "lint": "next lint || eslint .",
    "prisma:generate": "prisma generate",
    "prisma:migrate": "prisma migrate dev",
    "prisma:deploy": "prisma migrate deploy"
  }
}
```

If `next lint` is unavailable for the installed Next.js version, use a working ESLint command instead. Do not leave broken scripts.

Passing `npm run build` is not sufficient. Also run Cloudflare/OpenNext build and at least one Worker preview smoke test for `/`, `/pricing`, `/sign-in`, `/app`, unauthenticated `/api/rewrite` rejection, `/api/stripe/webhook` method/body handling, and one DB smoke test in Worker preview.

Cloudflare deployment target:

- Worker name: `replyinmyvoice-app`
- Use `wrangler.jsonc`
- Use `nodejs_compat`
- `wrangler.jsonc` must set `main` to `.open-next/worker.js`.
- `wrangler.jsonc` must set `assets.directory` to `.open-next/assets` and `assets.binding` to `ASSETS`.
- `wrangler.jsonc` must use `compatibility_date` of `2024-09-23` or later.
- Deploy and verify first on the independent `workers.dev` URL.
- Keep the current `replyinmyvoice` Pages holding page and `replyinmyvoice.com` live domain intact unless `LAUNCH_CONFIRMED=true`.
- If `LAUNCH_CONFIRMED=false`, do not modify DNS records or Pages custom domain. Document the final cutover steps in `docs/manual-setup.md`.
- Do not use plain `wrangler dev` as the main Cloudflare preview command for this Next.js app. Use `opennextjs-cloudflare preview` after an OpenNext build.
- Deploy with `opennextjs-cloudflare deploy -- --keep-vars` so existing Cloudflare dashboard variables are not wiped.

Create `open-next.config.ts` using the installed `@opennextjs/cloudflare` configuration helper.

Migration safety:

- Allowed: `npx prisma generate`, `npx prisma migrate dev --name init`, `npx prisma migrate deploy`.
- Forbidden unless the user explicitly approves: `prisma migrate reset`, `prisma db push --force-reset`, dropping tables, or deleting existing production data.
- `DIRECT_URL` is for migrations only. `DATABASE_URL` is runtime.
- If migration drift or destructive reset is suggested, stop that operation, document it, and continue with code that can still be completed.

Provider timeout and cost guard:

- Use `AbortController`/timeouts for OpenAI and Sapling calls.
- Use `WRITING_SIGNAL_TIMEOUT_SEC` for Sapling.
- Use `OPENAI_TIMEOUT_SEC=25` for OpenAI if configured.
- One production user request may perform at most 1 draft writing-signal call, up to 2 OpenAI rewrite attempts, and up to 2 rewrite writing-signal calls.
- If Sapling times out, return the best rewrite with `naturalness.label = "unavailable"`.
- If OpenAI fails, do not charge usage.
- Do not run the development evaluation loop against unbounded samples.

Use `postinstall` if needed so Prisma client is generated during install/build:

```json
{
  "scripts": {
    "postinstall": "prisma generate"
  }
}
```

## Autonomous execution protocol

Follow this sequence without asking the user for clarification:

1. Inspect the existing project structure.
2. Identify the framework, package manager, and current routes.
3. Preserve any existing useful homepage/brand copy.
4. Install only necessary dependencies.
5. Implement Prisma schema and helpers.
6. Implement Clerk auth routes and middleware.
7. Implement Stripe checkout, portal, and webhook routes.
8. Implement subscription gating.
9. Implement OpenAI rewrite route.
10. Implement landing page.
11. Implement `/app` rewrite workspace.
12. Add localStorage history.
13. Add docs and `.env.example`.
14. Run Prisma generate.
15. Run typecheck.
16. Run build.
17. Run naturalness optimization evaluation against the local sample set.
18. If the target reduction is not satisfactory, iterate prompts/strategies/scoring within the evaluation budget and rerun evaluation.
19. Write `docs/optimization-notes.md` with measured results and the selected best strategy.
20. Run auth flow checks for registration/sign-in gates, logged-in `/app` access, and unauthenticated rewrite API rejection.
21. Run grep for banned wording.
22. Run Cloudflare/OpenNext build and Worker preview smoke checks.
23. If verification passes, deploy to `replyinmyvoice-app` workers.dev URL.
24. If `LAUNCH_CONFIRMED=false`, do not cut over `replyinmyvoice.com`; write cutover steps to `docs/manual-setup.md`.
25. Commit and push after each completed phase.
26. Fix all errors found.
27. Repeat checks until passing.
28. Produce a final summary with changed files, commands run, remaining dashboard tasks, and any known limitations.

If blocked by missing external credentials:

- Do not stop.
- Keep code buildable.
- Add placeholder env var names.
- Add the manual setup step to `docs/manual-setup.md`.
- Continue implementing the rest.

## Acceptance criteria

The task is complete only when all are true:

- `npm run typecheck` passes.
- `npm run build` passes.
- Prisma client generation works.
- Landing page exists at `/` and includes required sections.
- Clerk sign-in and sign-up routes exist.
- `/app` is protected by auth.
- `/app` allows signed-in inactive/free users to use remaining free lifetime quota.
- `/app` shows hard paywall only after free quota is exhausted for inactive/free users.
- `/app` gates paid billing-period quota for active/trialing subscribers.
- `POST /api/rewrite` validates input and returns valid JSON when a valid OpenAI key is configured.
- `POST /api/rewrite` refuses unauthenticated users.
- `POST /api/rewrite` returns 402 only when signed-in users have neither active/trialing quota nor remaining free lifetime quota.
- Stripe Checkout session creation route is implemented.
- Stripe Billing Portal route is implemented.
- Stripe webhook route is implemented with production signature verification.
- User subscription status is persisted in Postgres.
- No rewrite history is persisted in the database.
- Last 5 rewrites are stored in localStorage.
- `.env.example` is complete.
- `README.md` is updated.
- `docs/manual-setup.md` is created.
- `docs/optimization-notes.md` is created with measured naturalness evaluation results.
- Banned marketing terms are not present in user-facing app code.
- Worker deployment is verified on the `replyinmyvoice-app` workers.dev URL.
- `replyinmyvoice.com` DNS/custom-domain cutover is not performed unless `LAUNCH_CONFIRMED=true`.

## Manual setup that must remain outside code agent scope
The code agent should document these, not attempt to perform them:

1. Buy/manage domain.
2. Configure DNS records in registrar.
3. Add production environment variables in Cloudflare.
4. Create Stripe product/price if not already created.
5. Add Stripe webhook endpoint after production URL exists.
6. Copy `STRIPE_WEBHOOK_SECRET` into Cloudflare.
7. Configure Clerk production allowed origins and redirect URLs.
8. Switch Stripe from test mode to live mode only after full test pass.

## Final response expected from code agent
When finished, provide:

- Summary of what was built
- Files changed
- Commands run and results
- Environment variables still required
- Manual setup checklist
- Known limitations, if any
- Exact next command for the user to deploy or test

## Autonomous Preflight — Must Run Before Coding

Before writing application code, run a preflight check and create `docs/preflight-report.md`.

The report must include:

- Current working directory.
- Git status and current branch.
- Whether the GitHub remote is configured.
- Existing project structure and framework detected.
- Package manager detected.
- Next.js version detected.
- Node and npm versions detected.
- Whether the current holding-page code exists and should be preserved.
- Whether `.gitignore` protects `.env`, `.env.local`, `.dev.vars`, `globalapikey/`, `node_modules/`, `.next/`, `.open-next/`, `.wrangler/`, and `dist/`.
- Required environment variable names present or missing. Do not print secret values.
- Cloudflare deployment target and whether `CLOUDFLARE_API_TOKEN` is available.
- Clerk configuration availability.
- Neon connection availability.
- Stripe price and webhook availability.
- OpenAI model availability.
- Sapling aidetect availability and observed response shape.
- Any blockers and the chosen fallback.

Preflight rules:

- Work only inside `/Users/qc/Desktop/CloudFlare`.
- Do not create a second unrelated app folder.
- Inspect the existing project before scaffolding.
- Preserve the current holding-page brand direction unless the existing codebase is unusable.
- Never print, summarize, commit, or expose secret values from `.env.local`, `.dev.vars`, or `globalapikey/`.
- Do not use static Next export.
- Production deployment target is Next.js App Router on Cloudflare Workers using OpenNext.
- If `CLOUDFLARE_API_TOKEN` is missing, continue building the full app, skip destructive deployment/cutover, and document the missing deployment step in `docs/manual-setup.md`.
- If dashboard-only actions are required, document them and keep going.
- If a build fails because of Node 24 compatibility, first try dependency-compatible fixes; if still blocked, document a recommendation to use Node 22 LTS or Node 20 LTS.
- Use Clerk middleware appropriate to the detected Next.js version. For Next.js 15, use `middleware.ts`.
- Stripe webhook signature verification is mandatory in production.
- Local webhook fallback without signature is allowed only when `NODE_ENV !== "production"`.
- Sapling must be called server-side only.
- Convert Sapling `score` from 0..1 to 0..100 if that is the observed response shape.
- If Sapling fails, still return the rewrite and show `Signal unavailable`.
- Usage quota must be enforced server-side, never from localStorage.
- Count exactly one successful user rewrite request after a successful response is ready.
- Do not count validation errors, auth failures, payment failures, provider errors, or server errors.
- Continue autonomously after preflight unless a stop condition from `AGENTS.md` is met.

## Next Phase Product Goal: Launch Cutover And Quality Target

The MVP implementation phase has produced a verified Worker deployment. The next phase goal is to make the product a launch candidate on the formal domain.

Plan file:

- `/Users/qc/Desktop/CloudFlare/docs/launch-cutover-plan.md`

Updated goal:

Launch `replyinmyvoice.com` on the Cloudflare Worker, keep Stripe in sandbox mode, verify the real account flow, and continue Naturalness Check optimization until the internal quality target is met.

In scope:

- Re-run launch preflight.
- Keep working only in `/Users/qc/Desktop/CloudFlare`.
- Treat `LAUNCH_CONFIRMED=true` as authorization to cut over `replyinmyvoice.com`.
- Keep Stripe sandbox keys and sandbox price. Do not switch to live Stripe mode.
- Check Clerk origins and redirect requirements.
- Check Stripe sandbox webhook requirements and document dashboard-only gaps.
- Deploy and verify `replyinmyvoice-app`.
- Attach or route `replyinmyvoice.com` to the verified Worker.
- Preserve the existing Pages project for rollback.
- Test a real account flow:
  - register
  - sign in
  - rewrite with free quota
  - free quota exhaustion
  - paywall
  - sandbox checkout
  - webhook subscription update
  - paid quota
- Continue Naturalness Check optimization until:
  - average AI-like signal reduction is at least 30 points
  - most evaluated samples rewrite to below 50%
- Commit and push after each phase.

Out of scope:

- Switching Stripe to live keys.
- Creating a live Stripe price.
- Deleting the existing Cloudflare Pages project.
- Exposing secret values in logs, docs, commits, or final responses.

## Next Development Addendum: Reduced Form Friction And Tested Samples

This addendum records the next product/UI development scope. It supersedes earlier Warm/Direct-only and heavy context-field wording for the next iteration.

Primary planning file:

- `/Users/qc/Desktop/CloudFlare/docs/next-development-brief.md`

### Product Goal

The next development round should make the app feel easier and more commercial without changing the subscription, quota, or Cloudflare architecture.

The user should be able to get a useful rewrite with:

- message/thread to reply to
- rough draft reply
- one audience preset
- one tone preset

Everything else should be optional.

### Rewrite Workspace UX

Keep the rewrite workspace as one page with grouped sections. Do not turn it into a step-by-step wizard.

Main fields:

- message/thread to reply to
- rough draft reply

Secondary controls should be grouped into `Quick context`:

- audience preset
- purpose preset
- what must stay the same chips
- tone preset
- optional extra context

`Extra context` should replace the heavier `What actually happened` wording and should be collapsed by default. Users open it only when they need it.

`What must stay the same` should replace the heavier `Facts to preserve` wording.

Audience and purpose should include `Other`. Selecting `Other` should reveal a custom input. Normal presets should not require custom text.

### Tone Presets And API

The visible tone controls should include more than two choices, such as:

- Warm
- Direct
- Professional
- Friendly
- Firm but polite
- Apologetic
- Concise

The selected visible tone preset must be sent to `/api/rewrite` as `tonePreset` or an equivalent explicit request field. The server prompt should use this preset directly. Existing `tone = warm | direct` behavior may remain as compatibility/fallback, but client-only mapping is not enough for the next phase.

### Landing Page And How It Works

Update the landing page so the workflow feels easy:

1. Paste the thread.
2. Pick quick context.
3. Choose a tone preset.
4. Review the signal.

Step 2 should not imply that users must write a detailed explanation. It should communicate that most context is optional and can be selected from presets.

### Homepage Sample Cases

Replace placeholder homepage demo samples with documented internal test cases:

- Teacher message
- Sales follow-up
- Workplace email
- Client reply

Rules:

- Do not introduce random names.
- Do not invent dates, times, numbers, prices, policies, promises, or next steps.
- If a rewrite uses a name, that name must exist in the incoming context or rough draft.
- Use examples long enough to be persuasive but still readable.
- Naturalness Check values shown on the homepage should come from recorded selected runs in `docs/sample-cases.md`, not repeated live Sapling calls on each render.

### Sapling Usage And Cost Estimate

The user has subscribed to the Sapling API plan. The next round may test longer, more realistic samples:

- 150-300 words for short reply contexts
- 300-600 words for normal workplace/customer contexts
- 600-1,000 words for longer thread or detailed client reply contexts

Create or update `docs/sample-cases.md` with:

- category
- incoming context
- rough draft
- rewritten reply
- word count
- estimated character count
- displayed excerpt word count
- displayed excerpt estimated character count
- Sapling call count
- estimated Sapling characters consumed
- draft score
- rewrite score
- score change
- preserved facts checklist
- whether the case is used on the homepage

Add a usage/cost estimate section that records:

- total selected sample count
- total evaluation sample count
- total Sapling calls used
- total estimated characters sent to Sapling
- average characters per sample
- any 429/rate/capacity errors

Unavailable Sapling results must not count as quality-target success. If Sapling returns 429 again, stop repeated evaluation calls, keep the best documented results, and continue product work.

### FAQ Layout

Change the FAQ from a two-column card grid to a common single-column list or accordion:

- centered max-width column
- question rows with light dividers
- concise answers
- optional chevron/plus interaction
- no large repeated FAQ cards

### Commercial Site Defaults

Pricing stays `NZD $9/month` for this round. Do not implement or advertise annual checkout until a Stripe annual price exists.

The commercial site should include:

- `Operated by TimeAwake Ltd.` in the footer.
- Contact/support email: `info@timeawake.co.nz`.
- Privacy and Terms footer links/pages when practical.

Privacy/Terms pages can be concise MVP pages. They should state that pasted messages and rewritten replies are processed for the request and are not saved to the database.

Sapling is only a third-party reference writing signal provider for the Naturalness Check. Do not copy Sapling's Pro feature table or market Sapling-specific features such as autocomplete, snippets, domain administration, or chat assist as Reply In My Voice features.

### Acceptance For This Addendum

- Workspace remains one page and feels faster to use.
- Context fields use presets and optional custom inputs.
- `Extra context` is collapsed by default.
- Tone preset is passed to the API.
- Homepage samples are documented and fact-consistent.
- `docs/sample-cases.md` includes sample counts and Sapling usage/cost estimates.
- FAQ uses a list/accordion pattern instead of card grid.
- Commercial footer/contact uses TimeAwake Ltd. and `info@timeawake.co.nz`.

## Workspace Redesign V2 And Diagnosis-Driven Rewrite

This section supersedes the previous Quick context workspace flow.

### Workspace Requirements

- Delete Quick context from the app workspace.
- Keep a one-page flow.
- Provide five scenario choices:
  - `Blank / custom`
  - `Email or message reply`
  - `Customer support`
  - `Cover letter`
  - `Work update`
- `Context or message` is optional.
- `Draft to rewrite` is required.
- Tone choices are limited to:
  - `Warm`
  - `Professional`
  - `Friendly`
  - `Concise`
- Show results vertically:
  - rewritten text
  - Naturalness Check
  - change summary and risk notes
  - collapsed local history

### Backend Rewrite Strategy

Each scenario has backend prompt guardrails so the user does not need to fill out many fields.

The production rewrite engine should run:

1. diagnose draft patterns
2. create rewrite plan
3. targeted rewrite
4. measure writing signal
5. repair missing critical facts if needed
6. select the best candidate

Critical facts must be preserved when provided, including names, emails, currency, dates/months, counts, product/reporting details, and requested next steps.

### Evaluation Requirement

Create `docs/scenario-evaluation-results.md` with at least 15 cases:

- 3 cases for each of the 5 scenarios
- before and after text
- diagnosis tags
- rewrite plan
- draft and rewrite AI-like signal
- facts preserved/missing
- pass/fail

Quality target:

- average AI-like signal reduction at least 30 points
- majority of measured rewrites below 50%

Latest documented run:

- 15 cases evaluated
- average AI-like signal drop: 64 points
- 11/15 rewrites below 50%
- 11/15 case pass count
