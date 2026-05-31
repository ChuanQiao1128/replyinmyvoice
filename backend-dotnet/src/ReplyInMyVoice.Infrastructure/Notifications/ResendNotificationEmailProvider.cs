using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace ReplyInMyVoice.Infrastructure.Notifications;

public sealed class ResendNotificationEmailProvider(
    HttpClient httpClient,
    string apiKey,
    string fromEmail,
    string? replyToEmail,
    ILogger<ResendNotificationEmailProvider> logger) : INotificationEmailProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<NotificationSendResult> SendAsync(
        NotificationEmail email,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.UserAgent.ParseAdd("replyinmyvoice-notifications/1.0");

        var payload = new ResendEmailRequest(
            fromEmail,
            [email.Recipient.Email],
            email.Subject,
            email.HtmlBody,
            email.PlainTextBody,
            string.IsNullOrWhiteSpace(replyToEmail) ? null : replyToEmail,
            [new ResendEmailTag("template", SanitizeTagValue(email.TemplateName))]);

        request.Content = new StringContent(
            JsonSerializer.Serialize(payload, JsonOptions),
            Encoding.UTF8,
            "application/json");

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Notification template {TemplateName} was not accepted by Resend. StatusCode={StatusCode}.",
                    email.TemplateName,
                    (int)response.StatusCode);
                return NotificationSendResult.Skipped("resend", "provider_error");
            }

            var operationId = await ReadOperationIdAsync(response, cancellationToken);
            return NotificationSendResult.Delivered("resend", operationId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(
                exception,
                "Notification template {TemplateName} could not be sent through Resend.",
                email.TemplateName);
            return NotificationSendResult.Skipped("resend", "provider_unavailable");
        }
    }

    private static async Task<string?> ReadOperationIdAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(contentStream, cancellationToken: cancellationToken);
            return document.RootElement.TryGetProperty("id", out var idElement)
                ? idElement.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string SanitizeTagValue(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (char.IsAsciiLetterOrDigit(character) || character is '_' or '-')
            {
                builder.Append(character);
            }
        }

        return builder.Length == 0 ? "notification" : builder.ToString();
    }

    private sealed record ResendEmailRequest(
        string From,
        string[] To,
        string Subject,
        string Html,
        string Text,
        [property: JsonPropertyName("reply_to")] string? ReplyTo,
        ResendEmailTag[] Tags);

    private sealed record ResendEmailTag(
        string Name,
        string Value);
}
