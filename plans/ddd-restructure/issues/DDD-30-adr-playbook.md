# DDD-30: ADR-0002 (DDD layering) + migration playbook

## Context
Record the target architecture and the strangler migration pattern so later waves follow it
consistently. Docs-only; no code change.
Read first: `docs/architecture-decision-record.md` (ADR-0001 format reference),
`plans/ddd-restructure/REQUIREMENT.md` (§3 target architecture, §4 decisions, §6 phasing).

## Constraints
- Docs-only. No code changes whatsoever.
- Follow the markdown structure of ADR-0001 (Context / Decision / Consequences / Alternatives).

## Changes required
1. `docs/architecture-decision-record-0002-ddd-layering.md` — record the target layering (§3),
   decisions D1-D5, rejected alternatives, and the migration sequencing across waves.
2. `docs/ddd-migration-playbook.md` — the standard strangler steps for migrating ONE bounded
   context (define interfaces → implement in Infrastructure → add Application handler → shell the
   entry point → later cleanup deletes the old service), using the Wave-1 Rewrite create/get
   migration (DDD-11/12/20/21) as the worked example.

## Acceptance (machine-checkable)
- `test -f docs/architecture-decision-record-0002-ddd-layering.md`
- `test -f docs/ddd-migration-playbook.md`
- `grep -qi "strangler" docs/ddd-migration-playbook.md` (playbook has real content)

## DO NOT
- Do NOT modify any code or csproj.
- Do NOT push, open a PR, or touch main.
