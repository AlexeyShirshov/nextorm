using System.Data;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Running;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Benchmark.Aot;

/// <summary>
/// Measures one question: what does a <em>prepared</em> query cost when its row materialiser runs
/// interpreted (NativeAOT, no JIT) against the same prepared query when the materialiser runs
/// compiled (JIT)? The command is prepared once in <see cref="AotMaterializerBenchmarks.Setup"/>,
/// so the timed body is execution + materialisation only — no command construction, no preparation,
/// no plan-key hashing. Both regimes run the same binary; only the job and the
/// <c>NEXTORM_EXPRESSION_INTERPRETATION</c> switch differ.
/// </summary>
public static class Program
{
    public static void Main(string[] args)
    {
        var config = new AotComparisonConfig();
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
    }
}

/// <summary>
/// Three out-of-process jobs, all on .NET 10 (BenchmarkDotNet 0.15.8 exposes NativeAOT only up to
/// <c>Net10_0</c>, and the packages ship a <c>net10.0</c> asset): JIT with compiled trees, JIT with
/// interpreted trees, and NativeAOT with interpreted trees. The middle arm separates the cost of the
/// interpreter itself from the cost of missing JIT codegen. The in-process toolchains used elsewhere
/// in this repository cannot build AOT, so every arm is out-of-process.
/// </summary>
public sealed class AotComparisonConfig : ManualConfig
{
    public AotComparisonConfig()
    {
        ArtifactsPath = Path.Combine(AppContext.BaseDirectory, "artifacts");
        // The NativeAOT arm runs ILC over nextorm.core and takes well over BenchmarkDotNet's
        // five-minute default on a developer host; the JIT arms finish in seconds. The build is
        // forced sequential because the two generated executables publish the same project
        // references and collide in the shared intermediate output directory.
        WithBuildTimeout(TimeSpan.FromMinutes(30));
        WithOptions(ConfigOptions.DisableParallelBuild);
        Orderer = new DefaultOrderer(SummaryOrderPolicy.FastestToSlowest);
        AddLogger(ConsoleLogger.Default);
        AddColumnProvider(DefaultColumnProviders.Instance);

        AddJob(Job.Default.WithWarmupCount(5).WithIterationCount(10)
            .WithRuntime(CoreRuntime.Core10_0)
            .WithEnvironmentVariable("NEXTORM_EXPRESSION_INTERPRETATION", "0")
            .WithId("JIT-compiled"));

        AddJob(Job.Default.WithWarmupCount(5).WithIterationCount(10)
            .WithRuntime(CoreRuntime.Core10_0)
            .WithEnvironmentVariable("NEXTORM_EXPRESSION_INTERPRETATION", "1")
            .WithId("JIT-interpreted"));

        // Exactly what a NativeAOT application gets: the switch unset, so the materialiser calls
        // Compile(), and NativeAOT answers with the interpreter because it has no dynamic codegen.
        AddJob(Job.Default.WithWarmupCount(5).WithIterationCount(10)
            .WithRuntime(NativeAotRuntime.Net10_0)
            .WithId("AOT-default"));

        // The same binary with compile-time materialisers registered instead of expression trees:
        // measures what source generation would recover under NativeAOT.
        AddJob(Job.Default.WithWarmupCount(5).WithIterationCount(10)
            .WithRuntime(NativeAotRuntime.Net10_0)
            .WithEnvironmentVariable("NEXTORM_GENERATED_MATERIALIZERS", "1")
            .WithId("AOT-generated"));
    }
}

[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class AotMaterializerBenchmarks
{
    private const int RowCount = 500;
    private string _databasePath = string.Empty;
    private SqliteDataContext _context = null!;
    private IPreparedQueryCommand<BenchRowDto> _preparedCtor = null!;
    private IPreparedQueryCommand<BenchRowFlat> _preparedMemberInit = null!;
    private IPreparedQueryCommand<BenchRowWide> _preparedWide = null!;
    private SqliteConnection _rawConnection = null!;
    private SqliteCommand _rawCommand = null!;
    private SqliteCommand _rawCommand12 = null!;

    [GlobalSetup]
    public void Setup()
    {
        // The generator registers its provider from the module initializer. The baseline arm removes
        // it so the same binary measures the built-in expression path.
        if (Environment.GetEnvironmentVariable("NEXTORM_GENERATED_MATERIALIZERS") is not ("1" or "true"))
        {
            RowMaterializerRegistry.Reset();
        }

        _databasePath = Path.Combine(Path.GetTempPath(), $"nextorm-aot-bench-{Guid.NewGuid():N}.db");
        using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            // Twelve numeric columns: three for the narrow shapes, twelve for the wide one that
            // exposes how the per-row cost scales with the projection width.
            command.CommandText =
                "CREATE TABLE bench_rows (id INTEGER PRIMARY KEY, name TEXT NOT NULL, amount INTEGER NOT NULL, " +
                "c4 INTEGER NOT NULL, c5 INTEGER NOT NULL, c6 INTEGER NOT NULL, c7 INTEGER NOT NULL, " +
                "c8 INTEGER NOT NULL, c9 INTEGER NOT NULL, c10 INTEGER NOT NULL, c11 INTEGER NOT NULL, c12 INTEGER NOT NULL)";
            command.ExecuteNonQuery();
            command.CommandText =
                "INSERT INTO bench_rows (id, name, amount, c4, c5, c6, c7, c8, c9, c10, c11, c12) " +
                "VALUES ($id, $name, $amount, $c4, $c5, $c6, $c7, $c8, $c9, $c10, $c11, $c12)";
            var id = command.CreateParameter();
            id.ParameterName = "$id";
            var name = command.CreateParameter();
            name.ParameterName = "$name";
            var amount = command.CreateParameter();
            amount.ParameterName = "$amount";
            command.Parameters.Add(id);
            command.Parameters.Add(name);
            command.Parameters.Add(amount);
            var extras = new SqliteParameter[9];
            for (var c = 0; c < extras.Length; c++)
            {
                extras[c] = command.CreateParameter();
                extras[c].ParameterName = "$c" + (c + 4);
                command.Parameters.Add(extras[c]);
            }

            for (var i = 1; i <= RowCount; i++)
            {
                id.Value = i;
                name.Value = "row-" + i;
                amount.Value = i * 3;
                for (var c = 0; c < extras.Length; c++)
                {
                    extras[c].Value = i * (c + 4);
                }

                command.ExecuteNonQuery();
            }
        }

        _context = new SqliteDataContext($"Data Source='{_databasePath}';Default Timeout=30", new DataContextBuilder());

        // Prepared once: the timed body must not pay command construction, preparation or hashing.
        _preparedCtor = _context.From<BenchRow>()
            .Where(row => row.Id > 0)
            .Select(row => new BenchRowDto(row.Id, row.Name, row.Amount))
            .Prepare();

        _preparedMemberInit = _context.From<BenchRow>()
            .Where(row => row.Id > 0)
            .Select(row => new BenchRowFlat { Id = row.Id, Name = row.Name, Amount = row.Amount })
            .Prepare();

        _preparedWide = _context.From<BenchRowWide>()
            .Where(row => row.Id > 0)
            .Select(row => new BenchRowWide
            {
                Id = row.Id,
                Name = row.Name,
                Amount = row.Amount,
                C4 = row.C4,
                C5 = row.C5,
                C6 = row.C6,
                C7 = row.C7,
                C8 = row.C8,
                C9 = row.C9,
                C10 = row.C10,
                C11 = row.C11,
                C12 = row.C12,
            })
            .Prepare();

        // The floor: the same three columns read by hand off a prepared ADO.NET command. Whatever
        // separates this from PreparedCtorToList is what nextorm's materialiser costs on top of the
        // provider round trip and the reader itself.
        _rawConnection = new SqliteConnection($"Data Source={_databasePath};Default Timeout=30");
        _rawConnection.Open();
        _rawCommand = _rawConnection.CreateCommand();
        _rawCommand.CommandText = "SELECT id, name, amount FROM bench_rows";
        _rawCommand12 = _rawConnection.CreateCommand();
        _rawCommand12.CommandText =
            "SELECT id, name, amount, c4, c5, c6, c7, c8, c9, c10, c11, c12 FROM bench_rows";

        // Warm the plan cache and build the materialiser delegate before any timed iteration.
        _ = _preparedCtor.ToList(_context);
        _ = _preparedMemberInit.ToList(_context);
        _ = _preparedWide.ToList(_context);
        _ = RawAdo3Col();
        _ = RawAdo12Col();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _rawCommand.Dispose();
        _rawCommand12.Dispose();
        _rawConnection.Dispose();
        _context.Dispose();
        try
        {
            File.Delete(_databasePath);
        }
        catch (IOException)
        {
            // best effort: the SQLite pool may still hold the file on some platforms
        }
    }

    /// <summary>Constructor-bound DTO: the materialiser resolves the ctor and binds positional arguments.</summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("ctor")]
    public int PreparedCtorToList() => _preparedCtor.ToList(_context).Count;

    /// <summary>Parameterless DTO with settable members: the member-init materialiser path.</summary>
    [Benchmark]
    [BenchmarkCategory("member-init")]
    public int PreparedMemberInitToList() => _preparedMemberInit.ToList(_context).Count;

    /// <summary>Twelve-column DTO: the same member-init path, four times as wide.</summary>
    [Benchmark]
    [BenchmarkCategory("wide")]
    public int PreparedWideToList() => _preparedWide.ToList(_context).Count;

    /// <summary>Hand-written reader over the same rows: the floor the materialiser sits on.</summary>
    [Benchmark]
    [BenchmarkCategory("raw")]
    public int RawAdo3Col()
    {
        var sum = 0;
        using var reader = _rawCommand.ExecuteReader();
        while (reader.Read())
        {
            sum += reader.GetInt32(0);
            sum += reader.GetString(1).Length;
            sum += reader.GetInt32(2);
        }

        return sum;
    }

    /// <summary>Hand-written reader over all twelve columns: the floor for the wide shape.</summary>
    [Benchmark]
    [BenchmarkCategory("raw")]
    public long RawAdo12Col()
    {
        long sum = 0;
        using var reader = _rawCommand12.ExecuteReader();
        while (reader.Read())
        {
            sum += reader.GetInt32(0);
            sum += reader.GetString(1).Length;
            for (var column = 2; column < 12; column++)
            {
                sum += reader.GetInt32(column);
            }
        }

        return sum;
    }
}

[SqlTable("bench_rows")]
public sealed class BenchRow
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public int Amount { get; set; }
}

[RowMaterializer]
public sealed class BenchRowDto
{
    public BenchRowDto(int id, string? name, int amount)
    {
        Id = id;
        Name = name;
        Amount = amount;
    }

    public int Id { get; }

    public string? Name { get; }

    public int Amount { get; }
}

[RowMaterializer]
public sealed class BenchRowFlat
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public int Amount { get; set; }
}

[SqlTable("bench_rows")]
public sealed class BenchRowWide
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public int Amount { get; set; }

    public int C4 { get; set; }

    public int C5 { get; set; }

    public int C6 { get; set; }

    public int C7 { get; set; }

    public int C8 { get; set; }

    public int C9 { get; set; }

    public int C10 { get; set; }

    public int C11 { get; set; }

    public int C12 { get; set; }
}
