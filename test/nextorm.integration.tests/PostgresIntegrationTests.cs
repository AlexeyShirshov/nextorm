namespace nextorm.integration.tests;

/// <summary>
/// Runs the shared integration suite against the PostgreSQL provider, backed by a Testcontainers
/// instance unless NEXTORM_POSTGRES_CONNECTION points at an existing server.
/// </summary>
public sealed class PostgresIntegrationTests : CommonTestSuite
{
    protected override ITestProvider Provider => PostgresTestProvider.Instance;
}
