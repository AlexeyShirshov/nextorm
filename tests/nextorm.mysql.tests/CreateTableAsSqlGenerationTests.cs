using FluentAssertions;
using NextORM.Core;

namespace NextORM.MySql.Tests;

/// <summary>
/// SQL generation of the <c>CREATE TABLE ... AS SELECT</c> terminals on MySQL (no database connection):
/// the column list is accepted, <c>ON COMMIT</c> and <c>WITH NO DATA</c> are not.
/// </summary>
public class CreateTableAsSqlGenerationTests
{
    [Fact]
    public void TempTable_ShouldRenderCreateTemporaryTableAsSelect()
    {
        using var ctx = MySqlTestContext.Create();

        ctx.From<ISimpleEntity>()
            .ToTempTableSql("recent_ids")
            .Should().Be("create temporary table recent_ids as select id from simple_entity");
    }

    [Fact]
    public void TempTable_WithColumnList_ShouldRenderColumnList()
    {
        using var ctx = MySqlTestContext.Create();

        ctx.From<ISimpleEntity>()
            .ToTempTableSql("recent_ids", new CreateTableOptions { IfNotExists = true, Columns = ["a", "b"] })
            .Should().Be("create temporary table if not exists recent_ids (a, b) as select id from simple_entity");
    }

    [Fact]
    public void TempTable_OnCommit_ShouldThrow()
    {
        using var ctx = MySqlTestContext.Create();

        var act = () => ctx.From<ISimpleEntity>()
            .ToTempTableSql("recent_ids", new CreateTableOptions { OnCommit = TempTableOnCommit.Drop });

        act.Should().Throw<NotSupportedException>().WithMessage("*ON COMMIT*");
    }

    [Fact]
    public void TempTable_WithNoData_ShouldThrow()
    {
        using var ctx = MySqlTestContext.Create();

        var act = () => ctx.From<ISimpleEntity>()
            .ToTempTableSql("recent_ids", new CreateTableOptions { WithData = false });

        act.Should().Throw<NotSupportedException>().WithMessage("*WITH NO DATA*");
    }

    [Fact]
    public void TempTable_WithPredicate_ShouldCarryParametersIntoBody()
    {
        using var ctx = MySqlTestContext.Create();
        var min = 5;

        var sql = ctx.From<ISimpleEntity>()
            .Where(x => x.Id > min)
            .ToTempTableSql("recent_ids");

        sql.Should().Contain("@min");
    }

    [Fact]
    public void Dialect_ShouldReportCreateTableAsSelect()
    {
        using var ctx = MySqlTestContext.Create();

        ((DataContext)ctx).Dialect.SupportsCreateTableAsSelect.Should().BeTrue();
        ((DataContext)ctx).Dialect.SupportsCreateTableAsSelectColumnList.Should().BeTrue();
        ((DataContext)ctx).Dialect.SupportsCreateTableAsSelectOnCommit.Should().BeFalse();
        ((DataContext)ctx).Dialect.SupportsCreateTableAsSelectWithNoData.Should().BeFalse();
    }
}
