using NextORM.Core;
using NextORM.QueryComponents.ProviderTests;

namespace NextORM.ClickHouse.Tests.QueryComponents;

public sealed class ExternalSelectWhereSqlGenerationTests : ExternalSelectWhereSqlGenerationTestsBase
{
    protected override IDataContext CreateContext() => ClickHouseTestContext.Create();
}
