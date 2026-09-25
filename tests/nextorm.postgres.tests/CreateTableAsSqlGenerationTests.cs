using FluentAssertions;
using NextORM.Core;

namespace NextORM.Postgres.Tests;

/// <summary>
/// SQL generation of the <c>CREATE TABLE ... AS SELECT</c> terminals on PostgreSQL (no database
/// connection): the temporary/persistent prefix, the column list, <c>ON COMMIT</c> and
/// <c>WITH NO DATA</c>.
/// </summary>
public class CreateTableAsSqlGenerationTests
{
    [Fact]
    public void TempTable_ShouldRenderCreateTemporaryTableAsSelect()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.From<ISimpleEntity>()
            .ToTempTableSql("recent_ids")
            .Should().Be("create temporary table recent_ids as select id from simple_entity");
    }

    [Fact]
    public void Table_ShouldRenderCreateTableAsSelect()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.From<ISimpleEntity>()
            .ToTableSql("archive_ids")
            .Should().Be("create table archive_ids as select id from simple_entity");
    }

    [Fact]
    public void TempTable_WithOptions_ShouldRenderColumnListOnCommitAndNoData()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.From<ISimpleEntity>()
            .ToTempTableSql("recent_ids", new CreateTableOptions
            {
                IfNotExists = true,
                Columns = ["a", "b"],
                OnCommit = TempTableOnCommit.Drop,
                WithData = false,
            })
            .Should().Be("create temporary table if not exists recent_ids (a, b) on commit drop as select id from simple_entity with no data");
    }

    [Fact]
    public void TempTable_WithBuilderOptions_ShouldMatchRecordOptions()
    {
        using var ctx = PostgresTestContext.Create();

        var sql = ctx.From<ISimpleEntity>()
            .ToTempTableSql("recent_ids", o => o
                .IfNotExists()
                .Columns("a", "b")
                .OnCommit(TempTableOnCommit.Drop)
                .WithData(false));

        sql.Should().Be("create temporary table if not exists recent_ids (a, b) on commit drop as select id from simple_entity with no data");
    }

    [Fact]
    public void TempTable_QuotedIdentifiers_ShouldQuoteTargetAndColumns()
    {
        using var ctx = PostgresTestContext.CreateQuoted();

        ctx.From<ISimpleEntity>()
            .ToTempTableSql("recent ids")
            .Should().Be("create temporary table \"recent ids\" as select \"id\" from \"simple_entity\"");
    }

    [Fact]
    public void TempTable_WithPredicate_ShouldCarryParametersIntoBody()
    {
        using var ctx = PostgresTestContext.Create();
        var min = 5;

        var sql = ctx.From<ISimpleEntity>()
            .Where(x => x.Id > min)
            .ToTempTableSql("recent_ids");

        sql.Should().StartWith("create temporary table recent_ids as select id from simple_entity");
        sql.Should().Contain("@min");
    }

    [Fact]
    public void Dialect_ShouldReportCreateTableAsSelect()
    {
        using var ctx = PostgresTestContext.Create();
        var dialect = DialectOf(ctx);

        dialect.SupportsCreateTableAsSelect.Should().BeTrue();
        dialect.SupportsCreateTableAsSelectColumnList.Should().BeTrue();
        dialect.SupportsCreateTableAsSelectOnCommit.Should().BeTrue();
        dialect.SupportsCreateTableAsSelectWithNoData.Should().BeTrue();
    }

    [Fact]
    public void TempTable_WithCteBody_ShouldPlaceWithInsideTheQuery()
    {
        using var ctx = PostgresTestContext.Create();
        var cte = ctx.From<ISimpleEntity>().Select(x => new { x.Id });

        var sql = ctx.With("recent", cte)
            .From("recent")
            .Select(t => new { Id = t["id"].AsInt })
            .ToTempTableSql("recent_ids");

        sql.Should().StartWith("create temporary table recent_ids as with recent as ");
        sql.Should().Contain(") select id from recent");
        sql.Should().NotContain(") create temporary table");
    }

    private static ISqlDialect DialectOf(IDataContext ctx) => ((DataContext)ctx).Dialect;
}
