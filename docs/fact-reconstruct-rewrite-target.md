# Fact Reconstruct Rewrite Target

Date: 2026-05-19

This is the source of truth for the next rewrite-engine run.

## Goal

Replace the old rewrite strategy with one official fact-reconstruct pipeline. The production API must no longer switch between the old prompt/repair/fallback workflow and a separate v2 path. The new workflow is the rewrite engine.

## Required Pipeline

Production `/api/rewrite` must run this sequence:

1. Normalize input.
   - `messageToReplyTo` is optional.
   - `roughDraftReply` is required.
   - All user-provided text can contribute facts.

2. Extract facts.
   - Extract facts from all available user-provided text.
   - Do not decide facts by visible scenario.
   - Preserve names, dates, times, deadlines, counts, money, assignments, conditions, support availability, signoffs, negative constraints, and ordered steps.

3. Classify scenario.
   - Use scenario only for style card and risk guidance.
   - Low confidence falls back to `general_professional_reply`.
   - Scenario classification must never decide which facts can be dropped.

4. Load style card.
   - Start with these cards:
     - `teacher_parent_email_default`
     - `workplace_followup_default`
     - `customer_support_resolution`
     - `sales_followup_warm`
     - `general_professional_reply`

5. Generate candidates.
   - Generate exactly 3 candidate replies from facts plus style card.
   - The writer must not directly paraphrase the original wording.
   - The writer must not receive Sapling scores.
   - The writer must not add subject lines, headings, invented promises, invented names, invented deadlines, invented discounts, or unsupported outcomes.

6. Review candidates.
   - Review for factual accuracy, tone appropriateness, naturalness, concision, low-template feel, and clarity.
   - Select the best candidate before Sapling is called.
   - Sapling must not rank candidates.

7. Finalize.
   - Apply only light edits.
   - Preserve all facts.
   - Do not make the message longer unless needed to restore facts.

8. Fact consistency gate.
   - Deterministic checks catch missing required facts and unsupported facts.
   - LLM fact check catches semantic fact loss or changed conditions.
   - If the first final fails facts, run one bounded escalation/repair from facts.

9. Naturalness Check gate.
   - Run Sapling after fact gates.
   - Sapling is a final reference gate only.
   - Sapling score, threshold, or detector-specific wording must not be included in prompts.

10. Bounded escalation.
    - If fact gate or Naturalness Check gate fails, run at most one strong-model escalation.
    - Re-run fact gates and Sapling on the escalated result.

11. Final decision.
    - If all gates pass, return success and charge one usage attempt.
    - If any required gate still fails, return quality failure, do not charge, and do not show a weak rewrite as successful.

## Naturalness Success Rule

Default:

```env
NATURALNESS_THRESHOLD=40
MAX_ESCALATIONS=1
```

Success requires:

- if draft AI-like signal is above `NATURALNESS_THRESHOLD`, rewrite AI-like signal must be at or below `NATURALNESS_THRESHOLD`;
- if draft AI-like signal is already at or below `NATURALNESS_THRESHOLD`, rewrite AI-like signal must not be higher than the draft;
- Sapling unavailable is a quality failure for production rewrite success and must not charge usage.

## Model Roles

Do not hardcode model names into business logic. Use role-based config:

```env
OPENAI_MODEL_CHEAP_STRUCTURED=
OPENAI_MODEL_MID_WRITER=
OPENAI_MODEL_STRONG_ESCALATION=
```

Role usage:

- `cheap_structured`: fact extraction, scenario classification, reviewer, LLM fact check.
- `mid_writer`: candidate generation, finalizer.
- `strong_escalation`: one bounded escalation after fact or naturalness failure.

If the role variables are absent in local development, fallback to `OPENAI_MODEL` so local testing can run with the existing configured model. Deployment should set the role variables explicitly.

## Old Rewrite Strategy Rule

The old `rewriteWithOptimization` style workflow must not remain in the `/api/rewrite` production path.

Allowed:

- keep small deterministic helper functions only if they are clearly named and used as a candidate source inside the new pipeline;
- keep historical docs and old eval reports as history.

Not allowed:

- runtime switch back to old rewrite strategy;
- returning best-available weak rewrites as success after gates fail;
- using old prompt-repair-fallback as a parallel production path.

## Focused Evaluation Scope

Do not run an 80-case eval in this phase.

Run at most 40 live measured eval cases:

- 25 draft-only cases
- 5 teacher/parent cases
- 5 workplace update cases
- 5 customer-support or sales follow-up cases

Every eval case must record:

- case id
- scenario inferred or style card used
- draft AI-like signal
- rewrite AI-like signal
- signal delta
- success or quality failure
- quality failure reason, if any
- fact preservation result
- unsupported fact result
- whether escalation was used

Core regression case:

- The long Monica/Jordan teacher draft-only email must return a successful fact-preserving rewrite under the Naturalness Check threshold. Returning quality failure for this case is not acceptable as final state.

## Completion Criteria

This run can stop only when all are true:

1. `/api/rewrite` uses the new fact-reconstruct pipeline as the only production rewrite path.
2. No config flag can switch production back to the old rewrite engine.
3. Usage is charged only after successful rewrite output passes all gates.
4. Quality failures return no-charge `422` responses.
5. Unit tests cover:
   - model output shape normalization
   - fact extraction/fact gate behavior
   - fact-gate escalation
   - Naturalness Check gate
   - Sapling unavailable quality failure
   - API no-charge quality failure
6. `npm run lint` passes.
7. `npm run typecheck` passes.
8. `npm test` passes.
9. `npm run build` passes using Node 22.
10. Focused eval report is written to `docs/scenario-evaluation-results.md`.
11. `docs/rewrite-strategy-memory.md` and `docs/rewrite-learning-system.md` reflect the final lessons.
12. GitHub push and Cloudflare deployment are completed if credentials remain valid.

## Stop Conditions

Do not stop for ordinary implementation failures. Continue fixing:

- TypeScript errors
- lint errors
- unit test failures
- build failures
- model JSON shape problems
- poor candidate quality
- Sapling scores above threshold
- eval failures
- Cloudflare build/deploy command issues

Stop and ask the user only if:

- OpenAI credentials are invalid and no configured fallback model works.
- Sapling credentials are invalid or unavailable long enough that Naturalness Check cannot be evaluated.
- GitHub push permission is denied.
- Cloudflare deploy permission is denied.
- A real paid Stripe live-mode action, real charge, or production-domain cutover is required.
- Any operation would expose or commit secrets.

## User-Facing Copy Rules

Do not use:

- AI detection bypass
- detector bypass
- undetectable
- humanizer
- evade detection
- bypass filters
- trick detectors

Use:

- Naturalness Check
- AI-like signal
- natural writing
- personal tone
- preserve facts
- internal quality bar

## Execution Result

Updated: 2026-05-19

Production rewrite behavior:

- `/api/rewrite` now uses the fact-reconstruct pipeline as the only production rewrite path.
- The old `rewriteWithOptimization` path was removed from production code.
- There is no runtime config switch back to the old rewrite engine.
- Quality-gate failures return no-charge quality failure responses instead of weak successful rewrites.

Latest focused evaluation:

- Cases evaluated: 40
- Draft-only cases: 29
- Measured cases: 40
- Average AI-like signal drop: 89 pts
- Rewrites below 50% AI-like signal: 40/40
- Final selected rewrites worse than draft: 0/40
- Customer-usable pass count: 38/40
- Strict signal pass count: 38/40
- Report: `docs/scenario-evaluation-results.md`

Verification completed:

- `npm run lint`
- `npm run typecheck`
- `npm test`
- `npm run build`
- user-facing banned-term grep over `app`, `components`, `public`, and `lib`
- Prisma migration deploy check: no pending migrations

Deployment completed:

- Worker: `replyinmyvoice-app`
- Verified URL: `https://replyinmyvoice-app.qc1128qc.workers.dev`
- Smoke checks:
  - `/` returned 200
  - `/pricing` returned 200
  - `/app` redirected unauthenticated users to `/sign-in`
  - same-origin unauthenticated `POST /api/rewrite` returned 401
  - `/api/health/db` returned `{"ok":true}`
  - invalid Stripe webhook payload returned 400

Deployment note:

- The local Desktop workspace had an old `.open-next` build artifact with macOS/iCloud `dataless` placeholder directories, which caused recursive delete/move operations to hang.
- The deployed build was produced from a temporary `/tmp/rimv-cf-build` source copy that excluded generated folders and secrets, with environment variables loaded from the original local `.env.local` without printing secret values.
- The production domain was not cut over.
