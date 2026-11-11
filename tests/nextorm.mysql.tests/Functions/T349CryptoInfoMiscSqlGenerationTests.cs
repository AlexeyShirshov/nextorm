using FluentAssertions;
using NextORM.Core;
using NextORM.MariaDb;
using NextORM.MySql;

namespace NextORM.MySql.Tests;

/// <summary>
/// SQL generation for the T349 MySQL 8.4 encryption/compression/hash, information and miscellaneous
/// catalogue (no database): every name renders its native spelling, the AES overload chain keeps its
/// documented positional arguments, SQL NULL arguments stay NULL, the <c>name_const</c> label and
/// <c>column_default</c> column are validated before any SQL is emitted, and MariaDB keeps the newly
/// enabled keys unsupported.
/// </summary>
public class T349CryptoInfoMiscSqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    [Fact]
    public void CryptoFunctions_ShouldRenderNatively()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.aes_encrypt(
                SqlFunctions.Parameter<byte[]>(0), SqlFunctions.Parameter<byte[]>(1))))
            .Should().Contain("aes_encrypt(@norm_p0, @norm_p1)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.aes_encrypt(
                SqlFunctions.Parameter<byte[]>(0), SqlFunctions.Parameter<byte[]>(1),
                SqlFunctions.Parameter<byte[]>(2))))
            .Should().Contain("aes_encrypt(@norm_p0, @norm_p1, @norm_p2)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.aes_encrypt(
                SqlFunctions.Parameter<byte[]>(0), SqlFunctions.Parameter<byte[]>(1),
                SqlFunctions.Parameter<byte[]>(2), "hkdf")))
            .Should().Contain("aes_encrypt(@norm_p0, @norm_p1, @norm_p2, _utf8mb4 X'686b6466')");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.aes_encrypt(
                SqlFunctions.Parameter<byte[]>(0), SqlFunctions.Parameter<byte[]>(1),
                SqlFunctions.Parameter<byte[]>(2), "hkdf", SqlFunctions.Parameter<byte[]>(3))))
            .Should().Contain("aes_encrypt(@norm_p0, @norm_p1, @norm_p2, _utf8mb4 X'686b6466', @norm_p3)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.aes_encrypt(
                SqlFunctions.Parameter<byte[]>(0), SqlFunctions.Parameter<byte[]>(1),
                SqlFunctions.Parameter<byte[]>(2), "hkdf", SqlFunctions.Parameter<byte[]>(3), "info")))
            .Should().Contain("aes_encrypt(@norm_p0, @norm_p1, @norm_p2, _utf8mb4 X'686b6466', @norm_p3, _utf8mb4 X'696e666f')");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.aes_encrypt(
                SqlFunctions.Parameter<byte[]>(0), SqlFunctions.Parameter<byte[]>(1),
                SqlFunctions.Parameter<byte[]>(2), "hkdf", SqlFunctions.Parameter<byte[]>(3), 1000)))
            .Should().Contain("aes_encrypt(@norm_p0, @norm_p1, @norm_p2, _utf8mb4 X'686b6466', @norm_p3, 1000)");

        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.aes_decrypt(
                SqlFunctions.Parameter<byte[]>(0), SqlFunctions.Parameter<byte[]>(1))))
            .Should().Contain("aes_decrypt(@norm_p0, @norm_p1)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.aes_decrypt(
                SqlFunctions.Parameter<byte[]>(0), SqlFunctions.Parameter<byte[]>(1),
                SqlFunctions.Parameter<byte[]>(2), "hkdf", SqlFunctions.Parameter<byte[]>(3), 1000)))
            .Should().Contain("aes_decrypt(@norm_p0, @norm_p1, @norm_p2, _utf8mb4 X'686b6466', @norm_p3, 1000)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.aes_decrypt(null, null)))
            .Should().Contain("aes_decrypt(null, null)");

        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.compress(SqlFunctions.Parameter<byte[]>(0))))
            .Should().Contain("compress(@norm_p0)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.compress(null)))
            .Should().Contain("compress(null)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.uncompress(SqlFunctions.Parameter<byte[]>(0))))
            .Should().Contain("uncompress(@norm_p0)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.uncompressed_length(SqlFunctions.Parameter<byte[]>(0))))
            .Should().Contain("uncompressed_length(@norm_p0)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.uncompressed_length(null)))
            .Should().Contain("uncompressed_length(null)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.random_bytes(16)))
            .Should().Contain("random_bytes(16)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.random_bytes(null)))
            .Should().Contain("random_bytes(null)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.validate_password_strength("abc")))
            .Should().Contain("validate_password_strength(_utf8mb4 X'616263')");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.statement_digest(x.String)))
            .Should().Contain("statement_digest(somestring)");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.statement_digest_text(x.String)))
            .Should().Contain("statement_digest_text(somestring)");
    }

    [Fact]
    public void InformationFunctions_ShouldRenderNatively()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { x.Id, V = SqlFunctions.MySql.found_rows() }))
            .Should().Contain("found_rows()");
        SqlOf(ctx, e.Select(x => new { x.Id, V = SqlFunctions.MySql.last_insert_id() }))
            .Should().Contain("last_insert_id()");
        SqlOf(ctx, e.Select(x => new { x.Id, V = SqlFunctions.MySql.last_insert_id(42UL) }))
            .Should().Contain("last_insert_id(42)");
        SqlOf(ctx, e.Select(x => new { x.Id, V = SqlFunctions.MySql.row_count() }))
            .Should().Contain("row_count()");
        SqlOf(ctx, e.Select(x => new { x.Id, V = SqlFunctions.MySql.current_role() }))
            .Should().Contain("current_role()");
        SqlOf(ctx, e.Select(x => new { x.Id, V = SqlFunctions.MySql.icu_version() }))
            .Should().Contain("icu_version()");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.charset(x.String)))
            .Should().Contain("charset(somestring)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.charset(null)))
            .Should().Contain("charset(null)");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.collation(x.String)))
            .Should().Contain("collation(somestring)");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.coercibility(x.String)))
            .Should().Contain("coercibility(somestring)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.coercibility(null)))
            .Should().Contain("coercibility(null)");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.benchmark(1L, x.String)))
            .Should().Contain("benchmark(1, somestring)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.benchmark(null, null)))
            .Should().Contain("benchmark(null, null)");
    }

    [Fact]
    public void MiscellaneousFunctions_ShouldRenderNatively()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { x.Id, V = SqlFunctions.MySql.uuid_short() }))
            .Should().Contain("uuid_short()");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.is_uuid(x.String)))
            .Should().Contain("is_uuid(somestring)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.is_uuid(null)))
            .Should().Contain("is_uuid(null)");
        SqlOf(ctx, e.Select(x => new { x.Id, V = SqlFunctions.MySql.sleep(0d) }))
            .Should().Contain("sleep(0)");
        SqlOf(ctx, e.Select(x => new { x.Id, V = SqlFunctions.MySql.grouping(x.String, x.Int) }))
            .Should().Contain("grouping(somestring, nullableint)");
        SqlOf(ctx, e.Select(x => new { x.Id, V = SqlFunctions.MySql.name_const("n", x.Id) }))
            .Should().Contain("name_const('n', id)");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.column_default(x.Id)))
            .Should().Contain("default(id)");
    }

    [Fact]
    public void NameConstAndColumnDefault_InvalidShapes_ShouldThrowBeforeAnySql()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var parameterName = () => SqlOf(ctx, e.Select(_ =>
            SqlFunctions.MySql.name_const(SqlFunctions.Parameter<string>(0), 1L)));
        parameterName.Should().Throw<NotSupportedException>()
            .WithMessage("*NAME_CONST*argument 0*compile-time string literal*");

        var nullName = () => SqlOf(ctx, e.Select(x =>
            SqlFunctions.MySql.name_const(null, x.Id)));
        nullName.Should().Throw<NotSupportedException>()
            .WithMessage("*NAME_CONST*non-null name literal*");

        var constant = () => SqlOf(ctx, e.Select(_ =>
            SqlFunctions.MySql.column_default(SqlFunctions.Parameter<long>(0))));
        constant.Should().Throw<NotSupportedException>()
            .WithMessage("MySQL DEFAULT() requires a column reference; constants, parameters, casts, " +
                "and computed expressions are not supported.");

        var computed = () => SqlOf(ctx, e.Select(x =>
            SqlFunctions.MySql.column_default(x.Id + 1)));
        computed.Should().Throw<NotSupportedException>()
            .WithMessage("*DEFAULT() requires a column reference*");
    }

    [Fact]
    public void T349CryptoInfoMiscFunctions_ShouldAllBeReportedSupported()
    {
        var capability = MySqlDialect.Instance.MySqlFunctions!;
        string[] names =
        [
            "aes_encrypt", "aes_decrypt", "compress", "uncompress", "uncompressed_length",
            "random_bytes", "validate_password_strength", "statement_digest", "statement_digest_text",
            "found_rows", "last_insert_id", "row_count", "current_role", "charset", "collation",
            "coercibility", "benchmark", "icu_version",
            "uuid_short", "is_uuid", "sleep", "grouping", "name_const", "column_default"
        ];

        names.Should().HaveCount(24);
        foreach (var name in names)
            capability.Supports(name).Should().BeTrue($"'{name}' is a MySQL 8.4 gate key");
    }

    [Fact]
    public void MariaDb_ShouldClaimTheKeysT350ProvedAndKeepTheAbsentOne()
    {
        var capability = MariaDbDialect.Instance.MySqlFunctions!;

        // #284 (T350 S1, MariaDB 11.4.13) accepted both names, so the T349 temporary exclusion is resolved.
        capability.Supports("uuid_short").Should().BeTrue("#284 delivered UUID_SHORT on MariaDB 11.4");
        capability.Supports("aes_encrypt").Should().BeTrue("#284 delivered AES_ENCRYPT on MariaDB 11.4");
        capability.Supports("grouping").Should().BeFalse("MariaDB's rollup uses GROUPING differently");
    }
}
