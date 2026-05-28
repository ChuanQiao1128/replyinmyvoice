using FluentAssertions;
using ReplyInMyVoice.Domain.Quality;
using ReplyInMyVoice.Domain.RewriteEngine;

namespace ReplyInMyVoice.Tests;

public class LoadBearingPhraseExtractorTests
{
    private static RewriteFactLedger DateLedger(params string[] dates) =>
        new(dates.Select((d, i) => new RewriteFact(
            $"id_{i}", d, "roughDraftReply", RewriteFactImportance.Critical,
            RewriteFactCategory.DateOrDeadline, true, d)).ToArray());

    [Theory]
    [InlineData("The quote expires on June 7. Please review.", "June 7", "expires on June 7")]
    [InlineData("This quote expires June 7.", "June 7", "expires June 7")]
    [InlineData("Please reply by June 7 if it still works.", "June 7", "reply by June 7")]
    [InlineData("Could you confirm by June 10? Thanks.", "June 10", "confirm by June 10")]
    [InlineData("Let me know by June 7 either way.", "June 7", "let me know by June 7")]
    [InlineData("Invoice is due June 10.", "June 10", "due June 10")]
    [InlineData("Invoice is due on June 10.", "June 10", "due on June 10")]
    [InlineData("Valid through June 7 only.", "June 7", "valid through June 7")]
    [InlineData("Send it no later than May 28 please.", "May 28", "no later than May 28")]
    [InlineData("Please ship before June 7.", "June 7", "before June 7")]
    public void Extracts_temporal_load_bearing_phrase(string draft, string date, string expectedPhrase)
    {
        var phrases = LoadBearingPhraseExtractor.Extract(draft, DateLedger(date));
        phrases.Should().Contain(p => string.Equals(p, expectedPhrase, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Does_not_fire_on_innocuous_date_context()
    {
        // "The meeting on June 7" — "on" alone is not in the verb set; no temporal load-bearer.
        var phrases = LoadBearingPhraseExtractor.Extract(
            "The meeting on June 7 went well; we'll talk again soon.",
            DateLedger("June 7"));

        phrases.Should().BeEmpty();
    }

    [Fact]
    public void Empty_ledger_or_draft_yields_empty()
    {
        LoadBearingPhraseExtractor.Extract(string.Empty, DateLedger("June 7")).Should().BeEmpty();
        LoadBearingPhraseExtractor.Extract("anything", new RewriteFactLedger(Array.Empty<RewriteFact>())).Should().BeEmpty();
    }

    private static RewriteFactLedger AmountLedger(params string[] amounts) =>
        new(amounts.Select((a, i) => new RewriteFact(
            $"id_a{i}", a, "roughDraftReply", RewriteFactImportance.Critical,
            RewriteFactCategory.Amount, false, a)).ToArray());

    private static RewriteFactLedger CountLedger(params string[] counts) =>
        new(counts.Select((c, i) => new RewriteFact(
            $"id_c{i}", c, "roughDraftReply", RewriteFactImportance.Critical,
            RewriteFactCategory.Count, false, c)).ToArray());

    [Theory]
    [InlineData("We'll process a refund of $40 to your card.", "$40", "refund of $40")]
    [InlineData("You owe $1,250.00 as of today.", "$1,250.00", "owe $1,250.00")]
    [InlineData("The invoice is due — invoice for $1,250.00 was sent on May 28.", "$1,250.00", "invoice for $1,250.00")]
    [InlineData("You were charged $40 monthly until now.", "$40", "charged $40 monthly")]
    [InlineData("The plan is at $42 per seat per month, including support.", "$42", "$42 per seat per month")]
    [InlineData("It's $42 per user.", "$42", "$42 per user")]
    public void Extracts_amount_load_bearing_phrase(string draft, string amount, string expectedPhrase)
    {
        var phrases = LoadBearingPhraseExtractor.Extract(draft, AmountLedger(amount));
        phrases.Should().Contain(p => string.Equals(p, expectedPhrase, StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("The quote is for 18 seats at $42 per seat per month.", "18", "18 seats")]
    [InlineData("Send 5 copies to the office.", "5", "5 copies")]
    [InlineData("It expires in 3 days.", "3", "3 days")]
    [InlineData("We have 12 attendees confirmed.", "12", "12 attendees")]
    public void Extracts_count_unit_load_bearing_phrase(string draft, string count, string expectedPhrase)
    {
        var phrases = LoadBearingPhraseExtractor.Extract(draft, CountLedger(count));
        phrases.Should().Contain(p => string.Equals(p, expectedPhrase, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Count_without_unit_noun_yields_nothing()
    {
        // "Let's meet at 18" — no unit noun next to 18, so no load-bearing phrase.
        LoadBearingPhraseExtractor.Extract("Let's meet at 18 for dinner.", CountLedger("18"))
            .Should().BeEmpty();
    }

    [Fact]
    public void Hard_terms_wired_into_ProtectedTermLedger_catch_expires_drift()
    {
        const string draft = "The quote expires on June 7. Please reply by June 7 to confirm.";
        var ledger = new RewriteFactLedger(new[]
        {
            new RewriteFact("d1", "June 7", "roughDraftReply", RewriteFactImportance.Critical,
                RewriteFactCategory.DateOrDeadline, true, "June 7"),
        });
        var loadBearing = LoadBearingPhraseExtractor.Extract(draft, ledger);

        var protectedLedger = ProtectedTermLedgerExtractor.Build(
            draft, ledger, proposedSpans: Array.Empty<string>(), loadBearingSpans: loadBearing);

        // "expires on June 7" is a hard ExactRequired term -> gate flags a rewrite that drifts it.
        protectedLedger.Terms.Should().Contain(t => t.Text.Contains("expires on June 7") && t.ExactRequired);
        ProtectedTermGate.Check(
            "This quotation is very good through June 7. Please let me know via June 7.",
            protectedLedger).Passed.Should().BeFalse();

        // A faithful rewrite that keeps both phrases verbatim passes.
        ProtectedTermGate.Check(
            "The offer expires on June 7. Kindly reply by June 7 to confirm.",
            protectedLedger).Passed.Should().BeTrue();
    }
}
