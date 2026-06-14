namespace ReplyInMyVoice.Application.UseCases.StripeEvent;

public sealed record IngestStripeWebhookCommand(
    string EventId,
    string Type,
    string RawBody,
    DateTimeOffset Now);

public enum StripeWebhookIngestResult
{
    Accepted,
    AlreadyProcessed,
    AlreadyPending,
}
