using System.Text.Json;
using FluentAssertions;
using MySqlConnector;

namespace NextORM.Integration.Tests;

/// <summary>
/// T350 (issue #284) S2 block 3b: the one bounded observation the <c>DECODE_ORACLE</c> row needs. The
/// delivered MariaDB arm renders the vendor's short spelling <c>decode(...)</c>, which outside
/// <c>sql_mode=ORACLE</c> resolves to the two-argument crypto function — the live matrix row fails with
/// <c>Incorrect parameter count in the call to native function 'decode'</c>. This probe asks the same
/// server the matrix runs against whether the long spelling <c>DECODE_ORACLE(...)</c> is available in
/// default mode, and records every raw answer.
/// <para>
/// The probe owns no product contract: it records the session mode, then the raw result (field type,
/// CLR type, value) or the vendor error of each declared statement, and asserts only that the record is
/// complete. The rendering decision belongs to PLAN; the record is the evidence it is taken on.
/// </para>
/// </summary>
[Trait("Category", "Diagnostic")]
public sealed class T350MariaDbDecodeOracleProbeTests
{
    private const string DefaultProbePath = "/tmp/opencode/t350-decode-oracle-s2.jsonl";

    /// <summary>
    /// The declared probe statements: the branch/default forms of the long spelling, the 3- and 4-argument
    /// shapes, and the short spelling the arm renders today for comparison.
    /// </summary>
    private static readonly (string Id, string Label, string Sql)[] Statements =
    [
        ("DO-01", "matching-first-branch", "SELECT DECODE_ORACLE(1, 1, 'a', 2, 'b', 'default')"),
        ("DO-02", "matching-second-branch", "SELECT DECODE_ORACLE(2, 1, 'a', 2, 'b', 'default')"),
        ("DO-03", "default-branch", "SELECT DECODE_ORACLE(9, 1, 'a', 2, 'b', 'default')"),
        ("DO-04", "null-expression-default-branch", "SELECT DECODE_ORACLE(NULL, 1, 'a', 2, 'b', 'default')"),
        ("DO-05", "three-argument-form", "SELECT DECODE_ORACLE(1, 1, 'a')"),
        ("DO-06", "four-argument-form", "SELECT DECODE_ORACLE(1, 1, 'a', 'default')"),
        ("DO-07", "short-spelling-two-arguments", "SELECT DECODE(1, 1)"),
        ("DO-08", "short-spelling-six-arguments", "SELECT DECODE(1, 1, 'a', 2, 'b', 'default')")
    ];

    [Fact]
    public void T350DecodeOracle_ShouldBeProbedInDefaultMode()
    {
        Assert.SkipUnless(MariaDbContainer.IsAvailable, MariaDbContainer.Failure ?? "MariaDB is not available.");

        var probePath = T350RawOracle.PathOf("T350_DECODE_ORACLE_PATH", DefaultProbePath);

        using var connection = new MySqlConnection(MariaDbContainer.ConnectionString);
        connection.Open();

        using var writer = T350RawOracle.Open(probePath, FileMode.Create);

        var sessionMode = T350RawOracle.Scalar(connection, "select @@session.sql_mode");
        var oracleMode = sessionMode?.Contains("ORACLE", StringComparison.OrdinalIgnoreCase) ?? false;
        writer.WriteLine(JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["kind"] = "server",
            ["serverVersion"] = T350RawOracle.Scalar(connection, "select version()"),
            ["sessionSqlMode"] = sessionMode,
            ["globalSqlMode"] = T350RawOracle.Scalar(connection, "select @@global.sql_mode"),
            ["oracleModeInSession"] = oracleMode
        }));
        writer.Flush();

        oracleMode.Should().BeFalse(
            "the probe answers a default-mode question: with sql_mode=ORACLE the short spelling would be " +
            "the Oracle form and no answer about default mode would be recorded");

        var recorded = 0;
        foreach (var (id, label, sql) in Statements)
        {
            var observation = T350RawOracle.Observe(connection, sql);
            writer.WriteLine(JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["kind"] = "probe",
                ["id"] = id,
                ["label"] = label,
                ["sql"] = observation.Sql,
                ["fieldType"] = observation.FieldType,
                ["valueType"] = observation.ValueType,
                ["isDbNull"] = observation.IsNull,
                ["textValue"] = observation.Text,
                ["errorNumber"] = observation.ErrorNumber,
                ["sqlState"] = observation.SqlState,
                ["error"] = observation.Error,
                ["durationMs"] = observation.DurationMs
            }));
            writer.Flush();
            recorded++;
        }

        recorded.Should().Be(Statements.Length, "every declared probe statement must be recorded");
        sessionMode.Should().NotBeNull("the session mode is the precondition the probe records");
    }
}
