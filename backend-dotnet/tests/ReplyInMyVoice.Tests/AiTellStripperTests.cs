using FluentAssertions;
using ReplyInMyVoice.Domain.Quality;

namespace ReplyInMyVoice.Tests;

public class AiTellStripperTests
{
    [Theory]
    [InlineData("Hi Dev—thanks again.", "Hi Dev, thanks again.", "Hi Dev, thanks again.")]
    [InlineData("Hi Dev — thanks again.", "Hi Dev, thanks again.", "Hi Dev, thanks again.")]
    [InlineData("So it's easier – really.", "So it is easier, really.", "So it is easier, really.")]
    public void Strips_em_or_en_dashes_when_original_has_none(string candidate, string original, string expected)
    {
        AiTellStripper.Strip(candidate, original).Should().Be(expected);
    }

    [Fact]
    public void Preserves_em_dashes_when_original_uses_them()
    {
        // Owner uses em-dashes themselves — don't second-guess their style.
        var result = AiTellStripper.Strip(
            "Sure—happy to help. I'll attach it later.",
            "Hi—let me know what you need.");
        result.Should().Contain("—"); // em-dash preserved because original has one
    }

    [Theory]
    [InlineData("So it's easier to find.", "So it is easier to find.", "So it is easier to find.")]
    [InlineData("I'm on it.", "I am on it.", "I am on it.")]
    [InlineData("They won't ship Monday.", "They will not ship Monday.", "They will not ship Monday.")]
    [InlineData("It's done — don't worry.", "It is done. Do not worry.", "It is done, do not worry.")]
    public void Expands_introduced_contractions(string candidate, string original, string expected)
    {
        AiTellStripper.Strip(candidate, original).Should().Be(expected);
    }

    [Theory]
    [InlineData("It's done.", "It's already done.")]                  // original uses it's -> leave alone
    [InlineData("We don't ship Monday.", "We don't ship on weekends.")] // original uses don't -> leave alone
    public void Leaves_contractions_alone_when_original_already_uses_them(string candidate, string original)
    {
        // Should NOT expand because the original already has the contraction.
        AiTellStripper.Strip(candidate, original).Should().Be(candidate);
    }

    [Fact]
    public void Preserves_capitalization_at_sentence_start()
    {
        AiTellStripper.Strip("It's done.", "It is done.").Should().Be("It is done.");
        AiTellStripper.Strip("I'm on it.", "I am on it.").Should().Be("I am on it.");
    }

    [Fact]
    public void Leaves_text_with_no_tells_unchanged()
    {
        const string clean = "Hi Mark, the planter ships Monday. Thanks, Dana";
        AiTellStripper.Strip(clean, clean).Should().Be(clean);
    }

    [Fact]
    public void Real_case_005_loop_output_normalized_to_original_punctuation_and_contraction()
    {
        const string original = "Hi Dev, thanks again for the call about the Northstar rollout. I attached quote Q-7719 again so it is easier to find.";
        const string loopOutput = "Hi Dev—thanks again for the call about the Northstar rollout. I attached quote Q-7719 again so it's easier to find.";

        AiTellStripper.Strip(loopOutput, original).Should().Be(original);
    }

    [Fact]
    public void Curly_apostrophes_are_normalized_then_expanded()
    {
        // U+2019 right-single-quote — LLMs and back-translation often emit this instead of ASCII '.
        // The stripper must normalize first, then match the contraction. (Case-005 real bug.)
        AiTellStripper.Strip("So I’ve attached it.", "So I have attached it.")
            .Should().Be("So I have attached it.");
        AiTellStripper.Strip("That’ll work.", "That will work.")
            .Should().Be("That will work.");
    }

    [Fact]
    public void Hyphenated_words_are_not_touched()
    {
        // - in "well-known" is a regular hyphen, not em/en dash — must NOT be touched.
        const string text = "She is a well-known speaker on top-tier panels.";
        AiTellStripper.Strip(text, text).Should().Be(text);
    }
}
