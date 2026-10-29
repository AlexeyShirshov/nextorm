using FluentAssertions;
using NextORM.Core;

namespace NextORM.ClickHouse.Tests;

/// <summary>The MySQL/MariaDB-only surface is rejected by ClickHouse (no database).</summary>
public class MySqlFunctionsRejectionTests
{
    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText;

    [Fact]
    public void FindInSet_ShouldThrowBecauseNotSupported()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => SqlFunctions.MySql.find_in_set(x.String, "a,b")));

        act.Should().Throw<NotSupportedException>().WithMessage("*find_in_set*not supported*");
    }

    [Fact]
    public void JsonStorageFree_ShouldThrowBecauseNotSupported()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => SqlFunctions.MySql.json_storage_free("{\"a\":1}")));

        act.Should().Throw<NotSupportedException>().WithMessage("*json_storage_free*not supported*");
    }

    [Fact]
    public void UuidToBin_ShouldThrowBecauseNotSupported()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => SqlFunctions.MySql.uuid_to_bin("6ccd780c-baba-1026-9564-5b8c656024db")));

        act.Should().Throw<NotSupportedException>().WithMessage("*uuid_to_bin*not supported*");
    }

    [Fact]
    public void T284_JsonFunctions_ShouldThrowOnUnsupportedProvider()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var equals = () => SqlOf(ctx, e.Select(x => SqlFunctions.MySql.json_equals(x.String, "{\"a\":1}")));
        equals.Should().Throw<NotSupportedException>().WithMessage("*json_equals*not supported*");

        var normalize = () => SqlOf(ctx, e.Select(x => SqlFunctions.MySql.json_normalize(x.String)));
        normalize.Should().Throw<NotSupportedException>().WithMessage("*json_normalize*not supported*");
    }
}
