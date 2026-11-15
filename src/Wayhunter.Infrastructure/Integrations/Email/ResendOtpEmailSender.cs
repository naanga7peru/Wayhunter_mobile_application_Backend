
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Text.Json;

namespace Wayhunter.Infrastructure.Integrations.Email;

public sealed record OtpEmailDeliveryResult(
    bool Accepted,
    string? ProviderMessageId,
    string? FailureCode);

public sealed class ResendOtpEmailSender
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _senderAddress;

    public ResendOtpEmailSender(
        HttpClient httpClient,
        string apiKey,
        string senderAddress)
    {
        _httpClient = httpClient
            ?? throw new ArgumentNullException(
                nameof(httpClient));

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException(
                "Resend API key is required.");

        if (string.IsNullOrWhiteSpace(senderAddress) ||
            !MailAddress.TryCreate(senderAddress, out _))
        {
            throw new ArgumentException(
                "Valid sender address is required.");
        }

        _apiKey = apiKey.Trim();
        _senderAddress = senderAddress.Trim();
    }

    public async Task<OtpEmailDeliveryResult> SendAsync(
        string recipientEmail,
        OtpEmailContent content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (string.IsNullOrWhiteSpace(recipientEmail))
            throw new ArgumentException(
                "Recipient email is required.");

        var recipient = recipientEmail.Trim();

        if (!MailAddress.TryCreate(recipient, out var parsed) ||
            !string.Equals(
                parsed.Address,
                recipient,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Invalid recipient email.");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://api.resend.com/emails");

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                _apiKey);

        request.Content = JsonContent.Create(new
        {
            from = _senderAddress,
            to = new[] { recipient },
            subject = content.Subject,
            html = content.HtmlBody,
            text = content.TextBody
        });

        try
        {
            using var response =
                await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new OtpEmailDeliveryResult(
                    false,
                    null,
                    $"provider_http_{(int)response.StatusCode}");
            }

            await using var stream =
                await response.Content.ReadAsStreamAsync(
                    cancellationToken);

            using var document =
                await JsonDocument.ParseAsync(
                    stream,
                    cancellationToken: cancellationToken);

            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("id", out var id) &&
                id.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(id.GetString()))
            {
                return new OtpEmailDeliveryResult(
                    true,
                    id.GetString(),
                    null);
            }

            return new OtpEmailDeliveryResult(
                false,
                null,
                "invalid_provider_response");
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return new OtpEmailDeliveryResult(
                false,
                null,
                "provider_timeout");
        }
        catch (HttpRequestException)
        {
            return new OtpEmailDeliveryResult(
                false,
                null,
                "provider_network_error");
        }
        catch (JsonException)
        {
            return new OtpEmailDeliveryResult(
                false,
                null,
                "invalid_provider_response");
        }
    }
}
