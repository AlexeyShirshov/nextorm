using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using FluentAssertions;

namespace NextORM.Integration.Tests;

/// <summary>
/// Row shape of the runtime <c>values</c> descriptor: its mapped properties are rendered as the leading
/// ClickHouse structure argument (<c>'a Int32, b String'</c>) while the tuple list stays verbatim.
/// </summary>
public interface IT320ChValuesRow
{
    [Column("a")]
    int A { get; set; }

    [Column("b")]
    string? B { get; set; }
}

/// <summary>
/// The runtime TVF factories under live test. ClickHouse has no user-defined table function, so the
/// supported forms are the provider built-ins: <c>values</c> with a caller-declared leading result schema
/// and <c>numbers</c> (whose <c>UInt64</c> column the dialect wraps with a cast).
/// </summary>
internal static class T320ChIntegrationTvf
{
    public static TvfEntityBuilder<IT320ChValuesRow> Values(string tuples) => new(
        "values",
        null,
        null,
        null,
        new[] { 0 },
        TableFunctionSchema.LeadingArgument);

    public static TvfEntityBuilder<SqlFunctions.INumbersRow> Numbers(long count) => new("numbers");
}

/// <summary>
/// #320 (T320) live ClickHouse evidence for the runtime TVF source (matrix row MX-CH, AC04/AC06/AC09).
/// The leading-result-schema form is materialised from a real server, an ordinary join over a TVF returns
/// data, the correlated APPLY is rejected by name, and a capture change on the same cached plan instance
/// changes the row set — all observed as data, not as "no exception".
/// </summary>
[Trait("Category", "T320")]
public sealed class T320ClickHouseTvfTests(ITestOutputHelper output) : ProviderTestSuite
{
    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    private static readonly object FixtureGate = new();
    private static string? _serverVersion;

    private IDataContext Context => _sut.DataProvider;

    /// <summary>Reads the server version once per process; ClickHouse needs no fixture DDL here.</summary>
    private string EnsureFixture()
    {
        lock (FixtureGate)
        {
            if (_serverVersion is not null)
                return _serverVersion;

            using var version = Context.ExecuteRaw("select version() as v");
            _serverVersion = version.Read<string>().Single();

            output.WriteLine($"T320 fixture ready; clickhouse version: {_serverVersion}");
            return _serverVersion;
        }
    }

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext context, QueryCommand<T> command, bool storeInCache)
        => (DbPreparedQueryCommand<T>)context.GetPreparedQueryCommand(command, false, storeInCache, CancellationToken.None);

    private static List<string> ParametersOf<T>(DbPreparedQueryCommand<T> prepared)
        => [.. prepared.DbCommandParams.Cast<DbParameter>().Select(p => $"{p.ParameterName}={p.Value ?? "null"}")];

    /// <summary>
    /// The leading result schema renders from the mapped row type and materialises: the structure is the
    /// first call argument, and the leading columns come back with the declared names and values.
    /// </summary>
    [Fact]
    public void Values_WithLeadingResultSchema_ShouldMaterialiseTheDeclaredRows()
    {
        var version = EnsureFixture();
        output.WriteLine($"clickhouse version: {version}");

        var command = Context.From(() => T320ChIntegrationTvf.Values("(1, 'x'), (2, 'y')"))
            .Select(r => new { r.A, r.B });

        var sql = Normalize(Prepare(Context, command, storeInCache: false).DbCommand.CommandText);
        output.WriteLine($"values sql: <<{sql}>>");

        sql.Should().Contain(
            "from values('a Int32, b String', (1, 'x'), (2, 'y'))",
            "the row-derived structure leads the call and the tuple list stays verbatim");

        var rows = command.ToList();

        rows.Should().HaveCount(2, "the two verbatim tuples are the row set");
        rows.Select(r => r.A).Should().Equal(new[] { 1, 2 }, "the leading column materialises with the declared values");
        rows.Select(r => r.B).Should().Equal(new[] { "x", "y" }, "the second leading column materialises in tuple order");
    }

    /// <summary>
    /// An ordinary (non-APPLY) join over a TVF is supported: the cast-wrapped <c>numbers</c> subquery joins
    /// the ordinary table and returns the intersecting rows.
    /// </summary>
    [Fact]
    public void OrdinaryJoinOverTvf_ShouldReturnTheIntersectingRows()
    {
        var version = EnsureFixture();
        output.WriteLine($"clickhouse version: {version}");

        var command = Context.From(() => T320ChIntegrationTvf.Numbers(4))
            .Join(Context.From<ISimpleEntity>(), (tvf, entity) => tvf.Value == entity.Id)
            .Select(p => new { Number = p.Item1.Value, Id = p.Item2.Id })
            .OrderBy(x => x.Number);

        var sql = Normalize(Prepare(Context, command, storeInCache: false).DbCommand.CommandText);
        output.WriteLine($"join sql: <<{sql}>>");

        sql.Should().Contain("from (select toInt64(number) as number", "the TVF is the cast-wrapped FROM source");
        sql.Should().Contain("join simple_entity", "the ordinary table is the joined source");

        var rows = command.ToList();

        rows.Should().HaveCount(3, "numbers(4) is 0..3, so ids 1..3 intersect the seeded table");
        rows.Select(r => r.Number).Should().Equal(new long[] { 1, 2, 3 }, "the joined rows carry the TVF column");
        rows.Select(r => r.Id).Should().Equal(new[] { 1, 2, 3 }, "the ordinary side carries the table column");
    }

    /// <summary>
    /// ClickHouse has no APPLY/LATERAL source: the correlated forms are rejected by name while the real
    /// database is reachable, never silently rendered as an ordinary join.
    /// </summary>
    [Fact]
    public void CorrelatedApply_ShouldBeRejectedByTheDialect()
    {
        var version = EnsureFixture();
        output.WriteLine($"clickhouse version: {version}");

        Context.From<ISimpleEntity>().Count().Should().Be(10, "the real database is reachable before the rejected calls");

        Action cross = () => Prepare(
            Context,
            Context.From<ISimpleEntity>()
                .CrossApply(row => T320ChIntegrationTvf.Numbers(row.Id))
                .Select(p => p.Item1.Id),
            storeInCache: false);
        cross.Should().Throw<NotSupportedException>()
            .WithMessage("*CrossApply join is not supported by this SQL dialect*");

        Action outer = () => Prepare(
            Context,
            Context.From<ISimpleEntity>()
                .OuterApply(row => T320ChIntegrationTvf.Numbers(row.Id))
                .Select(p => p.Item1.Id),
            storeInCache: false);
        outer.Should().Throw<NotSupportedException>()
            .WithMessage("*OuterApply join is not supported by this SQL dialect*");
    }

    /// <summary>
    /// A captured argument is a bound parameter: changing it and executing again reuses the same cached
    /// plan instance, refreshes the parameter and therefore changes the row set.
    /// </summary>
    [Fact]
    public void CapturedArgument_ShouldRefreshOnCacheHitAndChangeTheRowSet()
    {
        var version = EnsureFixture();
        output.WriteLine($"clickhouse version: {version}");

        Context.PurgeQueryCache();

        var capturedCount = 3L;
        QueryCommand<long> Build() => Context.From(() => T320ChIntegrationTvf.Numbers(capturedCount)).Select(r => r.Value);

        var first = Prepare(Context, Build(), storeInCache: true);
        var firstSql = Normalize(first.DbCommand.CommandText);
        var firstObserved = $"rendered statement: <<{firstSql}>>; parameters: [{string.Join(", ", ParametersOf(first))}]";
        output.WriteLine($"first run: {firstObserved}");

        ParametersOf(first).Should().Equal(new[] { "capturedCount=3" }, $"the capture is a bound parameter; {firstObserved}");
        firstSql.Should().Contain("numbers(@capturedCount)", firstObserved);
        firstSql.Should().NotContain("numbers(3)", $"a captured value is never inlined; {firstObserved}");

        var firstRows = Build().ToList().OrderBy(x => x).ToArray();
        firstRows.Should().Equal(new long[] { 0, 1, 2 }, $"the captured 3 selects three numbers; {firstObserved}");

        capturedCount = 5L;

        var second = Prepare(Context, Build(), storeInCache: true);
        var secondSql = Normalize(second.DbCommand.CommandText);
        var secondObserved = $"rendered statement: <<{secondSql}>>; parameters: [{string.Join(", ", ParametersOf(second))}]";
        output.WriteLine($"second run: {secondObserved}");

        ReferenceEquals(first, second).Should().BeTrue(
            $"the same shape must reuse the cached plan so the refresh path is exercised; {secondObserved}");
        secondSql.Should().Be(firstSql, $"a capture change must not re-render the statement; {secondObserved}");
        ParametersOf(second).Should().Equal(
            new[] { "capturedCount=5" },
            $"the cache hit must bind the new capture, never the stale 3; {secondObserved}");

        var secondRows = Build().ToList().OrderBy(x => x).ToArray();
        secondRows.Should().Equal(
            new long[] { 0, 1, 2, 3, 4 },
            $"the refreshed capture selects five numbers on the same context; {secondObserved}");
    }
}
