using FluentAssertions;
using NextORM.Core;

namespace NextORM.SqlServer.Tests;

/// <summary>
/// The SQLite <c>total()</c> aggregate (#285) is SQLite-only: SQL Server must reject it with the shared
/// provider message instead of emitting SQL it cannot execute.
/// </summary>
public class SqliteTotalFunctionGuardTests
{
    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText;

    [Fact]
    [Trait("Issue", "285")]
    public void Total_ShouldThrow()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sqlite.total(x.Int) }));

        act.Should().Throw<NotSupportedException>()
            .WithMessage("The SQLite function 'total' is not supported by this provider.");
    }
}
