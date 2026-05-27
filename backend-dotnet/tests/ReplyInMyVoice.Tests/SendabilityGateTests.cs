using FluentAssertions;
using ReplyInMyVoice.Domain.Quality;

namespace ReplyInMyVoice.Tests;

// Deterministic structural sendability: mechanical defects (sentinel/placeholder residue, garble,
// empty) MUST be Unsendable; clean professional email MUST be Sendable with no false positives.
public class SendabilityGateTests
{
    [Theory]
    [InlineData("Hi Mark, your order ships Monday. The sentinel [[A0]] slipped through. Thanks, Dana")]
    [InlineData("Hi, your refund of QZAN001QZ is processed. Best,")]
    public void Unsendable_on_leftover_sentinels(string text)
    {
        var result = SendabilityGate.Check(text);
        result.Passed.Should().BeFalse();
        result.Tier.Should().Be(SendabilityTier.Unsendable);
        result.Issues.Should().Contain(i => i.Kind == "sentinel_residue");
    }

    [Fact]
    public void Unsendable_on_unfilled_template_slot()
    {
        var result = SendabilityGate.Check("Hi, I can't make {date1}, but {date2} works. Thanks, Dana");
        result.Tier.Should().Be(SendabilityTier.Unsendable);
        result.Issues.Should().Contain(i => i.Kind == "unfilled_slot");
    }

    [Theory]
    [InlineData("Hi [Name], your invoice is attached. Best, Dana")]
    [InlineData("Hi Mark, the order ships Monday. Best, [Your Name]")]
    public void Unsendable_on_bracketed_placeholder(string text)
    {
        SendabilityGate.Check(text).Tier.Should().Be(SendabilityTier.Unsendable);
    }

    [Fact]
    public void Unsendable_on_leftover_cjk()
    {
        var result = SendabilityGate.Check("Hi Mark, your order 请查收 ships Monday. Thanks, Dana");
        result.Tier.Should().Be(SendabilityTier.Unsendable);
        result.Issues.Should().Contain(i => i.Kind == "cjk_leak");
    }

    [Fact]
    public void Unsendable_on_mojibake()
    {
        SendabilityGate.Check("Hi Mark, your order ships Monday ��. Thanks, Dana")
            .Tier.Should().Be(SendabilityTier.Unsendable);
    }

    [Fact]
    public void Unsendable_on_repeated_token_run()
    {
        SendabilityGate.Check("Hi Mark, the the the order ships Monday. Thanks, Dana")
            .Tier.Should().Be(SendabilityTier.Unsendable);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("Ok thanks")]
    public void Unsendable_on_empty_or_too_short(string text)
    {
        SendabilityGate.Check(text).Tier.Should().Be(SendabilityTier.Unsendable);
    }

    [Theory]
    [InlineData("Hi Mark,\n\nI can't make Tuesday — I've got a conflict. Wednesday afternoon works for me though. Does that work for you?\n\nThanks,\nDana")]
    [InlineData("Hi,\n\nFollowing up on invoice INV-204 for $1,250.00, due June 10. We haven't received payment yet. Could you confirm whether it was paid under a different reference?\n\nBest,")] // bare "Best," with no name is fine
    [InlineData("Thanks for flagging this. See note [1] in the attached summary; the figure is correct. Best, Dana")]      // citation-style [1] is not a placeholder
    public void Sendable_on_clean_professional_email(string text)
    {
        var result = SendabilityGate.Check(text);
        result.Passed.Should().BeTrue();
        result.Tier.Should().Be(SendabilityTier.Sendable);
        result.Issues.Should().BeEmpty();
    }

    [Theory]
    [InlineData("sendable", SendabilityTier.Sendable)]
    [InlineData("minor", SendabilityTier.Minor)]
    [InlineData("unsendable", SendabilityTier.Unsendable)]
    [InlineData("garbage-value", SendabilityTier.Unsendable)] // unrecognized -> fail closed
    public void ParseTier_maps_strings_and_fails_closed(string tier, SendabilityTier expected)
    {
        SendabilityGate.ParseTier(tier).Should().Be(expected);
    }
}
