using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Benchmark;

/// <summary>
/// T245 (#245) Step 2 — <b>measurement-only</b> object-level breakdown probe
/// (<c>--t245-step2-breakdown</c>). It does not change product behaviour, the acceptance recipe or any
/// threshold; it splits the two candidate buckets of the accepted Step 1 basis (the set-operation
/// construction rung and the prepare/hash/lookup rung) into the concrete objects that the production
/// code allocates, so DO can give every object a verdict before any product edit.
/// <para>
/// Variants, per set operation, each building a fresh fluent command exactly like the acceptance arm:
/// </para>
/// <list type="bullet">
/// <item><c>S0_construct</c> — both operands plus the set operation, no terminal (the construction rung).</item>
/// <item><c>S1_clone_only</c> — <c>QueryCommand.Clone()</c> of a prebuilt left operand only (the clone shell).</item>
/// <item><c>S2_rawshape</c> — construct plus <c>QueryCommand.CaptureRawShape</c> (the D201-3 raw descriptor).</item>
/// <item><c>S3_rawgraph</c> — construct plus <c>CaptureRawGraph</c>.</item>
/// <item><c>S4_prepare</c> — construct plus <c>PrepareCommand(false, ct)</c>, no plan key and no store.</item>
/// <item><c>S5_prepare_key</c> — construct plus <c>PrepareCommand</c> plus <c>GetOrCreatePlanKey(null)</c>.</item>
/// <item><c>S6_plan_only</c> — construct plus <c>GetPreparedQueryCommand(storeInCache: true)</c>, i.e. the
/// ladder's plan-only rung of the accepted Step 1 basis.</item>
/// <item><c>S7_key_only</c> — construct plus <c>GetOrCreatePlanKey(null)</c> on an unprepared command.</item>
/// </list>
/// <para>
/// Allocation is measured with <see cref="GC.GetAllocatedBytesForCurrentThread"/> deltas plus the
/// process-wide <see cref="GC.GetTotalAllocatedBytes"/> cross-check, exactly like
/// <see cref="T245AttributionProbe"/> and <see cref="T245ReconciliationProbe"/>, so the rows are
/// directly comparable with the accepted Step 1 numbers. The planner's D201-3 recipe counters are read
/// around one warm plan-only call so the report can state which prepare path actually runs.
/// </para>
/// </summary>
internal static class T245Step2BreakdownProbe
{
    private const int WarmupIterations = 3;
    private const int BatchIterations = 10;
    private const int BatchCount = 10;

    private const string ExceptOperation = "EXCEPT";
    private const string IntersectOperation = "INTERSECT";

    // Keeps constructed commands and descriptors reachable so the JIT cannot fold the window away.
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
        ((IConnectionManager)context).EnsureConnectionOpen();

        // Verification pass (not measured): the fluent shape must still produce rows.
        var exceptRows = ctx.SimpleEntity.Select(s => s.Id).Except(ctx.ComplexEntity.Select(c => (int)c.Id)).ToList();
        var intersectRows = ctx.SimpleEntity.Select(s => s.Id).Intersect(ctx.ComplexEntity.Select(c => (int)c.Id)).ToList();
        Console.WriteLine($"verification: {ExceptOperation} rows={exceptRows.Count}, {IntersectOperation} rows={intersectRows.Count}, db={dbPath}");

        // The clone-shell control measures one prebuilt left operand, built outside the window.
        var left = ctx.SimpleEntity.Select(s => s.Id);
        GC.KeepAlive(left);

        // Prime both plan-cache entries so the plan-only arm measures a warm cached path.
        _ = context.GetPreparedQueryCommand(
            ctx.SimpleEntity.Select(s => s.Id).Except(ctx.ComplexEntity.Select(c => (int)c.Id)), false, true, CancellationToken.None);
        _ = context.GetPreparedQueryCommand(
            ctx.SimpleEntity.Select(s => s.Id).Intersect(ctx.ComplexEntity.Select(c => (int)c.Id)), false, true, CancellationToken.None);

        var rows = new List<Row>();
        var recipe = new Dictionary<string, RecipeProbe>(StringComparer.Ordinal);

        foreach (var operation in new[] { ExceptOperation, IntersectOperation })
        {
            var isExcept = operation == ExceptOperation;

            rows.Add(Measure("S0_construct", operation, () =>
            {
                var cmd = isExcept
                    ? ctx.SimpleEntity.Select(s => s.Id).Except(ctx.ComplexEntity.Select(c => (int)c.Id))
                    : ctx.SimpleEntity.Select(s => s.Id).Intersect(ctx.ComplexEntity.Select(c => (int)c.Id));
                _sink += cmd.UnionType == UnionType.None ? 0 : 1;
                GC.KeepAlive(cmd);
                return Task.CompletedTask;
            }));

            rows.Add(Measure("S1_clone_only", operation, () =>
            {
                var cmd = left.Clone();
                GC.KeepAlive(cmd);
                return Task.CompletedTask;
            }));

            rows.Add(Measure("S2_rawshape", operation, () =>
            {
                var cmd = isExcept
                    ? ctx.SimpleEntity.Select(s => s.Id).Except(ctx.ComplexEntity.Select(c => (int)c.Id))
                    : ctx.SimpleEntity.Select(s => s.Id).Intersect(ctx.ComplexEntity.Select(c => (int)c.Id));
                var shape = QueryCommand.CaptureRawShape(cmd);
                GC.KeepAlive(shape);
                GC.KeepAlive(cmd);
                return Task.CompletedTask;
            }));

            rows.Add(Measure("S3_rawgraph", operation, () =>
            {
                var cmd = isExcept
                    ? ctx.SimpleEntity.Select(s => s.Id).Except(ctx.ComplexEntity.Select(c => (int)c.Id))
                    : ctx.SimpleEntity.Select(s => s.Id).Intersect(ctx.ComplexEntity.Select(c => (int)c.Id));
                var graph = cmd.CaptureRawGraph();
                GC.KeepAlive(graph);
                GC.KeepAlive(cmd);
                return Task.CompletedTask;
            }));

            rows.Add(Measure("S4_prepare", operation, () =>
            {
                var cmd = isExcept
                    ? ctx.SimpleEntity.Select(s => s.Id).Except(ctx.ComplexEntity.Select(c => (int)c.Id))
                    : ctx.SimpleEntity.Select(s => s.Id).Intersect(ctx.ComplexEntity.Select(c => (int)c.Id));
                cmd.PrepareCommand(false, CancellationToken.None);
                GC.KeepAlive(cmd);
                return Task.CompletedTask;
            }));

            rows.Add(Measure("S5_prepare_key", operation, () =>
            {
                var cmd = isExcept
                    ? ctx.SimpleEntity.Select(s => s.Id).Except(ctx.ComplexEntity.Select(c => (int)c.Id))
                    : ctx.SimpleEntity.Select(s => s.Id).Intersect(ctx.ComplexEntity.Select(c => (int)c.Id));
                cmd.PrepareCommand(false, CancellationToken.None);
                var key = cmd.GetOrCreatePlanKey(null);
                GC.KeepAlive(key);
                GC.KeepAlive(cmd);
                return Task.CompletedTask;
            }));

            rows.Add(Measure("S6_plan_only", operation, () =>
            {
                var cmd = isExcept
                    ? ctx.SimpleEntity.Select(s => s.Id).Except(ctx.ComplexEntity.Select(c => (int)c.Id))
                    : ctx.SimpleEntity.Select(s => s.Id).Intersect(ctx.ComplexEntity.Select(c => (int)c.Id));
                var prepared = context.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);
                GC.KeepAlive(prepared);
                GC.KeepAlive(cmd);
                return Task.CompletedTask;
            }));

            rows.Add(Measure("S7_key_only", operation, () =>
            {
                var cmd = isExcept
                    ? ctx.SimpleEntity.Select(s => s.Id).Except(ctx.ComplexEntity.Select(c => (int)c.Id))
                    : ctx.SimpleEntity.Select(s => s.Id).Intersect(ctx.ComplexEntity.Select(c => (int)c.Id));
                var key = cmd.GetOrCreatePlanKey(null);
                GC.KeepAlive(key);
                GC.KeepAlive(cmd);
                return Task.CompletedTask;
            }));

            rows.Add(Measure("S8_two_operands", operation, () =>
            {
                var l = ctx.SimpleEntity.Select(s => s.Id);
                var r = ctx.ComplexEntity.Select(c => (int)c.Id);
                _sink += l.IsPrepared || r.IsPrepared ? 0 : 1;
                GC.KeepAlive(l);
                GC.KeepAlive(r);
                return Task.CompletedTask;
            }));

            recipe[operation] = ProbeRecipeCounters(context, ctx, isExcept);
        }

        var payload = new Payload
        {
            Task = "T245",
            Step = "2",
            Unit = "object-level breakdown of the set-op construction and prepare/hash/lookup buckets",
            GeneratedUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            Method = "GC.GetAllocatedBytesForCurrentThread delta (precision true) over a batch of 10 logical queries, divided by the batch size; a second identical window records GC.GetTotalAllocatedBytes(precise: true); warm-up 3 calls per variant; 10 measured batches",
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
            },
            Variants = rows,
            Derived = BuildDerived(rows),
            Recipe = recipe,
            Sink = _sink,
            Notice = "Measurement-only probe. It changes no product behaviour, no acceptance recipe, no threshold and no denominator; its absolute level is the probe's own JIT regime (see the Step 1 reconciliation), so it is used for relative per-object attribution only.",
        };

        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        var jsonPath = Path.Combine(outDir, "probe-step2-breakdown.json");
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(payload, jsonOptions));

        var csvPath = Path.Combine(outDir, "probe-step2-breakdown.csv");
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

        Console.WriteLine($"{Environment.NewLine}=== T245 step 2 object-level breakdown (B/query) ===");
        foreach (var (operation, rec) in payload.Derived)
        {
            Console.WriteLine(
                $"{operation}: construct={rec.Construct:F2} operands={rec.TwoOperands:F2} "
                + $"construct-minus-operands={rec.ConstructMinusOperands:F2} clone-shell={rec.CloneShell:F2} "
                + $"rawshape={rec.RawShape:F2} rawgraph={rec.RawGraph:F2} prepare={rec.Prepare:F2} "
                + $"key-extra={rec.KeyExtra:F2} key-alone={rec.KeyAlone:F2} plan-only={rec.PlanOnly:F2} "
                + $"plan-only-minus-parts={rec.PlanOnlyMinusParts:F2}");
            var probe = payload.Recipe[operation];
            Console.WriteLine(
                $"{operation} recipe: reuses={probe.Reuses} rejections={probe.Rejections} publishes={probe.Publishes} "
                + $"apply-mismatches={probe.ApplyMismatches} prepared-after={probe.PreparedAfter}");
        }

        Console.WriteLine($"probe evidence: {jsonPath}");
        return Task.FromResult(0);
    }

    private static RecipeProbe ProbeRecipeCounters(IDataContext context, TestDataRepository ctx, bool isExcept)
    {
        QueryPlanner.ResetCounters();
        var cmd = isExcept
            ? ctx.SimpleEntity.Select(s => s.Id).Except(ctx.ComplexEntity.Select(c => (int)c.Id))
            : ctx.SimpleEntity.Select(s => s.Id).Intersect(ctx.ComplexEntity.Select(c => (int)c.Id));
        _ = context.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);
        return new RecipeProbe
        {
            Reuses = QueryPlanner.RecipeReuses,
            Rejections = QueryPlanner.RecipeRejections,
            Publishes = QueryPlanner.RecipePublishes,
            ApplyMismatches = QueryPlanner.RecipeApplyMismatches,
            PreparedAfter = cmd.IsPrepared,
        };
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

    private static double Median(double[] sorted)
    {
        var middle = sorted.Length / 2;
        return sorted.Length % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2.0;
    }

    private static Dictionary<string, Derived> BuildDerived(List<Row> rows)
    {
        var derived = new Dictionary<string, Derived>(StringComparer.Ordinal);
        foreach (var operation in new[] { ExceptOperation, IntersectOperation })
        {
            var byVariant = rows.Where(row => row.Operation == operation)
                .ToDictionary(row => row.Variant, row => row.ThreadMedian, StringComparer.Ordinal);

            var s0 = byVariant["S0_construct"];
            var s2 = byVariant["S2_rawshape"];
            var s3 = byVariant["S3_rawgraph"];
            var s4 = byVariant["S4_prepare"];
            var s5 = byVariant["S5_prepare_key"];
            var s6 = byVariant["S6_plan_only"];
            var s7 = byVariant["S7_key_only"];

            derived[operation] = new Derived
            {
                Construct = s0,
                TwoOperands = byVariant["S8_two_operands"],
                ConstructMinusOperands = s0 - byVariant["S8_two_operands"],
                CloneShell = byVariant["S1_clone_only"],
                RawShape = s2 - s0,
                RawGraph = s3 - s0,
                Prepare = s4 - s0,
                KeyExtra = s5 - s4,
                KeyAlone = s7 - s0,
                PlanOnly = s6 - s0,
                PlanOnlyMinusParts = s6 - s4 - (s5 - s4) - (s2 - s0) - (s3 - s0),
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
        public double Construct { get; set; }
        public double TwoOperands { get; set; }
        public double ConstructMinusOperands { get; set; }
        public double CloneShell { get; set; }
        public double RawShape { get; set; }
        public double RawGraph { get; set; }
        public double Prepare { get; set; }
        public double KeyExtra { get; set; }
        public double KeyAlone { get; set; }
        public double PlanOnly { get; set; }
        public double PlanOnlyMinusParts { get; set; }
    }

    private sealed class RecipeProbe
    {
        public int Reuses { get; set; }
        public int Rejections { get; set; }
        public int Publishes { get; set; }
        public int ApplyMismatches { get; set; }
        public bool PreparedAfter { get; set; }
    }

    private sealed class Harness
    {
        public string DbPath { get; set; } = string.Empty;
        public string DbSha256 { get; set; } = string.Empty;
        public string TieredCompilation { get; set; } = string.Empty;
        public string RuntimeVersion { get; set; } = string.Empty;
        public string LaunchCommand { get; set; } = string.Empty;
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
