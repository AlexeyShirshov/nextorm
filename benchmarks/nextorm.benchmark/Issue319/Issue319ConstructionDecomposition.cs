using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Benchmark;

/// <summary>
/// Issue #319 decomposition harness: how much of a fresh cached query is collection and how much
/// is preparation. Every arm builds the exact query shape of the frozen 2026-11-07 comparison
/// arms on a real <see cref="SqliteDataContext"/>; nothing is executed and no database is opened
/// (the context points at an in-memory file and preparation only renders SQL).
/// <para>
/// The <c>*_Construct</c> arms are the acceptance-relevant number: with a warm plan cache the
/// per-call cost is collection plus key lookup plus binding, so construction is the mass the
/// native raw description replaces. The <c>*_ConstructAndPrepare</c> arms add a full miss
/// (translation + SQL build + mapper) and exist only to show the prepared denominator used by the
/// R15 mapping.
/// </para>
/// </summary>
[HideColumns(Column.Job, Column.Runtime, Column.RatioSD, Column.Error, Column.StdDev)]
[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
public class Issue319ConstructionDecomposition
{
    private readonly IDataContext _db;

    public Issue319ConstructionDecomposition()
    {
        var builder = new DataContextBuilder();
        builder.UseSqlite("Data Source=:memory:");
        _db = builder.CreateDataContext();
    }

    [GlobalSetup]
    public void Verify()
    {
        // Metadata warm-up only: no connection, no SQL execution.
        _ = _db.From<SimpleEntity>();
        _ = _db.From<ComplexEntity>();

        if (BuildCte().Ctes?.Count != 1)
            throw new InvalidOperationException("Cte arm must carry one CTE declaration.");
        if (BuildRecursiveCte().Ctes?.Count != 1)
            throw new InvalidOperationException("RecursiveCte arm must carry one CTE declaration.");
        if (BuildExcept().UnionType != UnionType.Except)
            throw new InvalidOperationException("Except arm must carry an EXCEPT set operation.");
        if (BuildIntersect().UnionType != UnionType.Intersect)
            throw new InvalidOperationException("Intersect arm must carry an INTERSECT set operation.");
    }

    // ---- targets (frozen manifest T5, T3, T8, T6) ----------------------------

    [Benchmark]
    [BenchmarkCategory("Issue319", "T5")]
    public QueryCommand<CteJoinRow> Cte_Construct() => BuildCte();

    [Benchmark]
    [BenchmarkCategory("Issue319", "T5")]
    public IPreparedQueryCommand<CteJoinRow> Cte_ConstructAndPrepare() => Prepare(BuildCte());

    [Benchmark]
    [BenchmarkCategory("Issue319", "T3")]
    public QueryCommand<int> RecursiveCte_Construct() => BuildRecursiveCte();

    [Benchmark]
    [BenchmarkCategory("Issue319", "T3")]
    public IPreparedQueryCommand<int> RecursiveCte_ConstructAndPrepare() => Prepare(BuildRecursiveCte());

    [Benchmark]
    [BenchmarkCategory("Issue319", "T8")]
    public QueryCommand<int> Except_Construct() => BuildExcept();

    [Benchmark]
    [BenchmarkCategory("Issue319", "T8")]
    public IPreparedQueryCommand<int> Except_ConstructAndPrepare() => Prepare(BuildExcept());

    [Benchmark]
    [BenchmarkCategory("Issue319", "T6")]
    public QueryCommand<int> Intersect_Construct() => BuildIntersect();

    [Benchmark]
    [BenchmarkCategory("Issue319", "T6")]
    public IPreparedQueryCommand<int> Intersect_ConstructAndPrepare() => Prepare(BuildIntersect());

    // ---- controls: the floor a redesign cannot remove ------------------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Issue319", "Control")]
    public QueryCommand<long> Select_Control() => _db.From<ComplexEntity>().Select(c => c.Id);

    [Benchmark]
    [BenchmarkCategory("Issue319", "Control")]
    public QueryCommand<long> WhereSelect_Control() => _db.From<ComplexEntity>().Where(c => c.Id > 1).Select(c => c.Id);

    [Benchmark]
    [BenchmarkCategory("Issue319", "Control")]
    public QueryCommand<ComplexEntity> Identity_Control() => _db.From<ComplexEntity>().Select(c => c);

    private IPreparedQueryCommand<T> Prepare<T>(QueryCommand<T> command)
        => _db.GetPreparedQueryCommand(command, false, false, CancellationToken.None);

    private QueryCommand<CteJoinRow> BuildCte()
    {
        var recent = _db.From<ComplexEntity>().Where(c => c.Id > 1).Select(c => new { c.Id, c.RequiredString });
        return _db.With("recent", recent)
            .From("recent")
            .Join(_db.From<SimpleEntity>(), (r, s) => r["id"].AsInt == s.Id)
            .Select(p => new CteJoinRow { Id = p.Item1["id"].AsInt, SimpleId = p.Item2.Id });
    }

    private QueryCommand<int> BuildRecursiveCte()
    {
        var anchor = _db.From<SimpleEntity>().Where(s => s.Id == 1).Select(s => new CteNumberRow { n = s.Id });
        var step = _db.From("nums").Where(t => t["n"].AsInt < 5).Select(t => new CteNumberRow { n = t["n"].AsInt + 1 });
        return _db.WithRecursive("nums", anchor.UnionAll(step)).From("nums").Select(t => t["n"].AsInt);
    }

    private QueryCommand<int> BuildExcept()
        => _db.From<SimpleEntity>().Select(s => s.Id).Except(_db.From<ComplexEntity>().Select(c => (int)c.Id));

    private QueryCommand<int> BuildIntersect()
        => _db.From<SimpleEntity>().Select(s => s.Id).Intersect(_db.From<ComplexEntity>().Select(c => (int)c.Id));
}
