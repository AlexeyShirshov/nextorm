using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// #265: the new <c>RequiresRecursiveCteUnionAll</c> capability must stay off for ClickHouse and the
/// existing behavior of the typed recursive API (no recursive <c>WITH</c>) must not change. No database
/// is opened.
/// </summary>
public class T265RecursiveUnionTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    [Fact]
    public void ClickHouseRecursive_RemainsUnsupported()
    {
        using var ctx = ClickHouseTestContext.CreateClickHouse();

        ctx.Dialect.RequiresRecursiveCteUnionAll.Should().BeFalse();

        var nums = ctx.From<ISimpleEntity>()
            .Where(s => s.Id == 1)
            .Select(s => s.Id)
            .AsRecursiveCte("nums", self => ctx.From(self).Where(n => n < 5).Select(n => n + 1));

        var act = () => SqlOf(ctx, ctx.From(nums).Select(n => n));

        act.Should().Throw<NotSupportedException>().WithMessage("*not supported*");
    }
}
