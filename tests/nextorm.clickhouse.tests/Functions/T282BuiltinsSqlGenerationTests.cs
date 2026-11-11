using FluentAssertions;
using NextORM.Core;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// T282 (#282), revision rv2: the ClickHouse adapter binds a <c>byte[]</c> argument as
/// <c>Array(UInt8)</c> and reads a <c>String</c> result as <c>string</c>, so the <c>byte[]</c>
/// signature of the portable BASE64 pair is not usable there. Both names therefore fall through to the
/// default capability refusal, and crypto was never declared.
/// </summary>
public class T282BuiltinsSqlGenerationTests
{
    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None))
            .DbCommand.CommandText;

    private static void AssertRefused(Action act, string name)
        => act.Should().Throw<NotSupportedException>()
            .WithMessage($"The {name} function is not supported by this provider.");

    [Fact]
    public void T282_Base64Encode_ShouldFailClosed()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => { SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new
        {
            Value = SqlFunctions.Sql.base64_encode(SqlFunctions.Parameter<byte[]>(0))
        })); };

        AssertRefused(act, "base64_encode");
    }

    [Fact]
    public void T282_Base64Decode_ShouldFailClosed()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => { SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new
        {
            Value = SqlFunctions.Sql.base64_decode("AAEC/f7/")
        })); };

        AssertRefused(act, "base64_decode");
    }

    [Fact]
    public void T282_Crypto_ShouldFailClosed()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => { SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new
        {
            Value = SqlFunctions.Sql.crypt_gen_random(16)
        })); };

        AssertRefused(act, "crypt_gen_random");
    }
}
