using System.Globalization;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Benchmark;

/// <summary>
/// T245 (#245) D245-1 allocation attribution probe. It rebuilds the exact warm EXCEPT/INTERSECT
/// fluent query of <c>SqliteBenchmarkFeaturesFairCached.B_Nextorm_Cached_Except</c> /
/// <c>_Intersect</c> and stops at successive stages, so the per-stage allocation cost can be
/// subtracted (V6 - V5 = command shell, V4 - V1 = clone + set operation, V0 - V4 = prepare + lookup +
/// execution). Allocation is measured with <see cref="GC.GetAllocatedBytesForCurrentThread"/>
/// deltas (precision <c>true</c>) around a batch of logical queries; the process-wide
/// <see cref="GC.GetTotalAllocatedBytes"/> delta is recorded next to it as a cross-check, because an
/// async continuation that hops threads is invisible to the current-thread counter.
/// <para>
/// Benchmark-project-only entry point (<c>--t245-attribution</c>). It never touches nextorm.core and
/// runs no BenchmarkDotNet job; the numbers are plain in-process counters and are not BDN results.
/// </para>
/// </summary>
internal static class T245AttributionProbe
{
    private const int WarmupIterations = 3;
    private const int BatchIterations = 10;
    private const int BatchCount = 10;

    private const string ExceptOperation = "EXCEPT";
    private const string IntersectOperation = "INTERSECT";

    // Keeps constructed commands reachable so the JIT cannot fold the measured window away.
    private static long _sink;

    /// <summary>
    /// Runs every variant for both set operations, writes <c>probe-variants.json</c> /
    /// <c>probe-variants.csv</c> into <paramref name="outDir"/> and returns 0 when every row was
    /// measured and the derived closure is consistent.
    /// </summary>
    public static Task<int> RunAsync(string outDir)
    {
        Directory.CreateDirectory(outDir);

        var dbPath = BenchDb.FilePath;
        var dbSha = File.Exists(dbPath)
            ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(dbPath))).ToLowerInvariant()
            : "missing";

        // Exact context/provider construction of SqliteBenchmarkFeaturesFairCached.
        var builder = new DataContextBuilder();
        builder.UseSqlite(dbPath);
        var context = builder.CreateDataContext();
        var ctx = new TestDataRepository(context);
        ((IConnectionManager)context).EnsureConnectionOpen();

        // Verification pass (not measured): the fluent shape must still produce rows.
        var exceptRows = ctx.SimpleEntity.Select(s => s.Id).Except(ctx.ComplexEntity.Select(c => (int)c.Id)).ToList();
        var intersectRows = ctx.SimpleEntity.Select(s => s.Id).Intersect(ctx.ComplexEntity.Select(c => (int)c.Id)).ToList();
        Console.WriteLine($"verification: {ExceptOperation} rows={exceptRows.Count}, {IntersectOperation} rows={intersectRows.Count}, db={dbPath}");

        // Operands used by V3: built once, outside the measured loop.
        var left = ctx.SimpleEntity.Select(s => s.Id);
        var right = ctx.ComplexEntity.Select(c => (int)c.Id);
        GC.KeepAlive(left);
        GC.KeepAlive(right);

        // V5/V6 inputs: the SelectCore definition inputs for a fresh builder. Every other modifier is
        // default/null on a fresh EntityBuilder, so the record carries only Exp and SrcType.
        var sourceType = ctx.SimpleEntity.SourceEntityType;

        // V7 controls: prepared commands reused per iteration (mirrors
        // SqliteBenchmarkFeaturesFair.A_Nextorm_Prepared_Except / _Intersect).
        var preparedExcept = ctx.SimpleEntity.Select(s => s.Id).Except(ctx.ComplexEntity.Select(c => (int)c.Id)).Prepare();
        var preparedIntersect = ctx.SimpleEntity.Select(s => s.Id).Intersect(ctx.ComplexEntity.Select(c => (int)c.Id)).Prepare();

        // G2 inputs: pre-built expression trees for the record-only variant. Hoisting is a probe
        // artifice only; the fluent call emits a fresh tree per Select, so V8's bytes are NOT the
        // production per-query cost — V9 measures exactly what hoisting removes from the workload.
        Expression<Func<SimpleEntity, int>> hoistedLeft = s => s.Id;
        Expression<Func<ComplexEntity, int>> hoistedRight = c => (int)c.Id;
        GC.KeepAlive(hoistedLeft);
        GC.KeepAlive(hoistedRight);

        // Prime both plan-cache entries so V0's measured batches are warm (warm-up covers the miss).
        _ = context.GetPreparedQueryCommand(
            ctx.SimpleEntity.Select(s => s.Id).Except(ctx.ComplexEntity.Select(c => (int)c.Id)), false, true, CancellationToken.None);
        _ = context.GetPreparedQueryCommand(
            ctx.SimpleEntity.Select(s => s.Id).Intersect(ctx.ComplexEntity.Select(c => (int)c.Id)), false, true, CancellationToken.None);

        var rows = new List<VariantRow>();

        foreach (var operation in new[] { ExceptOperation, IntersectOperation })
        {
            var isExcept = operation == ExceptOperation;

            // V0 - the benchmark body exactly (the BDN body is the same 10-iteration loop inside one
            // method): both operands, set operation and the async list terminal per iteration.
            rows.Add(Measure("V0_full", operation, isExcept
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

            // V1 - both operand commands only (no set operation, no terminal).
            rows.Add(Measure("V1_operands", operation, () =>
            {
                var l = ctx.SimpleEntity.Select(s => s.Id);
                var r = ctx.ComplexEntity.Select(c => (int)c.Id);
                GC.KeepAlive(l);
                GC.KeepAlive(r);
                return Task.CompletedTask;
            }));

            // V2 - one operand command (operation-agnostic; measured inside both groups).
            rows.Add(Measure("V2_left_operand", operation, () =>
            {
                var l = ctx.SimpleEntity.Select(s => s.Id);
                GC.KeepAlive(l);
                return Task.CompletedTask;
            }));

            // V3 - clone + ResetPreparation + SetOperation on prebuilt operands.
            rows.Add(Measure("V3_clone_setop_prebuilt", operation, () =>
            {
                var cmd = isExcept ? left.Except(right) : left.Intersect(right);
                GC.KeepAlive(cmd);
                return Task.CompletedTask;
            }));

            // V4 - build both operands and the set operation, stop before any terminal/prepare.
            rows.Add(Measure("V4_setop_no_terminal", operation, () =>
            {
                var cmd = isExcept
                    ? ctx.SimpleEntity.Select(s => s.Id).Except(ctx.ComplexEntity.Select(c => (int)c.Id))
                    : ctx.SimpleEntity.Select(s => s.Id).Intersect(ctx.ComplexEntity.Select(c => (int)c.Id));
                GC.KeepAlive(cmd);
                return Task.CompletedTask;
            }));

            // ---- G1: decompose V4 (set operation, no terminal) into named stages -------------------

            // V4a - left operand only (definition + expression + shell); must equal V2 = 1544.
            rows.Add(Measure("V4a_left_operand", operation, () =>
            {
                var l = ctx.SimpleEntity.Select(s => s.Id);
                GC.KeepAlive(l);
                return Task.CompletedTask;
            }));

            // V4b - left + right operands, no set operation and no terminal; must equal V1 = 3184.
            rows.Add(Measure("V4b_two_operands", operation, () =>
            {
                var l = ctx.SimpleEntity.Select(s => s.Id);
                var r = ctx.ComplexEntity.Select(c => (int)c.Id);
                GC.KeepAlive(l);
                GC.KeepAlive(r);
                return Task.CompletedTask;
            }));

            // V4c - clone only: prebuilt left operands, Clone(), no reset and no set operation.
            rows.Add(Measure("V4c_clone_only", operation, () =>
            {
                var cmd = left.Clone();
                GC.KeepAlive(cmd);
                return Task.CompletedTask;
            }));

            // V4d - clone + ResetPreparation, no SetOperation. SetOperation is protected, so the
            // field assignment cannot be measured on its own; V4e (the public Except/Intersect call)
            // folds it in, and V4e - V4d isolates it.
            rows.Add(Measure("V4d_clone_reset", operation, () =>
            {
                var cmd = left.Clone();
                cmd.ResetPreparation();
                GC.KeepAlive(cmd);
                return Task.CompletedTask;
            }));

            // V4e - full V4 reproduced: fresh operands + set operation, no terminal.
            rows.Add(Measure("V4e_full_repro", operation, () =>
            {
                var cmd = isExcept
                    ? ctx.SimpleEntity.Select(s => s.Id).Except(ctx.ComplexEntity.Select(c => (int)c.Id))
                    : ctx.SimpleEntity.Select(s => s.Id).Intersect(ctx.ComplexEntity.Select(c => (int)c.Id));
                GC.KeepAlive(cmd);
                return Task.CompletedTask;
            }));

            // V4f - right operand only, exactly as the benchmark builds it: ComplexEntity + (int) cast.
            rows.Add(Measure("V4f_right_operand", operation, () =>
            {
                var r = ctx.ComplexEntity.Select(c => (int)c.Id);
                GC.KeepAlive(r);
                return Task.CompletedTask;
            }));

            // V4g - right operand without the cast (long projection): same entity, one tree node less.
            rows.Add(Measure("V4g_right_operand_nocast", operation, () =>
            {
                var r = ctx.ComplexEntity.Select(c => c.Id);
                GC.KeepAlive(r);
                return Task.CompletedTask;
            }));

            // V4h - left entity with a widening conversion (int -> long): same entity as V2/V4a,
            // one conversion node more. Isolates the conversion node from the entity type.
            rows.Add(Measure("V4h_left_widening", operation, () =>
            {
                var l = ctx.SimpleEntity.Select(s => (long)s.Id);
                GC.KeepAlive(l);
                return Task.CompletedTask;
            }));

            // V4j - right entity projected to a reference member (string): no conversion, no long
            // result. Separates the ComplexEntity source cost from the long projection cost.
            rows.Add(Measure("V4j_right_string_member", operation, () =>
            {
                var r = ctx.ComplexEntity.Select(c => c.String);
                GC.KeepAlive(r);
                return Task.CompletedTask;
            }));

            // V4k - left entity with one extra arithmetic node (binary expression, int result):
            // isolates the per-node expression-tree cost from the conversion/entity effects.
            rows.Add(Measure("V4k_left_binary_node", operation, () =>
            {
                var l = ctx.SimpleEntity.Select(s => s.Id + 1);
                GC.KeepAlive(l);
                return Task.CompletedTask;
            }));

            // V4l - left entity converted to double (conversion node, non-integer result type).
            rows.Add(Measure("V4l_left_double_convert", operation, () =>
            {
                var l = ctx.SimpleEntity.Select(s => (double)s.Id);
                GC.KeepAlive(l);
                return Task.CompletedTask;
            }));

            // V4m - a third class-mapped entity with a long member and no conversion: separates a
            // type-level effect from one specific to ComplexEntity.
            rows.Add(Measure("V4m_large_operand", operation, () =>
            {
                var l = ctx.LargeEntity.Select(e => e.Id);
                GC.KeepAlive(l);
                return Task.CompletedTask;
            }));

            // V4n - right entity, nullable-int member, no conversion: same entity as V4g, no long.
            rows.Add(Measure("V4n_right_nullable_int", operation, () =>
            {
                var r = ctx.ComplexEntity.Select(c => c.Int);
                GC.KeepAlive(r);
                return Task.CompletedTask;
            }));

            // ---- G2: record-only vs expression-only bytes (candidate D) -----------------------------

            // V8 - QueryDefinition record only; both expression fields are the hoisted instances.
            rows.Add(Measure("V8_definition_record_only", operation, () =>
            {
                var definition = new QueryDefinition { Exp = hoistedLeft, SrcType = sourceType };
                GC.KeepAlive(definition);
                return Task.CompletedTask;
            }));

            // V9 - the two per-call expression trees, no record (workload bytes the fluent call emits).
            rows.Add(Measure("V9_expressions_only", operation, () =>
            {
                Expression<Func<SimpleEntity, int>> l = s => s.Id;
                Expression<Func<ComplexEntity, int>> r = c => (int)c.Id;
                GC.KeepAlive(l);
                GC.KeepAlive(r);
                return Task.CompletedTask;
            }));

            // ---- G3: comparer lazy-init bytes (candidate C) ----------------------------------------

            // V10 - one shell + the comparer-init call QueryPlan's ctor performs (public surface).
            rows.Add(Measure("V10_comparer_init", operation, () =>
            {
                Expression<Func<SimpleEntity, int>> projection = s => s.Id;
                var definition = new QueryDefinition { Exp = projection, SrcType = sourceType };
                var cmd = new QueryCommand<int>(context, definition, ownJoins: true);
                var comparer = cmd.GetQueryPlanEqualityComparer();
                GC.KeepAlive(comparer);
                return Task.CompletedTask;
            }));

            // V10b - the real forced plan-key creation path (internal; benchmark has InternalsVisibleTo).
            rows.Add(Measure("V10b_plan_key", operation, () =>
            {
                Expression<Func<SimpleEntity, int>> projection = s => s.Id;
                var definition = new QueryDefinition { Exp = projection, SrcType = sourceType };
                var cmd = new QueryCommand<int>(context, definition, ownJoins: true);
                var plan = cmd.GetOrCreatePlanKey(null);
                GC.KeepAlive(plan);
                return Task.CompletedTask;
            }));

            // V10c - three fresh shells, each with its comparer init: multiplicative check. The number
            // is for three commands per body call, so the per-command delta is recomputed in the closure.
            rows.Add(Measure("V10c_comparer_init_three", operation, () =>
            {
                for (var c = 0; c < 3; c++)
                {
                    Expression<Func<SimpleEntity, int>> projection = s => s.Id;
                    var definition = new QueryDefinition { Exp = projection, SrcType = sourceType };
                    var cmd = new QueryCommand<int>(context, definition, ownJoins: true);
                    var comparer = cmd.GetQueryPlanEqualityComparer();
                    GC.KeepAlive(comparer);
                }

                return Task.CompletedTask;
            }));

            // V5 - the QueryDefinition record SelectCore builds, with the same inline projection lambda
            // (so the per-call expression-tree allocation is included exactly as in the fluent call).
            rows.Add(Measure("V5_definition", operation, () =>
            {
                Expression<Func<SimpleEntity, int>> projection = s => s.Id;
                var definition = new QueryDefinition { Exp = projection, SrcType = sourceType };
                GC.KeepAlive(definition);
                return Task.CompletedTask;
            }));

            // V6 - definition + command shell. The third constructor parameter is ownJoins (the
            // internal shape ctor), not forCache; SelectCore passes ownJoins: true.
            rows.Add(Measure("V6_shell", operation, () =>
            {
                Expression<Func<SimpleEntity, int>> projection = s => s.Id;
                var definition = new QueryDefinition { Exp = projection, SrcType = sourceType };
                var cmd = new QueryCommand<int>(context, definition, ownJoins: true);
                GC.KeepAlive(cmd);
                return Task.CompletedTask;
            }));

            // V7 - prepared control executed once per iteration (same loop-in-one-method shape as the
            // BDN A_Nextorm_Prepared_Except / _Intersect arm).
            rows.Add(Measure("V7_prepared_control", operation, isExcept
                ? async () =>
                {
                    for (var i = 0; i < BatchIterations; i++)
                        foreach (var row in await preparedExcept.ToListAsync(context))
                            _sink++;
                }
                : async () =>
                {
                    for (var i = 0; i < BatchIterations; i++)
                        foreach (var row in await preparedIntersect.ToListAsync(context))
                            _sink++;
                }, queriesPerCall: BatchIterations));
        }

        var derived = BuildDerived(rows);
        var payload = new ProbePayload
        {
            Task = "T245",
            Unit = "D245-1",
            GeneratedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            Method = "GC.GetAllocatedBytesForCurrentThread delta (precision true) over a batch of "
                + BatchIterations + " logical queries, divided by the batch size; a second identical "
                + "window records the process-wide GC.GetTotalAllocatedBytes(precise: true); warm-up "
                + WarmupIterations + " calls per variant; " + BatchCount + " measured batches",
            WarmupIterations = WarmupIterations,
            BatchIterations = BatchIterations,
            BatchCount = BatchCount,
            Notice = "Allocation-count probe only: deterministic counters, CPU-noise-immune, no host "
                + "lock and no BenchmarkDotNet job were used (per D245-1 brief).",
            BdnReference = new BdnReference
            {
                Source = ".pdca/T245/rv1/bdn/{entry-r2,decompose-entry,controls-entry}/summary.json",
                FullFairBytesPerQuery = new[] { 6528.1, 6512.1 },
                ConstructBytesPerQuery = 3632.16,
                WarmPlanOnlyBytesPerQuery = new[] { 5880.28, 5880.26 },
                ControlBytesPerQuery = new[] { 760.0, 744.0 },
            },
            Harness = new HarnessInfo
            {
                Argv = Environment.GetCommandLineArgs(),
                LaunchCommand = "env -u NEXTORM_BENCH_FULL NEXTORM_BENCH_DB=/tmp/nextorm-bench/test.db "
                    + "dotnet run --project benchmarks/nextorm.benchmark -c Release --no-build -- "
                    + "--t245-attribution .pdca/T245/rv1/attribution",
                Db = dbPath,
                DbSha256 = dbSha,
                Context = "DataContextBuilder.UseSqlite(BenchDb.FilePath) -> CreateDataContext + "
                    + "TestDataRepository + ((IConnectionManager)context).EnsureConnectionOpen() "
                    + "(same construction as SqliteBenchmarkFeaturesFairCached); Provider=SQLite",
            },
            Variants = rows,
            Derived = derived,
            Closure = BuildClosure(rows),
            Reconciliation = BuildReconciliation(rows),
        };

        var jsonPath = Path.Combine(outDir, "probe-variants2.json");
        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        };
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(payload, jsonOptions));

        var csv = new StringBuilder("variant,operation,batch,iterations,thread_bytes,process_total_bytes,bytes_per_query\n");
        foreach (var row in rows)
            for (var b = 0; b < row.ThreadBatchBytes.Length; b++)
                csv.Append(row.Variant).Append(',')
                    .Append(row.Operation).Append(',')
                    .Append(b).Append(',')
                    .Append(BatchIterations).Append(',')
                    .Append(row.ThreadBatchBytes[b]).Append(',')
                    .Append(row.ProcessBatchBytes[b]).Append(',')
                    .Append((row.ThreadBatchBytes[b] / (double)BatchIterations).ToString("F2", CultureInfo.InvariantCulture))
                    .Append('\n');
        File.WriteAllText(Path.Combine(outDir, "probe-variants2.csv"), csv.ToString());

        Console.WriteLine($"{"variant",-26} {"operation",-10} {"B/query min",12} {"median",12} {"max",12} {"total median",14}");
        foreach (var row in rows)
            Console.WriteLine(
                $"{row.Variant,-26} {row.Operation,-10} {row.BytesPerQuery.Min,12:F2} {row.BytesPerQuery.Median,12:F2} "
                + $"{row.BytesPerQuery.Max,12:F2} {row.ProcessTotalBytesPerQuery.Median,14:F2}");

        Console.WriteLine();
        foreach (var (operation, split) in derived)
            Console.WriteLine(
                $"{operation}: operands={split.Operands:F2} single={split.SingleOperand:F2} "
                + $"definition+expr={split.Definition:F2} shell={split.ShellCountPerQuery:F2} "
                + $"prebuilt-clone+setop={split.PrebuiltCloneSetOperation:F2} clone+setop={split.ClonePlusSetOperation:F2} "
                + $"prepare+execute={split.ExecutionResidual:F2} prepared-control={split.PreparedControl:F2}");

        Console.WriteLine();
        foreach (var (operation, rec) in payload.Closure)
            Console.WriteLine(
                $"{operation} closure: V4a={rec.LeftOperand:F2} V4b={rec.TwoOperands:F2} "
                + $"right={rec.RightOperand:F2} (expr +{rec.RightExpressionMinusLeftExpression:F2}) "
                + $"clone={rec.CloneOnly:F2} clone+reset={rec.CloneReset:F2} "
                + $"setop-assembly={rec.SetOperationAssembly:F2} V4e={rec.FullSetopNoTerminal:F2} "
                + $"prepare={rec.PreparePhase:F2} exec={rec.Execution:F2} residual={rec.Residual:F2} "
                + $"planner-model-residual={rec.PlannerOriginalResidual:F2} cast-node={rec.CastNodeBytes:F2} "
                + $"widening-node={rec.WideningNodeBytes:F2} string-member-minus-left={rec.RightStringMemberMinusLeft:F2} "
                + $"long-minus-string={rec.LongProjectionMinusStringMember:F2} binary-node={rec.BinaryNodeBytes:F2} "
                + $"convert-node={rec.ConvertNodeBytes:F2} large-minus-left={rec.LargeOperandMinusLeft:F2} "
                + $"nullable-int-minus-left={rec.RightNullableIntMinusLeft:F2} entity-footprint={rec.RightEntityFootprintBytes:F2}");
        foreach (var (operation, rec) in payload.Closure)
            Console.WriteLine(
                $"{operation} construction model: records={rec.DefinitionRecordPoolPerQuery:F2} "
                + $"expressions={rec.ExpressionsPerQuery:F2} operand-shells={rec.OperandShellsPerQuery:F2} "
                + $"clone-shell={rec.CloneShell:F2} total={rec.ConstructionModelTotal:F2} "
                + $"residual={rec.ConstructionModelResidual:F2}");
        foreach (var (operation, rec) in payload.Closure)
            Console.WriteLine(
                $"{operation} G2/G3: record-only={rec.DefinitionRecordOnly:F2} expressions-only={rec.ExpressionsOnly:F2} "
                + $"left-expression={rec.LeftExpressionOnly:F2} right-expression={rec.RightExpressionOnly:F2} "
                + $"comparer-init/cmd={rec.ComparerInitPerCommand:F2} plan-key-extra={rec.PlanKeyPathExtra:F2} "
                + $"comparer-init/cmd (3 shells)={rec.ComparerInitPerCommandFromThree:F2} "
                + $"multiplicative={rec.ComparerInitMultiplicative}");

        Console.WriteLine($"probe evidence: {jsonPath}");
        foreach (var (operation, rec) in payload.Reconciliation)
            Console.WriteLine(
                $"{operation} reconciliation: V0-BDNfull={rec.V0MinusBdnFullFair:F2} "
                + $"V4-BDNconstruct={rec.V4MinusBdnConstruct:F2} V7-BDNcontrol={rec.V7MinusBdnControl:F2} "
                + $"prepare={rec.PreparePhase:F2} (BDN {rec.BdnPreparePhase:F2}, delta {rec.PreparePhaseDelta:F2}) "
                + $"closure-residual={rec.ClosureResidual:F2}");
        return Task.FromResult(0);
    }

    private static VariantRow Measure(string variant, string operation, Func<Task> body, int queriesPerCall = 1)
    {
        var warmupCalls = Math.Max(1, (WarmupIterations + queriesPerCall - 1) / queriesPerCall);
        for (var i = 0; i < warmupCalls; i++)
            body().GetAwaiter().GetResult();

        var callsPerBatch = Math.Max(1, BatchIterations / queriesPerCall);
        var thread = new long[BatchCount];
        var process = new long[BatchCount];
        for (var batch = 0; batch < BatchCount; batch++)
        {
            // Two identical windows, never nested: the process-wide counter cannot contaminate the
            // per-thread window (allocation is deterministic, so both windows measure the same work).
            var threadBefore = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < callsPerBatch; i++)
                body().GetAwaiter().GetResult();
            thread[batch] = GC.GetAllocatedBytesForCurrentThread() - threadBefore;

            var processBefore = GC.GetTotalAllocatedBytes(precise: true);
            for (var i = 0; i < callsPerBatch; i++)
                body().GetAwaiter().GetResult();
            process[batch] = GC.GetTotalAllocatedBytes(precise: true) - processBefore;
        }

        var perQuery = thread.Select(static bytes => bytes / (double)BatchIterations).ToArray();
        var perQueryProcess = process.Select(static bytes => bytes / (double)BatchIterations).ToArray();
        var sorted = perQuery.OrderBy(static value => value).ToArray();
        var sortedProcess = perQueryProcess.OrderBy(static value => value).ToArray();

        return new VariantRow
        {
            Variant = variant,
            Operation = operation,
            Iterations = BatchCount * BatchIterations,
            BatchIterations = BatchIterations,
            BatchCount = BatchCount,
            ThreadBatchBytes = thread,
            ProcessBatchBytes = process,
            BytesPerQuery = new Stats
            {
                Min = sorted[0],
                Median = Median(sorted),
                Max = sorted[^1],
                Mean = sorted.Average(),
            },
            ProcessTotalBytesPerQuery = new Stats
            {
                Min = sortedProcess[0],
                Median = Median(sortedProcess),
                Max = sortedProcess[^1],
                Mean = sortedProcess.Average(),
            },
            Notes = NotesFor(variant),
        };
    }

    private static string NotesFor(string variant) => variant switch
    {
        "V0_full" => "benchmark body exactly: the BDN-style loop of 10 iterations inside one async body, each building two Select commands + set operation + await ToListAsync + row sink",
        "V1_operands" => "two operand commands, no set operation, no terminal",
        "V2_left_operand" => "one Select command; operation-agnostic, measured inside both operation groups",
        "V3_clone_setop_prebuilt" => "prebuilt operands; per iteration QueryCommand.Clone + ResetPreparation + SetOperation",
        "V4_setop_no_terminal" => "two Select commands + set operation, no terminal and no prepare; equivalent to BDN Construct",
        "V4a_left_operand" => "G1: left operand only, definition + inline expression + shell (control: equals V2)",
        "V4b_two_operands" => "G1: both operands, no set operation and no terminal (control: equals V1)",
        "V4c_clone_only" => "G1: prebuilt left operand Clone() only, no ResetPreparation and no SetOperation",
        "V4d_clone_reset" => "G1: prebuilt left operand Clone() + ResetPreparation; SetOperation is protected and is folded into V4e",
        "V4e_full_repro" => "G1: full V4 reproduced (fresh operands + set operation, no terminal) — closure control",
        "V4f_right_operand" => "G1: right operand only (ComplexEntity, (int)c.Id cast) — isolates the +96 B/query",
        "V4g_right_operand_nocast" => "G1: right operand without the cast (ComplexEntity.Id is long) — same entity, one conversion node less",
        "V4h_left_widening" => "G1: left entity with an int -> long widening conversion — same entity as V2, one conversion node more",
        "V4j_right_string_member" => "G1: right entity projected to string — no conversion and no long result; separates entity-source from long-projection cost",
        "V4k_left_binary_node" => "G1: left entity with one extra arithmetic node (s.Id + 1) — per-node expression-tree cost",
        "V4l_left_double_convert" => "G1: left entity converted to double — conversion node with a non-integer result type",
        "V4m_large_operand" => "G1: third class-mapped entity (LargeEntity) with a long member and no conversion — type-vs-entity discriminator",
        "V4n_right_nullable_int" => "G1: right entity with a nullable-int member and no conversion — same entity as V4g, no long",
        "V8_definition_record_only" => "G2: QueryDefinition record only, projection expression hoisted outside the loop — the attributable 'definition -> struct' pool",
        "V9_expressions_only" => "G2: both per-call expression trees, no record — compiler-emitted workload bytes (hoisting them would change the workload)",
        "V10_comparer_init" => "G3: one shell + cmd.GetQueryPlanEqualityComparer() (the lazy-init call QueryPlan's ctor makes)",
        "V10b_plan_key" => "G3: one shell + internal GetOrCreatePlanKey(null) — the actual forced plan-key path (comparers + QueryPlan + hash walk)",
        "V10c_comparer_init_three" => "G3: three fresh shells, each comparer-initialized — per-command multiplicative check (number is for three commands)",
        "V5_definition" => "QueryDefinition record + inline projection expression tree; includes the per-call expression tree exactly as the fluent call does",
        "V6_shell" => "V5 plus new QueryCommand<int>(context, definition, ownJoins: true); the ctor parameter is ownJoins (shape ctor), not forCache",
        "V7_prepared_control" => "prepared command reused once per iteration (mirrors A_Nextorm_Prepared_Except/_Intersect control)",
        _ => string.Empty,
    };

    private static Dictionary<string, DerivedSplit> BuildDerived(List<VariantRow> rows)
    {
        var derived = new Dictionary<string, DerivedSplit>(StringComparer.Ordinal);
        foreach (var operation in new[] { ExceptOperation, IntersectOperation })
        {
            var byVariant = rows.Where(row => row.Operation == operation)
                .ToDictionary(row => row.Variant, row => row.BytesPerQuery.Median, StringComparer.Ordinal);

            var v0 = byVariant["V0_full"];
            var v1 = byVariant["V1_operands"];
            var v2 = byVariant["V2_left_operand"];
            var v3 = byVariant["V3_clone_setop_prebuilt"];
            var v4 = byVariant["V4_setop_no_terminal"];
            var v4a = byVariant["V4a_left_operand"];
            var v4b = byVariant["V4b_two_operands"];
            var v4c = byVariant["V4c_clone_only"];
            var v4d = byVariant["V4d_clone_reset"];
            var v4e = byVariant["V4e_full_repro"];
            var v4f = byVariant["V4f_right_operand"];
            var v4g = byVariant["V4g_right_operand_nocast"];
            var v4h = byVariant["V4h_left_widening"];
            var v5 = byVariant["V5_definition"];
            var v6 = byVariant["V6_shell"];
            var v7 = byVariant["V7_prepared_control"];
            var v8 = byVariant["V8_definition_record_only"];
            var v9 = byVariant["V9_expressions_only"];
            var v10 = byVariant["V10_comparer_init"];
            var v10b = byVariant["V10b_plan_key"];
            var v10c = byVariant["V10c_comparer_init_three"];

            var shell = v6 - v5;
            derived[operation] = new DerivedSplit
            {
                ShellCountPerQuery = shell,
                Definition = v5,
                SingleOperand = v2,
                Operands = v1,
                PrebuiltCloneSetOperation = v3,
                ClonePlusSetOperation = v4 - v1,
                ExecutionResidual = v0 - v4,
                PreparedControl = v7,
                FullFair = v0,
                LeftOperand = v4a,
                RightOperand = v4b - v4a,
                CloneOnly = v4c,
                CloneReset = v4d,
                SetOperationAssembly = v4e - v4b,
                FullRepro = v4e,
                CastNodeBytes = v4f - v4g,
                WideningNodeBytes = v4h - v2,
                DefinitionRecordOnly = v8,
                ExpressionsOnly = v9,
                ComparerInitPerCommand = v10 - v6,
                PlanKeyPathExtra = v10b - v10,
                ComparerInitPerCommandFromThree = (v10c - (3 * v6)) / 3d,
            };
        }

        return derived;
    }

    private static Dictionary<string, Closure> BuildClosure(List<VariantRow> rows)
    {
        var closure = new Dictionary<string, Closure>(StringComparer.Ordinal);
        foreach (var operation in new[] { ExceptOperation, IntersectOperation })
        {
            var byVariant = rows.Where(row => row.Operation == operation)
                .ToDictionary(row => row.Variant, row => row.BytesPerQuery.Median, StringComparer.Ordinal);

            var v0 = byVariant["V0_full"];
            var v1 = byVariant["V1_operands"];
            var v2 = byVariant["V2_left_operand"];
            var v3 = byVariant["V3_clone_setop_prebuilt"];
            var v4 = byVariant["V4_setop_no_terminal"];
            var v4a = byVariant["V4a_left_operand"];
            var v4b = byVariant["V4b_two_operands"];
            var v4c = byVariant["V4c_clone_only"];
            var v4d = byVariant["V4d_clone_reset"];
            var v4e = byVariant["V4e_full_repro"];
            var v4f = byVariant["V4f_right_operand"];
            var v4g = byVariant["V4g_right_operand_nocast"];
            var v4h = byVariant["V4h_left_widening"];
            var v5 = byVariant["V5_definition"];
            var v6 = byVariant["V6_shell"];
            var v7 = byVariant["V7_prepared_control"];
            var v8 = byVariant["V8_definition_record_only"];
            var v9 = byVariant["V9_expressions_only"];
            var v10 = byVariant["V10_comparer_init"];
            var v10b = byVariant["V10b_plan_key"];
            var v10c = byVariant["V10c_comparer_init_three"];

            var v4j = byVariant["V4j_right_string_member"];
            var v4k = byVariant["V4k_left_binary_node"];
            var v4l = byVariant["V4l_left_double_convert"];
            var v4m = byVariant["V4m_large_operand"];
            var v4n = byVariant["V4n_right_nullable_int"];

            var shell = v6 - v5;
            var leftDefinition = v5;
            var rightOperand = v4b - v4a;
            var rightDefinition = rightOperand - shell;
            var setOperationAssembly = v4e - v4b;
            var preparePhase = v0 - v4e - v7;
            var plannerModel = (3 * shell) + (2 * v5);
            var attributed = v4e + preparePhase + v7;
            var constructionTotal = (2 * v8) + v9 + (2 * shell) + v4c;
            var comparerPerCommand = v10 - v6;
            var comparerPerCommandFromThree = (v10c - (3 * v6)) / 3d;

            closure[operation] = new Closure
            {
                V0 = v0,
                LeftOperand = v4a,
                LeftDefinitionAndExpression = leftDefinition,
                LeftShell = shell,
                RightOperand = rightOperand,
                RightDefinitionAndExpression = rightDefinition,
                RightExpressionMinusLeftExpression = rightDefinition - leftDefinition,
                TwoOperands = v4b,
                CloneOnly = v4c,
                CloneReset = v4d,
                CloneOnlyPlusReset = v4d - v4c,
                SetOperationAssembly = setOperationAssembly,
                SetOperationCallOnly = setOperationAssembly - v4d,
                CloneResetSetOperationMeasured = v3,
                FullSetopNoTerminal = v4e,
                PreparePhase = preparePhase,
                Execution = v7,
                ThreeShells = 3 * shell,
                TwoDefinitionsPlannerModel = 2 * v5,
                PlannerOriginalResidual = v4e - plannerModel,
                CastNodeBytes = v4f - v4g,
                WideningNodeBytes = v4h - v2,
                RightStringMemberBytes = v4j,
                RightStringMemberMinusLeft = v4j - v2,
                LongProjectionMinusStringMember = v4g - v4j,
                BinaryNodeBytes = v4k - v2,
                ConvertNodeBytes = v4l - v2,
                LargeOperandBytes = v4m,
                LargeOperandMinusLeft = v4m - v2,
                RightNullableIntBytes = v4n,
                RightNullableIntMinusLeft = v4n - v2,
                RightEntityFootprintBytes = v4j - v2,
                DefinitionRecordPoolPerQuery = 2 * v8,
                ExpressionsPerQuery = v9,
                OperandShellsPerQuery = 2 * shell,
                CloneShell = v4c,
                ConstructionModelTotal = constructionTotal,
                ConstructionModelResidual = v4e - constructionTotal,
                AttributedTotal = attributed,
                Residual = v0 - attributed,
                DefinitionRecordOnly = v8,
                ExpressionsOnly = v9,
                LeftExpressionOnly = v5 - v8,
                RightExpressionOnly = v9 - (v5 - v8),
                ComparerInitPerCommand = comparerPerCommand,
                PlanKeyPathExtra = v10b - v10,
                ComparerInitPerCommandFromThree = comparerPerCommandFromThree,
                ComparerInitMultiplicative = Math.Abs(comparerPerCommand - comparerPerCommandFromThree) <= 8d,
                LeftOperandMatchesV2 = Math.Abs(v4a - v2) < 0.01d,
                TwoOperandsMatchesV1 = Math.Abs(v4b - v1) < 0.01d,
                FullReproMatchesV4 = Math.Abs(v4e - v4) < 0.01d,
                ResidualNamedByComponents =
                    Math.Abs((rightDefinition - leftDefinition) - ((v4f - v4g) + (v4j - v2))) <= 8d,
            };
        }

        return closure;
    }

    private static Dictionary<string, Reconciliation> BuildReconciliation(List<VariantRow> rows)
    {
        var fullFair = new[] { 6528.1, 6512.1 };
        var warmPlanOnly = new[] { 5880.28, 5880.26 };
        var control = new[] { 760.0, 744.0 };
        var reconciliation = new Dictionary<string, Reconciliation>(StringComparer.Ordinal);
        var index = 0;
        foreach (var operation in new[] { ExceptOperation, IntersectOperation })
        {
            var byVariant = rows.Where(row => row.Operation == operation)
                .ToDictionary(row => row.Variant, row => row.BytesPerQuery.Median, StringComparer.Ordinal);

            var v0 = byVariant["V0_full"];
            var v4 = byVariant["V4_setop_no_terminal"];
            var v7 = byVariant["V7_prepared_control"];
            var preparePhase = v0 - v4 - v7;
            var bdnPreparePhase = warmPlanOnly[index] - 3632.16;

            reconciliation[operation] = new Reconciliation
            {
                V0MinusBdnFullFair = v0 - fullFair[index],
                V4MinusBdnConstruct = v4 - 3632.16,
                V7MinusBdnControl = v7 - control[index],
                PreparePhase = preparePhase,
                BdnPreparePhase = bdnPreparePhase,
                PreparePhaseDelta = preparePhase - bdnPreparePhase,
                ClosureResidual = v0 - (v4 + v7 + preparePhase),
            };
            index++;
        }

        return reconciliation;
    }

    private static double Median(double[] sorted)
    {
        var middle = sorted.Length / 2;
        return sorted.Length % 2 == 1
            ? sorted[middle]
            : (sorted[middle - 1] + sorted[middle]) / 2d;
    }

    private sealed class ProbePayload
    {
        public string Task { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public string GeneratedUtc { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;
        public int WarmupIterations { get; set; }
        public int BatchIterations { get; set; }
        public int BatchCount { get; set; }
        public HarnessInfo Harness { get; set; } = new();
        public List<VariantRow> Variants { get; set; } = new();
        public Dictionary<string, DerivedSplit> Derived { get; set; } = new(StringComparer.Ordinal);
        public Dictionary<string, Closure> Closure { get; set; } = new(StringComparer.Ordinal);
        public Dictionary<string, Reconciliation> Reconciliation { get; set; } = new(StringComparer.Ordinal);
        public BdnReference BdnReference { get; set; } = new();
        public string Notice { get; set; } = string.Empty;
    }

    private sealed class BdnReference
    {
        public string Source { get; set; } = string.Empty;
        public double[] FullFairBytesPerQuery { get; set; } = Array.Empty<double>();
        public double ConstructBytesPerQuery { get; set; }
        public double[] WarmPlanOnlyBytesPerQuery { get; set; } = Array.Empty<double>();
        public double[] ControlBytesPerQuery { get; set; } = Array.Empty<double>();
    }

    private sealed class Reconciliation
    {
        public double V0MinusBdnFullFair { get; set; }
        public double V4MinusBdnConstruct { get; set; }
        public double V7MinusBdnControl { get; set; }
        public double PreparePhase { get; set; }
        public double BdnPreparePhase { get; set; }
        public double PreparePhaseDelta { get; set; }
        public double ClosureResidual { get; set; }
    }

    private sealed class HarnessInfo
    {
        public string[] Argv { get; set; } = Array.Empty<string>();
        public string LaunchCommand { get; set; } = string.Empty;
        public string Db { get; set; } = string.Empty;
        public string DbSha256 { get; set; } = string.Empty;
        public string Context { get; set; } = string.Empty;
    }

    private sealed class VariantRow
    {
        public string Variant { get; set; } = string.Empty;
        public string Operation { get; set; } = string.Empty;
        public int Iterations { get; set; }
        public int BatchIterations { get; set; }
        public int BatchCount { get; set; }
        public long[] ThreadBatchBytes { get; set; } = Array.Empty<long>();
        public long[] ProcessBatchBytes { get; set; } = Array.Empty<long>();
        public Stats BytesPerQuery { get; set; } = new();
        public Stats ProcessTotalBytesPerQuery { get; set; } = new();
        public string Notes { get; set; } = string.Empty;
    }

    private sealed class Stats
    {
        public double Min { get; set; }
        public double Median { get; set; }
        public double Max { get; set; }
        public double Mean { get; set; }
    }

    private sealed class DerivedSplit
    {
        public double ShellCountPerQuery { get; set; }
        public double Definition { get; set; }
        public double SingleOperand { get; set; }
        public double Operands { get; set; }
        public double PrebuiltCloneSetOperation { get; set; }
        public double ClonePlusSetOperation { get; set; }
        public double ExecutionResidual { get; set; }
        public double PreparedControl { get; set; }
        public double FullFair { get; set; }
        public double LeftOperand { get; set; }
        public double RightOperand { get; set; }
        public double CloneOnly { get; set; }
        public double CloneReset { get; set; }
        public double SetOperationAssembly { get; set; }
        public double FullRepro { get; set; }
        public double CastNodeBytes { get; set; }
        public double WideningNodeBytes { get; set; }
        public double DefinitionRecordOnly { get; set; }
        public double ExpressionsOnly { get; set; }
        public double ComparerInitPerCommand { get; set; }
        public double PlanKeyPathExtra { get; set; }
        public double ComparerInitPerCommandFromThree { get; set; }
    }

    /// <summary>
    /// Byte-exact reconciliation of V0 from the named stages: three command shells + two
    /// definitions/expression trees + clone/reset/set-operation + prepare + execution. The planner's
    /// two-definition model left a 96 B/query category residual; <see cref="PlannerOriginalResidual"/>
    /// reproduces it and <see cref="CastNodeBytes"/> names it.
    /// </summary>
    private sealed class Closure
    {
        public double V0 { get; set; }
        public double LeftOperand { get; set; }
        public double LeftDefinitionAndExpression { get; set; }
        public double LeftShell { get; set; }
        public double RightOperand { get; set; }
        public double RightDefinitionAndExpression { get; set; }
        public double RightExpressionMinusLeftExpression { get; set; }
        public double TwoOperands { get; set; }
        public double CloneOnly { get; set; }
        public double CloneReset { get; set; }
        public double CloneOnlyPlusReset { get; set; }
        public double SetOperationAssembly { get; set; }
        public double SetOperationCallOnly { get; set; }
        public double CloneResetSetOperationMeasured { get; set; }
        public double FullSetopNoTerminal { get; set; }
        public double PreparePhase { get; set; }
        public double Execution { get; set; }
        public double ThreeShells { get; set; }
        public double TwoDefinitionsPlannerModel { get; set; }
        public double PlannerOriginalResidual { get; set; }
        public double CastNodeBytes { get; set; }
        public double WideningNodeBytes { get; set; }
        public double RightStringMemberBytes { get; set; }
        public double RightStringMemberMinusLeft { get; set; }
        public double LongProjectionMinusStringMember { get; set; }
        public double BinaryNodeBytes { get; set; }
        public double ConvertNodeBytes { get; set; }
        public double LargeOperandBytes { get; set; }
        public double LargeOperandMinusLeft { get; set; }
        public double RightNullableIntBytes { get; set; }
        public double RightNullableIntMinusLeft { get; set; }
        public double RightEntityFootprintBytes { get; set; }
        public double DefinitionRecordPoolPerQuery { get; set; }
        public double ExpressionsPerQuery { get; set; }
        public double OperandShellsPerQuery { get; set; }
        public double CloneShell { get; set; }
        public double ConstructionModelTotal { get; set; }
        public double ConstructionModelResidual { get; set; }
        public double AttributedTotal { get; set; }
        public double Residual { get; set; }
        public double DefinitionRecordOnly { get; set; }
        public double ExpressionsOnly { get; set; }
        public double LeftExpressionOnly { get; set; }
        public double RightExpressionOnly { get; set; }
        public double ComparerInitPerCommand { get; set; }
        public double PlanKeyPathExtra { get; set; }
        public double ComparerInitPerCommandFromThree { get; set; }
        public bool ComparerInitMultiplicative { get; set; }
        public bool LeftOperandMatchesV2 { get; set; }
        public bool TwoOperandsMatchesV1 { get; set; }
        public bool FullReproMatchesV4 { get; set; }
        public bool ResidualNamedByComponents { get; set; }
    }
}
