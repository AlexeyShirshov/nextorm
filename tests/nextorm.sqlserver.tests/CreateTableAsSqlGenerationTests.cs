using FluentAssertions;
using NextORM.Core;

namespace NextORM.SqlServer.Tests;

/// <summary>
/// SQL Server has no <c>CREATE TABLE ... AS SELECT</c> on its regular engine (it uses
/// <c>SELECT ... INTO #t</c>), which is a phase-2 form, so the materialisation terminals are rejected.
/// </summary>
public class CreateTableAsSqlGenerationTests
{
    [Fact]
    public void TempTableSql_ShouldThrow()
    {
        using var ctx = SqlServerTestContext.Create();

        var act = () => ctx.From<ISimpleEntity>().ToTempTableSql("recent_ids");

        act.Should().Throw<NotSupportedException>().WithMessage("*CREATE TABLE ... AS SELECT*");
    }

    [Fact]
    public void TempTable_ShouldThrow()
    {
        using var ctx = SqlServerTestContext.Create();

        var act = () => ctx.From<ISimpleEntity>().ToTempTable("recent_ids");

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Dialect_ShouldNotReportCreateTableAsSelect()
    {
        using var ctx = SqlServerTestContext.Create();

        ((DataContext)ctx).Dialect.SupportsCreateTableAsSelect.Should().BeFalse();
        ((DataContext)ctx).Dialect.SupportsCreateTableAsSelectColumnList.Should().BeFalse();
        ((DataContext)ctx).Dialect.SupportsCreateTableAsSelectOnCommit.Should().BeFalse();
        ((DataContext)ctx).Dialect.SupportsCreateTableAsSelectWithNoData.Should().BeFalse();
    }
}
