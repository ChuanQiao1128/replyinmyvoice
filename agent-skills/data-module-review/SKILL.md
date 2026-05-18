---
name: data-module-review
description: Use when reviewing Prisma, Entity Framework, migrations, data access services, usage counters, idempotency records, transactions, indexes, or persistence changes.
---

# Data Module Review

Review persistence changes for correctness, migration safety, concurrency, and long-term maintainability. Focus on data invariants before style.

## Workflow

1. Identify owned tables/entities and the services allowed to mutate them.
2. Read schema, migrations, data access code, and tests together.
3. Check invariants:
   - uniqueness and idempotency
   - quota/accounting correctness
   - subscription lifecycle correctness
   - foreign keys and cascade behavior
   - required indexes for lookup paths
   - nullable fields and default values
4. Check concurrency:
   - race-prone read-then-write sequences
   - missing transactions
   - duplicate webhook/event processing
   - quota reservation and release behavior
5. Check migration safety:
   - destructive changes
   - backfill requirements
   - compatibility with existing data
   - rollback or forward-fix notes
6. Report findings first, ordered by severity, with file and line references.
7. Recommend minimal tests that prove the data invariant.

## Review Output

Use this shape:

```markdown
Findings
- [P1] <risk> in <file>:<line>

Open Questions
- ...

Suggested Tests
- ...
```

## Bundled Resources

- Use `scripts/scan_data_risks.py` for a quick local scan of common persistence risk signals.
- Use `references/demo-prompts.md` for interview-safe demo prompts.

## Common Mistakes

- Reviewing schema without the service code that mutates it.
- Treating idempotency as optional for webhooks and queue consumers.
- Missing unique constraints that the application assumes.
- Adding nullable columns without deciding how old rows should behave.
