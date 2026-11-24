using Microsoft.Data.SqlClient;

namespace Wayhunter.IntegrationTests.Modules.TripSafety;

internal static class M3LocalDbProfile
{
    private const string Server = @"(localdb)\MSSQLLocalDB";
    private const string DisposableCatalogPrefix = "Wayhunter_Test_M3_";
    private const string PreviouslyAuthorizedCatalog = "Wayhunter_Test_M5_eee96080bb0540ebaa5cd326f366d2fb";

    public static string GetAuthorizedDisposableCatalog()
    {
        var name = Environment.GetEnvironmentVariable("WAYHUNTER_M3_TEST_DATABASE");
        if (!IsAuthorizedCatalog(name))
            throw new InvalidOperationException("M3 SQL tests require an explicitly selected authorized disposable catalog.");
        return name!;
    }

    public static SqlConnectionStringBuilder Validate(string? value, string expectedCatalog)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("The isolated M3 SQL test profile is missing.");

        SqlConnectionStringBuilder supplied;
        try
        {
            supplied = new(value);
        }
        catch (ArgumentException)
        {
            throw new InvalidOperationException("The isolated M3 SQL test profile is invalid.");
        }

        bool validTarget = IsAuthorizedCatalog(expectedCatalog) &&
            string.Equals(supplied.InitialCatalog, expectedCatalog, StringComparison.Ordinal);

        if (!string.Equals(supplied.DataSource, Server, StringComparison.OrdinalIgnoreCase) ||
            !validTarget ||
            !supplied.IntegratedSecurity || supplied.Keys.Cast<string>().Any(key =>
                supplied.ShouldSerialize(key) && key is not ("Data Source" or "Initial Catalog" or
                    "Integrated Security" or "Trust Server Certificate" or "Connect Timeout")))
            throw new InvalidOperationException("Only the exact isolated M3 LocalDB profile is allowed.");

        return new SqlConnectionStringBuilder
        {
            DataSource = Server,
            InitialCatalog = expectedCatalog,
            IntegratedSecurity = true,
            TrustServerCertificate = true,
            ConnectTimeout = 15
        };
    }

    private static bool IsUniqueDisposableCatalog(string? name) =>
        !string.IsNullOrWhiteSpace(name) &&
        name.StartsWith(DisposableCatalogPrefix, StringComparison.Ordinal) &&
        Guid.TryParseExact(name[DisposableCatalogPrefix.Length..], "N", out var id) &&
        id != Guid.Empty;

    private static bool IsAuthorizedCatalog(string? name) =>
        IsUniqueDisposableCatalog(name) ||
        string.Equals(name, PreviouslyAuthorizedCatalog, StringComparison.Ordinal);
}
