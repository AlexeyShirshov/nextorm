using System.Globalization;
using System.Text;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Benchmark;

/// <summary>
/// Issue #319 preparation-path probe: decomposes the warm plan-cache hit of a fresh command into
/// construction, raw fingerprint, raw lookup, prepared-state apply and plan-key building.
/// Benchmark-project-only (<c>--issue319-prep-probe &lt;outDir&gt;</c>); it changes no product behaviour
/// and runs no BenchmarkDotNet job.
/// <para>
/// The hit path of a fresh command is not "look up a key": a fresh command has no prepared state, so
/// the planner derives an allocation-free raw fingerprint, finds the published candidate, applies the
/// cached prepared state and then validates the normalized key. This probe attributes each of those
/// steps; the descriptor capture it used to measure happens on the miss only and is published for the
/// next hit.
/// </para>
/// </summary>
internal static class Issue319PreparationProbe
{
    private const int WarmupIterations = 5;
    private const int BatchCount = 15;
    private const int BatchIterations = 10;

    private static long _sink;

    public static Task<int> RunAsync(string outDir)
    {
        Directory.CreateDirectory(outDir);

        var builder = new DataContextBuilder();
        builder.UseSqlite(BenchDb.FilePath);
        var context = builder.CreateDataContext();
        var ctx = new TestDataRepository(context);
        ((IConnectionManager)context).EnsureConnectionOpen();

        // Prime the plan cache for every arm (miss once, so every measured call is a hit).
        _ = context.GetPreparedQueryCommand(BuildCte(ctx), false, true, CancellationToken.None);
        _ = context.GetPreparedQueryCommand(BuildRecursiveCte(ctx, context), false, true, CancellationToken.None);
        _ = context.GetPreparedQueryCommand(BuildExcept(ctx), false, true, CancellationToken.None);

        var rows = new List<Row>();
        rows.AddRange(Probe("Cte", () => BuildCte(ctx)));
        rows.AddRange(Probe("RecursiveCte", () => BuildRecursiveCte(ctx, context)));
        rows.AddRange(Probe("Except", () => BuildExcept(ctx)));

        Console.WriteLine();
        Console.WriteLine("=== Issue319 preparation path (B/query) ===");
        foreach (var arm in new[] { "Cte", "RecursiveCte", "Except" })
        {
            Console.WriteLine(arm);
            foreach (var row in rows.Where(r => r.Arm == arm))
                Console.WriteLine($"  {row.Variant,-14} median={row.Median,9:F2} min={row.Min,9:F2} max={row.Max,9:F2}");
        }

        var csv = new StringBuilder();
        csv.AppendLine("arm,variant,median_b,min_b,max_b");
        foreach (var row in rows)
            csv.AppendLine(string.Join(',', row.Arm, row.Variant,
                row.Median.ToString("F2", CultureInfo.InvariantCulture),
                row.Min.ToString("F2", CultureInfo.InvariantCulture),
                row.Max.ToString("F2", CultureInfo.InvariantCulture)));
        File.WriteAllText(Path.Combine(outDir, "issue319-prep.csv"), csv.ToString());
        Console.WriteLine($"probe evidence: {Path.Combine(outDir, "issue319-prep.csv")}");

        return Task.FromResult(0);
    }

    private static IEnumerable<Row> Probe<T>(string arm, Func<QueryCommand<T>> build)
    {
        yield return Measure(arm, "S0_build", build, static cmd => { });
        yield return Measure(arm, "S1_fingerprint", build, static cmd => { _ = QueryCommand.ComputeRawFingerprint(cmd); });
        yield return Measure(arm, "S2_rawlookup", build, static cmd => FingerprintAndLookup(cmd));
        yield return Measure(arm, "S3_applyprep", build, static cmd => TryApply(cmd));
        yield return Measure(arm, "S4_plankey", build, static cmd =>
        {
            if (TryApply(cmd))
                _ = cmd.GetOrCreatePlanKey(null);
        });
        yield return Measure(arm, "S5_full_hit", build, static cmd =>
        {
            _ = cmd.DataContext!.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);
        });
    }

    private static Row Measure<T>(string arm, string variant, Func<QueryCommand<T>> build, Action<QueryCommand<T>> step)
    {
        var samples = new double[BatchCount];
        for (var warm = 0; warm < WarmupIterations; warm++)
            for (var i = 0; i < BatchIterations; i++) step(build());

        for (var batch = 0; batch < BatchCount; batch++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < BatchIterations; i++) step(build());
            samples[batch] = (GC.GetAllocatedBytesForCurrentThread() - before) / (double)BatchIterations;
        }

        Array.Sort(samples);
        return new Row(arm, variant, samples[BatchCount / 2], samples[0], samples[^1]);
    }

    private static void FingerprintAndLookup<T>(QueryCommand<T> command)
    {
        if (QueryCommand.ComputeRawFingerprint(command) is int fingerprint)
            _sink += QueryPlanStore.TryGetRawFingerprint(command.DataContext!.GetType(), fingerprint, out _) ? 1 : 0;
    }

    private static bool TryApply<T>(QueryCommand<T> command)
    {
        if (QueryCommand.ComputeRawFingerprint(command) is not int fingerprint) return false;
        if (!QueryPlanStore.TryGetRawFingerprint(command.DataContext!.GetType(), fingerprint, out var plan) || plan is null) return false;

        _ = command.CaptureRawGraph();
        command.ApplyPreparationFrom(plan.QueryCommand);
        return true;
    }

    private static QueryCommand<CteJoinRow> BuildCte(TestDataRepository ctx)
    {
        var recent = ctx.ComplexEntity.Where(c => c.Id > 1).Select(c => new { c.Id, c.RequiredString });
        return ctx.DataContext.With("recent", recent)
            .From("recent")
            .Join(ctx.SimpleEntity, (r, s) => r["id"].AsInt == s.Id)
            .Select(p => new CteJoinRow { Id = p.Item1["id"].AsInt, SimpleId = p.Item2.Id });
    }

    private static QueryCommand<int> BuildRecursiveCte(TestDataRepository ctx, IDataContext context)
    {
        var anchor = ctx.SimpleEntity.Where(s => s.Id == 1).Select(s => new CteNumberRow { n = s.Id });
        var step = context.From("nums").Where(t => t["n"].AsInt < 5).Select(t => new CteNumberRow { n = t["n"].AsInt + 1 });
        return context.WithRecursive("nums", anchor.UnionAll(step)).From("nums").Select(t => t["n"].AsInt);
    }

    private static QueryCommand<int> BuildExcept(TestDataRepository ctx)
        => ctx.SimpleEntity.Select(s => s.Id).Except(ctx.ComplexEntity.Select(c => (int)c.Id));

    private readonly record struct Row(string Arm, string Variant, double Median, double Min, double Max);
}
