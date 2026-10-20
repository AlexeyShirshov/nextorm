using BenchmarkDotNet.Attributes;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Benchmark;

/// <summary>
/// D297 R05 real-provider-path construction arm: it builds the same Cte / RecursiveCte command
/// shapes as the spike graphs, from scratch, on a real <c>SqliteDataContext</c>, and measures the
/// construction allocations only. Nothing is executed, prepared or sent to the database.
/// <para>
/// This validates the <b>baseline</b> of the warm path. The candidate cannot be measured here
/// without the §13 product change in <c>src/**</c>, so the arm is baseline-validated only.
/// </para>
/// </summary>
[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
public class SqliteBenchmarkConstructAllocation
{
    private readonly IDataContext _db;

    public SqliteBenchmarkConstructAllocation()
    {
        var builder = new DataContextBuilder();
        builder.UseSqlite(BenchDb.FilePath);
        _db = builder.CreateDataContext();
    }

    [GlobalSetup]
    public void Verify()
    {
        // Metadata warm-up only; no SQL is executed by this class.
        _ = _db.From<SimpleEntity>();
        _ = _db.From("nums");

        var cte = BuildCte();
        if (cte.Ctes?.Count != 2)
            throw new InvalidOperationException($"Cte construction arm must build 2 CTE definitions, got {cte.Ctes?.Count ?? -1}.");

        var recursive = BuildRecursiveCte();
        if (recursive.Ctes?.Count != 3)
            throw new InvalidOperationException($"RecursiveCte construction arm must build 3 CTE definitions, got {recursive.Ctes?.Count ?? -1}.");

        Console.WriteLine($"SqliteBenchmarkConstructAllocation verification passed: Cte.Ctes.Count={cte.Ctes!.Count}, RecursiveCte.Ctes.Count={recursive.Ctes!.Count}");
    }

    [Benchmark]
    [BenchmarkCategory("B_Construct")]
    public QueryCommand<ConstructRow> B_Nextorm_Construct_Cte() => BuildCte();

    [Benchmark]
    [BenchmarkCategory("B_Construct")]
    public QueryCommand<ConstructRow> B_Nextorm_Construct_RecursiveCte() => BuildRecursiveCte();

    private QueryCommand<ConstructRow> BuildCte()
    {
        var body = _db.From<SimpleEntity>().Where(x => x.Id > 0).Select(x => new ConstructRow { Id = x.Id });
        var second = _db.From<SimpleEntity>().Where(x => x.Id > 1).Select(x => new ConstructRow { Id = x.Id });
        return _db.With("recent", body)
            .With("older", second)
            .From("older")
            .Select(t => new ConstructRow { Id = t["id"].AsInt });
    }

    private QueryCommand<ConstructRow> BuildRecursiveCte()
    {
        var anchor = _db.From<SimpleEntity>().Where(x => x.Id == 1).Select(x => new ConstructRow { Id = x.Id });
        var step = _db.From("nums").Where(t => t["n"].AsInt < 5).Select(t => new ConstructRow { Id = t["n"].AsInt + 1 });
        var body = anchor.UnionAll(step);
        var clone = (QueryCommand<ConstructRow>)body.Clone();
        return _db.With("seed", anchor)
            .With("other", anchor)
            .WithRecursive("nums", clone)
            .From("nums")
            .Select(t => new ConstructRow { Id = t["id"].AsInt });
    }
}

public sealed class ConstructRow
{
    public int Id { get; set; }
}
