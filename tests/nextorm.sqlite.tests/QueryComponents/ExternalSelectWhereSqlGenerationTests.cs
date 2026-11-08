using NextORM.Core;
using NextORM.QueryComponents.ProviderTests;

namespace NextORM.Sqlite.Tests.QueryComponents;

public sealed class ExternalSelectWhereSqlGenerationTests : ExternalSelectWhereSqlGenerationTestsBase
{
    protected override IDataContext CreateContext() => SqliteTestContext.Create();
}
