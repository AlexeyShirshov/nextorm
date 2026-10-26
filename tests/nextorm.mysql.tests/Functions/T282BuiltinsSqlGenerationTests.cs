using FluentAssertions;
using NextORM.Core;

namespace NextORM.MySql.Tests;

/// <summary>
/// T282 (#282): SQL generation for the portable binary functions on MySQL. Assertions cover the
/// native <c>TO_BASE64</c>/<c>FROM_BASE64</c> spellings with the CRLF wrapping removal, the
/// typed-NULL tokens and the <c>RANDOM_BYTES</c> crypto wrapper.
/// </summary>
public class T282BuiltinsSqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    [Fact]
    public void T282_Base64_ShouldEmitToBase64AndFromBase64()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new
        {
            Encoded = SqlFunctions.Sql.base64_encode(SqlFunctions.Parameter<byte[]>(0)),
            Decoded = SqlFunctions.Sql.base64_decode("AAEC/f7/")
        }));

        sql.Should().Contain("REPLACE(REPLACE(TO_BASE64(");
        sql.Should().Contain("CHAR(13), '')");
        sql.Should().Contain("CHAR(10), '')");
        sql.Should().Contain(" AS CHAR)");
        sql.Should().MatchRegex(@"CAST\(REPLACE\(REPLACE\(TO_BASE64\(@\w+\), CHAR\(13\), ''\), CHAR\(10\), ''\) AS CHAR\)");
        sql.Should().Contain("FROM_BASE64(_utf8mb4 X'414145432f66372f')");
    }

    [Fact]
    public void T282_Base64NullLiterals_ShouldRenderTypedNulls()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new
        {
            Encoded = SqlFunctions.Sql.base64_encode(null),
            Decoded = SqlFunctions.Sql.base64_decode(null)
        }));

        sql.Should().Contain("CAST(REPLACE(REPLACE(TO_BASE64(CAST(NULL AS BINARY)), CHAR(13), ''), CHAR(10), '') AS CHAR)");
        sql.Should().Contain("FROM_BASE64(CAST(NULL AS CHAR))");
    }

    [Fact]
    public void T282_Crypto_ShouldEmitTheFrozenCaseWrapper()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new
        {
            Constant = SqlFunctions.Sql.crypt_gen_random(16),
            Column = SqlFunctions.Sql.crypt_gen_random(x.Int),
            Null = SqlFunctions.Sql.crypt_gen_random(null)
        }));

        sql.Should().Contain("CASE WHEN 16 IS NULL THEN CAST(NULL AS BINARY) ELSE RANDOM_BYTES(16) END");
        sql.Should().Contain("CASE WHEN nullableint IS NULL THEN CAST(NULL AS BINARY) ELSE RANDOM_BYTES(nullableint) END");
        sql.Should().Contain("CASE WHEN CAST(NULL AS SIGNED) IS NULL THEN CAST(NULL AS BINARY) ELSE RANDOM_BYTES(CAST(NULL AS SIGNED)) END");
    }

    [Fact]
    public void T282_CryptoLengthOutsideThePortableDomain_ShouldThrow()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var zero = () => SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new { Value = SqlFunctions.Sql.crypt_gen_random(0) }));
        var tooLong = () => SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new { Value = SqlFunctions.Sql.crypt_gen_random(1025) }));

        zero.Should().Throw<ArgumentOutOfRangeException>();
        tooLong.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void T282_QuotedIdentifiers_ShouldNotChangeTheFunctionSql()
    {
        using var ctx = MySqlTestContext.CreateQuoted();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new
        {
            Encoded = SqlFunctions.Sql.base64_encode(SqlFunctions.Parameter<byte[]>(0)),
            Column = SqlFunctions.Sql.crypt_gen_random(x.Int)
        }));

        sql.Should().MatchRegex(@"CAST\(REPLACE\(REPLACE\(TO_BASE64\(@\w+\), CHAR\(13\), ''\), CHAR\(10\), ''\) AS CHAR\)");
        sql.Should().Contain("RANDOM_BYTES(`nullableint`)");
    }
}
