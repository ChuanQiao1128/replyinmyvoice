namespace ReplyInMyVoice.Application.UseCases.Rewrite;

public static class V1RewriteValidation
{
    public const int MinimumDraftLength = 10;
    public const int MaximumDraftWords = 300;
    public const int MaximumDraftCharacters = 2400;
    public const int MaximumIdempotencyKeyLength = 120;

    public static V1RewriteValidationResult ValidateDraft(string? rawDraft)
    {
        var draft = rawDraft?.Trim();
        if (string.IsNullOrWhiteSpace(draft) || draft.Length < MinimumDraftLength)
        {
            return V1RewriteValidationResult.Invalid(V1ErrorCatalog.DraftRequired);
        }

        if (draft.Length > MaximumDraftCharacters || CountWords(draft) > MaximumDraftWords)
        {
            return V1RewriteValidationResult.Invalid(V1ErrorCatalog.InputTooLong);
        }

        return V1RewriteValidationResult.Valid(draft);
    }

    public static V1RewriteValidationResult ValidateIdempotencyKey(string? key)
    {
        if (key?.Length > MaximumIdempotencyKeyLength)
        {
            return V1RewriteValidationResult.Invalid(V1ErrorCatalog.IdempotencyKeyTooLong);
        }

        return V1RewriteValidationResult.Valid(key);
    }

    public static int CountWords(string value)
    {
        var count = 0;
        var inWord = false;

        foreach (var character in value)
        {
            if (char.IsWhiteSpace(character))
            {
                inWord = false;
                continue;
            }

            if (inWord)
            {
                continue;
            }

            count += 1;
            inWord = true;
        }

        return count;
    }
}

public sealed record V1RewriteValidationResult(string? Value, V1ErrorCatalog.V1Error? Error)
{
    public bool IsValid => Error is null;

    public static V1RewriteValidationResult Valid(string? value) => new(value, null);

    public static V1RewriteValidationResult Invalid(V1ErrorCatalog.V1Error error) => new(null, error);
}
