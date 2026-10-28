using System.Reflection;
using FluentAssertions;
using NextORM.Core;
using NextORM.MySql;

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
            .Should().Contain("find_in_set(somestring, _utf8mb4 X'612c622c63')");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.field(x.String, "a", "b")))
            .Should().Contain("field(somestring, _utf8mb4 X'61', _utf8mb4 X'62')");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.elt(2, "a", "b")))
            .Should().Contain("elt(2, _utf8mb4 X'61', _utf8mb4 X'62')");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.substring_index(x.String, ".", 2)))
            .Should().Contain("substring_index(somestring, _utf8mb4 X'2e', 2)");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.format(1234.5m, 2)))
            .Should().Contain("format(1234.5, 2)");
    }

    [Fact]
    public void DateFunctions_ShouldRenderNatively()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.str_to_date("2023-01-02", "%Y-%m-%d")))
            .Should().Contain("str_to_date(_utf8mb4 X'323032332d30312d3032', _utf8mb4 X'25592d256d2d2564')");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.date_format(x.Datetime, "%Y-%m-%d")))
            .Should().Contain("date_format(dt, _utf8mb4 X'25592d256d2d2564')");
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
            .Should().Contain("inet_aton(_utf8mb4 X'3132372e302e302e31')");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.inet_ntoa(2130706433L)))
            .Should().Contain("inet_ntoa(2130706433)");
    }

    [Fact]
    public void JsonMutationFunctions_ShouldRenderNatively()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.json_set("{}", "$.a", "1")))
            .Should().Contain("json_set(_utf8mb4 X'7b7d', _utf8mb4 X'242e61', _utf8mb4 X'31')");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.json_insert("{}", "$.a", "1")))
            .Should().Contain("json_insert(_utf8mb4 X'7b7d', _utf8mb4 X'242e61', _utf8mb4 X'31')");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.json_replace("{}", "$.a", "1")))
            .Should().Contain("json_replace(_utf8mb4 X'7b7d', _utf8mb4 X'242e61', _utf8mb4 X'31')");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.json_remove("{}", "$.a")))
            .Should().Contain("json_remove(_utf8mb4 X'7b7d', _utf8mb4 X'242e61')");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.json_merge_patch("{}", "{\"a\":1}")))
            .Should().Contain("json_merge_patch(_utf8mb4 X'7b7d', _utf8mb4 X'7b2261223a317d')");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.json_merge_preserve("{}", "{\"a\":1}")))
            .Should().Contain("json_merge_preserve(_utf8mb4 X'7b7d', _utf8mb4 X'7b2261223a317d')");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.json_array_append("[]", "$", "1")))
            .Should().Contain("json_array_append(_utf8mb4 X'5b5d', _utf8mb4 X'24', _utf8mb4 X'31')");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.json_array_insert("[]", "$[0]", "1")))
            .Should().Contain("json_array_insert(_utf8mb4 X'5b5d', _utf8mb4 X'245b305d', _utf8mb4 X'31')");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.json_depth("{\"a\":[1]}")))
            .Should().Contain("json_depth(_utf8mb4 X'7b2261223a5b315d7d')");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.json_keys("{\"a\":1}")))
            .Should().Contain("json_keys(_utf8mb4 X'7b2261223a317d')");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.json_length("[1,2]")))
            .Should().Contain("json_length(_utf8mb4 X'5b312c325d')");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.json_type("{\"a\":1}")))
            .Should().Contain("json_type(_utf8mb4 X'7b2261223a317d')");
    }

    [Fact]
    public void JsonStorageFree_ShouldRenderNatively()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.json_storage_free("{\"a\":1}")))
            .Should().Contain("json_storage_free(_utf8mb4 X'7b2261223a317d')");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.json_storage_free(null)))
            .Should().Contain("json_storage_free(null)");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.json_storage_free(x.String)))
            .Should().Contain("json_storage_free(somestring)");
    }

    [Fact]
    public void JsonStorageFree_ShouldRenderNullableAndBoundArguments()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var captured = "{\"a\":1}";
        var method = typeof(MySqlFunctions).GetMethod(nameof(MySqlFunctions.json_storage_free))!;

        method.ReturnType.Should().Be<long?>();
        method.IsGenericMethod.Should().BeFalse();
        var parameter = method.GetParameters().Should().ContainSingle().Subject;
        parameter.ParameterType.Should().Be(typeof(string));
        parameter.IsOptional.Should().BeFalse();
        parameter.GetCustomAttribute<ParamArrayAttribute>().Should().BeNull();
        typeof(MySqlFunctions)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Count(m => m.Name == nameof(MySqlFunctions.json_storage_free))
            .Should().Be(1);

        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.json_storage_free(SqlFunctions.Parameter<string>(0))))
            .Should().Contain("json_storage_free(@norm_p0)");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.json_storage_free(captured)))
            .Should().Contain("json_storage_free(@captured)");
    }

    [Fact]
    public void NativeFunctionSurface_ShouldHaveCapabilityCoverage()
    {
        var capability = MySqlDialect.Instance.MySqlFunctions!;
        string[] mariaDbOnly =
        [
            "regexp_substr", "regexp_instr", "regexp_replace", "nvl", "nvl2", "add_months",
            "months_between", "to_char", "to_date", "to_number", "kdf", "xxh3", "xxh32",
            "json_detailed", "json_compact", "json_equals", "json_normalize",
            "next_value_for", "nextval", "setval", "lastval",
            // #284 (T350): delivered on MariaDB only — MySQL has none of these names.
            "rownum", "binlog_gtid_pos", "previous_value_for",
            "sformat", "trim_oracle", "natural_sort_key", "lengthb", "chr",
            "json_loose", "json_array_intersect", "json_key_value",
            "json_object_filter_keys", "json_object_to_array"
        ];
        var declared = typeof(MySqlFunctions)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)
            .Select(m => m.Name)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        // T285b declares the residual percentile family on the surface as *named refusals*: MySQL has
        // neither a median aggregate nor the WITHIN GROUP grammar, so the member exists to carry the
        // precise message. It is therefore expected to be unsupported here, not supported. T349 adds
        // the JSON_TABLE shape refusal (a table-producing construct, never a scalar expression) and the
        // UUID/VALUES refusals (a v1 UUID and a statement-position-only name this scalar API rejects).
        string[] declaredRefusals =
        [
            "median", "percentile_cont", "percentile_disc", "percentile", "json_table", "uuid", "values",
            // #284 (T350): members whose body only carries the MariaDB 11.4 refusal text (the clause /
            // baseline / table-dependent requirements below the surface), plus the 11.7 vector gate.
            "column_get", "trunc", "_rowid",
            "vec_from_text", "vec_to_text", "vec_distance_euclidean", "vec_distance_cosine", "vec_distance"
        ];
        var unsupportedByDesign = mariaDbOnly.Concat(declaredRefusals).ToArray();

        declared.Should().NotBeEmpty();
        foreach (var name in declared.Except(unsupportedByDesign, StringComparer.Ordinal))
            capability.Supports(name).Should().BeTrue($"MySQL must support the declared marker '{name}'");
        foreach (var name in declaredRefusals)
            capability.Supports(name).Should().BeFalse($"'{name}' is a declared refusal on MySQL");
        foreach (var name in mariaDbOnly)
            capability.Supports(name).Should().BeFalse($"'{name}' is MariaDB-only");
        capability.Supports("json_storage_free").Should().BeTrue();
    }

    [Fact]
    public void UuidFunctions_ShouldRenderNatively()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.uuid_to_bin("6ccd780c-baba-1026-9564-5b8c656024db")))
            .Should().Contain("uuid_to_bin(_utf8mb4 X'36636364373830632d626162612d313032362d393536342d356238633635363032346462')");

        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.bin_to_uuid(
                SqlFunctions.MySql.uuid_to_bin("6ccd780c-baba-1026-9564-5b8c656024db"))))
            .Should().Contain("bin_to_uuid(uuid_to_bin(_utf8mb4 X'36636364373830632d626162612d313032362d393536342d356238633635363032346462'))");

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

    [Fact]
    public void T284_JsonFunctions_ShouldThrowOnUnsupportedProvider()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var equals = () => SqlOf(ctx, e.Select(x => SqlFunctions.MySql.json_equals(x.String, "{\"a\":1}")));
        equals.Should().Throw<NotSupportedException>().WithMessage("*json_equals*not supported*");

        var normalize = () => SqlOf(ctx, e.Select(x => SqlFunctions.MySql.json_normalize(x.String)));
        normalize.Should().Throw<NotSupportedException>().WithMessage("*json_normalize*not supported*");
    }
}
