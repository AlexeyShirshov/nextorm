using System.Globalization;
using System.Text.Json;
using FluentAssertions;
using MySqlConnector;
using NextORM.MariaDb;

namespace NextORM.Integration.Tests;

/// <summary>
/// T350 (issue #284) S1: the raw MariaDB 11.4 oracle over the whole frozen universe — the 89-name
/// exclusion tuple, the concrete names of the MariaDB spec cell, and one version-scoped expansion of its
/// four wildcard groups plus the paired encryption forms. Nothing here asserts a product contract: the
/// only assertions are structural (every candidate produced an observation, the server identity was
/// captured, the <c>sys</c> routines were classified), because the dispositions are decided by PLAN from
/// the recorded evidence.
/// </summary>
[Trait("Category", "Diagnostic")]
public sealed class T350MariaDbInventoryProbeTests : IDisposable
{
    private const string DefaultOraclePath = "/tmp/opencode/t350-oracle-s1.jsonl";

    private const string DefaultIdentityPath = "/tmp/opencode/t350-server-identity.txt";

    private const string DefaultSysPath = "/tmp/opencode/t350-sys-routines.jsonl";

    private readonly MySqlConnection _connection;

    public T350MariaDbInventoryProbeTests()
    {
        Assert.SkipUnless(MariaDbContainer.IsAvailable, MariaDbContainer.Failure ?? "MariaDB is not available.");

        _connection = new MySqlConnection(MariaDbContainer.ConnectionString);
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    /// <summary>
    /// Executes every candidate with raw ADO.NET and flushes one JSON line per statement, then asserts
    /// that the observation count matches the case count and that every candidate carries a distinct
    /// inventory id (a short artifact or a collapsed table is a failed probe, never a silently green run).
    /// </summary>
    [Fact]
    public void T350Universe_ShouldRecordARawObservationForEveryCandidate()
    {
        var oraclePath = T350RawOracle.PathOf("T350_ORACLE_PATH", DefaultOraclePath);
        var server = ServerFacts();

        using var writer = T350RawOracle.Open(oraclePath, FileMode.Create);
        var observed = 0;

        foreach (var @case in T350Cases.All)
        {
            if (@case.Sql.Length == 0)
            {
                WriteLine(writer, server, @case, "no-scalar-form", null);
                observed++;
                continue;
            }

            if (@case.SetupSql is { Length: > 0 } setup)
                T350RawOracle.Execute(_connection, setup);
            try
            {
                var primary = T350RawOracle.Observe(_connection, @case.Sql);
                WriteLine(writer, server, @case, "primary", primary);
                observed++;

                if (@case.NullSql is { Length: > 0 } nullSql)
                {
                    WriteLine(writer, server, @case, "null", T350RawOracle.Observe(_connection, nullSql));
                    observed++;
                }
            }
            finally
            {
                if (@case.TeardownSql is { Length: > 0 } teardown)
                    T350RawOracle.Execute(_connection, teardown);
            }
        }

        writer.Flush();

        T350Cases.All.Select(@case => @case.Id).Distinct().Should().HaveCount(
            T350Cases.All.Length,
            "every candidate of the frozen universe is exactly one distinct inventory row");
        observed.Should().BeGreaterThanOrEqualTo(
            T350Cases.All.Length, "every candidate must produce at least one recorded observation");
        observed.Should().BeGreaterThan(0, "a zero-observation probe is a vacuous run");
    }

    /// <summary>
    /// Records the live server identity (raw ADO.NET plus the provider's own connection) so the baseline
    /// artifact names the exact 11.4 build the oracle was taken against.
    /// </summary>
    [Fact]
    public void T350Server_ShouldBeIdentifiedBeforeAnyDispositionIsTaken()
    {
        var identityPath = T350RawOracle.PathOf("T350_IDENTITY_PATH", DefaultIdentityPath);

        using var writer = T350RawOracle.Open(identityPath, FileMode.Create);

        var version = T350RawOracle.Scalar(_connection, "SELECT VERSION()");
        var comment = T350RawOracle.Scalar(_connection, "SELECT @@version_comment");
        var protocol = T350RawOracle.Scalar(_connection, "SELECT @@protocol_version");

        string? providerVersion = null;
        string? providerError = null;
        try
        {
            using var ctx = new MariaDbDataContext(MariaDbContainer.ConnectionString, new DataContextBuilder());
            ctx.EnsureConnectionOpen();
            using var command = ctx.CreateCommand("SELECT VERSION()");
            var value = command.ExecuteScalar();
            providerVersion = value is null or DBNull ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
        }
        catch (Exception exception)
        {
            providerError = exception.GetType().Name + ": " + exception.Message;
        }

        writer.WriteLine("captured_utc=" + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        writer.WriteLine("select version()=" + version);
        writer.WriteLine("select @@version_comment=" + comment);
        writer.WriteLine("select @@protocol_version=" + protocol);
        writer.WriteLine("provider version()=" + providerVersion);
        writer.WriteLine("provider error=" + (providerError ?? "none"));

        version.Should().NotBeNull("the probe must identify the live server");
        version.Should().StartWith("11.4", "the campaign's frozen server is MariaDB 11.4");
        providerError.Should().BeNull("the provider must connect to the same server the oracle uses");
    }

    /// <summary>
    /// Enumerates the <c>sys</c> schema routines on the running image and classifies each as function or
    /// procedure from <c>information_schema.ROUTINES</c> — the S1 fact the <c>SYS.*</c> row is decided on.
    /// </summary>
    [Fact]
    public void T350SysSchema_ShouldClassifyEveryRoutineAsFunctionOrProcedure()
    {
        var sysPath = T350RawOracle.PathOf("T350_SYS_PATH", DefaultSysPath);

        using var writer = T350RawOracle.Open(sysPath, FileMode.Create);
        var rows = 0;

        using (var command = _connection.CreateCommand())
        {
            command.CommandText =
                "SELECT ROUTINE_SCHEMA, ROUTINE_NAME, ROUTINE_TYPE, DTD_IDENTIFIER " +
                "FROM information_schema.ROUTINES WHERE ROUTINE_SCHEMA = 'sys' " +
                "ORDER BY ROUTINE_TYPE, ROUTINE_NAME";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                writer.WriteLine(JsonSerializer.Serialize(new Dictionary<string, object?>
                {
                    ["kind"] = "sys-routine",
                    ["schema"] = reader.IsDBNull(0) ? null : reader.GetString(0),
                    ["name"] = reader.IsDBNull(1) ? null : reader.GetString(1),
                    ["routineType"] = reader.IsDBNull(2) ? null : reader.GetString(2),
                    ["returns"] = reader.IsDBNull(3) ? null : reader.GetString(3)
                }));
                rows++;
            }
        }

        var schemaExists = T350RawOracle.Scalar(
            _connection,
            "SELECT COUNT(*) FROM information_schema.SCHEMATA WHERE SCHEMA_NAME = 'sys'");

        writer.WriteLine(JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["kind"] = "sys-schema-presence",
            ["schemaExists"] = schemaExists,
            ["routines"] = rows
        }));

        schemaExists.Should().NotBeNull("the sys-schema presence must be a recorded fact, including 0");
    }

    /// <summary>
    /// Re-probes the forms whose first observation was a <c>1064</c> syntax error, so a mistyped statement
    /// is never recorded as evidence about the server: the arrow operators with and without the spaced
    /// spelling, <c>MEMBER OF</c> with both array spellings, and the dynamic-column type-token forms of
    /// <c>COLUMN_GET</c>/<c>COLUMN_CREATE</c>/<c>COLUMN_ADD</c>, with the closest accepted control next to
    /// each. It also records the two facts the open rows turn on: <c>TRUNC</c> under <c>sql_mode=ORACLE</c>
    /// and the <c>_rowid</c> binding on tables that are not a single-integer-primary-key rowid table.
    /// </summary>
    [Fact]
    public void T350Grammar_ShouldReProbeEverySyntaxErrorFormAndItsControl()
    {
        var path = T350RawOracle.PathOf("T350_GRAMMAR_PATH", "/tmp/opencode/t350-grammar-s1.jsonl");

        using var writer = T350RawOracle.Open(path, FileMode.Create);
        var observed = 0;

        void Grammar(string id, string form, string sql, string mode = "default")
        {
            var observation = T350RawOracle.Observe(_connection, sql);
            writer.WriteLine(JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["kind"] = "grammar",
                ["id"] = id,
                ["form"] = form,
                ["mode"] = mode,
                ["sql"] = sql,
                ["accepted"] = observation.Accepted,
                ["fieldType"] = observation.FieldType,
                ["valueType"] = observation.ValueType,
                ["isDbNull"] = observation.IsNull,
                ["textValue"] = observation.Text,
                ["errorNumber"] = observation.ErrorNumber,
                ["error"] = observation.Error,
                ["sqlState"] = observation.SqlState
            }));
            observed++;
        }

        void InOracleMode(Action body)
        {
            T350RawOracle.Execute(_connection, "SET SESSION sql_mode = 'ORACLE'");
            try
            {
                body();
            }
            finally
            {
                T350RawOracle.Execute(_connection, "SET SESSION sql_mode = DEFAULT");
            }
        }

        // ---- the -> and ->> operators: frozen spelling, spaced spelling, and text-extraction controls
        Grammar("G01", "arrow", "SELECT '{\"a\":1}'->'$.a'");
        Grammar("G02", "arrow", "SELECT '{\"a\":1}' -> '$.a'");
        Grammar("G03", "arrow", "SELECT '{\"a\":1}'->>'$.a'");
        Grammar("G04", "arrow", "SELECT '{\"a\":1}' ->> '$.a'");
        Grammar("G05", "arrow-control", "SELECT JSON_EXTRACT('{\"a\":1}','$.a')");
        Grammar("G06", "arrow-control", "SELECT JSON_VALUE('{\"a\":1}','$.a')");

        // ---- MEMBER OF: frozen spelling, unparenthesised array, and the JSON_CONTAINS control
        Grammar("G07", "member-of", "SELECT 1 MEMBER OF ('[1,2]')");
        Grammar("G08", "member-of", "SELECT 1 MEMBER OF JSON_ARRAY(1,2)");
        Grammar("G09", "member-of-control", "SELECT JSON_CONTAINS('[1,2]','1')");

        // ---- dynamic-column type token: is the AS clause mandatory, and where is it allowed?
        Grammar("G10", "column-type-control", "SELECT COLUMN_GET(COLUMN_CREATE('a',1),'a' AS unsigned)");
        Grammar("G11", "column-type", "SELECT COLUMN_GET(COLUMN_CREATE('a',1),'a')");
        Grammar("G12", "column-type", "SELECT COLUMN_GET(COLUMN_CREATE('a',1),'a' AS char)");
        Grammar("G13", "column-type", "SELECT COLUMN_GET(COLUMN_CREATE('a',1),'a' AS int)");
        Grammar("G14", "column-type", "SELECT COLUMN_CREATE('a',1 AS unsigned)");
        Grammar("G15", "column-type", "SELECT COLUMN_ADD(COLUMN_CREATE('a',1),'b',2 AS unsigned)");

        // ---- TRUNC: the frozen string form, then the Oracle-mode date and numeric forms
        Grammar("G16", "trunc", "SELECT TRUNC('2025-09-24 12:43','DD')");
        InOracleMode(() =>
        {
            Grammar("G17", "trunc", "SELECT TRUNC(SYSDATE)", "oracle");
            Grammar("G18", "trunc", "SELECT TRUNC(CAST('2025-09-24 12:43' AS DATETIME),'DD')", "oracle");
            Grammar("G19", "trunc", "SELECT TRUNC(123.456)", "oracle");
            Grammar("G20", "rownum", "SELECT ROWNUM", "oracle");
        });
        Grammar("G21", "rownum", "SELECT ROWNUM");

        // ---- _rowid binding: the rowid table control, then no-PK, text-PK and composite-PK tables
        T350RawOracle.Execute(_connection, "DROP TEMPORARY TABLE IF EXISTS t350_rowid_ctl");
        T350RawOracle.Execute(_connection, "CREATE TEMPORARY TABLE t350_rowid_ctl (id INT NOT NULL PRIMARY KEY, v VARCHAR(10))");
        T350RawOracle.Execute(_connection, "INSERT INTO t350_rowid_ctl VALUES (1,'a')");
        Grammar("G22", "rowid-control", "SELECT _rowid FROM t350_rowid_ctl");

        T350RawOracle.Execute(_connection, "DROP TEMPORARY TABLE IF EXISTS t350_rowid_nopk");
        T350RawOracle.Execute(_connection, "CREATE TEMPORARY TABLE t350_rowid_nopk (a INT, b INT)");
        T350RawOracle.Execute(_connection, "INSERT INTO t350_rowid_nopk VALUES (1,2)");
        Grammar("G23", "rowid", "SELECT _rowid FROM t350_rowid_nopk");

        T350RawOracle.Execute(_connection, "DROP TEMPORARY TABLE IF EXISTS t350_rowid_textpk");
        T350RawOracle.Execute(_connection, "CREATE TEMPORARY TABLE t350_rowid_textpk (k VARCHAR(10) NOT NULL PRIMARY KEY)");
        T350RawOracle.Execute(_connection, "INSERT INTO t350_rowid_textpk VALUES ('a')");
        Grammar("G24", "rowid", "SELECT _rowid FROM t350_rowid_textpk");

        T350RawOracle.Execute(_connection, "DROP TEMPORARY TABLE IF EXISTS t350_rowid_composite");
        T350RawOracle.Execute(_connection, "CREATE TEMPORARY TABLE t350_rowid_composite (a INT, b INT, PRIMARY KEY (a,b))");
        T350RawOracle.Execute(_connection, "INSERT INTO t350_rowid_composite VALUES (1,2)");
        Grammar("G25", "rowid", "SELECT _rowid FROM t350_rowid_composite");

        writer.Flush();

        observed.Should().BeGreaterThan(20, "a truncated grammar re-probe is not evidence");
    }

    // ---------------------------------------------------------------- evidence projection

    private Dictionary<string, object?> ServerFacts() => new()
    {
        ["serverVersion"] = T350RawOracle.Scalar(_connection, "SELECT VERSION()"),
        ["sqlMode"] = T350RawOracle.Scalar(_connection, "SELECT @@session.sql_mode"),
        ["charset"] = T350RawOracle.Scalar(_connection, "SELECT @@character_set_connection"),
        ["collation"] = T350RawOracle.Scalar(_connection, "SELECT @@collation_connection"),
        ["timeZone"] = T350RawOracle.Scalar(_connection, "SELECT @@session.time_zone")
    };

    private static void WriteLine(
        StreamWriter writer,
        Dictionary<string, object?> server,
        T350Case @case,
        string variant,
        T350Observation? observation)
    {
        var record = new Dictionary<string, object?>(server)
        {
            ["kind"] = "oracle",
            ["id"] = @case.Id,
            ["name"] = @case.Name,
            ["family"] = @case.Family,
            ["provenance"] = @case.Provenance,
            ["operationKind"] = @case.Kind,
            ["variant"] = variant,
            ["note"] = @case.Note
        };

        if (observation is not null)
        {
            record["sql"] = observation.Sql;
            record["fieldType"] = observation.FieldType;
            record["valueType"] = observation.ValueType;
            record["isDbNull"] = observation.IsNull;
            record["textValue"] = observation.Text;
            record["errorNumber"] = observation.ErrorNumber;
            record["error"] = observation.Error;
            record["sqlState"] = observation.SqlState;
            record["durationMs"] = observation.DurationMs;
        }

        writer.WriteLine(JsonSerializer.Serialize(record));
        writer.Flush();
    }
}
