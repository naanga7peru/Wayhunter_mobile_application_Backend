
using System;
using Wayhunter.Infrastructure.Integrations.Email;
using Xunit;

namespace Wayhunter.UnitTests.Modules.IdentitySocial;

public sealed class OtpEmailTemplateTests
{
    [Fact]
    public void ValidOtp_ShouldCreateEmailContent()
    {
        var result = OtpEmailTemplate.Create(
            "Demo Traveler",
            "123456",
            5);

        Assert.Equal(
            "Wayhunter Email Verification",
            result.Subject);

        Assert.Contains("123456", result.TextBody);
        Assert.Contains("123456", result.HtmlBody);
        Assert.Contains("5 minutes", result.TextBody);
    }

    [Fact]
    public void HtmlTemplate_ShouldEscapeUnsafeName()
    {
        var result = OtpEmailTemplate.Create(
            "<script>alert(1)</script>",
            "123456",
            5);

        Assert.Contains(
            "&lt;script&gt;",
            result.HtmlBody);

        Assert.DoesNotContain(
            "<script>",
            result.HtmlBody);
    }

    [Fact]
    public void TextTemplate_ShouldIncludeTravelerName()
    {
        var result = OtpEmailTemplate.Create(
            "Shayanthan",
            "654321",
            10);

        Assert.Contains(
            "Hello Shayanthan",
            result.TextBody);

        Assert.Contains(
            "654321",
            result.TextBody);
    }

    [Fact]
    public void InvalidOtp_ShouldBeRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            OtpEmailTemplate.Create(
                "Demo User", "12345", 5));

        Assert.Throws<ArgumentException>(() =>
            OtpEmailTemplate.Create(
                "Demo User", "12345A", 5));

        Assert.Throws<ArgumentException>(() =>
            OtpEmailTemplate.Create(
                "Demo User", "1234567", 5));
    }

    [Fact]
    public void EmptyDisplayName_ShouldBeRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            OtpEmailTemplate.Create(
                "   ", "123456", 5));
    }

    [Fact]
    public void InvalidExpiry_ShouldBeRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            OtpEmailTemplate.Create(
                "Demo User", "123456", 0));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            OtpEmailTemplate.Create(
                "Demo User", "123456", -1));
    }
}
