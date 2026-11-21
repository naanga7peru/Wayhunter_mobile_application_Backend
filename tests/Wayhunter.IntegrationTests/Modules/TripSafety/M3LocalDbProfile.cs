using Microsoft.Data.SqlClient;

namespace Wayhunter.IntegrationTests.Modules.TripSafety;

internal static class M3LocalDbProfile
{
    private const string Server = @"(localdb)\MSSQLLocalDB";

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

        if (!string.Equals(supplied.DataSource, Server, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(supplied.InitialCatalog, expectedCatalog, StringComparison.Ordinal) ||
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
}
