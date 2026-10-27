using FluentAssertions;
using NextORM.ClickHouse;
using NextORM.Postgres;

namespace NextORM.Core.Tests;

/// <summary>
/// T323 (#323) D2: the <c>SelectGroup</c> guard matrix, one fact per row, always in both directions —
/// the new API rejects the shape and the equivalent legacy <c>GroupBy</c>/<c>Select</c> form keeps its
/// historical behaviour. Every expectation is a rendered SQL comparison or a preparation-time
/// exception; no database is opened (the contexts carry placeholder connection strings).
/// </summary>
public class T323SelectGroupValidationTests
{
    private const string PostgresConnectionString =
        "Host=localhost;Port=5432;Database=nextorm;Username=nextorm;Password=nextorm";

    private static IDataContext CreateContext()
        => new PostgresDataContext(PostgresConnectionString, new DataContextBuilder());

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
    {
        return (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);
    }

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd) => Normalize(Prepare(ctx, cmd).DbCommand.CommandText);

    private static void AssertRejected(Func<object?> prepare, string message)
    {
        FluentActions.Invoking(prepare)
            .Should().Throw<QueryPreparationException>()
            .WithMessage($"*{message}*");
    }

    [Fact]
    public void ExplicitGroupingModifiers_ShouldReject()
    {
        using var ctx = CreateContext();
        var e = ctx.From<T323LegacyEntity>();

        AssertRejected(() => e.GroupBy(x => x.Id).SelectGroup(x => new { x.Id }),
            "cannot be combined with explicit grouping");
        AssertRejected(() => e.GroupByRollup(x => x.Id).SelectGroup(x => new { x.Id }),
            "cannot be combined with explicit grouping");
        AssertRejected(() => e.GroupByCube(x => x.Id).SelectGroup(x => new { x.Id }),
            "cannot be combined with explicit grouping");
        AssertRejected(() => e.GroupByGroupingSets(x => new { x.Id, x.Group2 }, [0], [1]).SelectGroup(x => new { x.Id }),
            "cannot be combined with explicit grouping");
        AssertRejected(() => e.WithTotals().SelectGroup(x => new { x.Id }),
            "cannot be combined with explicit grouping");

        // The same explicit forms without SelectGroup keep rendering.
        SqlOf(ctx, e.GroupBy(x => x.Id).Select(x => new { x.Id, C = SqlFunctions.Sql.count() }))
            .Should().Contain("group by");
        SqlOf(ctx, e.GroupByRollup(x => x.Id).Select(x => new { x.Id, C = SqlFunctions.Sql.count() }))
            .Should().Contain("rollup", "the explicit rollup form is untouched by the SelectGroup guard");
        SqlOf(ctx, e.GroupByCube(x => x.Id).Select(x => new { x.Id, C = SqlFunctions.Sql.count() }))
            .Should().Contain("cube", "the explicit cube form is untouched by the SelectGroup guard");
        SqlOf(ctx, e.GroupByGroupingSets(x => new { x.Id, x.Group2 }, [0], [1])
                .Select(x => new { x.Id, x.Group2, C = SqlFunctions.Sql.count() }))
            .Should().Contain("grouping sets", "the explicit grouping-sets form is untouched by the SelectGroup guard");

        using var ch = new ClickHouseDataContext("Host=localhost", new DataContextBuilder());
        SqlOf(ch, ch.From<T323LegacyEntity>()
                .GroupBy(x => x.Id)
                .WithTotals()
                .Select(x => new { x.Id, C = SqlFunctions.Sql.count() }))
            .Should().Contain("with totals", "the explicit WITH TOTALS form is untouched by the SelectGroup guard");
    }

    [Fact]
    public void Window_InProjection_ShouldReject()
    {
        using var ctx = CreateContext();
        var e = ctx.From<T323LegacyEntity>();

        AssertRejected(() => SqlOf(ctx, e.SelectGroup(x => new { R = SqlFunctions.Sql.row_number().Over() })),
            "unsupported window expression");

        SqlOf(ctx, e.Select(x => new { R = SqlFunctions.Sql.row_number().Over() }))
            .Should().Contain("over ()", "the ordinary projection still renders its window function");
    }

    [Fact]
    public void Window_InHaving_ShouldReject()
    {
        using var ctx = CreateContext();
        var e = ctx.From<T323LegacyEntity>();

        AssertRejected(
            () => SqlOf(ctx, e
                .Having(x => SqlFunctions.Sql.sum_over(x.Amount).Over() > 0m)
                .SelectGroup(x => new { x.Id, S = SqlFunctions.Sql.sum(x.Amount) })),
            "unsupported window expression");

        SqlOf(ctx, e
                .GroupBy(x => x.Id)
                .Having(x => SqlFunctions.Sql.sum_over(x.Amount).Over() > 0m)
                .Select(x => new { x.Id, S = SqlFunctions.Sql.sum(x.Amount) }))
            .Should().Contain("over ()", "the explicit grouping path keeps rendering its HAVING predicate");
    }

    [Fact]
    public void Window_InOrderBy_ShouldReject()
    {
        using var ctx = CreateContext();
        var e = ctx.From<T323LegacyEntity>();

        AssertRejected(
            () => SqlOf(ctx, e
                .OrderBy(x => SqlFunctions.Sql.row_number().Over())
                .SelectGroup(x => new { x.Id, S = SqlFunctions.Sql.sum(x.Amount) })),
            "unsupported window expression");

        SqlOf(ctx, e
                .OrderBy(x => SqlFunctions.Sql.row_number().Over())
                .GroupBy(x => x.Id)
                .Select(x => new { x.Id, S = SqlFunctions.Sql.sum(x.Amount) }))
            .Should().Contain("over ()", "the explicit grouping path keeps rendering its expression ORDER BY");
    }

    [Fact]
    public void Subquery_InProjection_ShouldReject()
    {
        using var ctx = CreateContext();
        var e = ctx.From<T323LegacyEntity>();
        var other = ctx.From<T323LegacyOther>();

        AssertRejected(
            () => SqlOf(ctx, e.SelectGroup(x => new
            {
                x.Id,
                V = other.Where(o => o.Id == x.Id).Select(o => o.Value).First(),
            })),
            "unsupported subquery expression");

        SqlOf(ctx, e.Select(x => new
            {
                x.Id,
                V = other.Where(o => o.Id == x.Id).Select(o => o.Value).First(),
            }))
            .Should().Contain("(select", "the ordinary scalar-subquery projection still renders");
    }

    [Fact]
    public void Subquery_InHaving_ShouldReject()
    {
        using var ctx = CreateContext();
        var e = ctx.From<T323LegacyEntity>();
        var other = ctx.From<T323LegacyOther>();

        AssertRejected(
            () => SqlOf(ctx, e
                .Having(x => SqlFunctions.Sql.exists(other.Where(o => o.Id == x.Id)))
                .SelectGroup(x => new { x.Id, S = SqlFunctions.Sql.sum(x.Amount) })),
            "unsupported subquery expression");

        SqlOf(ctx, e
                .GroupBy(x => x.Id)
                .Having(x => SqlFunctions.Sql.exists(other.Where(o => o.Id == x.Id)))
                .Select(x => new { x.Id, S = SqlFunctions.Sql.sum(x.Amount) }))
            .Should().Contain("exists", "the explicit grouping path keeps rendering its HAVING subquery");
    }

    [Fact]
    public void Subquery_InOrderBy_ShouldReject()
    {
        using var ctx = CreateContext();
        var e = ctx.From<T323LegacyEntity>();
        var other = ctx.From<T323LegacyOther>();

        AssertRejected(
            () => SqlOf(ctx, e
                .OrderBy(x => SqlFunctions.Sql.exists(other.Where(o => o.Id == x.Id)))
                .SelectGroup(x => new { x.Id, S = SqlFunctions.Sql.sum(x.Amount) })),
            "unsupported subquery expression");

        SqlOf(ctx, e
                .OrderBy(x => SqlFunctions.Sql.exists(other.Where(o => o.Id == x.Id)))
                .GroupBy(x => x.Id)
                .Select(x => new { x.Id, S = SqlFunctions.Sql.sum(x.Amount) }))
            .Should().Contain("exists", "the explicit grouping path keeps rendering its expression ORDER BY");
    }

    [Fact]
    public void Subquery_InsideAggregateArgument_ShouldReject()
    {
        using var ctx = CreateContext();
        var e = ctx.From<T323LegacyEntity>();
        var other = ctx.From<T323LegacyOther>();

        AssertRejected(
            () => SqlOf(ctx, e.SelectGroup(x => new
            {
                x.Id,
                S = SqlFunctions.Sql.sum(other.Where(o => o.Id == x.Id).Select(o => o.Value).First()),
            })),
            "unsupported subquery expression");

        // Legacy path: only preparation matters here. The terminal inside the aggregate argument
        // degrades to a captured parameter in the rendered SQL — a pre-existing limitation of that
        // shape, unrelated to the SelectGroup guard, which is the only path that names it.
        SqlOf(ctx, e.Select(x => new
            {
                x.Id,
                S = SqlFunctions.Sql.sum(other.Where(o => o.Id == x.Id).Select(o => o.Value).First()),
            }))
            .Should().NotBeNullOrWhiteSpace("the ordinary projection still prepares");
    }

    [Fact]
    public void Subquery_InsideFilterPredicate_ShouldReject()
    {
        using var ctx = CreateContext();
        var e = ctx.From<T323LegacyEntity>();
        var other = ctx.From<T323LegacyOther>();

        AssertRejected(
            () => SqlOf(ctx, e.SelectGroup(x => new
            {
                x.Id,
                S = SqlFunctions.Sql.sum(x.Amount, () => SqlFunctions.Sql.exists(other.Where(o => o.Id == x.Id))),
            })),
            "unsupported subquery expression");

        // Legacy path: a subquery inside a FILTER predicate is already unsupported there (the aggregate
        // filter compiler throws a scope error), so the closest expressible legacy form is a filtered
        // aggregate over a row predicate — it still renders untouched.
        SqlOf(ctx, e.Select(x => new
            {
                x.Id,
                S = SqlFunctions.Sql.sum(x.Amount, () => x.Id > 5),
            }))
            .Should().Contain("filter", "the ordinary filtered-aggregate projection still renders");
    }

    [Fact]
    public void WhereSubquery_ShouldRemainSupported()
    {
        using var ctx = CreateContext();
        var e = ctx.From<T323LegacyEntity>();
        var other = ctx.From<T323LegacyOther>();

        var inferred = e
            .Where(x => SqlFunctions.Sql.exists(other.Where(o => o.Id == x.Id)))
            .SelectGroup(x => new { x.Id, S = SqlFunctions.Sql.sum(x.Amount) });
        var explicitForm = e
            .Where(x => SqlFunctions.Sql.exists(other.Where(o => o.Id == x.Id)))
            .GroupBy(x => x.Id)
            .Select(x => new { x.Id, S = SqlFunctions.Sql.sum(x.Amount) });

        var sql = SqlOf(ctx, inferred);
        sql.Should().Be(SqlOf(ctx, explicitForm));
        sql.Should().Contain("exists", "a subquery in WHERE is outside the inferred-key analysis");
    }

    [Fact]
    public void HavingUncoveredRowMember_ShouldReject()
    {
        using var ctx = CreateContext();
        var e = ctx.From<T323LegacyEntity>();

        AssertRejected(
            () => SqlOf(ctx, e
                .Having(x => SqlFunctions.Sql.sum(x.Amount) > 0m && x.Group2 > 1)
                .SelectGroup(x => new { x.Id, S = SqlFunctions.Sql.sum(x.Amount) })),
            "not covered by inferred keys");

        var explicitForm = e
            .GroupBy(x => new { x.Id, x.Group2 })
            .Having(x => SqlFunctions.Sql.sum(x.Amount) > 0m && x.Group2 > 1)
            .Select(x => new { x.Id, S = SqlFunctions.Sql.sum(x.Amount) });

        SqlOf(ctx, explicitForm)
            .Should().Contain("having", "the same HAVING is legal once the row member is an explicit key");
    }

    [Fact]
    public void HavingCapturedThreshold_ShouldBeValid()
    {
        using var ctx = CreateContext();
        var e = ctx.From<T323LegacyEntity>();
        var threshold = 100m;

        var inferred = e
            .Having(x => SqlFunctions.Sql.sum(x.Amount) > threshold)
            .SelectGroup(x => new { x.Id, S = SqlFunctions.Sql.sum(x.Amount) });
        var explicitForm = e
            .GroupBy(x => x.Id)
            .Having(x => SqlFunctions.Sql.sum(x.Amount) > threshold)
            .Select(x => new { x.Id, S = SqlFunctions.Sql.sum(x.Amount) });

        var sql = SqlOf(ctx, inferred);
        sql.Should().Be(SqlOf(ctx, explicitForm));
        sql.Should().Contain("having");
        sql.Should().NotContain("100", "a captured threshold is a parameter, never an extra key or literal");
    }

    [Fact]
    public void OrderByWholeKey_ShouldBeValid()
    {
        using var ctx = CreateContext();
        var e = ctx.From<T323LegacyEntity>();

        var inferred = e
            .OrderBy(x => SqlFunctions.Sql.date_trunc("quarter", x.OrderedAt))
            .SelectGroup(x => new
            {
                Q = SqlFunctions.Sql.date_trunc("quarter", x.OrderedAt),
                S = SqlFunctions.Sql.sum(x.Amount),
            });
        var explicitForm = e
            .OrderBy(x => SqlFunctions.Sql.date_trunc("quarter", x.OrderedAt))
            .GroupBy(x => SqlFunctions.Sql.date_trunc("quarter", x.OrderedAt))
            .Select(x => new
            {
                Q = SqlFunctions.Sql.date_trunc("quarter", x.OrderedAt),
                S = SqlFunctions.Sql.sum(x.Amount),
            });

        var sql = SqlOf(ctx, inferred);
        sql.Should().Be(SqlOf(ctx, explicitForm));
        sql.Should().Contain("order by");
    }

    [Fact]
    public void OrderByRawColumn_ShouldReject()
    {
        using var ctx = CreateContext();
        var e = ctx.From<T323LegacyEntity>();

        AssertRejected(
            () => SqlOf(ctx, e
                .OrderBy(x => x.OrderedAt)
                .SelectGroup(x => new
                {
                    Q = SqlFunctions.Sql.date_trunc("quarter", x.OrderedAt),
                    S = SqlFunctions.Sql.sum(x.Amount),
                })),
            "not covered by inferred keys");

        // The same raw column under the explicit grouping renders (the guard is SelectGroup-specific).
        SqlOf(ctx, e
                .OrderBy(x => x.OrderedAt)
                .GroupBy(x => SqlFunctions.Sql.date_trunc("quarter", x.OrderedAt))
                .Select(x => new
                {
                    Q = SqlFunctions.Sql.date_trunc("quarter", x.OrderedAt),
                    S = SqlFunctions.Sql.sum(x.Amount),
                }))
            .Should().Contain("order by");
    }

    [Fact]
    public void OrderByOrdinal_ShouldKeepExistingBounds()
    {
        using var ctx = CreateContext();
        var e = ctx.From<T323LegacyEntity>();

        FluentActions.Invoking(() => e.OrderBy(0)).Should().Throw<ArgumentException>();

        SqlOf(ctx, e.OrderBy(1).SelectGroup(x => new { x.Id }))
            .Should().Contain("order by", "a 1-based ordinal ORDER BY is still accepted for SelectGroup");
    }

    [Fact]
    public void NonScalarKey_ShouldReject()
    {
        using var ctx = CreateContext();
        var joined = ctx.From<T323LegacyEntity>()
            .Join(ctx.From<T323LegacyOther>(), (a, b) => a.Id == b.Id);

        FluentActions.Invoking(() => SqlOf(ctx, joined.SelectGroup(p => new { p.Item1 })))
            .Should().Throw<QueryPreparationException>()
            .WithMessage("*non-scalar*");

        SqlOf(ctx, joined.Select(p => new { p.Item1 }))
            .Should().NotBeNullOrWhiteSpace("the ordinary projection path still renders a whole joined entity");
    }

    [Fact]
    public void GlobalAggregate_HavingUncoveredRow_ShouldReject()
    {
        using var ctx = CreateContext();
        var e = ctx.From<T323LegacyEntity>();

        // A global aggregate infers no key, so a HAVING that reaches a row member has nothing to be
        // covered by and must fail at preparation instead of rendering a predicate over a column that
        // the statement does not group by.
        AssertRejected(
            () => SqlOf(ctx, e
                .Having(x => x.Group2 > 1)
                .SelectGroup(x => new { S = SqlFunctions.Sql.sum(x.Amount) })),
            "not covered by inferred keys");

        // The equivalent legacy form is untouched: an ungrouped HAVING there is still silently dropped.
        SqlOf(ctx, e.Having(x => x.Group2 > 1).Select(x => new { S = SqlFunctions.Sql.sum(x.Amount) }))
            .Should().NotContain("having");
    }
}
