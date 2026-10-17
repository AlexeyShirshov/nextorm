using FluentAssertions;
using NextORM.Core;

namespace NextORM.MariaDb.Tests;

/// <summary>
/// MariaDB inherits the MySQL/MariaDB-only native surface, so every name renders its native form
/// through the inherited dialect (no database).
/// </summary>
public class MySqlFunctionsSqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    [Fact]
    public void StringAndConditionalFunctions_ShouldRenderNatively()
    {
        using var ctx = MariaDbTestContext.Create();
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
        using var ctx = MariaDbTestContext.Create();
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
        using var ctx = MariaDbTestContext.Create();
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
        using var ctx = MariaDbTestContext.Create();
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
    public void UuidFunctions_ShouldThrowBecauseNotSupported()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var uuid = () => SqlOf(ctx, e.Select(x => SqlFunctions.MySql.uuid_to_bin("6ccd780c-baba-1026-9564-5b8c656024db")));
        uuid.Should().Throw<NotSupportedException>().WithMessage("*uuid_to_bin*not supported*");

        var bin = () => SqlOf(ctx, e.Select(x => SqlFunctions.MySql.bin_to_uuid(SqlFunctions.Parameter<byte[]>(0))));
        bin.Should().Throw<NotSupportedException>().WithMessage("*bin_to_uuid*not supported*");
    }

    [Fact]
    public void MariaDbOnlyRegexpAndConditionalFunctions_ShouldRenderNatively()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.regexp_instr(x.String, "a")))
            .Should().Contain("regexp_instr(somestring, 'a')");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.regexp_substr(x.String, "a")))
            .Should().Contain("regexp_substr(somestring, 'a')");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.regexp_replace(x.String, "a", "b")))
            .Should().Contain("regexp_replace(somestring, 'a', 'b')");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.nvl(x.String, "fallback")))
            .Should().Contain("nvl(somestring, 'fallback')");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.nvl2(x.String, "yes", "no")))
            .Should().Contain("nvl2(somestring, 'yes', 'no')");
    }

    [Fact]
    public void MariaDbOnlyDateAndNumberFunctions_ShouldRenderNatively()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.add_months(x.Datetime, 2)))
            .Should().Contain("add_months(dt, 2)");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.months_between(x.Datetime, x.Datetime)))
            .Should().Contain("months_between(dt, dt)");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.to_char(x.Datetime, "YYYY-MM-DD")))
            .Should().Contain("to_char(dt, 'YYYY-MM-DD')");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.to_date("2023-01-02", "YYYY-MM-DD")))
            .Should().Contain("to_date('2023-01-02', 'YYYY-MM-DD')");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.to_number("100.00", "999.99")))
            .Should().Contain("to_number('100.00', '999.99')");
    }

    [Fact]
    public void MariaDbOnlyHashAndJsonFunctions_ShouldRenderNatively()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.kdf("foo", "bar", "info", "hkdf")))
            .Should().Contain("kdf('foo', 'bar', 'info', 'hkdf')");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.xxh3(x.String)))
            .Should().Contain("xxh3(somestring)");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.xxh32(x.String)))
            .Should().Contain("xxh32(somestring)");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.json_detailed("{}")))
            .Should().Contain("json_detailed('{}')");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.json_compact("{}")))
            .Should().Contain("json_compact('{}')");
    }

    [Fact]
    public void MariaDbSequenceFunctions_ShouldRenderIdentifiers()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.next_value_for("my_seq")))
            .Should().Contain("next value for my_seq");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.nextval("my_seq")))
            .Should().Contain("nextval(my_seq)");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.setval("my_seq", 42)))
            .Should().Contain("setval(my_seq, 42)");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.lastval("my_seq")))
            .Should().Contain("lastval(my_seq)");
    }
}
