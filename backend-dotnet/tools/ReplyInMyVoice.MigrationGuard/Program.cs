using ReplyInMyVoice.MigrationGuard;

if (args.Length == 0)
{
    Console.WriteLine("migration guard: no migration files supplied");
    return 0;
}

var hasViolation = false;
var hasToolError = false;

foreach (var filePath in args)
{
    var fileName = Path.GetFileName(filePath);
    if (filePath.EndsWith(".Designer.cs", StringComparison.Ordinal) ||
        string.Equals(fileName, "AppDbContextModelSnapshot.cs", StringComparison.Ordinal))
    {
        continue;
    }

    if (!File.Exists(filePath))
    {
        Console.WriteLine($"::error::migration guard: file not found {filePath}");
        hasToolError = true;
        continue;
    }

    MigrationScanResult result;
    try
    {
        result = MigrationDisciplineScanner.ScanFile(filePath, File.ReadAllText(filePath));
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
    {
        Console.WriteLine(
            $"::error file={EscapeProperty(filePath)}::migration guard: failed to read file: {EscapeMessage(exception.Message)}");
        hasToolError = true;
        continue;
    }

    if (result.Findings.Count == 0)
    {
        continue;
    }

    if (!result.IsViolation)
    {
        Console.WriteLine(
            $"::notice file={EscapeProperty(filePath)}::Migration discipline: accepted risk marker - {EscapeMessage(result.MarkerReason ?? "reason unavailable")}");
        continue;
    }

    hasViolation = true;
    foreach (var finding in result.Findings)
    {
        Console.WriteLine($"{filePath}({finding.LineNumber}): {finding.Operation}: {finding.Reason}");
        Console.WriteLine(
            $"::error file={EscapeProperty(filePath)},line={finding.LineNumber}::Migration discipline: {finding.Operation} - {EscapeMessage(finding.Reason)}. Add '// MIGRATION-RISK-ACCEPTED: <reason>' after review, see docs/migration-discipline.md");
    }
}

if (hasToolError)
{
    return 2;
}

return hasViolation ? 1 : 0;

static string EscapeProperty(string value)
{
    return value
        .Replace("%", "%25", StringComparison.Ordinal)
        .Replace("\r", "%0D", StringComparison.Ordinal)
        .Replace("\n", "%0A", StringComparison.Ordinal)
        .Replace(":", "%3A", StringComparison.Ordinal)
        .Replace(",", "%2C", StringComparison.Ordinal);
}

static string EscapeMessage(string value)
{
    return value
        .Replace("%", "%25", StringComparison.Ordinal)
        .Replace("\r", "%0D", StringComparison.Ordinal)
        .Replace("\n", "%0A", StringComparison.Ordinal);
}
