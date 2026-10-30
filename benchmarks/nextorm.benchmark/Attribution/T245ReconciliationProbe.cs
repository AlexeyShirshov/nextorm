using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Benchmark;

/// <summary>
/// T245 (#245) Step 1 evidence completion — <b>measurement-only</b> reconciliation probe
/// (<c>--t245-reconcile</c>). It does not change any product behaviour, does not change the
/// acceptance benchmark recipe and does not change any threshold; it exists only to measure, in
/// <b>one</b> process and against the <b>same</b> pinned database, the exact bodies of the BDN arms
/// that the Step 1 attribution probe compares itself with:
/// <list type="bullet">
/// <item><c>full_method_10loop</c> — the exact body of
/// <see cref="SqliteBenchmarkFeaturesFairCached.B_Nextorm_Cached_Except"/> /
/// <c>_Intersect</c> as an <c>async Task</c> method (10 queries per invocation).</item>
/// <item><c>full_lambda_10loop</c> — the same body as an <c>async</c> lambda invoked through
/// <c>Func&lt;Task&gt;</c>, i.e. the shape <see cref="T245AttributionProbe"/>'s <c>V0_full</c> uses.</item>
/// <item><c>construct_ladder_*</c> — the exact body of
/// <see cref="SqliteBenchmarkWarmDecompose.Except_Construct"/> / <c>Intersect_Construct</c>
/// (a plain method, no ternary), measured as 1 call per window, as a 10-call loop and as a
/// 100-call loop.</item>
/// <item><c>construct_ternary_*</c> — <see cref="T245AttributionProbe"/>'s <c>V4e_full_repro</c>
/// body (ternary over the set operation), measured with the same three window shapes.</item>
/// <item><c>prepared_10loop</c> — the exact body of
/// <see cref="SqliteBenchmarkFeaturesFair.A_Nextorm_Prepared_Except"/> / <c>_Intersect</c>.</item>
/// <item><c>planonly_10loop</c> / <c>planonly_100loop</c> — the exact body of
/// <see cref="SqliteBenchmarkWarmDecompose.Except_Warm_PlanOnly"/> / <c>Intersect_Warm_PlanOnly</c>.</item>
/// </list>
/// Allocation is measured with <see cref="GC.GetAllocatedBytesForCurrentThread"/> deltas plus the
/// process-wide <see cref="GC.GetTotalAllocatedBytes"/> cross-check, exactly like
/// <see cref="T245AttributionProbe"/>, so the two probes are directly comparable.
/// </summary>
internal static class T245ReconciliationProbe
{
    private const int WarmupIterations = 3;
    private const int BatchIterations = 10;
    private const int BatchCount = 10;
    private const int LadderIterations = 100;

    private const string ExceptOperation = "EXCEPT";
    private const string IntersectOperation = "INTERSECT";

    // This-round (rv3, 2026-10-30) BDN values the reconciliation explains, B/query, EXCEPT/INTERSECT.
    private static readonly double[] BdnFull = { 7112.1, 7096.1 };
    private static readonly double[] BdnConstruct = { 4184.18, 4184.18 };
    private static readonly double[] BdnPrepared = { 760.0, 744.0 };
    private static readonly double[] BdnPlanOnly = { 6432.31, 6432.28 };

    private static long _sink;

    /// <summary>Runs every reconciliation variant for both set operations and writes the evidence files.</summary>
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
        ((IConnectionManager)context).EnsureConnectionOpen();

        var exceptRows = ctx.SimpleEntity.Select(s => s.Id).Except(ctx.ComplexEntity.Select(c => (int)c.Id)).ToList();
        var intersectRows = ctx.SimpleEntity.Select(s => s.Id).Intersect(ctx.ComplexEntity.Select(c => (int)c.Id)).ToList();
        Console.WriteLine($"verification: {ExceptOperation} rows={exceptRows.Count}, {IntersectOperation} rows={intersectRows.Count}, db={dbPath}");

        // Prime both plan-cache entries exactly as T245AttributionProbe does.
        _ = context.GetPreparedQueryCommand(BuildExcept(ctx), false, true, CancellationToken.None);
        _ = context.GetPreparedQueryCommand(BuildIntersect(ctx), false, true, CancellationToken.None);

        var preparedExcept = BuildExcept(ctx).Prepare();
        var preparedIntersect = BuildIntersect(ctx).Prepare();

        var payload = new Payload
        {
            GeneratedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            Db = dbPath,
            DbSha256 = dbSha,
            Argv = Environment.GetCommandLineArgs(),
            Method = "GC.GetAllocatedBytesForCurrentThread delta over a window of logical queries divided by "
                + "the window size (" + BatchIterations + " logical queries per batch window at queriesPerCall=1), "
                + BatchCount + " measured batches; process-wide GC.GetTotalAllocatedBytes(precise: true) twin window; "
                + WarmupIterations + " warm-up calls",
            Notice = "Measurement-only reconciliation probe: no product behaviour, no benchmark-recipe and no "
                + "threshold change. It runs no BenchmarkDotNet job; the numbers are plain in-process counters.",
        };

        var rows = new List<Row>();
        var operations = new[] { ExceptOperation, IntersectOperation };
        for (var index = 0; index < operations.Length; index++)
        {
            var operation = operations[index];
            var isExcept = index == 0;

            rows.Add(Measure("full_method_10loop", operation,
                () => FullBodyAsync(ctx, isExcept), queriesPerCall: BatchIterations));

            rows.Add(Measure("full_lambda_10loop", operation, isExcept
                ? async () =>
                {
                    for (var i = 0; i < BatchIterations; i++)
                        foreach (var row in await ctx.SimpleEntity.Select(s => s.Id)
                            .Except(ctx.ComplexEntity.Select(c => (int)c.Id)).ToListAsync())
                            _sink++;
                }
                : async () =>
                {
                    for (var i = 0; i < BatchIterations; i++)
                        foreach (var row in await ctx.SimpleEntity.Select(s => s.Id)
                            .Intersect(ctx.ComplexEntity.Select(c => (int)c.Id)).ToListAsync())
                            _sink++;
                }, queriesPerCall: BatchIterations));

            rows.Add(Measure("construct_ladder_1call", operation, () =>
            {
                var cmd = isExcept ? BuildExcept(ctx) : BuildIntersect(ctx);
                GC.KeepAlive(cmd);
                return Task.CompletedTask;
            }));

            rows.Add(Measure("construct_ladder_10loop", operation, () =>
            {
                for (var i = 0; i < BatchIterations; i++)
                {
                    var cmd = isExcept ? BuildExcept(ctx) : BuildIntersect(ctx);
                    GC.KeepAlive(cmd);
                }

                return Task.CompletedTask;
            }, queriesPerCall: BatchIterations));

            rows.Add(Measure("construct_ladder_100loop", operation, () =>
            {
                for (var i = 0; i < LadderIterations; i++)
                {
                    var cmd = isExcept ? BuildExcept(ctx) : BuildIntersect(ctx);
                    GC.KeepAlive(cmd);
                }

                return Task.CompletedTask;
            }, queriesPerCall: LadderIterations));

            rows.Add(Measure("construct_ternary_1call", operation, () =>
            {
                var cmd = isExcept
                    ? ctx.SimpleEntity.Select(s => s.Id).Except(ctx.ComplexEntity.Select(c => (int)c.Id))
                    : ctx.SimpleEntity.Select(s => s.Id).Intersect(ctx.ComplexEntity.Select(c => (int)c.Id));
                GC.KeepAlive(cmd);
                return Task.CompletedTask;
            }));

            rows.Add(Measure("construct_ternary_100loop", operation, () =>
            {
                for (var i = 0; i < LadderIterations; i++)
                {
                    var cmd = isExcept
                        ? ctx.SimpleEntity.Select(s => s.Id).Except(ctx.ComplexEntity.Select(c => (int)c.Id))
                        : ctx.SimpleEntity.Select(s => s.Id).Intersect(ctx.ComplexEntity.Select(c => (int)c.Id));
                    GC.KeepAlive(cmd);
                }

                return Task.CompletedTask;
            }, queriesPerCall: LadderIterations));

            rows.Add(Measure("prepared_10loop", operation,
                () => PreparedBodyAsync(ctx, isExcept, preparedExcept, preparedIntersect),
                queriesPerCall: BatchIterations));

            rows.Add(Measure("planonly_10loop", operation, () =>
            {
                for (var i = 0; i < BatchIterations; i++)
                {
                    var prepared = context.GetPreparedQueryCommand(
                        isExcept ? BuildExcept(ctx) : BuildIntersect(ctx), false, true, CancellationToken.None);
                    GC.KeepAlive(prepared);
                }

                return Task.CompletedTask;
            }, queriesPerCall: BatchIterations));

            rows.Add(Measure("planonly_100loop", operation, () =>
            {
                for (var i = 0; i < LadderIterations; i++)
                {
                    var prepared = context.GetPreparedQueryCommand(
                        isExcept ? BuildExcept(ctx) : BuildIntersect(ctx), false, true, CancellationToken.None);
                    GC.KeepAlive(prepared);
                }

                return Task.CompletedTask;
            }, queriesPerCall: LadderIterations));
        }

        payload.Rows = rows;
        payload.Derived = BuildDerived(rows);

        var jsonPath = Path.Combine(outDir, "probe-reconcile.json");
        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        };
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(payload, jsonOptions));

        var csv = new StringBuilder("variant,operation,batch,queries_per_window,thread_bytes,process_total_bytes,bytes_per_query\n");
        foreach (var row in rows)
            for (var b = 0; b < row.ThreadBatchBytes.Length; b++)
                csv.Append(row.Variant).Append(',')
                    .Append(row.Operation).Append(',')
                    .Append(b).Append(',')
                    .Append(row.QueriesPerWindow).Append(',')
                    .Append(row.ThreadBatchBytes[b]).Append(',')
                    .Append(row.ProcessBatchBytes[b]).Append(',')
                    .Append(row.BytesPerQuery[b].ToString("F2", CultureInfo.InvariantCulture))
                    .Append('\n');
        File.WriteAllText(Path.Combine(outDir, "probe-reconcile.csv"), csv.ToString());

        Console.WriteLine($"{"variant",-26} {"operation",-10} {"B/query min",12} {"median",12} {"max",12}");
        foreach (var row in rows)
        {
            var sorted = row.BytesPerQuery.OrderBy(static value => value).ToArray();
            Console.WriteLine(
                $"{row.Variant,-26} {row.Operation,-10} {sorted[0],12:F2} {Median(sorted),12:F2} {sorted[^1],12:F2}");
        }

        Console.WriteLine();
        foreach (var (operation, rec) in payload.Derived)
        {
            Console.WriteLine(
                $"{operation} gaps vs BDN: full_method={rec.GapFullMethod:F2} full_lambda={rec.GapFullLambda:F2} "
                + $"construct_1call={rec.GapConstructOneCall:F2} construct_10loop={rec.GapConstructTenLoop:F2} "
                + $"construct_100loop={rec.GapConstructHundredLoop:F2} prepared={rec.GapPrepared:F2} "
                + $"ternary_minus_ladder_1call={rec.TernaryMinusLadderOneCall:F2} "
                + $"ternary_minus_ladder_100loop={rec.TernaryMinusLadderHundredLoop:F2} "
                + $"loop100_minus_1call={rec.ConstructLoopHundredMinusOneCall:F2} "
                + $"inproc_planonly_plus_prepared_minus_full={rec.InProcessPlanOnlyPlusPreparedMinusFull:F2}");
        }

        Console.WriteLine($"probe evidence: {jsonPath}");
        return Task.FromResult(0);
    }

    private static async Task FullBodyAsync(TestDataRepository ctx, bool isExcept)
    {
        for (var i = 0; i < BatchIterations; i++)
        {
            if (isExcept)
            {
                foreach (var row in await ctx.SimpleEntity.Select(s => s.Id)
                    .Except(ctx.ComplexEntity.Select(c => (int)c.Id)).ToListAsync())
                    _sink++;
            }
            else
            {
                foreach (var row in await ctx.SimpleEntity.Select(s => s.Id)
                    .Intersect(ctx.ComplexEntity.Select(c => (int)c.Id)).ToListAsync())
                    _sink++;
            }
        }
    }

    private static async Task PreparedBodyAsync(
        TestDataRepository ctx, bool isExcept, IPreparedQueryCommand<int> except, IPreparedQueryCommand<int> intersect)
    {
        for (var i = 0; i < BatchIterations; i++)
        {
            var prepared = isExcept ? except : intersect;
            foreach (var row in await prepared.ToListAsync(ctx.DataContext)) _sink++;
        }
    }

    private static QueryCommand<int> BuildExcept(TestDataRepository ctx)
        => ctx.SimpleEntity.Select(s => s.Id).Except(ctx.ComplexEntity.Select(c => (int)c.Id));

    private static QueryCommand<int> BuildIntersect(TestDataRepository ctx)
        => ctx.SimpleEntity.Select(s => s.Id).Intersect(ctx.ComplexEntity.Select(c => (int)c.Id));

    private static Row Measure(string variant, string operation, Func<Task> body, int queriesPerCall = 1)
    {
        var warmupCalls = Math.Max(1, (WarmupIterations + queriesPerCall - 1) / queriesPerCall);
        for (var i = 0; i < warmupCalls; i++)
            body().GetAwaiter().GetResult();

        var callsPerWindow = Math.Max(1, BatchIterations / queriesPerCall);
        var thread = new long[BatchCount];
        var process = new long[BatchCount];
        var perQuery = new double[BatchCount];
        for (var batch = 0; batch < BatchCount; batch++)
        {
            var threadBefore = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < callsPerWindow; i++)
                body().GetAwaiter().GetResult();
            thread[batch] = GC.GetAllocatedBytesForCurrentThread() - threadBefore;

            var processBefore = GC.GetTotalAllocatedBytes(precise: true);
            for (var i = 0; i < callsPerWindow; i++)
                body().GetAwaiter().GetResult();
            process[batch] = GC.GetTotalAllocatedBytes(precise: true) - processBefore;

            // Every window issues exactly callsPerWindow * queriesPerCall logical queries, which is
            // BatchIterations for the 1-call and 10-call shapes and LadderIterations for the 100-call shape.
            perQuery[batch] = thread[batch] / (double)(callsPerWindow * queriesPerCall);
        }

        return new Row
        {
            Variant = variant,
            Operation = operation,
            QueriesPerWindow = queriesPerCall,
            CallsPerWindow = callsPerWindow,
            ThreadBatchBytes = thread,
            ProcessBatchBytes = process,
            BytesPerQuery = perQuery,
        };
    }

    private static Dictionary<string, Derived> BuildDerived(List<Row> rows)
    {
        var derived = new Dictionary<string, Derived>(StringComparer.Ordinal);
        var operations = new[] { ExceptOperation, IntersectOperation };
        for (var index = 0; index < operations.Length; index++)
        {
            var operation = operations[index];
            var byVariant = rows.Where(row => row.Operation == operation)
                .ToDictionary(row => row.Variant, row => Median(row.BytesPerQuery.OrderBy(static value => value).ToArray()), StringComparer.Ordinal);

            var fullMethod = byVariant["full_method_10loop"];
            var fullLambda = byVariant["full_lambda_10loop"];
            var constructOne = byVariant["construct_ladder_1call"];
            var constructTen = byVariant["construct_ladder_10loop"];
            var constructHundred = byVariant["construct_ladder_100loop"];
            var constructTernaryOne = byVariant["construct_ternary_1call"];
            var constructTernaryHundred = byVariant["construct_ternary_100loop"];
            var prepared = byVariant["prepared_10loop"];
            var planOnlyTen = byVariant["planonly_10loop"];
            var planOnlyHundred = byVariant["planonly_100loop"];

            derived[operation] = new Derived
            {
                FullMethod = fullMethod,
                FullLambda = fullLambda,
                ConstructLadderOneCall = constructOne,
                ConstructLadderTenLoop = constructTen,
                ConstructLadderHundredLoop = constructHundred,
                ConstructTernaryOneCall = constructTernaryOne,
                ConstructTernaryHundredLoop = constructTernaryHundred,
                Prepared = prepared,
                PlanOnlyTenLoop = planOnlyTen,
                PlanOnlyHundredLoop = planOnlyHundred,
                GapFullMethod = fullMethod - BdnFull[index],
                GapFullLambda = fullLambda - BdnFull[index],
                GapConstructOneCall = constructOne - BdnConstruct[index],
                GapConstructTenLoop = constructTen - BdnConstruct[index],
                GapConstructHundredLoop = constructHundred - BdnConstruct[index],
                GapPrepared = prepared - BdnPrepared[index],
                GapPlanOnly = planOnlyHundred - BdnPlanOnly[index],
                TernaryMinusLadderOneCall = constructTernaryOne - constructOne,
                TernaryMinusLadderHundredLoop = constructTernaryHundred - constructHundred,
                ConstructLoopHundredMinusOneCall = constructHundred - constructOne,
                InProcessPlanOnlyPlusPreparedMinusFull = (planOnlyHundred + prepared) - fullMethod,
                InProcessPlanOnlyTenPlusPreparedMinusFull = (planOnlyTen + prepared) - fullMethod,
                InProcessPrepareDeltaHundred = planOnlyHundred - constructHundred,
                InProcessPrepareDeltaTen = planOnlyTen - constructTen,
            };
        }

        return derived;
    }

    private static double Median(double[] sorted)
    {
        var middle = sorted.Length / 2;
        return sorted.Length % 2 == 1
            ? sorted[middle]
            : (sorted[middle - 1] + sorted[middle]) / 2d;
    }

    private sealed class Payload
    {
        public string GeneratedUtc { get; set; } = string.Empty;
        public string Db { get; set; } = string.Empty;
        public string DbSha256 { get; set; } = string.Empty;
        public string[] Argv { get; set; } = Array.Empty<string>();
        public string Method { get; set; } = string.Empty;
        public string Notice { get; set; } = string.Empty;
        public List<Row> Rows { get; set; } = new();
        public Dictionary<string, Derived> Derived { get; set; } = new(StringComparer.Ordinal);
    }

    private sealed class Row
    {
        public string Variant { get; set; } = string.Empty;
        public string Operation { get; set; } = string.Empty;
        public int QueriesPerWindow { get; set; }
        public int CallsPerWindow { get; set; }
        public long[] ThreadBatchBytes { get; set; } = Array.Empty<long>();
        public long[] ProcessBatchBytes { get; set; } = Array.Empty<long>();
        public double[] BytesPerQuery { get; set; } = Array.Empty<double>();
    }

    private sealed class Derived
    {
        public double FullMethod { get; set; }
        public double FullLambda { get; set; }
        public double ConstructLadderOneCall { get; set; }
        public double ConstructLadderTenLoop { get; set; }
        public double ConstructLadderHundredLoop { get; set; }
        public double ConstructTernaryOneCall { get; set; }
        public double ConstructTernaryHundredLoop { get; set; }
        public double Prepared { get; set; }
        public double PlanOnlyTenLoop { get; set; }
        public double PlanOnlyHundredLoop { get; set; }
        public double GapFullMethod { get; set; }
        public double GapFullLambda { get; set; }
        public double GapConstructOneCall { get; set; }
        public double GapConstructTenLoop { get; set; }
        public double GapConstructHundredLoop { get; set; }
        public double GapPrepared { get; set; }
        public double GapPlanOnly { get; set; }
        public double TernaryMinusLadderOneCall { get; set; }
        public double TernaryMinusLadderHundredLoop { get; set; }
        public double ConstructLoopHundredMinusOneCall { get; set; }
        public double InProcessPlanOnlyPlusPreparedMinusFull { get; set; }
        public double InProcessPlanOnlyTenPlusPreparedMinusFull { get; set; }
        public double InProcessPrepareDeltaHundred { get; set; }
        public double InProcessPrepareDeltaTen { get; set; }
    }
}
