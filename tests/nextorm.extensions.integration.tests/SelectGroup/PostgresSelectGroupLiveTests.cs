using FluentAssertions;
using NextORM.Core;
using NextORM.Extensions;
using NextORM.Postgres;

namespace NextORM.Integration.Tests;

[Collection("Postgres")]
public sealed class PostgresSelectGroupLiveTests : SelectGroupLiveTestsBase
{
    protected override ITestProvider Provider => PostgresTestProvider.Instance;

    [Fact]
    public void SelectGroupHaving_MemberInitializer_BindsGroupedKeyAndAggregate()
    {
        var rows = _sut.DataProvider.From<ExtremaEntity>()
            .SelectGroupHaving(
                row => new GroupSummary { Key = row.Category, Rows = SqlFunctions.Sql.count(row.Id) },
                group => (group.Key == "a" || group.Key == null) && group.Rows > 1)
            .ToList();

        rows.Select(group => (group.Key, group.Rows))
            .Should().BeEquivalentTo(new (string?, int)[] { ("a", 4), (null, 2) });
    }

    public sealed class GroupSummary
    {
        public string? Key { get; set; }
        public int Rows { get; set; }
    }
}
