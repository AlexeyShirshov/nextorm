using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using FluentAssertions;
using NextORM.Core;
using NextORM.SqlServer;

namespace NextORM.SqlServer.Tests;

/// <summary>
/// D-10 (#232, rv=4) coverage closure for the SQL Server typed PIVOT/UNPIVOT provider surface
/// (<see cref="SqlServerEntityBuilderExtensions"/>): the grouping-column resolution branches,
/// the derived-output naming branches and the naming-convention resolution. These are SQL-generation
/// assertions only; no connection is opened.
/// </summary>
public class T232CoverageClosureTests
{
    /// <summary>
    /// A base interface carrying a mapped column. Its property is declared on the base interface, so the
    /// entity metadata registered for the derived interface does not resolve it through the member's
    /// declaring type: the grouping-column resolution must fail closed instead of defaulting.
    /// </summary>
    public interface IInheritedColumns
    {
        [Column("category")]
        string? Category { get; set; }
    }

    /// <summary>The mapped entity: its <see cref="IInheritedColumns.Category"/> member is declared on the base interface.</summary>
    [SqlTable("t232_sales")]
    public interface IInheritedSales : IInheritedColumns
    {
        [Key]
        [Column("id")]
        int Id { get; set; }

        [Column("quarter")]
        int Quarter { get; set; }

        [Column("margin")]
        decimal? Margin { get; set; }
    }

    /// <summary>A static source member used to build a derived projection column that is not a row member.</summary>
    private static class StaticLabel
    {
        public static string? Text => "static-label";
    }

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    // ------------------------------------------------------------------ grouping-column resolution

    /// <summary>
    /// A plain entity whose grouping member has no registered column name fails closed instead of
    /// defaulting to the CLR member name.
    /// </summary>
    [Fact]
    public void TypedPivot_EntityMemberWithoutRegisteredColumnName_FailsClosed()
    {
        using var ctx = SqlServerTestContext.Create();

        var act = () => ctx.From<IInheritedSales>()
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter, (m, pv) => new { m.Category, V = pv.Value<decimal?>("1") });

        act.Should().Throw<BuildSqlCommandException>().WithMessage("*registered column name*");
    }

    /// <summary>
    /// A derived inner whose command was already prepared reuses its populated select list (the
    /// grouping-column resolution skips the redundant preparation).
    /// </summary>
    [Fact]
    public void TypedPivot_DerivedInnerAlreadyPrepared_ReusesThePreparedSelectList()
    {
        using var ctx = SqlServerTestContext.Create();

        var prepared = ctx.From<ISalesEntity>()
            .Select(s => new { s.Category, s.Quarter, s.Margin });
        prepared.PrepareCommand(CancellationToken.None);

        var sql = SqlOf(ctx, ctx.From(prepared)
            .Pivot(PivotAggregate.Sum, x => x.Margin, x => x.Quarter, (m, pv) => new { m.Category, V = pv.Value<decimal?>("1") })
            .Select(x => new { x.Category, x.V }));

        sql.Should().Contain("category");
        sql.Should().Contain("[1] as [V]");
    }

    // ------------------------------------------------------------------ derived output naming

    /// <summary>
    /// A derived projection column that keeps its mapped source column name resolves to that physical
    /// name (the derived table emits the bare column).
    /// </summary>
    [Fact]
    public void TypedPivot_DerivedGroupingColumn_KeepsItsMappedPhysicalName()
    {
        using var ctx = SqlServerTestContext.Create();

        var derived = ctx.From<ISalesEntity>()
            .Where(s => s.Margin > 0)
            .Select(s => new { s.Category, s.Quarter, s.Margin });

        var sql = SqlOf(ctx, ctx.From(derived)
            .Pivot(PivotAggregate.Sum, x => x.Margin, x => x.Quarter, (m, pv) => new { m.Category, V = pv.Value<decimal?>("1") })
            .Select(x => new { x.Category, x.V }));

        sql.Should().Contain("select category, [1] as [V]");
    }

    /// <summary>
    /// A derived projection column that is a computed (non-member) expression exposes the projected
    /// member name instead of a physical column name.
    /// </summary>
    [Fact]
    public void TypedPivot_DerivedGroupingColumn_ComputedExpression_UsesTheMemberName()
    {
        using var ctx = SqlServerTestContext.Create();

        var derived = ctx.From<ISalesEntity>()
            .Select(s => new { Label = s.Category + "!", s.Quarter, s.Margin });

        var sql = SqlOf(ctx, ctx.From(derived)
            .Pivot(PivotAggregate.Sum, x => x.Margin, x => x.Quarter, (m, pv) => new { m.Label, V = pv.Value<decimal?>("1") })
            .Select(x => new { x.Label, x.V }));

        sql.Should().Contain("Label");
    }

    /// <summary>
    /// A derived projection column whose defining expression is a member that carries no mapped column
    /// name falls back to the projected member name.
    /// </summary>
    [Fact]
    public void TypedPivot_DerivedGroupingColumn_UnmappedMember_UsesTheMemberName()
    {
        using var ctx = SqlServerTestContext.Create();

        var derived = ctx.From<ISalesEntity>()
            .Select(s => new { Label = StaticLabel.Text, s.Quarter, s.Margin });

        var sql = SqlOf(ctx, ctx.From(derived)
            .Pivot(PivotAggregate.Sum, x => x.Margin, x => x.Quarter, (m, pv) => new { m.Label, V = pv.Value<decimal?>("1") })
            .Select(x => new { x.Label, x.V }));

        sql.Should().Contain("Label");
    }

    // ------------------------------------------------------------------ naming-convention resolution

    /// <summary>
    /// The grouping-member resolution honours a per-query naming-convention override on the builder.
    /// </summary>
    [Fact]
    public void TypedPivot_PerQueryNamingConventionOverride_ResolvesTheGroupingColumn()
    {
        using var ctx = SqlServerTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<ISalesEntity>()
            .WithNamingConvention(SnakeCaseNamingConvention.Instance)
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter, (m, pv) => new { m.Category, V = pv.Value<decimal?>("1") })
            .Select(x => new { x.Category, x.V }));

        sql.Should().Contain("[1] as [V]");
    }
}
