using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.SqlServer.Tests;

/// <summary>
/// T303 (#303) SQL-generation matrix on SQL Server: an aggregate terminal inside a correlated WHERE
/// predicate lowers to the result-typed scalar subquery the projection form uses. The comparison stays
/// outside the inner SELECT, the outer row stays bound, the operator is preserved, and a terminal that
/// still returns the untyped placeholder fails closed with a public preparation exception. Every fact
/// only prepares SQL, so no connection is opened and no database is required.
/// </summary>
public class T303_AggregatePredicateTests
{
    /// <summary>The dialect's one-row marker the scalar subquery must carry.</summary>
    private const string SingleRow = "top(1)";

    /// <summary>The dialect's Int32 count rendering (ClickHouse wraps it).</summary>
    private const string CountSpan = "count(*)";

    /// <summary>The wide correlated count the navigation terminal renders.</summary>
    private const string NavigationCount = "count_big(";

    private static IDataContext CreateContext()
    {
        var ctx = SqlServerTestContext.Create();
        ctx.From<T303Child>();
        ctx.From<T303Parent>();
        return ctx;
    }

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    /// <summary>Drops the dialect quoting so identifier assertions read the same on every provider.</summary>
    private static string Decode(string sql)
        => sql.Replace("\"", string.Empty).Replace("`", string.Empty).Replace("[", string.Empty).Replace("]", string.Empty);

    /// <summary>
    /// The scalar contract: the aggregate renders inside the subquery, the subquery keeps its one-row
    /// cardinality, the subquery binds the outer row through the inner alias, and the comparison sits
    /// after the aggregate instead of inside it.
    /// </summary>
    private static void AssertCorrelatedScalar(string sql, string aggregate, string comparison)
    {
        sql.Should().Contain(aggregate, "the aggregate must render inside the subquery; SQL: " + sql);
        sql.Should().Contain(SingleRow, "the scalar subquery must keep its one-row cardinality; SQL: " + sql);
        sql.Should().Contain(comparison, "the comparison operator must be preserved; SQL: " + sql);
        Decode(sql).Should().Contain("t2.parent_id = t1.id", "the subquery must bind the outer row; SQL: " + sql);
        sql.IndexOf(comparison, StringComparison.Ordinal)
            .Should().BeGreaterThan(sql.IndexOf(aggregate, StringComparison.Ordinal),
                "the comparison must stay outside the aggregate; SQL: " + sql);
    }

    /// <summary>
    /// Matrix `Sum(nullable) > const` - the issue repro. The nullable selector aggregate composes with
    /// the ordered comparison and the whole predicate stays server-side.
    /// </summary>
    [Fact]
    public void SumNullable_OrderedComparison_ShouldLowerToCorrelatedScalar()
    {
        using var ctx = CreateContext();
        var parents = ctx.From<T303Parent>();
        var children = ctx.From<T303Child>();

        var sql = SqlOf(ctx, parents
            .Where(p => children.Where(c => c.ParentId == p.Id).Sum(c => c.Amount) > 6)
            .Select(p => p.Id));

        AssertCorrelatedScalar(sql, "sum(t2.amount)", "> 6");
        sql.Should().EndWith("> 6)", "the comparison is the outer predicate; SQL: " + sql);
    }

    /// <summary>Matrix `Count() > const` - the count branch composes in a predicate.</summary>
    [Fact]
    public void Count_OrderedComparison_ShouldLowerToCorrelatedScalar()
    {
        using var ctx = CreateContext();
        var parents = ctx.From<T303Parent>();
        var children = ctx.From<T303Child>();

        var sql = SqlOf(ctx, parents
            .Where(p => children.Where(c => c.ParentId == p.Id).Count() > 1)
            .Select(p => p.Id));

        AssertCorrelatedScalar(sql, CountSpan, "> 1");
        sql.Should().EndWith("> 1)", "the comparison is the outer predicate; SQL: " + sql);
    }

    /// <summary>Matrix `Min/Max/Avg with selector`, including a nullable selector.</summary>
    [Fact]
    public void SelectorAggregates_ShouldLowerToCorrelatedScalar()
    {
        using var ctx = CreateContext();
        var parents = ctx.From<T303Parent>();
        var children = ctx.From<T303Child>();

        AssertCorrelatedScalar(
            SqlOf(ctx, parents.Where(p => children.Where(c => c.ParentId == p.Id).Min(c => c.Score) > 1).Select(p => p.Id)),
            "min(t2.score)", "> 1");
        AssertCorrelatedScalar(
            SqlOf(ctx, parents.Where(p => children.Where(c => c.ParentId == p.Id).Max(c => c.Score) > 1).Select(p => p.Id)),
            "max(t2.score)", "> 1");
        AssertCorrelatedScalar(
            SqlOf(ctx, parents.Where(p => children.Where(c => c.ParentId == p.Id).Avg(c => c.Score) > 1).Select(p => p.Id)),
            "avg(cast(t2.score as float))", "> 1");
        AssertCorrelatedScalar(
            SqlOf(ctx, parents.Where(p => children.Where(c => c.ParentId == p.Id).Min(c => c.Amount) > 1).Select(p => p.Id)),
            "min(t2.amount)", "> 1");
    }

    /// <summary>Matrix `==` / `!=` - the boxed equality arm keeps its operators.</summary>
    [Fact]
    public void EqualityAndInequality_ShouldKeepTheirOperators()
    {
        using var ctx = CreateContext();
        var parents = ctx.From<T303Parent>();
        var children = ctx.From<T303Child>();

        var equal = SqlOf(ctx, parents.Where(p => children.Where(c => c.ParentId == p.Id).Count() == 2).Select(p => p.Id));
        var notEqual = SqlOf(ctx, parents.Where(p => children.Where(c => c.ParentId == p.Id).Count() != 2).Select(p => p.Id));

        Decode(equal).Should().Contain(") = 2", "the equality operator must be preserved; SQL: " + equal);
        Decode(notEqual).Should().Contain(") != 2", "the inequality operator must be preserved; SQL: " + notEqual);

        foreach (var sql in new[] { equal, notEqual })
        {
            sql.Should().Contain(CountSpan, "the count branch keeps its aggregate; SQL: " + sql);
            sql.Should().Contain(SingleRow, "the scalar subquery keeps its one-row cardinality; SQL: " + sql);
            Decode(sql).Should().Contain("t2.parent_id = t1.id", "the subquery binds the outer row; SQL: " + sql);
        }
    }

    /// <summary>Matrix `&amp;&amp;` / `||` / `!` - logical composition of a lowered predicate.</summary>
    [Fact]
    public void LogicalOperators_ShouldComposeWithLoweredPredicate()
    {
        using var ctx = CreateContext();
        var parents = ctx.From<T303Parent>();
        var children = ctx.From<T303Child>();

        var and = SqlOf(ctx, parents
            .Where(p => children.Where(c => c.ParentId == p.Id).Sum(c => c.Amount) > 6
                && children.Where(c => c.ParentId == p.Id).Count() > 1)
            .Select(p => p.Id));
        and.Should().Contain(" and ", "the conjunction must survive; SQL: " + and);
        and.Should().Contain("sum(t2.amount)", "the first aggregate subquery must render; SQL: " + and);
        and.Should().Contain(CountSpan, "the second aggregate subquery must render; SQL: " + and);

        var or = SqlOf(ctx, parents
            .Where(p => children.Where(c => c.ParentId == p.Id).Sum(c => c.Amount) > 6
                || children.Where(c => c.ParentId == p.Id).Count() == 0)
            .Select(p => p.Id));
        or.Should().Contain(" or ", "the disjunction must survive; SQL: " + or);

        var not = SqlOf(ctx, parents
            .Where(p => !(children.Where(c => c.ParentId == p.Id).Count() > 1))
            .Select(p => p.Id));
        Decode(not).Should().Contain("not (", "the negation must survive; SQL: " + not);
        not.Should().Contain(CountSpan);
    }

    /// <summary>Matrix `arithmetic / reversed comparison` - the subquery takes the correct side.</summary>
    [Fact]
    public void ArithmeticAndReversedComparison_ShouldKeepTheirShape()
    {
        using var ctx = CreateContext();
        var parents = ctx.From<T303Parent>();
        var children = ctx.From<T303Child>();

        var arithmetic = SqlOf(ctx, parents
            .Where(p => children.Where(c => c.ParentId == p.Id).Count() + 1 > 2)
            .Select(p => p.Id));
        arithmetic.Should().Contain(") + 1) > 2", "the arithmetic consumer must keep its shape; SQL: " + arithmetic);

        var reversed = SqlOf(ctx, parents
            .Where(p => 6 < children.Where(c => c.ParentId == p.Id).Sum(c => c.Amount))
            .Select(p => p.Id));
        Decode(reversed).Should().Contain("(6 < (select ", "the constant stays on the left; SQL: " + reversed);
        reversed.Should().Contain("sum(t2.amount)");
        reversed.Should().Contain(SingleRow);
    }

    /// <summary>
    /// Matrix `Uncorrelated aggregate predicate` - an aggregate over a subquery that references no outer
    /// column still lowers to a scalar subquery and must not invent a correlation.
    /// </summary>
    [Fact]
    public void UncorrelatedAggregatePredicate_ShouldStayUncorrelated()
    {
        using var ctx = CreateContext();
        var parents = ctx.From<T303Parent>();
        var children = ctx.From<T303Child>();

        var sql = SqlOf(ctx, parents.Where(p => children.Sum(c => c.Amount) > 6).Select(p => p.Id));

        Decode(sql).Should().Contain("sum(amount)", "the aggregate renders once over the inner source; SQL: " + sql);
        Decode(sql).Should().NotContain("t1.", "no outer alias may be bound; SQL: " + sql);
        sql.Should().Contain(SingleRow);
        sql.Should().EndWith("> 6)");
    }

    /// <summary>
    /// Matrix `Correlation depth 2 (scalar nesting)` - an aggregate predicate inside a correlated
    /// subquery that is itself the aggregate of the outer predicate: two nested levels, two bindings.
    /// </summary>
    [Fact]
    public void NestedAggregatePredicate_ShouldBindBothLevels()
    {
        using var ctx = CreateContext();
        var parents = ctx.From<T303Parent>();
        var children = ctx.From<T303Child>();

        var sql = SqlOf(ctx, parents
            .Where(p => children.Where(c => c.ParentId == p.Id
                    && children.Where(d => d.ParentId == c.Id).Sum(d => d.Amount) > 0)
                .Count() > 0)
            .Select(p => p.Id));
        var bare = Decode(sql);

        bare.Should().Contain("t2.parent_id = t1.id", "the outer level binds the outer row; SQL: " + sql);
        bare.Should().Contain("t3.parent_id = t2.id", "the inner level binds the middle row; SQL: " + sql);
        sql.Should().Contain("sum(t3.amount)", "the inner aggregate renders in its own subquery; SQL: " + sql);
        sql.Should().Contain(CountSpan, "the outer aggregate is the count branch; SQL: " + sql);
        sql.Should().Contain(SingleRow);
    }

    /// <summary>
    /// Matrix `Empty / all-NULL child set` (shared seed): the predicate compares the bare aggregate, so a
    /// SQL NULL sum cannot match a positive threshold. No coalesce / null-substitution may be injected.
    /// </summary>
    [Fact]
    public void EmptyOrAllNullChildSet_ShouldKeepSqlNullSemantics()
    {
        using var ctx = CreateContext();
        var parents = ctx.From<T303Parent>();
        var children = ctx.From<T303Child>();

        var sql = SqlOf(ctx, parents
            .Where(p => children.Where(c => c.ParentId == p.Id).Sum(c => c.Amount) > 0)
            .Select(p => p.Id));
        var bare = Decode(sql);

        bare.Should().Contain("sum(t2.amount)", "the nullable sum stays the compared value; SQL: " + sql);
        bare.Should().NotContain("coalesce", "NULL must not be substituted; SQL: " + sql);
        bare.Should().NotContain("ifnull", "NULL must not be substituted; SQL: " + sql);
        bare.Should().NotContain("isnull", "NULL must not be substituted; SQL: " + sql);
        bare.Should().NotContain("nvl(", "NULL must not be substituted; SQL: " + sql);
        sql.Should().EndWith("> 0)", "the positive threshold compares the raw aggregate; SQL: " + sql);
    }

    /// <summary>
    /// Matrix `Stdev/Stdevp/Var/Varp in predicate` - the SQL Server statistical spellings lower to the
    /// same correlated scalar (the deferred S1 providers stay out of scope).
    /// </summary>
    [Fact]
    public void StatisticalAggregates_ShouldLowerToCorrelatedScalar()
    {
        using var ctx = CreateContext();
        var parents = ctx.From<T303Parent>();
        var children = ctx.From<T303Child>();

        AssertCorrelatedScalar(
            SqlOf(ctx, parents.Where(p => children.Where(c => c.ParentId == p.Id).Stdev(c => c.Score) > 1).Select(p => p.Id)),
            "stdev(t2.score)", "> 1");
        AssertCorrelatedScalar(
            SqlOf(ctx, parents.Where(p => children.Where(c => c.ParentId == p.Id).Stdevp(c => c.Score) > 1).Select(p => p.Id)),
            "stdevp(t2.score)", "> 1");
        AssertCorrelatedScalar(
            SqlOf(ctx, parents.Where(p => children.Where(c => c.ParentId == p.Id).Var(c => c.Score) > 1).Select(p => p.Id)),
            "var(t2.score)", "> 1");
        AssertCorrelatedScalar(
            SqlOf(ctx, parents.Where(p => children.Where(c => c.ParentId == p.Id).Varp(c => c.Score) > 1).Select(p => p.Id)),
            "varp(t2.score)", "> 1");
    }

    /// <summary>
    /// Matrix `LongCount()` in predicate (T279 boundary): the terminal stays outside the aggregate
    /// whitelist, so it fails closed with a public preparation exception naming terminal and operator.
    /// </summary>
    [Fact]
    public void LongCountInPredicate_ShouldFailClosed()
    {
        using var ctx = CreateContext();
        var parents = ctx.From<T303Parent>();
        var children = ctx.From<T303Child>();

        FluentActions.Invoking(() => SqlOf(ctx, parents
                .Where(p => children.Where(c => c.ParentId == p.Id).LongCount() > 0)
                .Select(p => p.Id)))
            .Should().Throw<QueryPreparationException>()
            .WithMessage("*LongCount*")
            .WithMessage("*GreaterThan*");
    }

    /// <summary>
    /// Matrix `Leftover registry-query scalar with &gt;/&lt;`: a scalar <c>First()</c> subquery keeps
    /// today's unsupported status but reports it as a public refusal, naming the operator.
    /// </summary>
    [Fact]
    public void LeftoverScalarInPredicate_ShouldFailClosed()
    {
        using var ctx = CreateContext();
        var parents = ctx.From<T303Parent>();
        var children = ctx.From<T303Child>();

        FluentActions.Invoking(() => SqlOf(ctx, parents
                .Where(p => children.Where(c => c.ParentId == p.Id).Select(c => c.Score).First() > 5)
                .Select(p => p.Id)))
            .Should().Throw<QueryPreparationException>()
            .WithMessage("*First*")
            .WithMessage("*GreaterThan*");

        FluentActions.Invoking(() => SqlOf(ctx, parents
                .Where(p => children.Where(c => c.ParentId == p.Id).Select(c => c.Score).First() < 5)
                .Select(p => p.Id)))
            .Should().Throw<QueryPreparationException>()
            .WithMessage("*First*")
            .WithMessage("*LessThan*");
    }

    /// <summary>Matrix `Projection aggregate regression` - the projection form keeps its SQL shape.</summary>
    [Fact]
    public void ProjectionAggregates_ShouldKeepTheirRendering()
    {
        using var ctx = CreateContext();
        var parents = ctx.From<T303Parent>();
        var children = ctx.From<T303Child>();

        var sql = SqlOf(ctx, parents.Select(p => new
        {
            p.Id,
            C = children.Where(c => c.ParentId == p.Id).Count(),
            S = children.Where(c => c.ParentId == p.Id).Sum(c => c.Amount)
        }));
        var bare = Decode(sql);

        bare.Should().Contain("as C").And.Contain("as S", "the projection aliases survive; SQL: " + sql);
        sql.Should().Contain(CountSpan, "the projection count keeps its rendering; SQL: " + sql);
        sql.Should().Contain("sum(t3.amount)", "the projection sum keeps its rendering; SQL: " + sql);
        bare.Should().Contain("t2.parent_id = t1.id").And.Contain("t3.parent_id = t1.id",
            "both projection subqueries stay correlated; SQL: " + sql);
        sql.Should().NotContain("exists(", "a projected aggregate is not an EXISTS; SQL: " + sql);
    }

    /// <summary>
    /// Matrix `Any/EXISTS next to an aggregate predicate` - the boolean terminal keeps its EXISTS shape
    /// and does not disturb the neighbouring lowered aggregate.
    /// </summary>
    [Fact]
    public void AnyNextToAggregatePredicate_ShouldKeepExistsShape()
    {
        using var ctx = CreateContext();
        var parents = ctx.From<T303Parent>();
        var children = ctx.From<T303Child>();

        var sql = SqlOf(ctx, parents
            .Where(p => children.Where(c => c.ParentId == p.Id).Any()
                && children.Where(c => c.ParentId == p.Id).Sum(c => c.Amount) > 6)
            .Select(p => p.Id));
        var bare = Decode(sql);

        bare.Should().Contain("exists(select *", "the Any terminal keeps its EXISTS form; SQL: " + sql);
        bare.Should().Contain("t2.parent_id = t1.id", "EXISTS stays correlated; SQL: " + sql);
        bare.Should().Contain("sum(t3.amount)", "the neighbouring aggregate stays a scalar subquery; SQL: " + sql);
        bare.Should().Contain("t3.parent_id = t1.id", "the aggregate stays correlated; SQL: " + sql);
        sql.Should().Contain("> 6");
    }

    /// <summary>
    /// Matrix `Collection-navigation Children.Count() &gt; 1 regression` (shared shape): a navigation
    /// count keeps the wide correlated count with no in-database Int32 narrowing.
    /// </summary>
    [Fact]
    public void CollectionNavigationCount_ShouldKeepTheWideCount()
    {
        using var ctx = CreateContext();
        ctx.From<R3CountChild>();
        ctx.From<R3CountParent>(b => { b.HasMany(p => p.Children, c => c.ParentId); });

        var sql = SqlOf(ctx, ctx.From<R3CountParent>().Where(p => p.Children.Count() > 1).Select(p => p.Id));
        var bare = Decode(sql);

        bare.Should().Contain(NavigationCount, "the navigation count keeps the wide rendering; SQL: " + sql);
        bare.Should().NotContain("cast(count(", "no in-database narrowing cast; SQL: " + sql);
        bare.Should().Contain("t2.parent_id = t1.id", "the navigation count stays correlated; SQL: " + sql);
        sql.Should().Contain("> 1");
    }

    /// <summary>
    /// Matrix `Same-context A-&gt;A-&gt;B-&gt;B-&gt;A`: alternating aggregate-predicate shapes on one
    /// context keep identical SQL per shape and never disable the plan cache.
    /// </summary>
    [Fact]
    public void SameContext_AlternatingShapes_ShouldStayStableAndCached()
    {
        using var ctx = CreateContext();
        var parents = ctx.From<T303Parent>();
        var children = ctx.From<T303Child>();

        QueryCommand<long> ShapeA() => parents
            .Where(p => children.Where(c => c.ParentId == p.Id).Sum(c => c.Amount) > 6)
            .Select(p => p.Id);
        QueryCommand<long> ShapeB() => parents
            .Where(p => children.Where(c => c.ParentId == p.Id).Count() > 1)
            .Select(p => p.Id);

        string ObserveA()
        {
            var command = ShapeA();
            var sql = SqlOf(ctx, command);
            command.Cache.Should().BeTrue("preparing a predicate must not disable the plan cache");
            return sql;
        }

        string ObserveB()
        {
            var command = ShapeB();
            var sql = SqlOf(ctx, command);
            command.Cache.Should().BeTrue("preparing a predicate must not disable the plan cache");
            return sql;
        }

        var first = ObserveA();
        ObserveA().Should().Be(first, "the same shape must render identically on reuse");
        var other = ObserveB();
        ObserveB().Should().Be(other, "the second shape must render identically on reuse");
        ObserveA().Should().Be(first, "returning to the first shape must not leak state");
        first.Should().NotBe(other, "the two shapes must stay distinct");
    }

}

[SqlTable("t303_parent")]
public sealed class T303Parent
{
    [Key]
    [Column("id")]
    public long Id { get; set; }
}

[SqlTable("t303_child")]
public sealed class T303Child
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("parent_id")]
    public long ParentId { get; set; }

    [Column("amount")]
    public int? Amount { get; set; }

    [Column("score")]
    public int Score { get; set; }
}
