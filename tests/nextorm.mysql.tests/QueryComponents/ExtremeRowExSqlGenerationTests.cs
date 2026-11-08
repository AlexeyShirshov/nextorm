using NextORM.Core;
using NextORM.Extensions.ProviderTests;

namespace NextORM.MySql.Tests.QueryComponents;

public sealed class ExtremeRowExSqlGenerationTests : ExtremeRowExSqlGenerationTestsBase
{
    protected override IDataContext CreateContext() => MySqlTestContext.Create();
}
