using NextORM.Core;
using NextORM.Extensions.ProviderTests;

namespace NextORM.Sqlite.Tests.QueryComponents;

public sealed class ExtremeRowExSqlGenerationTests : ExtremeRowExSqlGenerationTestsBase
{
    protected override IDataContext CreateContext() => SqliteTestContext.Create();
}
