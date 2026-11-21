using Wayhunter.Infrastructure.Integrations.Email;

namespace Wayhunter.Api.Services.IdentitySocial;

public interface IIdentityOtpEmailSender
{
    Task<bool> SendAsync(
        string recipientEmail,
        OtpEmailContent content,
        CancellationToken cancellationToken = default);
}

public sealed class ResendIdentityOtpEmailSender : IIdentityOtpEmailSender
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public ResendIdentityOtpEmailSender(
        HttpClient httpClient,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<bool> SendAsync(
        string recipientEmail,
        OtpEmailContent content,
        CancellationToken cancellationToken = default)
    {
        var apiKey = _configuration["Identity:Email:ResendApiKey"];
        var sender = _configuration["Identity:Email:SenderAddress"];
        if (string.IsNullOrWhiteSpace(apiKey) ||
            string.IsNullOrWhiteSpace(sender))
        {
            return false;
        }

        var result = await new ResendOtpEmailSender(
                _httpClient, apiKey, sender)
            .SendAsync(recipientEmail, content, cancellationToken);
        return result.Accepted;
    }
}
