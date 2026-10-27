using FluentAssertions;
using NextORM.ClickHouse;
using NextORM.Core;

namespace NextORM.MySql.Tests;

/// <summary>
/// T323 (#323) D5a: MySQL generation parity between a <c>SelectGroup</c> projection and the
/// equivalent explicit <c>GroupBy</c>/<c>Select</c> form, the two key-less modes that are observable
/// in SQL, and the composition guards. Only preparation runs: the context carries a placeholder
/// connection, so no database is opened.
/// <para>
/// The <c>date_trunc</c> key cell is absent for this provider: the MySQL dialect cannot render
/// <c>SqlFunctions.Sql.date_trunc</c> (<c>SupportsDateTrunc</c> stays at its false default), so the
/// cell would be an unsupported render rather than a parity assertion.
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

    [Fact]
    public void SingleKey_ShouldEqualExplicitSql()
    {
        using var ctx = MySqlTestContext.Create();

        var inferred = ctx.From<T323SelectGroupEntity>()
            .SelectGroup(x => new { x.Id, C = SqlFunctions.Sql.count() });
        var explicitForm = ctx.From<T323SelectGroupEntity>()
            .GroupBy(x => x.Id)
            .Select(x => new { x.Id, C = SqlFunctions.Sql.count() });

        SqlOf(ctx, inferred).Should().Be(SqlOf(ctx, explicitForm));
    }

    [Fact]
    public void TwoKeysTwoAggregates_ShouldEqualExplicitSql()
    {
        using var ctx = MySqlTestContext.Create();

        var inferred = ctx.From<T323SelectGroupEntity>()
            .SelectGroup(x => new { x.Id, x.Group2, S = SqlFunctions.Sql.sum(x.Amount), C = SqlFunctions.Sql.count() });
        var explicitForm = ctx.From<T323SelectGroupEntity>()
            .GroupBy(x => new { x.Id, x.Group2 })
            .Select(x => new { x.Id, x.Group2, S = SqlFunctions.Sql.sum(x.Amount), C = SqlFunctions.Sql.count() });

        SqlOf(ctx, inferred).Should().Be(SqlOf(ctx, explicitForm));
    }

    [Fact]
    public void JoinedAggregate_ShouldEqualExplicitSql()
    {
        using var ctx = MySqlTestContext.Create();
        var joined = ctx.From<T323SelectGroupEntity>().Join(ctx.From<IComplexEntity>(), (a, b) => a.Id == b.Id);

        var inferred = joined
            .SelectGroup(p => new { p.Item1.Id, S = SqlFunctions.Sql.sum(p.Item1.Amount) });
        var explicitForm = joined
            .GroupBy(p => p.Item1.Id)
            .Select(p => new { p.Item1.Id, S = SqlFunctions.Sql.sum(p.Item1.Amount) });

        SqlOf(ctx, inferred).Should().Be(SqlOf(ctx, explicitForm));
    }

    [Fact]
    public void JoinedKeyAndAggregateAcrossItems_ShouldEqualExplicitSql()
    {
        using var ctx = MySqlTestContext.Create();
        var joined = ctx.From<T323SelectGroupEntity>().Join(ctx.From<IComplexEntity>(), (a, b) => a.Id == b.Id);

        var inferred = joined
            .SelectGroup(p => new { p.Item2.String, S = SqlFunctions.Sql.sum(p.Item1.Amount) });
        var explicitForm = joined
            .GroupBy(p => p.Item2.String)
            .Select(p => new { p.Item2.String, S = SqlFunctions.Sql.sum(p.Item1.Amount) });

        SqlOf(ctx, inferred).Should().Be(SqlOf(ctx, explicitForm));
    }

    [Fact]
    public void GlobalAggregateHaving_ShouldEmitHaving()
    {
        using var ctx = MySqlTestContext.Create();

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
        using var ctx = MySqlTestContext.Create();

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
        using var ctx = MySqlTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<T323SelectGroupEntity>()
            .Having(x => x.Id > 1)
            .Select(x => new { x.Id }));

        sql.Should().NotContain("having", "an ungrouped legacy HAVING keeps its historical behaviour");
    }

    [Fact]
    public void Guards_ShouldRejectExplicitGrouping()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<T323SelectGroupEntity>();

        FluentActions.Invoking(() => e.GroupBy(x => x.Id).SelectGroup(x => new { x.Id }))
            .Should().Throw<QueryPreparationException>().WithMessage("*cannot be combined with explicit grouping*");
        FluentActions.Invoking(() => e.GroupByRollup(x => x.Id).SelectGroup(x => new { x.Id }))
            .Should().Throw<QueryPreparationException>().WithMessage("*cannot be combined with explicit grouping*");
        FluentActions.Invoking(() => e.GroupByCube(x => x.Id).SelectGroup(x => new { x.Id }))
            .Should().Throw<QueryPreparationException>().WithMessage("*cannot be combined with explicit grouping*");
        FluentActions.Invoking(() => e.GroupByGroupingSets(x => new { x.Id, x.Group2 }, [0], [1]).SelectGroup(x => new { x.Id }))
            .Should().Throw<QueryPreparationException>().WithMessage("*cannot be combined with explicit grouping*");
        FluentActions.Invoking(() => e.WithTotals().SelectGroup(x => new { x.Id }))
            .Should().Throw<QueryPreparationException>().WithMessage("*cannot be combined with explicit grouping*");
        FluentActions.Invoking(() => ctx.From<T323SelectGroupEntity>().SelectGroup<int>(null!))
            .Should().Throw<ArgumentNullException>();
    }
}

/// <summary>The row shape of the D5a parity cells; <c>OrderedAt</c> feeds the <c>date_trunc</c> key.</summary>
public class T323SelectGroupEntity
{
    public int Id { get; set; }
    public int? Group2 { get; set; }
    public decimal Amount { get; set; }
    public string? Name { get; set; }
    public DateTime OrderedAt { get; set; }
}
