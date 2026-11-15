
using System.Net;

namespace Wayhunter.Infrastructure.Integrations.Email;

public sealed record OtpEmailContent(
    string Subject,
    string TextBody,
    string HtmlBody);

public static class OtpEmailTemplate
{
    public static OtpEmailContent Create(
        string displayName,
        string otpCode,
        int expiryMinutes)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException(
                "Display name is required.");

        if (otpCode is null ||
            otpCode.Length != 6 ||
            !otpCode.All(c => c >= '0' && c <= '9'))
        {
            throw new ArgumentException(
                "OTP must contain six digits.");
        }

        if (expiryMinutes <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(expiryMinutes));

        var name = displayName.Trim();
        var safeName = WebUtility.HtmlEncode(name);

        var subject = "Wayhunter Email Verification";

        var text = $"""
            Hello {name},

            Your Wayhunter verification code is: {otpCode}

            This code expires in {expiryMinutes} minutes.

            If you did not request this code, ignore this email.
            """;

        var html = $"""
            <html>
            <body>
              <h2>Wayhunter Email Verification</h2>
              <p>Hello {safeName},</p>
              <p>Your verification code is:</p>
              <h1>{otpCode}</h1>
              <p>This code expires in {expiryMinutes} minutes.</p>
              <p>If you did not request this code, ignore this email.</p>
            </body>
            </html>
            """;

        return new OtpEmailContent(
            subject,
            text,
            html);
    }
}
