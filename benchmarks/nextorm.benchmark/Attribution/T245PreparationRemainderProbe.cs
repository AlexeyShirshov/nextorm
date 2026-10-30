using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Benchmark;

/// <summary>
/// T245 (#245) Block 3 — measurement-only decomposition of the held `R1` preparation rung
/// remainder (`≈296 B/query` product-unknown after `RawShape 1 328 + plan key ≈80`).
/// (`--t245-prep-remainder`). Benchmark-project-only; no product change.
/// </summary>
internal static class T245PreparationRemainderProbe
{
    private const int WarmupIterations = 3;
    private const int BatchIterations = 10;
    private const int BatchCount = 10;

    private const string ExceptOperation = "EXCEPT";
    private const string IntersectOperation = "INTERSECT";

    private static long _sink;

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

        // Prime plan cache for both operations.
        _ = context.GetPreparedQueryCommand(
            ctx.SimpleEntity.Select(s => s.Id).Except(ctx.ComplexEntity.Select(c => (int)c.Id)), false, true, CancellationToken.None);
        _ = context.GetPreparedQueryCommand(
            ctx.SimpleEntity.Select(s => s.Id).Intersect(ctx.ComplexEntity.Select(c => (int)c.Id)), false, true, CancellationToken.None);

        var rows = new List<PrepRow>();

        foreach (var operation in new[] { ExceptOperation, IntersectOperation })
        {
            var isExcept = operation == ExceptOperation;

            Func<QueryCommand<int>> mkCmd = () => isExcept
                ? ctx.SimpleEntity.Select(s => s.Id).Except(ctx.ComplexEntity.Select(c => (int)c.Id))
                : ctx.SimpleEntity.Select(s => s.Id).Intersect(ctx.ComplexEntity.Select(c => (int)c.Id));

            // S0: full plan-only (baseline 1704)
            rows.Add(Measure("S0_planonly", operation, () =>
            {
                var cmd = mkCmd();
                var prepared = context.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);
                GC.KeepAlive(prepared);
                GC.KeepAlive(cmd);
                return Task.CompletedTask;
            }));

            // S1: CaptureRawShape only (1328)
            rows.Add(Measure("S1_rawshape", operation, () =>
            {
                var cmd = mkCmd();
                var shape = QueryCommand.CaptureRawShape(cmd);
                GC.KeepAlive(shape);
                GC.KeepAlive(cmd);
                return Task.CompletedTask;
            }));

            // S2: TryGetRaw lookup only (primed store)
            rows.Add(Measure("S2_trygetraw", operation, () =>
            {
                var cmd = mkCmd();
                var shape = QueryCommand.CaptureRawShape(cmd);
                if (shape is not null)
                {
                    var hit = QueryPlanStore.TryGetRaw(context.GetType(), shape, cmd, out var plan);
                    _sink += hit ? 1 : 0;
                    GC.KeepAlive(plan);
                }
                GC.KeepAlive(cmd);
                return Task.CompletedTask;
            }));

            // S3: CaptureRawGraph only (product allocation; expect 0)
            rows.Add(Measure("S3_rawgraph", operation, () =>
            {
                var cmd = mkCmd();
                var g = cmd.CaptureRawGraph();
                // avoid boxing by not GC.KeepAlive the struct via object; use field access
                _sink += g.Union is null ? 0 : 1;
                GC.KeepAlive(cmd);
                return Task.CompletedTask;
            }));

            // S4: ApplyPreparationFrom (clone.CopyTo) — needs a stored plan
            rows.Add(Measure("S4_applyprep", operation, () =>
            {
                var cmd = mkCmd();
                var shape = QueryCommand.CaptureRawShape(cmd);
                if (shape is not null && QueryPlanStore.TryGetRaw(context.GetType(), shape, cmd, out var plan) && plan is not null)
                {
                    var raw = cmd.CaptureRawGraph();
                    cmd.ApplyPreparationFrom(plan.QueryCommand);
                    GC.KeepAlive(raw);
                }
                GC.KeepAlive(cmd);
                return Task.CompletedTask;
            }));

            // S5: GetOrCreatePlanKey after ApplyPreparationFrom
            rows.Add(Measure("S5_key_after_apply", operation, () =>
            {
                var cmd = mkCmd();
                var shape = QueryCommand.CaptureRawShape(cmd);
                if (shape is not null && QueryPlanStore.TryGetRaw(context.GetType(), shape, cmd, out var plan) && plan is not null)
                {
                    var raw = cmd.CaptureRawGraph();
                    cmd.ApplyPreparationFrom(plan.QueryCommand);
                    var key = cmd.GetOrCreatePlanKey(null);
                    GC.KeepAlive(key);
                    GC.KeepAlive(raw);
                }
                GC.KeepAlive(cmd);
                return Task.CompletedTask;
            }));

            // S6: GetOrCreatePlanKey alone on fresh (unprepared) command
            rows.Add(Measure("S6_key_alone", operation, () =>
            {
                var cmd = mkCmd();
                var key = cmd.GetOrCreatePlanKey(null);
                GC.KeepAlive(key);
                GC.KeepAlive(cmd);
                return Task.CompletedTask;
            }));
        }

        var payload = new
        {
            Task = "T245",
            Block = "3",
            Unit = "preparation rung remainder decomposition (B/query)",
            GeneratedUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            Method = "GC.GetAllocatedBytesForCurrentThread delta over 10 logical queries / 10, median of 10 batches; held regime via env",
            Harness = new
            {
                DbPath = dbPath,
                DbSha256 = dbSha,
                TieredCompilation = Environment.GetEnvironmentVariable("DOTNET_TieredCompilation") ?? "(default)",
                RuntimeVersion = Environment.Version.ToString(),
                LaunchCommand = Environment.CommandLine,
            },
            Variants = rows,
            Sink = _sink,
        };

        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        var jsonPath = Path.Combine(outDir, "prep-remainder.json");
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(payload, jsonOptions));

        var csvPath = Path.Combine(outDir, "prep-remainder.csv");
        var csv = new StringBuilder();
        csv.AppendLine("operation,variant,bytes_per_query_median,bytes_per_query_min,bytes_per_query_max,bytes_per_query_mean");
        foreach (var row in rows)
            csv.AppendLine(string.Join(',',
                row.Operation,
                row.Variant,
                row.ThreadMedian.ToString("F2", CultureInfo.InvariantCulture),
                row.ThreadMin.ToString("F2", CultureInfo.InvariantCulture),
                row.ThreadMax.ToString("F2", CultureInfo.InvariantCulture),
                row.ThreadMean.ToString("F2", CultureInfo.InvariantCulture)));

        File.WriteAllText(csvPath, csv.ToString());

        Console.WriteLine($"{Environment.NewLine}=== T245 block 3 preparation remainder (B/query) ===");
        foreach (var op in new[] { ExceptOperation, IntersectOperation })
        {
            Console.WriteLine(op);
            foreach (var r in rows.Where(x => x.Operation == op))
                Console.WriteLine($"  {r.Variant}: median={r.ThreadMedian:F2} min={r.ThreadMin:F2} max={r.ThreadMax:F2}");
        }
        Console.WriteLine($"probe evidence: {jsonPath}");
        return Task.FromResult(0);
    }

    private static PrepRow Measure(string variant, string operation, Func<Task> body)
    {
        for (var i = 0; i < WarmupIterations; i++)
            body().GetAwaiter().GetResult();

        var thread = new long[BatchCount];
        for (var batch = 0; batch < BatchCount; batch++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < BatchIterations; i++)
                body().GetAwaiter().GetResult();
            thread[batch] = GC.GetAllocatedBytesForCurrentThread() - before;
        }

        var perQuery = thread.Select(static bytes => bytes / (double)BatchIterations).OrderBy(static value => value).ToArray();

        return new PrepRow
        {
            Variant = variant,
            Operation = operation,
            ThreadBatchBytes = thread,
            ThreadMin = perQuery[0],
            ThreadMax = perQuery[^1],
            ThreadMedian = Median(perQuery),
            ThreadMean = perQuery.Average(),
        };
    }

    private static double Median(double[] sorted)
    {
        var middle = sorted.Length / 2;
        return sorted.Length % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2.0;
    }
}

internal class PrepRow
{
    public string Variant { get; set; } = "";
    public string Operation { get; set; } = "";
    public long[] ThreadBatchBytes { get; set; } = Array.Empty<long>();
    public double ThreadMin { get; set; }
    public double ThreadMax { get; set; }
    public double ThreadMedian { get; set; }
    public double ThreadMean { get; set; }
}
