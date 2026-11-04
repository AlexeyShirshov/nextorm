using NextORM.Core;
using NextORM.Extensions.ProviderTests;

namespace NextORM.Postgres.Tests.QueryComponents;

public sealed class ExtremeRowExSqlGenerationTests : ExtremeRowExSqlGenerationTestsBase
{
    protected override IDataContext CreateContext() => PostgresTestContext.Create();
}
