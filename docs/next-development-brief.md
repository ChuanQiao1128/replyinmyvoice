# Next Development Brief

Last updated: 2026-05-21

## Current Priority: Azure-Native Backend And Auth Migration

The next Azure/.NET backend development run should use this plan as the source of truth:

```text
docs/superpowers/plans/2026-05-21-azure-functions-sql-entra-migration.md
```

This supersedes the earlier Clerk/Neon runtime plan and the older Azure App Service/PostgreSQL migration idea.

Immediate decision:

```text
Stop spending engineering time on Clerk production login.
Do not make Clerk the long-term customer identity system.
Move customer authentication to Microsoft Entra External ID.
Move application persistence from Neon/Postgres runtime to Azure SQL.
Move heavy backend/API work to Azure Functions/.NET.
Keep Cloudflare as the public frontend edge and DNS layer.
```

Target architecture:

```text
Cloudflare-hosted frontend
+ Microsoft Entra External ID customer authentication
+ Google social sign-in through Entra External ID
+ Azure Functions / .NET API
+ Azure SQL
+ Azure Service Bus
+ Azure Functions worker or .NET worker for async rewrite jobs
+ Key Vault / Function app settings
+ Application Insights with controlled log volume
```

Architecture boundary:

```text
Cloudflare serves the web UI.
Azure owns identity verification, API, persistence, queue, and background processing.
App Service is not required for this target unless Azure Functions proves unsuitable.
```

Cost target:

```text
Keep the low-usage Azure backend close to the current low monthly Azure spend by avoiding Windows App Service B1 unless a later production load test proves Functions are insufficient.
```

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
- Remove the current `Quick context` panel from the workspace. The user should not need to choose audience, purpose, or must-preserve chips.
- Make `Context or message to respond to` optional. Users may paste only a draft and still run the rewrite.
- Replace narrow starter templates with broader scenario choices.
- Scenario must affect backend prompt guardrails and rewrite strategy. It must not be only a UI label.
- Keep `tonePreset` or equivalent visible preset data in the API payload, but reduce visible choices to a short list.
- Keep `Try again` for users who dislike the first rewrite.
- Put the rewritten output below the draft flow, not in a right-side column.
- Put Naturalness Check below the rewritten output so before/after comparison follows the result.
- Keep recent history, but collapse or de-emphasize it by default.
- Homepage sample Naturalness Check values should come from documented selected runs, not repeated live calls.
- Add a usage/cost estimate section to `docs/sample-cases.md` with sample character counts and Sapling call counts.
- Reduce production rewrite input limits to keep OpenAI and Sapling cost bounded for the active rewrite packs and Pro/API pricing.

## Workspace Redesign V2 — Supersedes Earlier Quick Context Plan

The previous next-round plan added audience/purpose dropdowns, must-keep chips, and an optional extra context panel. The latest product decision is to remove that complexity.

### Product Goal

The workspace should feel like:

1. Pick the broad writing scenario.
2. Optionally paste the original message/context.
3. Paste the draft that sounds too stiff or too AI-written.
4. Pick a simple tone.
5. Begin rewrite.
6. Review the rewritten version and Naturalness Check.

The user should be able to run the product with only:

- scenario
- rough draft
- tone

The original message/context is helpful but optional.

### Recommended One-Page Layout

Use a vertical layout:

1. `Scenario`
   - Broad scenario chips/cards.
   - Do not prefill heavy hidden context fields.
   - Scenario may optionally load a sample only when the user explicitly chooses a sample/demo action.
2. `Context or message to respond to`
   - Optional textarea.
   - Helper: `Optional. Paste the email, prompt, job post, thread, or background context if it helps.`
   - Leave blank for draft-only rewriting.
3. `Draft to rewrite`
   - Required textarea.
   - This is the main user input.
4. `Tone`
   - Short set of buttons.
   - Recommended visible tones:
     - `Warm`
     - `Professional`
     - `Friendly`
     - `Concise`
   - Default: `Professional` for business-like scenarios, `Warm` for general message replies if no scenario-specific default is set.
5. `Begin rewrite`
   - Primary action.
6. `Rewritten version`
   - Show output below inputs.
   - Include `Copy` and `Try again`.
   - `Try again` should use the same inputs, scenario, and tone, but ask for another version.
7. `Naturalness Check`
   - Show draft and rewrite AI-like signal after the output.
   - Keep the disclaimer that this is a third-party reference signal, not a guarantee.
8. `Recent versions`
   - Collapsed or visually secondary by default.

### Final Scenario Set For This Round

Use exactly these five scenarios unless the user changes the product direction again:

1. `Blank / custom`
2. `Email or message reply`
3. `Customer support`
4. `Cover letter`
5. `Work update`

Do not keep narrow homepage-style categories such as `Teacher reply`, `Sales follow-up`, `Workplace update`, and `Client reply` as the main workspace scenario set. Those can remain as homepage examples or internal sample cases, but not the main user choice.

### Scenario-Specific Backend Prompt Guardrails

Each scenario must send explicit hidden strategy/guardrail instructions to the backend rewrite prompt. These guardrails replace the removed Quick context controls.

#### 1. Blank / custom

Use when the user only wants a generic rewrite.

Guardrails:

- Preserve the original meaning and intent.
- Do not add names, facts, timelines, claims, examples, outcomes, or promises.
- Keep numbers, dates, prices, organization names, titles, and proper nouns unchanged.
- If the draft is vague, keep it vague rather than inventing detail.
- Prefer natural wording over polished marketing language.

Rewrite behavior:

- Make the text sound like a real person wrote it.
- Keep length roughly similar unless the input is repetitive.
- Avoid making the output overly formal or over-structured.

#### 2. Email or message reply

Use for general replies: teacher/student, sales follow-up, colleague, client, vendor, or everyday professional messages.

Guardrails:

- Preserve greeting/name if present.
- Do not invent relationship details, meetings, deadlines, discounts, attachments, or next steps.
- Preserve the user’s answer and the requested action.
- Keep the reply send-ready for an email, DM, or short professional message.
- If there is an incoming message, answer that message directly.
- If there is no incoming message, improve only the draft.

Rewrite behavior:

- Use natural short paragraphs.
- Avoid stock phrases like `Thank you for reaching out`, `I understand your concern`, and `Please be advised` unless genuinely useful.
- Prefer a human thread style over formal memo style.

#### 3. Customer support

Use for billing explanations, customer complaints, bug reports, account issues, service questions, or support follow-ups.

Guardrails:

- Preserve amounts, dates, plan names, seat counts, user counts, account status, deadlines, and known causes.
- Do not promise refunds, credits, fixes, escalation, timelines, account changes, or compensation unless the draft explicitly says so.
- Do not remove operational next steps such as asking for names, email addresses, screenshots, files, or confirmation.
- Do not over-compress long support explanations. A short result is not acceptable if it stops answering the customer’s specific questions.
- If the draft includes a forwardable internal explanation, keep it or preserve a close equivalent.
- Keep responsibility clear: do not imply the team will make changes if the draft says no changes will be made without user approval.

Rewrite behavior:

- Use 3-5 short paragraphs for longer billing/support replies.
- Explain plainly, without sounding robotic.
- Keep a calm support tone.
- Preserve a concrete next step.

This scenario is important because the Priya billing/support sample showed that a very low AI-like signal can be achieved by over-compressing the answer, but that is not a good product result. Quality and factual usefulness must come before score optimization.

#### 4. Cover letter

Use for job applications, cover letters, personal statements, short professional bios, and application-style introductions.

Guardrails:

- Do not invent employers, job titles, degrees, years of experience, metrics, awards, skills, projects, or personal background.
- Preserve the target role/company if provided.
- If the draft has no specific evidence, make the writing cleaner but do not add fake achievements.
- Avoid exaggerated enthusiasm or generic “perfect fit” language.
- Keep the voice confident but grounded.

Rewrite behavior:

- Make the letter sound personal, specific, and professional.
- Prefer clear human motivation over corporate application clichés.
- Keep paragraphing readable.

#### 5. Work update

Use for internal work messages, status updates, manager updates, Slack/Teams notes, project notes, blockers, and handoffs.

Guardrails:

- Preserve owners, dates, deadlines, blockers, deliverables, statuses, and next steps.
- Do not invent completed work, approvals, blockers, or timelines.
- Do not soften a real risk so much that the update becomes misleading.
- Keep the message scannable.

Rewrite behavior:

- Make the update clearer and less stiff.
- Prefer direct, practical language.
- Keep it concise unless the draft contains necessary detail.

### Why The Rewrite Strategy Works Better Than A Simple GPT Prompt

The product should not rely on a simple instruction like “make this sound more natural.” Normal GPT rewrites often become more polished, smooth, and corporate. That polish can increase the AI-like signal because the output has:

- uniform sentence rhythm
- generic transition phrases
- overly complete explanations
- formal support language
- stock openings and closings
- balanced but unnatural paragraph structure

Reply In My Voice should use a bounded strategy that combines prompt design, scenario guardrails, candidate selection, and third-party signal measurement. The next development round should strengthen this into a diagnosis-driven rewrite engine.

Current strategy from the previous development round:

1. Measure the draft AI-like signal.
2. Generate an OpenAI rewrite using plain, real-message instructions.
3. Measure the rewrite AI-like signal.
4. If the rewrite remains high or did not drop enough, try a second internal strategy.
5. Select the best candidate only if it passes content-quality checks.
6. Charge the user once after a successful rewrite response is ready.

The strategy lowers AI-like signal by:

- avoiding polished corporate templates
- using natural short paragraphs where appropriate
- removing stock phrases when they do not add value
- preserving facts instead of over-explaining around them
- varying sentence length and rhythm
- using scenario-specific fallback structures for hard cases such as billing/support
- measuring before/after signal instead of trusting the model’s self-assessment

### Diagnosis-Driven Rewrite Engine

The next version should not only generate two candidates and pick the lower AI-like signal. It should use the signal workflow to diagnose why the original draft feels AI-like, then apply targeted repairs.

The intended pipeline:

1. `Analyze draft`
   - Inspect the original draft for likely AI-like causes before rewriting.
   - This analysis can be model-assisted and rule-assisted.
   - It should be internal only; do not show a long diagnosis to users in the MVP.
2. `Create rewrite plan`
   - Decide which parts must be preserved.
   - Decide which AI-like patterns should be repaired.
   - Decide the scenario-specific risks.
3. `Generate targeted rewrite`
   - Rewrite based on the plan, not only a generic "make it natural" instruction.
4. `Measure signal`
   - Run the third-party writing signal on the draft and rewrite.
5. `Repair if needed`
   - If the rewrite remains high or did not improve enough, diagnose the failed rewrite and apply a second targeted repair.
6. `Select usable candidate`
   - Choose the lowest acceptable signal candidate that still passes quality gates.

### AI-Like Cause Taxonomy

During analysis, tag the likely causes that make the draft score high or feel synthetic. Suggested internal tags:

- `stock_opening`
  - Examples: `Thank you for reaching out`, `I understand your concern`, `I hope this message finds you well`.
- `corporate_polish`
  - Text is too smooth, formal, balanced, or customer-service-like.
- `uniform_rhythm`
  - Sentences have similar length and structure.
- `over_explained`
  - The reply explains every step in a complete but unnatural way.
- `generic_transitions`
  - Examples: `Additionally`, `Furthermore`, `In conclusion`, `Please note that`.
- `policy_memo_voice`
  - Reply sounds like a policy document instead of a person responding.
- `low_specificity`
  - Lots of generic safe wording, not enough concrete details from the user.
- `too_balanced_structure`
  - Every paragraph has the same neat shape: acknowledge, explain, summarize, next step.
- `over_safe_tone`
  - Too neutral, risk-averse, or emotionally flattened.
- `support_template_voice`
  - Sounds like generic support macros rather than a real support reply.
- `application_cliche`
  - Cover-letter style generic claims such as "I am uniquely qualified" without evidence.

The taxonomy does not need to be perfect. It exists so the second pass can target the actual failure pattern instead of blindly asking for another rewrite.

### Targeted Repair Strategies

The rewrite engine should map diagnosis tags to concrete repairs:

- `stock_opening`
  - Replace with a specific, situational opener.
  - Example: `Thanks for laying this out` for a detailed customer issue.
- `corporate_polish`
  - Reduce formal density.
  - Replace abstract phrases with plain wording.
- `uniform_rhythm`
  - Vary sentence length.
  - Use one shorter sentence where natural.
- `over_explained`
  - Keep necessary details but remove redundant explanation.
  - Do not remove operational next steps.
- `generic_transitions`
  - Remove or replace with natural transitions.
- `policy_memo_voice`
  - Make the reply sound like one person answering another person.
- `low_specificity`
  - Pull concrete details from the draft/context without inventing new facts.
- `too_balanced_structure`
  - Break the overly neat structure.
  - Use more natural paragraph boundaries.
- `over_safe_tone`
  - Add a clearer human stance while staying accurate.
- `support_template_voice`
  - Keep support boundaries but remove macro-like wording.
- `application_cliche`
  - Replace generic enthusiasm with grounded, specific motivation from the draft.

### Why Users Cannot Easily Replicate This With GPT Alone

If a user manually tells GPT "write this less polished and more human," GPT can sometimes improve the output. But GPT alone usually lacks the full loop:

- It does not know the third-party writing signal result.
- It does not know whether the rewrite improved or got worse.
- It does not systematically diagnose why the draft was high.
- It does not apply scenario-specific repair strategies unless the user manually explains them.
- It does not keep a measured memory of which repair patterns work for customer support, cover letters, work updates, and general replies.
- It may reduce AI-like signal by making the reply too short or dropping important detail unless quality gates block that candidate.

Reply In My Voice should be positioned internally as:

> Diagnose why the draft feels AI-like, repair those causes with scenario-specific strategies, measure the result, and select the best usable rewrite.

This is the product value beyond a simple GPT prompt.

### Important Quality Rule

Never optimize only for the lowest AI-like signal.

The selected rewrite must also:

- preserve facts
- answer the user’s actual situation
- keep necessary detail
- maintain correct tone and risk boundaries
- avoid random names or unsupported facts
- avoid becoming too short to be useful

If a candidate gets a lower signal but drops important content, reject it and choose a more complete candidate.

### Implementation Notes For Next Round

Recommended technical changes:

- Replace `audienceOptions`, `purposeOptions`, and `mustKeepOptions` UI usage with `scenarioOptions`.
- Keep old request fields optional in the API temporarily for compatibility, but stop showing them in the main UI.
- Add `scenario` to the API request schema.
- Add scenario-specific prompt guardrails in `lib/openai.ts` or a small dedicated module such as `lib/rewrite-scenarios.ts`.
- Add a diagnosis step that produces internal tags from the AI-like cause taxonomy.
- Add a rewrite plan step or structured prompt section that uses the diagnosis tags.
- Add a targeted repair pass for candidates that remain too high or fail the improvement target.
- Add quality gates so lower-signal candidates are rejected if they drop required detail.
- Keep `tonePreset`, but reduce visible tone options.
- Update tests to assert:
  - exactly five scenario options exist
  - Quick context is not rendered
  - draft-only request passes validation
  - scenario is included in the prompt context
  - diagnosis tags can be generated for common AI-like patterns
  - targeted repair instructions are included when a candidate remains high
  - customer support scenario preserves long support detail
  - cover letter scenario does not invent experience
  - work update scenario preserves dates/status/next steps

### Required Evaluation Log Before Push And Deploy

During implementation, create and maintain:

- `docs/scenario-evaluation-results.md`

This file must be written before final push/deploy for the next development round.

Minimum evaluation requirement:

- 5 scenarios.
- At least 3 cases per scenario.
- At least 15 total cases.

For every case, record:

- scenario
- case name
- input type and approximate word/character count
- optional context/message if used
- rough draft before rewrite
- diagnosis tags
- rewrite plan summary
- rewritten output
- draft AI-like signal
- rewrite AI-like signal
- change points
- whether the rewrite went below 50%
- whether the output preserved names, dates, numbers, facts, and next steps
- whether any unsupported fact was introduced
- whether the output was rejected by a quality gate
- final decision: pass/fail/needs follow-up

Scenario coverage:

1. `Blank / custom`
   - Include at least one draft-only case.
   - Include one generic AI-written paragraph that is not an email.
   - Include one short bio or description.
2. `Email or message reply`
   - Include a teacher/student or school-related reply.
   - Include a sales or follow-up reply.
   - Include a general professional reply.
3. `Customer support`
   - Include the Priya billing/seat/proration style case.
   - Include one complaint or service issue.
   - Include one bug/account support case.
4. `Cover letter`
   - Include one job application draft.
   - Include one short personal statement.
   - Include one case with sparse details to verify it does not invent experience.
5. `Work update`
   - Include one status update.
   - Include one delay/blocker update.
   - Include one manager or team handoff note.

Deployment rule:

- Do not push and deploy the next rewrite-engine update until `docs/scenario-evaluation-results.md` contains the evaluation records.
- If some cases do not meet the target, document the failure and either fix the strategy or explicitly mark the residual risk.
- After tests and evaluation pass, push to GitHub and deploy to the production domain so the user can test on `replyinmyvoice.com`.

## Commercial Site Baseline

Use these defaults in the next development round:

- Pricing follows the active packs model: trial-code access plus Quick, Value, and Pro/API rewrite packs.
- Do not implement annual checkout in this round.
- Do not advertise an annual plan unless the Stripe annual price exists.
- Footer should include: `Operated by TimeAwake Ltd.`
- Support/contact email: `info@timeawake.co.nz`.
- Add or expose simple footer links for Privacy and Terms if the pages already exist or can be created safely.
- Privacy/Terms can be concise MVP pages. They must explain the current product truth: pasted messages, drafts, rewritten replies, writing-signal results, and rewrite metadata may be stored internally for quality improvement and are not exposed publicly, sold, or used in marketing without explicit approval.

Sapling feature boundary:

- Do not copy Sapling's Pro feature table into Reply In My Voice.
- Do not market Sapling-specific features such as autocomplete, snippets, domain administration, or chat assist.
- Use Sapling only as a third-party reference writing signal for the Naturalness Check.
- Keep user-facing terminology as `Naturalness Check`, `writing signal`, and `AI-like signal`.

## Open Items To Add Before Coding

The user mentioned there are more things to change. Before starting implementation, append any additional feedback under this section and then turn the full brief into an implementation plan.

## LearningOps V1 — DB-Backed Strategy Learning And Promotion

This section records the next product/system direction for rewrite learning. It supersedes any interpretation that "Codex memory" is the source of learning.

### Core Decision

Learning must come from production rewrite data stored by the app, not from Codex remembering chat history.

Production strategy changes must not become active by reading a new rule from the database at request time. A learned strategy only becomes production behavior after:

1. reading stored learning samples,
2. identifying a repeated failure pattern or severe regression,
3. adding or updating evaluation cases and tests,
4. changing the rewrite/repair code or prompt guardrails,
5. running the full validation suite,
6. pushing to GitHub,
7. deploying to Cloudflare.

In short:

```text
DB learning sample -> analysis -> strategy candidate -> code change -> tests -> push -> deploy
```

Do not implement:

```text
DB learning sample -> live prompt/rule hot update
```

### Why This Matters

The product should get smarter from real customer failures, but it must not let one noisy or private live sample silently change production behavior.

The correct model is:

- The website writes rewrite outcomes into the database.
- A scheduled LearningOps job reads those outcomes.
- The job proposes or applies bounded code changes only through the normal engineering pipeline.
- Production behavior changes only after tests and deployment.

This keeps the product learning loop real while preserving stability, testability, and rollback.

### Existing Source Of Truth

Current production sample table:

- `RewriteLearningSample`

Current fields already available:

- user id
- scenario
- tone preset
- optional context/message
- rough draft
- rewritten text
- draft AI-like signal
- rewrite AI-like signal
- signal change
- diagnosis tags
- rewrite plan summary
- candidate signal metadata
- internal strategy count
- repair count
- rejected candidate count
- status
- error code

The next LearningOps work should continue treating this table as the source of customer failure/success data.

### LearningOps V1 Scope

Add a first-class internal learning pipeline for Reply In My Voice.

Recommended components:

1. `Learning Sample Reader`
   - Reads recent `RewriteLearningSample` rows.
   - Focuses on failures, no-improvement results, worse-than-draft cases, high final signal, and repaired successes.
   - Does not print secrets.
   - Avoids dumping full user content into public docs or logs.

2. `Learning Analyzer`
   - Groups samples by scenario, diagnosis tags, tone preset, status, and signal outcome.
   - Detects repeated failure patterns.
   - Detects repair patterns that worked.
   - Separates weak one-off samples from promotable patterns.

3. `Strategy Candidate Generator`
   - Produces internal strategy candidates.
   - Each candidate must include:
     - failure pattern
     - affected scenario
     - evidence count
     - sample ids or safe references
     - proposed rewrite/repair change
     - required regression/eval case
     - risk level
     - promotion recommendation: `no-op`, `docs-only`, `test-needed`, `code-change`

4. `Promotion Planner`
   - Converts approved/promotable findings into development work:
     - update `docs/rewrite-strategy-memory.md`
     - add or update scenario evaluation cases
     - add unit/regression tests where possible
     - update `lib/rewrite.ts`, `lib/openai.ts`, `lib/rewrite-diagnosis.ts`, or scenario guardrail modules
   - Does not edit production strategy dynamically from DB rows.

5. `Validation And Deployment Gate`
   - A strategy promotion can only deploy if:
     - typecheck passes
     - lint passes
     - unit tests pass
     - e2e tests pass
     - Next build passes
     - Cloudflare/OpenNext build passes
     - banned-term scan passes
     - relevant scenario evaluation passes
   - If validation fails, do not deploy.

### Suggested Data Model Additions

Keep `RewriteLearningSample` as the raw event table.

Consider adding these tables in the next implementation:

- `LearningRun`
  - one row per daily/offline learning job
  - stores startedAt, finishedAt, status, sample count, findings count, promotion decision, validation status

- `LearningFinding`
  - one row per detected pattern
  - stores scenario, diagnosis tags, failure type, evidence count, severity, recommendation

- `StrategyCandidate`
  - one row per proposed strategy change
  - stores finding id, proposed change summary, risk level, status, linked test/eval path, linked commit hash if promoted

Do not add a live `RewriteStrategyRule` table that production reads dynamically unless a future design explicitly adds a reviewed, versioned, test-gated release mechanism. For now, production strategy lives in code.

### Scheduled Execution

The daily scheduled job should run every 24 hours and do this:

1. Read recent learning samples from the production database.
2. Generate or update `docs/rewrite-memory-digest.md`.
3. Write structured findings to the database or a committed internal artifact.
4. If no strong learning signal exists, stop after the digest/finding update.
5. If a strong repeated pattern or severe regression exists:
   - update strategy memory docs,
   - add/update eval or regression tests,
   - implement the minimal code/prompt guardrail change,
   - run validation,
   - commit and push,
   - deploy only if all gates pass.

The scheduled system may use Codex or a local agent runner as the execution backend, but the product learning source must remain the production database.

### Confirmed Automation Policy

The daily LearningOps workflow should be automatic, including GitHub push and Cloudflare deploy when qualified.

However, it must not deploy unconditionally every day.

Allowed automatic outcomes:

- `digest_only`
  - no strong learning signal found
  - update digest/finding records only
  - no production deploy
- `docs_only`
  - learning signal is useful but not enough for code promotion
  - update internal docs/report only
  - no production deploy
- `promoted`
  - strong repeated pattern or severe reproducible regression found
  - code/tests/eval updated
  - full validation passed
  - push to GitHub and deploy to Cloudflare
- `blocked`
  - validation failed, provider unavailable, sample evidence too weak, or change is outside rewrite-quality scope
  - write reason to docs/report
  - no push/deploy unless only safe report artifacts changed

Hard rule:

```text
Run every 24 hours automatically.
Push/deploy automatically only when a qualified strategy promotion passes all gates.
Never deploy just because the scheduled job ran.
```

### Safety Rules

- Do not expose raw user samples publicly.
- Do not use user samples in marketing.
- Do not log secrets.
- Do not auto-promote based on one weak sample.
- Severe regressions can trigger a targeted patch from one sample only when:
  - the failure is reproducible,
  - facts are preserved in the fix,
  - an eval/regression case is added,
  - full validation passes.
- Do not change Stripe, Clerk, DNS, pricing, payment behavior, or unrelated product behavior from the learning job.
- If the learning job cannot validate the change, it must stop before deploy and write the reason to docs.

### Acceptance Criteria For LearningOps V1

- Learning data flow is documented as:
  `user rewrite -> RewriteLearningSample -> LearningOps job -> strategy candidate -> code/test promotion`.
- The app never hot-loads unverified rewrite strategy from the database.
- A daily/offline command can read `RewriteLearningSample` and produce a useful digest/finding report.
- Strategy candidates include evidence, scenario, failure pattern, proposed change, risk, and required tests.
- A promoted strategy always creates or updates at least one regression/evaluation case.
- A promoted strategy only reaches production after GitHub push and Cloudflare deploy.
- Privacy/Terms copy accurately states that submitted content and rewrites may be stored internally for quality improvement.

## Next Rewrite Quality Fix — Signal Must Improve

This is the next required development goal before further commercial polish.

### Problem To Fix

Real testing found a serious failure mode:

- A long customer-support draft measured about 89% AI-like signal.
- The first rewrite and retry measured 99-100%.
- The UI still returned the rewritten text as if the request had succeeded.

That must not happen. A rewrite that increases the AI-like signal is a failed product result, not a successful rewrite.

### Required Product Behavior

When writing-signal scores are available, a successful user-visible rewrite must satisfy at least one:

- final rewrite is below 50% AI-like signal, or
- final rewrite is at least 30 points lower than the draft.

Hard rejection rules:

- Reject any candidate where `rewriteSignal >= draftSignal`.
- Reject or repair any candidate where `rewriteSignal > 50` and reduction is less than 30 points.
- Never display an increased signal result as `Lower AI-like signal`.
- If every bounded internal attempt fails, show a safe failure state instead of returning a bad rewrite.
- Do not charge usage when the request fails because every candidate is rejected by quality gates.

### Required Engine Change

Upgrade the engine from simple retry to:

`draft measurement -> diagnosis -> rewrite plan -> candidate rewrite -> candidate measurement -> failure analysis -> targeted repair -> remeasurement -> gated selection`

Repair must be specific. It should receive:

- the original draft
- the rejected candidate
- draft score
- candidate score
- diagnosis tags
- failure reason
- required facts
- scenario guardrails

The repair prompt/strategy must directly remove the observed failure pattern. For long customer-support replies, it must avoid macro-like patterns such as:

- `I see how this can be confusing`
- `From what you described`
- `It seems`
- `To help clarify`
- `For next steps`
- overly balanced paragraph rhythm
- polished support-template phrasing

The repaired reply must still preserve concrete facts, billing details, dates, numbers, user counts, and next steps.

### Evaluation Requirement

Before push/deploy, update `docs/scenario-evaluation-results.md`.

Minimum measured evaluation:

- at least 25 total measured cases
- at least 10 long cases of 300-900 words
- at least 5 long customer-support cases
- include the Priya billing/proration regression case that previously failed with `89 -> 99/100`
- include at least 3 cases where the first candidate fails and a targeted repair pass improves it

Each case must record:

- scenario
- tone
- approximate word/character count
- diagnosis tags
- rewrite plan summary
- draft AI-like signal
- first candidate AI-like signal
- repaired candidate AI-like signal if used
- final selected AI-like signal
- score change
- rejected candidate reason if any
- expected facts
- facts preserved
- unsupported facts introduced
- final decision: pass/fail

### Deployment Criteria

Do not push/deploy this rewrite-engine update unless:

- average measured reduction is at least 30 points
- at least 70% of measured rewrites are below 50% AI-like signal
- no measured case selects a final rewrite that is worse than the draft
- the Priya long customer-support regression passes
- tests cover candidate rejection, repair invocation, safe failure, no usage charge on quality failure, and Naturalness Check rendering for worse/no-improvement states

Development can use additional OpenAI and Sapling calls to find the strategy. This is acceptable during R&D. Production requests must remain bounded:

- 1 draft writing-signal call
- up to 3 initial rewrite candidates
- up to 2 targeted repair candidates
- up to 5 rewrite writing-signal calls

If the bounded production loop cannot produce a passing candidate, fail safely and do not charge usage.

## Cost-Controlled Character Limits — Next Fix

This section records the next development requirement from the 2026-05-21 cost discussion. The current live app still allows roughly 10,000 combined characters across the two visible textareas. That is too wide for the active rewrite packs and Pro/API pricing, because long support replies can trigger multiple OpenAI calls, Sapling draft/final/repair checks, and one strong-model escalation.

### Product Decision

Use these production limits for the next implementation run:

```text
Context or message: max 3000 characters
Draft to rewrite: 10 to 3000 characters
Visible combined cap: 5000 characters
Backend combined cap: 5000 characters
```

Keep the optional hidden/legacy fields bounded if they still exist in API types:

```text
Audience: max 300 characters
Purpose: max 500 characters
What actually happened: max 1000 characters
Facts to preserve: max 1000 characters
```

The server-side combined cap must include every submitted field, not only the two visible textareas.

### Rationale

The current pipeline treats about `260-300 words` as long/high-risk input and may allow more internal attempts. A 5,000-character combined cap still supports substantial everyday emails, customer-support replies, work updates, and cover letters, while reducing the chance that one low-price subscription user can consume excessive OpenAI + Sapling cost by pasting full long email threads.

Product guidance for long threads:

```text
For long threads, paste only the part you need to answer and the facts that matter.
```

### Files To Change

Backend validation:

```text
lib/validation.ts
```

Frontend workspace:

```text
components/app/rewrite-workspace.tsx
```

Tests:

```text
tests/unit/validation.test.ts
tests/unit/workspace-copy.test.ts or a new focused UI test if existing tests do not cover length labels
```

Docs:

```text
AGENTS.md
docs/next-development-brief.md
docs/manual-setup.md if it mentions request limits
```

### Implementation Requirements

- Replace `messageToReplyTo` max from `5000` to `3000`.
- Replace `roughDraftReply` max from `5000` to `3000`.
- Replace combined request cap from `10000` to `5000`.
- Update the visible combined counter from `/10000` to `/5000`.
- Make `canSubmit` use the same `5000` combined cap as backend validation.
- Keep `roughDraftReply` minimum at `10` characters.
- Add helper copy near the input area or under the combined counter:

```text
For long threads, paste only the part you need to answer and the facts that matter.
```

- Do not remove the Naturalness Check, adaptive rewrite loop, or quality-failure no-charge behavior.
- Do not count rejected validation requests against quota.

### Acceptance Criteria

- Frontend prevents submitting when `messageToReplyTo.length + roughDraftReply.length > 5000`.
- Backend returns `400` for a combined request over `5000` characters.
- Backend returns `400` when either visible field exceeds its individual max.
- A valid draft-only request under `3000` characters still works.
- A valid request with context plus draft under `5000` combined characters still works.
- UI shows the new remaining character values and `/5000` combined counter.
- Tests cover the new max values and combined cap.
- Build, typecheck, lint, unit tests, and production deployment pass before release.

## Admin Cost Observability And Internal Operations Dashboard

This section records the next development requirement for cost tracking and an internal admin dashboard. It should be implemented before making final live pricing decisions, because the adaptive rewrite orchestrator can use multiple OpenAI calls, Sapling calls, targeted repairs, and one strong-model escalation.

### Product Decision

Add DB-backed rewrite cost telemetry and an internal admin dashboard.

The dashboard is for the operator/developer only. It is not a customer-facing page and must not be linked from the public marketing navigation.

Primary question it must answer:

```text
For each rewrite request, who used it, when it ran, what scenario/tone was used,
whether it succeeded, how much the AI-like signal changed, how many internal
strategies/repairs/escalations ran, and what the estimated OpenAI + Sapling cost was.
```

### Why This Is Needed

Pricing cannot be set only from theoretical model prices. The product needs real request-level cost data:

- OpenAI input/output tokens by model role.
- Sapling character counts and call counts.
- Whether a strong-model escalation was used.
- Whether a request succeeded, quality-failed, or provider-failed.
- Average cost per successful rewrite.
- Cost distribution by scenario, tone, and input length.
- Heavy users and expensive requests.
- Gross-margin estimate for the current subscription quota.

### Admin Page Scope

Create an internal route:

```text
/admin
```

Recommended subpages:

```text
/admin
/admin/rewrites
/admin/rewrites/[id]
/admin/users
/admin/costs
/admin/learning
```

MVP can ship with `/admin` and `/admin/rewrites` first, as long as the data model supports later expansion.

### Admin Entry Point

The admin dashboard entry should appear only after the user signs in.

Placement:

```text
/app top header/account area
```

Behavior:

- If the signed-in user's email is in `ADMIN_EMAILS`, show a small `Admin` icon/button in the app header near the user/account controls.
- The button links to `/admin`.
- If the signed-in user is not an admin, do not render the button at all.
- Do not show the admin entry on the public landing page, pricing page, sign-in page, or sign-up page.
- Do not show disabled or hidden-placeholder admin UI for non-admins.

Recommended UI:

```text
Icon button or compact "Admin" button
Tooltip/title: Admin dashboard
Destination: /admin
```

This lets the owner sign in with `ADMIN_EMAILS=chuanqiao1128@gmail.com` and access the dashboard from the normal app UI, while every other user sees only the standard product workspace.

### Admin Access Control

Admin access must require Clerk authentication plus an explicit allowlist.

Recommended env vars:

```env
ADMIN_EMAILS=
ADMIN_CLERK_USER_IDS=
ADMIN_ALLOW_RAW_REWRITE_TEXT=false
```

Rules:

- If the signed-in Clerk user is not in `ADMIN_EMAILS` or `ADMIN_CLERK_USER_IDS`, return 404 or redirect to `/app`.
- Do not use subscription status as admin authorization.
- Do not expose admin pages to crawlers.
- Do not put admin links in the public header.
- Non-admin users must not see the `/app` admin entry.
- Manual navigation to `/admin` by a non-admin must be denied server-side.
- Default `ADMIN_ALLOW_RAW_REWRITE_TEXT=false` in production.

### Data To Log Per Rewrite Request

Add request-level telemetry, separate from the existing learning sample table.

Recommended aggregate table:

```text
RewriteCostLog
```

Fields:

```text
id
userId
learningSampleId nullable
requestId
strategyVersion
scenario
tonePreset
status
errorCode nullable
startedAt
finishedAt nullable
durationMs nullable
inputCharCount
draftWordCount
rewriteWordCount nullable
draftAiLikePercent nullable
rewriteAiLikePercent nullable
changePoints nullable
internalStrategies
repairCandidates
rejectedCandidates
usedEscalation
openAiInputTokens
openAiOutputTokens
openAiCostUsd
saplingCallCount
saplingCharacters
saplingCostUsd
totalEstimatedCostUsd
modelsUsedJson
providerCallsJson
createdAt
updatedAt
```

Recommended detailed provider-call table:

```text
RewriteProviderCall
```

Fields:

```text
id
costLogId
provider
role
model nullable
inputTokens nullable
outputTokens nullable
characters nullable
estimatedCostUsd
latencyMs nullable
success
errorCode nullable
createdAt
```

This gives both:

- Fast dashboard cards from `RewriteCostLog`.
- Debuggable call-level cost breakdown from `RewriteProviderCall`.

### Cost Estimation Rules

OpenAI:

- Use actual `usage.prompt_tokens` and `usage.completion_tokens` from OpenAI responses when available.
- Map the pipeline role to current pricing config:
  - `cheap_structured`
  - `mid_writer`
  - `strong_escalation`
- Pricing should continue to come from env/config, not hardcoded directly in dashboard components.

Sapling:

- Count the characters sent to Sapling for each Naturalness Check call.
- Estimate cost from a config value such as:

```env
SAPLING_PRICE_PER_1000_CHARS_USD=0.005
```

Exchange rate:

- Keep stored request costs in USD first.
- Add optional display conversion:

```env
ADMIN_NZD_PER_USD=
```

If conversion is missing, show USD only.

### Admin Dashboard Metrics

The `/admin` overview should show:

- Total rewrite requests today / 7 days / 30 days.
- Successful rewrites.
- Quality failures.
- Provider/server failures.
- Average AI-like signal drop.
- Percentage below 50% final signal.
- Average estimated cost per successful rewrite.
- P95 estimated cost per rewrite.
- Total estimated OpenAI cost.
- Total estimated Sapling cost.
- Total estimated AI cost.
- Average internal strategies per request.
- Escalation rate.
- Top expensive scenarios.

The `/admin/rewrites` table should show:

- Date/time.
- User email or user id.
- Scenario.
- Tone.
- Status.
- Draft signal.
- Rewrite signal.
- Signal change.
- Internal strategies / repairs / rejected candidates.
- Used escalation.
- Estimated cost.
- Duration.
- Link to detail.

The `/admin/rewrites/[id]` detail page should show:

- Request metadata.
- Cost breakdown by provider call.
- Candidate/signal metadata.
- Diagnosis tags and rewrite plan summary.
- Learning sample id if linked.
- Raw input/output only when `ADMIN_ALLOW_RAW_REWRITE_TEXT=true`.

### Privacy And Data Minimization

Because rewrite samples can contain sensitive user text:

- The overview/table should not show full user-submitted text.
- Show short previews only if needed, and avoid rendering long raw text in list views.
- Detail pages may show full raw input/output only for allowlisted admins and only when the explicit env flag is enabled.
- Never show secrets, API keys, Stripe ids beyond safe object ids, or raw provider payloads containing credentials.
- Do not expose admin API responses to unauthenticated users.

### Pricing Decision Support

The dashboard should include a simple pricing support panel:

- Current plan price, quota, and Stripe fee estimate.
- Estimated average variable cost per successful rewrite.
- Estimated cost at 40, 50, and 100 rewrites/month.
- Estimated gross margin for the current plan.

This panel is internal only. It helps decide whether the public plan should be:

- a lower-priced pack with a lower quota,
- `NZD $12/month` with about 50 successful rewrites,
- or `NZD $19/month` with about 100 successful rewrites.

Current public pricing decision: trial-code access plus Quick, Value, and Pro/API rewrite packs.

### Acceptance Criteria

- Every successful and quality-failed rewrite creates or updates a `RewriteCostLog`.
- OpenAI token usage is captured when the OpenAI API returns usage fields.
- Sapling call count and character count are captured for draft/final/repair measurements.
- The admin dashboard is inaccessible to non-admin signed-in users.
- `/admin` shows aggregate cost and quality metrics.
- `/admin/rewrites` shows recent request-level rows.
- A request detail view shows provider-call cost breakdown.
- Raw user text is hidden by default in production.
- Unit tests cover cost estimation and admin authorization.
- Route tests cover non-admin denial and admin access.
- Deployment docs list the required admin env vars without secret values.

---

## Next Required Plan: Azure Functions, Azure SQL, And Entra External ID Migration

Source plan:

```text
docs/superpowers/plans/2026-05-21-azure-functions-sql-entra-migration.md
```

Goal:

```text
Fully remove Clerk and Neon from the production runtime by moving customer
authentication to Microsoft Entra External ID, moving application data to Azure
SQL, and moving backend API/worker execution to Azure Functions/.NET.
```

Recommended architecture:

```text
Cloudflare frontend for public pages and app shell
Microsoft Entra External ID for customer sign-in/sign-up
Google social login configured inside Entra External ID
Azure Functions / .NET API for auth validation, Stripe, quota, rewrite requests, and admin APIs
Azure SQL for users, auth identities, subscriptions, quota, rewrite attempts, outbox, costs, and learning samples
Azure Service Bus for asynchronous rewrite jobs
Azure Functions worker or .NET worker for queue-triggered rewrite processing
Application Insights for API/worker telemetry
```

Important constraint:

```text
Do not start new work on Azure AD B2C unless the user explicitly chooses it.
Use Microsoft Entra External ID for customer-facing identity.
Do not reintroduce Clerk after this migration starts.
Do not use App Service unless Azure Functions cannot support the required runtime behavior.
```

Current missing inputs the user should prepare in `.env.local` / local environment notes.
Do not paste secret values into chat:

```text
AZURE_SUBSCRIPTION_ID
AZURE_TENANT_ID
AZURE_LOCATION
AZURE_RESOURCE_GROUP

AZURE_FUNCTION_APP_NAME
AZURE_FUNCTION_STORAGE_ACCOUNT_NAME
AZURE_APPLICATION_INSIGHTS_NAME

AZURE_SQL_SERVER_NAME
AZURE_SQL_DATABASE_NAME
AZURE_SQL_ADMIN_USER
AZURE_SQL_ADMIN_PASSWORD

AZURE_SERVICE_BUS_NAMESPACE
AZURE_SERVICE_BUS_QUEUE_NAME
AZURE_SERVICE_BUS_CONNECTION_STRING

AZURE_EXTERNAL_ID_TENANT_ID
AZURE_EXTERNAL_ID_TENANT_SUBDOMAIN
AZURE_EXTERNAL_ID_AUTHORITY
AZURE_EXTERNAL_ID_FRONTEND_CLIENT_ID
AZURE_EXTERNAL_ID_API_CLIENT_ID
AZURE_EXTERNAL_ID_API_AUDIENCE
AZURE_EXTERNAL_ID_API_SCOPE
AZURE_EXTERNAL_ID_WELL_KNOWN_URL
AZURE_EXTERNAL_ID_SIGN_IN_FLOW_NAME

GOOGLE_CLIENT_ID_FOR_ENTRA
GOOGLE_CLIENT_SECRET_FOR_ENTRA

NEXT_PUBLIC_AZURE_API_BASE_URL
NEXT_PUBLIC_ENTRA_AUTHORITY
NEXT_PUBLIC_ENTRA_CLIENT_ID
NEXT_PUBLIC_ENTRA_API_SCOPE
```

Dashboard-only or permission-sensitive work:

```text
Create or confirm a Microsoft Entra External ID external tenant.
Create the frontend app registration for the Cloudflare-hosted SPA/app shell.
Create the API app registration for the Azure Functions backend and expose an API scope.
Create a sign-up/sign-in user flow and attach the app.
Create a Google OAuth client for Entra External ID federation.
Add the exact Entra-provided Google redirect URI into Google Cloud Console.
Add Google client id/secret to Entra External ID identity providers.
Select Google as a sign-in option in the Entra user flow.
Confirm Azure CLI has permission to create/update Functions, Azure SQL, Service Bus, Key Vault/app settings, and Application Insights resources.
Create/update Stripe webhook endpoint after the Azure API URL is finalized.
```

Facebook/Apple/social-login expansion:

```text
Do not block the first migration on Facebook or Apple.
Finish Google through Entra External ID first.
Add Facebook later only after privacy policy and data deletion URL are ready.
```

User preparation checklist before the next autonomous run:

```text
1. Azure:
   - Confirm az login works.
   - Confirm the correct subscription is selected.
   - Confirm the resource group name to use.
   - Decide whether to reuse the existing Azure SQL server/database or let the run create fresh prod/dev names.

2. Entra External ID:
   - Create an external tenant if it does not exist.
   - Record tenant id and tenant subdomain.
   - Create app registrations for frontend and API, or allow the run to create them if Azure CLI permissions allow it.
   - Create sign-up/sign-in user flow.

3. Google:
   - Create a Google OAuth web application for Entra federation.
   - Add replyinmyvoice.com to the OAuth consent screen authorized domains if required by Google.
   - Add the exact redirect URI shown by Entra External ID when configuring Google federation.
   - Store Google client id and secret in `.env.local`; do not paste them into chat.

4. Stripe:
   - Wait until the Azure API route is deployed.
   - Then create/update the Stripe webhook endpoint to the Azure API webhook URL.
   - Store the new webhook secret in `.env.local`.

5. Secrets location:
   - Put local development values in `/Users/qc/Desktop/CloudFlare/.env.local`.
   - Put explanatory notes only in `/Users/qc/Desktop/CloudFlare/local-env.md`.
   - Never commit real secret values.
```
