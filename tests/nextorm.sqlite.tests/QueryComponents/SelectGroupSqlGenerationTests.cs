using NextORM.Sqlite;
using NextORM.Core;
using NextORM.Extensions.ProviderTests;

namespace NextORM.Sqlite.Tests.QueryComponents;

public sealed class SelectGroupSqlGenerationTests : SelectGroupSqlGenerationTestsBase
{
    protected override IDataContext CreateContext() => SqliteTestContext.Create();

    protected override IDataContext CreateQuotedContext() => SqliteTestContext.CreateQuoted();

    protected override SelectGroupSqlExpectations Expected { get; } = new(
        ThreeKeyGroupBy: """
select Category, Group2, Score, count(Id) as 'Total'
 from Entity
 group by Category, Group2, Score
 having (count(Id) > 5)
""",
        GlobalHaving: """
select count(Id) as 'Total'
 from Entity
 having (count(Id) > 5)
""",
        ConstantDistinct: """
select distinct 1
 from Entity
 where (Id > 5)
""",
        ConverterKey: """
select code, count(Id) as 'Total'
 from Entity
 group by code
 having ((code is null and $state is null) or (code is not null and $state is not null and code = $state))
""",
        ReservedKeyQuoted: """
select "order", count("Id") as 'Total'
 from "Entity"
 group by "order"
""",
        TwoParameters: """
select Category, count(Id) as 'Total'
 from Entity
 group by Category
 having (((Category is null and $skip is not null) or (Category is not null and $skip is null) or (Category is not null and $skip is not null and Category != $skip)) and (count(Id) > $floor))
""",
        NullableKey: """
select Score, count(Id) as 'Total'
 from Entity
 group by Score
""",
        Ordering: """
select Category, count(Id) as 'Total'
 from Entity
 group by Category
 having (count(Id) > 5)
 order by Category
"""
    );
}
