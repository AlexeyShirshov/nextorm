using NextORM.Core;
using NextORM.Extensions.ProviderTests;

namespace NextORM.MariaDb.Tests.QueryComponents;

public sealed class ExtremeRowExSqlGenerationTests : ExtremeRowExSqlGenerationTestsBase
{
    protected override IDataContext CreateContext() => MariaDbTestContext.Create();
}
