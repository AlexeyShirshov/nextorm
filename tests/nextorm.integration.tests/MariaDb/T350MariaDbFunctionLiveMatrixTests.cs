using System.Text.Json;
using FluentAssertions;
using MySqlConnector;

namespace NextORM.Integration.Tests;

/// <summary>
/// T350 (issue #284) S1 matrix: the same frozen universe re-observed as a compact per-candidate matrix
/// (accepted / refused, vendor error number, CLR field type), aggregated per family. It asserts only that
/// the matrix is complete and non-vacuous — every candidate lands in exactly one bucket, the three
/// permanent exclusions are refused, and no family is empty — because the dispositions themselves belong
/// to PLAN, not to this probe. S2/S3 extend this class with the translated (provider-path) assertions.
/// </summary>
[Trait("Category", "Diagnostic")]
public sealed class T350MariaDbFunctionLiveMatrixTests : IDisposable
{
    private const string DefaultMatrixPath = "/tmp/opencode/t350-matrix-s1.jsonl";

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
}
