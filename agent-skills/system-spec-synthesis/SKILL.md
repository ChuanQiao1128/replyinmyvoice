---
name: system-spec-synthesis
description: Use when converting product requirements, AGENTS.md rules, repo context, architecture notes, APIs, or data models into an implementation-ready system specification for Codex or Claude Code.
---

# System Spec Synthesis

Create a spec that another agent can implement without guessing. The spec must separate source facts from assumptions, preserve project constraints, and make interfaces, data flow, risks, and verification explicit.

## Workflow

1. Read project instructions first: `AGENTS.md`, requirements docs, existing design docs, and nearby code.
2. Identify source-of-truth inputs and quote file paths, not secret values.
3. Convert loose requirements into concrete sections:
   - problem and non-goals
   - actors and user journeys
   - components and ownership
   - APIs, messages, jobs, or events
   - data model and state lifecycle
   - error handling and resilience
   - rollout and verification
4. Mark assumptions explicitly. Do not invent product decisions, pricing, quotas, credentials, or external dashboard state.
5. End with implementation checkpoints that Codex can execute and tests that prove the behavior.

## Output Contract

Write or propose a spec with these headings:

```markdown
# <Feature/System> Specification

## Context
## Goals
## Non-Goals
## Current System
## Proposed Architecture
## Data Model
## API and Job Contracts
## State and Error Handling
## Security and Privacy
## Rollout Plan
## Verification Plan
## Open Questions
```

## Bundled Resources

- Use `scripts/spec_outline.py` to generate a clean spec skeleton.
- Use `references/demo-prompts.md` for interview-safe prompts that demonstrate this skill.

## Common Mistakes

- Do not turn uncertain requirements into silent decisions.
- Do not include secret values from env files or credential notes.
- Do not skip verification planning; a spec without checks is not implementation-ready.
- Do not mix project-specific policy into this skill. Put project-only rules in `AGENTS.md`.
