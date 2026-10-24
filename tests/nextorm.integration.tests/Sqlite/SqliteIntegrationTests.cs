namespace NextORM.Integration.Tests;

/// <summary>Runs the shared integration suite against the SQLite provider.</summary>
[Collection("Query plan cache")]
public sealed class SqliteIntegrationTests : CommonTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;
}
