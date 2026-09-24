using FluentAssertions;
using NextORM.Core;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// ClickHouse materialises a query into a <em>persistent</em> table with an explicit engine
/// (<c>CREATE TABLE ... ENGINE = MergeTree ORDER BY tuple() AS SELECT</c>); a temporary table takes an
/// explicit column list and no <c>AS SELECT</c>, so <c>ToTempTable</c> is rejected.
/// </summary>
public class CreateTableAsSqlGenerationTests
{
    [Fact]
    public void Table_ShouldRenderCreateTableWithEngineAsSelect()
    {
        using var ctx = ClickHouseTestContext.Create();

        ctx.From<ISimpleEntity>()
            .ToTableSql("archive_ids")
            .Should().Be("create table archive_ids engine = MergeTree order by tuple() as select id from simple_entity");
    }

    [Fact]
    public void Table_WithIfNotExists_ShouldRenderIfNotExists()
    {
        using var ctx = ClickHouseTestContext.Create();

        ctx.From<ISimpleEntity>()
            .ToTableSql("archive_ids", new CreateTableAsOptions { IfNotExists = true })
            .Should().Be("create table if not exists archive_ids engine = MergeTree order by tuple() as select id from simple_entity");
    }

    [Fact]
    public void TempTableSql_ShouldThrow()
    {
        using var ctx = ClickHouseTestContext.Create();

        var act = () => ctx.From<ISimpleEntity>().ToTempTableSql("recent_ids");

        act.Should().Throw<NotSupportedException>().WithMessage("*temporary table*");
    }

    [Fact]
    public void TempTable_ShouldThrow()
    {
        using var ctx = ClickHouseTestContext.Create();

        var act = () => ctx.From<ISimpleEntity>().ToTempTable("recent_ids");

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Table_WithColumns_ShouldThrow()
    {
        using var ctx = ClickHouseTestContext.Create();

        var act = () => ctx.From<ISimpleEntity>()
            .ToTableSql("archive_ids", new CreateTableAsOptions { Columns = ["a"] });

        act.Should().Throw<NotSupportedException>().WithMessage("*column list*");
    }

    [Fact]
    public void Dialect_ShouldReportPersistentCreateTableAsSelectOnly()
    {
        using var ctx = ClickHouseTestContext.Create();
        var dialect = ((DataContext)ctx).Dialect;

        dialect.SupportsCreateTableAsSelect.Should().BeTrue();
        dialect.SupportsTemporaryCreateTableAsSelect.Should().BeFalse();
        dialect.SupportsCreateTableAsSelectIfNotExists.Should().BeTrue();
        dialect.CreateTableAsSelectUsesSelectInto.Should().BeFalse();
        dialect.SupportsCreateTableAsSelectColumnList.Should().BeFalse();
        dialect.SupportsCreateTableAsSelectOnCommit.Should().BeFalse();
        dialect.SupportsCreateTableAsSelectWithNoData.Should().BeFalse();
    }
}
