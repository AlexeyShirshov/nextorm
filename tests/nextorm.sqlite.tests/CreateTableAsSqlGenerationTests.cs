using FluentAssertions;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// SQL generation of the <c>CREATE TABLE ... AS SELECT</c> terminals on SQLite (no database
/// connection): SQLite has no column list with <c>AS SELECT</c> and neither <c>ON COMMIT</c> nor
/// <c>WITH NO DATA</c>, so those options are rejected.
/// </summary>
public class CreateTableAsSqlGenerationTests
{
    [Fact]
    public void TempTable_ShouldRenderCreateTemporaryTableAsSelect()
    {
        using var ctx = SqliteTestContext.Create();

        ctx.From<ISimpleEntity>()
            .ToTempTableSql("recent_ids")
            .Should().Be("create temporary table recent_ids as select id from simple_entity");
    }

    [Fact]
    public void TempTable_ColumnList_ShouldThrow()
    {
        using var ctx = SqliteTestContext.Create();

        var act = () => ctx.From<ISimpleEntity>()
            .ToTempTableSql("recent_ids", new CreateTableAsOptions { Columns = ["a"] });

        act.Should().Throw<NotSupportedException>().WithMessage("*column list*");
    }

    [Fact]
    public void TempTable_OnCommit_ShouldThrow()
    {
        using var ctx = SqliteTestContext.Create();

        var act = () => ctx.From<ISimpleEntity>()
            .ToTempTableSql("recent_ids", new CreateTableAsOptions { OnCommit = TempTableOnCommit.Drop });

        act.Should().Throw<NotSupportedException>().WithMessage("*ON COMMIT*");
    }

    [Fact]
    public void TempTable_WithNoData_ShouldThrow()
    {
        using var ctx = SqliteTestContext.Create();

        var act = () => ctx.From<ISimpleEntity>()
            .ToTempTableSql("recent_ids", new CreateTableAsOptions { WithData = false });

        act.Should().Throw<NotSupportedException>().WithMessage("*WITH NO DATA*");
    }

    [Fact]
    public void TempTable_WithPredicate_ShouldCarryParametersIntoBody()
    {
        using var ctx = SqliteTestContext.Create();
        var min = 5;

        var sql = ctx.From<ISimpleEntity>()
            .Where(x => x.Id > min)
            .ToTempTableSql("recent_ids");

        sql.Should().Contain("$min");
    }

    [Fact]
    public void Dialect_ShouldReportCreateTableAsSelect()
    {
        using var ctx = SqliteTestContext.Create();

        ((DataContext)ctx).Dialect.SupportsCreateTableAsSelect.Should().BeTrue();
        ((DataContext)ctx).Dialect.SupportsCreateTableAsSelectColumnList.Should().BeFalse();
        ((DataContext)ctx).Dialect.SupportsCreateTableAsSelectOnCommit.Should().BeFalse();
        ((DataContext)ctx).Dialect.SupportsCreateTableAsSelectWithNoData.Should().BeFalse();
    }
}
