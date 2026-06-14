using System.Text;
using System.Text.RegularExpressions;

namespace ReplyInMyVoice.MigrationGuard;

public sealed record MigrationFinding(string Operation, int LineNumber, string Reason);

public sealed record MigrationScanResult(
    string FilePath,
    IReadOnlyList<MigrationFinding> Findings,
    bool HasRiskMarker,
    string? MarkerReason)
{
    public bool IsViolation => Findings.Count > 0 && !HasRiskMarker;
}

public static class MigrationDisciplineScanner
{
    private static readonly Regex RiskMarkerRegex = new(
        @"^\s*//\s*MIGRATION-RISK-ACCEPTED:[^\S\r\n]*(?<reason>\S[^\r\n]*)$",
        RegexOptions.CultureInvariant | RegexOptions.Multiline);

    private static readonly Regex UpMethodRegex = new(
        @"protected\s+override\s+void\s+Up\s*\(",
        RegexOptions.CultureInvariant);

    private static readonly Regex DownMethodRegex = new(
        @"protected\s+override\s+void\s+Down\s*\(",
        RegexOptions.CultureInvariant);

    private static readonly Regex DirectOperationRegex = new(
        @"migrationBuilder\s*\.\s*(?<op>DropTable|DropColumn|RenameColumn|RenameTable)\s*\(",
        RegexOptions.CultureInvariant);

    private static readonly Regex AlterColumnRegex = new(
        @"migrationBuilder\s*\.\s*AlterColumn\s*(?:<(?<clr>[^>]+)>)?\s*\(",
        RegexOptions.CultureInvariant);

    public static MigrationScanResult ScanFile(string filePath, string content)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(content);

        var markerMatch = RiskMarkerRegex.Match(content);
        var markerReason = markerMatch.Success
            ? markerMatch.Groups["reason"].Value.Trim()
            : null;
        var hasRiskMarker = !string.IsNullOrWhiteSpace(markerReason);

        var scanRegion = ExtractUpRegion(content);
        var strippedRegion = StripLineComments(scanRegion.Content);
        var findings = new List<MigrationFinding>();

        foreach (Match match in DirectOperationRegex.Matches(strippedRegion))
        {
            findings.Add(new MigrationFinding(
                match.Groups["op"].Value,
                GetLineNumber(strippedRegion, match.Index, scanRegion.StartLineNumber),
                "destructive operation in Up()"));
        }

        foreach (Match match in AlterColumnRegex.Matches(strippedRegion))
        {
            var openParenthesisIndex = strippedRegion.IndexOf('(', match.Index);
            if (openParenthesisIndex < 0)
            {
                continue;
            }

            var argumentBlock = ExtractArgumentBlock(strippedRegion, openParenthesisIndex);
            if (argumentBlock is null)
            {
                continue;
            }

            var reason = FindAlterColumnReason(match.Groups["clr"].Value, argumentBlock);
            if (reason is null)
            {
                continue;
            }

            findings.Add(new MigrationFinding(
                "AlterColumn",
                GetLineNumber(strippedRegion, match.Index, scanRegion.StartLineNumber),
                reason));
        }

        return new MigrationScanResult(filePath, findings, hasRiskMarker, markerReason);
    }

    private static ScanRegion ExtractUpRegion(string content)
    {
        var upMatch = UpMethodRegex.Match(content);
        if (!upMatch.Success)
        {
            return new ScanRegion(content, 1);
        }

        var startIndex = upMatch.Index + upMatch.Length;
        var downMatch = DownMethodRegex.Match(content, startIndex);
        var endIndex = downMatch.Success ? downMatch.Index : content.Length;

        return new ScanRegion(
            content[startIndex..endIndex],
            GetLineNumber(content, startIndex, 1));
    }

    private static string StripLineComments(string content)
    {
        var builder = new StringBuilder(content.Length);

        for (var lineStart = 0; lineStart < content.Length;)
        {
            var nextLineIndex = content.IndexOf('\n', lineStart);
            var lineEnd = nextLineIndex >= 0 ? nextLineIndex : content.Length;
            var line = content.AsSpan(lineStart, lineEnd - lineStart);
            var commentIndex = FindLineCommentStart(line);

            if (commentIndex >= 0)
            {
                builder.Append(line[..commentIndex]);
            }
            else
            {
                builder.Append(line);
            }

            if (nextLineIndex < 0)
            {
                break;
            }

            builder.Append('\n');
            lineStart = nextLineIndex + 1;
        }

        return builder.ToString();
    }

    private static int FindLineCommentStart(ReadOnlySpan<char> line)
    {
        var inString = false;
        var escaped = false;

        for (var index = 0; index < line.Length - 1; index++)
        {
            var current = line[index];

            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (current == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (current == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (current == '"')
            {
                inString = true;
                continue;
            }

            if (current == '/' && line[index + 1] == '/')
            {
                return index;
            }
        }

        return -1;
    }

    private static string? ExtractArgumentBlock(string content, int openParenthesisIndex)
    {
        var depth = 0;
        var inString = false;
        var escaped = false;

        for (var index = openParenthesisIndex; index < content.Length; index++)
        {
            var current = content[index];

            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (current == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (current == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (current == '"')
            {
                inString = true;
                continue;
            }

            if (current == '(')
            {
                depth++;
                continue;
            }

            if (current == ')')
            {
                depth--;
                if (depth == 0)
                {
                    return content[openParenthesisIndex..(index + 1)];
                }
            }
        }

        return null;
    }

    private static string? FindAlterColumnReason(string clrType, string argumentBlock)
    {
        var oldClrType = FindNamedType(argumentBlock, @"oldClrType:\s*typeof\s*\(\s*(?<v>[^)]+?)\s*\)");
        var newClrType = string.IsNullOrWhiteSpace(clrType) ? null : clrType.Trim();
        if (newClrType is not null &&
            oldClrType is not null &&
            !string.Equals(newClrType, oldClrType, StringComparison.Ordinal))
        {
            return "CLR type change";
        }

        var storeType = FindNamedString(argumentBlock, @"(^|[\s(,])type:\s*""(?<v>[^""]*)""");
        var oldStoreType = FindNamedString(argumentBlock, @"oldType:\s*""(?<v>[^""]*)""");
        if (storeType is not null && oldStoreType is not null)
        {
            if (!string.Equals(storeType.Trim(), oldStoreType.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return "store type change";
            }
        }
        else if (storeType is not null || oldStoreType is not null)
        {
            return "store type indeterminate";
        }

        var maxLength = FindNamedInt(argumentBlock, @"(^|[\s(,])maxLength:\s*(?<v>\d+)");
        var oldMaxLength = FindNamedInt(argumentBlock, @"oldMaxLength:\s*(?<v>\d+)");
        if (maxLength is not null && oldMaxLength is not null && maxLength < oldMaxLength)
        {
            return "max length narrowed";
        }

        var nullable = FindNamedBool(argumentBlock, @"(^|[\s(,])nullable:\s*(?<v>true|false)");
        var oldNullable = FindNamedBool(argumentBlock, @"oldNullable:\s*(?<v>true|false)");
        if (nullable == false && oldNullable == true)
        {
            return "nullability tightened";
        }

        return null;
    }

    private static string? FindNamedType(string content, string pattern)
    {
        var match = Regex.Match(content, pattern, RegexOptions.CultureInvariant);
        return match.Success ? match.Groups["v"].Value.Trim() : null;
    }

    private static string? FindNamedString(string content, string pattern)
    {
        var match = Regex.Match(content, pattern, RegexOptions.CultureInvariant);
        return match.Success ? match.Groups["v"].Value : null;
    }

    private static int? FindNamedInt(string content, string pattern)
    {
        var match = Regex.Match(content, pattern, RegexOptions.CultureInvariant);
        return match.Success && int.TryParse(match.Groups["v"].Value, out var value) ? value : null;
    }

    private static bool? FindNamedBool(string content, string pattern)
    {
        var match = Regex.Match(content, pattern, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        return match.Success && bool.TryParse(match.Groups["v"].Value, out var value) ? value : null;
    }

    private static int GetLineNumber(string content, int index, int startingLineNumber)
    {
        var lineNumber = startingLineNumber;
        for (var position = 0; position < index && position < content.Length; position++)
        {
            if (content[position] == '\n')
            {
                lineNumber++;
            }
        }

        return lineNumber;
    }

    private sealed record ScanRegion(string Content, int StartLineNumber);
}
