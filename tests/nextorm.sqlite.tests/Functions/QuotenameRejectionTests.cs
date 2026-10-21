using FluentAssertions;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// <c>QUOTENAME</c> is a SQL Server-only function, so SQLite must reject every argument form before any
/// SQL is rendered — literal, parameterized, null and column arguments alike (no database).
/// </summary>
public class QuotenameRejectionTests
{
    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText;

    [Fact]
    [Trait("Issue", "258")]
    public void Quotename_AllArgumentForms_ShouldRejectUnsupportedProvider()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.quotename("abc") }));
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.quotename("abc", "'") }));
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.quotename(x.String) }));
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.quotename(x.String, x.String) }));
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.quotename(null, null) }));

        var value = "O'Brien";
        var quote = "'";
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.quotename(value, quote) }));
    }

    private static void AssertUnsupported<T>(IDataContext ctx, QueryCommand<T> cmd)
        => FluentActions.Invoking(() => SqlOf(ctx, cmd))
            .Should().Throw<NotSupportedException>().WithMessage("*quotename*not supported*");
}
