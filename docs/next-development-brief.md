# Next Development Brief

Last updated: 2026-05-18

## Current Decision

Do not start coding this phase yet. This document records the next round of product and UX changes so the next development run has a clear target.

## User Feedback To Preserve

The current workflow asks users to provide too much detail. Users will paste their own email/thread and rough draft, which is fine, but the second step currently feels like homework:

- "Lock the facts" sounds heavy.
- Many users will not want to write full answers for audience, purpose, what happened, and facts to preserve.
- Some users may not know what to write in "What actually happened".
- "What must not change" is the clearer mental model.
- Audience/workplace/purpose should have preset options.
- Tone should have several preset choices, not just a manual decision.
- Review before/after AI-like signal is fine and should stay.

Main UX goal: make the product feel easy and guided, not like a long form.

## Product Goal For Next Round

Reduce perceived effort in the rewrite workspace by turning most context fields into quick choices:

1. User pastes the message/thread.
2. User pastes or writes the rough reply.
3. User optionally selects audience, purpose, must-keep facts, and tone from presets.
4. User can still add custom details when needed.
5. User runs rewrite and reviews before/after Naturalness Check.

The user should be able to get a useful rewrite with only:

- Message to reply to
- Rough draft reply
- One audience preset
- One tone preset

All other context should be optional.

## Landing Page Changes

Update `components/landing/how-it-works.tsx`.

Current problem:

- Step 2 says "Lock the facts" and asks users to explain what happened, who the audience is, and what must not change.
- This makes the product feel more complex than necessary.

Recommended new steps:

1. **Paste the thread**
   - Text: "Paste the message you are answering and your rough reply."
2. **Pick quick context**
   - Text: "Choose audience, purpose, and anything that must stay unchanged. Most fields are optional."
3. **Choose a tone preset**
   - Text: "Pick a warmer, clearer, firmer, or more professional style from a short list."
4. **Review the signal**
   - Text: "Compare the before and after AI-like signal, then copy the reply when it feels right."

Acceptance:

- Homepage clearly communicates that context can be selected, not typed from scratch.
- Step 2 must not imply the user must fully explain every detail.
- Step 3 must mention tone presets.

## Workspace UX Changes

Update `components/app/rewrite-workspace.tsx`.

### 1. Keep Primary Inputs Simple

Keep these as the main large fields:

- `Message to reply to`
- `Rough draft reply`

These are expected user actions and should remain prominent.

### 2. Replace Heavy Context Fields With A Quick Context Panel

Current fields:

- `Audience`
- `Purpose`
- `What actually happened`
- `Facts to preserve`

New UX:

- Create a "Quick context" section.
- Use select dropdowns and chips before showing optional textareas.
- Keep custom text inputs available, but make them secondary.

Recommended layout:

- Audience dropdown
- Purpose dropdown
- Must stay the same multi-select chips
- Extra context optional textarea, collapsed or visually secondary

Suggested section copy:

Title: `Quick context`

Helper text:

`Optional, but useful. Pick what fits; you can leave the rest blank.`

### 3. Audience Presets

Add a dropdown or segmented menu for audience.

Suggested options:

- Student
- Parent or guardian
- Prospect
- Customer or client
- Teammate
- Manager
- Vendor or partner
- General professional contact
- Other

Behavior:

- Selecting a preset should populate or derive the `audience` value.
- If `Other` is selected, show or keep a custom audience input.
- User can edit the custom audience text if needed.

### 4. Purpose Presets

Add a dropdown for purpose.

Suggested options:

- Reply clearly
- Follow up
- Explain a delay
- Apologize
- Say no politely
- Ask for more information
- Schedule or reschedule
- Clarify a misunderstanding
- Summarize next steps
- Other

Behavior:

- Selecting a preset should populate or derive the `purpose` value.
- If `Other` is selected, allow custom purpose text.

### 5. "What Must Stay The Same" Presets

Rename `Facts to preserve` to:

`What must stay the same`

Suggested chips:

- Names
- Dates and times
- Prices or numbers
- Policy details
- Next step
- Apology
- No new promises
- No extra details

Behavior:

- Chips should append concise phrases into the request sent to the API.
- Add an optional custom textarea for details.
- Placeholder: `Optional: add specific dates, numbers, policy details, or promises to avoid.`

### 6. Extra Context Should Be Optional

Rename `What actually happened` to:

`Extra context`

Suggested helper:

`Optional. Add anything the reply should understand but not over-explain.`

Add a quick option:

- `No extra context`

Behavior:

- If user selects `No extra context`, keep the extra context value blank.
- Do not force users to write this field.

### 7. Tone Presets

Current API only accepts:

- `warm`
- `direct`

Next round should either:

Option A, preferred for MVP:

- Keep API `tone` as `warm | direct`.
- Add UI-level `tonePreset` choices that map to warm/direct and inject a short style instruction into purpose or a new request field.

Option B:

- Expand validation/API schema to accept a new `tonePreset` field.
- Update `lib/validation.ts`, `lib/openai.ts`, and `lib/rewrite.ts` to pass it into the rewrite prompt.

Suggested visible tone presets:

- Warm
- Direct
- Professional
- Friendly
- Firm but polite
- Apologetic
- Concise

Recommended mapping for Option A:

- Warm -> `tone = warm`
- Friendly -> `tone = warm`
- Apologetic -> `tone = warm`
- Direct -> `tone = direct`
- Professional -> `tone = direct`
- Firm but polite -> `tone = direct`
- Concise -> `tone = direct`

Acceptance:

- User sees more than two tone choices.
- The API still receives valid data.
- Prompt receives enough information to reflect the selected style.

## Suggested Technical Structure

To avoid making `components/app/rewrite-workspace.tsx` too large, create a small shared preset file:

- Create: `lib/rewrite-presets.ts`

It should export:

- `audienceOptions`
- `purposeOptions`
- `mustKeepOptions`
- `tonePresetOptions`
- `tonePresetToTone`

Then:

- Modify `components/app/rewrite-workspace.tsx` to import presets.
- Keep form submission compatible with current `/api/rewrite`.
- If adding `tonePreset` to the server schema, update `lib/validation.ts` and `lib/openai.ts`.

## Testing Plan For Next Round

Add tests before implementation.

Recommended tests:

1. E2E landing copy test
   - File: `tests/e2e/commercial-site.spec.ts`
   - Assert How it works contains:
     - `Pick quick context`
     - `Choose a tone preset`

2. Unit test for preset mappings
   - File: `tests/unit/rewrite-presets.test.ts`
   - Assert all tone presets map to valid API tones.
   - Assert audience and purpose option lists include the expected product scenarios.

3. E2E or UI smoke test for workspace
   - If auth test coverage makes full workspace difficult, at minimum keep unit coverage for preset logic.
   - If authenticated test setup is available, verify:
     - Audience dropdown exists.
     - Purpose dropdown exists.
     - Must-stay chips exist.
     - Tone preset buttons exist.

## Non-Goals For This Round

Do not change:

- Stripe pricing.
- Subscription quota logic.
- Naturalness Check algorithm.
- Cloudflare deployment architecture.
- Database schema unless adding server-side `tonePreset` requires it, which should not be necessary for MVP.

Do not remove:

- Manual custom fields.
- Current scenario templates.
- Before/after signal display.

## Open Items To Add Before Coding

The user mentioned there are more things to change. Before starting implementation, append any additional feedback under this section and then turn the full brief into an implementation plan.

Potential questions to settle later:

- Should the workspace become a step-by-step wizard, or remain one page with grouped sections?
- Should `Extra context` be collapsed by default?
- Should the custom audience/purpose inputs show only after selecting `Other`?
- Should the tone preset be sent to the API as a new field, or mapped client-side for now?
