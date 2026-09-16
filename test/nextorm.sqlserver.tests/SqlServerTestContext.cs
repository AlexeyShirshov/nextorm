using nextorm.core;

namespace nextorm.sqlserver.tests;

/// <summary>
/// Builds a <see cref="SqlServerDbContext"/> for the SQL generation tests. These tests never open a
/// connection, so a placeholder connection string is enough. Tests that talk to a real server live
/// in nextorm.integration.tests, which starts a SQL Server Testcontainers instance (or uses the
/// server named by NEXTORM_SQLSERVER_CONNECTION).
/// </summary>
internal static class SqlServerTestContext
{
    private const string PlaceholderConnectionString =
        "Server=localhost,1433;Database=nextorm;User Id=sa;Password=nextorm!Passw0rd;TrustServerCertificate=True";

    public static IDataContext Create() =>
        new SqlServerDbContext(PlaceholderConnectionString, new DbContextBuilder());

    public static SqlServerDbContext CreateSqlServer() =>
        new(PlaceholderConnectionString, new DbContextBuilder());
}
