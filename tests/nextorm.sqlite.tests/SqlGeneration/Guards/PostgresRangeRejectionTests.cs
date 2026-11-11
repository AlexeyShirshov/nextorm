using FluentAssertions;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>The PostgreSQL-only range surface is rejected by SQLite (no native range type).</summary>
public class PostgresRangeRejectionTests
{
    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText;

    [Fact]
    public void Overlaps_ShouldThrowBecauseNotSupported()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var a = new Range<int>(1, 2);
        var b = new Range<int>(3, 4);

        var act = () => SqlOf(ctx, e.Where(x => SqlFunctions.Postgres.overlaps(a, b)).Select(x => new { x.Id }));

        act.Should().Throw<NotSupportedException>().WithMessage("*range*PostgreSQL*");
    }

    [Fact]
    public void RangeConstructor_ShouldThrowBecauseNotSupported()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { R = SqlFunctions.Postgres.int4range(1, 10) }));

        act.Should().Throw<NotSupportedException>().WithMessage("*range*PostgreSQL*");
    }
}
