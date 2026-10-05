using System.Linq.Expressions;
using NextORM.Core;

namespace NextORM.SqlServer.Tests;

/// <summary>
/// Builds a <see cref="SqlServerDataContext"/> for the SQL generation tests. These tests never open a
/// connection, so a placeholder connection string is enough. Tests that talk to a real server live
/// in NextORM.Integration.Tests, which starts a SQL Server Testcontainers instance (or uses the
/// server named by NEXTORM_SQLSERVER_CONNECTION).
/// </summary>
internal static class SqlServerTestContext
{
    private const string PlaceholderConnectionString =
        "Server=localhost,1433;Database=nextorm;User Id=sa;Password=nextorm!Passw0rd;TrustServerCertificate=True";

    public static IDataContext Create() =>
        new SqlServerDataContext(PlaceholderConnectionString, new DataContextBuilder());

    public static IDataContext CreateQuoted() =>
        new SqlServerDataContext(PlaceholderConnectionString, new DataContextBuilder().UseQuotedIdentifiers());

    public static IDataContext CreateUppercase() =>
        new SqlServerDataContext(PlaceholderConnectionString, new DataContextBuilder().UseKeywordCase());

    public static SqlServerDataContext CreateSqlServer() =>
        new(PlaceholderConnectionString, new DataContextBuilder());

    /// <summary>
    /// Creates a SQL Server context that exposes the <c>protected</c> CSV typed-column hook
    /// (<see cref="SqlServerDataContext.MapTypedColumnExpression"/>) so tests can inspect the mapped
    /// expression directly. The tests never open a connection.
    /// </summary>
    public static CsvHookSqlServerDataContext CreateCsvHook() =>
        new(PlaceholderConnectionString, new DataContextBuilder());
}

/// <summary>
/// A <see cref="SqlServerDataContext"/> for tests that need the otherwise-<c>protected</c> CSV
/// typed-column hook; it re-exposes the hook publicly without widening the production surface.
/// </summary>
internal sealed class CsvHookSqlServerDataContext(string connectionString, DataContextBuilder builder)
    : SqlServerDataContext(connectionString, builder)
{
    /// <summary>Re-exposes <see cref="SqlServerDataContext.MapTypedColumnExpression"/> to this test assembly.</summary>
    public new Expression MapTypedColumnExpression(SelectExpression column, Expression record, Type storageType)
        => base.MapTypedColumnExpression(column, record, storageType);
}
