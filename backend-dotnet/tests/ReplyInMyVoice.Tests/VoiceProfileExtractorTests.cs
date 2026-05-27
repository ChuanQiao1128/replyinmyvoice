using FluentAssertions;
using ReplyInMyVoice.Domain.Quality;

namespace ReplyInMyVoice.Tests;

public class VoiceProfileExtractorTests
{
    private const string CasualOne =
        "Hi Sam,\n\nThanks for the update! I can't make the 3pm slot, but I'm free after 4. Let's grab 15 minutes then. Does that work?\n\nThanks,\nDana";

    private const string CasualTwo =
        "Hey Priya,\n\nGot your note. I'll send the deck tonight — it's almost done. We're still missing the Q3 numbers though. Can you ping finance?\n\nThanks,\nDana";

    private const string FormalOne =
        "Dear Mr. Chen,\n\nThank you for your email. I would be grateful if you could review the attached proposal at your convenience. Please let me know should you require any clarification.\n\nBest regards,\nDana Whitfield";

    private const string FormalTwo =
        "Dear Ms. Alvarez,\n\nI appreciate your patience regarding this matter. Kindly find the revised schedule enclosed. Please do not hesitate to contact me should any questions arise.\n\nBest regards,\nDana Whitfield";

    [Fact]
    public void Extracts_casual_voice()
    {
        var profile = VoiceProfileExtractor.Extract(new[] { CasualOne, CasualTwo });

        profile.SampleCount.Should().Be(2);
        profile.HasEnoughSamples.Should().BeTrue();
        profile.OpeningStyle.Should().Match(o => o!.Contains("{name}")); // greeting + name
        profile.ClosingStyle.Should().Be("Thanks,");
        profile.ContractionRate.Should().BeGreaterThan(0.02); // can't / I'm / I'll / it's / we're
        profile.MedianSentenceWords.Should().NotBeNull();
    }

    [Fact]
    public void Extracts_formal_voice()
    {
        var profile = VoiceProfileExtractor.Extract(new[] { FormalOne, FormalTwo });

        profile.OpeningStyle.Should().Be("Dear {name},");
        profile.ClosingStyle.Should().Be("Best Regards,");
        profile.ContractionRate.Should().Be(0d);          // no contractions in formal writing
        profile.PolitenessLevel.Should().Be("high");       // thank you / grateful / please / appreciate / kindly
    }

    [Fact]
    public void Casual_voice_has_lower_politeness_than_formal()
    {
        var casual = VoiceProfileExtractor.Extract(new[] { CasualOne, CasualTwo });
        var formal = VoiceProfileExtractor.Extract(new[] { FormalOne, FormalTwo });

        // Formal samples are markedly more polite; casual ones lean on "Thanks".
        casual.PolitenessLevel.Should().NotBe("high");
        formal.PolitenessLevel.Should().Be("high");
    }

    [Fact]
    public void Empty_samples_yield_empty_profile_below_floor()
    {
        var profile = VoiceProfileExtractor.Extract(Array.Empty<string>());

        profile.Should().Be(VoiceProfile.Empty);
        profile.HasEnoughSamples.Should().BeFalse();
        profile.OpeningStyle.Should().BeNull();
    }

    [Fact]
    public void Single_sample_is_below_the_floor()
    {
        VoiceProfileExtractor.Extract(new[] { CasualOne }).HasEnoughSamples.Should().BeFalse();
    }

    [Fact]
    public void No_greeting_or_signoff_leaves_those_null()
    {
        var profile = VoiceProfileExtractor.Extract(new[]
        {
            "The figures are ready. I pushed them to the shared drive this morning.",
            "Numbers look right. Approving now.",
        });

        profile.OpeningStyle.Should().BeNull();
        profile.ClosingStyle.Should().BeNull();
        profile.MedianSentenceWords.Should().NotBeNull(); // sentences still measured
    }
}
