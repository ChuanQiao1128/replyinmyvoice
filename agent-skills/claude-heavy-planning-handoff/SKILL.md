---
name: claude-heavy-planning-handoff
description: Use when a task is too broad for immediate coding, spans multiple subsystems, needs architecture planning, or should be routed from Codex to Claude Code for heavy planning.
---

# Claude Heavy Planning Handoff

Prepare a clean handoff from Codex to Claude Code for architecture-heavy work. Codex should collect local repo facts and constraints; Claude Code can then do deeper planning without rediscovering the basics.

## When to Handoff

Use this skill when any condition is true:

- the task spans more than three modules or services
- the task changes architecture, data flow, deployment, auth, billing, or queues
- implementation depends on unresolved product decisions
- the next step is a plan/spec, not code
- the user explicitly asks to route heavy planning to Claude Code

## Workflow

1. Read project instructions and active docs without printing secrets.
2. Summarize current repo structure and relevant files.
3. Identify constraints, known decisions, and blockers.
4. Convert the task into a Claude Code prompt with:
   - objective
   - context files
   - constraints
   - expected output
   - questions to resolve
5. Keep the handoff actionable. Do not include raw `.env`, credentials, or private user text.
6. If Codex can safely continue with a small local implementation slice, propose that slice separately.

## Output Contract

```markdown
# Claude Code Planning Handoff

## Objective
## Repo Context
## Relevant Files
## Constraints
## Risks
## Requested Claude Code Output
## Prompt To Paste Into Claude Code
```

## Bundled Resources

- Use `scripts/build_handoff_brief.py` to generate a repo-aware handoff skeleton.
- Use `references/demo-prompts.md` for interview-safe demo prompts.

## Common Mistakes

- Sending secrets or entire env files into the handoff.
- Asking Claude Code to plan without enough repo context.
- Using handoff for small code edits that Codex can finish directly.
- Hiding unresolved decisions instead of listing them as questions.
