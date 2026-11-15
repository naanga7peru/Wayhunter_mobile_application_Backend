
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;

namespace Wayhunter.Api.Services.IdentitySocial;

public sealed class SignedSessionTokenService
{
    private readonly SessionTokenClaimsFactory _claimsFactory = new();

    private readonly string _issuer;
    private readonly string _audience;
    private readonly SymmetricSecurityKey _key;

    public SignedSessionTokenService(
        string issuer,
        string audience,
        string signingSecret)
    {
        if (string.IsNullOrWhiteSpace(issuer))
            throw new ArgumentException("Issuer required.", nameof(issuer));

        if (string.IsNullOrWhiteSpace(audience))
            throw new ArgumentException("Audience required.", nameof(audience));

        if (string.IsNullOrEmpty(signingSecret))
            throw new ArgumentException("Signing key required.", nameof(signingSecret));

        var bytes = Encoding.UTF8.GetBytes(signingSecret);

        if (bytes.Length < 32)
            throw new ArgumentException(
                "Signing key must contain at least 32 UTF-8 bytes.",
                nameof(signingSecret));

        _issuer = issuer;
        _audience = audience;
        _key = new SymmetricSecurityKey(bytes);
    }

    public string Issue(
        UserAccount account,
        IdentitySession session,
        DateTimeOffset nowUtc)
    {
        var claims = _claimsFactory.Create(
            account, session, nowUtc);

        // JWT registered time claims are supplied through
        // token constructor parameters instead.
        var payloadClaims = claims.Where(c =>
            c.Type != JwtRegisteredClaimNames.Nbf &&
            c.Type != JwtRegisteredClaimNames.Exp);

        var credentials = new SigningCredentials(
            _key,
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: payloadClaims,
            notBefore: nowUtc.UtcDateTime,
            expires: session.ExpiresAtUtc.UtcDateTime,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler()
            .WriteToken(token);
    }

    public ClaimsPrincipal Validate(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new SecurityTokenException("Token is required.");

        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _issuer,

            ValidateAudience = true,
            ValidAudience = _audience,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = _key,

            RequireSignedTokens = true,
            RequireExpirationTime = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,

            ValidAlgorithms =
            [
                SecurityAlgorithms.HmacSha256
            ]
        };

        var handler = new JwtSecurityTokenHandler
        {
            MapInboundClaims = false
        };

        var principal = handler.ValidateToken(
            token,
            parameters,
            out var validatedToken);

        if (validatedToken is not JwtSecurityToken jwt ||
            !string.Equals(
                jwt.Header.Alg,
                SecurityAlgorithms.HmacSha256,
                StringComparison.Ordinal))
        {
            throw new SecurityTokenException(
                "Invalid token algorithm.");
        }

        var userId = principal.FindFirst("sub")?.Value;
        var sessionId = principal.FindFirst("sid")?.Value;
        var tokenId = principal.FindFirst("jti")?.Value;

        if (!ValidId(userId) ||
            !ValidId(sessionId) ||
            !ValidId(tokenId))
        {
            throw new SecurityTokenException(
                "Required identity claims are invalid.");
        }

        return principal;
    }

    private static bool ValidId(string? value)
    {
        return Guid.TryParseExact(
            value, "D", out var parsed) &&
            parsed != Guid.Empty;
    }
}
