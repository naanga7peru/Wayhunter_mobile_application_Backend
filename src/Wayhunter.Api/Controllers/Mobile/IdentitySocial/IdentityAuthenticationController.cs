using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wayhunter.Api.Contracts;
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

[ApiController]
[AllowAnonymous]
[Route(IdentityApiContract.MobileBaseRoute)]
public sealed class IdentityAuthenticationController : ControllerBase
{
    private readonly IdentityRegistrationOtpService _registration;
    private readonly IdentitySignInService _signIn;

    public IdentityAuthenticationController(
        IdentityRegistrationOtpService registration,
        IdentitySignInService signIn)
    {
        _registration = registration;
        _signIn = signIn;
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
                ? Unauthorized(new { error = "invalid_credentials_or_account_state" })
                : Ok(result);
        }
        catch (InvalidOperationException)
        {
            return StatusCode(503, new { error = "authentication_configuration_unavailable" });
        }
    }

    private IActionResult ToActionResult(IdentityFlowResult result)
    {
        if (result.RetryAfter is { } retryAfter)
        {
            Response.Headers.RetryAfter = Math.Max(
                1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
        }

        return result.StatusCode == 204
            ? NoContent()
            : StatusCode(result.StatusCode, result.Response);
    }

    private async Task<IActionResult> ExecuteAsync(
        Task<IdentityFlowResult> operation)
    {
        var result = await operation;
        return ToActionResult(result);
    }
}
