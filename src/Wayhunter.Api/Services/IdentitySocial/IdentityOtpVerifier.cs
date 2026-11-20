using System.Security.Cryptography;
using System.Text;

namespace Wayhunter.Api.Services.IdentitySocial;

public sealed class IdentityOtpVerifier
{
    private readonly byte[] _key;

    public IdentityOtpVerifier(string signingSecret)
    {
        if (string.IsNullOrWhiteSpace(signingSecret) ||
            Encoding.UTF8.GetByteCount(signingSecret) < 32)
        {
            throw new ArgumentException(
                "OTP verifier key must contain at least 32 UTF-8 bytes.",
                nameof(signingSecret));
        }

        _key = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(signingSecret),
            Encoding.UTF8.GetBytes("wayhunter:identity:otp-verifier:v1"));
    }

    public string Hash(string userId, string code)
    {
        var bytes = Encoding.UTF8.GetBytes($"{userId}:{code}");
        return Convert.ToHexString(HMACSHA256.HashData(_key, bytes));
    }

    public bool Verify(string userId, string code, string expectedHash)
    {
        if (code.Length != 6 || code.Any(c => c is < '0' or > '9'))
            return false;

        byte[] expected;
        try
        {
            expected = Convert.FromHexString(expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Convert.FromHexString(Hash(userId, code));
        return expected.Length == actual.Length &&
            CryptographicOperations.FixedTimeEquals(expected, actual);
    }
}
