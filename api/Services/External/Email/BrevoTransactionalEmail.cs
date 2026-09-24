using Amanah.Api.Models.Common;

namespace Amanah.Api.Services.External.Email;

public static class BrevoTransactionalEmail
{
    public const string ApiUrl = "https://api.brevo.com/v3/smtp/email";

    public static HttpRequestMessage CreateRequest(
        string apiKey,
        string fromAddress,
        string? fromDisplayName,
        string toEmail,
        string subject,
        string textContent,
        string htmlContent,
        string? replyToEmail = null,
        string? idempotencyKey = null)
    {
        var requestBody = new BrevoEmailRequest
        {
            Sender = new BrevoEmailAddress
            {
                Email = fromAddress,
                Name = fromDisplayName,
            },
            To = [new BrevoEmailAddress { Email = toEmail }],
            Subject = subject,
            TextContent = textContent,
            HtmlContent = htmlContent,
            ReplyTo = string.IsNullOrWhiteSpace(replyToEmail)
                ? null
                : new BrevoEmailAddress { Email = replyToEmail },
            Headers = string.IsNullOrWhiteSpace(idempotencyKey)
                ? null
                : new Dictionary<string, string> { ["idempotencyKey"] = idempotencyKey },
        };

        var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl);
        request.Headers.TryAddWithoutValidation("api-key", apiKey);
        request.Content = JsonContent.Create(requestBody, options: ApiJson.SerializerOptions);
        return request;
    }

    internal sealed class BrevoEmailRequest
    {
        public required BrevoEmailAddress Sender { get; init; }

        public required IReadOnlyList<BrevoEmailAddress> To { get; init; }

        public required string Subject { get; init; }

        public required string TextContent { get; init; }

        public required string HtmlContent { get; init; }

        public BrevoEmailAddress? ReplyTo { get; init; }

        public Dictionary<string, string>? Headers { get; init; }
    }

    internal sealed class BrevoEmailAddress
    {
        public required string Email { get; init; }

        public string? Name { get; init; }
    }
}
