namespace NextORM.Integration.Tests;

/// <summary>Runs the shared integration suite against the SQLite provider.</summary>
[Collection("Sqlite")]
public sealed class SqliteIntegrationTests : CommonTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;
}
