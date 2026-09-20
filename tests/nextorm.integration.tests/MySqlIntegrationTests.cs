namespace NextORM.Integration.Tests;

/// <summary>
/// Runs the shared integration suite against the MySQL provider, backed by a Testcontainers
/// instance unless NEXTORM_MYSQL_CONNECTION points at an existing server.
/// </summary>
public sealed class MySqlIntegrationTests : CommonTestSuite
{
    protected override ITestProvider Provider => MySqlTestProvider.Instance;
}
