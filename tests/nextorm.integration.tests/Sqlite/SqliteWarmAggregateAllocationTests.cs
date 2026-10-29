using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Integration.Tests;

/// <summary>
/// Serializes the D243 allocation probe against every other integration collection: the measurement
/// is byte-exact per thread and reads the process-wide <see cref="DataContextCache"/> populations, so a
/// concurrent test sharing the process would perturb both the measured deltas and the probe counters.
/// </summary>
[CollectionDefinition("D243 warm-aggregate allocation", DisableParallelization = true)]
public sealed class D243WarmAggregateAllocationCollection;

/// <summary>
/// Unit-byte allocation oracle for D243 (warm aggregates, #243) on SQLite — the same provider and the
/// same data shape as <c>SqliteBenchmarkAggregates</c> (<c>large_table</c>, 10 000 rows, a
/// <see cref="long"/> column as the <c>Sum</c> target), measured with
/// <see cref="GC.GetAllocatedBytesForCurrentThread"/> instead of BenchmarkDotNet.
/// <para>
/// The measured region is a bare loop that rebuilds the fluent query and the inline selector on every
/// iteration, exactly like the benchmark arms <c>B_Nextorm_Cached_Count</c>
/// (<c>_ctx.LargeEntity.Count()</c>) and <c>B_Nextorm_Cached_Sum</c>
/// (<c>_ctx.LargeEntity.Sum(e =&gt; e.Id)</c>). The table is created and seeded once in the constructor,
/// i.e. strictly outside the measured region; only the harness loop body itself is inside it.
/// </para>
/// <para>
/// Technique: <see cref="GC.GetAllocatedBytesForCurrentThread"/> before/after each measured loop of
/// <see cref="MeasuredCalls"/> calls, three consecutive series on one thread. The empty-loop harness
/// overhead is measured separately and published, and query/selector allocations are <b>not</b>
/// subtracted, so a series value is a raw byte total for 256 calls. Thresholds come from the
/// historical linq2db <c>BytesPerOperation</c> oracle (Count 2864 B/query, Sum 3490 B/query) with a
/// 0 B/query tolerance. Warmup, assertions and formatting are all outside the measured loop.
/// </para>
/// <para>
/// When <c>D243_ALLOCATION_REPORT</c> is set the full probe (per-series bytes, derived B/query,
/// harness overhead, predicates and the construction probe) is written to that path as JSON; when it
/// is unset the test still asserts. The construction probe is described at its declaration: a
/// per-query artifact-construction count is <b>not observable at this tier</b> without product
/// instrumentation, so it is reported as such rather than as a fake zero.
/// </para>
/// </summary>
[Collection("D243 warm-aggregate allocation")]
[Trait("PDCA", "T243")]
[Trait("Category", "Diagnostic")]
public sealed class SqliteWarmAggregateAllocationTests : IDisposable
{
    /// <summary>Row count of the benchmark's <c>large_table</c>; the fixture mirrors it.</summary>
    public const int RowCount = 10_000;

    /// <summary>Unmeasured calls per arm before the first series (JIT + plan-cache warmup).</summary>
    public const int WarmupCalls = 64;

    /// <summary>Calls per measured series; the threshold is expressed per series of this many calls.</summary>
    public const int MeasuredCalls = 256;

    /// <summary>Consecutive measured series per arm, on one thread.</summary>
    public const int SeriesCount = 3;

    /// <summary>Historical linq2db <c>BytesPerOperation</c> for the cached <c>Count</c> arm.</summary>
    public const long CountBytesPerQueryThreshold = 2864;

    /// <summary>Historical linq2db <c>BytesPerOperation</c> for the cached <c>Sum</c> arm.</summary>
    public const long SumBytesPerQueryThreshold = 3490;

    private const string ReportPathVariable = "D243_ALLOCATION_REPORT";

    private const string AttributionPathVariable = "D243_ATTRIBUTION_REPORT";

    private static readonly string[] SeriesNames = ["series-1", "series-2", "series-3"];

    /// <summary>
    /// The Sum arm's inline selector. A non-capturing expression-tree lambda is cached by the compiler
    /// per call site, so the terminal receives the same instance on every call — stage A must feed the
    /// same shape into the rebuilt wrapper.
    /// </summary>
    private static readonly Expression<Func<AggregateRow, long>> SumSelector = x => x.Id;

    private readonly string _databasePath;
    private readonly IDataContext _db;
    private readonly EntityBuilder<AggregateRow> _rows;

    // Written by every measured call so the JIT cannot drop the query; a field store allocates nothing.
    private long _sink;

    // Stage A keeps the constructed (never executed) command alive after its measured loop.
    private object? _sinkRef;

    // Stage C's prepared commands, built once outside every measured loop.
    private IPreparedQueryCommand<int>? _preparedCount;
    private IPreparedQueryCommand<long>? _preparedSum;

    public SqliteWarmAggregateAllocationTests()
    {
        _databasePath = Path.Combine(
            Path.GetTempPath(),
            $"nextorm.d243.allocation.{Environment.ProcessId}.{Guid.NewGuid():N}.db");

        var builder = new DataContextBuilder();
        _db = new SqliteDataContext($"Data Source='{_databasePath}'", builder);
        ((IConnectionManager)_db).EnsureConnectionOpen();

        Seed();
        _rows = _db.From<AggregateRow>();
    }

    /// <summary>
    /// Creates and fills the 10 000-row <c>d243_large_table</c> mirroring the benchmark's
    /// <c>large_table</c> (a <see cref="long"/> key plus a string and a date column) once, before any
    /// measurement.
    /// </summary>
    private void Seed()
    {
        var connection = ((DataContext)_db).GetConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            create table d243_large_table (id integer primary key, someString text, dt datetime);
            with recursive c(i) as (select 1 union all select i + 1 from c where i < {RowCount})
            insert into d243_large_table (id, someString, dt)
            select i, 'row-' || i, datetime('2020-01-01 00:00:00', '+' || i || ' seconds') from c;
            """;
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// One call of the benchmark's <c>B_Nextorm_Cached_Count</c> arm: a fresh fluent query rebuilt from
    /// the entity builder on every call.
    /// </summary>
    private long CountOnce() => _rows.Count();

    /// <summary>
    /// One call of the benchmark's <c>B_Nextorm_Cached_Sum</c> arm: a fresh fluent query rebuilt from
    /// the entity builder plus a fresh inline selector on every call.
    /// </summary>
    private long SumOnce() => _rows.Sum(x => x.Id);

    [Fact]
    public void WarmAggregates_ShouldStayWithinTheUnitByteThreshold()
    {
        // ---------------------------------------------------------------- harness overhead (published)
        // Same loop shape and call count as a measured series, empty body: the cost of measurement
        // itself. It is reported next to the series totals and is NOT subtracted from them.
        var harnessBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < MeasuredCalls; i++)
            _sink = 1;
        var harnessBytes = GC.GetAllocatedBytesForCurrentThread() - harnessBefore;

        var expressionsBefore = DataContextCache.ExpressionsCache.Count;
        var selectListsBefore = DataContextCache.SelectListCache.Count;

        // ---------------------------------------------------------------- Count arm
        for (var i = 0; i < WarmupCalls; i++)
            _sink = CountOnce();

        var expressionsAfterCountWarmup = DataContextCache.ExpressionsCache.Count;

        var countSeriesBytes = new long[SeriesCount];
        for (var series = 0; series < SeriesCount; series++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < MeasuredCalls; i++)
                _sink = CountOnce();
            countSeriesBytes[series] = GC.GetAllocatedBytesForCurrentThread() - before;
        }

        var expressionsAfterCount = DataContextCache.ExpressionsCache.Count;
        var selectListsAfterCount = DataContextCache.SelectListCache.Count;

        // ---------------------------------------------------------------- Sum arm
        for (var i = 0; i < WarmupCalls; i++)
            _sink = SumOnce();

        var expressionsAfterSumWarmup = DataContextCache.ExpressionsCache.Count;

        var sumSeriesBytes = new long[SeriesCount];
        for (var series = 0; series < SeriesCount; series++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < MeasuredCalls; i++)
                _sink = SumOnce();
            sumSeriesBytes[series] = GC.GetAllocatedBytesForCurrentThread() - before;
        }

        var expressionsAfterSum = DataContextCache.ExpressionsCache.Count;
        var selectListsAfterSum = DataContextCache.SelectListCache.Count;

        // ---------------------------------------------------------------- thresholds + report
        // Everything below runs after the last measured loop closed.
        var countLimit = (long)MeasuredCalls * CountBytesPerQueryThreshold;
        var sumLimit = (long)MeasuredCalls * SumBytesPerQueryThreshold;

        var failures = new List<string>();
        for (var series = 0; series < SeriesCount; series++)
        {
            if (countSeriesBytes[series] > countLimit)
            {
                failures.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Count {SeriesNames[series]}: {countSeriesBytes[series]} B for {MeasuredCalls} calls = {(double)countSeriesBytes[series] / MeasuredCalls:F2} B/query, over the {CountBytesPerQueryThreshold} B/query threshold ({countLimit} B/series)."));
            }

            if (sumSeriesBytes[series] > sumLimit)
            {
                failures.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Sum {SeriesNames[series]}: {sumSeriesBytes[series]} B for {MeasuredCalls} calls = {(double)sumSeriesBytes[series] / MeasuredCalls:F2} B/query, over the {SumBytesPerQueryThreshold} B/query threshold ({sumLimit} B/series)."));
            }
        }

        WriteReport(
            harnessBytes,
            countSeriesBytes,
            sumSeriesBytes,
            countLimit,
            sumLimit,
            failures.Count == 0,
            expressionsBefore,
            expressionsAfterCountWarmup,
            expressionsAfterCount,
            expressionsAfterSumWarmup,
            expressionsAfterSum,
            selectListsBefore,
            selectListsAfterCount,
            selectListsAfterSum);

        (failures.Count == 0).Should().BeTrue(
            string.Join(Environment.NewLine, failures) +
            Environment.NewLine +
            $"measured on sqlite over {RowCount} rows; harness overhead {harnessBytes} B per {MeasuredCalls} empty calls; " +
            "query/selector allocations are not subtracted.");
    }

    /// <summary>
    /// Stage attribution for the per-call bytes of the two measured arms, on the same 10 000-row SQLite
    /// data and with the same <see cref="GC.GetAllocatedBytesForCurrentThread"/> technique as
    /// <see cref="WarmAggregates_ShouldStayWithinTheUnitByteThreshold"/>. Diagnostic only: it is skipped
    /// unless <c>D243_ATTRIBUTION_REPORT</c> is set, so the default red test and its bytes stay unchanged.
    /// <para>
    /// Stages are non-overlapping by construction — three separate loops, each with its own warmup, none
    /// nested in another: <b>A</b> constructs the caller/builder artifacts and stops before the terminal
    /// (no command execution, no DB round-trip), <b>B</b> is the full terminal of the byte fixture, and
    /// <b>C</b> is the prepared-command control (the benchmark's <c>A_Nextorm_Prepared_*</c> arms, which
    /// are available at the public API). <c>B - A</c> is the terminal + execution bytes and
    /// <c>(B - A) - C</c> is published as the unattributed residual, numerically.
    /// </para>
    /// </summary>
    [Fact]
    public void WarmAggregateStages_ShouldAttributeThePerCallBytes()
    {
        var reportPath = Environment.GetEnvironmentVariable(AttributionPathVariable);
        if (string.IsNullOrWhiteSpace(reportPath))
            Assert.Skip($"{AttributionPathVariable} is not set; the stage attribution probe is diagnostic-only.");

        // Stage C setup — exactly the benchmark's prepared arms, built once, outside every measured loop.
        var countCommand = _rows.Select(e => SqlFunctions.Sql.count());
        countCommand.SingleRow = true;
        _preparedCount = countCommand.Prepare(default, TestContext.Current.CancellationToken);

        var sumCommand = _rows.Select(e => SqlFunctions.Sql.sum(e.Id));
        sumCommand.SingleRow = true;
        _preparedSum = sumCommand.Prepare(default, TestContext.Current.CancellationToken);

        var harnessSeries = MeasureSeries(NoOp);

        // Stage A — caller + builder construction, no terminal.
        QueryPlanner.ResetCounters();
        var stageACount = MeasureSeries(BuildCountCommand);
        var stageASum = MeasureSeries(BuildSumCommand);
        var stageARecipe = RecipeCounters();

        // Stage B — the fixture's full terminal.
        QueryPlanner.ResetCounters();
        var stageBCount = MeasureSeries(CountTerminal);
        var stageBSum = MeasureSeries(SumTerminal);
        var stageBRecipe = RecipeCounters();

        // Stage C — prepared control: the same aggregate through a pre-prepared command.
        QueryPlanner.ResetCounters();
        var stageCCount = MeasureSeries(CountPreparedTerminal);
        var stageCSum = MeasureSeries(SumPreparedTerminal);
        var stageCRecipe = RecipeCounters();

        // Everything below runs after the last measured loop closed.
        GC.KeepAlive(_sinkRef);
        WriteAttributionReport(
            reportPath,
            harnessSeries,
            (stageACount, stageASum, stageARecipe),
            (stageBCount, stageBSum, stageBRecipe),
            (stageCCount, stageCSum, stageCRecipe));
    }

    /// <summary>
    /// One unmeasured warmup plus <see cref="SeriesCount"/> measured loops of <see cref="MeasuredCalls"/>
    /// calls of <paramref name="body"/>; nothing else is inside a measured region.
    /// </summary>
    private static long[] MeasureSeries(Action body)
    {
        for (var i = 0; i < WarmupCalls; i++)
            body();

        var series = new long[SeriesCount];
        for (var s = 0; s < SeriesCount; s++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < MeasuredCalls; i++)
                body();
            series[s] = GC.GetAllocatedBytesForCurrentThread() - before;
        }

        return series;
    }

    private static double PerCall(long[] series)
        => (double)series.Max() / MeasuredCalls;

    /// <summary>
    /// Snapshot of the planner's pre-preparation recipe counters — process-wide diagnostics that tell
    /// whether the terminal actually probed/reused a recipe for every fresh command.
    /// </summary>
    private static (int Publishes, int Reuses, int Rejections, int ApplyMismatches) RecipeCounters()
        => (QueryPlanner.RecipePublishes, QueryPlanner.RecipeReuses, QueryPlanner.RecipeRejections, QueryPlanner.RecipeApplyMismatches);

    private static Dictionary<string, object?> StageReport(long[] series) => new()
    {
        ["series_bytes"] = series,
        ["bytes_per_call_series"] = series.Select(static b => (double)b / MeasuredCalls).ToArray(),
        ["max_bytes_per_call"] = PerCall(series),
    };

    private static Dictionary<string, object?> RecipeReport(
        (int Publishes, int Reuses, int Rejections, int ApplyMismatches) counters) => new()
    {
        ["publishes"] = counters.Publishes,
        ["reuses"] = counters.Reuses,
        ["rejections"] = counters.Rejections,
        ["apply_mismatches"] = counters.ApplyMismatches,
    };

    /// <summary>Empty body for the harness-overhead series (delegate call only).</summary>
    private void NoOp()
    {
    }

    /// <summary>Stage A, Count arm: the <c>CountCore</c> seam up to (excluding) the terminal.</summary>
    private void BuildCountCommand()
        => _sinkRef = _rows.SelectParent(e => SqlFunctions.Sql.count());

    /// <summary>
    /// Stage A, Sum arm: the <c>AggregateCore</c> seam up to (excluding) the terminal, including its
    /// per-call <c>Expression.Lambda</c>/<c>Expression.Call</c>/<c>MakeGenericMethod</c> wrapper rebuild.
    /// </summary>
    private void BuildSumCommand()
        => _sinkRef = _rows.SelectParent(Expression.Lambda<Func<AggregateRow, long>>(
            Expression.Call(
                CommonFunctions.SQLExpression,
                CommonFunctions.SumMI.MakeGenericMethod(typeof(long)),
                SumSelector.Body),
            SumSelector.Parameters));

    /// <summary>Stage B, Count arm: the fixture's full terminal.</summary>
    private void CountTerminal() => _sink = CountOnce();

    /// <summary>Stage B, Sum arm: the fixture's full terminal.</summary>
    private void SumTerminal() => _sink = SumOnce();

    /// <summary>Stage C, Count arm: the prepared control.</summary>
    private void CountPreparedTerminal()
        => _sink = _db.ExecuteScalar(_preparedCount!, ReadOnlySpan<object?>.Empty, false);

    /// <summary>Stage C, Sum arm: the prepared control.</summary>
    private void SumPreparedTerminal()
        => _sink = _db.ExecuteScalar(_preparedSum!, ReadOnlySpan<object?>.Empty, false);

    /// <summary>
    /// Writes the stage-attribution JSON to <c>D243_ATTRIBUTION_REPORT</c>. Everything here is outside
    /// the measured loops.
    /// </summary>
    private void WriteAttributionReport(
        string reportPath,
        long[] harnessSeries,
        (long[] Count, long[] Sum, (int Publishes, int Reuses, int Rejections, int ApplyMismatches) Recipe) stageA,
        (long[] Count, long[] Sum, (int Publishes, int Reuses, int Rejections, int ApplyMismatches) Recipe) stageB,
        (long[] Count, long[] Sum, (int Publishes, int Reuses, int Rejections, int ApplyMismatches) Recipe) stageC)
    {
        var stageACount = stageA.Count;
        var stageASum = stageA.Sum;
        var stageBCount = stageB.Count;
        var stageBSum = stageB.Sum;
        var stageCCount = stageC.Count;
        var stageCSum = stageC.Sum;

        var fullPath = Path.GetFullPath(reportPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var countTerminalPlusExecution = PerCall(stageBCount) - PerCall(stageACount);
        var sumTerminalPlusExecution = PerCall(stageBSum) - PerCall(stageASum);
        var countExecutionOnly = PerCall(stageCCount);
        var sumExecutionOnly = PerCall(stageCSum);

        var report = new Dictionary<string, object?>
        {
            ["unit"] = "D243-02",
            ["test"] = $"{typeof(SqliteWarmAggregateAllocationTests).FullName}.{nameof(WarmAggregateStages_ShouldAttributeThePerCallBytes)}",
            ["provider"] = "sqlite",
            ["rows"] = RowCount,
            ["warmup_calls"] = WarmupCalls,
            ["measured_calls"] = MeasuredCalls,
            ["series"] = SeriesCount,
            ["technique"] =
                "GC.GetAllocatedBytesForCurrentThread() before/after each measured loop, " +
                "separate non-overlapping loops per stage, one delegate call per iteration",
            ["stage_overlap"] = false,
            ["stages"] = new Dictionary<string, object?>
            {
                ["A_builder_only"] = new Dictionary<string, object?>
                {
                    ["definition"] =
                        "caller + builder construction only: SelectParent -> WithoutJoinIntos -> SelectCore -> " +
                        "CreateCommand(new QueryDefinition, new QueryCommand) -> ResolveSource; no terminal, " +
                        "no command execution, no DB round-trip",
                    ["count"] = StageReport(stageACount),
                    ["sum"] = StageReport(stageASum),
                },
                ["B_full_terminal"] = new Dictionary<string, object?>
                {
                    ["definition"] =
                        "the fixture's measured terminal: fresh fluent command + QueryCommand.ExecuteScalar " +
                        "for Count() / Sum(x => x.Id)",
                    ["count"] = StageReport(stageBCount),
                    ["sum"] = StageReport(stageBSum),
                },
                ["C_prepared_control"] = new Dictionary<string, object?>
                {
                    ["definition"] =
                        "prepared control: IDataContext.ExecuteScalar(IPreparedQueryCommand<T>, ...) of the " +
                        "benchmark arms A_Nextorm_Prepared_Count/Sum; prepared once outside every measured loop",
                    ["public_api"] = true,
                    ["count"] = StageReport(stageCCount),
                    ["sum"] = StageReport(stageCSum),
                },
            },
            ["decomposition_bytes_per_call"] = new Dictionary<string, object?>
            {
                ["count"] = new Dictionary<string, object?>
                {
                    ["A_construction"] = PerCall(stageACount),
                    ["B_total"] = PerCall(stageBCount),
                    ["terminal_plus_execution_B_minus_A"] = countTerminalPlusExecution,
                    ["execution_only_prepared_C"] = countExecutionOnly,
                    ["unattributed_residual"] = countTerminalPlusExecution - countExecutionOnly,
                },
                ["sum"] = new Dictionary<string, object?>
                {
                    ["A_construction"] = PerCall(stageASum),
                    ["B_total"] = PerCall(stageBSum),
                    ["terminal_plus_execution_B_minus_A"] = sumTerminalPlusExecution,
                    ["execution_only_prepared_C"] = sumExecutionOnly,
                    ["unattributed_residual"] = sumTerminalPlusExecution - sumExecutionOnly,
                },
            },
            ["recipe_diagnostics"] = new Dictionary<string, object?>
            {
                ["definition"] =
                    "QueryPlanner pre-preparation recipe counters (process-wide, internal, reset before each " +
                    "stage); they say whether the terminal probed and reused a cached recipe per fresh command",
                ["expected_calls_per_stage"] = 2 * (WarmupCalls + SeriesCount * MeasuredCalls),
                ["A_builder_only"] = RecipeReport(stageA.Recipe),
                ["B_full_terminal"] = RecipeReport(stageB.Recipe),
                ["C_prepared_control"] = RecipeReport(stageC.Recipe),
            },
            ["harness_overhead"] = new Dictionary<string, object?>
            {
                ["calls"] = MeasuredCalls,
                ["series_bytes"] = harnessSeries,
                ["bytes_per_call"] = PerCall(harnessSeries),
                ["subtracted"] = false,
                ["note"] =
                    "empty body through the same MeasureSeries helper; every stage loop goes through that " +
                    "same delegate call, so stage deltas are comparable",
            },
            ["correctness_sink"] = new Dictionary<string, object?>
            {
                ["last_value"] = _sink,
                ["count_expected"] = RowCount,
                ["sum_expected"] = (long)RowCount * (RowCount + 1) / 2,
            },
            ["runtime"] = Environment.Version.ToString(),
            ["database"] = _databasePath,
        };

        File.WriteAllText(
            fullPath,
            System.Text.Json.JsonSerializer.Serialize(
                report,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>
    /// Writes the probe JSON to <c>D243_ALLOCATION_REPORT</c> when that variable is set. Everything here
    /// is outside the measured loops.
    /// </summary>
    private void WriteReport(
        long harnessBytes,
        long[] countSeriesBytes,
        long[] sumSeriesBytes,
        long countLimit,
        long sumLimit,
        bool passed,
        int expressionsBefore,
        int expressionsAfterCountWarmup,
        int expressionsAfterCount,
        int expressionsAfterSumWarmup,
        int expressionsAfterSum,
        int selectListsBefore,
        int selectListsAfterCount,
        int selectListsAfterSum)
    {
        var reportPath = Environment.GetEnvironmentVariable(ReportPathVariable);
        if (string.IsNullOrWhiteSpace(reportPath))
            return;

        var fullPath = Path.GetFullPath(reportPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var report = new Dictionary<string, object?>
        {
            ["unit"] = "D243-03",
            ["test"] = $"{typeof(SqliteWarmAggregateAllocationTests).FullName}.{nameof(WarmAggregates_ShouldStayWithinTheUnitByteThreshold)}",
            ["provider"] = "sqlite",
            ["rows"] = RowCount,
            ["warmup_calls"] = WarmupCalls,
            ["measured_calls"] = MeasuredCalls,
            ["series"] = SeriesCount,
            ["technique"] = "GC.GetAllocatedBytesForCurrentThread() before/after each measured loop",
            ["query_and_selector_allocations_subtracted"] = false,
            ["passed"] = passed,
            ["sink"] = _sink,
            ["count"] = new Dictionary<string, object?>
            {
                ["threshold_bytes_per_query"] = CountBytesPerQueryThreshold,
                ["threshold_bytes_per_series"] = countLimit,
                ["series_names"] = SeriesNames,
                ["series_bytes"] = countSeriesBytes,
                ["series_bytes_per_query"] = countSeriesBytes.Select(static b => (double)b / MeasuredCalls).ToArray(),
                ["max_bytes_per_query"] = (double)countSeriesBytes.Max() / MeasuredCalls,
                ["passed"] = countSeriesBytes.Max() <= countLimit,
            },
            ["sum"] = new Dictionary<string, object?>
            {
                ["threshold_bytes_per_query"] = SumBytesPerQueryThreshold,
                ["threshold_bytes_per_series"] = sumLimit,
                ["series_names"] = SeriesNames,
                ["series_bytes"] = sumSeriesBytes,
                ["series_bytes_per_query"] = sumSeriesBytes.Select(static b => (double)b / MeasuredCalls).ToArray(),
                ["max_bytes_per_query"] = (double)sumSeriesBytes.Max() / MeasuredCalls,
                ["passed"] = sumSeriesBytes.Max() <= sumLimit,
            },
            ["harness_overhead"] = new Dictionary<string, object?>
            {
                ["calls"] = MeasuredCalls,
                ["bytes"] = harnessBytes,
                ["bytes_per_call"] = (double)harnessBytes / MeasuredCalls,
                ["subtracted"] = false,
            },
            ["construction_probe"] = new Dictionary<string, object?>
            {
                ["status"] = "NOT-OBSERVABLE-AT-THIS-TIER",
                ["reason"] =
                    "A per-query count of warmed-artifact constructions (expression-tree/select-list/plan " +
                    "artifacts built or reused) has no public counter in nextorm.core; observing it requires " +
                    "product instrumentation, which this unit must not add. No fake zero is reported.",
                ["adjacent_observable"] =
                    "Process-wide cache populations before/after the arms (growth = new cache entries, " +
                    "not construction counts).",
                ["expressions_cache"] = new Dictionary<string, object?>
                {
                    ["before_arms"] = expressionsBefore,
                    ["after_count_warmup"] = expressionsAfterCountWarmup,
                    ["after_count_series"] = expressionsAfterCount,
                    ["after_sum_warmup"] = expressionsAfterSumWarmup,
                    ["after_sum_series"] = expressionsAfterSum,
                },
                ["select_list_cache"] = new Dictionary<string, object?>
                {
                    ["before_arms"] = selectListsBefore,
                    ["after_count_series"] = selectListsAfterCount,
                    ["after_sum_series"] = selectListsAfterSum,
                },
            },
            ["runtime"] = Environment.Version.ToString(),
            ["database"] = _databasePath,
        };

        File.WriteAllText(
            fullPath,
            System.Text.Json.JsonSerializer.Serialize(
                report,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }

    public void Dispose()
    {
        _db.Dispose();

        try
        {
            File.Delete(_databasePath);
        }
        catch (IOException exception)
        {
            Console.WriteLine($"could not delete the probe database '{_databasePath}': {exception.Message}");
        }
    }

    /// <summary>
    /// The <c>large_table</c> shape of <c>SqliteBenchmarkAggregates</c>: the <see cref="long"/> key is
    /// the <c>Sum</c> target, and the string/date columns keep the metadata mapping identical.
    /// </summary>
    [SqlTable("d243_large_table")]
    public sealed class AggregateRow
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("someString")]
        public string? Str { get; set; }

        [Column("dt")]
        public DateTime? Dt { get; set; }
    }
}
