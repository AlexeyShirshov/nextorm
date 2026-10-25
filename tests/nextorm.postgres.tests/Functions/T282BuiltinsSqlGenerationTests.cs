using FluentAssertions;
using NextORM.Core;

namespace NextORM.Postgres.Tests;

/// <summary>
/// T282 (#282): SQL generation for the portable binary functions on PostgreSQL. Assertions cover the
/// native <c>encode</c>/<c>decode</c> spellings with the line-wrap removal, the typed-NULL tokens and
/// the <c>pgcrypto</c>-based crypto wrapper.
/// </summary>
public class T282BuiltinsSqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    [Fact]
    public void T282_Base64_ShouldEmitEncodeDecodeWithWrapRemoval()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new
        {
            Encoded = SqlFunctions.Sql.base64_encode(SqlFunctions.Parameter<byte[]>(0)),
            Decoded = SqlFunctions.Sql.base64_decode("AAEC/f7/")
        }));

        sql.Should().MatchRegex(@"replace\(encode\(@\w+, 'base64'\), E'\\n', ''\)");
        sql.Should().Contain("decode(E'AAEC/f7/', 'base64')");
    }

    [Fact]
    public void T282_Base64NullLiterals_ShouldRenderTypedNulls()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new
        {
            Encoded = SqlFunctions.Sql.base64_encode(null),
            Decoded = SqlFunctions.Sql.base64_decode(null)
        }));

        sql.Should().Contain("replace(encode(CAST(NULL AS bytea), 'base64'), E'\\n', '')");
        sql.Should().Contain("decode(CAST(NULL AS text), 'base64')");
    }

    [Fact]
    public void T282_Crypto_ShouldEmitTheFrozenCaseWrapper()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new
        {
            Constant = SqlFunctions.Sql.crypt_gen_random(16),
            Column = SqlFunctions.Sql.crypt_gen_random(x.Int),
            Null = SqlFunctions.Sql.crypt_gen_random(null)
        }));

        sql.Should().Contain("CASE WHEN 16 IS NULL THEN CAST(NULL AS bytea) ELSE gen_random_bytes(16) END");
        sql.Should().Contain("CASE WHEN nullableint IS NULL THEN CAST(NULL AS bytea) ELSE gen_random_bytes(nullableint) END");
        sql.Should().Contain("CASE WHEN CAST(NULL AS integer) IS NULL THEN CAST(NULL AS bytea) ELSE gen_random_bytes(CAST(NULL AS integer)) END");
    }

    [Fact]
    public void T282_CryptoLengthOutsideThePortableDomain_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var zero = () => SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new { Value = SqlFunctions.Sql.crypt_gen_random(0) }));
        var tooLong = () => SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new { Value = SqlFunctions.Sql.crypt_gen_random(1025) }));

        zero.Should().Throw<ArgumentOutOfRangeException>();
        tooLong.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void T282_QuotedIdentifiers_ShouldNotChangeTheFunctionSql()
    {
        using var ctx = PostgresTestContext.CreateQuoted();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new
        {
            Encoded = SqlFunctions.Sql.base64_encode(SqlFunctions.Parameter<byte[]>(0)),
            Column = SqlFunctions.Sql.crypt_gen_random(x.Int)
        }));

        sql.Should().MatchRegex(@"replace\(encode\(@\w+, 'base64'\), E'\\n', ''\)");
        sql.Should().Contain("gen_random_bytes(\"nullableint\")");
    }
}
