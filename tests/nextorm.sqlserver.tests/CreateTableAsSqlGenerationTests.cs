using FluentAssertions;
using NextORM.Core;

namespace NextORM.SqlServer.Tests;

/// <summary>
/// SQL generation of the materialisation terminals on SQL Server (no database connection): the
/// <c>SELECT ... INTO</c> form, the absence of a temporary form and the rejected options.
/// </summary>
public class CreateTableAsSqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    [Fact]
    public void Table_ShouldRenderSelectInto()
    {
        using var ctx = SqlServerTestContext.Create();

        ctx.From<ISimpleEntity>()
            .ToTableSql("archive_ids")
            .Should().Be("select id into archive_ids from simple_entity");
    }

    [Fact]
    public void Table_WithPredicate_ShouldCarryParametersIntoBody()
    {
        using var ctx = SqlServerTestContext.Create();
        var min = 5;

        var sql = ctx.From<ISimpleEntity>()
            .Where(x => x.Id > min)
            .ToTableSql("recent_ids");

        Normalize(sql).Should().Be("select id into recent_ids from simple_entity\n where (id > @min)");
    }

    [Fact]
    public void Table_WithCteBody_ShouldPlaceWithBeforeSelectInto()
    {
        using var ctx = SqlServerTestContext.Create();
        var cte = ctx.From<ISimpleEntity>().Select(x => new { x.Id });

        var sql = ctx.With("recent", cte)
            .From("recent")
            .Select(t => new { Id = t["id"].AsInt })
            .ToTableSql("recent_ids");

        sql.Should().StartWith("with recent as ");
        sql.Should().Contain(") select id into recent_ids from recent");
    }

    [Fact]
    public void Table_WithOrderBy_ShouldKeepOrderByAfterInto()
    {
        using var ctx = SqlServerTestContext.Create();

        Normalize(ctx.From<ISimpleEntity>()
            .OrderBy(x => x.Id)
            .ToTableSql("archive_ids"))
            .Should().Be("select id into archive_ids from simple_entity\n order by id");
    }

    [Fact]
    public void Table_WithKeywordCaseUpper_ShouldKeepKeywordCaseConsistent()
    {
        using var ctx = SqlServerTestContext.Create();

        Normalize(ctx.From<ISimpleEntity>()
            .WithKeywordCase(KeywordCase.Upper)
            .ToTableSql("archive_ids"))
            .Should().Be("SELECT id INTO archive_ids FROM simple_entity");
    }

    [Fact]
    public void Table_WithQuotedIdentifiers_ShouldQuoteTargetAndColumns()
    {
        using var ctx = SqlServerTestContext.Create();

        ctx.From<ISimpleEntity>()
            .WithQuotedIdentifiers(true)
            .Select(x => new { x.Id })
            .ToTableSql("archive ids")
            .Should().Be("select [id] into [archive ids] from [simple_entity]");
    }

    [Fact]
    public void TempTable_ShouldThrow()
    {
        using var ctx = SqlServerTestContext.Create();

        var act = () => ctx.From<ISimpleEntity>().ToTempTableSql("recent_ids");

        act.Should().Throw<NotSupportedException>().WithMessage("*temporary table*");
    }

    [Fact]
    public void Table_WithIfNotExists_ShouldThrow()
    {
        using var ctx = SqlServerTestContext.Create();

        var act = () => ctx.From<ISimpleEntity>()
            .ToTableSql("archive_ids", new CreateTableAsOptions { IfNotExists = true });

        act.Should().Throw<NotSupportedException>().WithMessage("*IF NOT EXISTS*");
    }

    [Fact]
    public void Table_WithColumns_ShouldThrow()
    {
        using var ctx = SqlServerTestContext.Create();

        var act = () => ctx.From<ISimpleEntity>()
            .ToTableSql("archive_ids", new CreateTableAsOptions { Columns = ["a"] });

        act.Should().Throw<NotSupportedException>().WithMessage("*column list*");
    }

    [Fact]
    public void Dialect_ShouldReportSelectInto()
    {
        using var ctx = SqlServerTestContext.Create();
        var dialect = ((DataContext)ctx).Dialect;

        dialect.SupportsCreateTableAsSelect.Should().BeTrue();
        dialect.SupportsTemporaryCreateTableAsSelect.Should().BeFalse();
        dialect.SupportsCreateTableAsSelectIfNotExists.Should().BeFalse();
        dialect.CreateTableAsSelectUsesSelectInto.Should().BeTrue();
        dialect.SupportsCreateTableAsSelectColumnList.Should().BeFalse();
    }
}
