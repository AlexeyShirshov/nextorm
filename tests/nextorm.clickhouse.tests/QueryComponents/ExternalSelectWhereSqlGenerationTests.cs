using NextORM.ClickHouse;
using NextORM.Core;
using NextORM.QueryComponents.ProviderTests;

namespace NextORM.ClickHouse.Tests.QueryComponents;

public sealed class ExternalSelectWhereSqlGenerationTests : ExternalSelectWhereSqlGenerationTestsBase
{
    protected override IDataContext CreateContext() => ClickHouseTestContext.Create();

    protected override IDataContext CreateBuiltInReferenceContext()
        => new ForcedPortableClickHouseContext(ClickHouseTestContext.CreateClickHouse());

    private sealed class ForcedPortableClickHouseContext : ClickHouseDataContext
    {
        private readonly ISqlDialect _dialect;

        public ForcedPortableClickHouseContext(ClickHouseDataContext source)
            : base(GetConnectionString(source), new DataContextBuilder())
            => _dialect = new ForcedPortableDialect();

        public override ISqlDialect Dialect => _dialect;

        private static string GetConnectionString(ClickHouseDataContext source) => "Host=localhost;Port=8123;Username=default;Password=nextorm;Database=nextorm";
    }

    private sealed class ForcedPortableDialect : ClickHouseDialect
    {
        public override IExtremeRowRenderer? ExtremeRowRenderer => null;
    }
}
