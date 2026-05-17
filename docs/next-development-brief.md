# Next Development Brief

Last updated: 2026-05-18

## Current Decision

This document records the next round of product and UX changes and is the implementation brief for the current development run.

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

The workspace should remain one page with grouped sections. Do not turn the main rewrite workspace into a step-by-step wizard.

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

Keep the workspace as a single-page tool surface. Group related controls clearly, but do not force users through a multi-step wizard.

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
- Collapse `Extra context` by default.
- Let users expand it only when they need to explain more.

### 7. Tone Presets

Current API only accepts:

- `warm`
- `direct`

Next round decision:

- Expand validation/API schema to accept a new `tonePreset` field.
- Update `lib/validation.ts`, `lib/openai.ts`, `lib/rewrite.ts`, and the API request payload so the selected preset is passed to the server.
- Keep the existing `tone` field as compatibility if useful, but do not rely only on client-side mapping.
- The rewrite prompt should receive enough information to reflect the exact visible preset the user selected.

Suggested visible tone presets:

- Warm
- Direct
- Professional
- Friendly
- Firm but polite
- Apologetic
- Concise

Compatibility mapping:

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
- Prompt receives the selected tone preset or equivalent explicit instruction.

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
- Add `tonePreset` to the form submission payload and `/api/rewrite` validation.
- Keep `tonePresetToTone` only as compatibility/fallback logic for existing warm/direct code paths.
- Update `lib/openai.ts` so the visible tone preset influences the rewrite prompt.

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

## Homepage Sample Case Changes

Update `components/landing/interactive-demo.tsx`.

### Current Problem

The current demo examples were temporary placeholders. They are too short to be persuasive and contain detail consistency problems:

- Teacher draft starts with `Dear Student`, but the rewrite suddenly says `Hi Maya`.
- Sales rewrite suddenly says `Hi Jordan` even when the rough draft did not name Jordan.
- Placeholder examples feel generic and not connected to the actual product testing work.
- Short examples make the product look less convincing because the rewrite does not have enough context to show meaningful improvement.

### Product Goal

Replace the homepage demo samples with stronger examples that come from actual internal testing/evaluation, not arbitrary copy.

The samples should demonstrate:

- The rough draft sounds stiff or too generic.
- The rewrite sounds more natural and send-ready.
- Names, dates, prices, numbers, policies, and next steps are preserved.
- The rewrite does not introduce a random name or fact.
- Naturalness Check percentages reflect actual measured or recorded test results whenever available.

### Sample Selection Process

Before changing homepage copy:

1. Create or reuse 4 realistic test samples:
   - Teacher message
   - Sales follow-up
   - Workplace email
   - Client reply
2. Run the rewrite flow or evaluation script against them.
3. Check that the rewrite does not invent names or facts.
4. Choose the best example for each category.
5. Record the selected before/after text and measured signal values in `docs/optimization-notes.md` or a new `docs/sample-cases.md`.
6. Use those selected examples in `components/landing/interactive-demo.tsx`.

Do not call Sapling on every homepage render or every local page load. Homepage Naturalness Check values should be selected from a documented run, stored in `docs/sample-cases.md`, and reused as static sample values until a future sample refresh.

### Case Requirements

Each case should include:

- Incoming message or scenario context.
- Rough draft.
- Rewritten reply.
- Draft AI-like signal percent.
- Rewrite AI-like signal percent.
- Change points.
- Notes on preserved facts.

### Detail Consistency Rules

Strict rules:

- Do not introduce a name unless the incoming message or rough draft includes that name.
- If the rough draft says `Dear Student`, the rewrite should not say `Hi Maya`.
- If a name is needed in the rewrite, include it in the original sample first.
- Do not invent dates, times, numbers, prices, seat counts, policy details, or commitments.
- If the rewrite mentions a next step, that next step must come from the sample input.
- Do not use placeholders like `Maya`, `Jordan`, or random dates unless they are intentionally part of the sample.

### Recommended Sample Length

Current samples are too short. Make each rough draft and rewrite long enough to feel realistic but still readable on the homepage.

Recommended target:

- Rough draft: 55-90 words.
- Rewritten reply: 55-95 words.
- Incoming context, if displayed later: 30-70 words.

If the homepage card becomes too tall:

- Keep the card desktop layout wide.
- Use consistent min-height for rough/rewrite panels.
- Consider showing only rough/rewrite on the card and keeping context hidden or summarized.
- On mobile, stack panels vertically and allow more height.
- Avoid tiny text just to force the sample into the current layout.

### Suggested Four Case Themes

These are themes only. The final wording should come from actual test samples and selected outputs.

1. **Teacher message**
   - Student asks about a late reflection or participation grade.
   - Preserve course policy and next step.
   - Avoid inventing a student name unless included in the input.
2. **Sales follow-up**
   - Prospect is comparing options or reviewing a proposal.
   - Preserve proposal timing and next action.
   - Avoid making pressure or fake promises.
3. **Workplace email**
   - Teammate asks for numbers, review, or a document.
   - Preserve exact timing or document status.
   - Rewrite should sound like a real internal note.
4. **Client reply**
   - Client reports a discrepancy or asks what changed.
   - Preserve known cause and planned update.
   - Rewrite should acknowledge the concern without overpromising.

### Homepage UI Requirements For Longer Samples

The current `InteractiveDemo` card may need layout changes after sample length increases.

Consider:

- Add a small scenario/context row above rough/rewrite panels.
- Use `md:grid-cols-[1fr_auto_1fr]` on desktop as today, but increase panel min-height.
- On smaller screens, stack rough and rewrite cards.
- Keep the Naturalness Check below the text panels.
- If text feels dense, use 15px body text and line-height around 1.65.
- Do not truncate the sample copy unless there is an explicit expand control.

Acceptance:

- Each tab uses internally tested sample text.
- No random names or unsupported facts appear in rewrites.
- Each rough/rewrite pair is substantial enough to be persuasive.
- Desktop and mobile layout remain readable after longer sample copy.
- Naturalness Check values are either measured or clearly recorded as internal sample values.

## Sapling API Subscription And Longer Test Samples

The user has subscribed to the Sapling API plan. Next development can use longer, more realistic samples than the earlier free-quota testing allowed, but usage still needs to be controlled and recorded.

### Goal

Run the next round of Naturalness/sample testing with text lengths closer to real email workflows, not tiny placeholder snippets.

### Realistic Email Length Targets

Use several sample sizes:

- Short email reply context: 150-300 words total input.
- Normal workplace/customer email context: 300-600 words total input.
- Longer thread or detailed client reply: 600-1,000 words total input.

For homepage examples, keep displayed copy shorter than the full evaluation sample when necessary:

- Evaluation sample can be longer.
- Homepage demo rough/rewrite should be edited to a persuasive but readable excerpt.
- Do not change facts when shortening for display.

### Sapling Usage Estimate

Sapling AI Detector usage is character-based. Approximate English conversion:

- 1 word is about 5 characters.
- 300 words is about 1,500 characters.
- 600 words is about 3,000 characters.
- 1,000 words is about 5,000 characters.

Each production rewrite may call Sapling for:

- draft before signal
- rewrite after signal
- optional second rewrite after signal if strategy retry is used

So one full test sample can use roughly 2x to 3x the sample character length.

### Next Testing Rules

When using longer samples:

- Record the approximate word count and character count for every test sample.
- Record how many Sapling calls each sample caused.
- Do not run unbounded evaluation loops.
- Prefer 4 strong sample cases first, one per homepage tab.
- Then expand to 8-12 evaluation samples only if needed.
- Cache or save selected sample results in docs so homepage values do not require repeated Sapling calls.

### Suggested Output File

Create a new file during implementation:

- `docs/sample-cases.md`

It should record for each selected homepage case:

- category
- incoming context
- rough draft
- rewritten reply
- word count
- estimated character count
- displayed excerpt word count
- displayed excerpt estimated character count
- Sapling call count used for the selected result
- estimated Sapling characters consumed
- Sapling draft score
- Sapling rewrite score
- score change
- preserved facts checklist
- whether the case is used on homepage

Also add a usage/cost estimate section:

- total selected sample count
- total evaluation sample count
- total Sapling calls used
- total estimated characters sent to Sapling
- average characters per sample
- notes on whether any 429/rate/capacity errors occurred

### Acceptance

- Next sample testing uses realistic email lengths.
- Homepage samples are selected from documented test cases.
- Sapling usage is tracked enough to understand cost.
- No unavailable Sapling result is counted as target met.
- If Sapling returns 429 again, stop repeated eval calls, keep the best available documented results, and continue product work.

## FAQ Layout Changes

Update `components/landing/faq.tsx`.

### Current Problem

The FAQ currently uses a two-column card grid. It takes too much space and looks like another marketing card section instead of a familiar FAQ.

### Product Goal

Change FAQ to a common list-style FAQ pattern.

Preferred direction:

- Use a single centered column.
- Show each question as a row.
- Use light dividers instead of large cards.
- Use an accordion interaction if practical.
- Keep answers concise and readable.

### Recommended Layout

Desktop:

- Section max width around `max-w-3xl` or `max-w-4xl`.
- `FAQ` heading aligned with the list.
- Each item has:
  - Question row
  - Optional plus/minus or chevron icon
  - Answer shown below when expanded, or always visible if choosing non-interactive list.
- Use borders/dividers between rows.
- Avoid large padded rectangular cards for every item.

Mobile:

- Same single-column layout.
- Rows should have enough tap target height.
- Answers should not overflow or make text cramped.

### Interaction

Recommended:

- Accordion with one or multiple questions open.
- Default: first item open, or all closed. Choose whichever feels cleaner after visual check.

If accordion adds too much code:

- Use a static list with questions bold and answers below, separated by dividers.

### Styling Requirements

- Should feel quieter and more standard than the current card grid.
- Avoid nested cards.
- Avoid large empty boxes.
- Keep the page rhythm lighter after Pricing.
- Use existing color tokens: `paper`, `paper-deep`, `line`, `ink`, `clay`, `sage`.
- Do not introduce a new visual style only for FAQ.

Acceptance:

- FAQ no longer appears as a two-column card grid.
- It reads as a standard FAQ list.
- Mobile and desktop screenshots are clean.
- Existing FAQ content can remain unless later copy changes are requested.

## Confirmed Decisions From Latest Discussion

These decisions are no longer open:

- Keep the rewrite workspace as one page with grouped sections.
- Collapse `Extra context` by default.
- Show custom audience/purpose inputs when `Other` is selected.
- Pass `tonePreset` or equivalent visible preset data to the API, not only client-side mapping.
- Homepage sample Naturalness Check values should come from documented selected runs, not repeated live calls.
- Add a usage/cost estimate section to `docs/sample-cases.md` with sample character counts and Sapling call counts.

## Commercial Site Baseline

Use these defaults in the next development round:

- Pricing remains `NZD $9/month` for now.
- Do not implement annual checkout in this round.
- Do not advertise an annual plan unless the Stripe annual price exists.
- Footer should include: `Operated by TimeAwake Ltd.`
- Support/contact email: `info@timeawake.co.nz`.
- Add or expose simple footer links for Privacy and Terms if the pages already exist or can be created safely.
- Privacy/Terms can be concise MVP pages. They should explain that pasted messages and rewritten replies are processed for the request but are not saved to the database.

Sapling feature boundary:

- Do not copy Sapling's Pro feature table into Reply In My Voice.
- Do not market Sapling-specific features such as autocomplete, snippets, domain administration, or chat assist.
- Use Sapling only as a third-party reference writing signal for the Naturalness Check.
- Keep user-facing terminology as `Naturalness Check`, `writing signal`, and `AI-like signal`.

## Open Items To Add Before Coding

The user mentioned there are more things to change. Before starting implementation, append any additional feedback under this section and then turn the full brief into an implementation plan.
