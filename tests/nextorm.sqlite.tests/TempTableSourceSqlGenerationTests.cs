using FluentAssertions;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// SQL generation of the lazy temporary-table source (<c>AsTempTable</c> / <c>From(TempTableSource)</c>)
/// on SQLite (no database connection). Every read materialises the table with a drop-and-recreate in
/// the same batch as the read, so the generated SQL is a single <c>;</c>-joined command.
/// </summary>
public class TempTableSourceSqlGenerationTests
{
    [Fact]
    public void AsTempTable_ShouldNotExecuteAndGenerateAStableName()
    {
        using var ctx = SqliteTestContext.Create();
        var min = 5;

        var source = ctx.From<ISimpleEntity>()
            .Where(x => x.Id > min)
            .Select(x => new { x.Id })
            .AsTempTable();

        source.Name.Should().StartWith("__nextorm_temp_");
        source.Options.Should().NotBeNull();

        var second = ctx.From<ISimpleEntity>().Select(x => new { x.Id }).AsTempTable();
        second.Name.Should().NotBe(source.Name);
    }

    [Fact]
    public void AsTempTable_WithBuilderOptions_ShouldCarryThem()
    {
        using var ctx = SqliteTestContext.Create();

        var source = ctx.From<ISimpleEntity>()
            .Select(x => new { x.Id })
            .AsTempTable(o => o.IfNotExists());

        source.Options.IfNotExists.Should().BeTrue();
    }

    [Fact]
    public void Read_ShouldRenderDropCreateAndSelectAsOneBatch()
    {
        using var ctx = SqliteTestContext.Create();
        var min = 5;
        var readMin = 1;

        var source = ctx.From<ISimpleEntity>()
            .Where(x => x.Id > min)
            .Select(x => new { x.Id })
            .AsTempTable();

        var sql = ctx.From(source)
            .Where(t => t.GetInt32("id") > readMin)
            .Select(t => new { Id = t.GetInt32("id") })
            .ToBatchSql();

        sql.Should().Contain("drop table if exists " + source.Name);
        sql.Should().Contain("create temporary table " + source.Name + " as select id from simple_entity");
        sql.Should().Contain("$b1_min");
        sql.Should().Contain("select id from " + source.Name);
        sql.Should().Contain("$b2_readMin");
    }

    [Fact]
    public void Read_WithQuotedIdentifiers_ShouldQuoteTheTempTableName()
    {
        using var ctx = SqliteTestContext.CreateQuoted();

        var source = ctx.From<ISimpleEntity>().Select(x => new { x.Id }).AsTempTable();

        var sql = ctx.From(source).Select(t => new { Id = t.GetInt32("id") }).ToBatchSql();

        sql.Should().Contain("drop table if exists \"" + source.Name + "\"");
        sql.Should().Contain("create temporary table \"" + source.Name + "\" as select \"id\" from \"simple_entity\"");
    }

    [Fact]
    public void Read_InsideACte_ShouldMaterialiseTheTempTableFirst()
    {
        using var ctx = SqliteTestContext.Create();

        var source = ctx.From<ISimpleEntity>().Select(x => new { x.Id }).AsTempTable();

        var sql = ctx.With("ids", ctx.From(source).Select(t => new { Id = t.GetInt32("id") }))
            .From("ids")
            .Select(t => new { Id = t.GetInt32("id") })
            .ToBatchSql();

        sql.Should().Contain("create temporary table " + source.Name + " as select id from simple_entity");
        sql.Should().Contain("with ids as (select id from " + source.Name + ")");
    }

    [Fact]
    public void NestedTempTables_ShouldBeCreatedInDependencyOrder()
    {
        using var ctx = SqliteTestContext.Create();

        var first = ctx.From<ISimpleEntity>().Select(x => new { x.Id }).AsTempTable();
        var second = ctx.From(first).Select(t => new { Id = t.GetInt32("id") }).AsTempTable();

        var sql = ctx.From(second).Select(t => new { Id = t.GetInt32("id") }).ToBatchSql();

        var firstIndex = sql.IndexOf("create temporary table " + first.Name, StringComparison.Ordinal);
        var secondIndex = sql.IndexOf("create temporary table " + second.Name, StringComparison.Ordinal);

        firstIndex.Should().BeGreaterThanOrEqualTo(0);
        secondIndex.Should().BeGreaterThan(firstIndex);
    }

    [Fact]
    public void ToBatchSql_OnAPlainQuery_ShouldThrow()
    {
        using var ctx = SqliteTestContext.Create();

        var act = () => ctx.From<ISimpleEntity>().Select(x => new { x.Id }).ToBatchSql();

        act.Should().Throw<InvalidOperationException>();
    }
}
