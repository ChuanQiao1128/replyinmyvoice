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

    // Verbs/nouns paired with an AMOUNT carry contract/payment meaning that must survive translation
    // ("refund of $40", "owe $1,250.00", "charged $40 monthly", "invoice for $1,250.00").
    private const string AmountContextVerb =
        @"(?:refund(?:s|ed)?(?:\s+of)?" +
        @"|credit(?:ed)?(?:\s+of)?" +
        @"|payment(?:\s+of)?" +
        @"|charge(?:d|s)?(?:\s+(?:of|for))?" +
        @"|invoice(?:d)?(?:\s+for)?" +
        @"|fee(?:s)?(?:\s+of)?" +
        @"|balance(?:\s+of)?" +
        @"|amount(?:\s+of)?" +
        @"|owe(?:d|s)?" +
        @"|paid" +
        @"|due" +
        @"|cost(?:s)?" +
        @"|price(?:d)?(?:\s+(?:at|of))?)";

    // Per-unit / cadence suffix after an AMOUNT: "$42 per seat per month", "$42 per user", "$42 monthly".
    // This is the structure Youdao mangled into "for each seat of $42 every month" in case 005.
    private const string AmountPerUnitSuffix =
        @"(?:\s+per\s+\w+(?:\s+per\s+\w+)?|\s+(?:monthly|yearly|annually|weekly|daily))";

    // Unit nouns that, paired with a Count, carry the meaning of WHAT is being counted ("18 seats",
    // "5 days", "3 attendees"). The count-only mask lets "18" survive but loses "seats" (was the
    // "18 seat for each seat" garble in case 005).
    private const string CountUnitNoun =
        @"(?:seats?|users?|licenses?|items?|orders?|emails?|copies|tickets?|attendees?|spots?" +
        @"|days?|weeks?|months?|years?|hours?|minutes?|sessions?|meetings?|visits?)";

    public static IReadOnlyList<string> Extract(string draft, RewriteFactLedger factLedger)
    {
        if (string.IsNullOrWhiteSpace(draft) || factLedger.Facts.Count == 0)
        {
            return Array.Empty<string>();
        }

        var phrases = new HashSet<string>(StringComparer.Ordinal);

        // Temporal: <verb> + DATE
        foreach (var fact in factLedger.Facts.Where(f => f.Category == RewriteFactCategory.DateOrDeadline))
        {
            var date = fact.Text.Trim();
            if (date.Length < 3)
            {
                continue;
            }

            var pattern = @"\b" + TemporalVerb + @"\s+(?:on\s+)?" + Regex.Escape(date) + @"\b";
            AddMatches(phrases, draft, pattern);
        }

        // Amounts: <verb> + AMOUNT [+ per-unit], and bare AMOUNT + per-unit
        foreach (var fact in factLedger.Facts.Where(f => f.Category == RewriteFactCategory.Amount))
        {
            var amount = fact.Text.Trim();
            if (amount.Length < 2)
            {
                continue;
            }

            var escaped = Regex.Escape(amount);
            AddMatches(phrases, draft, @"\b" + AmountContextVerb + @"\s+" + escaped + @"(?:" + AmountPerUnitSuffix + @")?");
            AddMatches(phrases, draft, escaped + AmountPerUnitSuffix);
        }

        // Counts: NUMBER + unit-noun ("18 seats", "5 days")
        foreach (var fact in factLedger.Facts.Where(f => f.Category == RewriteFactCategory.Count))
        {
            var count = fact.Text.Trim();
            if (count.Length < 1)
            {
                continue;
            }

            AddMatches(phrases, draft, @"\b" + Regex.Escape(count) + @"\s+" + CountUnitNoun + @"\b");
        }

        return phrases.ToList();
    }

    private static void AddMatches(HashSet<string> sink, string text, string pattern)
    {
        foreach (Match match in Regex.Matches(text, pattern, RegexOptions.IgnoreCase))
        {
            var phrase = match.Value.Trim();
            if (phrase.Length > 0)
            {
                sink.Add(phrase);
            }
        }
    }
}
