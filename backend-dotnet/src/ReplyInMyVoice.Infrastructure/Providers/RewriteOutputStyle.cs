using System.Text.RegularExpressions;

namespace ReplyInMyVoice.Infrastructure.Providers;

/// <summary>
/// Output-style normalizer applied to every generated reply before it is scored
/// and returned. Em dashes (U+2014) and the rarer horizontal bar (U+2015) read as
/// an AI-writing tell, so each is replaced with plain punctuation. This is
/// punctuation-only: it never alters letters, names, numbers, amounts, dates, or
/// identifiers, so it cannot change a preserved fact. En dashes in ranges
/// ("5–7 days") and ordinary hyphens ("14-day") are deliberately left untouched.
/// </summary>
internal static partial class RewriteOutputStyle
{
    public static string Apply(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        // Each em-dash run becomes a comma so the adjacent clauses stay joined.
        var result = EmDashRun().Replace(text, ", ");
        // If the dash followed sentence-ending punctuation ("Done. — Next"), drop
        // the comma we just inserted so it reads as two sentences ("Done. Next").
        result = PunctuationThenComma().Replace(result, "$1$2");
        // Collapse an accidental ", ," when a dash sat next to an existing comma.
        result = DoubleComma().Replace(result, ", ");
        return result;
    }

    [GeneratedRegex(@"[ \t]*[—―]+[ \t]*")]
    private static partial Regex EmDashRun();

    [GeneratedRegex(@"([.!?;:]),(\s)")]
    private static partial Regex PunctuationThenComma();

    [GeneratedRegex(@",[ \t]*,")]
    private static partial Regex DoubleComma();
}
