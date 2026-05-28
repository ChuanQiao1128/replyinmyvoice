using FluentAssertions;
using ReplyInMyVoice.Domain.Quality;
using ReplyInMyVoice.Domain.RewriteEngine;

namespace ReplyInMyVoice.Tests;

// Locks in the 3 downstream-cleanup rules that the v1 frozen prompt deliberately doesn't try to
// solve (cheaper + safer to clean in code than to chase prompt regressions on the things that
// already work). See ClaimLedgerExtractor.cs header for the validation evidence.
public class ClaimLedgerJsonParserTests
{
    [Fact]
    public void Parses_a_single_well_formed_claim()
    {
        const string draft = "Hi Mark, the planter ships Monday.";
        const string json = """
            {"claims":[
              {"id":"C001","source_span":"the planter ships Monday","subject":"the planter",
               "action":"ships","object":null,"modality":"certainty","polarity":"positive",
               "time_scope":"Monday","condition":null,"must_preserve":["planter","Monday"]}
            ]}
            """;

        var ledger = ClaimLedgerJsonParser.Parse(json, draft);

        ledger.Claims.Should().HaveCount(1);
        var c = ledger.Claims[0];
        c.Id.Should().Be("C001");
        c.SourceSpan.Should().Be("the planter ships Monday");
        c.Subject.Should().Be("the planter");
        c.Action.Should().Be("ships");
        c.Object.Should().BeNull();
        c.Modality.Should().Be(RewriteClaimModality.Certainty);
        c.Polarity.Should().Be(RewriteClaimPolarity.Positive);
        c.TimeScope.Should().Be("Monday");
        c.Condition.Should().BeNull();
        c.MustPreserve.Should().Equal("planter", "Monday");
    }

    [Fact]
    public void Cleanup_rule_1_deduplicates_claims_by_source_span()
    {
        // Real bug from /tmp/claim_ledger_validation_v2/rewrite-draft-014.json:
        // compound `or` was split into two claims with identical source_spans.
        const string draft = "I cannot backdate the downgrade or refund the full June charge from this ticket.";
        const string json = """
            {"claims":[
              {"id":"C007","source_span":"I cannot backdate the downgrade or refund the full June charge from this ticket.",
               "subject":"I","action":"cannot backdate","object":"the downgrade","modality":"prohibition","polarity":"negative"},
              {"id":"C008","source_span":"I cannot backdate the downgrade or refund the full June charge from this ticket.",
               "subject":"I","action":"cannot refund","object":"the full June charge","modality":"prohibition","polarity":"negative"}
            ]}
            """;

        var ledger = ClaimLedgerJsonParser.Parse(json, draft);

        ledger.Claims.Should().HaveCount(1);
        ledger.Claims[0].Id.Should().Be("C007");
    }

    [Fact]
    public void Cleanup_rule_2_drops_claims_whose_source_span_is_not_in_the_draft()
    {
        // Hallucinated / paraphrased source_span — the post-check would never trust this claim
        // anyway, so drop it at parse time to keep the ledger tight.
        const string draft = "Hi Alina, thank you for meeting with the product team on May 10.";
        const string json = """
            {"claims":[
              {"id":"C001","source_span":"Alina met with the product team on May 10",
               "subject":"Alina","action":"met with","object":"the product team","modality":"certainty","polarity":"positive"},
              {"id":"C002","source_span":"thank you for meeting with the product team on May 10",
               "subject":"you","action":"met","object":"the product team","modality":"certainty","polarity":"positive"}
            ]}
            """;

        var ledger = ClaimLedgerJsonParser.Parse(json, draft);

        ledger.Claims.Should().HaveCount(1);
        ledger.Claims[0].Id.Should().Be("C002");
        ledger.Claims[0].SourceSpan.Should().Contain("thank you for meeting");
    }

    [Fact]
    public void Cleanup_rule_2_tolerates_whitespace_differences()
    {
        // Corpus drafts have soft line wraps; the LLM normalizes them to single spaces in
        // source_span. Naive Contains() would fail; the normalized check passes.
        const string draftWithWrap = "I checked the log on\nMarch 28 for the April 9 trip.";
        const string json = """
            {"claims":[
              {"id":"C001","source_span":"I checked the log on March 28 for the April 9 trip.",
               "subject":"I","action":"checked","object":"the log","modality":"certainty","polarity":"positive"}
            ]}
            """;

        var ledger = ClaimLedgerJsonParser.Parse(json, draftWithWrap);

        ledger.Claims.Should().HaveCount(1);
    }

    [Fact]
    public void Cleanup_rule_3_drops_empathy_openings()
    {
        // Real bug from /tmp/claim_ledger_validation_v2/rewrite-draft-001.json C011:
        // "I know this is a little frustrating because you remember signing the original form"
        // is an acknowledgment, not a substantive claim about the world.
        const string draft = "I know this is a little frustrating because you remember signing the original form, but I need the record to be complete before the trip.";
        const string json = """
            {"claims":[
              {"id":"C011","source_span":"I know this is a little frustrating because you remember signing the original form",
               "subject":"I","action":"know","object":"this is frustrating","modality":"uncertainty","polarity":"positive"},
              {"id":"C012","source_span":"I need the record to be complete before the trip.",
               "subject":"I","action":"need","object":"the record to be complete","modality":"requirement","polarity":"positive"}
            ]}
            """;

        var ledger = ClaimLedgerJsonParser.Parse(json, draft);

        ledger.Claims.Should().HaveCount(1);
        ledger.Claims[0].Id.Should().Be("C012");
    }

    [Fact]
    public void Empathy_filter_does_not_strip_substantive_claims_containing_know()
    {
        // Conservative pattern: only OPENS-with cues like "I know …", "I understand …" are
        // stripped. A real claim that mentions knowledge mid-sentence must survive.
        const string draft = "The customer wanted to know whether the refund had cleared.";
        const string json = """
            {"claims":[
              {"id":"C001","source_span":"The customer wanted to know whether the refund had cleared.",
               "subject":"the customer","action":"wanted to know","object":"whether the refund had cleared",
               "modality":"certainty","polarity":"positive"}
            ]}
            """;

        var ledger = ClaimLedgerJsonParser.Parse(json, draft);

        ledger.Claims.Should().HaveCount(1);
    }

    [Theory]
    [InlineData("certainty", RewriteClaimModality.Certainty)]
    [InlineData("uncertainty", RewriteClaimModality.Uncertainty)]
    [InlineData("requirement", RewriteClaimModality.Requirement)]
    [InlineData("permission", RewriteClaimModality.Permission)]
    [InlineData("capability", RewriteClaimModality.Capability)]
    [InlineData("prohibition", RewriteClaimModality.Prohibition)]
    [InlineData("offer", RewriteClaimModality.Offer)]
    [InlineData("CERTAINTY", RewriteClaimModality.Certainty)]   // case-insensitive
    [InlineData("garbage", RewriteClaimModality.Certainty)]      // unknown → safe default
    [InlineData(null, RewriteClaimModality.Certainty)]
    public void Parses_all_modality_values_case_insensitively(string? raw, RewriteClaimModality expected)
    {
        const string draft = "X is Y.";
        var json = $$"""
            {"claims":[
              {"id":"C001","source_span":"X is Y.","subject":"X","action":"is","object":"Y",
               "modality":{{(raw is null ? "null" : "\"" + raw + "\"")}},"polarity":"positive"}
            ]}
            """;

        var ledger = ClaimLedgerJsonParser.Parse(json, draft);

        ledger.Claims.Should().HaveCount(1);
        ledger.Claims[0].Modality.Should().Be(expected);
    }

    [Theory]
    [InlineData("positive", RewriteClaimPolarity.Positive)]
    [InlineData("negative", RewriteClaimPolarity.Negative)]
    [InlineData("Negative", RewriteClaimPolarity.Negative)]
    [InlineData("anything-else", RewriteClaimPolarity.Positive)]
    [InlineData(null, RewriteClaimPolarity.Positive)]
    public void Parses_polarity_with_safe_default(string? raw, RewriteClaimPolarity expected)
    {
        const string draft = "X.";
        var json = $$"""
            {"claims":[
              {"id":"C001","source_span":"X.","subject":"X","action":"is","object":null,
               "modality":"certainty","polarity":{{(raw is null ? "null" : "\"" + raw + "\"")}}}
            ]}
            """;

        var ledger = ClaimLedgerJsonParser.Parse(json, draft);

        ledger.Claims[0].Polarity.Should().Be(expected);
    }

    [Fact]
    public void Treats_empty_strings_as_null_for_optional_fields()
    {
        const string draft = "Done.";
        const string json = """
            {"claims":[
              {"id":"C001","source_span":"Done.","subject":"task","action":"is done",
               "object":"","modality":"certainty","polarity":"positive",
               "time_scope":"","condition":"   ","must_preserve":["", "real"]}
            ]}
            """;

        var ledger = ClaimLedgerJsonParser.Parse(json, draft);

        var c = ledger.Claims[0];
        c.Object.Should().BeNull();
        c.TimeScope.Should().BeNull();
        c.Condition.Should().BeNull();
        c.MustPreserve.Should().Equal("real");
    }

    [Fact]
    public void Assigns_a_sequential_id_when_the_extractor_omits_one()
    {
        const string draft = "X. Y.";
        const string json = """
            {"claims":[
              {"source_span":"X.","subject":"x","action":"is","modality":"certainty","polarity":"positive"},
              {"source_span":"Y.","subject":"y","action":"is","modality":"certainty","polarity":"positive"}
            ]}
            """;

        var ledger = ClaimLedgerJsonParser.Parse(json, draft);

        ledger.Claims.Select(c => c.Id).Should().Equal("C001", "C002");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json at all")]
    [InlineData("{\"unrelated\":42}")]
    public void Returns_empty_ledger_on_bad_or_unrelated_json(string? json)
    {
        var ledger = ClaimLedgerJsonParser.Parse(json!, "anything");
        ledger.Claims.Should().BeEmpty();
    }

    [Fact]
    public void Empty_claims_array_yields_empty_ledger()
    {
        var ledger = ClaimLedgerJsonParser.Parse("{\"claims\":[]}", "anything");
        ledger.Claims.Should().BeEmpty();
    }

    [Fact]
    public void Skips_claims_with_blank_source_span()
    {
        const string json = """
            {"claims":[
              {"id":"C001","source_span":"","subject":"x","action":"is","modality":"certainty","polarity":"positive"},
              {"id":"C002","source_span":"   ","subject":"x","action":"is","modality":"certainty","polarity":"positive"},
              {"id":"C003","source_span":"real","subject":"x","action":"is","modality":"certainty","polarity":"positive"}
            ]}
            """;

        var ledger = ClaimLedgerJsonParser.Parse(json, "real text here");

        ledger.Claims.Should().HaveCount(1);
        ledger.Claims[0].Id.Should().Be("C003");
    }
}
