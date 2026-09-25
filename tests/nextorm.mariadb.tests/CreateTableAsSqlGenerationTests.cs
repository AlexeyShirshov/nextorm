using FluentAssertions;
using NextORM.Core;

namespace NextORM.MariaDb.Tests;

/// <summary>
/// SQL generation of the <c>CREATE TABLE ... AS SELECT</c> terminals on MariaDB (no database connection):
/// MariaDB shares the MySQL dialect, so the column list is accepted while <c>ON COMMIT</c> and
/// <c>WITH NO DATA</c> are not.
/// </summary>
public class CreateTableAsSqlGenerationTests
{
    [Fact]
    public void TempTable_ShouldRenderCreateTemporaryTableAsSelect()
    {
        using var ctx = MariaDbTestContext.Create();

        ctx.From<ISimpleEntity>()
            .ToTempTableSql("recent_ids")
            .Should().Be("create temporary table recent_ids as select id from simple_entity");
    }

    [Fact]
    public void TempTableSource_ShouldRenderDropCreateAndSelectAsOneBatch()
    {
        using var ctx = MariaDbTestContext.Create();

        var source = ctx.From<ISimpleEntity>().Select(x => new { x.Id }).AsTempTable();

        var sql = ctx.From(source).Select(t => new { Id = t.GetInt32("id") }).ToBatchSql();

        sql.Should().Contain("drop table if exists " + source.Name);
        sql.Should().Contain("create temporary table " + source.Name + " as select id from simple_entity");
        sql.Should().Contain("select id from " + source.Name);
    }

    [Fact]
    public void TempTable_WithColumnList_ShouldRenderColumnList()
    {
        using var ctx = MariaDbTestContext.Create();

        ctx.From<ISimpleEntity>()
            .ToTempTableSql("recent_ids", new CreateTableOptions { Columns = ["a"] })
            .Should().Be("create temporary table recent_ids (a) as select id from simple_entity");
    }

    [Fact]
    public void TempTable_OnCommit_ShouldThrow()
    {
        using var ctx = MariaDbTestContext.Create();

        var act = () => ctx.From<ISimpleEntity>()
            .ToTempTableSql("recent_ids", new CreateTableOptions { OnCommit = TempTableOnCommit.Drop });

        act.Should().Throw<NotSupportedException>().WithMessage("*ON COMMIT*");
    }

    [Fact]
    public void TempTable_WithNoData_ShouldThrow()
    {
        using var ctx = MariaDbTestContext.Create();

        var act = () => ctx.From<ISimpleEntity>()
            .ToTempTableSql("recent_ids", new CreateTableOptions { WithData = false });

        act.Should().Throw<NotSupportedException>().WithMessage("*WITH NO DATA*");
    }

    [Fact]
    public void Dialect_ShouldReportCreateTableAsSelect()
    {
        using var ctx = MariaDbTestContext.Create();

        ((DataContext)ctx).Dialect.SupportsCreateTableAsSelect.Should().BeTrue();
        ((DataContext)ctx).Dialect.SupportsCreateTableAsSelectColumnList.Should().BeTrue();
        ((DataContext)ctx).Dialect.SupportsCreateTableAsSelectOnCommit.Should().BeFalse();
        ((DataContext)ctx).Dialect.SupportsCreateTableAsSelectWithNoData.Should().BeFalse();
    }
}
