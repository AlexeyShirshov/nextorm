using FluentAssertions;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Integration.Tests;

/// <summary>
/// Serializes the T245 allocation gate against every other integration collection: the measurement is
/// byte-exact per thread and reads the process-wide plan caches, so a concurrent test sharing the
/// process would perturb both the measured deltas and the probe counters.
/// </summary>
[CollectionDefinition("T245 warm set-operation allocation", DisableParallelization = true)]
public sealed class T245WarmSetOperationAllocationCollection;

/// <summary>
/// Native allocation gate for T245 (#245, warm <c>EXCEPT</c>/<c>INTERSECT</c>) on SQLite — the same
/// provider, tables and fluent shape as <c>SqliteBenchmarkFeaturesFairCached.B_Nextorm_Cached_Except</c>
/// / <c>_Intersect</c> and the <c>SqliteBenchmarkWarmDecompose.*_Warm_Reused</c> controls, measured with
/// <see cref="GC.GetAllocatedBytesForCurrentThread"/> instead of BenchmarkDotNet.
/// <para>
/// Four rows per operation, one per governed requirement: the fresh-fluent cached arm
/// (<c>AC-245-02</c>/<c>AC-245-06</c>), the prepared control and the <c>Warm_Reused</c> control
/// (<c>AC-245-05</c>). The measured region is a bare loop that rebuilds the fluent query on every
/// iteration, exactly like the benchmark arms (<c>SqliteBenchmarkFeaturesFairCached.cs:29</c>,
/// <c>Iterations = 10</c>); the fixture's seeding and the plan-cache warmup run strictly outside it.
/// </para>
/// <para>
/// The ceiling of each arm is the exact per-query byte count measured on the post-change tree
/// (<c>.pdca/rc5/items/T245/rv3/step2-r3/attribution.md</c>), with the campaign's allocation noise band
/// of <c>0 B</c>. The <c>Warm_Reused</c> rows are an exact <c>0 B</c> invariant, not a ceiling.
/// </para>
/// <para>
/// The cached arms are measured at <c>7 416</c> or <c>7 528 B/query</c> depending on the JIT tiering
/// state the test process happens to land in (a <c>112 B/query</c> construction-stage difference, the
/// same one the campaign's comparability condition names); both states repeat within a process, and the
/// state cannot be pinned from inside the test, so the ceiling spans the higher state. Each ceiling
/// carries a <c>2 B/query</c> integer margin: the measured per-query figures are integer division of a
/// 256-call series, and a measured series can land <c>1.3 B/query</c> above its modal value. The margin
/// covers that excursion, not a regression budget — the measured values are <c>7 416/7 528</c> (cached)
/// and <c>1 088</c> (prepared) B/query, and any regression of <c>2 B/query</c> or more fails.
/// </para>
/// </summary>
[Collection("T245 warm set-operation allocation")]
[Trait("PDCA", "T245")]
[Trait("Category", "Diagnostic")]
public sealed class SqliteWarmSetOperationAllocationTests : IDisposable
{
    /// <summary>Rows per seeded table; large enough that the set operations return a non-empty result.</summary>
    public const int RowCount = 300;

    /// <summary>Unmeasured calls per arm before the first series (JIT + plan-cache warmup).</summary>
    public const int WarmupCalls = 64;

    /// <summary>Calls per measured series; the ceiling is expressed per query, the series is scaled.</summary>
    public const int MeasuredCalls = 256;

    /// <summary>Consecutive measured series per arm, on one thread.</summary>
    public const int SeriesCount = 3;

    /// <summary>
    /// Locked ceiling for the fresh-fluent cached <c>EXCEPT</c> arm, bytes per query: the measured
    /// <c>7 528</c> B/query (higher JIT tiering state; lower state <c>7 416</c>) plus the <c>2 B/query</c>
    /// integer margin for the observed <c>+1.3 B/query</c> series excursion.
    /// </summary>
    public const long ExceptCachedBytesPerQueryCeiling = 7530;

    /// <summary>
    /// Locked ceiling for the fresh-fluent cached <c>INTERSECT</c> arm, bytes per query: the measured
    /// <c>7 528</c> B/query (higher JIT tiering state; lower state <c>7 416</c>) plus the <c>2 B/query</c>
    /// integer margin for the observed <c>+1.3 B/query</c> series excursion.
    /// </summary>
    public const long IntersectCachedBytesPerQueryCeiling = 7530;

    /// <summary>
    /// Locked ceiling for the prepared control of <c>EXCEPT</c>, bytes per query: the measured
    /// <c>1 088</c> B/query (zero spread over 45 series) plus the same <c>2 B/query</c> integer margin.
    /// </summary>
    public const long ExceptPreparedBytesPerQueryCeiling = 1090;

    /// <summary>
    /// Locked ceiling for the prepared control of <c>INTERSECT</c>, bytes per query: the measured
    /// <c>1 088</c> B/query (zero spread over 45 series) plus the same <c>2 B/query</c> integer margin.
    /// </summary>
    public const long IntersectPreparedBytesPerQueryCeiling = 1090;

    private readonly string _databasePath;
    private readonly IDataContext _db;
    private readonly EntityBuilder<ISimpleEntity> _simple;
    private readonly EntityBuilder<IComplexEntity> _complex;
    private readonly QueryCommand<int> _reusedExcept;
    private readonly QueryCommand<int> _reusedIntersect;
    private readonly IPreparedQueryCommand<int> _preparedExcept;
    private readonly IPreparedQueryCommand<int> _preparedIntersect;

    // Written by every measured call so the JIT cannot drop the query; a field store allocates nothing.
    private long _sink;
    private object? _sinkRef;

    private static CancellationToken Ct => CancellationToken.None;

    public SqliteWarmSetOperationAllocationTests()
    {
        _databasePath = Path.Combine(
            Path.GetTempPath(),
            $"nextorm.t245.allocation.{Environment.ProcessId}.{Guid.NewGuid():N}.db");

        _db = new SqliteDataContext($"Data Source='{_databasePath}'", new DataContextBuilder());
        ((IConnectionManager)_db).EnsureConnectionOpen();

        Seed();
        _simple = _db.From<ISimpleEntity>();
        _complex = _db.From<IComplexEntity>();

        // Already-prepared commands for the Warm_Reused rows: the loop pays the cache lookup only.
        _reusedExcept = BuildExcept();
        _ = _db.GetPreparedQueryCommand(_reusedExcept, false, true, Ct);
        _reusedIntersect = BuildIntersect();
        _ = _db.GetPreparedQueryCommand(_reusedIntersect, false, true, Ct);

        // The prepared controls: prepared and rendered once, then executed only.
        _preparedExcept = _db.GetPreparedQueryCommand(BuildExcept(), true, false, Ct);
        _preparedIntersect = _db.GetPreparedQueryCommand(BuildIntersect(), true, false, Ct);
    }

    /// <summary>Creates and fills both tables once, before any measurement.</summary>
    private void Seed()
    {
        var connection = ((DataContext)_db).GetConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            create table simple_entity (id integer primary key);
            create table complex_entity (id integer primary key, somestring text);
            with recursive c(i) as (select 1 union all select i + 1 from c where i < {RowCount})
            insert into simple_entity (id) select i from c;
            with recursive c(i) as (select 2 union all select i + 2 from c where i < {RowCount})
            insert into complex_entity (id, somestring) select i, 'row-' || i from c;
            """;
        command.ExecuteNonQuery();
    }

    /// <summary>The exact fluent shape of <c>B_Nextorm_Cached_Except</c>.</summary>
    private QueryCommand<int> BuildExcept()
        => _simple.Select(s => s.Id).Except(_complex.Select(c => (int)c.Id));

    /// <summary>The exact fluent shape of <c>B_Nextorm_Cached_Intersect</c>.</summary>
    private QueryCommand<int> BuildIntersect()
        => _simple.Select(s => s.Id).Intersect(_complex.Select(c => (int)c.Id));

    [Fact]
    public void WarmSetOperations_ShouldStayWithinTheUnitByteCeilings()
    {
        // ---------------------------------------------------------------- harness overhead (published)
        var harnessBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < MeasuredCalls; i++)
            _sink = 1;
        var harnessBytes = GC.GetAllocatedBytesForCurrentThread() - harnessBefore;

        var exceptCached = Measure(() => _sink = BuildExcept().ToList().Count);
        var intersectCached = Measure(() => _sink = BuildIntersect().ToList().Count);
        var exceptPrepared = Measure(() => _sink = _db.ToList(_preparedExcept).Count);
        var intersectPrepared = Measure(() => _sink = _db.ToList(_preparedIntersect).Count);
        var exceptReused = Measure(() => _sinkRef = _db.GetPreparedQueryCommand(_reusedExcept, false, true, Ct));
        var intersectReused = Measure(() => _sinkRef = _db.GetPreparedQueryCommand(_reusedIntersect, false, true, Ct));

        var failures = new List<string>();
        Check("except-cached", exceptCached, ExceptCachedBytesPerQueryCeiling, failures);
        Check("intersect-cached", intersectCached, IntersectCachedBytesPerQueryCeiling, failures);
        Check("except-prepared", exceptPrepared, ExceptPreparedBytesPerQueryCeiling, failures);
        Check("intersect-prepared", intersectPrepared, IntersectPreparedBytesPerQueryCeiling, failures);

        // Warm_Reused is an invariant, not a ceiling: a prepared command reused must allocate nothing.
        for (var series = 0; series < SeriesCount; series++)
        {
            if (exceptReused[series] != 0)
                failures.Add($"except-reused series {series}: {exceptReused[series]} B for {MeasuredCalls} calls, expected 0");
            if (intersectReused[series] != 0)
                failures.Add($"intersect-reused series {series}: {intersectReused[series]} B for {MeasuredCalls} calls, expected 0");
        }

        Console.WriteLine(
            $"T245 allocation gate (B/query): harness={harnessBytes} "
            + $"except-cached=[{Series(exceptCached)}] "
            + $"intersect-cached=[{Series(intersectCached)}] "
            + $"except-prepared=[{Series(exceptPrepared)}] "
            + $"intersect-prepared=[{Series(intersectPrepared)}] "
            + $"except-reused=[{string.Join(", ", exceptReused)}] intersect-reused=[{string.Join(", ", intersectReused)}]");

        // Optional machine-readable row (one line per run, appended): used by the cycle's evidence
        // capture to publish the observed spread next to the locked ceilings.
        if (Environment.GetEnvironmentVariable("T245_ALLOCATION_REPORT") is { Length: > 0 } reportPath)
        {
            File.AppendAllText(
                reportPath,
                $"except-cached={Series(exceptCached)};intersect-cached={Series(intersectCached)};"
                + $"except-prepared={Series(exceptPrepared)};intersect-prepared={Series(intersectPrepared)};"
                + $"except-reused={string.Join(",", exceptReused)};intersect-reused={string.Join(",", intersectReused)}"
                + $"|harness={harnessBytes}{Environment.NewLine}");
        }

        failures.Should().BeEmpty();
    }

    /// <summary>Warms one arm, then measures <see cref="SeriesCount"/> consecutive series of it.</summary>
    private long[] Measure(Action once)
    {
        for (var i = 0; i < WarmupCalls; i++)
            once();

        var series = new long[SeriesCount];
        for (var s = 0; s < SeriesCount; s++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < MeasuredCalls; i++)
                once();
            series[s] = GC.GetAllocatedBytesForCurrentThread() - before;
        }

        return series;
    }

    /// <summary>Formats one arm's series as per-query byte counts.</summary>
    private static string Series(long[] series)
        => string.Join(", ", series.Select(it => (it / (double)MeasuredCalls).ToString("F1", System.Globalization.CultureInfo.InvariantCulture)));

    /// <summary>Appends one failure per series whose per-query byte count exceeds the ceiling.</summary>
    private static void Check(string arm, long[] series, long ceilingBytesPerQuery, List<string> failures)
    {
        var limit = ceilingBytesPerQuery * MeasuredCalls;
        for (var s = 0; s < SeriesCount; s++)
        {
            if (series[s] > limit)
                failures.Add($"{arm} series {s}: {series[s] / (double)MeasuredCalls:F1} B/query exceeds the {ceilingBytesPerQuery} B/query ceiling");
        }
    }

    public void Dispose()
    {
        _db.Dispose();
        try
        {
            File.Delete(_databasePath);
        }
        catch (IOException)
        {
            // The fixture database is a throw-away file; a failed delete does not affect the measurement.
        }
    }
}
