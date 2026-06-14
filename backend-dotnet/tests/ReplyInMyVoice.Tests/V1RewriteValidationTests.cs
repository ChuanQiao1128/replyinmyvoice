using FluentAssertions;
using ReplyInMyVoice.Application.UseCases.Rewrite;

namespace ReplyInMyVoice.Tests;

public sealed class V1RewriteValidationTests
{
    public static TheoryData<string?, V1ErrorCatalog.V1Error> InvalidDraftCases => new()
    {
        { null, V1ErrorCatalog.DraftRequired },
        { string.Empty, V1ErrorCatalog.DraftRequired },
        { "     ", V1ErrorCatalog.DraftRequired },
        { "123456789", V1ErrorCatalog.DraftRequired },
        { new string('a', 2401), V1ErrorCatalog.InputTooLong },
        { string.Join(" ", Enumerable.Repeat("word", 301)), V1ErrorCatalog.InputTooLong },
    };

    [Theory]
    [MemberData(nameof(InvalidDraftCases))]
    public void ValidateDraft_rejects_invalid_boundary_input(
        string? draft,
        V1ErrorCatalog.V1Error expectedError)
    {
        var result = V1RewriteValidation.ValidateDraft(draft);

        result.IsValid.Should().BeFalse();
        result.Value.Should().BeNull();
        result.Error.Should().Be(expectedError);
        result.Error!.Code.Should().Be(expectedError.Code);
        result.Error.Message.Should().Be(expectedError.Message);
        result.Error.StatusCode.Should().Be(expectedError.StatusCode);
    }

    [Fact]
    public void ValidateDraft_accepts_and_trims_valid_draft()
    {
        var result = V1RewriteValidation.ValidateDraft("  1234567890  ");

        result.IsValid.Should().BeTrue();
        result.Value.Should().Be("1234567890");
        result.Error.Should().BeNull();
    }

    [Fact]
    public void ValidateIdempotencyKey_rejects_too_long_key()
    {
        var result = V1RewriteValidation.ValidateIdempotencyKey(new string('k', 121));

        result.IsValid.Should().BeFalse();
        result.Value.Should().BeNull();
        result.Error.Should().Be(V1ErrorCatalog.IdempotencyKeyTooLong);
        result.Error!.Message.Should().Be(V1ErrorCatalog.IdempotencyKeyTooLong.Message);
    }

    [Fact]
    public void ValidateIdempotencyKey_accepts_boundary_key()
    {
        var key = new string('k', 120);

        var result = V1RewriteValidation.ValidateIdempotencyKey(key);

        result.IsValid.Should().BeTrue();
        result.Value.Should().Be(key);
        result.Error.Should().BeNull();
    }
}
