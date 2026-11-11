using FluentAssertions;
using NextORM.Core;
using NextORM.Extensions;

namespace NextORM.SqlServer.Tests;

/// <summary>
/// T323 (#323) D5a: exact SQL Server SQL for the <c>SelectGroup</c> projection, the two key-less modes
/// that are observable in SQL, and the composition guards. Only preparation runs: the context carries a
/// placeholder connection, so no database is opened. Every statement is asserted in full against the
/// expected SQL Server rendering; no other implementation is used as the oracle.
/// <para>
/// The <c>date_trunc</c> key cell is present: the SQL Server dialect renders
/// <c>SqlFunctions.Sql.date_trunc</c> as <c>datetrunc(part, value)</c> (<c>SupportsDateTrunc</c> is true).
/// </para>
/// </summary>
public class T323SelectGroupSqlTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
    {
        return (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);
    }

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd) => Normalize(Prepare(ctx, cmd).DbCommand.CommandText);

    /// <summary>The expected literal uses the file's line endings; generated SQL always uses LF.</summary>
    private static string Lf(string sql) => sql.ReplaceLineEndings("\n");

    [Fact]
    public void SingleKey_ExactSql()
    {
        using var ctx = SqlServerTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<T323SelectGroupEntity>()
            .SelectGroup(x => new { x.Id, C = SqlFunctions.Sql.count() }));

        sql.Should().Be(Lf("""
select Id, count(*) as [C]
 from T323SelectGroupEntity
 group by Id
"""));
    }

    [Fact]
    public void TwoKeysTwoAggregates_ExactSql()
    {
        using var ctx = SqlServerTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<T323SelectGroupEntity>()
            .SelectGroup(x => new { x.Id, x.Group2, S = SqlFunctions.Sql.sum(x.Amount), C = SqlFunctions.Sql.count() }));

        sql.Should().Be(Lf("""
select Id, Group2, sum(Amount) as [S], count(*) as [C]
 from T323SelectGroupEntity
 group by Id, Group2
"""));
    }

    [Fact]
    public void DateTruncAndSum_ExactSql()
    {
        using var ctx = SqlServerTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<T323SelectGroupEntity>()
            .SelectGroup(x => new
            {
                Q = SqlFunctions.Sql.date_trunc("quarter", x.OrderedAt),
                S = SqlFunctions.Sql.sum(x.Amount),
            }));

        sql.Should().Be(Lf("""
select datetrunc(quarter, OrderedAt) as [Q], sum(Amount) as [S]
 from T323SelectGroupEntity
 group by datetrunc(quarter, OrderedAt)
"""));
    }

    [Fact]
    public void JoinedAggregate_ExactSql()
    {
        using var ctx = SqlServerTestContext.Create();
        var joined = ctx.From<T323SelectGroupEntity>().Join(ctx.From<IComplexEntity>(), (a, b) => a.Id == b.Id);

        var sql = SqlOf(ctx, joined
            .SelectGroup(p => new { p.Item1.Id, S = SqlFunctions.Sql.sum(p.Item1.Amount) }));

        sql.Should().Be(Lf("""
select t1.Id, sum(t1.Amount) as [S]
 from T323SelectGroupEntity as [t1]
 join complex_entity as [t2] on cast(t1.Id as bigint) = t2.id
 group by t1.Id
"""));
    }

    [Fact]
    public void JoinedKeyAndAggregateAcrossItems_ExactSql()
    {
        using var ctx = SqlServerTestContext.Create();
        var joined = ctx.From<T323SelectGroupEntity>().Join(ctx.From<IComplexEntity>(), (a, b) => a.Id == b.Id);

        var sql = SqlOf(ctx, joined
            .SelectGroup(p => new { p.Item2.String, S = SqlFunctions.Sql.sum(p.Item1.Amount) }));

        sql.Should().Be(Lf("""
select t2.somestring as [String], sum(t1.Amount) as [S]
 from T323SelectGroupEntity as [t1]
 join complex_entity as [t2] on cast(t1.Id as bigint) = t2.id
 group by t2.somestring
"""));
    }

    [Fact]
    public void GlobalAggregateHaving_ShouldEmitHaving()
    {
        using var ctx = SqlServerTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<T323SelectGroupEntity>()
            .Having(x => SqlFunctions.Sql.sum(x.Amount) > 0)
            .SelectGroup(x => new { S = SqlFunctions.Sql.sum(x.Amount) }));

        sql.Should().Contain(" having ", "a global aggregate has one row to filter");
        sql.Should().Contain("sum(", "the aggregate projection is unchanged");
        sql.Should().NotContain("group by", "a global aggregate has no grouping key");
    }

    [Fact]
    public void ConstantProjection_ShouldRenderDistinct()
    {
        using var ctx = SqlServerTestContext.Create();

        var grouped = SqlOf(ctx, ctx.From<T323SelectGroupEntity>().SelectGroup(x => new { V = 1 }));

        grouped.TrimStart().StartsWith("select distinct", StringComparison.OrdinalIgnoreCase)
            .Should().BeTrue($"a key-less constant projection must collapse to one row, got: {grouped}");
        grouped.Should().NotContain("group by");

        var legacy = SqlOf(ctx, ctx.From<T323SelectGroupEntity>().Select(x => new { V = 1 }));
        legacy.Should().NotContain("distinct", "the DISTINCT rendering is gated on the SelectGroup mode");
    }

    [Fact]
    public void LegacyUngroupedHaving_ShouldStillBeDropped()
    {
        using var ctx = SqlServerTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<T323SelectGroupEntity>()
            .Having(x => x.Id > 1)
            .Select(x => new { x.Id }));

        sql.Should().NotContain("having", "an ungrouped legacy HAVING keeps its historical behaviour");
    }

    /// <summary>
    /// Every grouping modifier the builder exposes is rejected before rendering. <c>WithTotals</c> is
    /// not asserted here: it is a <c>NextORM.ClickHouse</c> extension method and this test project does
    /// not reference that package; the builder-level grouping state it sets is covered by the core and
    /// the SQLite/MySQL/ClickHouse cells.
    /// </summary>
    [Fact]
    public void Guards_ShouldRejectExplicitGrouping()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<T323SelectGroupEntity>();

        FluentActions.Invoking(() => { var _c = e.GroupBy(x => x.Id).SelectGroup(x => new { x.Id }); ctx.GetPreparedQueryCommand(_c, false, true, CancellationToken.None); })
            .Should().Throw<QueryPreparationException>().WithMessage("*cannot be combined with explicit grouping*");
        FluentActions.Invoking(() => { var _c = e.GroupByRollup(x => x.Id).SelectGroup(x => new { x.Id }); ctx.GetPreparedQueryCommand(_c, false, true, CancellationToken.None); })
            .Should().Throw<QueryPreparationException>().WithMessage("*cannot be combined with explicit grouping*");
        FluentActions.Invoking(() => { var _c = e.GroupByCube(x => x.Id).SelectGroup(x => new { x.Id }); ctx.GetPreparedQueryCommand(_c, false, true, CancellationToken.None); })
            .Should().Throw<QueryPreparationException>().WithMessage("*cannot be combined with explicit grouping*");
        FluentActions.Invoking(() => { var _c = e.GroupByGroupingSets(x => new { x.Id, x.Group2 }, [0], [1]).SelectGroup(x => new { x.Id }); ctx.GetPreparedQueryCommand(_c, false, true, CancellationToken.None); })
            .Should().Throw<QueryPreparationException>().WithMessage("*cannot be combined with explicit grouping*");
        FluentActions.Invoking(() => { var _c = ctx.From<T323SelectGroupEntity>().SelectGroup<T323SelectGroupEntity, int>(null!); ctx.GetPreparedQueryCommand(_c, false, true, CancellationToken.None); })
            .Should().Throw<ArgumentNullException>();
    }
}

/// <summary>The row shape of the D5a cells; <c>OrderedAt</c> feeds the <c>date_trunc</c> key.</summary>
public class T323SelectGroupEntity
{
    public int Id { get; set; }
    public int? Group2 { get; set; }
    public decimal Amount { get; set; }
    public string? Name { get; set; }
    public DateTime OrderedAt { get; set; }
}
