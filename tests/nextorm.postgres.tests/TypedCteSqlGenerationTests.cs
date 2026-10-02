using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Postgres.Tests;

/// <summary>
/// Typed ordinary-CTE SQL generation (#146 slice A). The typed source reads the CTE name directly
/// (no derived-table wrapper); two CTEs of the same CLR type bind by source slot. No database is used.
/// </summary>
public class TypedCteSqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    [Fact]
    public void TypedCte_ShouldEmitWithAndReadBareName()
    {
        using var ctx = PostgresTestContext.Create();

        var recent = ctx.From<IComplexEntity>().Where(x => x.Id > 1).Select(x => new { x.Id, x.Int }).AsCte("recent");
        var sql = SqlOf(ctx, ctx.From(recent).Select(r => new { r.Id, r.Int }));

        sql.Should().Contain("with recent");
        sql.Should().Contain("from recent");
        sql.Should().NotContain("from (select");
    }

    /// <summary>
    /// PostgreSQL quoted identifiers are case-sensitive, so a typed CTE declaration must alias every
    /// column to the projection property name (case-exactly) and the consumer must reference that same
    /// quoted name; the mapped lowercase column would otherwise be unreachable (defect-3).
    /// </summary>
    [Fact]
    public void TypedCte_DeclarationAndConsumer_ShouldAgreeOnPropertyNameIdentifier()
    {
        using var ctx = PostgresTestContext.Create();

        var inner = ctx.From<IComplexEntity>().Where(x => x.Id > 1).Select(x => new { x.Id, x.Int }).AsCte("inner");
        var outer = ctx.From(inner).Select(x => new { x.Id }).AsCte("outer");

        var sql = SqlOf(ctx, ctx.From(outer).Select(x => x.Id));

        // Producer body exposes the property-name alias; the consumer body/final read reference it.
        sql.Should().Contain("inner as (select id as \"Id\"");
        sql.Should().Contain("outer as (select \"Id\" from inner as");
        sql.Should().Contain("select \"Id\" from outer as");
    }

    [Fact]
    public void TypedCte_TwoSameTypeCtes_ShouldBindBySourceSlot()
    {
        using var ctx = PostgresTestContext.Create();

        var high = ctx.From<IComplexEntity>().Where(x => x.Id > 1).Select(x => new { x.Id }).AsCte("high");
        var low = ctx.From<IComplexEntity>().Where(x => x.Id < 9).Select(x => new { x.Id }).AsCte("low");

        var sql = SqlOf(ctx, ctx.From(high)
            .Join(ctx.From(low), (a, b) => a.Id == b.Id)
            .Select(p => p.Item1.Id));

        sql.Should().Contain("with high as (");
        sql.Should().Contain(", low as (");
        sql.Should().Contain("join low as");
    }

    [Fact]
    public void TypedCte_SystemTupleProjection_ShouldFailFast()
    {
        using var ctx = PostgresTestContext.Create();

        // §6 and the code-smells review finding 65: a whole-row System.Tuple is projected as one opaque column; the typed CTE
        // read cannot address it, so preparation must reject it rather than emit the row-operand-less
        // "().f1" (an identity read would emit an empty select list). PostgreSQL has a native tuple
        // renderer, so this is the real-provider proof the guard fires before broken SQL.
        var tp = ctx.From<IComplexEntity>().Select(x => Tuple.Create(x.Id, x.String)).AsCte("tp");

        var act = () => SqlOf(ctx, ctx.From(tp).Select(r => r.Item1));

        act.Should().Throw<NotSupportedException>().WithMessage("*System.Tuple*");
    }

    [Fact]
    public void TypedCte_ValueTupleProjection_ShouldFailFast()
    {
        using var ctx = PostgresTestContext.Create();

        // §6: ValueTuple gets no new support just because the classifier recognises it. The defining
        // body would otherwise render a ROW(...) constructor the typed CTE read cannot expose as
        // readable ItemN columns, so preparation must fail fast rather than materialize a wrong shape.
        var vt = ctx.From<IComplexEntity>().Select(x => new ValueTuple<long, int?>(x.Id, x.Int)).AsCte("vt");

        var act = () => SqlOf(ctx, ctx.From(vt).Select(r => r.Item1));

        act.Should().Throw<NotSupportedException>().WithMessage("*ValueTuple*");
    }

}
