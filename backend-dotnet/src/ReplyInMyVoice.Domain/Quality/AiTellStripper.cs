using System.Text.RegularExpressions;

namespace ReplyInMyVoice.Domain.Quality;

// Strips two well-known AI-text tells the rewrite layer keeps introducing on its own, WITHOUT touching
// the same forms when the original draft already uses them (respects the user's actual style):
//   1. em-dashes / en-dashes that were NOT in the original — replaced by ", " (commas).
//      em-dash (— / –) is the strongest single AI tell; GPT models love them, real people in business
//      emails almost never type them by hand.
//   2. contractions the rewrite introduced where the original used the expanded form
//      (it's <- it is, don't <- do not, can't <- cannot, ...). The reverse is left alone:
//      a contraction already in the original is preserved.
//
// Deterministic, pure, no LLM, no I/O. Runs as the very last step of the loop transform; safe to apply
// to any English text.
public static class AiTellStripper
{
    private static readonly Regex EmDashWithSurroundingSpaceRegex = new(@"\s*[—–]\s*", RegexOptions.Compiled);
    private static readonly Regex EmOrEnDashRegex = new(@"[—–]", RegexOptions.Compiled);

    // (contracted, expanded). Expanded form is what we sub in when the rewrite introduced a contraction
    // that wasn't in the original.
    private static readonly (string Contracted, string Expanded)[] ContractionPairs =
    {
        ("it's", "it is"),
        ("that's", "that is"),
        ("there's", "there is"),
        ("here's", "here is"),
        ("what's", "what is"),
        ("let's", "let us"),
        ("don't", "do not"),
        ("doesn't", "does not"),
        ("didn't", "did not"),
        ("can't", "cannot"),
        ("won't", "will not"),
        ("isn't", "is not"),
        ("aren't", "are not"),
        ("wasn't", "was not"),
        ("weren't", "were not"),
        ("hasn't", "has not"),
        ("haven't", "have not"),
        ("hadn't", "had not"),
        ("wouldn't", "would not"),
        ("shouldn't", "should not"),
        ("couldn't", "could not"),
        ("mustn't", "must not"),
        ("I'm", "I am"),
        ("you're", "you are"),
        ("we're", "we are"),
        ("they're", "they are"),
        ("I'll", "I will"),
        ("you'll", "you will"),
        ("we'll", "we will"),
        ("they'll", "they will"),
        ("it'll", "it will"),
        ("that'll", "that will"),
        ("would've", "would have"),
        ("could've", "could have"),
        ("should've", "should have"),
        ("I'd", "I would"),
        ("you'd", "you would"),
        ("we'd", "we would"),
        ("they'd", "they would"),
        ("I've", "I have"),
        ("you've", "you have"),
        ("we've", "we have"),
        ("they've", "they have"),
    };

    public static string Strip(string candidate, string? originalDraft)
    {
        if (string.IsNullOrEmpty(candidate))
        {
            return candidate;
        }

        // Normalize curly/typographic quotes to straight ASCII apostrophes BEFORE matching — LLMs and
        // back-translation often emit U+2019 ('), which a literal ' regex would miss (this was the
        // case-005 bug where "I've" / "that'll" with curly apostrophes slipped past the stripper).
        var result = NormalizeQuotes(candidate);
        var original = NormalizeQuotes(originalDraft ?? string.Empty);

        // 1) Em / en dashes — strip if the original had NONE (don't touch user's own dash style).
        if (!EmOrEnDashRegex.IsMatch(original))
        {
            result = EmDashWithSurroundingSpaceRegex.Replace(result, ", ");
        }

        // 2) Contractions — expand any introduced by the rewrite that the original didn't use.
        var originalLower = original.ToLowerInvariant();
        foreach (var (contracted, expanded) in ContractionPairs)
        {
            // If original already used this contraction, leave the rewrite alone.
            if (Regex.IsMatch(originalLower, @"\b" + Regex.Escape(contracted.ToLowerInvariant()) + @"\b"))
            {
                continue;
            }

            // Replace contracted with expanded; preserve leading-letter capitalization.
            result = Regex.Replace(
                result,
                @"\b" + Regex.Escape(contracted) + @"\b",
                m => char.IsUpper(m.Value[0])
                    ? char.ToUpperInvariant(expanded[0]) + expanded[1..]
                    : expanded,
                RegexOptions.IgnoreCase);
        }

        return result;
    }

    // Maps typographic single quotes (U+2018, U+2019) and double quotes (U+201C, U+201D) to ASCII so
    // contraction regexes that contain ' / " actually match. Idempotent.
    private static string NormalizeQuotes(string value) =>
        value.Replace('‘', '\'').Replace('’', '\'')
             .Replace('“', '"').Replace('”', '"');
}
