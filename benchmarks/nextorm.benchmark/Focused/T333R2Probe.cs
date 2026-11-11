using System.Text.Json;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Benchmark;

/// <summary>
/// T333 revision r=2 — bounded, untimed probe entry point of the CANDIDATE (worktree) build.
///
/// It runs no BenchmarkDotNet job. It records, for the candidate arms, (i) the same database facts the
/// preserved probe records, (ii) the exact values returned for the same fixed id sequence, and (iii) — for
/// the <c>None</c> arm — the production command lifetime observed through the internal counters: exactly one
/// command created and exactly one disposed per execution, and exactly <see cref="Iterations"/> of each for
/// one full benchmark invocation.
/// </summary>
internal static class T333R2Probe
{
    private const int Iterations = 100;

    private static readonly long[] LargeIds = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 5000, 9998, 9999, 10000];
    private static readonly int[] BenchCIds = Enumerable.Range(0, Iterations).ToArray();

    public static int Run(string outputPath)
    {
        var builder = new DataContextBuilder();
        builder.UseSqlite(BenchDb.FilePath);
        using var db = builder.CreateDataContext();

        var repo = new TestDataRepository(db);
        var manager = (IConnectionManager)db;
        manager.EnsureConnectionOpen();
        var connection = manager.GetConnection()
            ?? throw new InvalidOperationException("The context returned no connection.");

        var document = new ProbeDocument
        {
            Tree = "candidate-worktree",
            DllDirectory = AppContext.BaseDirectory,
            DbPath = BenchDb.FilePath,
            RowCounts = new Dictionary<string, int>
            {
                ["simple_entity"] = ScalarCount(connection, "select count(*) from simple_entity;"),
                ["large_table"] = ScalarCount(connection, "select count(*) from large_table;"),
            },
        };

        var connectionLarge = repo.LargeEntity
            .Where(it => it.Id == SqlFunctions.Parameter<long>(0))
            .Select(it => it.Id)
            .Prepare();

        var warmBenchC = repo.SimpleEntity
            .Where(it => it.Id == SqlFunctions.Parameter<int>(0))
            .Select(it => it.Id)
            .Prepare();

        var noneBenchC0 = repo.SimpleEntity
            .Where(it => it.Id == SqlFunctions.Parameter<int>(0))
            .Select(it => it.Id)
            .Prepare(new PrepareOptions { Cache = PreparedCommandCache.None, Streaming = true });

        var noneBenchC = (DbPreparedQueryCommand<int>)noneBenchC0;

        // Warm every arm once, exactly like the benchmark setup does.
        _ = connectionLarge.ToList(db, 0L);
        _ = warmBenchC.ToList(db, 0);
        foreach (var _ in noneBenchC0.ToEnumerable(db, 0))
        {
        }

        var largeCmd = (DbPreparedQueryCommand<long>)connectionLarge;
        var warmCmd = (DbPreparedQueryCommand<int>)warmBenchC;

        document.Sql["large"] = largeCmd.CommandText;
        document.Sql["benchc"] = warmCmd.CommandText;
        document.ParamNames["large"] = largeCmd.ParamSeeds[0].Name;
        document.ParamNames["benchc"] = warmCmd.ParamSeeds[0].Name;
        document.Sql["benchc_none"] = noneBenchC.CommandText;
        document.ParamNames["benchc_none"] = noneBenchC.ParamSeeds[0].Name;

        document.Arms["ConnectionLarge"] = Collect(
            LargeIds,
            largeCmd.CommandText,
            largeCmd.ParamSeeds[0].Name,
            id => connectionLarge.ToList(db, id).Select(v => v).ToList());

        document.Arms["CandidateWarmBenchC"] = Collect(
            BenchCIds.Select(i => (long)i).ToArray(),
            warmCmd.CommandText,
            warmCmd.ParamSeeds[0].Name,
            id => warmBenchC.ToList(db, (int)id).Select(v => (long)v).ToList());

        document.Arms["NoneBenchC"] = Collect(
            BenchCIds.Select(i => (long)i).ToArray(),
            noneBenchC.CommandText,
            noneBenchC.ParamSeeds[0].Name,
            id => CountStreamed(noneBenchC0, db, (int)id));

        // --- production command lifetime of the None arm ------------------------------------------------
        var createdBefore = noneBenchC.CreatedCommandCount;
        var disposedBefore = noneBenchC.DisposedCommandCount;

        var singleRows = CountStreamed(noneBenchC0, db, 0);

        var singleCreated = noneBenchC.CreatedCommandCount - createdBefore;
        var singleDisposed = noneBenchC.DisposedCommandCount - disposedBefore;

        var createdLoop = noneBenchC.CreatedCommandCount;
        var disposedLoop = noneBenchC.DisposedCommandCount;
        var rows = 0;
        for (var i = 0; i < Iterations; i++)
        {
            foreach (var _ in noneBenchC0.ToEnumerable(db, i))
                rows++;
        }
        var loopCreated = noneBenchC.CreatedCommandCount - createdLoop;
        var loopDisposed = noneBenchC.DisposedCommandCount - disposedLoop;
        var publishedAfterLoop = noneBenchC.PeekCommand(connection);

        document.Extra["none_lifetime"] = new Dictionary<string, object?>
        {
            ["policy"] = noneBenchC.CachePolicy.ToString(),
            ["streaming"] = noneBenchC.StreamingEnabled,
            ["single_execution"] = new Dictionary<string, object?>
            {
                ["ids"] = new[] { 0 },
                ["rows"] = singleRows.Count,
                ["created_delta"] = singleCreated,
                ["disposed_delta"] = singleDisposed,
            },
            ["full_invocation"] = new Dictionary<string, object?>
            {
                ["iterations"] = Iterations,
                ["rows"] = rows,
                ["created_delta"] = loopCreated,
                ["disposed_delta"] = loopDisposed,
            },
            ["command_published_after_loop"] = publishedAfterLoop is not null,
            ["shape"] = "Prepare(new PrepareOptions { Cache = PreparedCommandCache.None, Streaming = true }) + ToEnumerable row counting",
            ["deviation_from_benchc"] = "bench-c's C_CommandPerInvoke built a raw ADO.NET command per invocation and counted reader rows without mapping; the rv2 None arm goes through the real production None execution/lease path and its streaming enumerator, which additionally creates an enumerator, applies the projection delegate per row and disposes both reader and command per execution",
            ["directional_bias"] = "upward on the None numerator (more per-query work than a bare reader + row loop), i.e. conservative against a PASS: the deviation cannot make None look cheaper than the historical shape",
        };

        // Untimed smoke run of the benchmark bodies themselves: exactly one invocation per arm, expected to
        // return 100 (the 100-query loop over ids 0..99, each id yielding one row). This proves the bodies the
        // campaign will time are correct before any timed run.
        var benchmarks = new T333R2Benchmarks();
        benchmarks.Setup();
        document.Extra["benchmark_body_smoke"] = new Dictionary<string, object?>
        {
            ["note"] = "one untimed invocation of each benchmark body; the loop covers ids 0..99, so the returned count is the number of those ids that exist in the workload (large_table ids 1..10000 -> 99; simple_entity ids 1..10 -> 10)",
            ["expected_rows_per_invocation"] = new Dictionary<string, int>
            {
                ["ConnectionLarge"] = 99,
                ["NoneBenchC"] = 10,
            },
            ["rows_per_invocation"] = new Dictionary<string, int>
            {
                ["ConnectionLarge"] = benchmarks.ConnectionLarge(),
                ["NoneBenchC"] = benchmarks.NoneBenchC(),
            },
        };
        benchmarks.Cleanup();

        File.WriteAllText(outputPath, JsonSerializer.Serialize(document, JsonOptions));
        Console.WriteLine($"probe written: {outputPath}");
        return 0;
    }

    private static int ScalarCount(System.Data.Common.DbConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static List<long> CountStreamed(IPreparedQueryCommand<int> prepared, IDataContext db, int id)
    {
        var rows = new List<long>();
        foreach (var value in prepared.ToEnumerable(db, id))
            rows.Add(value);
        return rows;
    }

    private static ArmResult Collect(long[] ids, string sql, string paramName, Func<long, List<long>> execute)
    {
        var arm = new ArmResult { Sql = sql, ParamName = paramName, Ids = ids, Values = new List<long[]>(), RowCounts = new int[ids.Length] };
        for (var i = 0; i < ids.Length; i++)
        {
            var rows = execute(ids[i]);
            arm.Values.Add(rows.ToArray());
            arm.RowCounts[i] = rows.Count;
            arm.TotalRows += rows.Count;
        }
        return arm;
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private sealed class ProbeDocument
    {
        public string Tree { get; set; } = "";
        public string DllDirectory { get; set; } = "";
        public string DbPath { get; set; } = "";
        public Dictionary<string, int> RowCounts { get; set; } = new();
        public Dictionary<string, string> Sql { get; set; } = new();
        public Dictionary<string, string> ParamNames { get; set; } = new();
        public Dictionary<string, ArmResult> Arms { get; set; } = new();
        public Dictionary<string, object?> Extra { get; set; } = new();
    }

    private sealed class ArmResult
    {
        public string Sql { get; set; } = "";
        public string ParamName { get; set; } = "";
        public long[] Ids { get; set; } = [];
        public List<long[]> Values { get; set; } = new();
        public int[] RowCounts { get; set; } = [];
        public int TotalRows { get; set; }
    }
}
