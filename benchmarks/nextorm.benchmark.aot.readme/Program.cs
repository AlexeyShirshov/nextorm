using System.ComponentModel.DataAnnotations.Schema;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Running;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Benchmark.AotReadme;

/// <summary>
/// Matched JIT vs real NativeAOT measurement of the README benchmark scenarios, restricted to the
/// <em>prepared</em> NextORM arms the published tables report. Both arms are out-of-process
/// <c>Job.Default</c> runs of the same benchmark assembly on the same net10.0 TFM and the same real
/// benchmark database; only the runtime differs (CoreCLR + JIT vs a compiled native executable).
/// </summary>
public static class Program
{
    public static void Main(string[] args)
        => BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, new ReadmeAotConfig());
}

/// <summary>Two matched out-of-process jobs. No warmup/iteration overrides: plain <c>Job.Default</c>.</summary>
public sealed class ReadmeAotConfig : ManualConfig
{
    public ReadmeAotConfig()
    {
        var dbPath = ReadmeDb.FilePath;
        ArtifactsPath = Environment.GetEnvironmentVariable("NEXTORM_AOT_README_ARTIFACTS")
            ?? Path.Combine(Path.GetTempPath(), "nextorm-aot-readme", "artifacts");
        // The NativeAOT arm publishes nextorm.core with ILC; that build is slow, and the two generated
        // executables share the same intermediate output, so builds stay sequential.
        WithBuildTimeout(TimeSpan.FromMinutes(30));
        WithOptions(ConfigOptions.DisableParallelBuild);
        Orderer = new DefaultOrderer(SummaryOrderPolicy.FastestToSlowest);
        AddLogger(ConsoleLogger.Default);
        AddColumnProvider(DefaultColumnProviders.Instance);
        AddExporter(MarkdownExporter.GitHub);
        AddExporter(CsvExporter.Default);

        AddJob(Job.Default.WithRuntime(CoreRuntime.Core10_0)
            .WithEnvironmentVariable("NEXTORM_BENCH_DB", dbPath)
            .WithId("JIT"));

        AddJob(Job.Default.WithRuntime(NativeAotRuntime.Net10_0)
            .WithEnvironmentVariable("NEXTORM_BENCH_DB", dbPath)
            .WithId("AOT"));
    }
}

/// <summary>Same resolution order as <c>BenchDb</c> in the benchmark project (env → tmpfs → cwd/data).</summary>
internal static class ReadmeDb
{
    private static readonly string _filePath = Resolve();

    public static string FilePath => _filePath;

    private static string Resolve()
    {
        var custom = Environment.GetEnvironmentVariable("NEXTORM_BENCH_DB");
        if (!string.IsNullOrEmpty(custom))
            return custom;

        const string wslPath = "/tmp/nextorm-bench/test.db";
        if (File.Exists(wslPath))
            return wslPath;

        return Path.Combine(Directory.GetCurrentDirectory(), "data", "test.db");
    }
}

/// <summary>
/// Mirrors the seven end-to-end README scenarios one by one, always through the prepared NextORM arm
/// of the original class (never the fastest arbitrary arm):
/// <list type="bullet">
/// <item>Data fetch — <c>SqliteBenchmarkIteration.Nextorm_Prepared_ToListAsync</c></item>
/// <item>Wide data fetch — <c>SqliteBenchmarkLargeIteration.Nextorm_Prepared_ToListAsync</c></item>
/// <item>Where — <c>SqliteBenchmarkWhere.Nextorm_Prepared_ToListAsync</c> (100 ops/op)</item>
/// <item>Simulate work — <c>SqliteBenchmarkSimulteWork.Nextorm_Prepared_ToListAsync</c> (500 rows × 10 inner)</item>
/// <item>Any — <c>SqliteBenchmarkAny.Nextorm_Prepared</c> (100 ops/op)</item>
/// <item>FirstOrDefault — <c>SqliteBenchmarkFirst.Nextorm_Prepared_Scalar_FirstOrDefault</c> (10 ops/op),
/// plus the entity arm <c>Nextorm_Prepared_Entity_FirstOrDefault</c> reported separately</item>
/// <item>SingleOrDefault — <c>SqliteBenchmarkSingle.Nextorm_Prepared_SingleOrDefault</c> (10 ops/op)</item>
/// </list>
/// Entity mappings match <c>ISimpleEntity.cs</c> / <c>ILargeEntity.cs</c>; the database is the same real
/// <c>data/test.db</c> copy resolved exactly like <c>BenchDb</c>.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByJob)]
public class ReadmeE2EBenchmark
{
    private const int LargeListSize = 500;
    private const int SmallIterations = 10;
    private const int WhereIterations = 100;
    private const int AnyIterations = 100;

    private IDataContext _db = null!;
    private IPreparedQueryCommand<SimpleEntity> _cmdDataFetch = null!;
    private IPreparedQueryCommand<LargeEntity> _cmdWide = null!;
    private IPreparedQueryCommand<SimpleEntity> _cmdWhere = null!;
    private IPreparedQueryCommand<LargeEntity> _cmdSimulate = null!;
    private IPreparedQueryCommand<SimpleEntity?> _cmdSimulateInner = null!;
    private IPreparedQueryCommand<bool> _cmdAny = null!;
    private IPreparedQueryCommand<int> _cmdFirstScalar = null!;
    private IPreparedQueryCommand<int> _cmdSingle = null!;

    [GlobalSetup]
    public void Setup()
    {
        var builder = new DataContextBuilder();
        builder.UseSqlite(ReadmeDb.FilePath);
        _db = builder.CreateDataContext();
        ((IConnectionManager)_db).EnsureConnectionOpen();

        _cmdDataFetch = _db.From<SimpleEntity>().Prepare();
        _cmdWide = _db.From<LargeEntity>()
            .Select(entity => new LargeEntity { Id = entity.Id, Str = entity.Str, Dt = entity.Dt })
            .Prepare();
        _cmdWhere = _db.From<SimpleEntity>()
            .Where(it => it.Id == SqlFunctions.Parameter<int>(0))
            .Prepare();
        _cmdSimulate = _db.From<LargeEntity>()
            .Where(it => it.Id < LargeListSize)
            .Select(entity => new LargeEntity { Id = entity.Id, Str = entity.Str, Dt = entity.Dt })
            .Prepare();
        _cmdSimulateInner = _db.From<SimpleEntity>()
            .Where(it => it.Id == SqlFunctions.Parameter<int>(0) + SqlFunctions.Parameter<int>(1))
            .FirstOrFirstOrDefaultCommand()
            .Prepare();
        _cmdAny = _db.From<SimpleEntity>()
            .Where(e => e.Id == SqlFunctions.Parameter<int>(0))
            .AnyCommand()
            .Prepare();
        _cmdFirstScalar = _db.From<SimpleEntity>()
            .Where(it => it.Id == SqlFunctions.Parameter<int>(0))
            .FirstOrFirstOrDefaultCommand(it => it.Id)
            .Prepare();
        _cmdSingle = _db.From<SimpleEntity>()
            .Where(it => it.Id == 1)
            .SingleOrSingleOrDefaultCommand(it => it.Id)
            .Prepare();

        // Warm every plan/materialiser and record the observable result sizes for both processes.
        var rows = new List<string>
        {
            "datafetch=" + _cmdDataFetch.ToListAsync(_db).GetAwaiter().GetResult().Count,
            "wide=" + _cmdWide.ToListAsync(_db).GetAwaiter().GetResult().Count,
            "where0=" + _cmdWhere.ToListAsync(_db, 1).GetAwaiter().GetResult().Count,
            "simulate=" + _cmdSimulate.ToListAsync(_db).GetAwaiter().GetResult().Count,
            "any1=" + _db.AnyAsync(_cmdAny, 1).GetAwaiter().GetResult(),
            "first=" + _cmdFirstScalar.FirstOrDefaultAsync(_db, 1).GetAwaiter().GetResult(),
            "single=" + _cmdSingle.SingleOrDefaultAsync(_db, 5).GetAwaiter().GetResult(),
        };

        var diagnosticPath = Path.Combine(
            Environment.GetEnvironmentVariable("NEXTORM_AOT_README_ARTIFACTS")
                ?? Path.Combine(Path.GetTempPath(), "nextorm-aot-readme", "artifacts"),
            "setup-diagnostics.log");
        Directory.CreateDirectory(Path.GetDirectoryName(diagnosticPath)!);
        File.AppendAllText(
            diagnosticPath,
            $"{DateTime.UtcNow:O} pid={Environment.ProcessId} " +
            $"dynamicCode={System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported} " +
            $"db={ReadmeDb.FilePath} " +
            $"interpretation={Environment.GetEnvironmentVariable("NEXTORM_EXPRESSION_INTERPRETATION") ?? "<unset>"} " +
            $"generatedMaterializers={Environment.GetEnvironmentVariable("NEXTORM_GENERATED_MATERIALIZERS") ?? "<unset>"} " +
            string.Join(' ', rows) + Environment.NewLine);
    }

    [Benchmark]
    public async Task Nextorm_Prepared_ToListAsync_SimpleEntity()
    {
        foreach (var row in await _cmdDataFetch.ToListAsync(_db))
        {
        }
    }

    [Benchmark]
    public async Task Nextorm_Prepared_ToListAsync_LargeEntity()
    {
        foreach (var row in await _cmdWide.ToListAsync(_db))
        {
        }
    }

    [Benchmark]
    public async Task Nextorm_Prepared_Where()
    {
        for (var i = 0; i < WhereIterations; i++)
        {
            foreach (var row in await _cmdWhere.ToListAsync(_db, i))
            {
            }
        }
    }

    [Benchmark]
    public async Task Nextorm_Prepared_SimulateWork()
    {
        foreach (var row in await _cmdSimulate.ToListAsync(_db))
        {
            for (var i = 0; i < SmallIterations; i++)
            {
                await _cmdSimulateInner.FirstOrDefaultAsync(_db, row.Id, i);
            }
        }
    }

    [Benchmark]
    public async Task Nextorm_Prepared_Any()
    {
        for (var i = 0; i < AnyIterations; i++)
            await _db.AnyAsync(_cmdAny, i);
    }

    [Benchmark]
    public async Task Nextorm_Prepared_FirstOrDefault_Scalar()
    {
        for (var i = 0; i < 10; i++)
            await _cmdFirstScalar.FirstOrDefaultAsync(_db, i);
    }

    [Benchmark]
    public async Task Nextorm_Prepared_SingleOrDefault()
    {
        for (var i = 0; i < 10; i++)
            await _cmdSingle.SingleOrDefaultAsync(_db, i);
    }

    [GlobalCleanup]
    public void Cleanup() => ((IDisposable)_db).Dispose();
}

[Table("simple_entity")]
public sealed class SimpleEntity
{
    public int Id { get; set; }
}

[Table("large_table")]
public sealed class LargeEntity
{
    public long Id { get; set; }

    [Column("someString")]
    public string? Str { get; set; }

    public DateTime? Dt { get; set; }
}


/// <summary>
/// The entity-returning prepared first arm, isolated in its own class: under NativeAOT this shape
/// builds per-column <c>Expression&lt;Func&lt;LargeEntity, T&gt;&gt;</c> nodes whose metadata can be
/// trimmed away, which fails GlobalSetup. Keeping it separate means such a failure is reported for this
/// arm only and cannot invalidate the other scenarios.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByJob)]
public class ReadmeEntityFirstBenchmark
{
    private IDataContext _db = null!;
    private IPreparedQueryCommand<LargeEntity?> _cmd = null!;

    [GlobalSetup]
    public void Setup()
    {
        var builder = new DataContextBuilder();
        builder.UseSqlite(ReadmeDb.FilePath);
        _db = builder.CreateDataContext();
        ((IConnectionManager)_db).EnsureConnectionOpen();
        _cmd = _db.From<LargeEntity>()
            .Where(it => it.Id == SqlFunctions.Parameter<int>(0))
            .FirstOrFirstOrDefaultCommand()
            .Prepare();
        _ = _cmd.FirstOrDefaultAsync(_db, 1).GetAwaiter().GetResult();
    }

    [Benchmark]
    public async Task Nextorm_Prepared_FirstOrDefault_Entity()
    {
        for (var i = 0; i < 10; i++)
            await _cmd.FirstOrDefaultAsync(_db, i);
    }

    [GlobalCleanup]
    public void Cleanup() => ((IDisposable)_db).Dispose();
}


/// <summary>
/// README Category A — one prepared NextORM arm per documented feature category, mirroring
/// <c>SqliteBenchmarkFeaturesFair</c>: the same <c>Prepare()</c> chains, the same ten-iteration loop
/// (the published table divides by ten) and the same sink counter. Two categories have two arms each
/// (string functions: ToUpper/Contains; window: RowNumber/SumOver); both are measured and reported, so
/// no variant is silently swapped between jobs.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByJob)]
public class ReadmeCategoryABenchmark
{
    private const int Iterations = 10;
    private static readonly long[] InValues = { 1, 3, 10 };

    private IDataContext _db = null!;
    private IPreparedQueryCommand<int> _case = null!;
    private IPreparedQueryCommand<CteJoinRow> _cte = null!;
    private IPreparedQueryCommand<int?> _distinct = null!;
    private IPreparedQueryCommand<int> _except = null!;
    private IPreparedQueryCommand<long> _inList = null!;
    private IPreparedQueryCommand<int> _intersect = null!;
    private IPreparedQueryCommand<FourJoinRow> _join4 = null!;
    private IPreparedQueryCommand<LeftJoinRow> _leftJoin = null!;
    private IPreparedQueryCommand<int> _recursiveCte = null!;
    private IPreparedQueryCommand<string> _toUpper = null!;
    private IPreparedQueryCommand<long> _contains = null!;
    private IPreparedQueryCommand<string> _udf = null!;
    private IPreparedQueryCommand<RowNumberRow> _rowNumber = null!;
    private IPreparedQueryCommand<SumOverRow> _sumOver = null!;
    private long _sink;

    [GlobalSetup]
    public void Setup()
    {
        var builder = new DataContextBuilder();
        builder.UseSqlite(ReadmeDb.FilePath);
        _db = builder.CreateDataContext();
        ((IConnectionManager)_db).EnsureConnectionOpen();
        var simple = _db.From<SimpleEntity>();
        var complex = _db.From<ComplexEntity>();

        _case = complex.Select(c => c.Id > 1 ? 10 : 20).Prepare();
        var recent = complex.Where(c => c.Id > 1).Select(c => new { c.Id, c.RequiredString });
        _cte = _db.With("recent", recent)
            .From("recent")
            .Join(simple, (r, s) => r["id"].AsInt == s.Id)
            .Select(p => new CteJoinRow { Id = p.Item1["id"].AsInt, SimpleId = p.Item2.Id })
            .Prepare();
        _distinct = complex.Select(c => c.Int).Distinct().Prepare();
        _except = simple.Select(s => s.Id).Except(complex.Select(c => (int)c.Id)).Prepare();
        _inList = complex.Where(c => SqlFunctions.Sql.@in(c.Id, InValues)).Select(c => c.Id).Prepare();
        _intersect = simple.Select(s => s.Id).Intersect(complex.Select(c => (int)c.Id)).Prepare();
        _join4 = simple
            .Join(complex, (s, c) => (long)s.Id == c.Id)
            .Join(simple, (p, s) => p.Item2.Id == (long)s.Id)
            .Join(complex, (p, c) => (long)p.Item3.Id == c.Id)
            .Select(p => new FourJoinRow { A = p.Item1.Id, B = p.Item2.RequiredString, C = p.Item3.Id, D = p.Item4.RequiredString })
            .Prepare();
        _leftJoin = simple
            .LeftJoin(complex, (s, c) => (long)s.Id == c.Id)
            .Select(p => new LeftJoinRow { Id = p.Item1.Id, RightString = p.Item2.RequiredString })
            .Prepare();
        var anchor = simple.Where(s => s.Id == 1).Select(s => new CteNumberRow { n = s.Id });
        var step = _db.From("nums").Where(t => t["n"].AsInt < 5).Select(t => new CteNumberRow { n = t["n"].AsInt + 1 });
        _recursiveCte = _db.WithRecursive("nums", anchor.UnionAll(step)).From("nums").Select(t => t["n"].AsInt).Prepare();
        _toUpper = complex.Where(c => c.Id == 2).Select(c => c.String!.ToUpper()).Prepare();
        _contains = complex.Where(c => c.String!.Contains("df")).Select(c => c.Id).Prepare();
        _udf = complex.Where(c => c.Id == 2).Select(c => Udf.ToUpper(c.String!)).Prepare();
        _rowNumber = complex.Select(c => new RowNumberRow
        {
            Id = c.Id,
            Rn = SqlFunctions.Sql.row_number().Over(partitionBy: () => c.Int, orderBy: () => c.Id)
        }).Prepare();
        _sumOver = complex.Select(c => new SumOverRow
        {
            Id = c.Id,
            Total = SqlFunctions.Sql.sum_over(c.Id).Over(partitionBy: () => c.Int)
        }).Prepare();

        // Warm every plan and record one observable result per arm for both processes.
        var probe = new List<string>
        {
            "case=" + _case.ToListAsync(_db).GetAwaiter().GetResult().Count,
            "cte=" + _cte.ToListAsync(_db).GetAwaiter().GetResult().Count,
            "distinct=" + _distinct.ToListAsync(_db).GetAwaiter().GetResult().Count,
            "except=" + _except.ToListAsync(_db).GetAwaiter().GetResult().Count,
            "in=" + _inList.ToListAsync(_db).GetAwaiter().GetResult().Count,
            "intersect=" + _intersect.ToListAsync(_db).GetAwaiter().GetResult().Count,
            "join4=" + _join4.ToListAsync(_db).GetAwaiter().GetResult().Count,
            "leftjoin=" + _leftJoin.ToListAsync(_db).GetAwaiter().GetResult().Count,
            "rcte=" + _recursiveCte.ToListAsync(_db).GetAwaiter().GetResult().Count,
            "upper=" + _toUpper.ToListAsync(_db).GetAwaiter().GetResult().Count,
            "contains=" + _contains.ToListAsync(_db).GetAwaiter().GetResult().Count,
            "udf=" + _udf.ToListAsync(_db).GetAwaiter().GetResult().Count,
            "rownum=" + _rowNumber.ToListAsync(_db).GetAwaiter().GetResult().Count,
            "sumover=" + _sumOver.ToListAsync(_db).GetAwaiter().GetResult().Count,
        };
        var diagnosticPath = Path.Combine(
            Environment.GetEnvironmentVariable("NEXTORM_AOT_README_ARTIFACTS")
                ?? Path.Combine(Path.GetTempPath(), "nextorm-aot-readme", "artifacts"),
            "setup-diagnostics.log");
        Directory.CreateDirectory(Path.GetDirectoryName(diagnosticPath)!);
        File.AppendAllText(
            diagnosticPath,
            $"{DateTime.UtcNow:O} pid={Environment.ProcessId} categoryA " +
            $"dynamicCode={System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported} " +
            $"db={ReadmeDb.FilePath} " +
            $"interpretation={Environment.GetEnvironmentVariable("NEXTORM_EXPRESSION_INTERPRETATION") ?? "<unset>"} " +
            $"generatedMaterializers={Environment.GetEnvironmentVariable("NEXTORM_GENERATED_MATERIALIZERS") ?? "<unset>"} " +
            string.Join(' ', probe) + Environment.NewLine);
    }

    [Benchmark] public async Task A_Nextorm_Prepared_CaseWhen()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _case.ToListAsync(_db)) _sink++;
    }

    [Benchmark] public async Task A_Nextorm_Prepared_Cte()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _cte.ToListAsync(_db)) _sink++;
    }

    [Benchmark] public async Task A_Nextorm_Prepared_Distinct()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _distinct.ToListAsync(_db)) _sink++;
    }

    [Benchmark] public async Task A_Nextorm_Prepared_Except()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _except.ToListAsync(_db)) _sink++;
    }

    [Benchmark] public async Task A_Nextorm_Prepared_In()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _inList.ToListAsync(_db)) _sink++;
    }

    [Benchmark] public async Task A_Nextorm_Prepared_Intersect()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _intersect.ToListAsync(_db)) _sink++;
    }

    [Benchmark] public async Task A_Nextorm_Prepared_Join4()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _join4.ToListAsync(_db)) _sink++;
    }

    [Benchmark] public async Task A_Nextorm_Prepared_LeftJoin()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _leftJoin.ToListAsync(_db)) _sink++;
    }

    [Benchmark] public async Task A_Nextorm_Prepared_RecursiveCte()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _recursiveCte.ToListAsync(_db)) _sink++;
    }

    [Benchmark] public async Task A_Nextorm_Prepared_ToUpper()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _toUpper.ToListAsync(_db)) _sink++;
    }

    [Benchmark] public async Task A_Nextorm_Prepared_Contains()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _contains.ToListAsync(_db)) _sink++;
    }

    [Benchmark] public async Task A_Nextorm_Prepared_Udf()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _udf.ToListAsync(_db)) _sink++;
    }

    [Benchmark] public async Task A_Nextorm_Prepared_RowNumber()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _rowNumber.ToListAsync(_db)) _sink++;
    }

    [Benchmark] public async Task A_Nextorm_Prepared_SumOver()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _sumOver.ToListAsync(_db)) _sink++;
    }

    [GlobalCleanup]
    public void Cleanup() => ((IDisposable)_db).Dispose();

    private static class Udf
    {
        [SqlFunction("upper")]
        public static string ToUpper(string value) => throw new NotSupportedException();
    }
}

[Table("complex_entity")]
public sealed class ComplexEntity
{
    public long Id { get; set; }

    [Column("nullableInt")] public int? Int { get; set; }
    [Column("someString")] public string? String { get; set; }
    [Column("d")] public double? Double { get; set; }
    [Column("dt")] public DateTime? Datetime { get; set; }
    [Column("b")] public bool? Boolean { get; set; }
    [Column("requiredString")] public string RequiredString { get; set; } = null!;
}

public sealed class LeftJoinRow
{
    public int Id { get; set; }
    public string? RightString { get; set; }
}

public sealed class FourJoinRow
{
    public int A { get; set; }
    public string? B { get; set; }
    public int C { get; set; }
    public string? D { get; set; }
}

public sealed class RowNumberRow
{
    public long Id { get; set; }
    public long Rn { get; set; }
}

public sealed class SumOverRow
{
    public long Id { get; set; }
    public long? Total { get; set; }
}

public sealed class CteJoinRow
{
    public int Id { get; set; }
    public int SimpleId { get; set; }
}

public sealed class CteNumberRow
{
    public int n { get; set; }
}
