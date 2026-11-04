using NextORM.Core;
using NextORM.Extensions.ProviderTests;

namespace NextORM.SqlServer.Tests.QueryComponents;

public sealed class ExtremeRowExSqlGenerationTests : ExtremeRowExSqlGenerationTestsBase
{
    protected override IDataContext CreateContext() => SqlServerTestContext.Create();
}
