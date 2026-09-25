using FluentAssertions;
using NextORM.Core;

namespace NextORM.SqlServer.Tests;

/// <summary>The MySQL/MariaDB-only surface is rejected by SQL Server (no database).</summary>
public class MySqlFunctionsRejectionTests
{
    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText;

    [Fact]
    public void FindInSet_ShouldThrowBecauseNotSupported()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => SqlFunctions.MySql.find_in_set(x.String, "a,b")));

        act.Should().Throw<NotSupportedException>().WithMessage("*find_in_set*not supported*");
    }

    [Fact]
    public void UuidToBin_ShouldThrowBecauseNotSupported()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => SqlFunctions.MySql.uuid_to_bin("6ccd780c-baba-1026-9564-5b8c656024db")));

        act.Should().Throw<NotSupportedException>().WithMessage("*uuid_to_bin*not supported*");
    }
}
