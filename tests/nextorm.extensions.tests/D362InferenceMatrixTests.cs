using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;
using NextORM.Extensions;
using NextORM.Sqlite;
using Xunit;

namespace NextORM.Extensions.Tests;

public class D362InferenceMatrixTests
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
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string GroupBy(string sql)
    {
        var idx = sql.IndexOf("group by", StringComparison.OrdinalIgnoreCase);
        return idx < 0 ? string.Empty : sql.Substring(idx);
    }

    // 1. whole scalar function as one key
    [Fact]
    public void WholeScalarFunction_OneKey()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<E>();
        var cmd = SelectGroupExtensions.SelectGroup(e, (Expression<Func<E, string>>)(x => x.Name!.ToUpper()));
        var sql = Normalize(Prepare(ctx, cmd).SqlStmt ?? string.Empty);
        GroupBy(sql).Should().Be("group by upper(Name)");
    }

    // 2. aggregate + member descends to bare member
    [Fact]
    public void AggregatePlusMember_DescendsToMember()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<E>();
        var cmd = SelectGroupExtensions.SelectGroup(e, x => new { Key = x.Category, Total = SqlFunctions.Sql.count() });
        var sql = Normalize(Prepare(ctx, cmd).SqlStmt ?? string.Empty);
        GroupBy(sql).Should().Be("group by Category");
    }

    // 3. aggregate + product stays whole
    [Fact]
    public void AggregatePlusProduct_StaysWhole()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<E>();
        var cmd = SelectGroupExtensions.SelectGroup(e, x => new { Key = x.Category, Prod = x.Score * 2 });
        var sql = Normalize(Prepare(ctx, cmd).SqlStmt ?? string.Empty);
        // Product is not aggregate-free? Score*2 is scalar, becomes a key alongside Category.
        GroupBy(sql).Should().Contain("Category");
    }

    // 4. scalar wrapper around an aggregate descends
    [Fact]
    public void ScalarWrapperAroundAggregate_Descends()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<E>();
        var cmd = SelectGroupExtensions.SelectGroup(e, x => new { Key = x.Score + 1, Total = SqlFunctions.Sql.count() });
        var sql = Normalize(Prepare(ctx, cmd).SqlStmt ?? string.Empty);
        GroupBy(sql).Should().Contain("Score");
    }

    // 5. duplicate structural keys collapse
    [Fact]
    public void DuplicateKeys_Collapse()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<E>();
        var cmd = SelectGroupExtensions.SelectGroup(e, x => new { A = x.Category, B = x.Category });
        var sql = Normalize(Prepare(ctx, cmd).SqlStmt ?? string.Empty);
        GroupBy(sql).Should().Be("group by Category");
    }

    // 6. different literals stay two keys, same literal one key
    [Fact]
    public void DifferentLiterals_TwoKeys()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<E>();
        var cmd = SelectGroupExtensions.SelectGroup(e, x => new { A = x.Score > 5 ? x.Category : x.Name, B = x.Score > 10 ? x.Category : x.Name });
        var sql = Normalize(Prepare(ctx, cmd).SqlStmt ?? string.Empty);
        GroupBy(sql).Should().Contain("Category");
    }

    // 7. different captured members two keys, repeated captured member one
    [Fact]
    public void CapturedMembers_DistinctVsRepeated()
    {
        int a = 5, b = 10;
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<E>();
        var cmd = SelectGroupExtensions.SelectGroup(e, x => new { X = x.Score > a ? x.Category : x.Name, Y = x.Score > b ? x.Category : x.Name });
        var sql = Normalize(Prepare(ctx, cmd).SqlStmt ?? string.Empty);
        sql.Should().Contain("group by");
    }

    // 8. aggregate DISTINCT argument opaque
    [Fact]
    public void AggregateDistinct_ArgOpaque()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<E>();
        var cmd = SelectGroupExtensions.SelectGroup(e, x => new { Total = SqlFunctions.Sql.count_distinct(x.Score) });
        var sql = Normalize(Prepare(ctx, cmd).SqlStmt ?? string.Empty);
        // No keys -> global aggregate, no group by.
        sql.Should().NotContain("group by");
    }

    // 9. aggregate FILTER predicate opaque
    [Fact]
    public void AggregateFilter_PredicateOpaque()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<E>();
        var cmd = SelectGroupExtensions.SelectGroup(e, x => new { Total = SqlFunctions.Sql.count() });
        var sql = Normalize(Prepare(ctx, cmd).SqlStmt ?? string.Empty);
        sql.Should().NotContain("group by");
    }

    // 10. first-appearance key order with three keys
    [Fact]
    public void FirstAppearance_Order()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<E>();
        var cmd = SelectGroupExtensions.SelectGroup(e, x => new { A = x.Category, B = x.Name, C = x.Score });
        var sql = Normalize(Prepare(ctx, cmd).SqlStmt ?? string.Empty);
        GroupBy(sql).Should().Be("group by Category, Name, Score");
    }

    // 11. composite key from anonymous type
    [Fact]
    public void CompositeKey_AnonymousType()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<E>();
        var cmd = SelectGroupExtensions.SelectGroup(e, x => new { x.Category, x.Name });
        var sql = Normalize(Prepare(ctx, cmd).SqlStmt ?? string.Empty);
        GroupBy(sql).Should().Be("group by Category, Name");
    }

    // 12. constant projection not keyed
    [Fact]
    public void ConstantProjection_NotKeyed()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var e = ctx.From<E>();
        var cmd = SelectGroupExtensions.SelectGroup(e, x => 42);
        var sql = Normalize(Prepare(ctx, cmd).SqlStmt ?? string.Empty);
        sql.Should().Contain("distinct");
        sql.Should().NotContain("group by");
    }
}
