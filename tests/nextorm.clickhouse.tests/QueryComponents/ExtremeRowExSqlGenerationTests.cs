using NextORM.ClickHouse;
using NextORM.Core;
using NextORM.Extensions.ProviderTests;

namespace NextORM.ClickHouse.Tests.QueryComponents;

public sealed class ExtremeRowExSqlGenerationTests : ExtremeRowExSqlGenerationTestsBase
{
    protected override IDataContext CreateContext() => new ForcedPortableClickHouseContext(ClickHouseTestContext.CreateClickHouse());

    protected override IDataContext CreateBuiltInReferenceContext()
        => new ForcedPortableClickHouseContext(ClickHouseTestContext.CreateClickHouse());

    private sealed class ForcedPortableClickHouseContext : ClickHouseDataContext
    {
        private readonly ISqlDialect _dialect;

        public ForcedPortableClickHouseContext(ClickHouseDataContext source)
            : base(GetConnectionString(source), new DataContextBuilder())
            => _dialect = new ForcedPortableDialect();

        public override ISqlDialect Dialect => _dialect;

        private static string GetConnectionString(ClickHouseDataContext source) => source.ConnectionString;
    }

    private sealed class ForcedPortableDialect : ClickHouseDialect
    {
        public override IExtremeRowRenderer? ExtremeRowRenderer => null;
    }
}
