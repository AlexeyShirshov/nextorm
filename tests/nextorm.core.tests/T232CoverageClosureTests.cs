using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using FluentAssertions;
using NextORM.Core;
using NextORM.SqlServer;

namespace NextORM.Core.Tests;

/// <summary>
/// D-10 (#232, rv=4) coverage closure: the minimum targeted tests that execute the newly added
/// guard/recognition/fallback branches of the typed SQL Server PIVOT/UNPIVOT feature in the two
/// touched core classes (<see cref="NormSqlTranslator"/> marker recognition and
/// <c>QueryCommand.QueryPreparer</c> derived-shape pass-through). These are pure SQL-generation
/// assertions over a placeholder connection string; no connection is ever opened.
/// </summary>
public class T232CoverageClosureTests
{
    /// <summary>A mapped entity whose projection columns are row-member accesses (used for the fail-closed shape).</summary>
    [SqlTable("t232_cov_sales")]
    public interface ICovSales
    {
        [Key]
        [Column("id")]
        int Id { get; set; }

        [Column("category")]
        string? Category { get; set; }

        [Column("quarter")]
        int Quarter { get; set; }

        [Column("margin")]
        decimal? Margin { get; set; }
    }

    private const string PlaceholderConnectionString =
        "Server=localhost,1433;Database=nextorm;User Id=sa;Password=nextorm!Passw0rd;TrustServerCertificate=True";

    private static IDataContext CreateContext()
        => new SqlServerDataContext(PlaceholderConnectionString, new DataContextBuilder());

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    private static string SqlOfCached<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None)).DbCommand.CommandText);

    // ------------------------------------------------------------------ NormSqlTranslator: markers

    /// <summary>
    /// A marker whose column name is not a compile-time/constant string (here a captured variable
    /// folded to a member access) must fail closed instead of emitting an unresolved identifier.
    /// </summary>
    [Fact]
    public void Marker_NonConstantColumnName_FailsClosed()
    {
        using var ctx = CreateContext();
        var pivot = new PivotValues();
        var runtimeName = "[1]";

        var act = () => SqlOf(ctx, ctx.From("t232_sales").Select(t => new { Q1 = pivot.Value<decimal?>(runtimeName) }));

        act.Should().Throw<BuildSqlCommandException>().WithMessage("*compile-time constant column name*");
    }

    /// <summary>A marker with an empty column name fails closed through the same guard.</summary>
    [Fact]
    public void Marker_EmptyColumnName_FailsClosed()
    {
        using var ctx = CreateContext();
        var pivot = new PivotValues();

        var act = () => SqlOf(ctx, ctx.From("t232_sales").Select(t => new { Q1 = pivot.Value<decimal?>("") }));

        act.Should().Throw<BuildSqlCommandException>().WithMessage("*compile-time constant column name*");
    }

    /// <summary>The UNPIVOT marker accessors share the same fail-closed column-name guard.</summary>
    [Fact]
    public void UnpivotMarker_NonConstantRoleName_FailsClosed()
    {
        using var ctx = CreateContext();
        var unpivot = new UnpivotValues();
        var runtimeName = "val";

        var act = () => SqlOf(ctx, ctx.From("t232_quarterly").Select(t => new { V = unpivot.Value<decimal?>(runtimeName) }));

        act.Should().Throw<BuildSqlCommandException>().WithMessage("*compile-time constant column name*");
    }

    /// <summary>
    /// The marker accessors are also exercised on the param-mode pass (the cached-hit parameter
    /// refresh renders the command with the SQL builder in param mode, where a marker emits nothing
    /// but must still be recognised as translated instead of failing).
    /// </summary>
    [Fact]
    public void Marker_CachedPrepare_IsRecognisedOnTheParamModePass()
    {
        using var ctx = CreateContext();
        var pivot = new PivotValues();

        string Query() => SqlOfCached(ctx, ctx.From("t232_sales")
            .Pivot(PivotAggregate.Sum, t => SqlFunctions.Column<decimal?>(t, "margin"), t => SqlFunctions.Column<int>(t, "quarter"), PivotValue.Create("1"))
            .Select(t => new { Q1 = pivot.Value<decimal?>("[1]") }));

        var first = Query();
        // A second cached prepare re-renders the same command through the param-mode refresh.
        var second = Query();

        first.Should().Contain("[1] as [Q1]");
        second.Should().Be(first);
    }

    /// <summary>
    /// A marker in a WHERE predicate of an aliased source renders the alias-qualified identifier
    /// (the same alias route the by-name <c>SqlFunctions.Column</c> reference uses).
    /// </summary>
    [Fact]
    public void Marker_InAWhereOverAnAliasedSource_UsesTheAliasQualifiedIdentifier()
    {
        using var ctx = CreateContext();
        var pivot = new PivotValues();

        var sql = SqlOf(ctx, ctx.From("t232_sales")
            .Pivot(PivotAggregate.Sum, t => SqlFunctions.Column<decimal?>(t, "margin"), t => SqlFunctions.Column<int>(t, "quarter"), PivotValue.Create("1"))
            .Where(t => pivot.Value<decimal?>("[1]") > 0)
            .Select(t => new { Q1 = pivot.Value<decimal?>("[1]") }));

        sql.Should().Contain("[1]");
    }

    // ------------------------------------------------------------------ QueryPreparer: pass-through

    /// <summary>
    /// A metadata-less derived source read without a projection exposes the derived subquery's own
    /// projection columns (the additive fallback). The columns defined by a by-name
    /// <c>SqlFunctions.Column</c> reference keep the source's own identifier.
    /// </summary>
    [Fact]
    public void DerivedShapePassThrough_MultiColumn_ReferencesTheSourceOutputs()
    {
        using var ctx = CreateContext();

        var inner = ctx.From("t232_sales").Select(t => new
        {
            Category = SqlFunctions.Column<string>(t, "category"),
            Margin = SqlFunctions.Column<decimal?>(t, "margin"),
        });

        var sql = SqlOf(ctx, ctx.From(ctx.From(inner)).Select(x => new { x.Category, x.Margin }));

        sql.Should().Contain("from (select [category], [margin]");
    }

    /// <summary>
    /// A pass-through column whose projected member differs from the referenced source column exposes
    /// the projected member name (the alias the derived table actually emits).
    /// </summary>
    [Fact]
    public void DerivedShapePassThrough_RenamedColumn_UsesTheProjectedMemberName()
    {
        using var ctx = CreateContext();

        var inner = ctx.From("t232_sales").Select(t => new
        {
            Renamed = SqlFunctions.Column<string>(t, "category"),
        });

        var sql = SqlOf(ctx, ctx.From(ctx.From(inner)).Select(x => new { x.Renamed }));

        sql.Should().Contain("[Renamed]");
    }

    /// <summary>
    /// A single-scalar derived source read without a projection collapses to the scalar reuse branch
    /// (the consumer reads the derived table's single output column).
    /// </summary>
    [Fact]
    public void DerivedShapePassThrough_SingleScalar_UsesTheScalarBranch()
    {
        using var ctx = CreateContext();

        var inner = ctx.From("t232_sales").Select(t => SqlFunctions.Column<decimal?>(t, "margin"));

        var sql = SqlOf(ctx, ctx.From(ctx.From(inner)).Select(x => x));

        sql.Should().Contain("margin");
    }

    /// <summary>
    /// A derived source whose inner command was already prepared has its select list reused directly
    /// (the grouping-column resolution reads the prepared derived projection).
    /// </summary>
    [Fact]
    public void DerivedShapePassThrough_AlreadyPreparedInner_ReusesThePreparedSelectList()
    {
        using var ctx = CreateContext();

        var prepared = ctx.From("t232_sales").Select(t => new
        {
            Category = SqlFunctions.Column<string>(t, "category"),
            Margin = SqlFunctions.Column<decimal?>(t, "margin"),
        });
        prepared.PrepareCommand(CancellationToken.None);

        var sql = SqlOf(ctx, ctx.From(prepared).Select(x => new { x.Category, x.Margin }));

        sql.Should().Contain("category");
    }

    /// <summary>
    /// A derived projection column defined by a computed (non-member) expression is passed through
    /// under its projected member name; the column identification of the pass-through must reject the
    /// non-<c>SqlFunctions.Column</c> call.
    /// </summary>
    [Fact]
    public void DerivedShapePassThrough_ComputedColumn_UsesTheMemberName()
    {
        using var ctx = CreateContext();

        var inner = ctx.From("t232_sales").Select(t => new
        {
            Doubled = SqlFunctions.Column<int>(t, "quarter") * 2,
            Margin = SqlFunctions.Column<decimal?>(t, "margin"),
        });

        var sql = SqlOf(ctx, ctx.From(ctx.From(inner)).Select(x => new { x.Doubled, x.Margin }));

        sql.Should().Contain("Doubled");
    }

    /// <summary>
    /// A derived projection column defined by a method call that is not <c>SqlFunctions.Column</c> is
    /// passed through under its projected member name.
    /// </summary>
    [Fact]
    public void DerivedShapePassThrough_NonColumnMethodCall_UsesTheMemberName()
    {
        using var ctx = CreateContext();

        var inner = ctx.From("t232_sales").Select(t => new
        {
            Cat = t.GetString("category"),
        });

        var sql = SqlOf(ctx, ctx.From(ctx.From(inner)).Select(x => new { x.Cat }));

        sql.Should().Contain("Cat");
    }

    /// <summary>
    /// A derived projection over a mapped entity (a computed column and a renamed row member) is
    /// passed through as the derived table's own output identifiers.
    /// </summary>
    [Fact]
    public void DerivedShapePassThrough_MappedEntityDerived_UsesTheDerivedOutputs()
    {
        using var ctx = CreateContext();

        var derived = ctx.From<ICovSales>().Select(s => new { Doubled = s.Quarter * 2, M = s.Margin });

        var sql = SqlOf(ctx, ctx.From(derived).Select(x => new { x.Doubled, x.M }));

        sql.Should().Contain("[Doubled]");
        sql.Should().Contain("[M]");
    }
}
