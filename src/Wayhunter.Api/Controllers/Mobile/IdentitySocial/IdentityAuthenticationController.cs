using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wayhunter.Api.Contracts;
using Wayhunter.Api.Contracts.Responses;
using Wayhunter.Api.Services.IdentitySocial;

namespace Wayhunter.Api.Controllers.Mobile.IdentitySocial;

public sealed record IdentityRegisterRequest(
    string DisplayName,
    string Email,
    string Password,
    string ConfirmPassword);

public sealed record IdentityOtpRequest(string Email, string Code);
public sealed record IdentityOtpResendRequest(string Email);
public sealed record IdentitySignInRequest(string Email, string Password);
public sealed record IdentityForgotPasswordRequest(string Email);
public sealed record IdentityResetPasswordRequest(
    string Email,
    string Token,
    string NewPassword,
    string ConfirmPassword);

[ApiController]
[AllowAnonymous]
[Route(IdentityApiContract.MobileBaseRoute)]
public sealed class IdentityAuthenticationController : ControllerBase
{
    private readonly IdentityRegistrationOtpService _registration;
    private readonly IdentitySignInService _signIn;
    private readonly PasswordRecoveryService _passwordRecovery;

    public IdentityAuthenticationController(
        IdentityRegistrationOtpService registration,
        IdentitySignInService signIn,
        PasswordRecoveryService passwordRecovery)
    {
        _registration = registration;
        _signIn = signIn;
        _passwordRecovery = passwordRecovery;
    }

    [HttpPost("register")]
    public Task<IActionResult> Register(
        IdentityRegisterRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(_registration.RegisterAsync(
            request.DisplayName,
            request.Email,
            request.Password,
            request.ConfirmPassword,
            cancellationToken));

    [HttpPost("resend-otp")]
    public Task<IActionResult> ResendOtp(
        IdentityOtpResendRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(_registration.ResendAsync(
            request.Email, cancellationToken));

    [HttpPost("verify-otp")]
    public Task<IActionResult> VerifyOtp(
        IdentityOtpRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(_registration.VerifyAsync(
            request.Email, request.Code, cancellationToken));

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        IdentitySignInRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _signIn.SignInAsync(
                request.Email, request.Password, cancellationToken);
            return result is null
                ? Error(StatusCodes.Status401Unauthorized,
                    "invalid_credentials_or_account_state",
                    "The supplied credentials could not be accepted.")
                : Ok(result);
        }
        catch (InvalidOperationException)
        {
            return Error(StatusCodes.Status503ServiceUnavailable,
                "authentication_configuration_unavailable",
                "Authentication is temporarily unavailable.");
        }
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(
        IdentityForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        await _passwordRecovery.RequestResetAsync(
            request.Email, cancellationToken);
        return Accepted(new { message = "If the account is eligible, reset instructions will be sent." });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(
        IdentityResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var reset = await _passwordRecovery.ResetPasswordAsync(
            request.Email,
            request.Token,
            request.NewPassword,
            request.ConfirmPassword,
            cancellationToken);
        return reset
            ? NoContent()
            : Error(StatusCodes.Status400BadRequest,
                "invalid_or_expired_reset_credential",
                "The reset credential is invalid or expired.");
    }

    private IActionResult ToActionResult(IdentityFlowResult result)
    {
        if (result.RetryAfter is { } retryAfter)
        {
            Response.Headers.RetryAfter = Math.Max(
                1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
        }

        if (result.StatusCode == StatusCodes.Status204NoContent)
            return NoContent();

        if (TryGetErrorCode(result.Response, out var code))
        {
            return Error(result.StatusCode, code, GetSafeMessage(code));
        }

        return StatusCode(result.StatusCode, result.Response);
    }

    private async Task<IActionResult> ExecuteAsync(
        Task<IdentityFlowResult> operation)
    {
        var result = await operation;
        return ToActionResult(result);
    }

    private IActionResult Error(int statusCode, string code, string message) =>
        StatusCode(statusCode, ApiErrorResponse.Create(
            code, message, HttpContext.TraceIdentifier));

    private static bool TryGetErrorCode(object? response, out string code)
    {
        var property = response?.GetType().GetProperty("error");
        if (property?.GetValue(response) is string value &&
            !string.IsNullOrWhiteSpace(value))
        {
            code = value;
            return true;
        }

        code = string.Empty;
        return false;
    }

    private static string GetSafeMessage(string code) => code switch
    {
        "invalid_registration" => "The registration details are invalid.",
        "account_conflict" => "The account could not be created with those details.",
        "otp_delivery_unavailable" => "Verification delivery is temporarily unavailable.",
        "otp_resend_cooldown" => "Please wait before requesting another verification code.",
        "invalid_or_expired_otp" => "The verification code is invalid or expired.",
        _ => "The authentication request could not be completed."
    };
}
