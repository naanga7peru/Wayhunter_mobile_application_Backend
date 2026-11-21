using System.Net;
using Wayhunter.Infrastructure.Integrations.Email;

namespace Wayhunter.Api.Services.IdentitySocial;

public interface IPasswordResetEmailSender
{
    Task<bool> SendAsync(
        string email,
        string displayName,
        string resetToken,
        int expiryMinutes,
        CancellationToken cancellationToken = default);
}

public sealed class ResendPasswordResetEmailSender : IPasswordResetEmailSender
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public ResendPasswordResetEmailSender(
        HttpClient httpClient,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<bool> SendAsync(
        string email,
        string displayName,
        string resetToken,
        int expiryMinutes,
        CancellationToken cancellationToken = default)
    {
        var apiKey = _configuration["Identity:Email:ResendApiKey"];
        var sender = _configuration["Identity:Email:SenderAddress"];
        if (string.IsNullOrWhiteSpace(apiKey) ||
            string.IsNullOrWhiteSpace(sender))
        {
            return false;
        }

        var safeName = WebUtility.HtmlEncode(displayName);
        var safeToken = WebUtility.HtmlEncode(resetToken);
        var content = new OtpEmailContent(
            "Wayhunter password reset",
            $"Hello {displayName},\n\nYour password reset credential is: {resetToken}\n\nIt expires in {expiryMinutes} minutes.",
            $"<html><body><p>Hello {safeName},</p><p>Your password reset credential is:</p><p><code>{safeToken}</code></p><p>It expires in {expiryMinutes} minutes.</p></body></html>");

        var result = await new ResendOtpEmailSender(
                _httpClient, apiKey, sender)
            .SendAsync(email, content, cancellationToken);
        return result.Accepted;
    }
}
