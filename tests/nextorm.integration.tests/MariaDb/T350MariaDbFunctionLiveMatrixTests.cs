using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using FluentAssertions;
using MySqlConnector;
using NextORM.Core;
using NextORM.MariaDb;

namespace NextORM.Integration.Tests;

/// <summary>
/// One delivered T350 name as a live case. The oracle-first protocol is structural: <see cref="RawSql"/>
/// (and <see cref="RawNullSql"/>) are executed with the raw path and flushed to the oracle artifact before
/// any translated assertion runs, so the expected value always comes from the server observed in the same
/// session, never from a hand-written constant.
/// </summary>
/// <param name="Id">Stable live-case id (the inventory id for primary rows, suffixed for extras).</param>
/// <param name="Name">Canonical vendor name as spelled in the inventory.</param>
/// <param name="Family">Inventory family.</param>
/// <param name="RawSql">The raw SQL executed in phase 1.</param>
/// <param name="RawNullSql">The raw NULL-argument form, or <c>null</c> when NULL is not applicable.</param>
/// <param name="Translated">The production-path projection of the same name.</param>
/// <param name="TranslatedNull">The production-path projection of the NULL form.</param>
/// <param name="ExpectedClr">The CLR type the delivered signature must materialise.</param>
/// <param name="Eq">Value comparison (type-exact unless the case is deliberately non-deterministic).</param>
/// <param name="NullPolicy"><c>propagates</c>, <c>non-propagating:&lt;expected&gt;</c> or <c>n/a</c>.</param>
/// <param name="SetupSql">Per-case setup replayed before the raw and the translated observation.</param>
/// <param name="TeardownSql">Per-case teardown replayed after the case, in both phases.</param>
internal sealed record T350LiveCase(
    string Id,
    string Name,
    string Family,
    string RawSql,
    string? RawNullSql,
    Func<MariaDbDataContext, Task<object?>> Translated,
    Func<MariaDbDataContext, Task<object?>>? TranslatedNull,
    string ExpectedClr,
    Func<object?, object?, bool> Eq,
    string NullPolicy,
    string? SetupSql = null,
    string? TeardownSql = null);

/// <summary>The one-row probe table the translated projections read from, so no case needs its own source.</summary>
[SqlTable("t350_probe")]
public sealed class T350ProbeRow
{
    /// <summary>The probe row identifier.</summary>
    [Key]
    [Column("id")]
    public int Id { get; set; }
}

/// <summary>
/// T350 (issue #284) S1 matrix: the same frozen universe re-observed as a compact per-candidate matrix
/// (accepted / refused, vendor error number, CLR field type), aggregated per family. It asserts only that
/// the matrix is complete and non-vacuous — every candidate lands in exactly one bucket, the three
/// permanent exclusions are refused, and no family is empty — because the dispositions themselves belong
/// to PLAN, not to this probe. S2 extends the same class with the record-driven, two-phase live matrix
/// (<see cref="T350DeliveredNames_ShouldMatchTheRawOraclePerName"/>): the raw oracle writes its JSONL lines
/// first, and only then is every delivered name compared through the production path against that record.
/// </summary>
[Trait("Category", "Diagnostic")]
public sealed class T350MariaDbFunctionLiveMatrixTests : IDisposable
{
    private const string DefaultMatrixPath = "/tmp/opencode/t350-matrix-s1.jsonl";

    private const string DefaultLiveOraclePath = "/tmp/opencode/t350-live-oracle-s2.jsonl";

    private const string DefaultLiveResultPath = "/tmp/opencode/t350-live-result-s2.jsonl";

    private readonly MySqlConnection _connection;

    public T350MariaDbFunctionLiveMatrixTests()
    {
        Assert.SkipUnless(MariaDbContainer.IsAvailable, MariaDbContainer.Failure ?? "MariaDB is not available.");

        _connection = new MySqlConnection(MariaDbContainer.ConnectionString);
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    /// <summary>
    /// Re-observes every candidate and records one matrix line per case, then asserts the matrix closes:
    /// no candidate is unclassified, the permanent exclusions are refused, and at least one name is
    /// accepted so an all-refused server misconfiguration cannot pass as a matrix.
    /// </summary>
    [Fact]
    public void T350Matrix_ShouldClassifyEveryCandidateFromTheRawServer()
    {
        var matrixPath = T350RawOracle.PathOf("T350_MATRIX_PATH", DefaultMatrixPath);
        var server = T350RawOracle.Scalar(_connection, "SELECT VERSION()");

        using var writer = T350RawOracle.Open(matrixPath, FileMode.Create);

        var accepted = 0;
        var refused = 0;
        var families = new SortedDictionary<string, int>(StringComparer.Ordinal);

        foreach (var @case in T350Cases.All)
        {
            if (@case.SetupSql is { Length: > 0 } setup)
                T350RawOracle.Execute(_connection, setup);

            T350Observation observation;
            try
            {
                observation = T350RawOracle.Observe(_connection, @case.Sql);
            }
            finally
            {
                if (@case.TeardownSql is { Length: > 0 } teardown)
                    T350RawOracle.Execute(_connection, teardown);
            }

            if (observation.Accepted)
                accepted++;
            else
                refused++;

            families[@case.Family] = families.TryGetValue(@case.Family, out var count) ? count + 1 : 1;

            writer.WriteLine(JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["kind"] = "matrix",
                ["serverVersion"] = server,
                ["id"] = @case.Id,
                ["name"] = @case.Name,
                ["family"] = @case.Family,
                ["provenance"] = @case.Provenance,
                ["operationKind"] = @case.Kind,
                ["sql"] = observation.Sql,
                ["verdict"] = observation.Accepted ? "accepted" : "refused",
                ["errorNumber"] = observation.ErrorNumber,
                ["error"] = observation.Error,
                ["fieldType"] = observation.FieldType,
                ["valueType"] = observation.ValueType,
                ["isDbNull"] = observation.IsNull,
                ["textValue"] = observation.Text
            }));
        }

        writer.WriteLine(JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["kind"] = "matrix-summary",
            ["serverVersion"] = server,
            ["candidates"] = T350Cases.All.Length,
            ["accepted"] = accepted,
            ["refused"] = refused,
            ["families"] = families
        }));

        (accepted + refused).Should().Be(
            T350Cases.All.Length, "every candidate must land in exactly one matrix bucket");
        accepted.Should().BeGreaterThan(0, "an all-refused matrix means the probe server is wrong");
        refused.Should().BeGreaterThanOrEqualTo(
            3, "the three permanent exclusions (UUID_TO_BIN, BIN_TO_UUID, JSON_STORAGE_FREE) must be refused");
        families.Should().HaveCountGreaterThan(
            5, "the universe spans more than five families; a collapsed grouping is a broken case table");
    }

    /// <summary>
    /// The record-driven live matrix: phase 1 executes every <see cref="T350LiveCases.All"/> row with the raw
    /// path and flushes one JSONL line per statement to <c>T350_LIVE_ORACLE_PATH</c>; the structural
    /// line-count assertion runs before phase 2 starts, so the oracle is complete before any expectation is
    /// read. Phase 2 then executes each row through the production path on a <see cref="MariaDbDataContext"/>
    /// and compares value, NULL result and CLR type against the raw record, closing on
    /// <c>compared == cases</c> with an empty failure list.
    /// </summary>
    [Fact]
    public async Task T350DeliveredNames_ShouldMatchTheRawOraclePerName()
    {
        var oraclePath = T350RawOracle.PathOf("T350_LIVE_ORACLE_PATH", DefaultLiveOraclePath);
        var resultPath = T350RawOracle.PathOf("T350_LIVE_RESULT_PATH", DefaultLiveResultPath);

        using var ctx = new MariaDbDataContext(MariaDbContainer.ConnectionString, new DataContextBuilder());
        ctx.EnsureConnectionOpen();
        CreateProbeTable(ctx);

        try
        {
            var cases = T350LiveCases.All;
            var server = ServerFacts(ctx);

            // ---------------------------------------------------------------- phase 1: raw oracle
            using var oracle = T350RawOracle.Open(oraclePath, FileMode.Create);
            var rawById = new Dictionary<string, T350LiveObservation>(StringComparer.Ordinal);
            var rawNullById = new Dictionary<string, T350LiveObservation>(StringComparer.Ordinal);
            var lines = 0;

            foreach (var @case in cases)
            {
                var primary = Observe(ctx, @case.RawSql, @case.SetupSql);
                rawById[@case.Id] = primary;
                WriteLive(oracle, server, @case, "primary", primary);
                lines++;

                if (@case.RawNullSql is { Length: > 0 } nullSql)
                {
                    var nullObservation = Observe(ctx, nullSql, @case.SetupSql);
                    rawNullById[@case.Id] = nullObservation;
                    WriteLive(oracle, server, @case, "null", nullObservation);
                    lines++;
                }

                if (@case.TeardownSql is { Length: > 0 } teardown)
                    ExecuteRaw(ctx, teardown);
            }

            oracle.Flush();
            lines.Should().BeGreaterThanOrEqualTo(
                cases.Length,
                "the raw oracle must be complete before any translated assertion runs");

            // ---------------------------------------------------------------- phase 2: translated
            using var results = T350RawOracle.Open(resultPath, FileMode.Create);
            var failures = new List<string>();
            var compared = 0;
            var nullCompared = 0;

            foreach (var @case in cases)
            {
                var raw = rawById[@case.Id];
                var translated = await TryTranslated(ctx, @case, @case.Translated);
                var verdict = CompareCase(@case, "primary", raw, translated, expectedNull: false);
                if (verdict is not null)
                    failures.Add(verdict);
                compared++;

                object? translatedNull = null;
                string? nullError = null;
                if (@case.TranslatedNull is { } nullProjection)
                {
                    var attempt = await TryTranslated(ctx, @case, nullProjection);
                    translatedNull = attempt.Value;
                    nullError = attempt.Error;
                    if (@case.RawNullSql is { Length: > 0 })
                    {
                        var nullVerdict = CompareCase(
                            @case, "NULL", rawNullById[@case.Id], attempt, expectedNull: true);
                        if (nullVerdict is not null)
                            failures.Add(nullVerdict);
                        nullCompared++;
                    }
                }

                WriteLive(
                    results, server, @case, "translated",
                    new T350LiveObservation(
                        @case.RawSql, raw.FieldType, translated.Value?.GetType().FullName ?? "NULL",
                        translated.Value is null, translated.Value, Format(translated.Value), translated.Error),
                    raw, translated.Value, translated.Error, translatedNull, nullError, verdict);

                if (@case.TeardownSql is { Length: > 0 } teardown)
                    ExecuteRaw(ctx, teardown);
            }

            compared.Should().Be(cases.Length, "every live case must be compared");
            failures.Should().BeEmpty(
                "every delivered name must match the raw oracle; failures: " + string.Join(" | ", failures));
            Console.WriteLine(
                $"T350 live matrix: cases={cases.Length} compared={compared} " +
                $"nullCompared={nullCompared} oracleLines={lines} failures=0");
        }
        finally
        {
            ExecuteRaw(ctx, "drop temporary table if exists t350_probe");
        }
    }

    // ---------------------------------------------------------------- comparison

    private static string? CompareCase(
        T350LiveCase @case, string variant, T350LiveObservation raw, Attempt translated, bool expectedNull)
    {
        var label = $"{@case.Id}/{@case.Name}/{variant}";
        if (raw.Error is not null)
            return $"{label}: the raw oracle failed: {raw.Error}";
        if (translated.Error is not null)
            return $"{label}: the production path failed: {translated.Error}";

        var translatedType = translated.Value is null ? "NULL" : ClrName(translated.Value.GetType());
        var rawType = raw.Value is null ? "NULL" : ClrName(raw.Value.GetType());

        // A NULL result on both sides is a complete observation: the CLR type of a NULL carries no
        // information (the delivered signature is still checked on the non-NULL case).
        if (translated.Value is null && raw.Value is null)
            return @case.Eq(null, null) ? null : $"{label}: raw and translated NULL disagree";

        if (expectedNull)
        {
            return @case.Eq(raw.Value, translated.Value)
                ? null
                : $"{label}: translated NULL '{Format(translated.Value)}' [{translatedType}] != " +
                  $"raw NULL '{Format(raw.Value)}' [{rawType}]";
        }

        if (translatedType != @case.ExpectedClr)
            return $"{label}: translated CLR type {translatedType} != delivered signature {@case.ExpectedClr}";
        if (rawType != @case.ExpectedClr)
            return $"{label}: raw oracle CLR type {rawType} != delivered signature {@case.ExpectedClr}";
        if (!@case.Eq(raw.Value, translated.Value))
            return $"{label}: translated '{Format(translated.Value)}' [{translatedType}] != " +
                   $"raw '{Format(raw.Value)}' [{rawType}]";

        return null;
    }

    private static async Task<Attempt> TryTranslated(
        MariaDbDataContext ctx, T350LiveCase @case, Func<MariaDbDataContext, Task<object?>> projection)
    {
        try
        {
            if (@case.SetupSql is { Length: > 0 } prepare)
                ExecuteRaw(ctx, prepare);
            return new Attempt(await projection(ctx), null);
        }
        catch (Exception exception)
        {
            return new Attempt(null, $"{exception.GetType().Name}: {exception.Message}");
        }
    }

    // ---------------------------------------------------------------- raw oracle

    private static T350LiveObservation Observe(MariaDbDataContext ctx, string sql, string? setupSql)
    {
        if (setupSql is { Length: > 0 } prepare)
            ExecuteRaw(ctx, prepare);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var command = ctx.CreateCommand(sql);
            using var reader = command.ExecuteReader();
            if (reader.FieldCount == 0)
                return new T350LiveObservation(sql, null, null, false, null, null, "no result set", stopwatch.ElapsedMilliseconds);
            if (!reader.Read())
                return new T350LiveObservation(sql, null, null, false, null, null, "no rows", stopwatch.ElapsedMilliseconds);

            var fieldType = reader.GetFieldType(0).FullName;
            var isNull = reader.IsDBNull(0);
            var value = isNull ? null : reader.GetValue(0);
            var valueType = isNull ? "NULL" : ClrName(value!.GetType());

            while (reader.Read())
            {
                // Drain the remaining rows so the connection stays usable for the next case.
            }

            return new T350LiveObservation(
                sql, fieldType, valueType, isNull, value, Format(value), null, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception exception)
        {
            return new T350LiveObservation(
                sql, null, null, false, null, null,
                $"{exception.GetType().Name}: {exception.Message}", stopwatch.ElapsedMilliseconds);
        }
    }

    private static Dictionary<string, object?> ServerFacts(MariaDbDataContext ctx) => new()
    {
        ["serverVersion"] = Scalar(ctx, "select version()"),
        ["sqlMode"] = Scalar(ctx, "select @@session.sql_mode"),
        ["charset"] = Scalar(ctx, "select @@character_set_connection"),
        ["collation"] = Scalar(ctx, "select @@collation_connection"),
        ["timeZone"] = Scalar(ctx, "select @@session.time_zone")
    };

    private static string? Scalar(MariaDbDataContext ctx, string sql)
    {
        try
        {
            using var command = ctx.CreateCommand(sql);
            var value = command.ExecuteScalar();
            return value is null or DBNull ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
        }
        catch (Exception exception)
        {
            return $"{exception.GetType().Name}: {exception.Message}";
        }
    }

    private static void WriteLive(
        StreamWriter writer,
        Dictionary<string, object?> server,
        T350LiveCase @case,
        string variant,
        T350LiveObservation observation,
        T350LiveObservation? raw = null,
        object? translatedValue = null,
        string? translatedError = null,
        object? translatedNullValue = null,
        string? translatedNullError = null,
        string? verdict = null)
    {
        var record = new Dictionary<string, object?>(server)
        {
            ["kind"] = variant is "primary" or "null" ? "oracle" : "translated",
            ["id"] = @case.Id,
            ["name"] = @case.Name,
            ["family"] = @case.Family,
            ["variant"] = variant,
            ["sql"] = observation.Sql,
            ["fieldType"] = observation.FieldType,
            ["valueType"] = observation.ValueType,
            ["isDbNull"] = observation.IsNull,
            ["textValue"] = observation.Text,
            ["error"] = observation.Error,
            ["durationMs"] = observation.DurationMs,
            ["expectedClr"] = @case.ExpectedClr,
            ["nullPolicy"] = @case.NullPolicy
        };

        if (variant == "translated")
        {
            record["rawValueType"] = raw?.ValueType;
            record["rawTextValue"] = raw?.Text;
            record["translatedValue"] = Format(translatedValue);
            record["translatedError"] = translatedError;
            record["translatedNullValue"] = Format(translatedNullValue);
            record["translatedNullError"] = translatedNullError;
            record["verdict"] = verdict ?? "match";
        }

        writer.WriteLine(JsonSerializer.Serialize(record));
        writer.Flush();
    }

    // ---------------------------------------------------------------- probe table

    private static void CreateProbeTable(MariaDbDataContext ctx)
    {
        ExecuteRaw(ctx, "drop temporary table if exists t350_probe");
        ExecuteRaw(ctx, "create temporary table t350_probe (id int not null primary key)");
        ExecuteRaw(ctx, "insert into t350_probe (id) values (1)");
    }

    private static void ExecuteRaw(MariaDbDataContext ctx, string sql)
    {
        using var command = ctx.CreateCommand(sql);
        command.ExecuteNonQuery();
    }

    // ---------------------------------------------------------------- formatting and equality

    private static string ClrName(Type type) => type == typeof(byte[]) ? "byte[]" : type.FullName ?? type.Name;

    private static string? Format(object? value)
    {
        var text = value switch
        {
            null => null,
            byte[] bytes => "hex:" + Convert.ToHexString(bytes),
            DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
            TimeSpan timeSpan => timeSpan.ToString("c", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };

        return text is { Length: > 300 } ? text[..300] : text;
    }

    /// <summary>Type-exact equality; NULL equals only NULL.</summary>
    private static bool Exact(object? raw, object? translated)
        => raw is null || translated is null
            ? raw is null && translated is null
            : raw.GetType() == translated.GetType() && raw.Equals(translated);

    /// <summary>Value-exact equality for the binary results, plus <see cref="Exact"/> for NULL.</summary>
    private static bool Bytes(object? raw, object? translated)
        => raw is byte[] left && translated is byte[] right
            ? left.AsSpan().SequenceEqual(right)
            : Exact(raw, translated);

    /// <summary>
    /// Type-exact equality without a value comparison, for the deliberately non-deterministic rows
    /// (<c>SYS_GUID</c>): the observed CLR type is the whole contract there.
    /// </summary>
    private static bool TypeOnly(object? raw, object? translated)
        => raw is null || translated is null
            ? raw is null && translated is null
            : raw.GetType() == translated.GetType();


    private sealed record Attempt(object? Value, string? Error);

    private sealed record T350LiveObservation(
        string Sql,
        string? FieldType,
        string? ValueType,
        bool IsNull,
        object? Value,
        string? Text,
        string? Error,
        long DurationMs = 0);

    /// <summary>
    /// The live cases of the S2 matrix. The two rows below are the already-covered controls of the
    /// inventory (<c>JSON_EQUALS</c> and <c>JSON_NORMALIZE</c>, both delivered by T284): they prove the two
    /// phases, the value comparison, the CLR-type comparison and the NULL policy end to end without
    /// claiming any new surface. Every name the S2/S3 delivery blocks add is appended here as its own row,
    /// so the case count and the delivered count stay the same number.
    /// </summary>
    private static class T350LiveCases
    {
        internal static readonly T350LiveCase[] All =
        [
            new T350LiveCase(
                "S2-C1",
                "JSON_EQUALS",
                "JSON",
                "select json_equals('{\"a\":1}', '{\"a\":1}')",
                "select json_equals(null, '{\"a\":1}')",
                ctx => Task.FromResult<object?>(
                    ctx.From<T350ProbeRow>().Where(x => x.Id == 1)
                        .Select(x => new { V = SqlFunctions.MySql.json_equals("{\"a\":1}", "{\"a\":1}") })
                        .ToList()[0].V),
                ctx => Task.FromResult<object?>(
                    ctx.From<T350ProbeRow>().Where(x => x.Id == 1)
                        .Select(x => new { V = SqlFunctions.MySql.json_equals(null, "{\"a\":1}") })
                        .ToList()[0].V),
                "System.Int32",
                Exact,
                "propagates"),
            new T350LiveCase(
                "S2-C2",
                "JSON_NORMALIZE",
                "JSON",
                "select json_normalize('true')",
                "select json_normalize(null)",
                ctx => Task.FromResult<object?>(
                    ctx.From<T350ProbeRow>().Where(x => x.Id == 1)
                        .Select(x => new { V = SqlFunctions.MySql.json_normalize("true") })
                        .ToList()[0].V),
                ctx => Task.FromResult<object?>(
                    ctx.From<T350ProbeRow>().Where(x => x.Id == 1)
                        .Select(x => new { V = SqlFunctions.MySql.json_normalize(null) })
                        .ToList()[0].V),
                "System.String",
                Exact,
                "propagates"),
            // ---------------------------------------------------------------- T350 block 2: string
            new T350LiveCase(
                "90",
                "SFORMAT",
                "string",
                "select sformat('{}', 42)",
                "select sformat('{}', null)",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.sformat("{}", 42))),
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.sformat("{}", (object?)null))),
                "System.String",
                Exact,
                "propagates"),
            new T350LiveCase(
                "91",
                "TRIM_ORACLE",
                "string",
                "select trim_oracle('  a  ')",
                "select trim_oracle(null)",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.trim_oracle("  a  "))),
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.trim_oracle(null))),
                "System.String",
                Exact,
                "propagates"),
            new T350LiveCase(
                "92",
                "NATURAL_SORT_KEY",
                "string",
                "select natural_sort_key('a10')",
                "select natural_sort_key(null)",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.natural_sort_key("a10"))),
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.natural_sort_key(null))),
                "System.String",
                Exact,
                "propagates"),
            new T350LiveCase(
                "93",
                "LENGTHB",
                "string",
                "select lengthb('abc')",
                "select lengthb(null)",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.lengthb("abc"))),
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.lengthb(null))),
                "System.Int32",
                Exact,
                "propagates"),
            new T350LiveCase(
                "94",
                "CHR",
                "string",
                "select chr(65)",
                "select chr(null)",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.chr(65))),
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.chr(null))),
                "System.String",
                Exact,
                "propagates"),
            // ---------------------------------------------------------------- T350 block 2: JSON
            new T350LiveCase(
                "110",
                "JSON_LOOSE",
                "JSON",
                "select json_loose('{\"a\":1,\"b\":[2,3]}')",
                "select json_loose(null)",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.json_loose("{\"a\":1,\"b\":[2,3]}"))),
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.json_loose(null))),
                "System.String",
                Exact,
                "propagates"),
            new T350LiveCase(
                "113",
                "JSON_ARRAY_INTERSECT",
                "JSON",
                "select json_array_intersect('[1,2,3]','[1,2,4]')",
                "select json_array_intersect(null,'[1,2,4]')",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.json_array_intersect("[1,2,3]", "[1,2,4]"))),
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.json_array_intersect(null, "[1,2,4]"))),
                "System.String",
                Exact,
                "propagates"),
            new T350LiveCase(
                "114",
                "JSON_KEY_VALUE",
                "JSON",
                "select json_key_value('{\"a\":1}','$')",
                "select json_key_value(null,'$')",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.json_key_value("{\"a\":1}", "$"))),
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.json_key_value(null, "$"))),
                "System.String",
                Exact,
                "propagates"),
            new T350LiveCase(
                "115",
                "JSON_OBJECT_FILTER_KEYS",
                "JSON",
                "select json_object_filter_keys('{\"a\":1,\"b\":2}','[\"a\"]')",
                "select json_object_filter_keys(null,'[\"a\"]')",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.json_object_filter_keys("{\"a\":1,\"b\":2}", "[\"a\"]"))),
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.json_object_filter_keys(null, "[\"a\"]"))),
                "byte[]",
                Bytes,
                "propagates"),
            new T350LiveCase(
                "116",
                "JSON_OBJECT_TO_ARRAY",
                "JSON",
                "select json_object_to_array('{\"a\":1}')",
                "select json_object_to_array(null)",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.json_object_to_array("{\"a\":1}"))),
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.json_object_to_array(null))),
                "byte[]",
                Bytes,
                "propagates"),
            // ---------------------------------------------------------------- T350 block 2: crypto
            new T350LiveCase(
                "101",
                "ENCODE",
                "crypto",
                "select encode('abc','key')",
                "select encode(null,'key')",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.encode("abc", "key"))),
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.encode(null, "key"))),
                "byte[]",
                Bytes,
                "propagates"),
            new T350LiveCase(
                "102",
                "DECODE",
                "crypto",
                "select decode(encode('abc','key'),'key')",
                "select decode(null,'key')",
                ctx => Task.FromResult<object?>(
                    Project(ctx, x => SqlFunctions.MySql.decode(SqlFunctions.MySql.encode("abc", "key"), "key"))),
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.decode(null, "key"))),
                "byte[]",
                Bytes,
                "propagates"),
            new T350LiveCase(
                "103",
                "ENCRYPT",
                "crypto",
                "select encrypt('abc','ab')",
                "select encrypt(null,'ab')",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.encrypt("abc", "ab"))),
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.encrypt(null, "ab"))),
                "byte[]",
                Bytes,
                "propagates"),
            new T350LiveCase(
                "104",
                "DES_ENCRYPT",
                "crypto",
                "select des_encrypt('abc','key')",
                "select des_encrypt(null,'key')",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.des_encrypt("abc", "key"))),
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.des_encrypt(null, "key"))),
                "byte[]",
                Bytes,
                "propagates"),
            new T350LiveCase(
                "105",
                "DES_DECRYPT",
                "crypto",
                "select des_decrypt(des_encrypt('abc','key'),'key')",
                "select des_decrypt(null,'key')",
                ctx => Task.FromResult<object?>(
                    Project(ctx, x => SqlFunctions.MySql.des_decrypt(SqlFunctions.MySql.des_encrypt("abc", "key"), "key"))),
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.des_decrypt(null, "key"))),
                "byte[]",
                Bytes,
                "propagates"),
            new T350LiveCase(
                "106",
                "OLD_PASSWORD",
                "crypto",
                "select old_password('abc')",
                "select old_password(null)",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.old_password("abc"))),
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.old_password(null))),
                "System.String",
                Exact,
                "propagates"),
            // ---------------------------------------------------------------- T350 block 2: dynamic columns
            // The calls are pure over their blobs, so no table state is needed: the "A/B cycle" is the
            // nested COLUMN_CREATE -> COLUMN_ADD/DELETE expression of each row, observed on both phases.
            new T350LiveCase(
                "123",
                "COLUMN_CREATE",
                "dynamic columns",
                "select column_create('a',1)",
                "select column_create('a',null)",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.column_create("a", 1))),
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.column_create("a", (object?)null))),
                "byte[]",
                Bytes,
                "non-propagating:blob"),
            new T350LiveCase(
                "124",
                "COLUMN_ADD",
                "dynamic columns",
                "select column_add(column_create('a',1),'b',2)",
                "select column_add(column_create('a',1),'b',null)",
                ctx => Task.FromResult<object?>(Project(ctx, x =>
                    SqlFunctions.MySql.column_add(SqlFunctions.MySql.column_create("a", 1), "b", 2))),
                ctx => Task.FromResult<object?>(Project(ctx, x =>
                    SqlFunctions.MySql.column_add(SqlFunctions.MySql.column_create("a", 1), "b", (object?)null))),
                "byte[]",
                Bytes,
                "non-propagating:blob"),
            new T350LiveCase(
                "125",
                "COLUMN_DELETE",
                "dynamic columns",
                "select column_delete(column_create('a',1,'b',2),'b')",
                "select column_delete(null,'b')",
                ctx => Task.FromResult<object?>(Project(ctx, x =>
                    SqlFunctions.MySql.column_delete(SqlFunctions.MySql.column_create("a", 1, "b", 2), "b"))),
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.column_delete(null, "b"))),
                "byte[]",
                Bytes,
                "propagates"),
            new T350LiveCase(
                "127",
                "COLUMN_JSON",
                "dynamic columns",
                "select column_json(column_create('a',1))",
                "select column_json(null)",
                ctx => Task.FromResult<object?>(Project(ctx, x =>
                    SqlFunctions.MySql.column_json(SqlFunctions.MySql.column_create("a", 1)))),
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.column_json(null))),
                "System.String",
                Exact,
                "propagates"),
            new T350LiveCase(
                "128",
                "COLUMN_LIST",
                "dynamic columns",
                "select column_list(column_create('a',1))",
                "select column_list(null)",
                ctx => Task.FromResult<object?>(Project(ctx, x =>
                    SqlFunctions.MySql.column_list(SqlFunctions.MySql.column_create("a", 1)))),
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.column_list(null))),
                "System.String",
                Exact,
                "propagates"),
            new T350LiveCase(
                "129",
                "COLUMN_EXISTS",
                "dynamic columns",
                "select column_exists(column_create('a',1),'a')",
                "select column_exists(null,'a')",
                ctx => Task.FromResult<object?>(Project(ctx, x =>
                    SqlFunctions.MySql.column_exists(SqlFunctions.MySql.column_create("a", 1), "a"))),
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.column_exists(null, "a"))),
                "System.Int32",
                Exact,
                "propagates"),
            new T350LiveCase(
                "130",
                "COLUMN_CHECK",
                "dynamic columns",
                "select column_check(column_create('a',1))",
                "select column_check(null)",
                ctx => Task.FromResult<object?>(Project(ctx, x =>
                    SqlFunctions.MySql.column_check(SqlFunctions.MySql.column_create("a", 1)))),
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.column_check(null))),
                "System.Int32",
                Exact,
                "propagates"),
            // ---------------------------------------------------------------- T350 block 2: galera
            // The three WSREP_* names stay readable on the default non-Galera server: the probe recorded
            // "0-0-0" for both GTID readers and 1 for the sync wait, so the row needs no cluster. The two
            // readers take no argument at all, so they have no NULL form.
            new T350LiveCase(
                "131",
                "WSREP_LAST_SEEN_GTID",
                "galera",
                "select wsrep_last_seen_gtid()",
                null,
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.wsrep_last_seen_gtid())),
                null,
                "byte[]",
                Bytes,
                "n/a"),
            new T350LiveCase(
                "132",
                "WSREP_LAST_WRITTEN_GTID",
                "galera",
                "select wsrep_last_written_gtid()",
                null,
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.wsrep_last_written_gtid())),
                null,
                "byte[]",
                Bytes,
                "n/a"),
            new T350LiveCase(
                "133",
                "WSREP_SYNC_WAIT_UPTO_GTID",
                "galera",
                "select wsrep_sync_wait_upto_gtid('0-0-0')",
                null,
                ctx => Task.FromResult<object?>(
                    Project(ctx, x => SqlFunctions.MySql.wsrep_sync_wait_upto_gtid("0-0-0"))),
                null,
                "System.String",
                Exact,
                "n/a"),
            // ---------------------------------------------------------------- T350 block 2: singles
            new T350LiveCase(
                "96",
                "FORMAT_PICO_TIME",
                "date/time",
                "select format_pico_time(1)",
                "select format_pico_time(null)",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.format_pico_time(1L))),
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.format_pico_time(null))),
                "System.String",
                Exact,
                "propagates"),
            // Two search branches plus the trailing default; the NULL expression matches neither branch and
            // therefore falls through to the default, exactly like the four-argument form the probe recorded.
            new T350LiveCase(
                "97",
                "DECODE_ORACLE",
                "control flow",
                "select decode_oracle(1, 1, 'a', 2, 'b', 'z')",
                "select decode_oracle(null, 1, 'a', 2, 'b', 'z')",
                ctx => Task.FromResult<object?>(
                    Project(ctx, x => SqlFunctions.MySql.decode_oracle(1, 1, "a", 2, "b", "z"))),
                ctx => Task.FromResult<object?>(
                    Project(ctx, x => SqlFunctions.MySql.decode_oracle(null, 1, "a", 2, "b", "z"))),
                "System.String",
                Exact,
                "non-propagating:z"),
            new T350LiveCase(
                "99",
                "DECODE_HISTOGRAM",
                "information",
                "select decode_histogram('SINGLE_PREC_HB', 'abc')",
                null,
                ctx => Task.FromResult<object?>(
                    Project(ctx, x => SqlFunctions.MySql.decode_histogram("SINGLE_PREC_HB", "abc"))),
                null,
                "System.String",
                Exact,
                "n/a"),
            new T350LiveCase(
                "107",
                "CRC32C",
                "numeric",
                "select crc32c('abc')",
                "select crc32c(null)",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.crc32c("abc"))),
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.crc32c(null))),
                "System.UInt32",
                Exact,
                "propagates"),
            // Random by definition: the row pins the materialised CLR type, not the value.
            new T350LiveCase(
                "108",
                "SYS_GUID",
                "miscellaneous",
                "select sys_guid()",
                null,
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.sys_guid())),
                null,
                "System.String",
                TypeOnly,
                "n/a")
        ];

        /// <summary>Evaluates one production projection against the single probe row.</summary>
        private static object? Project<T>(
            MariaDbDataContext ctx, System.Linq.Expressions.Expression<Func<T350ProbeRow, T>> projection)
            => ctx.From<T350ProbeRow>()
                .Where(x => x.Id == 1)
                .Select(projection)
                .ToList()[0];
    }
}
