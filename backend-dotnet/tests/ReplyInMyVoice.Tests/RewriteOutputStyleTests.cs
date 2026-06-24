using FluentAssertions;
using ReplyInMyVoice.Infrastructure.Providers;

namespace ReplyInMyVoice.Tests;

public class RewriteOutputStyleTests
{
    [Theory]
    [InlineData("Hi Mara — good news, you're set.", "Hi Mara, good news, you're set.")]
    [InlineData("the deadline moved—Wednesday now.", "the deadline moved, Wednesday now.")]
    [InlineData("All done. — Next, the refund.", "All done. Next, the refund.")]
    [InlineData("Refund is $89 — paid back to your card.", "Refund is $89, paid back to your card.")]
    public void Apply_replaces_em_dashes_with_plain_punctuation(string input, string expected)
    {
        RewriteOutputStyle.Apply(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("Setup takes 5–7 business days.")] // en-dash numeric range is kept
    [InlineData("You're inside the 14-day window.")] // ordinary hyphen is kept
    [InlineData("No long dashes in this sentence.")] // nothing to normalize
    public void Apply_leaves_ranges_hyphens_and_clean_text_untouched(string input)
    {
        RewriteOutputStyle.Apply(input).Should().Be(input);
    }

    [Fact]
    public void Apply_strips_every_em_dash_while_preserving_names_and_numbers()
    {
        var input = "Hi Priya — the $160,000 to $185,000 range works — let's talk.";

        var result = RewriteOutputStyle.Apply(input);

        result.Should().NotContain("—");
        result.Should().Contain("Priya");
        result.Should().Contain("$160,000 to $185,000");
    }
}
