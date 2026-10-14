using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using FluentAssertions;
using NextORM.Core;
using NextORM.SqlServer;

namespace NextORM.SqlServer.Tests;

/// <summary>
/// D-01 (#232) evidence-only probes. They do not exercise the not-yet-existing typed
/// <c>Pivot</c>/<c>Unpivot</c> overloads; they pin the two execution-only unknowns the typed
/// overload must rely on, as deterministic exact-SQL / exact-name assertions:
/// <list type="number">
/// <item>the mapped / naming-convention column name is resolvable at the extension-call
/// lifecycle (after <c>From&lt;T&gt;</c> registered the entity, before preparation), and is not
/// silently defaulted when the entity was never registered;</item>
/// <item>an existing PIVOT/UNPIVOT source can be consumed with <see cref="SqlFunctions.Column{T}"/>,
/// proving the concrete safe name-resolution route (the pivot source parameter is resolved and its
/// identifier/rename alias emitted) and that the <c>TableAlias</c> source registration performed by
/// the pivot renderer precedes the column use;</item>
/// <item>the same by-name route fails closed when its argument is not a source parameter.</item>
/// </list>
/// These tests never open a connection.
/// </summary>
public class T232PreparationProbeTests
{
    private const string PlaceholderConnectionString =
        "Server=localhost,1433;Database=nextorm;User Id=sa;Password=nextorm!Passw0rd;TrustServerCertificate=True";

    /// <summary>An entity whose half of the mapping is declared and half is naming-convention driven.</summary>
    [SqlTable("t232_probe_widget")]
    private interface IProbeWidget
    {
        [Key]
        [Column("id")]
        int Id { get; set; }

        // No [Column]: resolved through the active INamingConvention.
        decimal? MarginValue { get; set; }
    }

    /// <summary>Never passed to <c>From&lt;T&gt;</c>: proves resolution never invents a name.</summary>
    [SqlTable("t232_probe_unregistered")]
    private interface IProbeUnregisteredEntity
    {
        [Key]
        int Id { get; set; }

        decimal? NeverRegistered { get; set; }
    }

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd) => Normalize(Prepare(ctx, cmd).DbCommand.CommandText);

    [Fact]
    public void PreparationProbe_MappedColumnName_IsResolvedAtExtensionCallLifecycle()
    {
        using var ctx = SqlServerTestContext.Create();

        // The lifecycle mirrors a typed Pivot/Unpivot call: From<TEntity>() first registers the
        // mapping, then an extension method must read the effective column name.
        _ = ctx.From<IProbeWidget>();

        var property = typeof(IProbeWidget).GetProperty(nameof(IProbeWidget.Id))!;

        property.GetPropertyColumnName(SnakeCaseNamingConvention.Instance).Should().Be("id");
    }

    [Fact]
    public void PreparationProbe_NamingConventionColumnName_IsResolvedAtExtensionCallLifecycle()
    {
        using var ctx = new SqlServerDataContext(
            PlaceholderConnectionString,
            new DataContextBuilder().UseNamingConvention(SnakeCaseNamingConvention.Instance));

        _ = ctx.From<IProbeWidget>();

        // A declared column name is returned verbatim, the active convention never overrides it.
        typeof(IProbeWidget).GetProperty(nameof(IProbeWidget.Id))!
            .GetPropertyColumnName(SnakeCaseNamingConvention.Instance).Should().Be("id");

        // The convention-only column name is derived from the registered auto mapping.
        typeof(IProbeWidget).GetProperty(nameof(IProbeWidget.MarginValue))!
            .GetPropertyColumnName(SnakeCaseNamingConvention.Instance).Should().Be("margin_value");
    }

    [Fact]
    public void PreparationProbe_UnregisteredEntity_ColumnNameIsEmpty_FailClosed()
    {
        using var ctx = SqlServerTestContext.Create();

        // The type was never passed to From<T>(), so no mapping is registered and no name may be
        // invented by defaulting (unknown 2 in the reconnaissance section).
        var property = typeof(IProbeUnregisteredEntity).GetProperty(nameof(IProbeUnregisteredEntity.NeverRegistered))!;

        property.GetPropertyColumnName().Should().BeEmpty();
    }

    [Fact]
    public void PreparationProbe_PivotSource_ColumnByName_EmitsIdentifierAndRenameAlias()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ISalesEntity>();

        // Existing PIVOT + SqlFunctions.Column<T>: the pivot source is a TableAlias, and the
        // by-name route must resolve its source parameter (BaseExpressionVisitor.ResolveColumnSource)
        // and emit the identifier. Setting ColumnName additionally renames the mismatching member
        // (unknown 1). Because the pivot renderer registers the TableAlias source before the select
        // list is rendered, the columns bind without an "entity source parameter" failure (unknown 4).
        SqlOf(ctx, e
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter, PivotValue.Create("1"), PivotValue.Create("2"))
            .Select(t => new
            {
                Category = SqlFunctions.Column<string>(t, "category"),
                Q1 = SqlFunctions.Column<decimal?>(t, "[1]"),
                Q2 = SqlFunctions.Column<decimal?>(t, "[2]")
            }))
            .Should().Be("select category, [1] as [Q1], [2] as [Q2]\n from sales pivot (sum(margin) for quarter in ([1], [2])) as [t1]");
    }

    [Fact]
    public void PreparationProbe_UnpivotSource_ColumnByName_EmitsIdentifierAndRenameAlias()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IQuarterlyEntity>();

        SqlOf(ctx, e
            .Unpivot("val", "qtr", UnpivotColumn.Create("q1"), UnpivotColumn.Create("q2"))
            .Select(t => new
            {
                Category = SqlFunctions.Column<string>(t, "category"),
                Quarter = SqlFunctions.Column<string>(t, "qtr"),
                Measure = SqlFunctions.Column<decimal?>(t, "val")
            }))
            .Should().Be("select category, qtr as [Quarter], val as [Measure]\n from quarterly unpivot ([val] for [qtr] in ([q1], [q2])) as [t1]");
    }

    [Fact]
    public void PreparationProbe_PivotSource_ColumnByName_NonSourceArgument_FailsClosed()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<ISalesEntity>();

        // A by-name column only resolves against a real source parameter; a non-source argument must
        // not silently render an unqualified or wrong identifier.
        var act = () => SqlOf(ctx, e
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter, PivotValue.Create("1"))
            .Select(t => new { Bad = SqlFunctions.Column<int>("not-a-source", "id") }));

        act.Should().Throw<BuildSqlCommandException>();
    }
}
