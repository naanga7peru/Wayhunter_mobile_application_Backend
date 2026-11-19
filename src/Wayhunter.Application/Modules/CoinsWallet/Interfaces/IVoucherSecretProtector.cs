namespace Wayhunter.Application.Modules.CoinsWallet.Interfaces;

public interface IVoucherSecretProtector
{
    string Protect(string plaintextCode);
    string Unprotect(string protectedCode);
    string ComputeHash(string plaintextCode);
}
