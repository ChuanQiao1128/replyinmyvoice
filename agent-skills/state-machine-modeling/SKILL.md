---
name: state-machine-modeling
description: Use when designing, changing, or testing lifecycles with statuses, transitions, quotas, subscriptions, jobs, webhooks, reservations, deployments, or multi-step workflows.
---

# State Machine Modeling

Make lifecycle behavior explicit before implementation. A good state machine prevents ambiguous status fields, hidden transitions, and quota or billing edge cases.

## Workflow

1. Name the entity whose lifecycle is being modeled.
2. List all states and define what each state means in persisted data.
3. List events that cause transitions. Separate external events from internal commands.
4. Create a transition table with allowed from-state, event, to-state, side effects, and rejection behavior.
5. Define invariants that must always hold.
6. Add tests for allowed transitions, illegal transitions, duplicate events, and recovery from partial failure.
7. Reflect the model in code with enums, constants, typed helpers, or a transition function. Avoid scattered string comparisons.

## Output Contract

Always produce:

- state list
- event list
- transition table
- invariants
- illegal transitions
- persistence implications
- test checklist

## Implementation Guidance

- Prefer a single transition function for complex lifecycles.
- Treat duplicate webhooks and queue redelivery as normal events.
- Keep billing state, quota state, and job state distinct unless the product explicitly couples them.
- Use database constraints for invariants that must survive concurrent requests.
- Document migration behavior when renaming or splitting states.

## Bundled Resources

- Use `scripts/state_machine_template.py` to generate a markdown transition-table skeleton.
- Use `references/demo-prompts.md` for interview-safe demo prompts.

## Common Mistakes

- Using `active` or `complete` to mean several different things.
- Allowing every state to transition to every other state in service code.
- Forgetting terminal states and retryable failure states.
- Testing only valid transitions and skipping illegal or duplicate events.
