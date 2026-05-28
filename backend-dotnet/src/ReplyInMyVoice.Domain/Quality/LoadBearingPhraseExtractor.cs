using System.Text.RegularExpressions;
using ReplyInMyVoice.Domain.RewriteEngine;

namespace ReplyInMyVoice.Domain.Quality;

// Extracts LOAD-BEARING phrases — a temporal/relational verb-or-preposition + a date fact, as a single
// unit. Examples: "expires June 7", "expires on June 7", "reply by June 7", "due June 10", "valid through
// June 7", "no later than May 28", "before May 28".
//
// Why: token-only masking (just "June 7") protects the date but leaves the verb to be translated, which is
// exactly where the loop's drift kept landing — "expires June 7" -> "is very good through June 7" (loses
// expiry), "reply by June 7" -> "let me know via June 7" (wrong preposition). Masking the WHOLE phrase as
// a unit (the verb + the date) lets it ride through Youdao verbatim, so the meaning is preserved AND the
// deterministic gate can require its verbatim presence in the output.
//
// Strictly date-anchored (ledger Date facts), so it never fires on innocuous "by the rules" / "before the
// meeting" — no false positives on non-date contexts.
public static class LoadBearingPhraseExtractor
{
    private const string TemporalVerb =
        @"(?:expires?(?:\s+on)?" +
        @"|valid\s+(?:through|until|to)" +
        @"|due(?:\s+(?:on|by))?" +
        @"|(?:reply|respond|confirm|get\s+back(?:\s+to\s+(?:me|us))?|let\s+me\s+know)\s+by" +
        @"|no\s+later\s+than" +
        @"|until|before|after|by)";

    public static IReadOnlyList<string> Extract(string draft, RewriteFactLedger factLedger)
    {
        if (string.IsNullOrWhiteSpace(draft) || factLedger.Facts.Count == 0)
        {
            return Array.Empty<string>();
        }

        var phrases = new HashSet<string>(StringComparer.Ordinal);
        foreach (var fact in factLedger.Facts.Where(f => f.Category == RewriteFactCategory.DateOrDeadline))
        {
            var date = fact.Text.Trim();
            if (date.Length < 3)
            {
                continue;
            }

            var pattern = @"\b" + TemporalVerb + @"\s+(?:on\s+)?" + Regex.Escape(date) + @"\b";
            foreach (Match match in Regex.Matches(draft, pattern, RegexOptions.IgnoreCase))
            {
                var phrase = match.Value.Trim();
                if (phrase.Length > 0)
                {
                    phrases.Add(phrase);
                }
            }
        }

        return phrases.ToList();
    }
}
