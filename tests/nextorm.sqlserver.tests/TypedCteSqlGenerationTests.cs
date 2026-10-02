using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.SqlServer.Tests;

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
        using var ctx = SqlServerTestContext.Create();

        var recent = ctx.From<IComplexEntity>().Where(x => x.Id > 1).Select(x => new { x.Id, x.Int }).AsCte("recent");
        var sql = SqlOf(ctx, ctx.From(recent).Select(r => new { r.Id, r.Int }));

        sql.Should().Contain("with recent");
        sql.Should().Contain("from recent");
        sql.Should().NotContain("from (select");
    }

    [Fact]
    public void TypedCte_TwoSameTypeCtes_ShouldBindBySourceSlot()
    {
        using var ctx = SqlServerTestContext.Create();

        var high = ctx.From<IComplexEntity>().Where(x => x.Id > 1).Select(x => new { x.Id }).AsCte("high");
        var low = ctx.From<IComplexEntity>().Where(x => x.Id < 9).Select(x => new { x.Id }).AsCte("low");

        var sql = SqlOf(ctx, ctx.From(high)
            .Join(ctx.From(low), (a, b) => a.Id == b.Id)
            .Select(p => p.Item1.Id));

        sql.Should().Contain("with high as (");
        sql.Should().Contain(", low as (");
        sql.Should().Contain("join low as");
    }
}
