using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.SqlServer.Tests;

/// <summary>
/// D-232 (#232, rv=5) V23 closure: the ordinary PIVOT/UNPIVOT marker
/// (<see cref="PivotValues.Value{T}"/>, <see cref="UnpivotValues.Value{T}"/>,
/// <see cref="UnpivotValues.Name{T}"/>) carries no source argument, so
/// <c>NormSqlTranslator.TryTranslateMarker</c> must fail closed when more than one
/// <see cref="TableAlias"/>-bound source is in scope instead of silently binding the first one. These
/// are deterministic SQL-generation facts over a placeholder connection string; no connection is
/// opened.
/// </summary>
public class T232Rv5AmbiguityTests
{
    /// <summary>A type with the same method names as the markers; it must never be recognised.</summary>
    private sealed class SameNamedMarker
    {
        public T Value<T>(string name) => default!;

        public T Name<T>(string name) => default!;
    }

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    /// <summary>An ordinary marker-mode PIVOT source (an untyped <see cref="TableAlias"/> builder).</summary>
    private static EntityBuilder<TableAlias> PivotOne(IDataContext ctx)
        => ctx.From<ISalesEntity>().Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter, PivotValue.Create("1"), PivotValue.Create("2"));

    /// <summary>
    /// A second, distinguishable <see cref="TableAlias"/>-bound source joined to <paramref name="pivot"/>:
    /// both sources are TableAlias-typed and the rendered SQL qualifies them as t1 and t2.
    /// </summary>
    private static JoinedEntityBuilder<TableAlias, TableAlias> TwoTableAliasSources(IDataContext ctx, EntityBuilder<TableAlias> pivot)
        => pivot.Join(
            ctx.From("t232_other"),
            (x, y) => SqlFunctions.Column<int>(x, "quarter") == SqlFunctions.Column<int>(y, "quarter"));

    // ------------------------------------------------------------------ (a) single matching source

    [Fact]
    public void OrdinaryMarker_WithASingleTableAliasSource_KeepsThePositiveResult()
    {
        using var ctx = SqlServerTestContext.Create();
        var marker = new PivotValues();

        var sql = SqlOf(ctx, PivotOne(ctx).Select(t => new { Q1 = marker.Value<decimal?>("1") }));

        // Unchanged positive contract: the single TableAlias PIVOT source binds and the cell is aliased
        // to its projected member (the id/name route of the frozen projection contract).
        sql.Should().Be("select [1] as [Q1]\n from sales pivot (sum(margin) for quarter in ([1], [2])) as [t1]");
    }

    // ------------------------------------------------------------------ (b) explicitly named source

    [Fact]
    public void ExplicitColumnReference_OverTwoTableAliasSources_BindsEachNamedSource()
    {
        using var ctx = SqlServerTestContext.Create();

        // The source-naming route keeps working over the same two-source shape: p.Item1/p.Item2 resolve
        // by occurrence, so each reference binds its own source and the guard never applies here.
        var sql = SqlOf(ctx, TwoTableAliasSources(ctx, PivotOne(ctx)).Select(p => new
        {
            Left = SqlFunctions.Column<string>(p.Item1, "category"),
            Right = SqlFunctions.Column<string>(p.Item2, "category")
        }));

        sql.Should().Contain("t1.[category] as [Left]");
        sql.Should().Contain("t2.[category] as [Right]");
    }

    // ------------------------------------------------------------------ (c) ambiguous ordinary marker

    [Fact]
    public void OrdinaryMarker_WithTwoTableAliasSources_FailsClosed()
    {
        using var ctx = SqlServerTestContext.Create();
        var marker = new PivotValues();

        var act = () => SqlOf(ctx, TwoTableAliasSources(ctx, PivotOne(ctx))
            .Select(p => new { V = marker.Value<decimal?>("[1]") }));

        act.Should().Throw<BuildSqlCommandException>().WithMessage("*more than one TableAlias source*");
    }

    [Fact]
    public void UnpivotMarker_WithTwoTableAliasSources_FailsClosed()
    {
        using var ctx = SqlServerTestContext.Create();
        var marker = new UnpivotValues();

        var act = () => SqlOf(ctx, TwoTableAliasSources(ctx, PivotOne(ctx))
            .Select(p => new { N = marker.Name<string>("qtr") }));

        act.Should().Throw<BuildSqlCommandException>().WithMessage("*more than one TableAlias source*");
    }

    // ------------------------------------------------------------------ (c3) no source keeps its message

    [Fact]
    public void OrdinaryMarker_WithNoTableAliasSource_KeepsTheNoSourceMessage()
    {
        using var ctx = SqlServerTestContext.Create();
        var marker = new PivotValues();

        // The pre-existing "no source in scope" failure is untouched by the ambiguity guard.
        var act = () => SqlOf(ctx, ctx.From<ISalesEntity>().Select(t => new { Q1 = marker.Value<decimal?>("[1]") }));

        act.Should().Throw<BuildSqlCommandException>().WithMessage("*no TableAlias source is in scope*");
    }

    // ------------------------------------------------------------------ (d) unrelated same-named method

    [Fact]
    public void SameNamedMethod_OnAnotherType_IsNotRecognisedAsTheMarker()
    {
        using var ctx = SqlServerTestContext.Create();
        var marker = new PivotValues();
        var sameNamed = new SameNamedMarker();

        // The guard only ever narrows the recognised marker path: an unrelated same-named generic
        // Value/Name method is still not a marker (it is evaluated/folded, never a by-name column).
        var sql = SqlOf(ctx, PivotOne(ctx).Select(t => new { V = sameNamed.Value<decimal?>("Alpha") }));

        sql.Should().NotContain("Alpha");
    }
}
