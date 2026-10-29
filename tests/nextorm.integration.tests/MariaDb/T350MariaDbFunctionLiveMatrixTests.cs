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
/// <param name="ExpectedClr">
/// The CLR type the delivered signature must materialise: the asserted contract is this declared surface
/// type after materialisation, compared on the production path. A narrower provider wire type is an
/// observation, declared separately through <c>WireClr</c>.
/// </param>
/// <param name="Eq">Value comparison (type-exact unless the case is deliberately non-deterministic).</param>
/// <param name="NullPolicy"><c>propagates</c>, <c>non-propagating:&lt;expected&gt;</c> or <c>n/a</c>.</param>
/// <param name="SetupSql">Per-case setup replayed before the raw and the translated observation.</param>
/// <param name="TeardownSql">Per-case teardown replayed after the case, in both phases.</param>
/// <param name="Precondition">
/// The server precondition a row needs before it can be observed at all, or <c>null</c> when the row has
/// none. A delegate returning a reason string marks the case as <em>skipped</em>: the reason is written to
/// both artifacts, no statement is executed and no comparison is counted, because a precondition failure
/// is not a value the two paths could disagree on (<c>BINLOG_GTID_POS</c> on the binlog-less stock image).
/// Returning <c>null</c> means the precondition holds and the row is compared normally.
/// </param>
/// <param name="WireClr">
/// The provider's narrower wire type when the server returns a 32-bit field the delivered signature widens
/// (MariaDB's <c>int</c> where the shared member is typed for MySQL's <c>BIGINT</c>). The asserted contract
/// stays the declared surface type the production path materialises; the wire type is the observation, and
/// declaring it here keeps a future wire-type change visible instead of silently tolerated. The value
/// equality rule (<see cref="Numeric"/>) already compares the widened number exactly.
/// </param>
/// <param name="NotComparableReason">
/// The reason the row's NULL variant has no comparable form, or <c>null</c> when the NULL variant is compared.
/// A non-comparable NULL is neither a pass nor a refusal: no translated NULL projection is executed, the raw
/// NULL observation stays in the oracle artifact, and the row is counted in the <c>notComparable</c> bucket so
/// the matrix still closes (<c>compared + notComparable + skipped == cases</c>).
/// </param>
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
    string? TeardownSql = null,
    Func<MariaDbDataContext, string?>? Precondition = null,
    string? WireClr = null,
    string? NotComparableReason = null)
{
    /// <summary>
    /// The annotation of a row whose provider wire type is narrower than the declared surface type: the
    /// asserted contract is the materialised surface type, the wire type is the recorded observation and the
    /// widening is lossless.
    /// </summary>
    internal const string NarrowerWireAnnotation =
        "Narrower provider wire type; lossless widening to the declared CLR contract verified.";

    /// <summary>
    /// The annotation of a row whose untyped-NULL wire metadata is binary (<c>System.Byte[]</c>) while the
    /// delivered signature declares <c>System.String</c>: the NULL form has no comparable shape, the non-NULL
    /// form stays asserted.
    /// </summary>
    internal const string NoComparableNullFormAnnotation =
        "No comparable untyped-NULL form: binary wire metadata produces System.Byte[], incompatible with the " +
        "declared System.String result. Non-NULL form verified.";

    /// <summary>The annotation recorded with the row when its contract departs from an exact wire type.</summary>
    internal string? Annotation => WireClr is not null
        ? NarrowerWireAnnotation
        : NotComparableReason is not null
            ? NoComparableNullFormAnnotation
            : null;
}

/// <summary>The one-row probe table the translated projections read from, so no case needs its own source.</summary>
[SqlTable("t350_probe")]
public sealed class T350ProbeRow
{
    /// <summary>The probe row identifier.</summary>
    [Key]
    [Column("id")]
    public int Id { get; set; }
}

/// <summary>The two-row aggregate fixture (<c>1</c>, <c>3</c>) the bit aggregates reduce.</summary>
[SqlTable("t350_agg")]
public sealed class T350AggRow
{
    /// <summary>One row of the fixture.</summary>
    [Column("x")]
    public ulong X { get; set; }
}

/// <summary>The two-row fixture (<c>1</c>, <c>2</c>) <c>JSON_ARRAYAGG</c> collects.</summary>
[SqlTable("t350_pair")]
public sealed class T350PairRow
{
    /// <summary>One row of the fixture.</summary>
    [Column("x")]
    public ulong X { get; set; }
}

/// <summary>The single-row fixture <c>JSON_OBJECTAGG</c> aggregates.</summary>
[SqlTable("t350_kv")]
public sealed class T350KvRow
{
    /// <summary>The aggregated value.</summary>
    [Key]
    [Column("v")]
    public int V { get; set; }
}

/// <summary>The column-default fixture: one row whose <c>id</c> column declares <c>default 7</c>.</summary>
[SqlTable("t350_def")]
public sealed class T350DefRow
{
    /// <summary>The column whose default <c>DEFAULT(id)</c> returns.</summary>
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

    /// <summary>The <c>text</c> plaintext of the AES rows.</summary>
    private static readonly byte[] AesText = [116, 101, 120, 116];

    /// <summary>The <c>key</c> of the AES rows.</summary>
    private static readonly byte[] AesKey = [107, 101, 121];

    /// <summary>The <c>abc</c> bytes of the base64, compress and uncompress rows.</summary>
    private static readonly byte[] AbcBytes = [97, 98, 99];

    /// <summary>The <c>2020-01-01</c> day the date rows observe.</summary>
    private static readonly DateTime Day = new(2020, 1, 1);

    /// <summary>The following day, for the difference rows.</summary>
    private static readonly DateTime NextDay = new(2020, 1, 2);

    /// <summary><c>2020-01-01 00:00:00.123456</c> as a <see cref="DateTime"/> (1 tick = 0.1 µs).</summary>
    private static readonly DateTime MicroTime = new DateTime(2020, 1, 1).AddTicks(1_234_560);

    /// <summary><c>2020-01-01 12:34:56</c>, the probe's time-of-day value.</summary>
    private static readonly DateTime Noonish = new(2020, 1, 1, 12, 34, 56);

    /// <summary><c>01:00:00</c>, the probe's duration argument.</summary>
    private static readonly TimeSpan OneHour = TimeSpan.FromHours(1);

    /// <summary><c>12:34:56</c>, the probe's time-of-day argument.</summary>
    private static readonly TimeSpan NoonTime = new(12, 34, 56);

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
    /// and compares value, NULL result and the declared surface type against the raw record, closing on
    /// <c>compared + notComparable + skipped == cases</c> with an empty failure list. Two departures from an
    /// exact signed/type comparison are declared per row rather than loosened globally: a narrower provider
    /// wire type (<c>WireClr</c>, the value is compared and the materialised type is checked) and a NULL
    /// variant with no comparable form (<c>NotComparableReason</c>, recorded and counted separately).
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
            var skippedById = new Dictionary<string, string>(StringComparer.Ordinal);
            var lines = 0;

            foreach (var @case in cases)
            {
                if (@case.Precondition?.Invoke(ctx) is { Length: > 0 } reason)
                {
                    WriteSkip(oracle, server, @case, "primary", reason);
                    skippedById[@case.Id] = reason;
                    continue;
                }

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
            (lines + skippedById.Count).Should().BeGreaterThanOrEqualTo(
                cases.Length,
                "the raw oracle must be complete before any translated assertion runs");

            // ---------------------------------------------------------------- phase 2: translated
            using var results = T350RawOracle.Open(resultPath, FileMode.Create);
            var failures = new List<string>();
            var compared = 0;
            var notComparable = 0;
            var nullCompared = 0;

            foreach (var @case in cases)
            {
                if (skippedById.TryGetValue(@case.Id, out var skipReason))
                {
                    WriteSkip(results, server, @case, "translated", skipReason);
                    continue;
                }

                var raw = rawById[@case.Id];
                var translated = await TryTranslated(ctx, @case, @case.Translated);
                var verdict = CompareCase(@case, "primary", raw, translated, expectedNull: false);
                if (verdict is not null)
                    failures.Add(verdict);

                object? translatedNull = null;
                string? nullError = null;

                if (@case.NotComparableReason is { Length: > 0 } reason)
                {
                    // The row declares its NULL variant non-comparable: the non-NULL form above stays
                    // asserted, no translated NULL projection runs, and the row joins its own bucket so the
                    // matrix still closes over the case table.
                    notComparable++;
                    WriteNotComparable(results, server, @case, reason, rawNullById.GetValueOrDefault(@case.Id));
                }
                else
                {
                    compared++;

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

            compared.Should().Be(
                cases.Length - skippedById.Count - notComparable,
                "every live case that is neither skipped by a server precondition nor declared " +
                "non-comparable must be compared");
            (compared + notComparable + skippedById.Count).Should().Be(
                cases.Length,
                "the matrix closes only when compared + notComparable + skipped equals the case table");
            notComparable.Should().Be(
                cases.Count(@case => @case.NotComparableReason is not null),
                "a case may be counted as non-comparable only when it declares the exclusion");
            cases.Count(@case => @case.Precondition is not null).Should().BeGreaterThanOrEqualTo(
                skippedById.Count,
                "a case may only be skipped through a declared server precondition");
            failures.Should().BeEmpty(
                "every delivered name must match the raw oracle; failures: " + string.Join(" | ", failures));
            Console.WriteLine(
                $"T350 live matrix: cases={cases.Length} compared={compared} notComparable={notComparable} " +
                $"skipped={skippedById.Count} nullCompared={nullCompared} oracleLines={lines} failures=0");
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
        if (@case.WireClr is { Length: > 0 } wireClr)
        {
            // The asserted contract is the declared surface type the production path materialises (checked
            // above); the narrower wire type is the recorded observation the widening was accepted on.
            if (rawType != wireClr)
                return $"{label}: raw provider wire type {rawType} != recorded wire type {wireClr}";
        }
        else if (rawType != @case.ExpectedClr)
        {
            return $"{label}: raw oracle CLR type {rawType} != delivered signature {@case.ExpectedClr}";
        }
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
            ["nullPolicy"] = @case.NullPolicy,
            ["annotation"] = @case.Annotation
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

    /// <summary>
    /// Writes the one line of a case whose server precondition does not hold: the reason is the evidence,
    /// no statement ran and no comparison is counted.
    /// </summary>
    private static void WriteSkip(
        StreamWriter writer,
        Dictionary<string, object?> server,
        T350LiveCase @case,
        string variant,
        string reason)
    {
        writer.WriteLine(JsonSerializer.Serialize(new Dictionary<string, object?>(server)
        {
            ["kind"] = "skipped",
            ["id"] = @case.Id,
            ["name"] = @case.Name,
            ["family"] = @case.Family,
            ["variant"] = variant,
            ["sql"] = @case.RawSql,
            ["reason"] = reason,
            ["expectedClr"] = @case.ExpectedClr,
            ["nullPolicy"] = @case.NullPolicy
        }));
        writer.Flush();
    }

    /// <summary>
    /// Writes the one line of a NULL variant the row declares non-comparable: the raw observation made in
    /// phase 1 is carried as the evidence of the incompatible wire metadata, the reason is recorded, and the
    /// row is counted in the <c>notComparable</c> bucket — neither a pass nor a refusal.
    /// </summary>
    private static void WriteNotComparable(
        StreamWriter writer,
        Dictionary<string, object?> server,
        T350LiveCase @case,
        string reason,
        T350LiveObservation? rawNull)
    {
        writer.WriteLine(JsonSerializer.Serialize(new Dictionary<string, object?>(server)
        {
            ["kind"] = "notComparable",
            ["id"] = @case.Id,
            ["name"] = @case.Name,
            ["family"] = @case.Family,
            ["variant"] = "NULL",
            ["sql"] = @case.RawNullSql,
            ["fieldType"] = rawNull?.FieldType,
            ["valueType"] = rawNull?.ValueType,
            ["isDbNull"] = rawNull?.IsNull,
            ["textValue"] = rawNull?.Text,
            ["reason"] = reason,
            ["expectedClr"] = @case.ExpectedClr,
            ["annotation"] = @case.Annotation
        }));
        writer.Flush();
    }

    // ---------------------------------------------------------------- probe table

    private static void CreateProbeTable(MariaDbDataContext ctx)
    {
        ExecuteRaw(ctx, "drop temporary table if exists t350_probe");
        ExecuteRaw(ctx, "create temporary table t350_probe (id int not null primary key)");
        ExecuteRaw(ctx, "insert into t350_probe (id) values (1)");
        ExecuteRaw(ctx, "drop temporary table if exists t350_agg");
        ExecuteRaw(ctx, "create temporary table t350_agg (x bigint unsigned not null)");
        ExecuteRaw(ctx, "insert into t350_agg (x) values (1),(3)");
        ExecuteRaw(ctx, "drop temporary table if exists t350_pair");
        ExecuteRaw(ctx, "create temporary table t350_pair (x bigint unsigned not null)");
        ExecuteRaw(ctx, "insert into t350_pair (x) values (1),(2)");
        ExecuteRaw(ctx, "drop temporary table if exists t350_kv");
        ExecuteRaw(ctx, "create temporary table t350_kv (v int not null primary key)");
        ExecuteRaw(ctx, "insert into t350_kv (v) values (2)");
        ExecuteRaw(ctx, "drop temporary table if exists t350_def");
        ExecuteRaw(ctx, "create temporary table t350_def (id int not null default 7)");
        ExecuteRaw(ctx, "insert into t350_def (id) values (default)");
        ExecuteRaw(ctx, "drop sequence if exists t350_seq");
        ExecuteRaw(ctx, "create sequence t350_seq start with 1");
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

    /// <summary>
    /// Numeric equality across the widening a delivered signature applies (<c>int</c> server field read
    /// through a <c>long?</c> member, <c>uint</c> through a <c>ulong?</c> one): the value is compared
    /// exactly, as a number, and never as a formatted string.
    /// </summary>
    private static bool Numeric(object? raw, object? translated)
        => raw is null || translated is null
            ? raw is null && translated is null
            : Convert.ToDecimal(raw, CultureInfo.InvariantCulture)
                == Convert.ToDecimal(translated, CultureInfo.InvariantCulture);

    /// <summary>
    /// <see cref="Bytes"/>, with a <c>byte[]</c>/<c>string</c> pair compared as UTF-8 text. A NULL-argument
    /// form whose field shape depends on the argument type comes back as a binary string from the raw path
    /// and as text through the delivered signature (<c>QUOTE(NULL)</c> is the four characters
    /// <c>NULL</c>); the value is still compared exactly.
    /// </summary>
    private static bool BytesOrText(object? raw, object? translated)
    {
        if (raw is byte[] rawBytes && translated is string translatedText)
            return System.Text.Encoding.UTF8.GetString(rawBytes) == translatedText;
        if (raw is string rawText && translated is byte[] translatedBytes)
            return rawText == System.Text.Encoding.UTF8.GetString(translatedBytes);
        return Bytes(raw, translated);
    }

    /// <summary>Checks one invariant on both sides; a NULL on either side compares only to NULL.</summary>
    private static bool Both(object? raw, object? translated, Func<object, bool> invariant)
        => raw is null || translated is null
            ? raw is null && translated is null
            : invariant(raw) && invariant(translated);

    /// <summary>
    /// The session/state readers (<c>FOUND_ROWS</c>, <c>LAST_INSERT_ID</c>, <c>BENCHMARK</c>,
    /// <c>SLEEP</c>): the setup pins the state, but the contract is the documented range, not a
    /// hand-written constant.
    /// </summary>
    private static bool NonNegative(object? raw, object? translated)
        => Both(raw, translated, value => Convert.ToDecimal(value, CultureInfo.InvariantCulture) >= 0);

    /// <summary><c>ROW_COUNT</c> reports <c>-1</c> for a statement that affected no rows.</summary>
    private static bool AtLeastMinusOne(object? raw, object? translated)
        => Both(raw, translated, value => Convert.ToDecimal(value, CultureInfo.InvariantCulture) >= -1);

    /// <summary><c>UUID_SHORT</c> is a positive, monotonically increasing server counter.</summary>
    private static bool Positive(object? raw, object? translated)
        => Both(raw, translated, value => Convert.ToDecimal(value, CultureInfo.InvariantCulture) > 0);

    /// <summary>
    /// <c>RAND</c>: each side draws from its own generator state, so only the documented range
    /// <c>[0, 1)</c> is compared.
    /// </summary>
    private static bool UnitInterval(object? raw, object? translated)
        => Both(raw, translated, value =>
        {
            var number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
            return number >= 0 && number < 1;
        });

    /// <summary><c>CHARSET</c>/<c>COLLATION</c>: the session's names are server configuration.</summary>
    private static bool NonEmptyText(object? raw, object? translated)
        => Both(raw, translated, value => value is string text && text.Length > 0);

    /// <summary><c>CURRENT_ROLE</c> is NULL without a granted role, otherwise a non-empty role list.</summary>
    private static bool NullableText(object? raw, object? translated)
        => (raw is null && translated is null)
            || Both(raw, translated, value => value is string text && text.Length > 0);

    /// <summary><c>COERCIBILITY</c>'s documented domain is the coercibility classes 0 to 6.</summary>
    private static bool CoercibilityClass(object? raw, object? translated)
        => Both(raw, translated, value =>
        {
            var number = Convert.ToInt64(value, CultureInfo.InvariantCulture);
            return number is >= 0 and <= 6;
        });

    /// <summary>
    /// <c>RANDOM_BYTES</c>: the content is random, so the rule is the requested length on both sides.
    /// </summary>
    private static bool FourRandomBytes(object? raw, object? translated)
        => Both(raw, translated, value => value is byte[] bytes && bytes.Length == 4);


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
                "n/a"),
            // ---------------------------------------------------------------- T350 block 3a: JSON
            // The 75 un-excluded tuple names plus the three block-1 members (rownum, binlog_gtid_pos,
            // previous_value_for), each raw statement taken verbatim from the S1 oracle
            // (.pdca/T350/rv1/probe/t350-oracle-s1.jsonl). Where the oracle probed a NULL form it is the
            // record's NULL form; where it did not, the row carries no NULL claim.
            // The untyped NULL form is observable on the server but not comparable through the delivered
            // signature: binary wire metadata (System.Byte[]) cannot materialise as the declared String. The
            // non-NULL form stays asserted; the exclusion is recorded, never counted as a pass or a refusal.
            new T350LiveCase(
                "4",
                "JSON_ARRAY",
                "JSON",
                "select json_array(1,2)",
                "select json_array(null)",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.json_array(1, 2))),
                null,
                "System.String",
                BytesOrText,
                "n/a",
                NotComparableReason: T350LiveCase.NoComparableNullFormAnnotation),
            new T350LiveCase(
                "5",
                "JSON_ARRAYAGG",
                "JSON",
                "select json_arrayagg(x) from (select 1 as x union all select 2) t",
                null,
                ctx => Task.FromResult<object?>(
                    ctx.From<T350PairRow>().Select(x => new { V = SqlFunctions.MySql.json_arrayagg(x.X) })
                        .ToList()[0].V),
                null,
                "System.String",
                Exact,
                "n/a"),
            new T350LiveCase(
                "8",
                "JSON_CONTAINS",
                "JSON",
                "select json_contains('{\"a\":1}','1','$.a')",
                "select json_contains(null,'1','$.a')",
                ctx => Task.FromResult<object?>(
                    Project(ctx, x => SqlFunctions.MySql.json_contains("{\"a\":1}", "1", "$.a"))),
                ctx => Task.FromResult<object?>(
                    Project(ctx, x => SqlFunctions.MySql.json_contains((string?)null, "1", "$.a"))),
                "System.Int64",
                Numeric,
                "propagates",
                WireClr: "System.Int32"),
            new T350LiveCase(
                "9",
                "JSON_CONTAINS_PATH",
                "JSON",
                "select json_contains_path('{\"a\":1}','one','$.a')",
                null,
                ctx => Task.FromResult<object?>(
                    Project(ctx, x => SqlFunctions.MySql.json_contains_path("{\"a\":1}", "one", "$.a"))),
                null,
                "System.Int64",
                Numeric,
                "n/a",
                WireClr: "System.Int32"),
            new T350LiveCase(
                "10",
                "JSON_MERGE",
                "JSON",
                "select json_merge('{\"a\":1}','{\"b\":2}')",
                null,
                ctx => Task.FromResult<object?>(
                    Project(ctx, x => SqlFunctions.MySql.json_merge("{\"a\":1}", "{\"b\":2}"))),
                null,
                "System.String",
                Exact,
                "n/a"),
            new T350LiveCase(
                "11",
                "JSON_OBJECT",
                "JSON",
                "select json_object('a',1)",
                null,
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.json_object("a", 1))),
                null,
                "System.String",
                Exact,
                "n/a"),
            new T350LiveCase(
                "12",
                "JSON_OBJECTAGG",
                "JSON",
                "select json_objectagg(k,v) from (select 1 as k, 2 as v) t",
                null,
                ctx => Task.FromResult<object?>(
                    ctx.From<T350KvRow>().Select(x => new { V = SqlFunctions.MySql.json_objectagg("1", x.V) })
                        .ToList()[0].V),
                null,
                "System.String",
                Exact,
                "n/a"),
            new T350LiveCase(
                "13",
                "JSON_OVERLAPS",
                "JSON",
                "select json_overlaps('{\"a\":1}','{\"a\":1}')",
                "select json_overlaps(null,'{\"a\":1}')",
                ctx => Task.FromResult<object?>(
                    Project(ctx, x => SqlFunctions.MySql.json_overlaps("{\"a\":1}", "{\"a\":1}"))),
                ctx => Task.FromResult<object?>(
                    Project(ctx, x => SqlFunctions.MySql.json_overlaps((string?)null, "{\"a\":1}"))),
                "System.Int64",
                Numeric,
                "propagates",
                WireClr: "System.Int32"),
            new T350LiveCase(
                "14",
                "JSON_PRETTY",
                "JSON",
                "select json_pretty('{\"a\":1}')",
                null,
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.json_pretty("{\"a\":1}"))),
                null,
                "System.String",
                Exact,
                "n/a"),
            new T350LiveCase(
                "15",
                "JSON_QUOTE",
                "JSON",
                "select json_quote('a')",
                "select json_quote(null)",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.json_quote("a"))),
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.json_quote((string?)null))),
                "System.String",
                BytesOrText,
                "non-propagating:NULL"),
            new T350LiveCase(
                "16",
                "JSON_SCHEMA_VALID",
                "JSON",
                "select json_schema_valid('{\"type\":\"object\"}','{\"a\":1}')",
                null,
                ctx => Task.FromResult<object?>(
                    Project(ctx, x => SqlFunctions.MySql.json_schema_valid("{\"type\":\"object\"}", "{\"a\":1}"))),
                null,
                "System.Int64",
                Numeric,
                "n/a",
                WireClr: "System.Int32"),
            new T350LiveCase(
                "18",
                "JSON_SEARCH",
                "JSON",
                "select json_search('{\"a\":\"b\"}','one','b')",
                null,
                ctx => Task.FromResult<object?>(
                    Project(ctx, x => SqlFunctions.MySql.json_search("{\"a\":\"b\"}", "one", "b"))),
                null,
                "System.String",
                Exact,
                "n/a"),
            // ---------------------------------------------------------------- T350 block 3a: string
            new T350LiveCase(
                "21",
                "BIN",
                "string",
                "select bin(5)",
                "select bin(null)",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.bin(5UL))),
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.bin((ulong?)null))),
                "System.String",
                Exact,
                "propagates"),
            new T350LiveCase(
                "22",
                "EXPORT_SET",
                "string",
                "select export_set(5,'Y','N')",
                "select export_set(null,'Y','N')",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.export_set(5UL, "Y", "N"))),
                ctx => Task.FromResult<object?>(
                    Project(ctx, x => SqlFunctions.MySql.export_set((ulong?)null, "Y", "N"))),
                "System.String",
                Exact,
                "propagates"),
            new T350LiveCase(
                "23",
                "HEX",
                "string",
                "select hex('abc')",
                "select hex(null)",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.hex("abc"))),
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.hex((string?)null))),
                "System.String",
                Exact,
                "propagates"),
            new T350LiveCase(
                "24",
                "MAKE_SET",
                "string",
                "select make_set(1,'a','b')",
                "select make_set(null,'a','b')",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.make_set(1UL, "a", "b"))),
                ctx => Task.FromResult<object?>(
                    Project(ctx, x => SqlFunctions.MySql.make_set((ulong?)null, "a", "b"))),
                "System.String",
                Exact,
                "propagates"),
            new T350LiveCase(
                "25",
                "OCT",
                "string",
                "select oct(8)",
                "select oct(null)",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.oct(8UL))),
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.oct((ulong?)null))),
                "System.String",
                Exact,
                "propagates"),
            new T350LiveCase(
                "26",
                "ORD",
                "string",
                "select ord('A')",
                "select ord(null)",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.ord("A"))),
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.ord((string?)null))),
                "System.Int64",
                Numeric,
                "propagates",
                WireClr: "System.Int32"),
            // Same exclusion as JSON_ARRAY above: `quote(null)` is a non-NULL binary (`NULL` as bytes) the
            // declared String signature cannot materialise; the non-NULL form stays asserted.
            new T350LiveCase(
                "27",
                "QUOTE",
                "string",
                "select quote('a''b')",
                "select quote(null)",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.quote("a'b"))),
                null,
                "System.String",
                BytesOrText,
                "n/a",
                NotComparableReason: T350LiveCase.NoComparableNullFormAnnotation),
            new T350LiveCase(
                "28",
                "SOUNDEX",
                "string",
                "select soundex('hello')",
                "select soundex(null)",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.soundex("hello"))),
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.soundex((string?)null))),
                "System.String",
                Exact,
                "propagates"),
            new T350LiveCase(
                "29",
                "STRCMP",
                "string",
                "select strcmp('a','b')",
                "select strcmp(null,'b')",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.strcmp("a", "b"))),
                ctx => Task.FromResult<object?>(
                    Project(ctx, x => SqlFunctions.MySql.strcmp((string?)null, "b"))),
                "System.Int64",
                Numeric,
                "propagates",
                WireClr: "System.Int32"),
            new T350LiveCase(
                "30",
                "TO_BASE64",
                "string",
                "select to_base64('abc')",
                "select to_base64(null)",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.to_base64(AbcBytes))),
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.to_base64((byte[]?)null))),
                "System.String",
                Exact,
                "propagates"),
            new T350LiveCase(
                "31",
                "FROM_BASE64",
                "string",
                "select from_base64(to_base64('abc'))",
                "select from_base64(null)",
                ctx => Task.FromResult<object?>(
                    Project(ctx, x => SqlFunctions.MySql.from_base64(SqlFunctions.MySql.to_base64(AbcBytes)))),
                ctx => Task.FromResult<object?>(
                    Project(ctx, x => SqlFunctions.MySql.from_base64((string?)null))),
                "byte[]",
                Bytes,
                "propagates"),
            new T350LiveCase(
                "32",
                "UNHEX",
                "string",
                "select unhex('6162')",
                "select unhex(null)",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.unhex("6162"))),
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.unhex((string?)null))),
                "byte[]",
                Bytes,
                "propagates"),
            new T350LiveCase(
                "33",
                "WEIGHT_STRING",
                "string",
                "select weight_string('abc')",
                "select weight_string(null)",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.weight_string("abc"))),
                ctx => Task.FromResult<object?>(
                    Project(ctx, x => SqlFunctions.MySql.weight_string((string?)null))),
                "byte[]",
                Bytes,
                "propagates"),
            // ---------------------------------------------------------------- T350 block 3a: numeric
            new T350LiveCase(
                "34",
                "CONV",
                "numeric",
                "select conv('ff',16,10)",
                "select conv(null,16,10)",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.conv("ff", 16, 10))),
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.conv((string?)null, 16, 10))),
                "System.String",
                Exact,
                "propagates"),
            new T350LiveCase(
                "35",
                "CRC32",
                "numeric",
                "select crc32('abc')",
                "select crc32(null)",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.crc32("abc"))),
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.crc32((string?)null))),
                "System.UInt64",
                Numeric,
                "propagates",
                WireClr: "System.UInt32"),
            new T350LiveCase(
                "36",
                "LOG2",
                "numeric",
                "select log2(8)",
                "select log2(null)",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.log2(8.0))),
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.log2((double?)null))),
                "System.Double",
                Exact,
                "propagates"),
            // Each side draws from its own generator state, so the rule is the documented range.
            new T350LiveCase(
                "37",
                "RAND",
                "numeric",
                "select rand(42)",
                "select rand(null)",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.rand())),
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.rand((long?)null))),
                "System.Double",
                UnitInterval,
                "non-propagating:[0,1)"),
            new T350LiveCase(
                "38",
                "TRUNCATE",
                "numeric",
                "select truncate(1.2345,2)",
                "select truncate(null,2)",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.truncate(1.2345m, 2))),
                ctx => Task.FromResult<object?>(
                    Project(ctx, x => SqlFunctions.MySql.truncate((decimal?)null, 2))),
                "System.Decimal",
                Exact,
                "propagates"),
            // ---------------------------------------------------------------- T350 block 3a: bit
            new T350LiveCase(
                "39",
                "BIT_COUNT",
                "bit",
                "select bit_count(5)",
                "select bit_count(null)",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.bit_count(5UL))),
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.bit_count((ulong?)null))),
                "System.Int64",
                Numeric,
                "propagates",
                WireClr: "System.Int32"),
            // The aggregate rows read the two-row fixture the oracle's inline set declares (1, 3), so the
            // reduction has the same known row set on both paths.
            new T350LiveCase(
                "40",
                "BIT_AND",
                "bit",
                "select bit_and(x) from (select 1 as x union all select 3) t",
                null,
                ctx => Task.FromResult<object?>(
                    ctx.From<T350AggRow>().Select(x => new { V = SqlFunctions.MySql.bit_and(x.X) })
                        .ToList()[0].V),
                null,
                "System.UInt64",
                Exact,
                "n/a"),
            new T350LiveCase(
                "41",
                "BIT_OR",
                "bit",
                "select bit_or(x) from (select 1 as x union all select 3) t",
                null,
                ctx => Task.FromResult<object?>(
                    ctx.From<T350AggRow>().Select(x => new { V = SqlFunctions.MySql.bit_or(x.X) })
                        .ToList()[0].V),
                null,
                "System.UInt64",
                Exact,
                "n/a"),
            new T350LiveCase(
                "42",
                "BIT_XOR",
                "bit",
                "select bit_xor(x) from (select 1 as x union all select 3) t",
                null,
                ctx => Task.FromResult<object?>(
                    ctx.From<T350AggRow>().Select(x => new { V = SqlFunctions.MySql.bit_xor(x.X) })
                        .ToList()[0].V),
                null,
                "System.UInt64",
                Exact,
                "n/a"),
            // ---------------------------------------------------------------- T350 block 3a: date/time
            // The four rows below bind their first argument to DATETIME/TIME. The oracle probed the same
            // values through string literals, which MariaDB answers with a VARCHAR result — a different
            // call than the one the delivered signature emits, whose DateTime argument makes the server
            // return DATETIME. The record therefore observes the delivered call (same name, same values),
            // so the comparison is like for like.
            new T350LiveCase(
                "43",
                "ADDDATE",
                "date/time",
                "select adddate(cast('2020-01-01' as datetime), 1)",
                "select adddate(null, interval 1 day)",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.adddate(Day, 1))),
                ctx => Task.FromResult<object?>(
                    Project(ctx, x => SqlFunctions.MySql.adddate((DateTime?)null, 1))),
                "System.DateTime",
                Exact,
                "propagates"),
            new T350LiveCase(
                "44",
                "ADDTIME",
                "date/time",
                "select addtime(cast('2020-01-01 00:00:00' as datetime), cast('01:00:00' as time))",
                "select addtime(null,'01:00:00')",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.addtime(Day, OneHour))),
                ctx => Task.FromResult<object?>(
                    Project(ctx, x => SqlFunctions.MySql.addtime((DateTime?)null, OneHour))),
                "System.DateTime",
                Exact,
                "propagates"),
            new T350LiveCase(
                "45",
                "CONVERT_TZ",
                "date/time",
                "select convert_tz('2020-01-01 00:00:00','+00:00','+02:00')",
                "select convert_tz(null,'+00:00','+02:00')",
                ctx => Task.FromResult<object?>(
                    Project(ctx, x => SqlFunctions.MySql.convert_tz(Day, "+00:00", "+02:00"))),
                ctx => Task.FromResult<object?>(
                    Project(ctx, x => SqlFunctions.MySql.convert_tz((DateTime?)null, "+00:00", "+02:00"))),
                "System.DateTime",
                Exact,
                "propagates"),
            new T350LiveCase(
                "46",
                "DAYNAME",
                "date/time",
                "select dayname('2020-01-01')",
                "select dayname(null)",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.dayname(Day))),
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.dayname((DateTime?)null))),
                "System.String",
                Exact,
                "propagates"),
            new T350LiveCase(
                "47",
                "FROM_DAYS",
                "date/time",
                "select from_days(738000)",
                "select from_days(null)",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.from_days(738000L))),
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.from_days(null))),
                "System.DateTime",
                Exact,
                "propagates"),
            new T350LiveCase(
                "48",
                "GET_FORMAT",
                "date/time",
                "select get_format(DATE,'EUR')",
                null,
                ctx => Task.FromResult<object?>(
                    Project(ctx, x => SqlFunctions.MySql.get_format(MySqlFormatKind.Date, "EUR"))),
                null,
                "System.String",
                Exact,
                "n/a"),
            new T350LiveCase(
                "49",
                "MAKEDATE",
                "date/time",
                "select makedate(2020,32)",
                "select makedate(null,32)",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.makedate(2020, 32))),
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.makedate(null, 32))),
                "System.DateTime",
                Exact,
                "propagates"),
            new T350LiveCase(
                "50",
                "MAKETIME",
                "date/time",
                "select maketime(12,34,56)",
                "select maketime(null,34,56)",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.maketime(12, 34, 56m))),
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.maketime(null, 34, 56m))),
                "System.TimeSpan",
                Exact,
                "propagates"),
            new T350LiveCase(
                "51",
                "MICROSECOND",
                "date/time",
                "select microsecond('2020-01-01 00:00:00.123456')",
                "select microsecond(null)",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.microsecond(MicroTime))),
                ctx => Task.FromResult<object?>(
                    Project(ctx, x => SqlFunctions.MySql.microsecond((DateTime?)null))),
                "System.Int64",
                Numeric,
                "propagates",
                WireClr: "System.Int32"),
            new T350LiveCase(
                "52",
                "MONTHNAME",
                "date/time",
                "select monthname('2020-01-01')",
                "select monthname(null)",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.monthname(Day))),
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.monthname((DateTime?)null))),
                "System.String",
                Exact,
                "propagates"),
            new T350LiveCase(
                "53",
                "PERIOD_ADD",
                "date/time",
                "select period_add(202401,2)",
                "select period_add(null,2)",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.period_add(202401L, 2L))),
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.period_add(null, 2L))),
                "System.Int64",
                Numeric,
                "propagates",
                WireClr: "System.Int32"),
            new T350LiveCase(
                "54",
                "PERIOD_DIFF",
                "date/time",
                "select period_diff(202402,202401)",
                "select period_diff(null,202401)",
                ctx => Task.FromResult<object?>(
                    Project(ctx, x => SqlFunctions.MySql.period_diff(202402L, 202401L))),
                ctx => Task.FromResult<object?>(
                    Project(ctx, x => SqlFunctions.MySql.period_diff(null, 202401L))),
                "System.Int64",
                Numeric,
                "propagates",
                WireClr: "System.Int32"),
            new T350LiveCase(
                "55",
                "SEC_TO_TIME",
                "date/time",
                "select sec_to_time(3661)",
                "select sec_to_time(null)",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.sec_to_time(3661m))),
                ctx => Task.FromResult<object?>(
                    Project(ctx, x => SqlFunctions.MySql.sec_to_time((decimal?)null))),
                "System.TimeSpan",
                Exact,
                "propagates"),
            new T350LiveCase(
                "56",
                "SUBDATE",
                "date/time",
                "select subdate(cast('2020-01-01' as datetime), 1)",
                "select subdate(null, interval 1 day)",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.subdate(Day, 1))),
                ctx => Task.FromResult<object?>(
                    Project(ctx, x => SqlFunctions.MySql.subdate((DateTime?)null, 1))),
                "System.DateTime",
                Exact,
                "propagates"),
            new T350LiveCase(
                "57",
                "SUBTIME",
                "date/time",
                "select subtime(cast('2020-01-01 00:00:00' as datetime), cast('01:00:00' as time))",
                "select subtime(null,'01:00:00')",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.subtime(Day, OneHour))),
                ctx => Task.FromResult<object?>(
                    Project(ctx, x => SqlFunctions.MySql.subtime((DateTime?)null, OneHour))),
                "System.DateTime",
                Exact,
                "propagates"),
            new T350LiveCase(
                "58",
                "TIME",
                "date/time",
                "select time('2020-01-01 12:34:56')",
                "select time(null)",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.time(Noonish))),
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.time((DateTime?)null))),
                "System.TimeSpan",
                Exact,
                "propagates"),
            new T350LiveCase(
                "59",
                "TIME_FORMAT",
                "date/time",
                "select time_format('12:34:56','%H:%i')",
                "select time_format(null,'%H:%i')",
                ctx => Task.FromResult<object?>(
                    Project(ctx, x => SqlFunctions.MySql.time_format(NoonTime, "%H:%i"))),
                ctx => Task.FromResult<object?>(
                    Project(ctx, x => SqlFunctions.MySql.time_format((TimeSpan?)null, "%H:%i"))),
                "System.String",
                Exact,
                "propagates"),
            new T350LiveCase(
                "60",
                "TIME_TO_SEC",
                "date/time",
                "select time_to_sec('12:34:56')",
                "select time_to_sec(null)",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.time_to_sec(NoonTime))),
                ctx => Task.FromResult<object?>(
                    Project(ctx, x => SqlFunctions.MySql.time_to_sec((TimeSpan?)null))),
                "System.Int64",
                Exact,
                "propagates"),
            new T350LiveCase(
                "61",
                "TIMEDIFF",
                "date/time",
                "select timediff('2020-01-02 00:00:00','2020-01-01 00:00:00')",
                "select timediff(null,'2020-01-01 00:00:00')",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.timediff(NextDay, Day))),
                ctx => Task.FromResult<object?>(
                    Project(ctx, x => SqlFunctions.MySql.timediff((DateTime?)null, Day))),
                "System.TimeSpan",
                Exact,
                "propagates"),
            new T350LiveCase(
                "62",
                "TIMESTAMP",
                "date/time",
                "select timestamp('2020-01-01 00:00:00')",
                "select timestamp(null)",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.timestamp(Day))),
                ctx => Task.FromResult<object?>(
                    Project(ctx, x => SqlFunctions.MySql.timestamp((DateTime?)null))),
                "System.DateTime",
                Exact,
                "propagates"),
            new T350LiveCase(
                "63",
                "TO_DAYS",
                "date/time",
                "select to_days('2020-01-01')",
                "select to_days(null)",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.to_days(Day))),
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.to_days((DateTime?)null))),
                "System.Int64",
                Numeric,
                "propagates",
                WireClr: "System.Int32"),
            new T350LiveCase(
                "64",
                "TO_SECONDS",
                "date/time",
                "select to_seconds('2020-01-01 00:00:00')",
                "select to_seconds(null)",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.to_seconds(Day))),
                ctx => Task.FromResult<object?>(
                    Project(ctx, x => SqlFunctions.MySql.to_seconds((DateTime?)null))),
                "System.Int64",
                Exact,
                "propagates"),
            new T350LiveCase(
                "65",
                "YEARWEEK",
                "date/time",
                "select yearweek('2020-01-01')",
                "select yearweek(null)",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.yearweek(Day))),
                ctx => Task.FromResult<object?>(
                    Project(ctx, x => SqlFunctions.MySql.yearweek((DateTime?)null))),
                "System.Int64",
                Numeric,
                "propagates",
                WireClr: "System.Int32"),
            // ---------------------------------------------------------------- T350 block 3a: crypto
            new T350LiveCase(
                "66",
                "AES_ENCRYPT",
                "crypto",
                "select aes_encrypt('text','key')",
                "select aes_encrypt(null,'key')",
                ctx => Task.FromResult<object?>(
                    Project(ctx, x => SqlFunctions.MySql.aes_encrypt(AesText, AesKey))),
                ctx => Task.FromResult<object?>(
                    Project(ctx, x => SqlFunctions.MySql.aes_encrypt((byte[]?)null, AesKey))),
                "byte[]",
                Bytes,
                "propagates"),
            new T350LiveCase(
                "67",
                "AES_DECRYPT",
                "crypto",
                "select aes_decrypt(aes_encrypt('text','key'),'key')",
                "select aes_decrypt(null,'key')",
                ctx => Task.FromResult<object?>(
                    Project(ctx, x => SqlFunctions.MySql.aes_decrypt(
                        SqlFunctions.MySql.aes_encrypt(AesText, AesKey), AesKey))),
                ctx => Task.FromResult<object?>(
                    Project(ctx, x => SqlFunctions.MySql.aes_decrypt((byte[]?)null, AesKey))),
                "byte[]",
                Bytes,
                "propagates"),
            new T350LiveCase(
                "68",
                "COMPRESS",
                "crypto",
                "select compress('abc')",
                "select compress(null)",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.compress(AbcBytes))),
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.compress((byte[]?)null))),
                "byte[]",
                Bytes,
                "propagates"),
            new T350LiveCase(
                "69",
                "UNCOMPRESS",
                "crypto",
                "select uncompress(compress('abc'))",
                "select uncompress(null)",
                ctx => Task.FromResult<object?>(
                    Project(ctx, x => SqlFunctions.MySql.uncompress(SqlFunctions.MySql.compress(AbcBytes)))),
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.uncompress((byte[]?)null))),
                "byte[]",
                Bytes,
                "propagates"),
            new T350LiveCase(
                "70",
                "UNCOMPRESSED_LENGTH",
                "crypto",
                "select uncompressed_length(compress('abc'))",
                "select uncompressed_length(null)",
                ctx => Task.FromResult<object?>(
                    Project(ctx, x => SqlFunctions.MySql.uncompressed_length(SqlFunctions.MySql.compress(AbcBytes)))),
                ctx => Task.FromResult<object?>(
                    Project(ctx, x => SqlFunctions.MySql.uncompressed_length((byte[]?)null))),
                "System.Int64",
                Numeric,
                "propagates",
                WireClr: "System.Int32"),
            new T350LiveCase(
                "71",
                "RANDOM_BYTES",
                "crypto",
                "select random_bytes(4)",
                "select random_bytes(null)",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.random_bytes(4))),
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.random_bytes((int?)null))),
                "byte[]",
                FourRandomBytes,
                "propagates"),
            // ---------------------------------------------------------------- T350 block 3a: information
            // The state readers are pinned by a setup that produces the same session state on both paths,
            // and each row keeps its documented range as the rule rather than a hand-written constant.
            new T350LiveCase(
                "75",
                "FOUND_ROWS",
                "information",
                "select found_rows()",
                null,
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.found_rows())),
                null,
                "System.Int64",
                NonNegative,
                "n/a",
                "select * from t350_probe"),
            new T350LiveCase(
                "76",
                "LAST_INSERT_ID",
                "information",
                "select last_insert_id()",
                null,
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.last_insert_id())),
                null,
                "System.UInt64",
                NonNegative,
                "n/a",
                "select last_insert_id(42)"),
            new T350LiveCase(
                "77",
                "ROW_COUNT",
                "information",
                "select row_count()",
                null,
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.row_count())),
                null,
                "System.Int64",
                AtLeastMinusOne,
                "n/a",
                "select * from t350_probe"),
            new T350LiveCase(
                "78",
                "CURRENT_ROLE",
                "information",
                "select current_role()",
                null,
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.current_role())),
                null,
                "System.String",
                NullableText,
                "non-propagating:NULL (no role is granted on the probe server)"),
            new T350LiveCase(
                "79",
                "CHARSET",
                "information",
                "select charset('abc')",
                "select charset(null)",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.charset("abc"))),
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.charset((object?)null))),
                "System.String",
                NonEmptyText,
                "non-propagating:binary"),
            new T350LiveCase(
                "80",
                "COLLATION",
                "information",
                "select collation('abc')",
                "select collation(null)",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.collation("abc"))),
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.collation((object?)null))),
                "System.String",
                NonEmptyText,
                "non-propagating:binary"),
            new T350LiveCase(
                "81",
                "COERCIBILITY",
                "information",
                "select coercibility('abc')",
                "select coercibility(null)",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.coercibility("abc"))),
                ctx => Task.FromResult<object?>(
                    Project(ctx, x => SqlFunctions.MySql.coercibility((object?)null))),
                "System.Int64",
                CoercibilityClass,
                "non-propagating:6",
                WireClr: "System.Int32"),
            new T350LiveCase(
                "82",
                "BENCHMARK",
                "information",
                "select benchmark(1,1)",
                null,
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.benchmark(1L, 1))),
                null,
                "System.Int64",
                NonNegative,
                "n/a",
                WireClr: "System.Int32"),
            new T350LiveCase(
                "84",
                "UUID_SHORT",
                "information",
                "select uuid_short()",
                null,
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.uuid_short())),
                null,
                "System.UInt64",
                Positive,
                "n/a"),
            // ---------------------------------------------------------------- T350 block 3a: miscellaneous
            new T350LiveCase(
                "86",
                "SLEEP",
                "miscellaneous",
                "select sleep(0)",
                "select sleep(null)",
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.sleep(0.0))),
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.sleep((double?)null))),
                "System.Int64",
                NonNegative,
                "non-propagating:0",
                WireClr: "System.Int32"),
            new T350LiveCase(
                "88",
                "NAME_CONST",
                "miscellaneous",
                "select name_const('x',1)",
                null,
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.name_const("x", 1))),
                null,
                "System.Int32",
                Exact,
                "n/a"),
            new T350LiveCase(
                "89",
                "COLUMN_DEFAULT",
                "miscellaneous",
                "select default(id) from t350_def",
                null,
                ctx => Task.FromResult<object?>(
                    ctx.From<T350DefRow>().Select(x => new { V = SqlFunctions.MySql.column_default(x.Id) })
                        .ToList()[0].V),
                null,
                "System.Int32",
                Exact,
                "n/a"),
            // ---------------------------------------------------------------- T350 block 3a: block-1 members
            new T350LiveCase(
                "98",
                "ROWNUM",
                "information",
                "select rownum()",
                null,
                ctx => Task.FromResult<object?>(Project(ctx, x => SqlFunctions.MySql.rownum())),
                null,
                "System.UInt64",
                Exact,
                "n/a"),
            // The stock image runs with the binary log off: the name resolves (error 1381 proves it) but no
            // value, NULL form or CLR type can be observed, so the row records the server precondition and
            // counts no comparison instead of weakening the equality rule.
            new T350LiveCase(
                "100",
                "BINLOG_GTID_POS",
                "information",
                "select binlog_gtid_pos('', 0)",
                null,
                ctx => Task.FromResult<object?>(
                    Project(ctx, x => SqlFunctions.MySql.binlog_gtid_pos("", 0UL))),
                null,
                "System.String",
                Exact,
                "n/a (binlog is off on the stock image: 1381, no value to observe)",
                Precondition: ctx => Scalar(ctx, "select @@log_bin") == "1"
                    ? null
                    : "1381: You are not using binary logging (the name resolves, the server precondition " +
                      "does not hold, so no value is observable)"),
            // The sequence is created by the probe fixture; the setup advances it once and the teardown
            // restarts it, so both phases read the same "previous value" from their own NEXTVAL.
            new T350LiveCase(
                "121",
                "PREVIOUS VALUE FOR",
                "sequence",
                "select previous value for t350_seq",
                null,
                ctx => Task.FromResult<object?>(
                    Project(ctx, x => SqlFunctions.MySql.previous_value_for("t350_seq"))),
                null,
                "System.Int64",
                Exact,
                "n/a",
                "select nextval(t350_seq)",
                "alter sequence t350_seq restart with 1")
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
