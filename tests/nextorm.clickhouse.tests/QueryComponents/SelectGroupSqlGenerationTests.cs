using NextORM.ClickHouse;
using NextORM.Core;
using NextORM.Extensions.ProviderTests;

namespace NextORM.ClickHouse.Tests.QueryComponents;

public sealed class SelectGroupSqlGenerationTests : SelectGroupSqlGenerationTestsBase
{
    protected override IDataContext CreateContext() => new ForcedPortableClickHouseContext(ClickHouseTestContext.CreateClickHouse());

    protected override IDataContext CreateQuotedContext() => new ForcedPortableClickHouseContext((ClickHouseDataContext)ClickHouseTestContext.CreateQuoted(), quoted: true);

    protected override SelectGroupSqlExpectations Expected { get; } = new(
        ThreeKeyGroupBy: """
select Category, Group2, Score, toInt32(count(Id)) as `Total`
 from Entity
 group by Category, Group2, Score
 having (toInt32(count(Id)) > 5)
""",
        GlobalHaving: """
select toInt32(count(Id)) as `Total`
 from Entity
 having (toInt32(count(Id)) > 5)
""",
        ConstantDistinct: """
select distinct 1
 from Entity
 where (Id > 5)
""",
        ConverterKey: """
select code, toInt32(count(Id)) as `Total`
 from Entity
 group by code
 having ((code is null and @state is null) or (code is not null and @state is not null and code = @state))
""",
        ReservedKeyQuoted: """
select `order`, toInt32(count(`Id`)) as `Total`
 from `Entity`
 group by `order`
""",
        TwoParameters: """
select Category, toInt32(count(Id)) as `Total`
 from Entity
 group by Category
 having (((Category is null and @skip is not null) or (Category is not null and @skip is null) or (Category is not null and @skip is not null and Category != @skip)) and (toInt32(count(Id)) > @floor))
""",
        NullableKey: """
select Score, toInt32(count(Id)) as `Total`
 from Entity
 group by Score
""",
        Ordering: """
select Category, toInt32(count(Id)) as `Total`
 from Entity
 group by Category
 having (toInt32(count(Id)) > 5)
 order by Category
"""
    );

    private sealed class ForcedPortableClickHouseContext : ClickHouseDataContext
    {
        private readonly ISqlDialect _dialect;

        public ForcedPortableClickHouseContext(ClickHouseDataContext source, bool quoted = false)
            : base(GetConnectionString(source), quoted ? new DataContextBuilder().UseQuotedIdentifiers() : new DataContextBuilder())
            => _dialect = new ForcedPortableDialect();

        public override ISqlDialect Dialect => _dialect;

        private static string GetConnectionString(ClickHouseDataContext source) => source.ConnectionString;
    }

    private sealed class ForcedPortableDialect : ClickHouseDialect
    {
        public override IExtremeRowRenderer? ExtremeRowRenderer => null;
    }
}
