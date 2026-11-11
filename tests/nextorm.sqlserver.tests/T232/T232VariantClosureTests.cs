using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using FluentAssertions;
using NextORM.Core;
using NextORM.SqlServer;

namespace NextORM.SqlServer.Tests;

/// <summary>
/// D-09 (#232, rv=4) variant closure: the four execution variants that were not executable-closed at
/// rv3, each in its own class so its exact fully-qualified filter selects it. Every fact is a
/// deterministic SQL-generation assertion (or a declared API-boundary assertion); none opens a
/// connection.
/// </summary>
public class T232VariantV05Tests
{
    /// <summary>The deterministic table-valued-function row shape; its mapped members are the typed PIVOT inputs.</summary>
    public interface ITvfSalesRow
    {
        [Column("category")]
        string? Category { get; set; }

        [Column("quarter")]
        int Quarter { get; set; }

        [Column("margin")]
        decimal? Margin { get; set; }
    }

    /// <summary>A deterministic SQL Server table-valued function used as the PIVOT inner.</summary>
    private static class SalesTvf
    {
        public static TvfEntityBuilder<ITvfSalesRow> Sales() => new("t232_sales_tvf");
    }

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    private static EntityBuilder<TShape> TypedTvfPivot<TShape>(
        IDataContext ctx, System.Linq.Expressions.Expression<System.Func<ITvfSalesRow, PivotValues, TShape>> projection)
        => ctx.From(() => SalesTvf.Sales())
            .Pivot(PivotAggregate.Sum, r => r.Margin, r => r.Quarter, projection);

    [Fact]
    public void TypedPivot_OverTvfInner_EmitsTheDeclaredProjection()
    {
        using var ctx = SqlServerTestContext.Create();

        var typed = TypedTvfPivot(ctx, (r, pv) => new { r.Category, Q1 = pv.Value<decimal?>("1"), Q2 = pv.Value<decimal?>("2") });

        var sql = SqlOf(ctx, typed.Select(x => new { x.Category, x.Q1, x.Q2 }));

        // The inner source is the TVF call (aliased because SQL Server requires a subquery alias); the
        // typed projection is the frozen rename/order contract applied to that source.
        sql.Should().Be(
            "select [category], [Q1], [Q2]\n" +
            " from (select [category], [1] as [Q1], [2] as [Q2]\n" +
            " from t232_sales_tvf() as [t1] pivot (sum(margin) for quarter in ([1], [2])) as [t2]) as [t3]");
    }

    [Fact]
    public void TypedPivot_OverTvfInner_ComposesWithWhereAndSelect()
    {
        using var ctx = SqlServerTestContext.Create();

        var typed = TypedTvfPivot(ctx, (r, pv) => new { r.Category, Q1 = pv.Value<decimal?>("1") });

        var sql = SqlOf(ctx, typed.Where(x => x.Q1 > 0).Select(x => new { x.Category, x.Q1 }));

        sql.Should().Contain("from t232_sales_tvf() as [t1] pivot (sum(margin) for quarter in ([1])) as [t2]");
        sql.Should().Contain("where (t3.[Q1] > cast(0 as decimal(38, 10)))");
    }
}

/// <summary>
/// D-09 (#232, rv=4) V18 closure, SQL-generation half (rv=5): a PIVOT value that is absent from (a
/// typo against) the source data. The declared value string is a runtime identifier with no
/// compile-time membership guarantee, so it is emitted verbatim. The executed database behaviour of
/// that missing cell (SQL <c>NULL</c>) is asserted against a real SQL Server by
/// <c>T232SqlServerTestsRv5</c> in <c>nextorm.integration.tests</c>; these facts never open a
/// connection.
/// </summary>
public class T232VariantV18Tests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    [Fact]
    public void AbsentPivotValue_StillEmitsItsDeclaredIdentifier()
    {
        using var ctx = SqlServerTestContext.Create();

        // "3" is absent from the frozen fixture (quarters 1 and 2 only). The typed overload does not
        // validate membership against the data or the mapping, so it must not throw and the rendered SQL
        // must still carry the declared identifier.
        var typed = ctx.From<ISalesEntity>()
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter,
                (m, pv) => new { m.Category, Q3 = pv.Value<decimal?>("3") });

        var sql = SqlOf(ctx, typed.Select(x => new { x.Category, x.Q3 }));

        sql.Should().Contain("[3] as [Q3]");
        sql.Should().Contain("for quarter in ([3])");
        sql.Should().Be(
            "select [category], [Q3]\n" +
            " from (select [category], [3] as [Q3]\n" +
            " from sales pivot (sum(margin) for quarter in ([3])) as [t1]) as [t2]");
    }

    [Fact]
    public void AbsentPivotValue_IsDeclaredVerbatim_WithoutMembershipValidation()
    {
        using var ctx = SqlServerTestContext.Create();

        // The declared value list is a runtime identifier list: it is never validated against the data
        // or the mapping. "3" is absent from the frozen fixture, yet it stays in the emitted IN list
        // exactly as declared — never dropped and never extended with a value from the data (the absent
        // value's executed NULL materialization is asserted against a real SQL Server by
        // T232SqlServerTestsRv5, not against the in-memory fixture here).
        var typed = ctx.From<ISalesEntity>()
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter,
                (m, pv) => new { m.Category, Q1 = pv.Value<decimal?>("1"), Q3 = pv.Value<decimal?>("3") });

        var sql = SqlOf(ctx, typed.Select(x => new { x.Category, x.Q1, x.Q3 }));

        sql.Should().Contain("for quarter in ([1], [3])");
        sql.Should().Contain("[3] as [Q3]");
        sql.Should().NotContain("[2]", "the declared list is exactly what the projection declares");
    }
}

/// <summary>
/// D-09 (#232, rv=4) V19 closure: the documented API boundary for a dynamic PIVOT. A value list known
/// only at runtime is outside the typed shape (its cells are declared one by one from compile-time or
/// captured constants); the legacy untyped overload remains available and unchanged for that route.
/// </summary>
public class T232VariantV19Tests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    [Fact]
    public void RowDependentValueName_IsRejectedByTheTypedOverload()
    {
        using var ctx = SqlServerTestContext.Create();

        // A genuinely dynamic PIVOT derives its value list from the data, so its cell name is only known
        // per row. The typed overload has no runtime-collection parameter and requires a compile-time or
        // captured name, so a row-dependent name is rejected fail-closed instead of emitting an incorrect
        // or unbounded IN list.
        var act = () => ctx.From<ISalesEntity>()
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter,
                (m, pv) => new { m.Category, V = pv.Value<decimal?>(m.Category!) });

        act.Should().Throw<ArgumentException>().WithMessage("*column name*");
    }

    [Fact]
    public void LegacyUntypedPivot_WithARuntimeValueList_RemainsAvailableAndUnchanged()
    {
        using var ctx = SqlServerTestContext.Create();

        // The dynamic route: the value list is built at runtime and fed to the legacy untyped overload,
        // which stays available and emits the frozen legacy SQL (bare identifiers, no member aliases).
        var runtimeValues = new List<string> { "1", "2" };
        var values = runtimeValues.Select(PivotValue.Create).ToArray();

        var sql = SqlOf(ctx, ctx.From<ISalesEntity>()
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter, values)
            .Select(t => new
            {
                Category = t.GetString("category"),
                Q1 = t.GetNullableDecimal("[1]"),
                Q2 = t.GetNullableDecimal("[2]")
            }));

        sql.Should().Be("select category, [1], [2]\n from sales pivot (sum(margin) for quarter in ([1], [2])) as [t1]");
    }
}

/// <summary>
/// D-09 (#232, rv=4) V21 closure: the eager/single-query state guards and cache locality. The existing
/// legacy guards are asserted alongside their typed equivalents, and interleaved typed queries must share
/// no sticky command/plan-cache state.
/// </summary>
public class T232VariantV21Tests
{
    /// <summary>An eager-load parent shape (the same guard shape the legacy guard test uses).</summary>
    public sealed class GuardParent
    {
        public int Id { get; set; }

        public decimal? Margin { get; set; }

        public int Quarter { get; set; }

        public ICollection<GuardChild> Children { get; set; } = new List<GuardChild>();
    }

    /// <summary>An eager-load child shape.</summary>
    public sealed class GuardChild
    {
        public int Id { get; set; }

        public int ParentId { get; set; }
    }

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    private static string SqlOfCached<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None)).DbCommand.CommandText);

    private static EntityBuilder<GuardParent> EagerParents(IDataContext ctx)
        => ctx.From<GuardParent>().LoadWith(p => p.Children, c => c.From<GuardChild>(), p => p.Id, c => c.ParentId);

    [Fact]
    public void EagerLoadState_GuardsTheLegacyAndTypedOperators()
    {
        using var ctx = SqlServerTestContext.Create();

        // The existing (legacy untyped) guards ...
        new Action(() => EagerParents(ctx)
                .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter, PivotValue.Create("1")))
            .Should().Throw<NotSupportedException>().WithMessage("*LoadWith*Pivot*");
        new Action(() => EagerParents(ctx)
                .Unpivot("val", "qtr", UnpivotColumn.Create("quarter")))
            .Should().Throw<NotSupportedException>().WithMessage("*LoadWith*Unpivot*");

        // ... and their typed equivalents.
        new Action(() => EagerParents(ctx)
                .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter, (m, pv) => new { m.Id, V = pv.Value<decimal?>("1") }))
            .Should().Throw<NotSupportedException>().WithMessage("*LoadWith*Pivot*");
        new Action(() => EagerParents(ctx)
                .Unpivot((m, u) => new { m.Id, N = u.Name<string>("qtr"), V = u.Value<decimal?>("val") }, UnpivotColumn.Create("Margin")))
            .Should().Throw<NotSupportedException>().WithMessage("*LoadWith*Unpivot*");
    }

    [Fact]
    public void InterleavedTypedQueries_ShareNoStickyCommandOrPlanCacheState()
    {
        using var ctx = SqlServerTestContext.Create();

        var first = ctx.From<ISalesEntity>()
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter, (m, pv) => new { m.Category, V = pv.Value<decimal?>("1") });
        var second = ctx.From<ISalesEntity>()
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter, (m, pv) => new { m.Category, V = pv.Value<decimal?>("2") });

        var firstSql = SqlOf(ctx, first.Select(x => new { x.Category, x.V }));
        var secondSql = SqlOf(ctx, second.Select(x => new { x.Category, x.V }));

        secondSql.Should().Contain("[2] as [V]").And.Contain("in ([2])");
        firstSql.Should().Contain("[1] as [V]").And.Contain("in ([1])");
        firstSql.Should().NotContain("[2]");
        secondSql.Should().NotContain("[1]");
        // Repeating the first query after the interleaved one yields the identical SQL: no sticky state.
        SqlOf(ctx, first.Select(x => new { x.Category, x.V })).Should().Be(firstSql);
        // The plan cache is not mutated by the interleaved use: a cached prepare yields the same SQL.
        SqlOfCached(ctx, first.Select(x => new { x.Category, x.V })).Should().Be(firstSql);
    }
}
