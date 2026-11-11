using FluentAssertions;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// T282 (#282): SQLite has no standard BASE64 or cryptographic generator, so all three portable binary
/// functions are rejected before any SQL is emitted, with the exact unsupported-provider message.
/// </summary>
public class T282BuiltinsSqlGenerationTests
{
    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None))
            .DbCommand.CommandText;

    [Fact]
    public void T282_Base64Encode_ShouldFailClosed()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new
        {
            Value = SqlFunctions.Sql.base64_encode(SqlFunctions.Parameter<byte[]>(0))
        }));

        act.Should().Throw<NotSupportedException>()
            .WithMessage("The base64_encode function is not supported by this provider.");
    }

    [Fact]
    public void T282_Base64Decode_ShouldFailClosed()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new
        {
            Value = SqlFunctions.Sql.base64_decode("AAEC/f7/")
        }));

        act.Should().Throw<NotSupportedException>()
            .WithMessage("The base64_decode function is not supported by this provider.");
    }

    [Fact]
    public void T282_Crypto_ShouldFailClosed()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new
        {
            Value = SqlFunctions.Sql.crypt_gen_random(16)
        }));

        act.Should().Throw<NotSupportedException>()
            .WithMessage("The crypt_gen_random function is not supported by this provider.");
    }

    [Fact]
    public void T282_CryptoLengthOutsideThePortableDomain_ShouldThrowBeforeCapability()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var zero = () => SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new { Value = SqlFunctions.Sql.crypt_gen_random(0) }));
        var tooLong = () => SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new { Value = SqlFunctions.Sql.crypt_gen_random(1025) }));

        zero.Should().Throw<ArgumentOutOfRangeException>();
        tooLong.Should().Throw<ArgumentOutOfRangeException>();
    }
}
