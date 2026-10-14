using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.SqlServer.Tests;

/// <summary>
/// D-03 (#232) core-marker behaviour: the translation-only <see cref="PivotValues"/> /
/// <see cref="UnpivotValues"/> markers and their narrow recognition in the core translator. The
/// markers are usable in an ordinary <c>Select</c> over an existing <c>PIVOT</c>/<c>UNPIVOT</c>
/// source (a captured marker instance), executing them at runtime throws, an unrelated same-named
/// method is not recognised, and an unbound marker source fails closed. These tests never open a
/// connection.
/// </summary>
public class T232MarkerTests
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

    [Fact]
    public void PivotValueMarker_InOrdinarySelect_EmitsColumnWithMemberAlias()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ISalesEntity>();
        var pivot = new PivotValues();

        // The captured marker binds the single TableAlias PIVOT source and sets ColumnName, so each
        // cell is aliased to its projected member (the typed-column route of the frozen contract).
        SqlOf(ctx, e
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter, PivotValue.Create("1"), PivotValue.Create("2"))
            .Select(t => new
            {
                Category = SqlFunctions.Column<string>(t, "category"),
                Q1 = pivot.Value<decimal?>("[1]"),
                Q2 = pivot.Value<decimal?>("[2]")
            }))
            .Should().Be("select category, [1] as [Q1], [2] as [Q2]\n from sales pivot (sum(margin) for quarter in ([1], [2])) as [t1]");
    }

    [Fact]
    public void UnpivotMarkers_InOrdinarySelect_EmitsRoleColumnsWithMemberAliases()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IQuarterlyEntity>();
        var unpivot = new UnpivotValues();

        SqlOf(ctx, e
            .Unpivot("val", "qtr", UnpivotColumn.Create("q1"), UnpivotColumn.Create("q2"))
            .Select(t => new
            {
                Category = SqlFunctions.Column<string>(t, "category"),
                Quarter = unpivot.Name<string>("qtr"),
                Measure = unpivot.Value<decimal?>("val")
            }))
            .Should().Be("select category, qtr as [Quarter], val as [Measure]\n from quarterly unpivot ([val] for [qtr] in ([q1], [q2])) as [t1]");
    }

    [Fact]
    public void MarkerAccessors_ThrowAtRuntime()
    {
        var pivot = new PivotValues();
        var unpivot = new UnpivotValues();

        // AC-07 negative arm: the accessors are translation-only, so executing them is a clear error.
        new Action(() => pivot.Value<decimal?>("[1]")).Should()
            .Throw<NotSupportedException>().WithMessage("*translation-only*");
        new Action(() => unpivot.Value<decimal?>("val")).Should()
            .Throw<NotSupportedException>().WithMessage("*translation-only*");
        new Action(() => unpivot.Name<string>("qtr")).Should()
            .Throw<NotSupportedException>().WithMessage("*translation-only*");
    }

    [Fact]
    public void MarkerRecognition_MatchesOnlyTheExactMethodDefinitions()
    {
        using var ctx = SqlServerTestContext.Create();
        var pivot = new PivotValues();
        var sameNamed = new SameNamedMarker();

        var recognised = SqlOf(ctx, ctx.From<ISalesEntity>()
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter, PivotValue.Create("1"))
            .Select(t => new { Q1 = pivot.Value<decimal?>("Alpha") }));

        var unrelated = SqlOf(ctx, ctx.From<ISalesEntity>()
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter, PivotValue.Create("1"))
            .Select(t => new { Q1 = sameNamed.Value<decimal?>("Alpha") }));

        // The exact marker definition emits the Alpha column; the same-named method on another type
        // is evaluated/folded instead and never becomes the by-name column.
        recognised.Should().Contain("Alpha as [Q1]");
        unrelated.Should().NotContain("Alpha");
    }

    [Fact]
    public void Marker_WithoutABoundPivotSource_FailsClosed()
    {
        using var ctx = SqlServerTestContext.Create();
        var pivot = new PivotValues();

        // No PIVOT/UNPIVOT source is bound, so the marker cannot resolve a TableAlias source and must
        // fail clearly instead of emitting an unqualified or wrong identifier.
        var act = () => SqlOf(ctx, ctx.From<ISalesEntity>().Select(t => new { Q1 = pivot.Value<decimal?>("[1]") }));

        act.Should().Throw<BuildSqlCommandException>().WithMessage("*PIVOT/UNPIVOT result source*");
    }
}
