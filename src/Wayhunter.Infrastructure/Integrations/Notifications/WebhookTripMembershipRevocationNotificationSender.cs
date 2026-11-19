
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;

namespace Wayhunter.Infrastructure.Integrations.Notifications;

public sealed class M3OutboxWebhookSettings
{
    public string WebhookUrl { get; set; } = string.Empty;

    public string WebhookApiKey { get; set; } = string.Empty;

    public void Validate()
    {
        if (!Uri.TryCreate(
                WebhookUrl,
                UriKind.Absolute,
                out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new InvalidOperationException(
                "M3 notification webhook requires a valid HTTPS URL.");
        }

        if (string.IsNullOrWhiteSpace(WebhookApiKey) ||
            WebhookApiKey.Length < 32 ||
            WebhookApiKey.Contains('\r') ||
            WebhookApiKey.Contains('\n'))
        {
            throw new InvalidOperationException(
                "M3 notification webhook secret is not configured safely.");
        }
    }
}

public sealed class WebhookTripMembershipRevocationNotificationSender
    : ITripMembershipRevocationNotificationSender
{
    private readonly HttpClient _httpClient;

    private readonly M3OutboxWebhookSettings _settings;

    public WebhookTripMembershipRevocationNotificationSender(
        HttpClient httpClient,
        M3OutboxWebhookSettings settings)
    {
        _httpClient = httpClient ??
            throw new ArgumentNullException(nameof(httpClient));

        _settings = settings ??
            throw new ArgumentNullException(nameof(settings));

        _settings.Validate();
    }

    public async Task SendAsync(
        TripMembershipRevocationNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        cancellationToken.ThrowIfCancellationRequested();

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            _settings.WebhookUrl);

        request.Headers.TryAddWithoutValidation(
            "Idempotency-Key",
            notification.NotificationId.ToString("D"));

        request.Headers.TryAddWithoutValidation(
            "X-Wayhunter-Webhook-Secret",
            _settings.WebhookApiKey);

        request.Content = JsonContent.Create(new
        {
            EventType = "TripMembershipRevoked",
            notification.NotificationId,
            notification.TripPlanId,
            notification.MemberId,
            notification.MemberUserId,
            notification.RemovedAtUtc
        });

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            // Do not persist response body or the secret.
            throw new HttpRequestException(
                "M3 notification delivery was not acknowledged.");
        }

        // Successful response is treated as acknowledgement
        // only when the configured receiver guarantees
        // durable acceptance and idempotent processing.
    }
}
