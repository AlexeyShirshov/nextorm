using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using FluentAssertions;
using MySqlConnector;
using NextORM.Core;
using NextORM.MySql;

namespace NextORM.Integration.Tests;

/// <summary>
/// T349 (issue #283) diagnostic probe: executes every raw-oracle case from
/// <see cref="T349MySqlFunctionCases"/> on one MySQL 8.4 connection and records server metadata,
/// the connector field type, the materialised CLR value type and any error. It asserts nothing per
/// name — the matrix assertions arrive later; the only assertions here are structural (the oracle
/// file exists and carries every inventory case). The artifact is flushed after each case so a
/// crash still leaves partial evidence.
/// </summary>
[Trait("Category", "Diagnostic")]
public sealed class T349MySqlFunctionMatrixTests
{
    private const string DefaultOraclePath = "/tmp/opencode/t349-oracle.jsonl";

    /// <summary>The typed date/time literal of the temporal probe.</summary>
    private static readonly DateTime Moment = new(2020, 1, 15, 12, 34, 56);

    /// <summary>A fractional typed duration (<c>12:34:56.789</c>).</summary>
    private static readonly TimeSpan FractionalDuration = new(0, 12, 34, 56, 789);

    /// <summary>An integral typed duration (<c>12:34:56</c>).</summary>
    private static readonly TimeSpan IntegralDuration = new(0, 12, 34, 56);

    [Fact, Trait("Category", "Diagnostic")]
    public void T349Probe_ShouldRecordRawOracleAndClrTypes()
    {
        Assert.SkipUnless(MySqlContainer.IsAvailable, MySqlContainer.Failure ?? "MySQL is not available.");

        var oraclePath = Environment.GetEnvironmentVariable("T349_ORACLE_PATH") is { Length: > 0 } configured
            ? configured
            : DefaultOraclePath;
        var directory = Path.GetDirectoryName(oraclePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        using var connection = new MySqlConnection(MySqlContainer.ConnectionString);
        connection.Open();

        var cases = T349MySqlFunctionCases.All;
        using var writer = new StreamWriter(new FileStream(oraclePath, FileMode.Create, FileAccess.Write, FileShare.Read))
        {
            AutoFlush = true
        };

        WriteLine(writer, new Dictionary<string, object?>
        {
            ["kind"] = "environment",
            ["serverVersion"] = Scalar(connection, "SELECT VERSION()", out var versionError),
            ["versionError"] = versionError,
            ["sqlMode"] = Scalar(connection, "SELECT @@SESSION.sql_mode", out _),
            ["charset"] = Scalar(connection, "SELECT @@character_set_connection", out _),
            ["collation"] = Scalar(connection, "SELECT @@collation_connection", out _),
            ["timeZone"] = Scalar(connection, "SELECT @@SESSION.time_zone", out _),
            ["mysqlTimeZoneRows"] = Scalar(connection, "SELECT COUNT(*) FROM mysql.time_zone_name", out var zoneError),
            ["mysqlTimeZoneError"] = zoneError,
            ["validatePasswordComponent"] = PasswordComponentState(connection, out var passwordNote),
            ["validatePasswordNote"] = passwordNote,
            ["caseCount"] = cases.Length
        });

        var fieldType = string.Empty;
        var valueType = string.Empty;

        foreach (var @case in cases)
        {
            if (@case.SetupSql is { Length: > 0 } setup)
                RunNonQuery(connection, setup);

            try
            {
                if (@case.RawSql.Length == 0)
                {
                    var refusalExtras = new List<Dictionary<string, object?>>();
                    foreach (var extra in @case.ExtraRawSql)
                        refusalExtras.Add(ToRecord(Observe(connection, extra)));

                    WriteLine(writer, new Dictionary<string, object?>
                    {
                        ["id"] = @case.Id,
                        ["name"] = @case.Name,
                        ["family"] = @case.Family,
                        ["disposition"] = @case.Disposition,
                        ["sql"] = @case.RawSql,
                        ["skipped"] = "refusal without a scalar raw form",
                        ["extras"] = refusalExtras,
                        ["notes"] = @case.Notes
                    });
                    continue;
                }

                var observation = Observe(connection, @case.RawSql);
                fieldType = observation.FieldType ?? "-";
                valueType = observation.ValueType ?? "-";

                var extras = new List<Dictionary<string, object?>>();
                foreach (var extra in @case.ExtraRawSql)
                    extras.Add(ToRecord(Observe(connection, extra)));

                WriteLine(writer, new Dictionary<string, object?>
                {
                    ["id"] = @case.Id,
                    ["name"] = @case.Name,
                    ["family"] = @case.Family,
                    ["disposition"] = @case.Disposition,
                    ["sql"] = @case.RawSql,
                    ["connectorFieldType"] = observation.FieldType,
                    ["valueType"] = observation.ValueType,
                    ["isDbNull"] = observation.IsNull,
                    ["textValue"] = observation.Text,
                    ["error"] = observation.Error,
                    ["durationMs"] = observation.DurationMs,
                    ["extras"] = extras,
                    ["notes"] = @case.Notes
                });

                Console.WriteLine(
                    $"{@case.Id} | {@case.Name} | {fieldType} | {valueType} | {observation.IsNull} | {observation.Error}");
            }
            finally
            {
                if (@case.TeardownSql is { Length: > 0 } teardown)
                    RunNonQuery(connection, teardown);
            }
        }

        File.Exists(oraclePath).Should().BeTrue();
        var lines = File.ReadAllLines(oraclePath);
        lines.Length.Should().BeGreaterThanOrEqualTo(cases.Length + 1);
        lines.Length.Should().BeGreaterThanOrEqualTo(92, "the oracle carries one environment record plus the 91 inventory names");
    }

    /// <summary>
    /// T349 (b2) typed-input probe: evaluates the temporal functions through the production path
    /// (<c>ctx.GetPreparedQueryCommand</c> with typed CLR constants) and compares each result with the
    /// raw-ADO oracle for the equivalent SQL bound with the same typed parameter. The observations are
    /// appended as one JSON line per function so the frozen signatures rest on measured evidence.
    /// </summary>
    [Fact, Trait("Category", "Diagnostic")]
    public void T349TemporalTypedProbe_ShouldMatchRawSqlForTypedArguments()
    {
        Assert.SkipUnless(MySqlContainer.IsAvailable, MySqlContainer.Failure ?? "MySQL is not available.");

        var oraclePath = Environment.GetEnvironmentVariable("T349_ORACLE_PATH") is { Length: > 0 } configured
            ? configured
            : "/tmp/opencode/t349-t2-oracle.jsonl";
        var directory = Path.GetDirectoryName(oraclePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        using var ctx = new MySqlDataContext(MySqlContainer.ConnectionString, new DataContextBuilder());
        ctx.EnsureConnectionOpen();
        ExecuteRaw(ctx, "drop temporary table if exists t349_temporal_probe");
        ExecuteRaw(ctx, "create temporary table t349_temporal_probe (id int not null primary key)");
        ExecuteRaw(ctx, "insert into t349_temporal_probe (id) values (1)");

        var e = ctx.From<IT349TemporalProbe>();
        // Append mode: the b1a oracle is a separate artifact and must never be truncated.
        using var writer = new StreamWriter(
            new FileStream(oraclePath, FileMode.Append, FileAccess.Write, FileShare.Read))
        {
            AutoFlush = true
        };

        var failures = new List<string?>
        {
            Probe(ctx, writer, "adddate",
                "select adddate(@moment, 1)",
                command => command.Parameters.AddWithValue("@moment", Moment),
                e.Select(_ => SqlFunctions.MySql.adddate(Moment, 1)),
                typeof(DateTime)),

            Probe(ctx, writer, "subdate",
                "select subdate(@moment, 1)",
                command => command.Parameters.AddWithValue("@moment", Moment),
                e.Select(_ => SqlFunctions.MySql.subdate(Moment, 1)),
                typeof(DateTime)),

            Probe(ctx, writer, "addtime",
                "select addtime(@moment, @duration)",
                command =>
                {
                    command.Parameters.AddWithValue("@moment", Moment);
                    command.Parameters.AddWithValue("@duration", FractionalDuration);
                },
                e.Select(_ => SqlFunctions.MySql.addtime(Moment, FractionalDuration)),
                typeof(DateTime)),

            Probe(ctx, writer, "subtime",
                "select subtime(@moment, @duration)",
                command =>
                {
                    command.Parameters.AddWithValue("@moment", Moment);
                    command.Parameters.AddWithValue("@duration", FractionalDuration);
                },
                e.Select(_ => SqlFunctions.MySql.subtime(Moment, FractionalDuration)),
                typeof(DateTime)),

            // The integral input pins the whole-second result against the typed raw oracle.
            Probe(ctx, writer, "time_to_sec_integral",
                "select time_to_sec(@duration)",
                command => command.Parameters.AddWithValue("@duration", IntegralDuration),
                e.Select(_ => SqlFunctions.MySql.time_to_sec(IntegralDuration)),
                typeof(long)),

            // parameter binding truncates sub-second precision before TIME_TO_SEC executes (T349 decision, 2026-10-28)
            Probe(ctx, writer, "time_to_sec_fractional",
                "select time_to_sec(@duration)",
                command => command.Parameters.AddWithValue("@duration", FractionalDuration),
                e.Select(_ => SqlFunctions.MySql.time_to_sec(FractionalDuration)),
                typeof(long),
                expectedProductionValue: 45296L,
                rawStringLiteralSql: "select time_to_sec('12:34:56.789')",
                expectedRawStringLiteral: 45296m,
                nominalSeconds: FractionalDuration.TotalSeconds)
        };

        // Every observation is in the artifact before this assertion fails, so a mismatch still leaves
        // the measured values and CLR types on disk.
        failures.Where(failure => failure is not null).Should().BeEmpty(
            "the temporal functions must match the typed raw oracle through the production path");
    }

    private static string? Probe<T>(
        MySqlDataContext ctx,
        StreamWriter writer,
        string name,
        string oracleSql,
        Action<MySqlCommand> bindOracle,
        QueryCommand<T> query,
        Type? expectedOracleType = null,
        object? expectedProductionValue = null,
        string? rawStringLiteralSql = null,
        decimal? expectedRawStringLiteral = null,
        double? nominalSeconds = null)
    {
        var oracle = ObserveOracle(ctx, oracleSql, bindOracle);
        var rawStringLiteral = rawStringLiteralSql is null
            ? default((string? FieldType, string? ClrType, object? Value)?)
            : ObserveOracle(ctx, rawStringLiteralSql, _ => { });

        var prepared = (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(
            query, false, false, CancellationToken.None);
        var productionSql = prepared.DbCommand.CommandText;
        object? productionValue = null;
        string? productionError = null;
        try
        {
            var values = ctx.ToList(prepared);
            values.Should().ContainSingle($"{name} must return exactly one row");
            productionValue = values[0];
        }
        catch (Exception exception)
        {
            productionError = $"{exception.GetType().Name}: {exception.Message}";
        }

        var productionType = productionValue?.GetType().FullName ?? "NULL";

        Console.WriteLine(
            $"{name} | production={Format(productionValue)} [{productionType}] " +
            $"error={productionError ?? "-"} | " +
            $"oracle={Format(oracle.Value)} [{oracle.ClrType}] (field {oracle.FieldType})");

        // The observation is written before any assertion, so a failing comparison still leaves the
        // measured evidence on disk.
        WriteLine(writer, new Dictionary<string, object?>
        {
            ["kind"] = "temporal-typed-probe",
            ["name"] = name,
            ["oracleSql"] = oracleSql,
            ["oracleFieldType"] = oracle.FieldType,
            ["oracleClrType"] = oracle.ClrType,
            ["oracleValue"] = Format(oracle.Value),
            ["productionSql"] = productionSql,
            ["productionClrType"] = productionType,
            ["productionValue"] = Format(productionValue),
            ["productionError"] = productionError,
            ["rawStringLiteral"] = rawStringLiteral is { } literal
                ? new Dictionary<string, object?>
                {
                    ["sql"] = rawStringLiteralSql,
                    ["fieldType"] = literal.FieldType,
                    ["clrType"] = literal.ClrType,
                    ["value"] = Format(literal.Value)
                }
                : null,
            ["nominalSeconds"] = nominalSeconds
        });

        if (productionError is not null)
            return $"{name}: production '{Format(productionValue)}' [{productionType}] != " +
                   $"oracle '{Format(oracle.Value)}' [{oracle.ClrType}] (field {oracle.FieldType}): {productionError}";

        if (expectedOracleType is not null && oracle.Value?.GetType() != expectedOracleType)
            return $"{name}: the typed raw oracle returned '{Format(oracle.Value)}' " +
                   $"[{oracle.ClrType}] (field {oracle.FieldType}), expected {expectedOracleType.FullName}";

        if (expectedProductionValue is not null
            && (productionValue?.GetType() != expectedProductionValue.GetType()
                || !Equals(productionValue, expectedProductionValue)))
            return $"{name}: the translated result was '{Format(productionValue)}' [{productionType}], " +
                   $"expected {expectedProductionValue} [{expectedProductionValue.GetType().FullName}]";

        if (expectedRawStringLiteral is not null)
        {
            var literalDecimal = rawStringLiteral is { Value: { } literalValue }
                ? Convert.ToDecimal(literalValue, CultureInfo.InvariantCulture)
                : (decimal?)null;
            if (literalDecimal != expectedRawStringLiteral)
                return $"{name}: the raw string-literal oracle returned '{Format(rawStringLiteral?.Value)}' " +
                       $"[{rawStringLiteral?.ClrType}] (field {rawStringLiteral?.FieldType}), " +
                       $"expected {expectedRawStringLiteral}";
        }

        if (Nullable.GetUnderlyingType(typeof(T)) == typeof(long))
        {
            var productionLong = Convert.ToInt64(productionValue, CultureInfo.InvariantCulture);
            var oracleLong = Convert.ToInt64(oracle.Value, CultureInfo.InvariantCulture);
            return productionLong == oracleLong
                ? null
                : $"{name}: production long {productionLong} [{productionType}] != " +
                  $"oracle long {oracleLong} [{oracle.ClrType}]";
        }

        return Equals(productionValue, oracle.Value)
            ? null
            : $"{name}: production '{Format(productionValue)}' [{productionType}] != " +
              $"oracle '{Format(oracle.Value)}' [{oracle.ClrType}]";
    }

    private static (string? FieldType, string? ClrType, object? Value) ObserveOracle(
        MySqlDataContext ctx, string sql, Action<MySqlCommand> bind)
    {
        using var command = (MySqlCommand)ctx.CreateCommand(sql);
        bind(command);
        using var reader = command.ExecuteReader();
        reader.Read().Should().BeTrue($"the oracle '{sql}' must return one row");
        var fieldType = reader.GetFieldType(0).FullName;
        var value = reader.IsDBNull(0) ? null : reader.GetValue(0);

        while (reader.Read())
        {
            // Drain remaining rows so the connection stays usable for the next case.
        }

        return (fieldType, value?.GetType().FullName, value);
    }

    private static void ExecuteRaw(MySqlDataContext ctx, string sql)
    {
        using var command = ctx.CreateCommand(sql);
        command.ExecuteNonQuery();
    }

    private static Dictionary<string, object?> ToRecord(Observation observation) => new()
    {
        ["sql"] = observation.Sql,
        ["connectorFieldType"] = observation.FieldType,
        ["valueType"] = observation.ValueType,
        ["isDbNull"] = observation.IsNull,
        ["textValue"] = observation.Text,
        ["error"] = observation.Error,
        ["durationMs"] = observation.DurationMs
    };

    private static void WriteLine(StreamWriter writer, Dictionary<string, object?> record)
    {
        writer.WriteLine(JsonSerializer.Serialize(record));
        writer.Flush();
    }

    private static Observation Observe(MySqlConnection connection, string sql)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var command = new MySqlCommand(sql, connection);
            using var reader = command.ExecuteReader();
            if (reader.FieldCount == 0)
                return new Observation(sql, null, null, false, null, "no result set", stopwatch.ElapsedMilliseconds);

            if (!reader.Read())
                return new Observation(sql, null, null, false, null, "no rows", stopwatch.ElapsedMilliseconds);

            var connectorFieldType = reader.GetFieldType(0).FullName;
            var isNull = reader.IsDBNull(0);
            var value = isNull ? null : reader.GetValue(0);
            var valueType = isNull ? "NULL" : value!.GetType().FullName;
            var text = isNull ? null : Format(value);

            while (reader.Read())
            {
                // Drain remaining rows so the connection stays usable for the next case.
            }

            return new Observation(sql, connectorFieldType, valueType, isNull, text, null, stopwatch.ElapsedMilliseconds);
        }
        catch (MySqlException exception)
        {
            return new Observation(
                sql, null, null, false, null, $"MySqlException {exception.Number}: {exception.Message}", stopwatch.ElapsedMilliseconds);
        }
        catch (Exception exception)
        {
            return new Observation(
                sql, null, null, false, null, $"{exception.GetType().Name}: {exception.Message}", stopwatch.ElapsedMilliseconds);
        }
    }

    private static string? Format(object? value)
    {
        var text = value switch
        {
            null => null,
            byte[] bytes => "hex:" + Convert.ToHexString(bytes)[..Math.Min(64, bytes.Length * 2)],
            DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
            TimeSpan timeSpan => timeSpan.ToString("c", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };

        return text is { Length: > 200 } ? text[..200] : text;
    }

    private static void RunNonQuery(MySqlConnection connection, string sql)
    {
        try
        {
            using var command = new MySqlCommand(sql, connection);
            command.ExecuteNonQuery();
        }
        catch (MySqlException exception)
        {
            Console.WriteLine($"setup/teardown failed ({exception.Number}): {exception.Message} for: {sql}");
        }
    }

    private static string? Scalar(MySqlConnection connection, string sql, out string? error)
    {
        error = null;
        try
        {
            using var command = new MySqlCommand(sql, connection);
            var value = command.ExecuteScalar();
            return value is null or DBNull ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
        }
        catch (MySqlException exception)
        {
            error = $"MySqlException {exception.Number}: {exception.Message}";
            return null;
        }
    }

    private static string PasswordComponentState(MySqlConnection connection, out string? note)
    {
        var count = Scalar(
            connection,
            "SELECT COUNT(*) FROM information_schema.plugins WHERE plugin_name LIKE '%validate_password%'",
            out var countError);

        if (countError is not null)
        {
            note = countError;
            return "unknown";
        }

        if (count is not null && count != "0")
        {
            note = $"{count} plugin row(s) present";
            return "present";
        }

        try
        {
            using var command = new MySqlCommand("INSTALL COMPONENT 'file://component_validate_password'", connection);
            command.ExecuteNonQuery();
            var after = Scalar(
                connection,
                "SELECT COUNT(*) FROM information_schema.plugins WHERE plugin_name LIKE '%validate_password%'",
                out _);
            note = $"installed during the probe; plugin rows after install: {after}";
            return "installed";
        }
        catch (MySqlException exception)
        {
            note = $"INSTALL COMPONENT failed: MySqlException {exception.Number}: {exception.Message}";
            return "absent";
        }
    }

    private sealed record Observation(
        string Sql,
        string? FieldType,
        string? ValueType,
        bool IsNull,
        string? Text,
        string? Error,
        long DurationMs);
}

/// <summary>The one-row temporal probe table the typed-input probe selects from.</summary>
[SqlTable("t349_temporal_probe")]
public interface IT349TemporalProbe
{
    /// <summary>The probe row identifier.</summary>
    [Key]
    [Column("id")]
    int Id { get; set; }
}
