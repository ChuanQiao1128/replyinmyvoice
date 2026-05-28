using ReplyInMyVoice.Domain.Quality;
using ReplyInMyVoice.Domain.RewriteEngine;

namespace ReplyInMyVoice.Eval;

// IClaimLedgerExtractor impl for the EN→ZH safe-intermediate pipeline. Calls DeepSeek with the
// frozen claim-ledger-v1 prompt (SystemPrompt in Domain.Quality.ClaimLedgerJsonParser) and feeds
// the raw JSON through the deterministic parser (which dedupes, drops paraphrased spans, and
// soft-skips empathy openings).
//
// Lives in the eval tool for now because the Infrastructure project does not yet have a generic
// DeepSeek chat client refactored out of TranslationPilotV2. When Phase 1 moves to prod, lift the
// DeepSeekChatClient + this extractor into Infrastructure unchanged.
internal sealed class DeepSeekClaimLedgerExtractor(DeepSeekChatClient chat) : IClaimLedgerExtractor
{
    // The frozen claim-ledger-v1 prompt validated 2026-05-28 across 10 corpus cases.
    // See /tmp/claim_ledger_validation_v2/ + ClaimLedgerJsonParser.SystemPrompt for the text.
    public async Task<RewriteClaimLedger> ExtractAsync(string draft, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(draft))
        {
            return new RewriteClaimLedger(Array.Empty<RewriteClaim>());
        }

        // max_tokens budget: case 041 produced 20 claims in ~2.4k tokens of JSON; 2500 covers
        // the upper envelope with headroom. temperature 0 for reproducibility.
        var content = await chat.CompleteAsync(
            ClaimLedgerJsonParser.SystemPrompt,
            draft,
            maxTokens: 2500,
            temperature: 0.0,
            cancellationToken);

        return ClaimLedgerJsonParser.Parse(content ?? string.Empty, draft);
    }
}
