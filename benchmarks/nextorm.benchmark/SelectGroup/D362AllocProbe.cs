using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Benchmark;

/// <summary>
/// Temporary PF3 allocation probe: measures the per-call managed allocation of the raw-shape capture for
/// a plain (component-free) GroupBy command, and the size of the objects the closure scanner used to
/// allocate. The same source is copied into the baseline twin; the candidate/baseline difference names
/// the regression.
/// </summary>
public static class D362AllocProbe
{
    private const string DbPath = "/tmp/nextorm.d362.allocprobe.db";

    public static int Run()
    {
        D362ControlFixture.Seed(DbPath);
        using var ctx = new SqliteDataContext($"Data Source='{DbPath}'", new DataContextBuilder());

        QueryCommand<D362ProbeRow> Build() => ctx.From<D362BenchRow>()
            .GroupBy(x => x.Category)
            .Select(x => new D362ProbeRow { Category = x.Category, Total = x.Score });

        // Warm the caches and the raw-shape path.
        for (var i = 0; i < 50; i++)
        {
            var warm = Build();
            _ = QueryCommand.CaptureRawShape(warm);
        }

        var b0 = GC.GetAllocatedBytesForCurrentThread();
        _ = new List<object>();
        var listSize = GC.GetAllocatedBytesForCurrentThread() - b0;

        const int iterations = 1000;
        var b1 = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < iterations; i++)
        {
            var cmd = Build();
            _ = QueryCommand.CaptureRawShape(cmd);
        }
        var perCall = (GC.GetAllocatedBytesForCurrentThread() - b1) / (double)iterations;

        var b2 = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < iterations; i++)
        {
            _ = Build();
        }
        var constructPerCall = (GC.GetAllocatedBytesForCurrentThread() - b2) / (double)iterations;

        // The benchmark arm body: fresh command + warm raw-cache hit through the planner.
        ctx.PurgeQueryCache();
        for (var i = 0; i < 50; i++)
        {
            var warm = Build();
            _ = ctx.GetPreparedQueryCommand(warm, false, true, CancellationToken.None);
        }
        var b3 = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < iterations; i++)
        {
            var cmd = Build();
            _ = ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);
        }
        var warmHitPerCall = (GC.GetAllocatedBytesForCurrentThread() - b3) / (double)iterations;

        // The exact benchmark arm shape (anonymous projection), measured in three rounds.
        ctx.PurgeQueryCache();
        for (var i = 0; i < 50; i++)
        {
            var warm = ctx.From<D362BenchRow>().GroupBy(x => x.Category)
                .Select(x => new { x.Category, Total = x.Score });
            _ = ctx.GetPreparedQueryCommand(warm, false, true, CancellationToken.None);
        }
        var anon = new List<double>(3);
        for (var round = 0; round < 3; round++)
        {
            var b4 = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < iterations; i++)
            {
                var cmd = ctx.From<D362BenchRow>().GroupBy(x => x.Category)
                    .Select(x => new { x.Category, Total = x.Score });
                _ = ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);
            }
            anon.Add((GC.GetAllocatedBytesForCurrentThread() - b4) / (double)iterations);
        }

        Console.WriteLine($"D362-ALLOC-PROBE list_object={listSize} capture_per_call={perCall:F2} construct_per_call={constructPerCall:F2} warmhit_per_call={warmHitPerCall:F2} anon_rounds={string.Join(",", anon.Select(x => x.ToString("F2")))}");
        return 0;
    }
}

public sealed class D362ProbeRow
{
    public string? Category { get; set; }
    public int? Total { get; set; }
}
