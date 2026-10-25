using FluentAssertions;
using NextORM.Core;

namespace NextORM.SqlServer.Tests;

/// <summary>
/// T282 (#282): SQL generation for the portable binary functions on SQL Server. Assertions cover the
/// exact native spelling, the typed-NULL argument tokens, the frozen crypto <c>CASE</c> wrapper, the
/// portable length guard and the fact that identifier quoting does not change the function SQL.
/// </summary>
public class T282BuiltinsSqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    [Fact]
    public void T282_Base64_ShouldEmitTheNativeFunctions()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new
        {
            Encoded = SqlFunctions.Sql.base64_encode(SqlFunctions.Parameter<byte[]>(0)),
            Decoded = SqlFunctions.Sql.base64_decode("AAEC/f7/")
        }));

        sql.Should().MatchRegex(@"BASE64_ENCODE\(@\w+\)");
        sql.Should().Contain("BASE64_DECODE(N'AAEC/f7/')");
        sql.ToLowerInvariant().Should().NotContain("to_base64");
    }

    [Fact]
    public void T282_Base64NullLiterals_ShouldRenderTypedNulls()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new
        {
            Encoded = SqlFunctions.Sql.base64_encode(null),
            Decoded = SqlFunctions.Sql.base64_decode(null)
        }));

        sql.Should().Contain("BASE64_ENCODE(CAST(NULL AS varbinary(8000)))");
        sql.Should().Contain("BASE64_DECODE(CAST(NULL AS varchar(8000)))");
    }

    [Fact]
    public void T282_Crypto_ShouldEmitTheFrozenCaseWrapper()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new
        {
            Constant = SqlFunctions.Sql.crypt_gen_random(16),
            Column = SqlFunctions.Sql.crypt_gen_random(x.Int),
            Null = SqlFunctions.Sql.crypt_gen_random(null)
        }));

        sql.Should().Contain("CASE WHEN 16 IS NULL THEN CAST(NULL AS varbinary(8000)) ELSE CRYPT_GEN_RANDOM(16) END");
        sql.Should().Contain("CASE WHEN nullableint IS NULL THEN CAST(NULL AS varbinary(8000)) ELSE CRYPT_GEN_RANDOM(nullableint) END");
        sql.Should().Contain("CASE WHEN CAST(NULL AS int) IS NULL THEN CAST(NULL AS varbinary(8000)) ELSE CRYPT_GEN_RANDOM(CAST(NULL AS int)) END");
    }

    [Fact]
    public void T282_CryptoLengthOutsideThePortableDomain_ShouldThrow()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var zero = () => SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new { Value = SqlFunctions.Sql.crypt_gen_random(0) }));
        var negative = () => SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new { Value = SqlFunctions.Sql.crypt_gen_random(-1) }));
        var tooLong = () => SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new { Value = SqlFunctions.Sql.crypt_gen_random(1025) }));

        zero.Should().Throw<ArgumentOutOfRangeException>();
        negative.Should().Throw<ArgumentOutOfRangeException>();
        tooLong.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void T282_QuotedIdentifiers_ShouldNotChangeTheFunctionSql()
    {
        using var ctx = SqlServerTestContext.CreateQuoted();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new
        {
            Encoded = SqlFunctions.Sql.base64_encode(SqlFunctions.Parameter<byte[]>(0)),
            Column = SqlFunctions.Sql.crypt_gen_random(x.Int)
        }));

        sql.Should().MatchRegex(@"BASE64_ENCODE\(@\w+\)");
        sql.Should().Contain("CRYPT_GEN_RANDOM([nullableint])");
    }
}
