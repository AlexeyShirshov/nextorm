using NextORM.Core;
using NextORM.QueryComponents.ProviderTests;

namespace NextORM.SqlServer.Tests.QueryComponents;

public sealed class ExternalSelectWhereSqlGenerationTests : ExternalSelectWhereSqlGenerationTestsBase
{
    protected override IDataContext CreateContext() => SqlServerTestContext.Create();
}
