using FluentAssertions;
using NextORM.ClickHouse;
using NextORM.Core;

namespace NextORM.Postgres.Tests;

/// <summary>
/// T323 (#323) D1: PostgreSQL generation parity between a <c>SelectGroup</c> projection and the
/// equivalent explicit <c>GroupBy</c>/<c>Select</c> form, the key-inference shape rules that are
/// observable in SQL, and the composition guards. Only preparation runs: the context carries a
/// placeholder connection, so no database is opened.
/// </summary>
public class T323SelectGroupSqlTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
    {
        return (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);
    }

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd) => Normalize(Prepare(ctx, cmd).DbCommand.CommandText);

    /// <summary>
    /// Extracts the rendered <c>GROUP BY</c> line. Assertions on the key list must not be satisfied by
    /// an aggregate's argument in the select list, which precedes the clause.
    /// </summary>
    private static string GroupByLine(string sql)
    {
        var index = sql.IndexOf("group by ", StringComparison.Ordinal);
        index.Should().BeGreaterThanOrEqualTo(0, "the rendered SQL must carry a GROUP BY clause");
        var end = sql.IndexOf('\n', index);
        return end < 0 ? sql[index..] : sql[index..end];
    }

    [Fact]
    public void SingleKey_ShouldEqualExplicitSql()
    {
        using var ctx = PostgresTestContext.Create();

        var inferred = ctx.From<T323LegacyEntity>()
            .SelectGroup(x => new { x.Id, C = SqlFunctions.Sql.count() });
        var explicitForm = ctx.From<T323LegacyEntity>()
            .GroupBy(x => x.Id)
            .Select(x => new { x.Id, C = SqlFunctions.Sql.count() });

        SqlOf(ctx, inferred).Should().Be(SqlOf(ctx, explicitForm));
    }

    [Fact]
    public void TwoKeysTwoAggregates_ShouldEqualExplicitSql()
    {
        using var ctx = PostgresTestContext.Create();

        var inferred = ctx.From<T323LegacyEntity>()
            .SelectGroup(x => new { x.Id, x.Group2, S = SqlFunctions.Sql.sum(x.Amount), C = SqlFunctions.Sql.count() });
        var explicitForm = ctx.From<T323LegacyEntity>()
            .GroupBy(x => new { x.Id, x.Group2 })
            .Select(x => new { x.Id, x.Group2, S = SqlFunctions.Sql.sum(x.Amount), C = SqlFunctions.Sql.count() });

        SqlOf(ctx, inferred).Should().Be(SqlOf(ctx, explicitForm));
    }

    [Fact]
    public void DateTruncAndSum_ShouldEqualExplicitSql()
    {
        using var ctx = PostgresTestContext.Create();

        var inferred = ctx.From<T323LegacyEntity>()
            .SelectGroup(x => new
            {
                Q = SqlFunctions.Sql.date_trunc("quarter", x.OrderedAt),
                S = SqlFunctions.Sql.sum(x.Amount),
            });
        var explicitForm = ctx.From<T323LegacyEntity>()
            .GroupBy(x => SqlFunctions.Sql.date_trunc("quarter", x.OrderedAt))
            .Select(x => new
            {
                Q = SqlFunctions.Sql.date_trunc("quarter", x.OrderedAt),
                S = SqlFunctions.Sql.sum(x.Amount),
            });

        SqlOf(ctx, inferred).Should().Be(SqlOf(ctx, explicitForm));
    }

    [Fact]
    public void JoinedAggregate_ShouldEqualExplicitSql()
    {
        using var ctx = PostgresTestContext.Create();
        var joined = ctx.From<T323LegacyEntity>().Join(ctx.From<IComplexEntity>(), (a, b) => a.Id == b.Id);

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
        using var ctx = PostgresTestContext.Create();
        var joined = ctx.From<T323LegacyEntity>().Join(ctx.From<IComplexEntity>(), (a, b) => a.Id == b.Id);

        var inferred = joined
            .SelectGroup(p => new { p.Item2.String, S = SqlFunctions.Sql.sum(p.Item1.Amount) });
        var explicitForm = joined
            .GroupBy(p => p.Item2.String)
            .Select(p => new { p.Item2.String, S = SqlFunctions.Sql.sum(p.Item1.Amount) });

        SqlOf(ctx, inferred).Should().Be(SqlOf(ctx, explicitForm));
    }

    [Fact]
    public void OrderOfKeys_ShouldFollowFirstAppearance()
    {
        using var ctx = PostgresTestContext.Create();

        // The aggregate comes first in the anonymous shape, so the keys appear in the order the row
        // expressions are visited: Group2, then Id.
        var inferred = ctx.From<T323LegacyEntity>()
            .SelectGroup(x => new { C = SqlFunctions.Sql.count(), G = x.Group2, I = x.Id });
        var sameOrder = ctx.From<T323LegacyEntity>()
            .GroupBy(x => new { x.Group2, x.Id })
            .Select(x => new { C = SqlFunctions.Sql.count(), G = x.Group2, I = x.Id });
        var reversed = ctx.From<T323LegacyEntity>()
            .GroupBy(x => new { x.Id, x.Group2 })
            .Select(x => new { C = SqlFunctions.Sql.count(), G = x.Group2, I = x.Id });

        var inferredSql = SqlOf(ctx, inferred);
        var sameOrderSql = SqlOf(ctx, sameOrder);

        inferredSql.Should().Be(sameOrderSql);
        GroupByLine(inferredSql).Should().Be(GroupByLine(sameOrderSql));
        GroupByLine(inferredSql).Should().NotBe(GroupByLine(SqlOf(ctx, reversed)));
    }

    [Fact]
    public void AggregateArguments_ShouldNotBecomeKeys()
    {
        using var ctx = PostgresTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<T323LegacyEntity>()
            .SelectGroup(x => new { K = x.Id, V = SqlFunctions.Sql.sum(x.Amount) }));

        var keys = GroupByLine(sql);
        keys.Should().ContainEquivalentOf("id");
        keys.Should().NotContainEquivalentOf("amount", "an aggregate's argument is opaque and never a grouping key");
    }

    [Fact]
    public void Guards_ShouldRejectExplicitGrouping()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<T323LegacyEntity>();

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
        FluentActions.Invoking(() => e.SelectGroup<int>(null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void IdentityProjection_ShouldBeRejected()
    {
        using var ctx = PostgresTestContext.Create();

        FluentActions.Invoking(() => SqlOf(ctx, ctx.From<T323LegacyEntity>().SelectGroup(x => x)))
            .Should().Throw<QueryPreparationException>()
            .WithMessage("*identity projection*");
    }

    [Fact]
    public void ExplicitForm_ShouldStillRenderLegacySql()
    {
        using var ctx = PostgresTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<T323LegacyEntity>()
            .GroupBy(x => x.Id)
            .Select(x => new { x.Id, C = SqlFunctions.Sql.count() }));

        GroupByLine(sql).Should().BeEquivalentTo("group by Id");
        sql.Should().NotContain("sg_key_", "the inferred-key aliases belong to SelectGroup only");
    }
}
