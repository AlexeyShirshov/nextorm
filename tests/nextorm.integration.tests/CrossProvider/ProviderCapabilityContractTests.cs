using FluentAssertions;

namespace NextORM.Integration.Tests;

/// <summary>
/// Pins the <see cref="ITestProvider"/> capability matrix.
/// <para>
/// The shared <c>CommonTestSuite</c> fans every fact out over all providers and each capability flag
/// can silence a fact through <c>Assert.Skip</c>. Without a contract, flipping a flag by accident
/// removes coverage instead of turning a test red — and a skip is easy to miss in the run summary.
/// This test makes every change to the matrix a deliberate edit of the pinned table below.
/// </para>
/// </summary>
public sealed class ProviderCapabilityContractTests
{
    private static readonly ITestProvider[] AllProviders =
    [
        SqliteTestProvider.Instance,
        PostgresTestProvider.Instance,
        SqlServerTestProvider.Instance,
        MySqlTestProvider.Instance,
        ClickHouseTestProvider.Instance,
    ];

    private static readonly Dictionary<string, IReadOnlyDictionary<string, bool>> Pinned =
        new(StringComparer.Ordinal)
        {
            ["sqlite"] = new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["SupportsFullJoin"] = true,
                ["SupportsFractionalAverage"] = true,
                ["SupportsIntersectExceptAll"] = false,
                ["SupportsVarianceAggregates"] = true,
                ["SupportsTableValuedFunctions"] = true,
                ["EnforcesScalarSubqueryCardinality"] = false,
                ["SupportsApply"] = false,
                ["SupportsInsertReturning"] = true,
                ["SupportsIgnoreDuplicates"] = true,
                ["SupportsTruncate"] = false,
                ["SupportsDeleteJoin"] = false,
                ["SupportsCreateTableAsSelect"] = true,
                ["SupportsTemporaryCreateTableAsSelect"] = true,
                ["SupportsBatch"] = true,
                ["SupportsStoredProcedures"] = false,
                ["SupportsTableValuedParameters"] = true,
                ["SupportsTransactions"] = true,
                ["SupportsSavepoints"] = true,
                ["SupportsRegex"] = true,
                ["SupportsLobStreaming"] = true,
                ["SupportsLobDataReader"] = true,
                ["SupportsZeroColumnResult"] = false,
                ["SupportsDynamicColumnsRead"] = true,
                ["SupportsTupleIn"] = true,
                ["SupportsAffectedRowMetadata"] = true,
            },
            ["postgres"] = new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["SupportsFullJoin"] = true,
                ["SupportsFractionalAverage"] = true,
                ["SupportsIntersectExceptAll"] = true,
                ["SupportsVarianceAggregates"] = true,
                ["SupportsTableValuedFunctions"] = false,
                ["EnforcesScalarSubqueryCardinality"] = true,
                ["SupportsApply"] = true,
                ["SupportsInsertReturning"] = true,
                ["SupportsIgnoreDuplicates"] = true,
                ["SupportsTruncate"] = true,
                ["SupportsDeleteJoin"] = true,
                ["SupportsCreateTableAsSelect"] = true,
                ["SupportsTemporaryCreateTableAsSelect"] = true,
                ["SupportsBatch"] = true,
                ["SupportsStoredProcedures"] = true,
                ["SupportsTableValuedParameters"] = true,
                ["SupportsTransactions"] = true,
                ["SupportsSavepoints"] = true,
                ["SupportsRegex"] = true,
                ["SupportsLobStreaming"] = true,
                ["SupportsLobDataReader"] = true,
                ["SupportsZeroColumnResult"] = true,
                ["SupportsDynamicColumnsRead"] = true,
                ["SupportsTupleIn"] = true,
                ["SupportsAffectedRowMetadata"] = true,
            },
            ["sqlserver"] = new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["SupportsFullJoin"] = true,
                // T291 (#291): SQL Server widens an integral AVG argument to float inside the
                // aggregate, so the fractional result holds and the shared AVG facts are live.
                ["SupportsFractionalAverage"] = true,
                ["SupportsIntersectExceptAll"] = false,
                ["SupportsVarianceAggregates"] = true,
                ["SupportsTableValuedFunctions"] = false,
                ["EnforcesScalarSubqueryCardinality"] = true,
                ["SupportsApply"] = true,
                ["SupportsInsertReturning"] = true,
                ["SupportsIgnoreDuplicates"] = false,
                ["SupportsTruncate"] = true,
                ["SupportsDeleteJoin"] = true,
                ["SupportsCreateTableAsSelect"] = true,
                ["SupportsTemporaryCreateTableAsSelect"] = false,
                ["SupportsBatch"] = true,
                ["SupportsStoredProcedures"] = true,
                ["SupportsTableValuedParameters"] = true,
                ["SupportsTransactions"] = true,
                ["SupportsSavepoints"] = true,
                ["SupportsRegex"] = true,
                ["SupportsLobStreaming"] = true,
                ["SupportsLobDataReader"] = true,
                ["SupportsZeroColumnResult"] = false,
                ["SupportsDynamicColumnsRead"] = true,
                ["SupportsTupleIn"] = false,
                ["SupportsAffectedRowMetadata"] = true,
            },
            ["mysql"] = new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["SupportsFullJoin"] = false,
                ["SupportsFractionalAverage"] = true,
                ["SupportsIntersectExceptAll"] = false,
                ["SupportsVarianceAggregates"] = true,
                ["SupportsTableValuedFunctions"] = false,
                ["EnforcesScalarSubqueryCardinality"] = true,
                ["SupportsApply"] = true,
                ["SupportsInsertReturning"] = false,
                ["SupportsIgnoreDuplicates"] = true,
                ["SupportsTruncate"] = true,
                ["SupportsDeleteJoin"] = true,
                ["SupportsCreateTableAsSelect"] = true,
                ["SupportsTemporaryCreateTableAsSelect"] = true,
                ["SupportsBatch"] = true,
                ["SupportsStoredProcedures"] = true,
                ["SupportsTableValuedParameters"] = true,
                ["SupportsTransactions"] = true,
                ["SupportsSavepoints"] = true,
                ["SupportsRegex"] = true,
                ["SupportsLobStreaming"] = false,
                ["SupportsLobDataReader"] = false,
                ["SupportsZeroColumnResult"] = false,
                ["SupportsDynamicColumnsRead"] = true,
                ["SupportsTupleIn"] = true,
                ["SupportsAffectedRowMetadata"] = true,
            },
            ["clickhouse"] = new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["SupportsFullJoin"] = true,
                ["SupportsFractionalAverage"] = true,
                ["SupportsIntersectExceptAll"] = true,
                ["SupportsVarianceAggregates"] = true,
                ["SupportsTableValuedFunctions"] = false,
                ["EnforcesScalarSubqueryCardinality"] = true,
                ["SupportsApply"] = false,
                ["SupportsInsertReturning"] = false,
                ["SupportsIgnoreDuplicates"] = false,
                ["SupportsTruncate"] = true,
                ["SupportsDeleteJoin"] = false,
                ["SupportsCreateTableAsSelect"] = true,
                ["SupportsTemporaryCreateTableAsSelect"] = false,
                ["SupportsBatch"] = false,
                ["SupportsStoredProcedures"] = false,
                ["SupportsTableValuedParameters"] = true,
                ["SupportsTransactions"] = false,
                ["SupportsSavepoints"] = false,
                ["SupportsRegex"] = true,
                ["SupportsLobStreaming"] = false,
                ["SupportsLobDataReader"] = false,
                ["SupportsZeroColumnResult"] = false,
                ["SupportsDynamicColumnsRead"] = true,
                ["SupportsTupleIn"] = true,
                ["SupportsAffectedRowMetadata"] = false,
            },
        };

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    [InlineData("sqlserver")]
    [InlineData("mysql")]
    [InlineData("clickhouse")]
    public void Capabilities_ShouldMatchThePinnedMatrix(string providerName)
    {
        var provider = AllProviders.Single(p => p.Name == providerName);

        ReadCapabilities(provider).Should().BeEquivalentTo(Pinned[providerName],
            "a capability flag silently turning off hides shared facts behind Assert.Skip");
    }

    [Fact]
    public void PinnedMatrix_ShouldCoverEveryProviderAndEveryCapabilityFlag()
    {
        Pinned.Keys.Should().BeEquivalentTo(AllProviders.Select(p => p.Name));

        var flags = typeof(ITestProvider).GetProperties()
            .Where(p => p.PropertyType == typeof(bool) && p.Name != nameof(ITestProvider.IsAvailable))
            .Select(p => p.Name)
            .ToArray();

        flags.Should().NotBeEmpty();

        foreach (var (providerName, matrix) in Pinned)
            matrix.Keys.Should().BeEquivalentTo(flags, $"{providerName} must pin every capability flag");
    }

    [Fact]
    public void ProvidersWithoutTableValuedFunctions_ShouldExplainWhy()
    {
        foreach (var provider in AllProviders.Where(p => !p.SupportsTableValuedFunctions))
            provider.TableValuedFunctionSkipReason.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// T289: the savepoint capability is pinned once and must agree on both surfaces — the harness flag
    /// that gates the shared live facts and the dialect capability the context injects into the
    /// transaction manager. A mismatch would let the harness skip a provider whose dialect reports
    /// support (or execute facts against a dialect that reports none).
    /// </summary>
    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    [InlineData("sqlserver")]
    [InlineData("mysql")]
    [InlineData("clickhouse")]
    public void SavepointCapability_ShouldMatchThePinnedMatrixOnBothSurfaces(string providerName)
    {
        var provider = AllProviders.Single(p => p.Name == providerName);
        var pinned = Pinned[providerName][nameof(ITestProvider.SupportsSavepoints)];

        provider.SupportsSavepoints.Should().Be(pinned, "the harness flag is pinned per provider");

        using var context = (DataContext)provider.CreateContext();
        context.Dialect.SupportsSavepoints.Should().Be(pinned,
            "the dialect capability the context injects must equal the harness flag");
        provider.SupportsSavepoints.Should().Be(context.Dialect.SupportsSavepoints);
    }

    private static Dictionary<string, bool> ReadCapabilities(ITestProvider provider) =>
        new(StringComparer.Ordinal)
        {
            [nameof(ITestProvider.SupportsFullJoin)] = provider.SupportsFullJoin,
            [nameof(ITestProvider.SupportsFractionalAverage)] = provider.SupportsFractionalAverage,
            [nameof(ITestProvider.SupportsIntersectExceptAll)] = provider.SupportsIntersectExceptAll,
            [nameof(ITestProvider.SupportsVarianceAggregates)] = provider.SupportsVarianceAggregates,
            [nameof(ITestProvider.SupportsTableValuedFunctions)] = provider.SupportsTableValuedFunctions,
            [nameof(ITestProvider.EnforcesScalarSubqueryCardinality)] = provider.EnforcesScalarSubqueryCardinality,
            [nameof(ITestProvider.SupportsApply)] = provider.SupportsApply,
            [nameof(ITestProvider.SupportsInsertReturning)] = provider.SupportsInsertReturning,
            [nameof(ITestProvider.SupportsIgnoreDuplicates)] = provider.SupportsIgnoreDuplicates,
            [nameof(ITestProvider.SupportsTruncate)] = provider.SupportsTruncate,
            [nameof(ITestProvider.SupportsDeleteJoin)] = provider.SupportsDeleteJoin,
            [nameof(ITestProvider.SupportsCreateTableAsSelect)] = provider.SupportsCreateTableAsSelect,
            [nameof(ITestProvider.SupportsTemporaryCreateTableAsSelect)] = provider.SupportsTemporaryCreateTableAsSelect,
            [nameof(ITestProvider.SupportsBatch)] = provider.SupportsBatch,
            [nameof(ITestProvider.SupportsStoredProcedures)] = provider.SupportsStoredProcedures,
            [nameof(ITestProvider.SupportsTableValuedParameters)] = provider.SupportsTableValuedParameters,
            [nameof(ITestProvider.SupportsTransactions)] = provider.SupportsTransactions,
            [nameof(ITestProvider.SupportsSavepoints)] = provider.SupportsSavepoints,
            [nameof(ITestProvider.SupportsRegex)] = provider.SupportsRegex,
            [nameof(ITestProvider.SupportsLobStreaming)] = provider.SupportsLobStreaming,
            [nameof(ITestProvider.SupportsLobDataReader)] = provider.SupportsLobDataReader,
            [nameof(ITestProvider.SupportsZeroColumnResult)] = provider.SupportsZeroColumnResult,
            [nameof(ITestProvider.SupportsDynamicColumnsRead)] = provider.SupportsDynamicColumnsRead,
            [nameof(ITestProvider.SupportsTupleIn)] = provider.SupportsTupleIn,
            [nameof(ITestProvider.SupportsAffectedRowMetadata)] = provider.SupportsAffectedRowMetadata,
        };
}
