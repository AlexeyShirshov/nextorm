using NextORM.Core;
using NextORM.QueryComponents.ProviderTests;

namespace NextORM.Postgres.Tests.QueryComponents;

public sealed class ExternalSelectWhereSqlGenerationTests : ExternalSelectWhereSqlGenerationTestsBase
{
    protected override IDataContext CreateContext() => PostgresTestContext.Create();
}
