using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Benchmark;

/// <summary>
/// T245 (#245) Step 2 — <b>measurement-only</b> decomposition of the warm cached <b>execution</b> bucket
/// (<c>--t245-exec-decomposition</c>). It changes no product behaviour, no acceptance recipe, no threshold
/// and no denominator; it splits the prepared-command execution/materialization stage that Step 1 measured
/// at ≈<c>760 / 744 B/query</c> into the named parts the production code executes on the warm path:
/// binding/checkout, reader construction, the row-read loop, the materializer loop, the result list, the
/// empty runtime-parameter array and the asynchronous terminal machinery.
/// <para>
/// Every variant runs the <b>real</b> retained <c>R1*</c> prepared command of
/// <c>B_Nextorm_Cached_Except</c> / <c>_Intersect</c> (same context, same pinned DB, same warm plan cache).
/// Where a part is isolated, the probe uses the same internal seams the production terminal uses
/// (<c>DbPreparedQueryCommand.Acquire</c>, the leased <c>DbCommand</c> and
/// <c>PreparedQueryCommand.MapDelegate</c>); nothing is written to <c>src/</c>.
/// </para>
/// <para>
/// Allocation is measured with <see cref="GC.GetAllocatedBytesForCurrentThread"/> deltas plus the
/// process-wide <see cref="GC.GetTotalAllocatedBytes"/> cross-check, exactly like
/// <see cref="T245AttributionProbe"/> / <see cref="T245Step2BreakdownProbe"/>, so the rows are directly
/// comparable with the accepted Step 1 and Step 2 numbers.
/// </para>
/// </summary>
internal static class T245ExecDecompositionProbe
{
    private const int WarmupIterations = 3;
    private const int BatchIterations = 10;
    private const int BatchCount = 10;

    private const string ExceptOperation = "EXCEPT";
    private const string IntersectOperation = "INTERSECT";

    // Keeps read values and command/reader objects reachable so the JIT cannot fold the window away.
    private static long _sink;

    /// <summary>Runs every variant for both set operations and writes the evidence files.</summary>
    public static Task<int> RunAsync(string outDir)
    {
        Directory.CreateDirectory(outDir);

        var dbPath = BenchDb.FilePath;
        var dbSha = File.Exists(dbPath)
            ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(dbPath))).ToLowerInvariant()
            : "missing";

        var builder = new DataContextBuilder();
        builder.UseSqlite(dbPath);
        var context = builder.CreateDataContext();
        var ctx = new TestDataRepository(context);
        var dc = (DataContext)context;
        var connections = (IConnectionManager)context;
        connections.EnsureConnectionOpen();
        var connection = connections.GetConnection();

        // Verification pass (not measured): the fluent shape must still produce rows, and the count is the
        // list capacity the warm terminal actually uses (the lease hint is set from the previous read).
        var exceptRowCount = ctx.SimpleEntity.Select(s => s.Id).Except(ctx.ComplexEntity.Select(c => (int)c.Id)).ToList().Count;
        var intersectRowCount = ctx.SimpleEntity.Select(s => s.Id).Intersect(ctx.ComplexEntity.Select(c => (int)c.Id)).ToList().Count;
        Console.WriteLine($"verification: {ExceptOperation} rows={exceptRowCount}, {IntersectOperation} rows={intersectRowCount}, db={dbPath}");

        // The exact same preparation the benchmark arm uses: fresh fluent command, cached plan, warm cache.
        var exceptPrepared = (DbPreparedQueryCommand<int>)context.GetPreparedQueryCommand(
            ctx.SimpleEntity.Select(s => s.Id).Except(ctx.ComplexEntity.Select(c => (int)c.Id)), false, true, CancellationToken.None);
        var intersectPrepared = (DbPreparedQueryCommand<int>)context.GetPreparedQueryCommand(
            ctx.SimpleEntity.Select(s => s.Id).Intersect(ctx.ComplexEntity.Select(c => (int)c.Id)), false, true, CancellationToken.None);
        GC.KeepAlive(exceptPrepared);
        GC.KeepAlive(intersectPrepared);

        // Warm the plan cache a second time so both entries are hit, not published, by the measured calls.
        _ = context.GetPreparedQueryCommand(ctx.SimpleEntity.Select(s => s.Id).Except(ctx.ComplexEntity.Select(c => (int)c.Id)), false, true, CancellationToken.None);
        _ = context.GetPreparedQueryCommand(ctx.SimpleEntity.Select(s => s.Id).Intersect(ctx.ComplexEntity.Select(c => (int)c.Id)), false, true, CancellationToken.None);

        var createParam = new Func<DbCommand, string, object?, DbParameter>((_, name, value) => dc.CreateParam(name, value));

        // The executor is the production implementation of the public IQueryExecutor role; the probe reads
        // it once (outside every measured window) so the DataContext async wrapper can be separated from it.
        var executorField = typeof(DataContext).GetField("_executor", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("DataContext._executor not found");
        var executor = (IQueryExecutor)(executorField.GetValue(context)
            ?? throw new InvalidOperationException("DataContext._executor is null"));

        var rows = new List<Row>();
        var recipe = new Dictionary<string, RecipeProbe>(StringComparer.Ordinal);

        foreach (var operation in new[] { ExceptOperation, IntersectOperation })
        {
            var isExcept = operation == ExceptOperation;
            var prepared = isExcept ? exceptPrepared : intersectPrepared;
            var hint = isExcept ? exceptRowCount : intersectRowCount;
            var behavior = prepared.Behavior;
            var mapper = prepared.MapDelegate
                ?? throw new InvalidOperationException("the prepared set-operation command has no row mapper");
            var preparedInterface = (IPreparedQueryCommand<int>)prepared;

            rows.Add(Measure("E0_lease_bind", operation, () =>
            {
                var lease = prepared.Acquire(connection, createParam, null, null, null);
                try
                {
                    _sink += lease.Command is null ? 0 : 1;
                }
                finally
                {
                    lease.Dispose();
                }

                return Task.CompletedTask;
            }));

            rows.Add(Measure("E1_reader_open_sync", operation, () =>
            {
                var lease = prepared.Acquire(connection, createParam, null, null, null);
                try
                {
                    var reader = lease.Command.ExecuteReader(behavior);
                    _sink += reader.FieldCount;
                    reader.Dispose();
                }
                finally
                {
                    lease.Dispose();
                }

                return Task.CompletedTask;
            }));

            rows.Add(Measure("E2_reader_rows_sync", operation, () =>
            {
                var lease = prepared.Acquire(connection, createParam, null, null, null);
                try
                {
                    var reader = lease.Command.ExecuteReader(behavior);
                    var count = 0;
                    while (reader.Read()) count++;
                    _sink += count;
                    reader.Dispose();
                }
                finally
                {
                    lease.Dispose();
                }

                return Task.CompletedTask;
            }));

            rows.Add(Measure("E3_reader_map_sync", operation, () =>
            {
                var lease = prepared.Acquire(connection, createParam, null, null, null);
                try
                {
                    var reader = lease.Command.ExecuteReader(behavior);
                    var total = 0;
                    while (reader.Read()) total += mapper(reader);
                    _sink += total;
                    reader.Dispose();
                }
                finally
                {
                    lease.Dispose();
                }

                return Task.CompletedTask;
            }));

            rows.Add(Measure("E4_list_hint", operation, () =>
            {
                var list = new List<int>(hint);
                for (var i = 0; i < hint; i++) list.Add(i);
                _sink += list.Count;
                return Task.CompletedTask;
            }));

            rows.Add(Measure("E4b_list_default", operation, () =>
            {
                var list = new List<int>();
                for (var i = 0; i < hint; i++) list.Add(i);
                _sink += list.Count;
                return Task.CompletedTask;
            }));

            rows.Add(Measure("E5_full_sync_manual", operation, () =>
            {
                var lease = prepared.Acquire(connection, createParam, null, null, null);
                try
                {
                    var reader = lease.Command.ExecuteReader(behavior);
                    var list = new List<int>(hint);
                    while (reader.Read()) list.Add(mapper(reader));
                    _sink += list.Count;
                    reader.Dispose();
                }
                finally
                {
                    lease.Dispose();
                }

                return Task.CompletedTask;
            }));

            rows.Add(Measure("E6_full_sync", operation, () =>
            {
                var list = preparedInterface.ToList(context);
                _sink += list.Count;
                return Task.CompletedTask;
            }));

            rows.Add(Measure("E7_full_async", operation, async () =>
            {
                var list = await preparedInterface.ToListAsync(context).ConfigureAwait(false);
                _sink += list.Count;
            }));

            rows.Add(Measure("E8_full_async_no_params", operation, async () =>
            {
                var list = await dc.ToListAsync(preparedInterface, null, CancellationToken.None).ConfigureAwait(false);
                _sink += list.Count;
            }));

            rows.Add(Measure("E9_reader_open_async", operation, async () =>
            {
                var lease = prepared.Acquire(connection, createParam, null, null, null);
                try
                {
                    var reader = await lease.Command.ExecuteReaderAsync(behavior, CancellationToken.None).ConfigureAwait(false);
                    _sink += reader.FieldCount;
                    reader.Dispose();
                }
                finally
                {
                    lease.Dispose();
                }
            }));

            rows.Add(Measure("E10_executor_full_async", operation, async () =>
            {
                var list = await executor.ToListAsync(preparedInterface, null, CancellationToken.None).ConfigureAwait(false);
                _sink += list.Count;
            }));

            rows.Add(Measure("E11_full_async_manual", operation, async () =>
            {
                var lease = prepared.Acquire(connection, createParam, null, null, null);
                try
                {
                    var reader = await lease.Command.ExecuteReaderAsync(behavior, CancellationToken.None).ConfigureAwait(false);
                    var list = new List<int>(hint);
                    while (reader.Read()) list.Add(mapper(reader));
                    _sink += list.Count;
                    reader.Dispose();
                }
                finally
                {
                    lease.Dispose();
                }
            }));

            // E12/E13 are floor measurements for the E10 - E11 remainder (step-2 n=3/3 investigation):
            // they allocate no product data, only the return-value machinery an async
            // Task<List<TResult>> terminal must produce. Measurement-only.
            var prebuilt = new List<int>(hint);
            for (var i = 0; i < hint; i++) prebuilt.Add(i);

            rows.Add(Measure("E12_taskresult_prebuilt", operation, async () =>
            {
                var task = Task.FromResult(prebuilt);
                var list = await task.ConfigureAwait(false);
                _sink += list.Count;
            }));

            rows.Add(Measure("E13_async_generic_id", operation, async () =>
            {
                var list = await IdAsync(prebuilt).ConfigureAwait(false);
                _sink += list.Count;
            }));

            recipe[operation] = new RecipeProbe
            {
                RowCount = hint,
                MapperPresent = mapper is not null,
                Behavior = behavior.ToString(),
                NoParams = prepared.NoParams,
                NeedsParamRefresh = prepared.NeedsParamRefresh,
            };
        }

        var payload = new Payload
        {
            Task = "T245",
            Step = "2",
            Unit = "execution-bucket decomposition on the retained R1* prepared command",
            GeneratedUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            Method = "GC.GetAllocatedBytesForCurrentThread delta (precision true) over a batch of 10 logical queries, divided by the batch size; "
                + "a second identical window records GC.GetTotalAllocatedBytes(precise: true); warm-up 3 calls per variant; 10 measured batches. "
                + "Parts are isolated with the production internal seams (Acquire/leased DbCommand/MapDelegate); the full terminals are the real ToList/ToListAsync calls.",
            WarmupIterations = WarmupIterations,
            BatchIterations = BatchIterations,
            BatchCount = BatchCount,
            Harness = new Harness
            {
                DbPath = dbPath,
                DbSha256 = dbSha,
                TieredCompilation = Environment.GetEnvironmentVariable("DOTNET_TieredCompilation") ?? "(default)",
                RuntimeVersion = Environment.Version.ToString(),
                LaunchCommand = Environment.CommandLine,
                Rows = recipe,
            },
            Variants = rows,
            Derived = BuildDerived(rows, recipe),
            Recipe = recipe,
            Sink = _sink,
            Notice = "Measurement-only probe. No product behaviour, acceptance recipe, threshold or denominator changes. "
                + "Its absolute level is the probe's own JIT regime, so it is used for relative per-part attribution only.",
        };

        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        var jsonPath = Path.Combine(outDir, "exec-decomposition.json");
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(payload, jsonOptions));

        var csvPath = Path.Combine(outDir, "exec-decomposition.csv");
        var csv = new StringBuilder();
        csv.AppendLine("operation,variant,bytes_per_query_median,bytes_per_query_min,bytes_per_query_max,bytes_per_query_mean,process_median");
        foreach (var row in rows)
            csv.AppendLine(string.Join(',',
                row.Operation,
                row.Variant,
                row.ThreadMedian.ToString("F2", CultureInfo.InvariantCulture),
                row.ThreadMin.ToString("F2", CultureInfo.InvariantCulture),
                row.ThreadMax.ToString("F2", CultureInfo.InvariantCulture),
                row.ThreadMean.ToString("F2", CultureInfo.InvariantCulture),
                row.ProcessMedian.ToString("F2", CultureInfo.InvariantCulture)));
        File.WriteAllText(csvPath, csv.ToString());

        Console.WriteLine($"{Environment.NewLine}=== T245 step 2 execution-bucket decomposition (B/query) ===");
        foreach (var (operation, rec) in payload.Derived)
        {
            Console.WriteLine(
                $"{operation}: rows={recipe[operation].RowCount} binding={rec.Binding:F2} reader-open={rec.ReaderOpenSync:F2} "
                + $"row-loop={rec.RowReadLoop:F2} map-loop={rec.MapperLoop:F2} list-hint={rec.ListHint:F2} "
                + $"list-default={rec.ListDefault:F2} sync-full={rec.FullSync:F2} sync-manual={rec.FullSyncManual:F2} "
                + $"sync-closure-residual={rec.SyncClosureResidual:F2}");
            Console.WriteLine(
                $"{operation}: async-full={rec.FullAsync:F2} async-manual={rec.FullAsyncManual:F2} "
                + $"params-array={rec.ParamsArray:F2} wrapper={rec.DataContextWrapper:F2} "
                + $"async-reader-open-delta={rec.ReaderOpenAsyncDelta:F2} async-over-sync={rec.AsyncOverSync:F2} "
                + $"async-closure-residual={rec.AsyncClosureResidual:F2}");
        }

        Console.WriteLine($"probe evidence: {jsonPath}");
        return Task.FromResult(0);
    }

    private static Row Measure(string variant, string operation, Func<Task> body)
    {
        for (var i = 0; i < WarmupIterations; i++)
            body().GetAwaiter().GetResult();

        var thread = new long[BatchCount];
        var process = new long[BatchCount];
        for (var batch = 0; batch < BatchCount; batch++)
        {
            var threadBefore = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < BatchIterations; i++)
                body().GetAwaiter().GetResult();
            thread[batch] = GC.GetAllocatedBytesForCurrentThread() - threadBefore;

            var processBefore = GC.GetTotalAllocatedBytes(precise: true);
            for (var i = 0; i < BatchIterations; i++)
                body().GetAwaiter().GetResult();
            process[batch] = GC.GetTotalAllocatedBytes(precise: true) - processBefore;
        }

        var perQuery = thread.Select(static bytes => bytes / (double)BatchIterations).OrderBy(static value => value).ToArray();
        var perQueryProcess = process.Select(static bytes => bytes / (double)BatchIterations).OrderBy(static value => value).ToArray();

        return new Row
        {
            Variant = variant,
            Operation = operation,
            ThreadBatchBytes = thread,
            ProcessBatchBytes = process,
            ThreadMin = perQuery[0],
            ThreadMax = perQuery[^1],
            ThreadMedian = Median(perQuery),
            ThreadMean = perQuery.Average(),
            ProcessMedian = Median(perQueryProcess),
        };
    }

    private static async Task<List<int>> IdAsync(List<int> value)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        return value;
    }

    private static double Median(double[] sorted)
    {
        var middle = sorted.Length / 2;
        return sorted.Length % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2.0;
    }

    private static Dictionary<string, Derived> BuildDerived(List<Row> rows, Dictionary<string, RecipeProbe> recipe)
    {
        var derived = new Dictionary<string, Derived>(StringComparer.Ordinal);
        foreach (var operation in new[] { ExceptOperation, IntersectOperation })
        {
            var byVariant = rows.Where(row => row.Operation == operation)
                .ToDictionary(row => row.Variant, row => row.ThreadMedian, StringComparer.Ordinal);

            var e0 = byVariant["E0_lease_bind"];
            var e1 = byVariant["E1_reader_open_sync"];
            var e2 = byVariant["E2_reader_rows_sync"];
            var e3 = byVariant["E3_reader_map_sync"];
            var e4 = byVariant["E4_list_hint"];
            var e4b = byVariant["E4b_list_default"];
            var e5 = byVariant["E5_full_sync_manual"];
            var e6 = byVariant["E6_full_sync"];
            var e7 = byVariant["E7_full_async"];
            var e8 = byVariant["E8_full_async_no_params"];
            var e9 = byVariant["E9_reader_open_async"];
            var e10 = byVariant["E10_executor_full_async"];
            var e11 = byVariant["E11_full_async_manual"];

            derived[operation] = new Derived
            {
                Binding = e0,
                ReaderOpenSync = e1 - e0,
                RowReadLoop = e2 - e1,
                MapperLoop = e3 - e2,
                ListHint = e4,
                ListDefault = e4b,
                FullSync = e6,
                FullSyncManual = e5,
                SyncClosureResidual = e6 - e3 - e4,
                FullAsync = e7,
                FullAsyncManual = e11,
                ParamsArray = e7 - e8,
                DataContextWrapper = e8 - e10,
                ReaderOpenAsyncDelta = e9 - e1,
                AsyncOverSync = e7 - e6,
                AsyncClosureResidual = e11 - e3 - (e9 - e1) - e4,
            };
        }

        return derived;
    }

    private sealed class Row
    {
        public string Variant { get; set; } = string.Empty;
        public string Operation { get; set; } = string.Empty;
        public long[] ThreadBatchBytes { get; set; } = [];
        public long[] ProcessBatchBytes { get; set; } = [];
        public double ThreadMin { get; set; }
        public double ThreadMax { get; set; }
        public double ThreadMedian { get; set; }
        public double ThreadMean { get; set; }
        public double ProcessMedian { get; set; }
    }

    private sealed class Derived
    {
        public double Binding { get; set; }
        public double ReaderOpenSync { get; set; }
        public double RowReadLoop { get; set; }
        public double MapperLoop { get; set; }
        public double ListHint { get; set; }
        public double ListDefault { get; set; }
        public double FullSync { get; set; }
        public double FullSyncManual { get; set; }
        public double SyncClosureResidual { get; set; }
        public double FullAsync { get; set; }
        public double FullAsyncManual { get; set; }
        public double ParamsArray { get; set; }
        public double DataContextWrapper { get; set; }
        public double ReaderOpenAsyncDelta { get; set; }
        public double AsyncOverSync { get; set; }
        public double AsyncClosureResidual { get; set; }
    }

    private sealed class RecipeProbe
    {
        public int RowCount { get; set; }
        public bool MapperPresent { get; set; }
        public string Behavior { get; set; } = string.Empty;
        public bool NoParams { get; set; }
        public bool NeedsParamRefresh { get; set; }
    }

    private sealed class Harness
    {
        public string DbPath { get; set; } = string.Empty;
        public string DbSha256 { get; set; } = string.Empty;
        public string TieredCompilation { get; set; } = string.Empty;
        public string RuntimeVersion { get; set; } = string.Empty;
        public string LaunchCommand { get; set; } = string.Empty;
        public Dictionary<string, RecipeProbe> Rows { get; set; } = new(StringComparer.Ordinal);
    }

    private sealed class Payload
    {
        public string Task { get; set; } = string.Empty;
        public string Step { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public string GeneratedUtc { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;
        public int WarmupIterations { get; set; }
        public int BatchIterations { get; set; }
        public int BatchCount { get; set; }
        public Harness Harness { get; set; } = new();
        public List<Row> Variants { get; set; } = [];
        public Dictionary<string, Derived> Derived { get; set; } = new(StringComparer.Ordinal);
        public Dictionary<string, RecipeProbe> Recipe { get; set; } = new(StringComparer.Ordinal);
        public long Sink { get; set; }
        public string Notice { get; set; } = string.Empty;
    }
}
