using NextORM.Core;
using NextORM.QueryComponents.ProviderTests;

namespace NextORM.MySql.Tests.QueryComponents;

public sealed class ExternalSelectWhereSqlGenerationTests : ExternalSelectWhereSqlGenerationTestsBase
{
    protected override IDataContext CreateContext() => MySqlTestContext.Create();
}
