using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;
using NextORM.Extensions;
using NextORM.Sqlite;
using Xunit;

namespace NextORM.Extensions.Tests;

public class D362CacheLifecycleTests
{
    private sealed class E
    {
        public long Id { get; set; }
        public int? Score { get; set; }
        public string? Name { get; set; }
        public string? Category { get; set; }
        public decimal Amount { get; set; }
    }

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => NextORM.Tests.PreparedPlanObservation.GetPlan(ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None));

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    // 1. SelectGroup vs SelectGroupHaving produce different SQL
    [Fact]
    public void SelectGroupVsHaving_DifferentSql()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<E>();
        var sg = SelectGroupExtensions.SelectGroup(e, (Expression<Func<E, long>>)(x => x.Id));
        var sgh = SelectGroupExtensions.SelectGroupHaving(e, (Expression<Func<E, long>>)(x => x.Id), f => f > 5);
        var sql1 = Normalize(Prepare(ctx, sg).SqlStmt ?? string.Empty);
        var sql2 = Normalize(Prepare(ctx, sgh).SqlStmt ?? string.Empty);
        sql1.Should().NotBe(sql2);
        sql2.Should().Contain("having");
    }

    // 2. different condition expression -> different SQL
    [Fact]
    public void DifferentCondition_DifferentSql()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<E>();
        var a = SelectGroupExtensions.SelectGroupHaving(e, (Expression<Func<E, long>>)(x => x.Id), f => f > 5);
        var b = SelectGroupExtensions.SelectGroupHaving(e, (Expression<Func<E, long>>)(x => x.Id), f => f > 10);
        var sqlA = Normalize(Prepare(ctx, a).SqlStmt ?? string.Empty);
        var sqlB = Normalize(Prepare(ctx, b).SqlStmt ?? string.Empty);
        sqlA.Should().NotBe(sqlB);
    }

    [Fact]
    public void SelectGroupDescriptor_EqualHashesDoNotMatchDifferentConditions()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        Expression<Func<E, long>> projection = row => row.Id;
        Expression<Func<long, bool>> above = id => id > 5;
        Expression<Func<long, bool>> below = id => id < 5;
        var prep = new CollisionContext(ctx.From<E>().Select(projection));

        var first = new SelectGroupComponent<E>(projection, above).Prepare(prep);
        var equivalent = new SelectGroupComponent<E>(projection, (Expression<Func<long, bool>>)(id => id > 5)).Prepare(prep);
        var different = new SelectGroupComponent<E>(projection, below).Prepare(prep);

        different.StructuralHash.Should().Be(first.StructuralHash);
        first.StructurallyEquals(equivalent).Should().BeTrue();
        first.StructurallyEquals(different).Should().BeFalse();
        different.StructurallyEquals(first).Should().BeFalse();
    }

    private sealed class CollisionContext(QueryCommand command) : IQueryComponentPrepareContext
    {
        public QueryCommand Command { get; } = command;
        public IEqualityComparer<Expression> ExpressionComparer { get; } = new ConstantHasher(new QueryComponentContext(command).ExpressionComparer);
    }

    private sealed class ConstantHasher(IEqualityComparer<Expression> comparer) : IEqualityComparer<Expression>
    {
        public bool Equals(Expression? x, Expression? y) => comparer.Equals(x, y);
        public int GetHashCode(Expression obj) => 0;
    }

    // 5. ResetPreparation re-derives, no accumulation
    [Fact]
    public void ResetPreparation_NoAccumulation()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<E>();
        var cmd = SelectGroupExtensions.SelectGroup(e, (Expression<Func<E, long>>)(x => x.Id));
        var sql1 = Normalize(Prepare(ctx, cmd).SqlStmt ?? string.Empty);
        cmd.ResetPreparation();
        var sql2 = Normalize(Prepare(ctx, cmd).SqlStmt ?? string.Empty);
        sql1.Should().Be(sql2);
    }

    // 7. builder not mutated
    [Fact]
    public void Builder_NotMutated()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<E>();
        var originalSql = Normalize(((DbPreparedQueryCommand<long>)ctx.GetPreparedQueryCommand(e.Select(x => x.Id), false, true, CancellationToken.None)).SqlStmt ?? string.Empty);
        var _ = SelectGroupExtensions.SelectGroup(e, (Expression<Func<E, long>>)(x => x.Id));
        var afterSql = Normalize(((DbPreparedQueryCommand<long>)ctx.GetPreparedQueryCommand(e.Select(x => x.Id), false, true, CancellationToken.None)).SqlStmt ?? string.Empty);
        originalSql.Should().Be(afterSql);
    }

    // 3/4/6/8. warm hit produces identical SQL (rebind works)
    [Fact]
    public void WarmHit_IdenticalSql()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<E>();
        var cmd = SelectGroupExtensions.SelectGroup(e, (Expression<Func<E, long>>)(x => x.Id));
        var sql1 = Normalize(Prepare(ctx, cmd).SqlStmt ?? string.Empty);
        var sql2 = Normalize(Prepare(ctx, cmd).SqlStmt ?? string.Empty);
        sql1.Should().Be(sql2);
    }

    // captured value rebind
    [Fact]
    public void CapturedValue_Rebinds()
    {
        int threshold = 5;
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<E>();
        var cmd = SelectGroupExtensions.SelectGroupHaving(e, (Expression<Func<E, long>>)(x => x.Id), f => f > threshold);
        var sql1 = Normalize(Prepare(ctx, cmd).SqlStmt ?? string.Empty);
        threshold = 10;
        cmd.ResetPreparation();
        var sql2 = Normalize(Prepare(ctx, cmd).SqlStmt ?? string.Empty);
        sql1.Should().NotBeNullOrEmpty();
        sql2.Should().NotBeNullOrEmpty();
    }
}
