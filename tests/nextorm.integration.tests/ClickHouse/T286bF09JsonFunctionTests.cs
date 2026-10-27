using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using ClickHouse.Driver;
using ClickHouse.Driver.ADO;
using FluentAssertions;
using FluentAssertions.Execution;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// T286b D9 (F09 JSON): live execution of the ClickHouse-only JSON members against the pinned
/// <c>25.8-alpine</c> (25.8.33.6) server. The tests pin RFC 7396 merge-patch and its malformed-input
/// error, the <c>Nullable(UInt64)</c> array length, the native-JSON dynamic/shared path pairs with
/// their <c>WithTypes</c> maps and their argument-type refusals, <c>JSONKey</c>,
/// <c>JSONExtractKeysAndValuesRaw</c>, <c>JSONExtractUInt</c>, <c>isValidJSON</c> and the
/// <c>simpleJSONExtract*</c> fast path, the NULL flow of every nullable result, and the deterministic
/// refusals of the two members absent from 25.8 (<c>prettyPrintJSON</c>,
/// <c>JSONExtractUIntOrDefault</c>).
/// </summary>
public sealed class T286bF09JsonFunctionTests : ProviderTestSuite
{
    [SqlTable("t286b_f09_entity")]
    internal interface IT286bJsonEntity
    {
        [Key]
        [Column("id")]
        int Id { get; set; }

        [Column("doc")]
        string? Doc { get; set; }

        [Column("doc_nn")]
        string DocNonNull { get; set; }

        [Column("ndoc")]
        string? NativeDoc { get; set; }

        [Column("nndoc")]
        string? NullableNativeDoc { get; set; }

        [Column("idx")]
        int Idx { get; set; }
    }

    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    private const string Doc =
        "{\"a\":1,\"b\":\"x\",\"c\":[1,2],\"d\":{\"e\":true},\"f\":1.5,\"o\":{\"k\":7},\"t\":true}";

    private static T Scalar<T>(string expression)
    {
        using var context = ClickHouseTestProvider.Instance.CreateContext();
        using var result = context.ExecuteRaw($"select {expression} as v");
        return result.Read<T>().Single();
    }

    private static void Seed()
    {
        using var connection = new ClickHouseConnection(ClickHouseContainer.ConnectionString);
        connection.Open();
        foreach (var statement in new[]
                 {
                     "drop table if exists t286b_f09_entity",
                     "create table t286b_f09_entity (id Int32, doc Nullable(String), doc_nn String, "
                     + "ndoc JSON(max_dynamic_paths=1), nndoc Nullable(JSON), idx Int32) engine = Memory",
                     "insert into t286b_f09_entity (id, doc, doc_nn, ndoc, nndoc, idx) values "
                     + "(1, '" + Doc + "', '" + Doc + "', '{\"a\":42}', '{\"a\":1}', 1), "
                     + "(2, NULL, 'null', '{\"b\":\"Hello\"}', NULL, 2), "
                     + "(3, 'not json', 'not json', '{\"a\":[1,2,3],\"c\":\"2020-01-01\"}', '{\"c\":1}', 1), "
                     + "(4, '[1,2,3]', '[1,2,3]', '{}', '{}', 1)"
                 })
        {
            using var command = connection.CreateCommand();
            command.CommandText = statement;
            command.ExecuteNonQuery();
        }
    }

    [Fact]
    public void MergePatch_ShouldApplyRfc7396Patch()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var two = ctx.From<IT286bJsonEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new { V = SqlFunctions.ClickHouse.json_merge_patch("{\"a\":1,\"b\":2}", "{\"b\":3,\"c\":4}") })
            .First();
        var three = ctx.From<IT286bJsonEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new { V = SqlFunctions.ClickHouse.json_merge_patch("{\"a\":1}", "{\"b\":2}", "{\"c\":3}") })
            .First();
        var column = ctx.From<IT286bJsonEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new { V = SqlFunctions.ClickHouse.json_merge_patch(x.Doc, "{\"a\":99}") })
            .First();

        using (new AssertionScope())
        {
            two.V.Should().Be("{\"a\":1,\"b\":3,\"c\":4}");
            three.V.Should().Be("{\"a\":1,\"b\":2,\"c\":3}");
            using var patch = JsonDocument.Parse(column.V);
            patch.RootElement.GetProperty("a").GetInt32().Should().Be(99);
            patch.RootElement.GetProperty("b").GetString().Should().Be("x");
        }

        FluentActions.Invoking(() => ctx.From<IT286bJsonEntity>()
                .Where(x => x.Id == 1)
                .Select(x => new { V = SqlFunctions.ClickHouse.json_merge_patch("not json", "{\"b\":2}") })
                .First())
            .Should().Throw<ClickHouseServerException>()
            .WithMessage("*Wrong JSON string to merge*");
    }

    [Fact]
    public void JsonArrayLength_ShouldReturnNullForNonArrayAndMalformed()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var r = ctx.From<IT286bJsonEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Array = SqlFunctions.ClickHouse.json_array_length("[1,2,3]"),
                Empty = SqlFunctions.ClickHouse.json_array_length("[]"),
                Object = SqlFunctions.ClickHouse.json_array_length("{\"a\":1}"),
                Malformed = SqlFunctions.ClickHouse.json_array_length("not json"),
                Blank = SqlFunctions.ClickHouse.json_array_length(""),
                ColumnObject = SqlFunctions.ClickHouse.json_array_length(x.Doc),
                NullLiteral = SqlFunctions.ClickHouse.json_array_length(null)
            })
            .First();
        var nullColumn = ctx.From<IT286bJsonEntity>()
            .Where(x => x.Id == 2)
            .Select(x => new { V = SqlFunctions.ClickHouse.json_array_length(x.Doc) })
            .First();
        var arrayColumn = ctx.From<IT286bJsonEntity>()
            .Where(x => x.Id == 4)
            .Select(x => new { V = SqlFunctions.ClickHouse.json_array_length(x.Doc) })
            .First();

        using (new AssertionScope())
        {
            r.Array.Should().Be(3L);
            r.Empty.Should().Be(0L);
            r.Object.Should().BeNull();
            r.Malformed.Should().BeNull();
            r.Blank.Should().BeNull();
            r.ColumnObject.Should().BeNull("the document is a JSON object, not an array");
            r.NullLiteral.Should().BeNull();
            nullColumn.V.Should().BeNull("SQL NULL flows through Nullable(UInt64)");
            arrayColumn.V.Should().Be(3L);
        }
    }

    [Fact]
    public void NativeJsonPaths_ShouldReportEveryPathKind()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var first = ctx.From<IT286bJsonEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                D = SqlFunctions.ClickHouse.json_dynamic_paths(x.NativeDoc),
                DT = SqlFunctions.ClickHouse.json_dynamic_paths_with_types(x.NativeDoc),
                S = SqlFunctions.ClickHouse.json_shared_data_paths(x.NativeDoc),
                ST = SqlFunctions.ClickHouse.json_shared_data_paths_with_types(x.NativeDoc)
            })
            .First();
        var second = ctx.From<IT286bJsonEntity>()
            .Where(x => x.Id == 2)
            .Select(x => new
            {
                D = SqlFunctions.ClickHouse.json_dynamic_paths(x.NativeDoc),
                DT = SqlFunctions.ClickHouse.json_dynamic_paths_with_types(x.NativeDoc),
                S = SqlFunctions.ClickHouse.json_shared_data_paths(x.NativeDoc),
                ST = SqlFunctions.ClickHouse.json_shared_data_paths_with_types(x.NativeDoc)
            })
            .First();
        var third = ctx.From<IT286bJsonEntity>()
            .Where(x => x.Id == 3)
            .Select(x => new
            {
                D = SqlFunctions.ClickHouse.json_dynamic_paths(x.NativeDoc),
                DT = SqlFunctions.ClickHouse.json_dynamic_paths_with_types(x.NativeDoc),
                S = SqlFunctions.ClickHouse.json_shared_data_paths(x.NativeDoc),
                ST = SqlFunctions.ClickHouse.json_shared_data_paths_with_types(x.NativeDoc)
            })
            .First();

        using (new AssertionScope())
        {
            first.D.Should().Equal("a");
            first.DT.Should().ContainKey("a").WhoseValue.Should().Be("Int64");
            first.S.Should().BeEmpty();
            first.ST.Should().BeEmpty();

            second.D.Should().BeEmpty();
            second.DT.Should().BeEmpty();
            second.S.Should().Equal("b");
            second.ST.Should().ContainKey("b").WhoseValue.Should().Be("String");

            third.D.Should().Equal("a");
            third.DT.Should().ContainKey("a");
            third.DT["a"].Should().StartWith("Array(");
            third.S.Should().Equal("c");
            third.ST.Should().ContainKey("c").WhoseValue.Should().Be("Date");
        }
    }

    [Fact]
    public void NativeJsonPaths_ShouldRejectStringAndNullableJsonArguments()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        FluentActions.Invoking(() => ctx.From<IT286bJsonEntity>()
                .Where(x => x.Id == 1)
                .Select(x => new { V = SqlFunctions.ClickHouse.json_dynamic_paths("{\"a\":1}") })
                .First())
            .Should().Throw<ClickHouseServerException>()
            .WithMessage("*requires argument with type JSON*");

        FluentActions.Invoking(() => ctx.From<IT286bJsonEntity>()
                .Where(x => x.Id == 1)
                .Select(x => new { V = SqlFunctions.ClickHouse.json_dynamic_paths(x.NullableNativeDoc) })
                .First())
            .Should().Throw<ClickHouseServerException>()
            .WithMessage("*Nested type Array(String) cannot be inside Nullable type*");
    }

    [Fact]
    public void JsonKey_ShouldReturnTheKeyAtPathAndIndex()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var r = ctx.From<IT286bJsonEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                First = SqlFunctions.ClickHouse.json_key("{\"a\":\"hello\",\"b\":[1,2]}", 1),
                Second = SqlFunctions.ClickHouse.json_key("{\"a\":\"hello\",\"b\":[1,2]}", 2),
                Beyond = SqlFunctions.ClickHouse.json_key("{\"a\":\"hello\",\"b\":[1,2]}", 3),
                ByKey = SqlFunctions.ClickHouse.json_key(x.Doc, "a"),
                MissingKey = SqlFunctions.ClickHouse.json_key(x.Doc, "zzz"),
                ByColumnIndex = SqlFunctions.ClickHouse.json_key(x.Doc, x.Idx)
            })
            .First();
        var nullColumn = ctx.From<IT286bJsonEntity>()
            .Where(x => x.Id == 2)
            .Select(x => new { V = SqlFunctions.ClickHouse.json_key(x.Doc, 1) })
            .First();

        using (new AssertionScope())
        {
            r.First.Should().Be("a");
            r.Second.Should().Be("b");
            r.Beyond.Should().BeEmpty();
            r.ByKey.Should().Be("a");
            r.MissingKey.Should().BeEmpty();
            r.ByColumnIndex.Should().Be("a");
            nullColumn.V.Should().BeNull();
        }
    }

    [Fact]
    public void JsonExtractKeysAndValuesRaw_ShouldReturnRawPairs()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var r = ctx.From<IT286bJsonEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Whole = SqlFunctions.ClickHouse.json_extract_keys_and_values_raw("{\"a\":1,\"b\":\"x\"}"),
                Nested = SqlFunctions.ClickHouse.json_extract_keys_and_values_raw("{\"o\":{\"a\":1}}", "o"),
                Malformed = SqlFunctions.ClickHouse.json_extract_keys_and_values_raw("not json"),
                NotAnObject = SqlFunctions.ClickHouse.json_extract_keys_and_values_raw("[1,2]"),
                Column = SqlFunctions.ClickHouse.json_extract_keys_and_values_raw(x.DocNonNull),
                NullLiteral = SqlFunctions.ClickHouse.json_extract_keys_and_values_raw(null)
            })
            .First();

        using (new AssertionScope())
        {
            r.Whole.Should().BeEquivalentTo(new[]
            {
                Tuple.Create("a", "1"), Tuple.Create("b", "\"x\"")
            });
            r.Nested.Should().Equal(Tuple.Create("a", "1"));
            r.Malformed.Should().BeEmpty("the vendor returns an empty array for malformed JSON");
            r.NotAnObject.Should().BeEmpty();
            r.Column.Should().Contain(Tuple.Create("a", "1")).And.Contain(Tuple.Create("c", "[1,2]"));
            r.NullLiteral.Should().BeNull("a SQL NULL document yields NULL, not an empty pair array");
        }

        // The vendor result is Array(Tuple(String, String)); ClickHouse cannot nest it in Nullable,
        // so a Nullable(String) argument is rejected instead of silently returning an empty array.
        FluentActions.Invoking(() => ctx.From<IT286bJsonEntity>()
                .Where(x => x.Id == 1)
                .Select(x => new { V = SqlFunctions.ClickHouse.json_extract_keys_and_values_raw(x.Doc) })
                .First())
            .Should().Throw<ClickHouseServerException>()
            .WithMessage("*cannot be inside Nullable type*");
    }

    [Fact]
    public void JsonExtractUInt_ShouldKeepTheVendorZeroAndNullForNull()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var r = ctx.From<IT286bJsonEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Hit = SqlFunctions.ClickHouse.json_extract_uint("{\"a\":42}", "a"),
                Negative = SqlFunctions.ClickHouse.json_extract_uint("{\"a\":-1}", "a"),
                WrongType = SqlFunctions.ClickHouse.json_extract_uint("{\"a\":\"x\"}", "a"),
                Malformed = SqlFunctions.ClickHouse.json_extract_uint("not json", "a"),
                Missing = SqlFunctions.ClickHouse.json_extract_uint("{\"b\":1}", "a"),
                Column = SqlFunctions.ClickHouse.json_extract_uint(x.Doc, "a"),
                NullLiteral = SqlFunctions.ClickHouse.json_extract_uint(null, "a")
            })
            .First();
        var nullColumn = ctx.From<IT286bJsonEntity>()
            .Where(x => x.Id == 2)
            .Select(x => new { V = SqlFunctions.ClickHouse.json_extract_uint(x.Doc, "a") })
            .First();
        var malformedColumn = ctx.From<IT286bJsonEntity>()
            .Where(x => x.Id == 3)
            .Select(x => new { V = SqlFunctions.ClickHouse.json_extract_uint(x.Doc, "a") })
            .First();

        using (new AssertionScope())
        {
            r.Hit.Should().Be(42UL);
            r.Negative.Should().Be(0UL, "a negative value is the vendor default, not a wrapped unsigned value");
            r.WrongType.Should().Be(0UL);
            r.Malformed.Should().Be(0UL);
            r.Missing.Should().Be(0UL);
            r.Column.Should().Be(1UL);
            r.NullLiteral.Should().BeNull("SQL NULL is never replaced by the zero default");
            nullColumn.V.Should().BeNull();
            malformedColumn.V.Should().Be(0UL);
        }
    }

    [Fact]
    public void SimpleJsonExtract_ShouldMatchTheFastPathVendorSemantics()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        const string doc = "{\"s\":\"x\",\"n\":42,\"u\":42,\"f\":1.5,\"b\":true,\"o\":{\"x\":1}}";

        var r = ctx.From<IT286bJsonEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                S = SqlFunctions.ClickHouse.simple_json_extract_string(doc, "s"),
                I = SqlFunctions.ClickHouse.simple_json_extract_int(doc, "n"),
                U = SqlFunctions.ClickHouse.simple_json_extract_uint(doc, "u"),
                F = SqlFunctions.ClickHouse.simple_json_extract_float(doc, "f"),
                B = SqlFunctions.ClickHouse.simple_json_extract_bool(doc, "b"),
                R = SqlFunctions.ClickHouse.simple_json_extract_raw(doc, "o"),
                MissingS = SqlFunctions.ClickHouse.simple_json_extract_string(doc, "nope"),
                WrongS = SqlFunctions.ClickHouse.simple_json_extract_string(doc, "n"),
                WrongI = SqlFunctions.ClickHouse.simple_json_extract_int(doc, "s"),
                NegativeU = SqlFunctions.ClickHouse.simple_json_extract_uint("{\"a\":-5}", "a"),
                WrongF = SqlFunctions.ClickHouse.simple_json_extract_float(doc, "s"),
                WrongB = SqlFunctions.ClickHouse.simple_json_extract_bool(doc, "s"),
                MalformedR = SqlFunctions.ClickHouse.simple_json_extract_raw("not json", "a"),
                NullS = SqlFunctions.ClickHouse.simple_json_extract_string(null, "a"),
                NullI = SqlFunctions.ClickHouse.simple_json_extract_int(null, "a"),
                NullU = SqlFunctions.ClickHouse.simple_json_extract_uint(null, "a"),
                NullF = SqlFunctions.ClickHouse.simple_json_extract_float(null, "a"),
                NullB = SqlFunctions.ClickHouse.simple_json_extract_bool(null, "a")
            })
            .First();
        var nullColumn = ctx.From<IT286bJsonEntity>()
            .Where(x => x.Id == 2)
            .Select(x => new
            {
                S = SqlFunctions.ClickHouse.simple_json_extract_string(x.Doc, "a"),
                R = SqlFunctions.ClickHouse.simple_json_extract_raw(x.Doc, "a")
            })
            .First();

        using (new AssertionScope())
        {
            r.S.Should().Be("x");
            r.I.Should().Be(42L);
            r.U.Should().Be(42UL);
            r.F.Should().Be(1.5);
            r.B.Should().BeTrue();
            r.R.Should().Be("{\"x\":1}");
            r.MissingS.Should().BeEmpty();
            r.WrongS.Should().BeEmpty();
            r.WrongI.Should().Be(0L);
            r.NegativeU.Should().Be(0UL);
            r.WrongF.Should().Be(0.0);
            r.WrongB.Should().BeFalse();
            r.MalformedR.Should().BeEmpty();
            r.NullS.Should().BeNull();
            r.NullI.Should().BeNull();
            r.NullU.Should().BeNull();
            r.NullF.Should().BeNull();
            r.NullB.Should().BeNull();
            nullColumn.S.Should().BeNull();
            nullColumn.R.Should().BeNull();
        }
    }

    [Fact]
    public void IsValidJson_ShouldReturnFalseForMalformedAndNullForNull()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var r = ctx.From<IT286bJsonEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Object = SqlFunctions.ClickHouse.is_valid_json("{\"a\":1}"),
                Array = SqlFunctions.ClickHouse.is_valid_json("[1,2]"),
                Malformed = SqlFunctions.ClickHouse.is_valid_json("not json"),
                Blank = SqlFunctions.ClickHouse.is_valid_json(""),
                Column = SqlFunctions.ClickHouse.is_valid_json(x.Doc),
                NullLiteral = SqlFunctions.ClickHouse.is_valid_json(null)
            })
            .First();
        var nullColumn = ctx.From<IT286bJsonEntity>()
            .Where(x => x.Id == 2)
            .Select(x => new { V = SqlFunctions.ClickHouse.is_valid_json(x.Doc) })
            .First();
        var malformedColumn = ctx.From<IT286bJsonEntity>()
            .Where(x => x.Id == 3)
            .Select(x => new { V = SqlFunctions.ClickHouse.is_valid_json(x.Doc) })
            .First();

        using (new AssertionScope())
        {
            r.Object.Should().BeTrue();
            r.Array.Should().BeTrue();
            r.Malformed.Should().BeFalse();
            r.Blank.Should().BeFalse();
            r.Column.Should().BeTrue();
            r.NullLiteral.Should().BeNull("NULL is not false");
            nullColumn.V.Should().BeNull();
            malformedColumn.V.Should().BeFalse();
        }
    }

    [Fact]
    public void Nulls_ShouldFlowThroughEveryNullableResult()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var nullRow = ctx.From<IT286bJsonEntity>()
            .Where(x => x.Id == 2)
            .Select(x => new
            {
                ArrayLength = SqlFunctions.ClickHouse.json_array_length(x.Doc),
                Key = SqlFunctions.ClickHouse.json_key(x.Doc, 1),
                UInt = SqlFunctions.ClickHouse.json_extract_uint(x.Doc, "a"),
                Valid = SqlFunctions.ClickHouse.is_valid_json(x.Doc),
                SimpleRaw = SqlFunctions.ClickHouse.simple_json_extract_raw(x.Doc, "a"),
                SimpleString = SqlFunctions.ClickHouse.simple_json_extract_string(x.Doc, "a"),
                SimpleInt = SqlFunctions.ClickHouse.simple_json_extract_int(x.Doc, "a"),
                SimpleUInt = SqlFunctions.ClickHouse.simple_json_extract_uint(x.Doc, "a"),
                SimpleFloat = SqlFunctions.ClickHouse.simple_json_extract_float(x.Doc, "a"),
                SimpleBool = SqlFunctions.ClickHouse.simple_json_extract_bool(x.Doc, "a")
            })
            .First();

        using (new AssertionScope())
        {
            nullRow.ArrayLength.Should().BeNull();
            nullRow.Key.Should().BeNull();
            nullRow.UInt.Should().BeNull();
            nullRow.Valid.Should().BeNull();
            nullRow.SimpleRaw.Should().BeNull();
            nullRow.SimpleString.Should().BeNull();
            nullRow.SimpleInt.Should().BeNull();
            nullRow.SimpleUInt.Should().BeNull();
            nullRow.SimpleFloat.Should().BeNull();
            nullRow.SimpleBool.Should().BeNull();
        }

        // The native-JSON path functions also keep a SQL NULL JSON value as NULL.
        Scalar<bool>("isNull(JSONDynamicPaths(NULL))").Should().BeTrue();
        Scalar<bool>("isNull(JSONSharedDataPaths(NULL))").Should().BeTrue();
    }

    [Fact]
    public void MalformedInput_ShouldNeverBecomeASilentDefault()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var r = ctx.From<IT286bJsonEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                ArrayLength = SqlFunctions.ClickHouse.json_array_length("not json"),
                Pairs = SqlFunctions.ClickHouse.json_extract_keys_and_values_raw("not json"),
                UInt = SqlFunctions.ClickHouse.json_extract_uint("not json", "a"),
                Valid = SqlFunctions.ClickHouse.is_valid_json("not json"),
                SimpleRaw = SqlFunctions.ClickHouse.simple_json_extract_raw("not json", "a"),
                SimpleInt = SqlFunctions.ClickHouse.simple_json_extract_int("not json", "a"),
                SimpleBool = SqlFunctions.ClickHouse.simple_json_extract_bool("not json", "a")
            })
            .First();

        using (new AssertionScope())
        {
            r.ArrayLength.Should().BeNull("the vendor returns NULL for malformed JSON, never 0");
            r.Pairs.Should().BeEmpty("the vendor returns an empty array, never a fabricated pair");
            r.UInt.Should().Be(0UL, "the vendor return value is asserted, not a library substitution");
            r.Valid.Should().BeFalse();
            r.SimpleRaw.Should().BeEmpty();
            r.SimpleInt.Should().Be(0L);
            r.SimpleBool.Should().BeFalse();
        }

        FluentActions.Invoking(() => ctx.From<IT286bJsonEntity>()
                .Where(x => x.Id == 1)
                .Select(x => new { V = SqlFunctions.ClickHouse.json_merge_patch("not json", "{}") })
                .First())
            .Should().Throw<ClickHouseServerException>()
            .WithMessage("*Wrong JSON string to merge*");
    }

    [Fact]
    public void AlreadyCoveredJsonSurface_ShouldReExecute()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        const string flat = "{\"p\":\"v\",\"n\":7,\"x\":1.5,\"ok\":true,\"o\":{\"k\":1}}";

        var r = ctx.From<IT286bJsonEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                S = SqlFunctions.ClickHouse.json_extract_string(x.Doc, "b"),
                I = SqlFunctions.ClickHouse.json_extract_int(x.Doc, "a"),
                F = SqlFunctions.ClickHouse.json_extract_float(x.Doc, "f"),
                B = SqlFunctions.ClickHouse.json_extract_bool(x.Doc, "t"),
                R = SqlFunctions.ClickHouse.json_extract_raw(x.Doc, "d"),
                A = SqlFunctions.ClickHouse.json_extract_array_raw(x.DocNonNull, "c"),
                K = SqlFunctions.ClickHouse.json_extract_keys(x.DocNonNull),
                KV = SqlFunctions.ClickHouse.json_extract_keys_and_values<int>("{\"a\":1,\"b\":2}"),
                H = SqlFunctions.ClickHouse.json_has(x.Doc, "a"),
                L = SqlFunctions.ClickHouse.json_length(x.Doc, "c"),
                T = SqlFunctions.ClickHouse.json_type(x.Doc, "b"),
                P = SqlFunctions.ClickHouse.json_all_paths(x.NativeDoc),
                PT = SqlFunctions.ClickHouse.json_all_paths_with_types(x.NativeDoc),
                J = SqlFunctions.ClickHouse.to_json_string(x.NativeDoc),
                VS = SqlFunctions.ClickHouse.visit_param_extract_string(flat, "p"),
                VI = SqlFunctions.ClickHouse.visit_param_extract_int(flat, "n"),
                VF = SqlFunctions.ClickHouse.visit_param_extract_float(flat, "x"),
                VB = SqlFunctions.ClickHouse.visit_param_extract_bool(flat, "ok"),
                VR = SqlFunctions.ClickHouse.visit_param_extract_raw(flat, "o")
            })
            .First();

        using (new AssertionScope())
        {
            r.S.Should().Be("x");
            r.I.Should().Be(1L);
            r.F.Should().Be(1.5);
            r.B.Should().BeTrue();
            r.R.Should().Be("{\"e\":true}");
            r.A.Should().Equal("1", "2");
            r.K.Should().Contain(["a", "b", "c", "d", "f", "o", "t"]);
            r.KV.Should().BeEquivalentTo(new[] { Tuple.Create("a", 1), Tuple.Create("b", 2) });
            r.H.Should().BeTrue();
            r.L.Should().Be(2L);
            r.T.Should().Be("String");
            r.P.Should().Contain("a");
            r.PT.Should().ContainKey("a").WhoseValue.Should().Be("Int64");
            r.J.Should().Contain("\"a\"");
            r.VS.Should().Be("v");
            r.VI.Should().Be(7L);
            r.VF.Should().Be(1.5);
            r.VB.Should().BeTrue();
            r.VR.Should().Be("{\"k\":1}");
        }
    }

    [Fact]
    public void Refusals_ShouldNeverReachTheServer()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        using (new AssertionScope())
        {
            FluentActions.Invoking(() => ctx.From<IT286bJsonEntity>()
                    .Where(x => x.Id == 1)
                    .Select(x => new { V = SqlFunctions.ClickHouse.pretty_print_json(x.Doc) })
                    .First())
                .Should().Throw<NotSupportedException>()
                .WithMessage("*prettyPrintJSON*26.4*25.8.33.6*UNKNOWN_FUNCTION*");

            FluentActions.Invoking(() => ctx.From<IT286bJsonEntity>()
                    .Where(x => x.Id == 1)
                    .Select(x => new { V = SqlFunctions.ClickHouse.json_extract_uint_or_default(x.Doc, "a", 7UL) })
                    .First())
                .Should().Throw<NotSupportedException>()
                .WithMessage("*JSONExtractUIntOrDefault*25.8.33.6*UNKNOWN_FUNCTION*invent a semantic*");
        }

        // The pinned server proves both absences: the names are not in system.functions and the
        // engine rejects them with UNKNOWN_FUNCTION.
        Scalar<ulong>("(select count() from system.functions where name in ('prettyPrintJSON', 'JSONExtractUIntOrDefault'))")
            .Should().Be(0UL);
        Scalar<ulong>("(select count() from system.functions where name in "
                      + "('JSONMergePatch', 'JSONArrayLength', 'JSONDynamicPaths', 'JSONSharedDataPaths', "
                      + "'JSONKey', 'JSONExtractKeysAndValuesRaw', 'JSONExtractUInt', 'isValidJSON', "
                      + "'simpleJSONExtractRaw', 'simpleJSONExtractString', 'simpleJSONExtractInt', "
                      + "'simpleJSONExtractUInt', 'simpleJSONExtractFloat', 'simpleJSONExtractBool'))")
            .Should().Be(14UL);

        FluentActions.Invoking(() =>
            {
                using var result = ctx.ExecuteRaw("select prettyPrintJSON('{\"a\":1}')");
                result.Read<string>().Single();
            })
            .Should().Throw<ClickHouseServerException>().WithMessage("*UNKNOWN_FUNCTION*");

        FluentActions.Invoking(() =>
            {
                using var result = ctx.ExecuteRaw("select JSONExtractUIntOrDefault('{\"a\":1}', 'a', 7)");
                result.Read<ulong>().Single();
            })
            .Should().Throw<ClickHouseServerException>().WithMessage("*UNKNOWN_FUNCTION*");
    }

    [Fact]
    public void Projection_ShouldMaterialiseEveryDeliveredMember()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var r = ctx.From<IT286bJsonEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                M = SqlFunctions.ClickHouse.json_merge_patch(x.Doc, "{\"a\":99}"),
                AL = SqlFunctions.ClickHouse.json_array_length(x.Doc),
                DP = SqlFunctions.ClickHouse.json_dynamic_paths(x.NativeDoc),
                DPT = SqlFunctions.ClickHouse.json_dynamic_paths_with_types(x.NativeDoc),
                SP = SqlFunctions.ClickHouse.json_shared_data_paths(x.NativeDoc),
                SPT = SqlFunctions.ClickHouse.json_shared_data_paths_with_types(x.NativeDoc),
                K = SqlFunctions.ClickHouse.json_key(x.Doc, 1),
                KV = SqlFunctions.ClickHouse.json_extract_keys_and_values_raw(x.DocNonNull),
                UI = SqlFunctions.ClickHouse.json_extract_uint(x.Doc, "a"),
                V = SqlFunctions.ClickHouse.is_valid_json(x.Doc),
                SR = SqlFunctions.ClickHouse.simple_json_extract_raw(x.Doc, "a"),
                SS = SqlFunctions.ClickHouse.simple_json_extract_string(x.Doc, "a"),
                SI = SqlFunctions.ClickHouse.simple_json_extract_int(x.Doc, "a"),
                SU = SqlFunctions.ClickHouse.simple_json_extract_uint(x.Doc, "a"),
                SF = SqlFunctions.ClickHouse.simple_json_extract_float(x.Doc, "a"),
                SB = SqlFunctions.ClickHouse.simple_json_extract_bool(x.Doc, "a")
            })
            .First();

        using (new AssertionScope())
        {
            using var patch = JsonDocument.Parse(r.M);
            patch.RootElement.GetProperty("a").GetInt32().Should().Be(99);

            r.AL.Should().BeNull("the document is an object");
            r.DP.Should().Equal("a");
            r.DPT.Should().ContainKey("a").WhoseValue.Should().Be("Int64");
            r.SP.Should().BeEmpty();
            r.SPT.Should().BeEmpty();
            r.K.Should().Be("a");
            r.KV.Should().Contain(Tuple.Create("a", "1"));
            r.UI.Should().Be(1UL);
            r.V.Should().BeTrue();
            r.SR.Should().Be("1");
            r.SS.Should().BeEmpty("the value at 'a' is a number, not a string");
            r.SI.Should().Be(1L);
            r.SU.Should().Be(1UL);
            r.SF.Should().Be(1.0);
            r.SB.Should().BeFalse("the value at 'a' is a number, not a boolean");
        }
    }
}
