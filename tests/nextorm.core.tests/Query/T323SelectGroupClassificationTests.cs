using System.Linq.Expressions;
using FluentAssertions;
using NextORM.ClickHouse;
using NextORM.Postgres;

namespace NextORM.Core.Tests;

/// <summary>
/// T323 (#323) D3a: the aggregate classifier behind <c>SelectGroup</c> key inference, proved where the
/// classification is observable — the annotated <c>IsAggregate</c> boundary, the ClickHouse aggregate
/// catalogue (which the PostgreSQL dialect refuses, so the cell lives here with a ClickHouse context),
/// and the window-name exclusion. Only preparation runs: the contexts carry placeholder connection
/// strings, so no database is opened.
/// </summary>
public class T323SelectGroupClassificationTests
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
    public void AttributeIsAggregate_ShouldBeRecognizedByTheClassifier()
    {
        using var ctx = CreateContext();

        // Same projection shape twice: only the annotation differs. The aggregate keeps its argument
        // opaque, the scalar one groups by it.
        var annotated = ctx.From<T323LegacyEntity>()
            .SelectGroup(x => new { x.Id, V = T323CoreAggregateUdf.T323CoreAgg(x.Amount) });
        var annotatedExplicitForm = ctx.From<T323LegacyEntity>()
            .GroupBy(x => x.Id)
            .Select(x => new { x.Id, V = T323CoreAggregateUdf.T323CoreAgg(x.Amount) });

        var scalar = ctx.From<T323LegacyEntity>()
            .SelectGroup(x => new { V = T323CoreScalarUdf.T323CoreScalar(x.Amount) });
        var scalarExplicitForm = ctx.From<T323LegacyEntity>()
            .GroupBy(x => T323CoreScalarUdf.T323CoreScalar(x.Amount))
            .Select(x => new { V = T323CoreScalarUdf.T323CoreScalar(x.Amount) });

        var annotatedSql = SqlOf(ctx, annotated);
        annotatedSql.Should().Be(SqlOf(ctx, annotatedExplicitForm));
        GroupByLine(annotatedSql).Should().NotContainEquivalentOf(
            "amount",
            "An attributed IsAggregate call is an opaque aggregate boundary");

        var scalarSql = SqlOf(ctx, scalar);
        scalarSql.Should().Be(SqlOf(ctx, scalarExplicitForm));
        GroupByLine(scalarSql).Should().ContainEquivalentOf(
            "amount",
            "without IsAggregate the same call is a scalar and its argument is a key");
    }

    [Fact]
    public void ClickHouseAggregate_ShouldBeOpaque()
    {
        // Hosted here rather than in the PostgreSQL project: the ClickHouse aggregate catalogue is
        // refused by the PostgreSQL dialect, while a ClickHouse context renders the same projection in
        // both the inferred and the explicit form.
        using var ctx = new ClickHouseDataContext("Host=localhost", new DataContextBuilder());

        var inferred = ctx.From<T323LegacyEntity>()
            .SelectGroup(x => new { x.Id, Q = SqlFunctions.ClickHouse.quantile(0.5, x.Amount) });
        var explicitForm = ctx.From<T323LegacyEntity>()
            .GroupBy(x => x.Id)
            .Select(x => new { x.Id, Q = SqlFunctions.ClickHouse.quantile(0.5, x.Amount) });

        var sql = SqlOf(ctx, inferred);

        sql.Should().Be(SqlOf(ctx, explicitForm));
        sql.Should().Contain("quantile(");
        GroupByLine(sql).Should().NotContainEquivalentOf(
            "amount",
            "a ClickHouse quantile aggregate is an opaque boundary for key inference");
    }

    [Fact]
    public void WindowMappedName_ShouldNotBeClassifiedAsAggregate()
    {
        using var ctx = CreateContext();
        var e = ctx.From<T323LegacyEntity>();

        // exponential_time_decayed_sum is a window member: its name is in the WindowSql map and the
        // renderer refuses the bare call. The window exclusion therefore wins over any aggregate
        // reading, and the shape is rejected instead of being silently grouped.
        Expression<Func<T323LegacyEntity, WindowFunction<double>>> bare =
            x => SqlFunctions.ClickHouse.exponential_time_decayed_sum(1, x.Amount, x.OrderedAt);

        SelectGroupExpressionAnalyzer.IsAggregateCall(bare.Body)
            .Should().BeFalse("a window-mapped CLR name is never an aggregate");

        FluentActions.Invoking(() => SqlOf(ctx, e.SelectGroup(x => new
            {
                V = SqlFunctions.ClickHouse.exponential_time_decayed_sum(1, x.Amount, x.OrderedAt),
            })))
            .Should().Throw<QueryPreparationException>()
            .WithMessage("*unsupported window expression*");
    }
}

/// <summary>A declaring-type <see cref="SqlFunctionAttribute"/> that marks every member an aggregate.</summary>
[SqlFunction(IsAggregate = true)]
public static class T323CoreAggregateUdf
{
    /// <summary>No method attribute: the type attribute applies entirely, the SQL name is the CLR name.</summary>
    public static decimal T323CoreAgg(decimal value) => value;
}

/// <summary>The same shape without <c>IsAggregate</c>: the default (scalar) annotation.</summary>
public static class T323CoreScalarUdf
{
    [SqlFunction("t323_core_scalar")]
    public static decimal T323CoreScalar(decimal value) => value;
}
