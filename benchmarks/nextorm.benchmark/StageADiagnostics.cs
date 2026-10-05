using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Benchmark;

/// <summary>
/// Iteration 15 / #183 Stage A diagnostic batches. These run <b>outside</b> BenchmarkDotNet and outside
/// any timed operation: they prove the cache hits, the plan/parameter counts and the boundary
/// correctness of the fresh-fluent cached path, then serialise the observed facts to JSON so the
/// evidence runner can persist them verbatim.
/// </summary>
/// <remarks>
/// Invoked from <c>Program</c> as <c>--stage-a &lt;mode&gt;</c> where mode is <c>diagnostics</c>,
/// <c>correctness</c>, <c>profile</c> or <c>all</c>. The JSON is written to
/// <c>$NEXTORM_STAGE_A_OUT</c> when set, otherwise under the benchmark artifacts folder. No production
/// core code is touched; the hit proof uses the public <c>GetPreparedQueryCommand</c> result identity
/// (a hit returns the cached command instance), exactly as the production tests do.
/// </remarks>
internal static class StageADiagnostics
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static int Run(string mode)
    {
        var root = new Dictionary<string, object?>
        {
            ["mode"] = mode,
            ["runtimeVersion"] = Environment.Version.ToString(),
            ["frameworkDescription"] = RuntimeInformation.FrameworkDescription,
            ["osDescription"] = RuntimeInformation.OSDescription,
            ["processArchitecture"] = RuntimeInformation.ProcessArchitecture.ToString(),
            ["processorCount"] = Environment.ProcessorCount,
            ["databaseFile"] = BenchDb.FilePath,
        };

        var sw = Stopwatch.StartNew();
        try
        {
            switch (mode)
            {
                case "diagnostics":
                    RunDiagnostics(root);
                    break;
                case "correctness":
                    RunCorrectness(root);
                    break;
                case "profile":
                    RunProfile(root);
                    break;
                case "all":
                default:
                    RunDiagnostics(root);
                    RunCorrectness(root);
                    break;
            }
        }
        catch (Exception ex)
        {
            root["error"] = ex.ToString();
            root["exitCode"] = 2;
            root["durationSeconds"] = sw.Elapsed.TotalSeconds;
            Emit(root, mode);
            Console.Error.WriteLine(ex);
            return 2;
        }

        sw.Stop();
        root["durationSeconds"] = sw.Elapsed.TotalSeconds;
        root["exitCode"] = 0;
        Emit(root, mode);
        return 0;
    }

    private static void Emit(Dictionary<string, object?> root, string mode)
    {
        var json = JsonSerializer.Serialize(root, JsonOptions);
        var outPath = Environment.GetEnvironmentVariable("NEXTORM_STAGE_A_OUT");
        if (string.IsNullOrEmpty(outPath))
            outPath = Path.Combine(BenchmarkArtifacts.Path, "iteration15-stage-a", $"stage-a-{mode}.json");

        var dir = Path.GetDirectoryName(outPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(outPath, json);

        Console.WriteLine("---- STAGE-A-JSON-BEGIN ----");
        Console.WriteLine(json);
        Console.WriteLine("---- STAGE-A-JSON-END ----");
        Console.Error.WriteLine($"stage-a diagnostics written to: {outPath}");
    }

    private static (IDataContext Db, TestDataRepository Repo) Create()
    {
        var builder = new DataContextBuilder();
        builder.UseSqlite(BenchDb.FilePath);
        var db = builder.CreateDataContext();
        var repo = new TestDataRepository(db);
        ((IConnectionManager)db).EnsureConnectionOpen();
        return (db, repo);
    }

    private static CancellationToken Ct => CancellationToken.None;

    // ---- diagnostics ---------------------------------------------------------------------------

    private static void RunDiagnostics(Dictionary<string, object?> root)
    {
        var (db, repo) = Create();
        db.PurgeQueryCache();

        root["where"] = FreshHitProof(
            db,
            value => repo.SimpleEntity.Where(it => it.Id == value).Select(it => it.Id),
            iterations: 100);

        root["join"] = FreshHitProof(
            db,
            value => repo.LargeEntity
                .Join(repo.SimpleEntity, (t1, t2) => t1.Id == t2.Id)
                .Where(p => p.Item2.Id == value)
                .Select(p => new LargeEntity { Id = p.Item1.Id, Dt = p.Item1.Dt, Str = p.Item1.Str }),
            iterations: 100);

        // Same plan as the captured arm but with an explicit runtime placeholder: no ExtractParams
        // refresh (the value is applied at execution time). This is the refresh-cost control.
        root["where_runtime_placeholder"] = FreshHitProof(
            db,
            _ => repo.SimpleEntity.Where(it => it.Id == SqlFunctions.Parameter<int>(0)).Select(it => it.Id),
            iterations: 100);

        // Constant predicate: renders without a parameter (NoParams), the no-refresh baseline.
        root["where_constant"] = FreshHitProof(
            db,
            _ => repo.SimpleEntity.Where(it => it.Id == 5).Select(it => it.Id),
            iterations: 100);

        // The combined lookup+equality+ExtractParams stage is split here on the public surface by
        // comparing a hit that needs a parameter refresh with one that does not.
        root["paramRefreshAttribution"] = MeasureParamRefresh(db, repo);
    }

    // A measured cached-hit loop on the public GetPreparedQueryCommand surface.
    private readonly record struct HitLoopMeasurement(
        int Iterations,
        double NsPerOp,
        double BytesPerOp,
        long AllocatedBytes,
        long Hits,
        long Misses,
        bool NeedsParamRefresh);

    private static HitLoopMeasurement MeasureHitLoop(
        IDataContext db,
        Func<int, QueryCommand<int>> build,
        int iterations)
    {
        // Warm the plan and JIT the loop before measuring, so every measured call is a cache hit and
        // no first-call cost lands in the sample.
        for (var i = 0; i < 32; i++) _ = db.GetPreparedQueryCommand(build(i), false, true, Ct);

        DbPreparedQueryCommand<int>? cached = null;
        var hits = 0;
        var misses = 0;
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            var prep = (DbPreparedQueryCommand<int>)db.GetPreparedQueryCommand(build(i), false, true, Ct);
            if (cached is null || !ReferenceEquals(cached, prep))
            {
                cached = prep;
                misses++;
            }
            else
            {
                hits++;
            }
        }
        sw.Stop();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        return new HitLoopMeasurement(
            iterations,
            sw.Elapsed.TotalNanoseconds / iterations,
            (double)allocated / iterations,
            allocated,
            hits,
            misses,
            cached?.NeedsParamRefresh ?? false);
    }

    private static Dictionary<string, object?> MeasureParamRefresh(IDataContext db, TestDataRepository repo)
    {
        const int iterations = 2_000;

        // Captured local: the cached command needs a parameter refresh, so every cache hit re-runs
        // ExtractParams (MakeSelect in parameter mode) through the public hit path.
        var refreshNeeded = MeasureHitLoop(
            db,
            i => repo.SimpleEntity.Where(it => it.Id == i).Select(it => it.Id),
            iterations);

        // Runtime placeholder control: same SQL shape, parameter count and rows, but NeedsParamRefresh
        // is false (SqlFunctions.Parameter is applied at execution time), so the hit path skips
        // ExtractParams. The delta is the parameter-refresh cost plus the single-leaf expression-shape
        // difference (captured member access vs runtime method call), not a pure ExtractParams sample.
        var refreshSkipped = MeasureHitLoop(
            db,
            _ => repo.SimpleEntity.Where(it => it.Id == SqlFunctions.Parameter<int>(0)).Select(it => it.Id),
            iterations);

        return new Dictionary<string, object?>
        {
            ["iterations"] = iterations,
            ["refreshNeeded"] = Describe(refreshNeeded),
            ["refreshSkipped"] = Describe(refreshSkipped),
            ["refreshDeltaNsPerOp"] = refreshNeeded.NsPerOp - refreshSkipped.NsPerOp,
            ["refreshDeltaBytesPerOp"] = refreshNeeded.BytesPerOp - refreshSkipped.BytesPerOp,
            ["note"] =
                "Both arms are warm cache hits on the same SQL shape/parameter count/rows. The refresh " +
                "arm re-runs ExtractParams on every hit; the control (runtime SqlFunctions.Parameter " +
                "placeholder) skips it. The control predicate differs in one leaf (captured member access " +
                "vs runtime method call), so the delta is the refresh cost plus that leaf's " +
                "lookup/equality residual, not a pure ExtractParams isolation.",
        };
    }

    private static Dictionary<string, object?> Describe(HitLoopMeasurement m) => new()
    {
        ["iterations"] = m.Iterations,
        ["hits"] = m.Hits,
        ["misses"] = m.Misses,
        ["needsParamRefresh"] = m.NeedsParamRefresh,
        ["meanNsPerOp"] = m.NsPerOp,
        ["allocatedBytesPerOp"] = m.BytesPerOp,
        ["allocatedBytes"] = m.AllocatedBytes,
    };

    private static Dictionary<string, object?> FreshHitProof<T>(
        IDataContext db,
        Func<int, QueryCommand<T>> build,
        int iterations)
    {
        DbPreparedQueryCommand<T>? cached = null;
        var misses = 0;
        var hits = 0;
        var paramValueMismatches = 0;
        var sqlStatements = new HashSet<string>(StringComparer.Ordinal);
        var paramCounts = new HashSet<int>();
        var paramValues = new HashSet<object?>();

        for (var i = 0; i < iterations; i++)
        {
            var prep = (DbPreparedQueryCommand<T>)db.GetPreparedQueryCommand(build(i), false, true, Ct);

            if (cached is null || !ReferenceEquals(cached, prep))
            {
                cached = prep;
                misses++;
            }
            else
            {
                hits++;
            }

            if (prep.SqlStmt is not null) sqlStatements.Add(prep.SqlStmt);
            paramCounts.Add(prep.DbCommandParams.Count);
            if (prep.DbCommandParams.Count > 0)
            {
                var value = prep.DbCommandParams[0].Value;
                paramValues.Add(value);
                if (!Equals(value, i)) paramValueMismatches++;
            }
        }

        var needsRefresh = cached?.NeedsParamRefresh ?? false;
        return new Dictionary<string, object?>
        {
            ["iterations"] = iterations,
            ["misses"] = misses,
            ["hits"] = hits,
            ["hitProofOk"] = misses == 1 && hits == iterations - 1,
            ["distinctSql"] = sqlStatements.Count,
            ["sqlStmt"] = cached?.SqlStmt,
            ["paramCounts"] = paramCounts.OrderBy(x => x).ToArray(),
            ["paramValuesSeen"] = paramValues.Count,
            ["paramValueMismatches"] = paramValueMismatches,
            ["needsParamRefresh"] = needsRefresh,
            ["noParams"] = cached?.NoParams,
            ["paramRefreshProved"] = needsRefresh ? paramValueMismatches == 0 && paramValues.Count == iterations : null,
        };
    }

    // ---- correctness ---------------------------------------------------------------------------

    private static void RunCorrectness(Dictionary<string, object?> root)
    {
        var (db, repo) = Create();
        db.PurgeQueryCache();

        var wherePrepared = repo.SimpleEntity
            .Where(it => it.Id == SqlFunctions.Parameter<int>(0))
            .Select(it => it.Id)
            .Prepare();
        var joinPrepared = repo.LargeEntity
            .Join(repo.SimpleEntity, (t1, t2) => t1.Id == t2.Id)
            .Where(p => p.Item2.Id == SqlFunctions.Parameter<int>(0))
            .Select(p => new LargeEntity { Id = p.Item1.Id, Dt = p.Item1.Dt, Str = p.Item1.Str })
            .Prepare();

        var whereCachedParam = (DbPreparedQueryCommand<int>)db.GetPreparedQueryCommand(
            repo.SimpleEntity.Where(it => it.Id == SqlFunctions.Parameter<int>(0)).Select(it => it.Id), false, true, Ct);
        var joinCachedParam = (DbPreparedQueryCommand<LargeEntity>)db.GetPreparedQueryCommand(
            repo.LargeEntity.Join(repo.SimpleEntity, (t1, t2) => t1.Id == t2.Id)
                .Where(p => p.Item2.Id == SqlFunctions.Parameter<int>(0))
                .Select(p => new LargeEntity { Id = p.Item1.Id, Dt = p.Item1.Dt, Str = p.Item1.Str }), false, true, Ct);

        var checks = new Dictionary<string, object?>
        {
            ["where_sql_match"] = string.Equals(whereCachedParam.SqlStmt, ((DbPreparedQueryCommand<int>)wherePrepared).SqlStmt, StringComparison.Ordinal),
            ["join_sql_match"] = string.Equals(joinCachedParam.SqlStmt, ((DbPreparedQueryCommand<LargeEntity>)joinPrepared).SqlStmt, StringComparison.Ordinal),
        };

        var whereRowsMatch = true;
        var joinRowsMatch = true;
        for (var v = 0; v < 5; v++)
        {
            var value = v;
            var whereCaptured = repo.SimpleEntity.Where(it => it.Id == value).Select(it => it.Id).ToList();
            var wherePrep = wherePrepared.ToList(db, value);
            whereRowsMatch &= whereCaptured.SequenceEqual(wherePrep);

            var joinCaptured = repo.LargeEntity
                .Join(repo.SimpleEntity, (t1, t2) => t1.Id == t2.Id)
                .Where(p => p.Item2.Id == value)
                .Select(p => new LargeEntity { Id = p.Item1.Id, Dt = p.Item1.Dt, Str = p.Item1.Str })
                .ToList();
            var joinPrep = joinPrepared.ToList(db, value);
            joinRowsMatch &= joinCaptured.Count == joinPrep.Count
                && joinCaptured.Select(r => r.Id).SequenceEqual(joinPrep.Select(r => r.Id));
        }

        checks["where_rows_match"] = whereRowsMatch;
        checks["join_rows_match"] = joinRowsMatch;

        // Controls: Any / First / Single / Where must keep returning correct results on the cached path.
        checks["any"] = repo.SimpleEntity.Where(it => it.Id == 1).Select(it => it.Id).Any();
        checks["first"] = repo.SimpleEntity.Where(it => it.Id == 1).Select(it => it.Id).First() == 1;
        checks["single"] = repo.SimpleEntity.Where(it => it.Id == 1).Select(it => it.Id).SingleOrDefault() == 1;
        checks["where_rows"] = repo.SimpleEntity.Where(it => it.Id == 1).Select(it => it.Id).ToList().Count == 1;

        // No sticky queryCommand.Cache=false / _dontCache: the command must remain cacheable after use.
        var stickyCommand = repo.SimpleEntity.Where(it => it.Id == 1).Select(it => it.Id);
        _ = db.GetPreparedQueryCommand(stickyCommand, false, true, Ct);
        checks["sticky_cache_false_absent"] = stickyCommand.Cache;

        // Matched SQL/types/rows summary consumed by the evidence runner.
        checks["all_pass"] = checks.Values.All(v => v is bool b && b);

        root["correctness"] = checks;
    }

    // ---- profiler workload ---------------------------------------------------------------------

    private static void RunProfile(Dictionary<string, object?> root)
    {
        var seconds = int.TryParse(Environment.GetEnvironmentVariable("NEXTORM_STAGE_A_PROFILE_SECONDS"), out var s) && s > 0 ? s : 12;
        var (db, repo) = Create();
        db.PurgeQueryCache();

        for (var i = 0; i < 3; i++)
        {
            _ = db.GetPreparedQueryCommand(repo.SimpleEntity.Where(it => it.Id == i).Select(it => it.Id), false, true, Ct);
            _ = db.GetPreparedQueryCommand(repo.LargeEntity
                .Join(repo.SimpleEntity, (t1, t2) => t1.Id == t2.Id)
                .Where(p => p.Item2.Id == i)
                .Select(p => new LargeEntity { Id = p.Item1.Id, Dt = p.Item1.Dt, Str = p.Item1.Str }), false, true, Ct);
        }

        long ops = 0;
        long sink = 0;
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed.TotalSeconds < seconds)
        {
            for (var i = 0; i < 100 && sw.Elapsed.TotalSeconds < seconds; i++)
            {
                sink += JoinCachedToList(repo, i);
                sink += WhereCachedPlanOnly(db, repo, i);
                // Two logical ops per inner iteration. The loop can exit mid-batch on the time budget,
                // so ops is incremented here rather than by a fixed 200 per outer pass (no over-count).
                ops += 2;
            }
        }
        sw.Stop();

        root["profile"] = new Dictionary<string, object?>
        {
            ["targetSeconds"] = seconds,
            ["elapsedSeconds"] = sw.Elapsed.TotalSeconds,
            ["logicalOps"] = ops,
            ["sink"] = sink,
            ["workload"] = "fresh captured Join ToList + fresh captured Where plan-only",
        };
    }

    private static long JoinCachedToList(TestDataRepository repo, int value)
    {
        long sum = 0;
        foreach (var row in repo.LargeEntity
            .Join(repo.SimpleEntity, (t1, t2) => t1.Id == t2.Id)
            .Where(p => p.Item2.Id == value)
            .Select(p => new LargeEntity { Id = p.Item1.Id, Dt = p.Item1.Dt, Str = p.Item1.Str })
            .ToList())
        {
            sum += row.Id;
        }
        return sum;
    }

    private static long WhereCachedPlanOnly(IDataContext db, TestDataRepository repo, int value)
    {
        var command = repo.SimpleEntity.Where(it => it.Id == value).Select(it => it.Id);
        var prepared = (DbPreparedQueryCommand<int>)db.GetPreparedQueryCommand(command, false, true, Ct);
        return prepared.DbCommandParams.Count == 0 ? 0 : Convert.ToInt64(prepared.DbCommandParams[0].Value ?? 0);
    }
}
