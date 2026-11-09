using System.Linq.Expressions;
using FluentAssertions;
using NextORM.ClickHouse;
using NextORM.Core;
using NextORM.Extensions;
using NextORM.Sqlite;
using Xunit;

namespace NextORM.Extensions.Tests;

public class D362ValidationMatrixTests
{
    private sealed class E
    {
        public long Id { get; set; }
        public int? Score { get; set; }
        public string? Name { get; set; }
        public string? Category { get; set; }
        public decimal Amount { get; set; }
        public int Group2 { get; set; }
    }

    private sealed class Other
    {
        public long Id { get; set; }
        public int Value { get; set; }
    }

    private sealed class IdRow
    {
        public long Id { get; set; }
        public int? Unbound { get; set; }
    }

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static void Reject(IDataContext ctx, Action act)
    {
        FluentActions.Invoking(act).Should().Throw<QueryPreparationException>();
    }

    // 1. Every explicit grouping modifier is rejected by the package from the raw snapshot, with the
    //    concrete exception type and the shared message; each modifier has its own fact so a regression
    //    names the modifier that stopped being rejected.
    private static void RejectExplicitGrouping(IDataContext ctx, Func<EntityBuilder<E>, QueryCommand<object>> build)
    {
        var e = ctx.From<E>();
        var cmd = build(e);
        FluentActions.Invoking(() => ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None))
            .Should().Throw<QueryPreparationException>()
            .WithMessage("*cannot be combined with explicit grouping*");
    }

    [Fact]
    public void GroupBy_Rejected()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        RejectExplicitGrouping(ctx, e => SelectGroupExtensions.SelectGroup(e.GroupBy(x => x.Id), (Expression<Func<E, object>>)(x => x.Id)));
    }

    [Fact]
    public void GroupByRollup_Rejected()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        RejectExplicitGrouping(ctx, e => SelectGroupExtensions.SelectGroup(e.GroupByRollup(x => x.Id), (Expression<Func<E, object>>)(x => x.Id)));
    }

    [Fact]
    public void GroupByCube_Rejected()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        RejectExplicitGrouping(ctx, e => SelectGroupExtensions.SelectGroup(e.GroupByCube(x => x.Id), (Expression<Func<E, object>>)(x => x.Id)));
    }

    [Fact]
    public void GroupByGroupingSets_Rejected()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        RejectExplicitGrouping(ctx, e => SelectGroupExtensions.SelectGroup(e.GroupByGroupingSets(x => new { x.Id, x.Group2 }, [0], [1]), (Expression<Func<E, object>>)(x => x.Id)));
    }

    [Fact]
    public void GroupByWithTotals_Rejected()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        RejectExplicitGrouping(ctx, e => SelectGroupExtensions.SelectGroup(e.WithTotals(), (Expression<Func<E, object>>)(x => x.Id)));
    }

    // 2. window in projection rejected
    [Fact]
    public void Window_InProjection_Rejected()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<E>();
        Reject(ctx, () => { var c = SelectGroupExtensions.SelectGroup(e, (Expression<Func<E, object>>)(x => new { R = SqlFunctions.Sql.row_number().Over() })); ctx.GetPreparedQueryCommand(c, false, true, CancellationToken.None); });
    }

    // 3. null args
    [Fact]
    public void NullProjection_Throws()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<E>();
        FluentActions.Invoking(() => SelectGroupExtensions.SelectGroup(e, (Expression<Func<E, object>>)null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void NullCondition_Throws()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<E>();
        FluentActions.Invoking(() => SelectGroupExtensions.SelectGroupHaving(e, (Expression<Func<E, object>>)(x => x.Id), null!))
            .Should().Throw<ArgumentNullException>();
    }

    // 4. HAVING member the projection does not bind -> rejected
    [Fact]
    public void Having_UncoveredRowMember_Rejected()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<E>();
        Reject(ctx, () => { var c = SelectGroupExtensions.SelectGroupHaving(e, (Expression<Func<E, IdRow>>)(x => new IdRow { Id = x.Id }), r => r.Unbound > 5); ctx.GetPreparedQueryCommand(c, false, true, CancellationToken.None); });
    }

    // 5. HAVING using whole key -> valid
    [Fact]
    public void Having_WholeKey_Valid()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<E>();
        var cmd = SelectGroupExtensions.SelectGroupHaving(e, (Expression<Func<E, long>>)(x => x.Id), f => f > 5);
        var sql = Normalize(Prepare(ctx, cmd).SqlStmt ?? string.Empty);
        sql.Should().Contain("having");
    }

    // 6. identity projection rejected
    [Fact]
    public void IdentityProjection_Rejected()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<E>();
        Reject(ctx, () => { var c = SelectGroupExtensions.SelectGroup(e, (Expression<Func<E, object>>)(x => x)); ctx.GetPreparedQueryCommand(c, false, true, CancellationToken.None); });
    }

    // 7. non-scalar key rejected
    [Fact]
    public void NonScalarKey_Rejected()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<E>();
        Reject(ctx, () => { var c = SelectGroupExtensions.SelectGroup(e, (Expression<Func<E, object>>)(x => new { x })); ctx.GetPreparedQueryCommand(c, false, true, CancellationToken.None); });
    }

    // 8. condition does not enlarge key set
    [Fact]
    public void Condition_DoesNotEnlargeKeys()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<E>();
        // A projected non-key member (an aggregate) can be narrowed by the condition without joining GROUP BY.
        var cmd = SelectGroupExtensions.SelectGroupHaving(
            e, x => new { x.Id, S = SqlFunctions.Sql.sum(x.Amount) }, r => r.S > 0m);
        var sql = Normalize(Prepare(ctx, cmd).SqlStmt ?? string.Empty);
        var groupBy = sql[(sql.IndexOf("group by", StringComparison.Ordinal) + "group by".Length)..];
        var having = groupBy.IndexOf(" having ", StringComparison.Ordinal);
        if (having >= 0)
            groupBy = groupBy[..having];

        groupBy.Should().Contain("Id");
        groupBy.Should().NotContain("Amount", "the aggregate member of the result never becomes a grouping key");
    }

    // 9. global aggregation with a member the projection does not bind -> rejected
    [Fact]
    public void GlobalAgg_UncoveredHaving_Rejected()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<E>();
        Reject(ctx, () => { var c = SelectGroupExtensions.SelectGroupHaving(e, (Expression<Func<E, IdRow>>)(x => new IdRow { Id = SqlFunctions.Sql.count(x.Id) }), r => r.Unbound > 5); ctx.GetPreparedQueryCommand(c, false, true, CancellationToken.None); });
    }

    // 10. captured threshold valid
    [Fact]
    public void CapturedThreshold_Valid()
    {
        int threshold = 5;
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<E>();
        var cmd = SelectGroupExtensions.SelectGroupHaving(e, (Expression<Func<E, long>>)(x => x.Id), f => f > threshold);
        var sql = Normalize(Prepare(ctx, cmd).SqlStmt ?? string.Empty);
        sql.Should().Contain("having");
    }

    // window in HAVING
    [Fact]
    public void Window_InHaving_Rejected()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<E>();
        Reject(ctx, () => { var c = SelectGroupExtensions.SelectGroup(e.Having(x => SqlFunctions.Sql.sum_over(x.Amount).Over() > 0m), (Expression<Func<E, object>>)(x => new { x.Id, S = SqlFunctions.Sql.sum(x.Amount) })); ctx.GetPreparedQueryCommand(c, false, true, CancellationToken.None); });
    }

    // window in ORDER BY
    [Fact]
    public void Window_InOrderBy_Rejected()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<E>();
        Reject(ctx, () => { var c = SelectGroupExtensions.SelectGroup(e.OrderBy(x => SqlFunctions.Sql.row_number().Over()), (Expression<Func<E, object>>)(x => new { x.Id, S = SqlFunctions.Sql.sum(x.Amount) })); ctx.GetPreparedQueryCommand(c, false, true, CancellationToken.None); });
    }

    // subquery in WHERE allowed (positive control)
    [Fact]
    public void WhereSubquery_Allowed()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<E>();
        var other = ctx.From<Other>();
        var cmd = e.Where(x => SqlFunctions.Sql.exists(other.Where(o => o.Id == x.Id)))
            .SelectGroup(x => new { x.Id, S = SqlFunctions.Sql.sum(x.Amount) });
        var sql = Normalize(Prepare(ctx, cmd).SqlStmt ?? string.Empty);
        sql.Should().Contain("exists");
    }

    // ORDER BY whole key valid
    [Fact]
    public void OrderBy_WholeKey_Valid()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<E>();
        var cmd = SelectGroupExtensions.SelectGroup(e.OrderBy(x => x.Id), (Expression<Func<E, long>>)(x => x.Id));
        var sql = Normalize(Prepare(ctx, cmd).SqlStmt ?? string.Empty);
        sql.Should().Contain("order by");
    }

    // ORDER BY raw non-key column rejected
    [Fact]
    public void OrderBy_RawColumn_Rejected()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<E>();
        Reject(ctx, () => { var c = SelectGroupExtensions.SelectGroup(e.OrderBy(x => x.Amount), (Expression<Func<E, object>>)(x => new { x.Id, S = SqlFunctions.Sql.sum(x.Amount) })); ctx.GetPreparedQueryCommand(c, false, true, CancellationToken.None); });
    }

    // ordinal ORDER BY bounds
    [Fact]
    public void OrderByOrdinal_InBounds_Valid()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<E>();
        var cmd = SelectGroupExtensions.SelectGroup(e.OrderBy(1), (Expression<Func<E, long>>)(x => x.Id));
        var sql = Normalize(Prepare(ctx, cmd).SqlStmt ?? string.Empty);
        sql.Should().Contain("select");
    }

    [Fact]
    public void OrderByOrdinal_Zero_Rejected()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<E>();
        FluentActions.Invoking(() => e.OrderBy(0)).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void OrderByOrdinal_Negative_Rejected()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<E>();
        FluentActions.Invoking(() => e.OrderBy(-1)).Should().Throw<ArgumentException>();
    }


    // subquery in projection -> rejected
    [Fact]
    public void Subquery_InProjection_Rejected()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<E>();
        var other = ctx.From<Other>();
        Reject(ctx, () => { var c = SelectGroupExtensions.SelectGroup(e, (Expression<Func<E, object>>)(x => new { x.Id, V = other.Where(o => o.Id == x.Id).Select(o => o.Value).FirstOrDefault() })); ctx.GetPreparedQueryCommand(c, false, true, CancellationToken.None); });
    }

    // subquery in HAVING -> rejected
    [Fact]
    public void Subquery_InHaving_Rejected()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<E>();
        var other = ctx.From<Other>();
        Reject(ctx, () => { var c = SelectGroupExtensions.SelectGroupHaving(e, x => new { x.Id, S = SqlFunctions.Sql.sum(x.Amount) }, r => SqlFunctions.Sql.exists(other.Where(o => o.Id == r.Id))); ctx.GetPreparedQueryCommand(c, false, true, CancellationToken.None); });
    }

    // subquery in ORDER BY -> rejected
    [Fact]
    public void Subquery_InOrderBy_Rejected()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<E>();
        var other = ctx.From<Other>();
        Reject(ctx, () => { var c = SelectGroupExtensions.SelectGroup(e.OrderBy(x => SqlFunctions.Sql.exists(other.Where(o => o.Id == x.Id))), (Expression<Func<E, object>>)(x => new { x.Id, S = SqlFunctions.Sql.sum(x.Amount) })); ctx.GetPreparedQueryCommand(c, false, true, CancellationToken.None); });
    }

    // subquery inside aggregate argument -> rejected
    [Fact]
    public void Subquery_InsideAggregateArgument_Rejected()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<E>();
        var other = ctx.From<Other>();
        Reject(ctx, () => { var c = SelectGroupExtensions.SelectGroup(e, (Expression<Func<E, object>>)(x => new { x.Id, S = SqlFunctions.Sql.sum(other.Where(o => o.Id == x.Id).Select(o => o.Value).FirstOrDefault()) })); ctx.GetPreparedQueryCommand(c, false, true, CancellationToken.None); });
    }

    // subquery inside aggregate FILTER -> rejected
    [Fact]
    public void Subquery_InsideAggregateFilter_Rejected()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<E>();
        var other = ctx.From<Other>();
        Reject(ctx, () => { var c = SelectGroupExtensions.SelectGroup(e, (Expression<Func<E, object>>)(x => new { x.Id, S = SqlFunctions.Sql.sum(x.Amount, () => SqlFunctions.Sql.exists(other.Where(o => o.Id == x.Id))) })); ctx.GetPreparedQueryCommand(c, false, true, CancellationToken.None); });
    }

}
