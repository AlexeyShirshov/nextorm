namespace NextORM.Integration.Tests;

/// <summary>
/// Runs the shared integration suite against the PostgreSQL provider, backed by a Testcontainers
/// instance unless NEXTORM_POSTGRES_CONNECTION points at an existing server.
/// </summary>
[Collection("Query plan cache")]
public sealed class PostgresIntegrationTests : CommonTestSuite
{
    protected override ITestProvider Provider => PostgresTestProvider.Instance;
}
