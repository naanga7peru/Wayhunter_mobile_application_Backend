using System.Globalization;
using System.Security.Cryptography;

namespace Wayhunter.Api.Services.IdentitySocial;

public interface IIdentityOtpCodeGenerator
{
    string Generate();
}

public sealed class CryptographicIdentityOtpCodeGenerator
    : IIdentityOtpCodeGenerator
{
    public string Generate() =>
        RandomNumberGenerator.GetInt32(0, 1_000_000)
            .ToString("D6", CultureInfo.InvariantCulture);
}
