using FluentAssertions;
using NextORM.Core;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// ClickHouse expresses a persistent <c>CREATE TABLE ... AS SELECT</c>, but a temporary one requires an
/// explicit column list and cannot take <c>AS SELECT</c>, so the materialisation terminals are rejected.
/// </summary>
public class CreateTableAsSqlGenerationTests
{
    [Fact]
    public void TempTableSql_ShouldThrow()
    {
        using var ctx = ClickHouseTestContext.Create();

        var act = () => ctx.From<ISimpleEntity>().ToTempTableSql("recent_ids");

        act.Should().Throw<NotSupportedException>().WithMessage("*CREATE TABLE ... AS SELECT*");
    }

    [Fact]
    public void TempTable_ShouldThrow()
    {
        using var ctx = ClickHouseTestContext.Create();

        var act = () => ctx.From<ISimpleEntity>().ToTempTable("recent_ids");

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Dialect_ShouldNotReportCreateTableAsSelect()
    {
        using var ctx = ClickHouseTestContext.Create();

        ((DataContext)ctx).Dialect.SupportsCreateTableAsSelect.Should().BeFalse();
        ((DataContext)ctx).Dialect.SupportsCreateTableAsSelectColumnList.Should().BeFalse();
        ((DataContext)ctx).Dialect.SupportsCreateTableAsSelectOnCommit.Should().BeFalse();
        ((DataContext)ctx).Dialect.SupportsCreateTableAsSelectWithNoData.Should().BeFalse();
    }
}
