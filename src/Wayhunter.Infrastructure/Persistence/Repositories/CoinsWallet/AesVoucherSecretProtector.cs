using System.Security.Cryptography;
using System.Text;
using Wayhunter.Application.Modules.CoinsWallet.Interfaces;

namespace Wayhunter.Infrastructure.Persistence.Repositories.CoinsWallet;

public sealed class AesVoucherSecretProtector
    : IVoucherSecretProtector
{
    private readonly byte[] _key;

    public AesVoucherSecretProtector(string base64Key)
    {
        if (string.IsNullOrWhiteSpace(base64Key))
            throw new InvalidOperationException(
                "Voucher protection key is not configured.");

        try
        {
            _key = Convert.FromBase64String(base64Key.Trim());
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                "Voucher protection key must be Base64.", ex);
        }

        if (_key.Length != 32)
            throw new InvalidOperationException(
                "Voucher protection key must contain 32 bytes.");
    }

    public string Protect(string plaintextCode)
    {
        string code = RequireCode(plaintextCode);
        byte[] plaintext = Encoding.UTF8.GetBytes(code);
        byte[] nonce = RandomNumberGenerator.GetBytes(12);
        byte[] tag = new byte[16];
        byte[] ciphertext = new byte[plaintext.Length];

        using var aes = new AesGcm(_key, tag.Length);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        byte[] result = new byte[nonce.Length + tag.Length + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, result, nonce.Length, tag.Length);
        Buffer.BlockCopy(
            ciphertext,
            0,
            result,
            nonce.Length + tag.Length,
            ciphertext.Length);

        return Convert.ToBase64String(result);
    }

    public string Unprotect(string protectedCode)
    {
        byte[] payload;
        try
        {
            payload = Convert.FromBase64String(
                RequireCode(protectedCode));
        }
        catch (FormatException ex)
        {
            throw new CryptographicException(
                "Voucher code payload is invalid.", ex);
        }

        if (payload.Length <= 28)
            throw new CryptographicException(
                "Voucher code payload is invalid.");

        ReadOnlySpan<byte> nonce = payload.AsSpan(0, 12);
        ReadOnlySpan<byte> tag = payload.AsSpan(12, 16);
        ReadOnlySpan<byte> ciphertext = payload.AsSpan(28);
        byte[] plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(_key, tag.Length);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }

    public string ComputeHash(string plaintextCode)
    {
        string code = RequireCode(plaintextCode);
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(code)));
    }

    private static string RequireCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Voucher code is required.");

        string result = value.Trim();
        if (result.Length > 200)
            throw new ArgumentException(
                "Voucher code is too long.");

        return result;
    }
}
