namespace ReplyInMyVoice.Domain.Contracts;

public sealed record RewriteRequest(
    string? MessageToReplyTo,
    string RoughDraftReply,
    string? Audience,
    string? Purpose,
    string? WhatHappened,
    string? FactsToPreserve,
    string Tone);
