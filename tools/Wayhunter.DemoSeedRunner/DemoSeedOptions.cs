using Microsoft.Data.SqlClient;

namespace Wayhunter.DemoSeedRunner;

public sealed record DemoSeedOptions(
    string ConnectionString,
    string Catalog,
    string Password,
    string VoucherCode,
    string VoucherImageReference,
    string VoucherProtectionKey,
    bool SeedVoucher)
{
    public const string ConfirmationPrefix = "SEED_DEMO_DATABASE:";

    public static DemoSeedOptions FromEnvironment(
        IReadOnlyDictionary<string, string?> env,
        bool seedVoucher = true)
    {
        string connection = Required(env, "WAYHUNTER_DEMO_SEED_CONNECTION");
        string catalog = Required(env, "WAYHUNTER_DEMO_SEED_DATABASE");
        string confirmation = Required(env, "WAYHUNTER_DEMO_SEED_CONFIRM");
        string password = Required(env, "WAYHUNTER_DEMO_ACCOUNT_PASSWORD");
        string voucherCode = seedVoucher ? Required(env, "WAYHUNTER_DEMO_VOUCHER_CODE") : string.Empty;
        string image = seedVoucher ? Required(env, "WAYHUNTER_DEMO_VOUCHER_IMAGE_REFERENCE") : string.Empty;
        string protector = seedVoucher ? Required(env, "WAYHUNTER_DEMO_VOUCHER_PROTECTION_KEY") : string.Empty;

        if (password.Length < 8)
            throw new InvalidOperationException("Demo account password must be at least 8 characters.");
        if (confirmation != ConfirmationPrefix + catalog)
            throw new InvalidOperationException("Seed confirmation must exactly name the target database.");
        if (catalog.Contains("official", StringComparison.OrdinalIgnoreCase) ||
            catalog.Contains("prod", StringComparison.OrdinalIgnoreCase) ||
            !catalog.StartsWith("Wayhunter_Demo_", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Seed runner permits only Wayhunter_Demo_* catalogs; Official/production are refused.");

        var builder = new SqlConnectionStringBuilder(connection);
        if (!string.Equals(builder.InitialCatalog, catalog, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Connection catalog does not match the explicit seed database name.");
        if (!string.Equals(builder.DataSource, @"(localdb)\MSSQLLocalDB", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Demo seed runner only permits the local MSSQLLocalDB instance.");
        if (!builder.IntegratedSecurity)
            throw new InvalidOperationException("Demo seed runner requires integrated authentication.");
        var allowedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Data Source", "Initial Catalog", "Integrated Security", "Trust Server Certificate", "Connect Timeout"
        };
        foreach (string key in builder.Keys.Cast<string>())
            if (builder.ShouldSerialize(key) && !allowedKeys.Contains(key))
                throw new InvalidOperationException($"Connection string option '{key}' is not allowed by the seed safety policy.");
        if (seedVoucher && System.Uri.TryCreate(image, UriKind.Absolute, out var uri) &&
            uri.Scheme is "http" or "https")
            throw new InvalidOperationException("Voucher image must be a protected opaque media reference, not a URL.");

        var safeConnection = new SqlConnectionStringBuilder
        {
            DataSource = builder.DataSource,
            InitialCatalog = builder.InitialCatalog,
            IntegratedSecurity = true,
            TrustServerCertificate = true,
            ConnectTimeout = 15
        };
        return new DemoSeedOptions(safeConnection.ConnectionString, catalog, password,
            voucherCode, image.Trim(), protector, seedVoucher);
    }

    private static string Required(IReadOnlyDictionary<string, string?> env, string name) =>
        env.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : throw new InvalidOperationException($"Required environment variable {name} is missing.");
}
