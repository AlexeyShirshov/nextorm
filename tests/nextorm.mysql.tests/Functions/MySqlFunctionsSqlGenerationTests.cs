using FluentAssertions;
using NextORM.Core;

namespace NextORM.MySql.Tests;

/// <summary>SQL generation for the MySQL/MariaDB-only native functions on MySQL (no database).</summary>
public class MySqlFunctionsSqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    [Fact]
    public void StringAndConditionalFunctions_ShouldRenderNatively()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.find_in_set(x.String, "a,b,c")))
            .Should().Contain("find_in_set(somestring, 'a,b,c')");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.field(x.String, "a", "b")))
            .Should().Contain("field(somestring, 'a', 'b')");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.elt(2, "a", "b")))
            .Should().Contain("elt(2, 'a', 'b')");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.substring_index(x.String, ".", 2)))
            .Should().Contain("substring_index(somestring, '.', 2)");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.format(1234.5m, 2)))
            .Should().Contain("format(1234.5, 2)");
    }

    [Fact]
    public void DateFunctions_ShouldRenderNatively()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.str_to_date("2023-01-02", "%Y-%m-%d")))
            .Should().Contain("str_to_date('2023-01-02', '%Y-%m-%d')");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.date_format(x.Datetime, "%Y-%m-%d")))
            .Should().Contain("date_format(dt, '%Y-%m-%d')");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.from_unixtime(1672656000L)))
            .Should().Contain("from_unixtime(1672656000)");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.unix_timestamp(x.Datetime)))
            .Should().Contain("unix_timestamp(dt)");
    }

    [Fact]
    public void HashAndInetFunctions_ShouldRenderNatively()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.md5(x.String)))
            .Should().Contain("md5(somestring)");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.sha1(x.String)))
            .Should().Contain("sha1(somestring)");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.sha2(x.String, 256)))
            .Should().Contain("sha2(somestring, 256)");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.inet_aton("127.0.0.1")))
            .Should().Contain("inet_aton('127.0.0.1')");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.inet_ntoa(2130706433L)))
            .Should().Contain("inet_ntoa(2130706433)");
    }

    [Fact]
    public void JsonMutationFunctions_ShouldRenderNatively()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.json_set("{}", "$.a", "1")))
            .Should().Contain("json_set('{}', '$.a', '1')");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.json_insert("{}", "$.a", "1")))
            .Should().Contain("json_insert('{}', '$.a', '1')");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.json_replace("{}", "$.a", "1")))
            .Should().Contain("json_replace('{}', '$.a', '1')");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.json_remove("{}", "$.a")))
            .Should().Contain("json_remove('{}', '$.a')");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.json_merge_patch("{}", "{\"a\":1}")))
            .Should().Contain("json_merge_patch('{}', '{\"a\":1}')");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.json_merge_preserve("{}", "{\"a\":1}")))
            .Should().Contain("json_merge_preserve('{}', '{\"a\":1}')");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.json_array_append("[]", "$", "1")))
            .Should().Contain("json_array_append('[]', '$', '1')");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.json_array_insert("[]", "$[0]", "1")))
            .Should().Contain("json_array_insert('[]', '$[0]', '1')");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.json_depth("{\"a\":[1]}")))
            .Should().Contain("json_depth('{\"a\":[1]}')");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.json_keys("{\"a\":1}")))
            .Should().Contain("json_keys('{\"a\":1}')");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.json_length("[1,2]")))
            .Should().Contain("json_length('[1,2]')");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.json_type("{\"a\":1}")))
            .Should().Contain("json_type('{\"a\":1}')");
    }

    [Fact]
    public void UuidFunctions_ShouldRenderNatively()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.uuid_to_bin("6ccd780c-baba-1026-9564-5b8c656024db")))
            .Should().Contain("uuid_to_bin('6ccd780c-baba-1026-9564-5b8c656024db')");

        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.bin_to_uuid(
                SqlFunctions.MySql.uuid_to_bin("6ccd780c-baba-1026-9564-5b8c656024db"))))
            .Should().Contain("bin_to_uuid(uuid_to_bin('6ccd780c-baba-1026-9564-5b8c656024db'))");

        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.bin_to_uuid(SqlFunctions.Parameter<byte[]>(0))))
            .Should().Contain("bin_to_uuid(@norm_p0)");
    }

    [Fact]
    public void FieldAndElt_WithoutValues_ShouldThrow()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var field = () => SqlOf(ctx, e.Select(x => SqlFunctions.MySql.field(x.String)));
        field.Should().Throw<NotSupportedException>().WithMessage("*field*at least one value*");

        var elt = () => SqlOf(ctx, e.Select(x => SqlFunctions.MySql.elt<string>(1)));
        elt.Should().Throw<NotSupportedException>().WithMessage("*elt*at least one value*");
    }

    [Fact]
    public void MariaDbOnlyFunctions_ShouldThrowBecauseNotSupported()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var regexp = () => SqlOf(ctx, e.Select(x => SqlFunctions.MySql.regexp_instr(x.String, "a")));
        regexp.Should().Throw<NotSupportedException>().WithMessage("*regexp_instr*not supported*");

        var nvl = () => SqlOf(ctx, e.Select(x => SqlFunctions.MySql.nvl(x.String, "fallback")));
        nvl.Should().Throw<NotSupportedException>().WithMessage("*nvl*not supported*");

        var addMonths = () => SqlOf(ctx, e.Select(x => SqlFunctions.MySql.add_months(x.Datetime, 1)));
        addMonths.Should().Throw<NotSupportedException>().WithMessage("*add_months*not supported*");

        var kdf = () => SqlOf(ctx, e.Select(x => SqlFunctions.MySql.kdf("p", "s", "i", "hkdf")));
        kdf.Should().Throw<NotSupportedException>().WithMessage("*kdf*not supported*");

        var sequence = () => SqlOf(ctx, e.Select(x => SqlFunctions.MySql.next_value_for("s")));
        sequence.Should().Throw<NotSupportedException>().WithMessage("*next_value_for*not supported*");
    }
}
