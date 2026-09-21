namespace NextORM.Integration.Tests;

/// <summary>
/// Runs the shared integration suite against the Microsoft SQL Server provider, backed by a
/// Testcontainers instance unless NEXTORM_SQLSERVER_CONNECTION points at an existing server.
/// </summary>
public sealed class SqlServerIntegrationTests : CommonTestSuite
{
    protected override ITestProvider Provider => SqlServerTestProvider.Instance;
}
