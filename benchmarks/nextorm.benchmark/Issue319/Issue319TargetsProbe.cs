using System.Globalization;
using System.Text;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Benchmark;

/// <summary>
/// Issue #319 increment I6 probe: attributes the four remaining frozen allocation targets (Any,
/// Where, GroupByCount, PageAsync) to collection, preparation, plan-key lookup and execution.
/// Benchmark-project-only (<c>--issue319-targets &lt;outDir&gt;</c>); it changes no product behaviour and
/// runs no BenchmarkDotNet job.
/// <para>
/// The <c>O_*</c> stages are the benchmark-shaped numbers: one operation is the whole
/// <c>Iterations</c> loop of the arm, with the captured value hoisted into one display class per
/// operation, exactly like a benchmark method. The per-call stages below them decompose that cost into
/// the builder, the command, the preparation (or raw-recipe reuse), the plan-key lookup and the
/// execution of an already-prepared shared command.
/// </para>
/// </summary>
internal static class Issue319TargetsProbe
{
    private const int WarmupIterations = 20;
    private const int BatchCount = 15;
    private const int BatchIterations = 10;
    private const int OpIterations = 100;
    private const int OffsetRows = 5_000;
    private const int PageSize = 100;
    private const int GroupModulus = 100;

    private static long _sink;

    /// <summary>The invocation-scoped captured value (the benchmark's hoisted loop variable).</summary>
    private sealed class Capture
    {
        public int I;
    }

    public static Task<int> RunAsync(string outDir)
    {
        Directory.CreateDirectory(outDir);

        var builder = new DataContextBuilder();
        builder.UseSqlite(BenchDb.FilePath);
        var context = builder.CreateDataContext();
        var ctx = new TestDataRepository(context);
        ((IConnectionManager)context).EnsureConnectionOpen();

        var capture = new Capture();
        for (var i = 0; i < 3; i++)
        {
            capture.I = i;
            _ = ctx.SimpleEntity.Where(e => e.Id == capture.I).AnyAsync().GetAwaiter().GetResult();
            _ = ctx.SimpleEntity.Where(e => e.Id == capture.I).Select(e => new { e.Id }).ToListAsync().GetAwaiter().GetResult();
            _ = BuildGroupBy(ctx).ToListAsync().GetAwaiter().GetResult();
            _ = BuildPage(ctx).ToListAsync().GetAwaiter().GetResult();
        }

        var rows = new List<Row>();
        rows.AddRange(ProbeArm("Any", () => ProbeAny(context, ctx, capture)));
        rows.AddRange(ProbeArm("Where", () => ProbeWhere(context, ctx, capture)));
        rows.AddRange(ProbeArm("GroupByCount", () => ProbeGroupBy(context, ctx)));
        rows.AddRange(ProbeArm("Page", () => ProbePage(context, ctx)));
        rows.AddRange(ProbeArm("Ops", () => ProbeOps(context, ctx)));

        Console.WriteLine();
        Console.WriteLine("=== Issue319 I6 targets (B/op) ===");
        foreach (var arm in new[] { "Any", "Where", "GroupByCount", "Page", "Ops" })
        {
            Console.WriteLine(arm);
            foreach (var row in rows.Where(r => r.Arm == arm))
                Console.WriteLine($"  {row.Stage,-24} median={row.Median,10:F1} min={row.Min,10:F1} max={row.Max,10:F1}");
        }

        var csv = new StringBuilder();
        csv.AppendLine("arm,stage,median_b,min_b,max_b");
        foreach (var row in rows)
            csv.AppendLine(string.Join(',', row.Arm, row.Stage,
                row.Median.ToString("F2", CultureInfo.InvariantCulture),
                row.Min.ToString("F2", CultureInfo.InvariantCulture),
                row.Max.ToString("F2", CultureInfo.InvariantCulture)));
        File.WriteAllText(Path.Combine(outDir, "issue319-targets.csv"), csv.ToString());
        Console.WriteLine($"probe evidence: {Path.Combine(outDir, "issue319-targets.csv")}");

        return Task.FromResult(0);
    }

    /// <summary>Runs one arm's stages and reports how the raw-recipe and parameter-recipe paths behaved.</summary>
    private static IEnumerable<Row> ProbeArm(string arm, Func<IEnumerable<Row>> probe)
    {
        var reuses0 = QueryPlanner.RecipeReuses;
        var publishes0 = QueryPlanner.RecipePublishes;
        var rejections0 = QueryPlanner.RecipeRejections;
        var mismatches0 = QueryPlanner.RecipeApplyMismatches;
        var fastBinds0 = ParamRefreshRecipe.FastBindHits;
        var fallbacks0 = QueryPlanner.FallbackRefreshes;
        var nativeHits0 = QueryPlanStore.NativeHits;
        var nativePublishes0 = QueryPlanStore.NativePublishes;
        var nativeMisses0 = QueryPlanStore.NativeMisses;
        var result = probe().ToList();
        Console.WriteLine($"native[{arm}]: hits={QueryPlanStore.NativeHits - nativeHits0} " +
            $"publishes={QueryPlanStore.NativePublishes - nativePublishes0} misses={QueryPlanStore.NativeMisses - nativeMisses0}");
        Console.WriteLine($"recipe[{arm}]: reuses={QueryPlanner.RecipeReuses - reuses0} publishes={QueryPlanner.RecipePublishes - publishes0} " +
            $"rejections={QueryPlanner.RecipeRejections - rejections0} mismatches={QueryPlanner.RecipeApplyMismatches - mismatches0} " +
            $"fastBinds={ParamRefreshRecipe.FastBindHits - fastBinds0} fallbacks={QueryPlanner.FallbackRefreshes - fallbacks0}");
        return result;
    }

    private static IEnumerable<Row> ProbeAny(IDataContext context, TestDataRepository ctx, Capture capture)
    {
        yield return Measure("Any", "A0_builder", i => { capture.I = i; _ = ctx.SimpleEntity.Where(e => e.Id == capture.I); });
        yield return Measure("Any", "A1_command", i =>
        {
            capture.I = i;
            var cmd = ctx.SimpleEntity.Where(e => e.Id == capture.I).ToParentCommand();
            cmd.IgnoreColumns = true;
        });
        yield return Measure("Any", "A2_cmd_prepared", i =>
        {
            capture.I = i;
            var cmd = ctx.SimpleEntity.Where(e => e.Id == capture.I).ToParentCommand();
            cmd.IgnoreColumns = true;
            cmd.PrepareCommand(false, CancellationToken.None);
        });
        yield return Measure("Any", "A3_plan_hit", i =>
        {
            capture.I = i;
            var cmd = ctx.SimpleEntity.Where(e => e.Id == capture.I).ToParentCommand();
            cmd.IgnoreColumns = true;
            var shared = EntityBuilderExtensions.GetAnyCommand(context, cmd);
            _ = context.GetPreparedQueryCommand(shared, false, true, CancellationToken.None);
        });
        yield return Measure("Any", "A4_full", i =>
        {
            capture.I = i;
            _sink += ctx.SimpleEntity.Where(e => e.Id == capture.I).AnyAsync().GetAwaiter().GetResult() ? 1 : 0;
        });

        // Execution only: one shared any-command prepared once, then executed repeatedly.
        capture.I = 0;
        var sharedCmd = EntityBuilderExtensions.GetAnyCommand(context,
            PrepareParent(ctx.SimpleEntity.Where(e => e.Id == capture.I)));
        var prepared = context.GetPreparedQueryCommand(sharedCmd, false, true, CancellationToken.None);
        yield return Measure("Any", "A5_exec_shared", _ =>
            _sink += context.ExecuteScalar<bool>(prepared, [], true) ? 1 : 0);
    }

    private static IEnumerable<Row> ProbeWhere(IDataContext context, TestDataRepository ctx, Capture capture)
    {
        yield return Measure("Where", "W0_builder", i => { capture.I = i; _ = ctx.SimpleEntity.Where(e => e.Id == capture.I); });
        yield return Measure("Where", "W1_command", i =>
        {
            capture.I = i;
            _ = ctx.SimpleEntity.Where(e => e.Id == capture.I).Select(e => new { e.Id });
        });
        yield return Measure("Where", "W2_plan_hit", i =>
        {
            capture.I = i;
            var cmd = ctx.SimpleEntity.Where(e => e.Id == capture.I).Select(e => new { e.Id });
            _ = context.GetPreparedQueryCommand(cmd, true, true, CancellationToken.None);
        });
        yield return Measure("Where", "W3_full", i =>
        {
            capture.I = i;
            foreach (var row in ctx.SimpleEntity.Where(e => e.Id == capture.I).Select(e => new { e.Id }).ToListAsync().GetAwaiter().GetResult())
                _sink += row.Id;
        });

        capture.I = 0;
        var shared = context.GetPreparedQueryCommand(
            ctx.SimpleEntity.Where(e => e.Id == capture.I).Select(e => new { e.Id }), true, true, CancellationToken.None);
        yield return Measure("Where", "W4_exec_shared", i =>
        {
            capture.I = i;
            foreach (var row in shared.ToListAsync(context, capture.I).GetAwaiter().GetResult()) _sink += row.Id;
        });
    }

    private static IEnumerable<Row> ProbeGroupBy(IDataContext context, TestDataRepository ctx)
    {
        yield return Measure("GroupByCount", "G0_command", _i => { _ = BuildGroupBy(ctx); });
        yield return Measure("GroupByCount", "G1_plan_hit", _i =>
        {
            var cmd = BuildGroupBy(ctx);
            _ = context.GetPreparedQueryCommand(cmd, true, true, CancellationToken.None);
        });
        yield return Measure("GroupByCount", "G2_full", _i =>
        {
            foreach (var row in BuildGroupBy(ctx).ToListAsync().GetAwaiter().GetResult()) _sink += row.Count;
        });

        var shared = context.GetPreparedQueryCommand(BuildGroupBy(ctx), true, true, CancellationToken.None);
        yield return Measure("GroupByCount", "G3_exec_shared", _i =>
        {
            foreach (var row in shared.ToListAsync(context).GetAwaiter().GetResult()) _sink += row.Count;
        });
    }

    private static IEnumerable<Row> ProbePage(IDataContext context, TestDataRepository ctx)
    {
        yield return Measure("Page", "P0_command", _i => { _ = BuildPage(ctx); });
        yield return Measure("Page", "P1_plan_hit", _i =>
        {
            var cmd = BuildPage(ctx);
            _ = context.GetPreparedQueryCommand(cmd, true, true, CancellationToken.None);
        });
        yield return Measure("Page", "P2_full", _i =>
        {
            foreach (var row in BuildPage(ctx).ToListAsync().GetAwaiter().GetResult()) _sink += row.Id;
        });

        var shared = context.GetPreparedQueryCommand(BuildPage(ctx), true, true, CancellationToken.None);
        yield return Measure("Page", "P3_exec_shared", _i =>
        {
            foreach (var row in shared.ToListAsync(context).GetAwaiter().GetResult()) _sink += row.Id;
        });
    }

    /// <summary>The benchmark-shaped stages: one operation is the arm's whole iteration loop.</summary>
    private static IEnumerable<Row> ProbeOps(IDataContext context, TestDataRepository ctx)
    {
        yield return MeasureOp("Ops", "O_any_full", () =>
        {
            for (var i = 0; i < OpIterations; i++)
                _sink += ctx.SimpleEntity.Where(e => e.Id == i).AnyAsync().GetAwaiter().GetResult() ? 1 : 0;
        });
        yield return MeasureOp("Ops", "O_where_from", () =>
        {
            for (var i = 0; i < OpIterations; i++)
                _ = ctx.SimpleEntity;
        });
        yield return MeasureOp("Ops", "O_where_command_only", () =>
        {
            // The command object alone: no caller trees, no builder clone, no execution.
            var condition = (System.Linq.Expressions.Expression<Func<SimpleEntity, bool>>)(e => e.Id == 1);
            var projection = (System.Linq.Expressions.Expression<Func<SimpleEntity, object>>)(e => new { e.Id });
            var builder = ctx.SimpleEntity.Where(condition);
            for (var i = 0; i < OpIterations; i++)
                _ = builder.Select(projection);
        });
        yield return MeasureOp("Ops", "O_where_tree2", () =>
        {
            for (var i = 0; i < OpIterations; i++)
                _ = (System.Linq.Expressions.Expression<Func<SimpleEntity, object>>)(e => new { e.Id });
        });
        yield return MeasureOp("Ops", "O_where_builder", () =>
        {
            for (var i = 0; i < OpIterations; i++)
                _ = ctx.SimpleEntity.Where(e => e.Id == i);
        });
        yield return MeasureOp("Ops", "O_where_tree", () =>
        {
            // The same filter expression tree the command is built from, materialised on its own: the
            // part of the arm the library cannot remove.
            for (var i = 0; i < OpIterations; i++)
                _ = (System.Linq.Expressions.Expression<Func<SimpleEntity, bool>>)(e => e.Id == i);
        });
        yield return MeasureOp("Ops", "O_where_command", () =>
        {
            for (var i = 0; i < OpIterations; i++)
                _ = ctx.SimpleEntity.Where(e => e.Id == i).Select(e => new { e.Id });
        });
        yield return MeasureOp("Ops", "O_where_exec", () =>
        {
            var shared = ctx.SimpleEntity.Where(e => e.Id == 1).Select(e => new { e.Id }).ToListAsync().GetAwaiter().GetResult();
            _ = shared;
            var prepared = context.GetPreparedQueryCommand(
                ctx.SimpleEntity.Where(e => e.Id == 1).Select(e => new { e.Id }), false, true, CancellationToken.None);
            for (var i = 0; i < OpIterations; i++)
                foreach (var row in prepared.ToListAsync(context).GetAwaiter().GetResult()) _sink += row.Id;
        });
        yield return MeasureOp("Ops", "O_where_filterprobe", () =>
        {
            // The metadata-only filter probe the native gate runs on every call.
            for (var i = 0; i < OpIterations; i++)
            {
                var cmd = ctx.SimpleEntity.Where(e => e.Id == i).Select(e => new { e.Id });
                _ = QueryCommand.HasApplicableFiltersInCommandGraph(cmd, context);
            }
        });
        yield return MeasureOp("Ops", "O_where_exec_wrapper", () =>
        {
            // Exactly the call the native hit makes: the context wrapper with an (empty) params array.
            var prepared = context.GetPreparedQueryCommand(
                ctx.SimpleEntity.Where(e => e.Id == 1).Select(e => new { e.Id }), false, true, CancellationToken.None);
            var empty = Array.Empty<object>();
            for (var i = 0; i < OpIterations; i++)
                foreach (var row in context.ToListAsync(prepared, empty, CancellationToken.None).GetAwaiter().GetResult()) _sink += row.Id;
        });
        yield return MeasureOp("Ops", "O_where_full", () =>
        {
            for (var i = 0; i < OpIterations; i++)
            {
                var cmd = ctx.SimpleEntity.Where(e => e.Id == i).Select(e => new { e.Id });
                foreach (var row in cmd.ToListAsync().GetAwaiter().GetResult()) _sink += row.Id;
            }
        });
        yield return MeasureOp("Ops", "O_groupby_trees", () =>
        {
            for (var i = 0; i < OpIterations; i++)
            {
                _ = (System.Linq.Expressions.Expression<Func<LargeEntity, object>>)(e => new { Bucket = e.Id % GroupModulus });
                _ = (System.Linq.Expressions.Expression<Func<LargeEntity, GroupCountRow>>)(e => new GroupCountRow { Key = e.Id % GroupModulus, Count = SqlFunctions.Sql.count() });
            }
        });
        yield return MeasureOp("Ops", "O_groupby_clone", () =>
        {
            var group = (System.Linq.Expressions.Expression<Func<LargeEntity, object>>)(e => new { Bucket = e.Id % GroupModulus });
            var source = ctx.LargeEntity;
            for (var i = 0; i < OpIterations; i++)
                _ = source.GroupBy(group);
        });
        yield return MeasureOp("Ops", "O_groupby_command", () =>
        {
            var group = (System.Linq.Expressions.Expression<Func<LargeEntity, object>>)(e => new { Bucket = e.Id % GroupModulus });
            var projection = (System.Linq.Expressions.Expression<Func<LargeEntity, GroupCountRow>>)(e => new GroupCountRow { Key = e.Id % GroupModulus, Count = SqlFunctions.Sql.count() });
            var grouped = ctx.LargeEntity.GroupBy(group);
            for (var i = 0; i < OpIterations; i++)
                _ = grouped.Select(projection);
        });
        yield return MeasureOp("Ops", "O_groupby_exec", () =>
        {
            var shared = context.GetPreparedQueryCommand(BuildGroupBy(ctx), false, true, CancellationToken.None);
            for (var i = 0; i < OpIterations; i++)
                foreach (var row in shared.ToListAsync(context).GetAwaiter().GetResult()) _sink += row.Count;
        });
        yield return MeasureOp("Ops", "O_groupby_full", () =>
        {
            foreach (var row in BuildGroupBy(ctx).ToListAsync().GetAwaiter().GetResult()) _sink += row.Count;
        });
        yield return MeasureOp("Ops", "O_page_full", () =>
        {
            foreach (var row in BuildPage(ctx).ToListAsync().GetAwaiter().GetResult()) _sink += row.Id;
        });
    }

    private static Row MeasureOp(string arm, string stage, Action action)
        => Measure(arm, stage, _ => action());

    private static QueryCommand<SimpleEntity> PrepareParent(EntityBuilder<SimpleEntity> builder)
    {
        var cmd = builder.ToParentCommand();
        cmd.IgnoreColumns = true;
        return cmd;
    }

    private static QueryCommand<GroupCountRow> BuildGroupBy(TestDataRepository ctx)
        => ctx.LargeEntity
            .GroupBy(e => new { Bucket = e.Id % GroupModulus })
            .Select(e => new GroupCountRow { Key = e.Id % GroupModulus, Count = SqlFunctions.Sql.count() });

    private static QueryCommand<ProjectionDto> BuildPage(TestDataRepository ctx)
        => ctx.LargeEntity
            .Select(e => new ProjectionDto { Id = e.Id, Str = e.Str })
            .OrderBy(e => e.Id)
            .Offset(OffsetRows)
            .Limit(PageSize);

    private static Row Measure(string arm, string stage, Action<int> action)
    {
        var samples = new double[BatchCount];
        for (var warm = 0; warm < WarmupIterations; warm++)
            for (var i = 0; i < BatchIterations; i++) action(i);

        for (var batch = 0; batch < BatchCount; batch++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < BatchIterations; i++) action(i);
            samples[batch] = (GC.GetAllocatedBytesForCurrentThread() - before) / (double)BatchIterations;
        }

        Array.Sort(samples);
        return new Row(arm, stage, samples[BatchCount / 2], samples[0], samples[^1]);
    }

    private readonly record struct Row(string Arm, string Stage, double Median, double Min, double Max);
}
