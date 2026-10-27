using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using FluentAssertions;
using NextORM.Core;
using NextORM.MySql;

namespace NextORM.Integration.Tests;

/// <summary>
/// The one-row probe table of the live matrix: one column per CLR shape the T349 surface can bind
/// (<c>string</c>, signed and unsigned integers, decimal, binary, datetime, time and JSON).
/// </summary>
[SqlTable("t349_probe")]
public interface IT349Probe
{
    /// <summary>The probe row identifier.</summary>
    [Key]
    [Column("id")]
    int Id { get; set; }

    /// <summary>A utf8mb4 string column.</summary>
    [Column("s")]
    string? S { get; set; }

    /// <summary>A signed BIGINT column.</summary>
    [Column("n")]
    long? N { get; set; }

    /// <summary>A DECIMAL(20,4) column.</summary>
    [Column("d")]
    decimal? D { get; set; }

    /// <summary>A VARBINARY column.</summary>
    [Column("b")]
    byte[]? B { get; set; }

    /// <summary>A DATETIME(6) column.</summary>
    [Column("dt")]
    DateTime? Dt { get; set; }

    /// <summary>A TIME(6) column.</summary>
    [Column("tm")]
    TimeSpan? Tm { get; set; }

    /// <summary>A native JSON column read as text.</summary>
    [Column("j")]
    string? J { get; set; }

    /// <summary>A BIGINT UNSIGNED column.</summary>
    [Column("u")]
    ulong? U { get; set; }
}

/// <summary>
/// One delivered name of the T349 MySQL 8.4 surface as a live case. The oracle-first protocol is
/// structural: <see cref="RawSql"/> (and <see cref="RawNullSql"/>) are executed with raw ADO.NET and
/// flushed to the oracle artifact before any translated assertion runs, so the expected value always
/// comes from the server observed in the same session, never from a hand-written constant.
/// </summary>
/// <param name="Id">Stable live-case id (the inventory id for primary rows, suffixed for extras).</param>
/// <param name="Name">Delivered surface name as spelled in the T349 capability registry.</param>
/// <param name="Family">Inventory family.</param>
/// <param name="RawSql">The raw SQL executed in phase 1.</param>
/// <param name="RawNullSql">The raw NULL-argument form, or <c>null</c> when NULL is not applicable.</param>
/// <param name="Translated">The production-path projection of the same name.</param>
/// <param name="TranslatedNull">The production-path projection of the NULL form.</param>
/// <param name="ExpectedClr">The CLR type the delivered signature must materialise (<c>byte[]</c> for binary).</param>
/// <param name="Eq">Value comparison (type-exact unless the case is deliberately non-deterministic).</param>
/// <param name="NullPolicy">
/// <c>propagates</c>, <c>non-propagating:&lt;expected&gt;</c> or <c>n/a</c> with a reason — the label is
/// the documented reading of the vendor semantics, the assertion always compares translated to raw.
/// </param>
/// <param name="PrepareSql">Session setup replayed immediately before both the raw and translated observation.</param>
internal sealed record LiveCase(
    string Id,
    string Name,
    string Family,
    string RawSql,
    string? RawNullSql,
    Func<MySqlDataContext, Task<object?>> Translated,
    Func<MySqlDataContext, Task<object?>>? TranslatedNull,
    string ExpectedClr,
    Func<object?, object?, bool> Eq,
    string NullPolicy,
    string? PrepareSql = null);

/// <summary>
/// T349 (issue #283) block b6a: the live MySQL 8.4 matrix. Phase 1 executes every delivered name with
/// raw ADO.NET on the same open connection and flushes one JSON line per statement to
/// <c>T349_ORACLE_PATH</c>; the structural line-count assertion runs before phase 2 starts. Phase 2
/// then executes each name through the production path
/// (<c>ctx.From&lt;IT349Probe&gt;().Where(...).Select(...).ToListAsync()</c>) and compares value, NULL
/// result and CLR type against the raw oracle. Two tests only: the matrix and the bare
/// zero-argument projection record.
/// </summary>
[Collection("MySql")]
public sealed class T349MySqlFunctionLiveMatrixTests : ProviderTestSuite
{
    private const string DefaultOraclePath = "/tmp/opencode/t349-oracle-b6.jsonl";

    private const string DefaultResultPath = "/tmp/opencode/t349-live-result-b6.jsonl";

    private const string DefaultBarePath = "/tmp/opencode/t349-bare-projection-b6.jsonl";

    private const int MinimumOracleLines = 100;

    private static readonly DateTime ConvertFrom = new(2026, 1, 15, 12, 0, 0);

    private static readonly DateTime DayStart = new(2020, 1, 15, 0, 0, 0);

    private static readonly TimeSpan OneHour = TimeSpan.FromHours(1);

    private static readonly byte[] AesText = [116, 101, 120, 116];

    private static readonly byte[] AesKey = [107, 101, 121];

    private static readonly byte[] ZeroIv = new byte[16];

    private static readonly byte[] CompressInput = [97, 98, 99];

    private static readonly byte[] TwoBytes = [1, 2];

    protected override ITestProvider Provider => MySqlTestProvider.Instance;

    [Fact]
    public async Task T349DeliveredNames_ShouldMatchTheRawOraclePerName()
    {
        var oraclePath = PathOf("T349_ORACLE_PATH", DefaultOraclePath);
        var resultPath = PathOf("T349_LIVE_RESULT_PATH", DefaultResultPath);

        using var ctx = new MySqlDataContext(MySqlContainer.ConnectionString, new DataContextBuilder());
        ctx.EnsureConnectionOpen();
        CreateProbe(ctx);

        try
        {
            // ---------------------------------------------------------------- phase 1: raw oracle
            using var oracle = Open(oraclePath, FileMode.Create);
            var rawById = new Dictionary<string, RawObservation>(StringComparer.Ordinal);
            var rawNullById = new Dictionary<string, RawObservation>(StringComparer.Ordinal);
            var server = ServerFacts(ctx);
            var lines = 0;

            foreach (var @case in LiveCases.All)
            {
                var primary = Observe(ctx, @case.RawSql, @case.PrepareSql);
                rawById[@case.Id] = primary;
                WriteLine(oracle, server, @case, "primary", primary);
                lines++;

                if (@case.RawNullSql is { Length: > 0 } nullSql)
                {
                    var nullObservation = Observe(ctx, nullSql, @case.PrepareSql);
                    rawNullById[@case.Id] = nullObservation;
                    WriteLine(oracle, server, @case, "null", nullObservation);
                    lines++;
                }
            }

            oracle.Flush();
            lines.Should().BeGreaterThanOrEqualTo(
                MinimumOracleLines,
                "the raw oracle must be complete before any translated assertion runs");

            // ---------------------------------------------------------------- phase 2: translated
            using var results = Open(resultPath, FileMode.Create);

            // §4 claim evidence: JSON_TABLE is not a scalar member (refusal unit-tested), but the raw
            // table shape must still execute on the same server, otherwise the refusal has no ground.
            var jsonTable = Observe(
                ctx,
                "select * from json_table('[{\"a\":1}]', '$[*]' columns (a int path '$.a')) jt",
                null);
            results.WriteLine(JsonSerializer.Serialize(new Dictionary<string, object?>(server)
            {
                ["kind"] = "json-table-shape",
                ["id"] = "L55",
                ["name"] = "JSON_TABLE",
                ["sql"] = jsonTable.Sql,
                ["fieldType"] = jsonTable.FieldType,
                ["valueType"] = jsonTable.ValueType,
                ["isDbNull"] = jsonTable.IsNull,
                ["textValue"] = jsonTable.Text,
                ["error"] = jsonTable.Error
            }));
            results.Flush();
            jsonTable.Error.Should().BeNull(
                "the raw JSON_TABLE table shape must execute; the refusal applies to the scalar surface only");

            var failures = new List<string>();
            var compared = 0;
            var nullCompared = 0;

            foreach (var @case in LiveCases.All)
            {
                var raw = rawById[@case.Id];
                var translated = await TryTranslated(ctx, @case, @case.Translated);
                var verdict = Compare(@case, raw, translated, translatedValue: true);
                if (verdict is not null)
                    failures.Add(verdict);
                compared++;

                RawObservation? rawNull = rawNullById.TryGetValue(@case.Id, out var observed) ? observed : null;
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
                            @case, "NULL", rawNull!, new Attempt(translatedNull, nullError), expectedNull: true);
                        if (nullVerdict is not null)
                            failures.Add(nullVerdict);
                        nullCompared++;
                    }
                }

                WriteLine(results, server, @case, "translated", new RawObservation(
                    @case.RawSql,
                    raw.FieldType,
                    translated.Value?.GetType().FullName ?? "NULL",
                    translated.Value is null,
                    translated.Value,
                    Format(translated.Value),
                    translated.Error), raw, translated.Value, translated.Error, translatedNull, nullError, verdict);
            }

            compared.Should().Be(LiveCases.All.Length, "every live case must be compared");
            failures.Should().BeEmpty(
                "every delivered name must match the raw oracle; failures: " + string.Join(" | ", failures));
            Console.WriteLine(
                $"T349 live matrix: cases={LiveCases.All.Length} compared={compared} " +
                $"nullCompared={nullCompared} oracleLines={lines} failures=0");
        }
        finally
        {
            ExecuteRaw(ctx, "drop temporary table if exists t349_probe");
        }
    }

    [Fact]
    public async Task T349BareZeroArgumentProjection_ShouldBeRecorded()
    {
        var barePath = PathOf("T349_BARE_PATH", DefaultBarePath);

        using var ctx = new MySqlDataContext(MySqlContainer.ConnectionString, new DataContextBuilder());
        ctx.EnsureConnectionOpen();
        CreateProbe(ctx);

        try
        {
            using var writer = Open(barePath, FileMode.Create);
            var observations = new List<BareObservation>
            {
                await ObserveBare(ctx, writer, "uuid_short", preExisting: false,
                    ctx.From<IT349Probe>().Where(x => x.Id == 1)
                        .Select(x => SqlFunctions.MySql.uuid_short())),
                await ObserveBare(ctx, writer, "icu_version", preExisting: false,
                    ctx.From<IT349Probe>().Where(x => x.Id == 1)
                        .Select(x => SqlFunctions.MySql.icu_version())),
                await ObserveBare(ctx, writer, "current_role", preExisting: false,
                    ctx.From<IT349Probe>().Where(x => x.Id == 1)
                        .Select(x => SqlFunctions.MySql.current_role())),
                await ObserveBare(ctx, writer, "found_rows", preExisting: false,
                    ctx.From<IT349Probe>().Where(x => x.Id == 1)
                        .Select(x => SqlFunctions.MySql.found_rows())),
                await ObserveBare(ctx, writer, "row_count", preExisting: false,
                    ctx.From<IT349Probe>().Where(x => x.Id == 1)
                        .Select(x => SqlFunctions.MySql.row_count())),
                // Pre-existing zero-argument members: the planner behaviour baseline. The dispatch
                // named SqlFunctions.Now, which this tree does not declare; SqlFunctions.Sql.pi() is
                // the pre-existing parameterless scalar that does exist.
                await ObserveBare(ctx, writer, "pi", preExisting: true,
                    ctx.From<IT349Probe>().Where(x => x.Id == 1)
                        .Select(x => SqlFunctions.Sql.pi())),
                await ObserveBare(ctx, writer, "connection_id", preExisting: true,
                    ctx.From<IT349Probe>().Where(x => x.Id == 1)
                        .Select(x => SqlFunctions.Sql.connection_id()))
            };

            var newFolded = observations.Where(o => !o.PreExisting && o.Folded).Select(o => o.Name).ToArray();
            var preExistingFolded = observations.Where(o => o.PreExisting && o.Folded).Select(o => o.Name).ToArray();

            Console.WriteLine(
                $"T349 bare projection: foldedNew=[{string.Join(", ", newFolded)}] " +
                $"foldedPreExisting=[{string.Join(", ", preExistingFolded)}]");

            (newFolded.Length == 0 || preExistingFolded.Length > 0).Should().BeTrue(
                "a new zero-argument member must not fold client-side when the pre-existing members do not: " +
                $"folded new=[{string.Join(", ", newFolded)}], " +
                $"pre-existing=[{string.Join(", ", preExistingFolded)}]");
        }
        finally
        {
            ExecuteRaw(ctx, "drop temporary table if exists t349_probe");
        }
    }

    // ---------------------------------------------------------------- comparison

    private static string? Compare(LiveCase @case, RawObservation raw, Attempt translated, bool translatedValue)
        => CompareCase(@case, "primary", raw, translated, expectedNull: false);

    private static string? CompareCase(LiveCase @case, string variant, RawObservation raw, Attempt translated, bool expectedNull)
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
        MySqlDataContext ctx, LiveCase @case, Func<MySqlDataContext, Task<object?>> projection)
    {
        try
        {
            if (@case.PrepareSql is { Length: > 0 } prepare)
                ExecuteRaw(ctx, prepare);
            return new Attempt(await projection(ctx), null);
        }
        catch (Exception exception)
        {
            return new Attempt(null, $"{exception.GetType().Name}: {exception.Message}");
        }
    }

    // ---------------------------------------------------------------- raw oracle

    private static RawObservation Observe(MySqlDataContext ctx, string sql, string? prepareSql)
    {
        if (prepareSql is { Length: > 0 } prepare)
            ExecuteRaw(ctx, prepare);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var command = ctx.CreateCommand(sql);
            using var reader = command.ExecuteReader();
            if (reader.FieldCount == 0)
                return new RawObservation(sql, null, null, false, null, null, "no result set", stopwatch.ElapsedMilliseconds);
            if (!reader.Read())
                return new RawObservation(sql, null, null, false, null, null, "no rows", stopwatch.ElapsedMilliseconds);

            var fieldType = reader.GetFieldType(0).FullName;
            var isNull = reader.IsDBNull(0);
            var value = isNull ? null : reader.GetValue(0);
            var valueType = isNull ? "NULL" : ClrName(value!.GetType());

            while (reader.Read())
            {
                // Drain remaining rows so the connection stays usable for the next case.
            }

            return new RawObservation(
                sql, fieldType, valueType, isNull, value, Format(value), null, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception exception)
        {
            return new RawObservation(
                sql, null, null, false, null, null,
                $"{exception.GetType().Name}: {exception.Message}", stopwatch.ElapsedMilliseconds);
        }
    }

    private static Dictionary<string, object?> ServerFacts(MySqlDataContext ctx) => new()
    {
        ["serverVersion"] = Scalar(ctx, "select version()"),
        ["sqlMode"] = Scalar(ctx, "select @@session.sql_mode"),
        ["charset"] = Scalar(ctx, "select @@character_set_connection"),
        ["collation"] = Scalar(ctx, "select @@collation_connection"),
        ["timeZone"] = Scalar(ctx, "select @@session.time_zone")
    };

    private static string? Scalar(MySqlDataContext ctx, string sql)
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

    private static void WriteLine(
        StreamWriter writer,
        Dictionary<string, object?> server,
        LiveCase @case,
        string variant,
        RawObservation observation,
        RawObservation? raw = null,
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

    // ---------------------------------------------------------------- bare projection

    private static async Task<BareObservation> ObserveBare<T>(
        MySqlDataContext ctx, StreamWriter writer, string name, bool preExisting, QueryCommand<T> query)
    {
        var sql = PreparedSql(ctx, query);
        object? value = null;
        string? error = null;
        try
        {
            var rows = await query.ToListAsync();
            value = rows.Count == 0 ? null : rows[0];
        }
        catch (Exception exception)
        {
            error = $"{exception.GetType().Name}: {exception.Message}";
        }

        var folded = !sql.Contains(name + "(", StringComparison.OrdinalIgnoreCase);
        var observation = new BareObservation(name, preExisting, folded, sql, Format(value), error);

        writer.WriteLine(JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["kind"] = "bare-projection",
            ["name"] = name,
            ["preExisting"] = preExisting,
            ["folded"] = folded,
            ["sql"] = sql,
            ["value"] = observation.Value,
            ["error"] = error
        }));
        writer.Flush();

        return observation;
    }

    private static string PreparedSql<T>(MySqlDataContext ctx, QueryCommand<T> query)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(query, false, false, CancellationToken.None))
            .DbCommand.CommandText;

    // ---------------------------------------------------------------- probe table

    private static void CreateProbe(MySqlDataContext ctx)
    {
        ExecuteRaw(ctx, "drop temporary table if exists t349_probe");
        ExecuteRaw(
            ctx,
            "create temporary table t349_probe (" +
            "id int not null primary key, s varchar(64), n bigint default 7, d decimal(20,4), " +
            "b varbinary(64), dt datetime(6), tm time(6), j json, u bigint unsigned)");
        ExecuteRaw(
            ctx,
            "insert into t349_probe (id, s, n, d, b, dt, tm, j, u) values " +
            "(1, 'abc', 7, 1.2345, 0x0102, '2020-01-15 12:34:56.789012', " +
            "'12:34:56.789012', '{\"a\":1,\"b\":[1,2]}', 18446744073709551615)");
    }

    private static void ExecuteRaw(MySqlDataContext ctx, string sql)
    {
        using var command = ctx.CreateCommand(sql);
        command.ExecuteNonQuery();
    }

    // ---------------------------------------------------------------- formatting and equality

    private static string PathOf(string variable, string fallback)
    {
        var configured = Environment.GetEnvironmentVariable(variable);
        var path = configured is { Length: > 0 } ? configured : fallback;
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        return path;
    }

    private static StreamWriter Open(string path, FileMode mode) =>
        new(new FileStream(path, mode, FileAccess.Write, FileShare.Read)) { AutoFlush = true };

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

    /// <summary>Binary equality: the same CLR type and the same byte sequence; NULL matches only NULL.</summary>
    private static bool Bytes(object? raw, object? translated)
        => raw is null || translated is null
            ? raw is null && translated is null
            : raw is byte[] left && translated is byte[] right && left.SequenceEqual(right);

    /// <summary>Binary equality by type and length (NULL matches only NULL).</summary>
    private static bool BytesOfLength(object? raw, object? translated, int length)
        => raw is null || translated is null
            ? raw is null && translated is null
            : raw is byte[] left && translated is byte[] right && left.Length == length && right.Length == length;

    /// <summary>Binary equality by type and equal length (NULL matches only NULL).</summary>
    private static bool BytesSameLength(object? raw, object? translated)
        => raw is null || translated is null
            ? raw is null && translated is null
            : raw is byte[] left && translated is byte[] right && left.Length == right.Length;

    /// <summary>Type-only equality (the value itself is a server-side random).</summary>
    private static bool TypeOnly(object? raw, object? translated)
        => raw is null || translated is null
            ? raw is null && translated is null
            : raw.GetType() == translated.GetType();

    /// <summary>A positive unsigned BIGINT on both sides (<c>UUID_SHORT</c>).</summary>
    private static bool PositiveUnsigned(object? raw, object? translated)
        => raw is ulong left && translated is ulong right && left > 0 && right > 0;

    /// <summary>Either both sides are NULL or both are a non-empty string (<c>CURRENT_ROLE</c>, <c>ICU_VERSION</c>).</summary>
    private static bool StringOrNull(object? raw, object? translated)
        => raw is null && translated is null
           || raw is string left && translated is string right && left.Length > 0 && right.Length > 0;

    private sealed record Attempt(object? Value, string? Error);

    private sealed record BareObservation(
        string Name, bool PreExisting, bool Folded, string Sql, string? Value, string? Error);

    private sealed record RawObservation(
        string Sql,
        string? FieldType,
        string? ValueType,
        bool IsNull,
        object? Value,
        string? Text,
        string? Error,
        long DurationMs = 0);

    /// <summary>
    /// The 86 delivered capability keys (85 inventory names plus <c>column_default</c>) and the two
    /// already-covered controls (<c>json_storage_free</c>, <c>connection_id</c>). Three extra rows
    /// carry overloads that change the SQL shape, the NULL row is spelled per name, and every raw
    /// statement runs with the same argument types the production projection binds.
    /// </summary>
    private static class LiveCases
    {
        internal static readonly LiveCase[] All =
        [
            // ---------------------------------------------------------------- string (13)
            new("L01", "BIN", "string",
                "select bin(5)",
                "select bin(null)",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.bin(5UL)).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.bin((ulong?)null)).ToListAsync()).Single(),
                "System.String", Exact, "propagates"),
            new("L02", "EXPORT_SET", "string",
                "select export_set(5,'Y','N')",
                "select export_set(null,'Y','N')",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.export_set(5UL, "Y", "N")).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.export_set((ulong?)null, "Y", "N")).ToListAsync()).Single(),
                "System.String", Exact, "propagates"),
            new("L03", "HEX", "string",
                "select hex('abc')",
                "select hex(null)",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.hex("abc")).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.hex((string?)null)).ToListAsync()).Single(),
                "System.String", Exact, "propagates"),
            new("L04", "MAKE_SET", "string",
                "select make_set(5,'a','b')",
                "select make_set(null,'a','b')",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.make_set(5UL, "a", "b")).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.make_set((ulong?)null, "a", "b")).ToListAsync()).Single(),
                "System.String", Exact, "propagates"),
            new("L05", "OCT", "string",
                "select oct(8)",
                "select oct(null)",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.oct(8UL)).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.oct((ulong?)null)).ToListAsync()).Single(),
                "System.String", Exact, "propagates"),
            new("L06", "ORD", "string",
                "select ord('A')",
                "select ord(null)",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.ord("A")).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.ord((string?)null)).ToListAsync()).Single(),
                "System.Int64", Exact, "propagates"),
            new("L07", "QUOTE", "string",
                "select quote('a''b')",
                "select quote(null)",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.quote("a'b")).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.quote((string?)null)).ToListAsync()).Single(),
                "System.String", Exact, "non-propagating:\"NULL\""),
            new("L08", "SOUNDEX", "string",
                "select soundex('hello')",
                "select soundex(null)",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.soundex("hello")).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.soundex((string?)null)).ToListAsync()).Single(),
                "System.String", Exact, "propagates"),
            new("L09", "STRCMP", "string",
                "select strcmp('a','b')",
                "select strcmp(null,'b')",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.strcmp("a", "b")).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.strcmp((string?)null, "b")).ToListAsync()).Single(),
                "System.Int64", Exact, "propagates"),
            new("L10", "TO_BASE64", "string",
                "select to_base64(b) from t349_probe",
                "select to_base64(null)",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.to_base64(x.B)).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.to_base64((byte[]?)null)).ToListAsync()).Single(),
                "System.String", Exact, "propagates"),
            new("L11", "FROM_BASE64", "string",
                "select from_base64('YWJj')",
                "select from_base64(null)",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.from_base64("YWJj")).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.from_base64((string?)null)).ToListAsync()).Single(),
                "byte[]", Bytes, "propagates"),
            new("L12", "UNHEX", "string",
                "select unhex('6162')",
                "select unhex(null)",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.unhex("6162")).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.unhex((string?)null)).ToListAsync()).Single(),
                "byte[]", Bytes, "propagates"),
            new("L13", "WEIGHT_STRING", "string",
                "select weight_string(s) from t349_probe",
                "select weight_string(null)",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.weight_string(x.S)).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.weight_string((string?)null)).ToListAsync()).Single(),
                "byte[]", Bytes, "propagates"),

            // ---------------------------------------------------------------- numeric (5)
            new("L14", "CONV", "numeric",
                "select conv('ff',16,10)",
                "select conv(null,16,10)",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.conv("ff", 16, 10)).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.conv((string?)null, 16, 10)).ToListAsync()).Single(),
                "System.String", Exact, "propagates"),
            new("L15", "CRC32", "numeric",
                "select crc32('abc')",
                "select crc32(null)",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.crc32("abc")).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.crc32((string?)null)).ToListAsync()).Single(),
                "System.UInt64", Exact, "propagates"),
            new("L16", "LOG2", "numeric",
                "select log2(8)",
                "select log2(null)",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.log2(8d)).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.log2((double?)null)).ToListAsync()).Single(),
                "System.Double", Exact, "propagates"),
            new("L17", "RAND", "numeric",
                "select rand(42)",
                null,
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.rand(42L)).ToListAsync()).Single(),
                null,
                "System.Double", Exact, "n/a (seeded PRNG, no NULL semantics)"),
            new("L17-b", "RAND", "numeric",
                "select rand()",
                null,
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => new { x.Id, V = SqlFunctions.MySql.rand() }).ToListAsync()).Single().V,
                null,
                "System.Double", TypeOnly, "n/a (unseeded PRNG; type only)"),
            new("L18", "TRUNCATE", "numeric",
                "select truncate(d,2) from t349_probe",
                "select truncate(null,2)",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.truncate(x.D, 2)).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.truncate((decimal?)null, 2)).ToListAsync()).Single(),
                "System.Decimal", Exact, "propagates"),

            // ---------------------------------------------------------------- date/time (23)
            new("L19", "ADDDATE", "date/time",
                "select adddate(dt,1) from t349_probe",
                "select adddate(null,1)",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.adddate(x.Dt, 1)).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.adddate((DateTime?)null, 1)).ToListAsync()).Single(),
                "System.DateTime", Exact, "propagates"),
            new("L20", "ADDTIME", "date/time",
                "select addtime(dt,'01:00:00') from t349_probe",
                "select addtime(null,null)",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.addtime(x.Dt, OneHour)).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.addtime((DateTime?)null, (TimeSpan?)null)).ToListAsync()).Single(),
                "System.DateTime", Exact, "propagates"),
            new("L21", "CONVERT_TZ", "date/time",
                "select convert_tz('2026-01-15 12:00:00','UTC','Europe/Berlin')",
                "select convert_tz(null,'UTC','Europe/Berlin')",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.convert_tz(ConvertFrom, "UTC", "Europe/Berlin")).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.convert_tz((DateTime?)null, "UTC", "Europe/Berlin")).ToListAsync()).Single(),
                "System.DateTime", Exact, "propagates"),
            new("L22", "DAYNAME", "date/time",
                "select dayname(dt) from t349_probe",
                "select dayname(null)",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.dayname(x.Dt)).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.dayname((DateTime?)null)).ToListAsync()).Single(),
                "System.String", Exact, "propagates"),
            new("L23", "FROM_DAYS", "date/time",
                "select from_days(738000)",
                "select from_days(null)",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.from_days(738000L)).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.from_days(null)).ToListAsync()).Single(),
                "System.DateTime", Exact, "propagates"),
            new("L24", "GET_FORMAT", "date/time",
                "select get_format(DATE,'EUR')",
                null,
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.get_format(MySqlFormatKind.Date, "EUR")).ToListAsync()).Single(),
                null,
                "System.String", Exact,
                "n/a (both arguments are grammar constants; the delivered guard refuses a NULL format literal before any SQL)"),
            new("L25", "MAKEDATE", "date/time",
                "select makedate(2020,32)",
                "select makedate(null,32)",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.makedate(2020, 32)).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.makedate(null, 32)).ToListAsync()).Single(),
                "System.DateTime", Exact, "propagates"),
            new("L26", "MAKETIME", "date/time",
                "select maketime(12,34,56)",
                "select maketime(12,null,56)",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.maketime(12, 34, 56m)).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.maketime(12, null, 56m)).ToListAsync()).Single(),
                "System.TimeSpan", Exact, "propagates"),
            new("L27", "MICROSECOND", "date/time",
                "select microsecond(dt) from t349_probe",
                "select microsecond(null)",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.microsecond(x.Dt)).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.microsecond((DateTime?)null)).ToListAsync()).Single(),
                "System.Int64", Exact, "propagates"),
            new("L28", "MONTHNAME", "date/time",
                "select monthname(dt) from t349_probe",
                "select monthname(null)",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.monthname(x.Dt)).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.monthname((DateTime?)null)).ToListAsync()).Single(),
                "System.String", Exact, "propagates"),
            new("L29", "PERIOD_ADD", "date/time",
                "select period_add(202401,2)",
                "select period_add(null,2)",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.period_add(202401L, 2L)).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.period_add(null, 2L)).ToListAsync()).Single(),
                "System.Int64", Exact, "propagates"),
            new("L30", "PERIOD_DIFF", "date/time",
                "select period_diff(202402,202401)",
                "select period_diff(null,202401)",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.period_diff(202402L, 202401L)).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.period_diff(null, 202401L)).ToListAsync()).Single(),
                "System.Int64", Exact, "propagates"),
            new("L31", "SEC_TO_TIME", "date/time",
                "select sec_to_time(3661)",
                "select sec_to_time(null)",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.sec_to_time(3661m)).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.sec_to_time((decimal?)null)).ToListAsync()).Single(),
                "System.TimeSpan", Exact, "propagates"),
            new("L32", "SUBDATE", "date/time",
                "select subdate(dt,1) from t349_probe",
                "select subdate(null,1)",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.subdate(x.Dt, 1)).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.subdate((DateTime?)null, 1)).ToListAsync()).Single(),
                "System.DateTime", Exact, "propagates"),
            new("L33", "SUBTIME", "date/time",
                "select subtime(dt,'01:00:00') from t349_probe",
                "select subtime(null,null)",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.subtime(x.Dt, OneHour)).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.subtime((DateTime?)null, (TimeSpan?)null)).ToListAsync()).Single(),
                "System.DateTime", Exact, "propagates"),
            new("L34", "TIME", "date/time",
                "select time(dt) from t349_probe",
                "select time(null)",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.time(x.Dt)).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.time((DateTime?)null)).ToListAsync()).Single(),
                "System.TimeSpan", Exact, "propagates"),
            new("L35", "TIME_FORMAT", "date/time",
                "select time_format(tm,'%H:%i') from t349_probe",
                "select time_format(null,'%H:%i')",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.time_format(x.Tm, "%H:%i")).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.time_format((TimeSpan?)null, "%H:%i")).ToListAsync()).Single(),
                "System.String", Exact, "propagates"),
            new("L36", "TIME_TO_SEC", "date/time",
                "select time_to_sec(tm) from t349_probe",
                "select time_to_sec(null)",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.time_to_sec(x.Tm)).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.time_to_sec((TimeSpan?)null)).ToListAsync()).Single(),
                "System.Int64", Exact, "propagates"),
            new("L37", "TIMEDIFF", "date/time",
                "select timediff(dt,'2020-01-15 00:00:00') from t349_probe",
                "select timediff(null,null)",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.timediff(x.Dt, DayStart)).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.timediff((DateTime?)null, (DateTime?)null)).ToListAsync()).Single(),
                "System.TimeSpan", Exact, "propagates"),
            new("L38", "TIMESTAMP", "date/time",
                "select timestamp(dt) from t349_probe",
                "select timestamp(null)",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.timestamp(x.Dt)).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.timestamp((DateTime?)null)).ToListAsync()).Single(),
                "System.DateTime", Exact, "propagates"),
            new("L39", "TO_DAYS", "date/time",
                "select to_days(dt) from t349_probe",
                "select to_days(null)",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.to_days(x.Dt)).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.to_days((DateTime?)null)).ToListAsync()).Single(),
                "System.Int64", Exact, "propagates"),
            new("L40", "TO_SECONDS", "date/time",
                "select to_seconds(dt) from t349_probe",
                "select to_seconds(null)",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.to_seconds(x.Dt)).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.to_seconds((DateTime?)null)).ToListAsync()).Single(),
                "System.Int64", Exact, "propagates"),
            new("L41", "YEARWEEK", "date/time",
                "select yearweek(dt) from t349_probe",
                "select yearweek(null)",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.yearweek(x.Dt)).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.yearweek((DateTime?)null)).ToListAsync()).Single(),
                "System.Int64", Exact, "propagates"),

            // ---------------------------------------------------------------- JSON (17)
            new("L42", "JSON_ARRAY", "JSON",
                "select json_array(1,'a',null)",
                "select json_array(null)",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.json_array(1, "a", null)).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.json_array((object?)null)).ToListAsync()).Single(),
                "System.String", Exact, "non-propagating:[null]"),
            new("L43", "JSON_CONTAINS", "JSON",
                "select json_contains(j,'1','$.a') from t349_probe",
                "select json_contains(null,'1')",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.json_contains(x.J, "1", "$.a")).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.json_contains((string?)null, "1")).ToListAsync()).Single(),
                "System.Int64", Exact, "propagates"),
            new("L44", "JSON_CONTAINS_PATH", "JSON",
                "select json_contains_path(j,'one','$.a') from t349_probe",
                "select json_contains_path(null,'one','$.a')",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.json_contains_path(x.J, "one", "$.a")).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.json_contains_path((string?)null, "one", "$.a")).ToListAsync()).Single(),
                "System.Int64", Exact, "propagates"),
            new("L45", "JSON_MERGE", "JSON",
                "select json_merge('{\"a\":1}','{\"b\":2}')",
                "select json_merge(null,'{\"b\":2}')",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.json_merge("{\"a\":1}", "{\"b\":2}")).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.json_merge((string?)null, "{\"b\":2}")).ToListAsync()).Single(),
                "System.String", Exact, "propagates"),
            new("L46", "JSON_OBJECT", "JSON",
                "select json_object('k',1)",
                "select json_object('k',null)",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.json_object("k", 1)).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.json_object("k", null)).ToListAsync()).Single(),
                "System.String", Exact, "non-propagating:{\"k\": null}"),
            new("L47", "JSON_OVERLAPS", "JSON",
                "select json_overlaps('[1,2]','[2,3]')",
                "select json_overlaps(null,'[2,3]')",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.json_overlaps("[1,2]", "[2,3]")).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.json_overlaps((string?)null, "[2,3]")).ToListAsync()).Single(),
                "System.Int64", Exact, "propagates"),
            new("L48", "JSON_PRETTY", "JSON",
                "select json_pretty(j) from t349_probe",
                "select json_pretty(null)",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.json_pretty(x.J)).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.json_pretty((string?)null)).ToListAsync()).Single(),
                "System.String", Exact, "propagates"),
            new("L49", "JSON_QUOTE", "JSON",
                "select json_quote('a\"b')",
                "select json_quote(null)",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.json_quote("a\"b")).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.json_quote((string?)null)).ToListAsync()).Single(),
                "System.String", Exact, "propagates"),
            new("L50", "JSON_SCHEMA_VALID", "JSON",
                "select json_schema_valid('{\"type\":\"object\"}','{}')",
                "select json_schema_valid(null,'{}')",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.json_schema_valid("{\"type\":\"object\"}", "{}")).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.json_schema_valid((string?)null, "{}")).ToListAsync()).Single(),
                "System.Int64", Exact, "propagates"),
            new("L51", "JSON_SCHEMA_VALIDATION_REPORT", "JSON",
                "select json_schema_validation_report('{\"type\":\"object\"}','{}')",
                "select json_schema_validation_report(null,'{}')",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.json_schema_validation_report("{\"type\":\"object\"}", "{}")).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.json_schema_validation_report((string?)null, "{}")).ToListAsync()).Single(),
                "System.String", Exact, "propagates"),
            new("L52", "JSON_SEARCH", "JSON",
                "select json_search('[\"abc\"]','one','abc')",
                "select json_search(null,'one','abc')",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.json_search("[\"abc\"]", "one", "abc")).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.json_search((string?)null, "one", "abc")).ToListAsync()).Single(),
                "System.String", Exact, "propagates"),
            new("L54", "JSON_STORAGE_SIZE", "JSON",
                "select json_storage_size(j) from t349_probe",
                "select json_storage_size(null)",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.json_storage_size(x.J)).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.json_storage_size((string?)null)).ToListAsync()).Single(),
                "System.Int64", Exact, "propagates"),
            new("L56", "JSON_ARRAYAGG", "JSON",
                "select json_arrayagg(s) from t349_probe where id = 1",
                "select json_arrayagg(null) from t349_probe where id = 1",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => new { x.Id, V = SqlFunctions.MySql.json_arrayagg(x.S) }).ToListAsync()).Single().V,
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => new { x.Id, V = SqlFunctions.MySql.json_arrayagg((string?)null) }).ToListAsync()).Single().V,
                "System.String", Exact, "non-propagating:[null]"),
            new("L57", "JSON_OBJECTAGG", "JSON",
                "select json_objectagg('k',n) from t349_probe where id = 1",
                "select json_objectagg('k',null) from t349_probe where id = 1",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => new { x.Id, V = SqlFunctions.MySql.json_objectagg("k", x.N) }).ToListAsync()).Single().V,
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => new { x.Id, V = SqlFunctions.MySql.json_objectagg("k", null) }).ToListAsync()).Single().V,
                "System.String", Exact, "non-propagating:{\"k\": null}"),
            new("L58", "->", "JSON",
                "select j -> '$.a' from t349_probe",
                null,
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.json_arrow(x.J, "$.a")).ToListAsync()).Single(),
                null,
                "System.String", Exact,
                "n/a (the operator requires a column reference on the left; NULL cannot be spelled in that grammar position)"),
            new("L59", "->>", "JSON",
                "select j ->> '$.a' from t349_probe",
                null,
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.json_arrow_text(x.J, "$.a")).ToListAsync()).Single(),
                null,
                "System.String", Exact,
                "n/a (the operator requires a column reference on the left; NULL cannot be spelled in that grammar position)"),
            new("L60", "MEMBER OF", "JSON",
                "select s member of ('[\"a\"]') from t349_probe",
                "select null member of ('[\"a\"]')",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.member_of(x.S, "[\"a\"]")).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.member_of((object?)null, "[\"a\"]")).ToListAsync()).Single(),
                "System.Int64", Exact, "propagates"),

            // ---------------------------------------------------------------- aggregate (3)
            new("L61", "BIT_AND", "aggregate",
                "select bit_and(u) from t349_probe where id = 1",
                "select bit_and(null) from t349_probe where id = 1",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => new { x.Id, V = SqlFunctions.MySql.bit_and(x.U) }).ToListAsync()).Single().V,
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => new { x.Id, V = SqlFunctions.MySql.bit_and((ulong?)null) }).ToListAsync()).Single().V,
                "System.UInt64", Exact, "non-propagating:0 (the aggregate of a NULL argument is the identity 0)"),
            new("L62", "BIT_OR", "aggregate",
                "select bit_or(u) from t349_probe where id = 1",
                "select bit_or(null) from t349_probe where id = 1",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => new { x.Id, V = SqlFunctions.MySql.bit_or(x.U) }).ToListAsync()).Single().V,
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => new { x.Id, V = SqlFunctions.MySql.bit_or((ulong?)null) }).ToListAsync()).Single().V,
                "System.UInt64", Exact, "non-propagating:0 (the aggregate of a NULL argument is the identity 0)"),
            new("L63", "BIT_XOR", "aggregate",
                "select bit_xor(u) from t349_probe where id = 1",
                "select bit_xor(null) from t349_probe where id = 1",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => new { x.Id, V = SqlFunctions.MySql.bit_xor(x.U) }).ToListAsync()).Single().V,
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => new { x.Id, V = SqlFunctions.MySql.bit_xor((ulong?)null) }).ToListAsync()).Single().V,
                "System.UInt64", Exact, "non-propagating:0 (the aggregate of a NULL argument is the identity 0)"),

            // ---------------------------------------------------------------- bit (1)
            new("L64", "BIT_COUNT", "bit",
                "select bit_count(7)",
                "select bit_count(null)",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.bit_count(7UL)).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.bit_count((ulong?)null)).ToListAsync()).Single(),
                "System.Int64", Exact, "propagates"),

            // ---------------------------------------------------------------- crypto (9)
            new("L65", "AES_ENCRYPT", "crypto",
                "select aes_encrypt('text','key')",
                "select aes_encrypt(null,null)",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.aes_encrypt(AesText, AesKey)).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.aes_encrypt(null, null)).ToListAsync()).Single(),
                "byte[]", BytesSameLength,
                "propagates (the payload is randomised by the generated IV; type and length only)"),
            new("L65-b", "AES_ENCRYPT", "crypto",
                "select aes_encrypt('text','key',unhex('00000000000000000000000000000000'))",
                null,
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.aes_encrypt(AesText, AesKey, ZeroIv)).ToListAsync()).Single(),
                null,
                "byte[]", Bytes, "n/a (explicit IV overload; deterministic value)"),
            new("L66", "AES_DECRYPT", "crypto",
                "select aes_decrypt(aes_encrypt('text','key'),'key')",
                "select aes_decrypt(null,null)",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.aes_decrypt(
                        SqlFunctions.MySql.aes_encrypt(AesText, AesKey), AesKey)).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.aes_decrypt(null, null)).ToListAsync()).Single(),
                "byte[]", Bytes, "propagates"),
            new("L67", "COMPRESS", "crypto",
                "select compress('abc')",
                "select compress(null)",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.compress(CompressInput)).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.compress(null)).ToListAsync()).Single(),
                "byte[]", Bytes, "propagates"),
            new("L68", "UNCOMPRESS", "crypto",
                "select uncompress(compress('abc'))",
                "select uncompress(null)",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.uncompress(SqlFunctions.MySql.compress(CompressInput))).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.uncompress(null)).ToListAsync()).Single(),
                "byte[]", Bytes, "propagates"),
            new("L69", "UNCOMPRESSED_LENGTH", "crypto",
                "select uncompressed_length(compress('abc'))",
                "select uncompressed_length(null)",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.uncompressed_length(SqlFunctions.MySql.compress(CompressInput))).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.uncompressed_length(null)).ToListAsync()).Single(),
                "System.Int64", Exact, "propagates"),
            new("L70", "RANDOM_BYTES", "crypto",
                "select random_bytes(4)",
                "select random_bytes(null)",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.random_bytes(4)).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.random_bytes(null)).ToListAsync()).Single(),
                "byte[]", (raw, translated) => BytesOfLength(raw, translated, 4),
                "propagates (the payload is random; type and length only)"),
            new("L71", "VALIDATE_PASSWORD_STRENGTH", "crypto",
                "select validate_password_strength('abc')",
                "select validate_password_strength(null)",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.validate_password_strength("abc")).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.validate_password_strength((string?)null)).ToListAsync()).Single(),
                "System.Int64", Exact,
                "propagates (component absent; the value 0 is not a grading claim)"),
            new("L72", "STATEMENT_DIGEST", "crypto",
                "select statement_digest('SELECT 1')",
                "select statement_digest(null)",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.statement_digest("SELECT 1")).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.statement_digest((string?)null)).ToListAsync()).Single(),
                "System.String", Exact,
                "propagates (the connector materialises the VARBINARY digest as a lowercase hex string)"),
            new("L73", "STATEMENT_DIGEST_TEXT", "crypto",
                "select statement_digest_text('SELECT 1')",
                "select statement_digest_text(null)",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.statement_digest_text("SELECT 1")).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.statement_digest_text((string?)null)).ToListAsync()).Single(),
                "System.String", Exact, "propagates"),

            // ---------------------------------------------------------------- information (9 + control)
            new("L74", "CONNECTION_ID", "information",
                "select connection_id()",
                null,
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => new { x.Id, V = SqlFunctions.Sql.connection_id() }).ToListAsync()).Single().V,
                null,
                "System.UInt64", Exact,
                "n/a (zero arguments; the control is compared on the same open connection)"),
            new("L75", "FOUND_ROWS", "information",
                "select found_rows()",
                null,
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => new { x.Id, V = SqlFunctions.MySql.found_rows() }).ToListAsync()).Single().V,
                null,
                "System.Int64", Exact,
                "n/a (session state; SQL_CALC_FOUND_ROWS is replayed immediately before each observation)",
                "select sql_calc_found_rows 1"),
            new("L76", "LAST_INSERT_ID", "information",
                "select last_insert_id()",
                null,
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => new { x.Id, V = SqlFunctions.MySql.last_insert_id() }).ToListAsync()).Single().V,
                null,
                "System.UInt64", Exact,
                "n/a (session state; LAST_INSERT_ID(7) is replayed immediately before each observation)",
                "select last_insert_id(7)"),
            new("L76-b", "LAST_INSERT_ID", "information",
                "select last_insert_id(7)",
                null,
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => new { x.Id, V = SqlFunctions.MySql.last_insert_id(7UL) }).ToListAsync()).Single().V,
                null,
                "System.UInt64", Exact, "n/a (write form; no NULL semantics)"),
            new("L77", "ROW_COUNT", "information",
                "select row_count()",
                null,
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => new { x.Id, V = SqlFunctions.MySql.row_count() }).ToListAsync()).Single().V,
                null,
                "System.Int64", Exact,
                "n/a (session state; a no-op DO statement is replayed immediately before each observation)",
                "do 1"),
            new("L78", "CURRENT_ROLE", "information",
                "select current_role()",
                null,
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => new { x.Id, V = SqlFunctions.MySql.current_role() }).ToListAsync()).Single().V,
                null,
                "System.String", Exact, "n/a (zero arguments; NULL or the granted role, observed)"),
            new("L79", "CHARSET", "information",
                "select charset('a')",
                "select charset(null)",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.charset("a")).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.charset(null)).ToListAsync()).Single(),
                "System.String", Exact, "non-propagating:server-defined (a NULL argument has a charset label, not NULL)"),
            new("L80", "COLLATION", "information",
                "select collation('a')",
                "select collation(null)",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.collation("a")).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.collation(null)).ToListAsync()).Single(),
                "System.String", Exact, "non-propagating:server-defined (a NULL argument has a collation label, not NULL)"),
            new("L81", "COERCIBILITY", "information",
                "select coercibility('a')",
                "select coercibility(null)",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.coercibility("a")).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.coercibility(null)).ToListAsync()).Single(),
                "System.Int64", Exact, "non-propagating:server-defined (a NULL argument has a coercibility, not NULL)"),
            new("L82", "BENCHMARK", "information",
                "select benchmark(1,1+1)",
                null,
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => new { x.Id, V = SqlFunctions.MySql.benchmark(1L, 1 + 1) }).ToListAsync()).Single().V,
                null,
                "System.Int64", Exact, "n/a (timing helper; always 0)"),
            new("L83", "ICU_VERSION", "information",
                "select icu_version()",
                null,
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => new { x.Id, V = SqlFunctions.MySql.icu_version() }).ToListAsync()).Single().V,
                null,
                "System.String", Exact, "n/a (zero arguments; a non-empty server build string)"),

            // ---------------------------------------------------------------- miscellaneous (5)
            new("L85", "UUID_SHORT", "miscellaneous",
                "select uuid_short()",
                null,
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => new { x.Id, V = SqlFunctions.MySql.uuid_short() }).ToListAsync()).Single().V,
                null,
                "System.UInt64", PositiveUnsigned, "n/a (server-generated; unsigned 64-bit and positive)"),
            new("L86", "IS_UUID", "miscellaneous",
                "select is_uuid('00000000-0000-0000-0000-000000000000')",
                "select is_uuid(null)",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.is_uuid("00000000-0000-0000-0000-000000000000")).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.is_uuid((string?)null)).ToListAsync()).Single(),
                "System.Int64", Exact, "propagates"),
            new("L87", "SLEEP", "miscellaneous",
                "select sleep(0)",
                null,
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => new { x.Id, V = SqlFunctions.MySql.sleep(0d) }).ToListAsync()).Single().V,
                null,
                "System.Int64", Exact, "n/a (timing helper; always 0 for a zero duration)"),
            new("L88", "GROUPING", "miscellaneous",
                "select grouping(n) from t349_probe where id = 1 group by n with rollup order by 1 limit 1",
                null,
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .GroupByRollup(x => new { x.N })
                    .Select(x => new { V = SqlFunctions.MySql.grouping(x.N) })
                    .OrderBy(x => x.V).ToListAsync()).First().V,
                null,
                "System.Int64", Exact,
                "n/a (requires a GROUP BY ... WITH ROLLUP position; the grouped row is observed first)"),
            new("L90", "NAME_CONST", "miscellaneous",
                "select name_const('n',1)",
                null,
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.name_const("n", 1L)).ToListAsync()).Single(),
                null,
                "System.Int64", Exact,
                "n/a (NAME_CONST requires a constant value argument; a column reference is rejected with 1210)"),

            // ---------------------------------------------------------------- the 86th delivered key
            new("L91", "COLUMN_DEFAULT", "miscellaneous",
                "select default(n) from t349_probe",
                null,
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.column_default(x.N)).ToListAsync()).Single(),
                null,
                "System.Int64", Exact,
                "n/a (DEFAULT(col) returns the declared column default; a column without one server-errors 1364)"),

            // ---------------------------------------------------------------- already-covered controls (2)
            new("L53", "JSON_STORAGE_FREE", "JSON",
                "select json_storage_free(j) from t349_probe",
                "select json_storage_free(null)",
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.json_storage_free(x.J)).ToListAsync()).Single(),
                async ctx => (object?)(await ctx.From<IT349Probe>().Where(x => x.Id == 1)
                    .Select(x => SqlFunctions.MySql.json_storage_free((string?)null)).ToListAsync()).Single(),
                "System.Int64", Exact, "propagates (regression control: already-covered name)")
        ];
    }
}
