# Migration Discipline

The Azure workflow applies EF Core migrations to Azure SQL after the local SQL Server migration gate passes. A destructive schema operation that reaches that path can remove or reshape live data, so schema changes must use an expand and contract workflow.

## Workflow

Use three separate steps when removing or replacing a column or table:

1. Expand: add the new nullable column, table, or index and ship code that can read or write both shapes.
2. Migrate: backfill or copy data until the new shape is complete and verified.
3. Contract: in a later PR, remove or rename the old shape only after no deployed code reads it.

Contract migrations must include a single line with a non-empty reason:

```csharp
// MIGRATION-RISK-ACCEPTED: <reason>
```

The reason should state why the old data shape is safe to remove or rename, such as the later contract phase, verified backfill, backup status, and the deployed version that stopped reading it.

## What The Guard Flags

The CI guard scans only the `Up()` body of newly added migration `.cs` files in the pull request or push diff. It flags these operations unless the migration carries the accepted-risk marker:

- `DropTable`
- `DropColumn`
- `RenameColumn`
- `RenameTable`
- `AlterColumn` when the CLR type changes
- `AlterColumn` when the store type changes or cannot be compared from both old and new values
- `AlterColumn` when `maxLength` is narrower than `oldMaxLength`
- `AlterColumn` when `nullable: false` changes an old nullable column to not-null

## What It Ignores

The guard deliberately ignores:

- `Down()` bodies, because rollback code commonly removes objects created in `Up()`
- `*.Designer.cs` files and `AppDbContextModelSnapshot.cs`
- `DropIndex`, because index removal is performance-scoped rather than data-removal scoped
- modified existing migrations, because the workflow guard is diff-scoped to newly added migration files
- raw SQL strings; reviewers must still inspect SQL statements for risky DDL

Editing an already-applied migration remains an anti-pattern even when this guard does not flag it. Prefer a new forward migration.

## Local Usage

Run the guard directly before opening a PR:

```bash
dotnet run --project backend-dotnet/tools/ReplyInMyVoice.MigrationGuard -- backend-dotnet/src/ReplyInMyVoice.Infrastructure/Migrations/*.cs
```

No arguments is a clean no-op. Designer files and the model snapshot are skipped by the tool.
