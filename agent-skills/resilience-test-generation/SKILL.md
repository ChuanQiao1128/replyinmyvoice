---
name: resilience-test-generation
description: Use when adding or reviewing tests for retries, timeouts, rate limits, provider failures, webhook replay, queue failures, quota races, idempotency, or recovery behavior.
---

# Resilience Test Generation

Generate tests that prove the system behaves correctly when dependencies fail, requests repeat, or state changes race. Prefer deterministic local fakes over live cloud/provider calls.

## Workflow

1. Identify the critical operation and the invariant that must survive failure.
2. List dependency boundaries: database, queue, webhook provider, payment provider, AI provider, writing signal provider, email, cache, or cloud runtime.
3. Build a failure matrix:
   - timeout
   - transient 5xx
   - permanent 4xx
   - duplicate request/event
   - partial success after persistence
   - concurrent requests
   - malformed payload
4. Choose the lowest-level test that proves the invariant. Use unit tests for pure service behavior, integration tests for persistence and routing, and end-to-end tests only for user-visible flows.
5. Implement fakes, seeded data, and assertions before changing production behavior when this is a bugfix.
6. Run the focused test command, then the broader suite that could regress.

## Test Design Rules

- Do not hit live Stripe, OpenAI, Sapling, Azure, Clerk, or database production endpoints.
- Assert final state, not just response status.
- Assert idempotency by repeating the same event or request.
- Assert quota/accounting behavior on both success and provider failure.
- Assert no duplicate jobs or billing side effects when retries occur.
- Keep clock and random behavior injectable or fixed.

## Bundled Resources

- Use `scripts/resilience_matrix.py` to generate a markdown failure matrix.
- Use `references/demo-prompts.md` for interview-safe demo prompts.

## Common Mistakes

- Testing only the happy path after adding retry logic.
- Mocking so much that the test no longer proves persistence or idempotency.
- Counting failed provider attempts as successful usage without an explicit product rule.
- Forgetting duplicated webhook events and queue redelivery.
