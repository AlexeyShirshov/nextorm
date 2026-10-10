using BenchmarkDotNet.Attributes;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Benchmark;

/// <summary>
/// T208 (#208) contract rv2 changed-path acceptance case (benchmark-only evidence; the product fix
/// itself is unchanged). It measures exactly the code path the fix touched: a SELECT-list
/// <see cref="SqlFunctions.Parameter{T}"/> prepared UNCACHED (<c>storeInCache: false</c>, the
/// <c>SqliteBenchmarkCachedPlan.Build_Sql</c> pattern), so the per-projection visitor runs on every
/// measured operation and reaches the <c>SqlFunctions.Parameter</c> arm in
/// <c>src/nextorm.core/Visitors/NormSqlTranslator.cs:77-86</c> (which sets <c>NeedAliasForColumn</c>).
/// </summary>
[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
public class T208ChangedPathBenchmark
{
    private const int Iterations = 100;

    private readonly IDataContext _db;
    private readonly TestDataRepository _repo;

    public T208ChangedPathBenchmark()
    {
        var builder = new DataContextBuilder();
        builder.UseSqlite(BenchDb.FilePath);
        _db = builder.CreateDataContext();
        _repo = new TestDataRepository(_db);
        ((IConnectionManager)_db).EnsureConnectionOpen();

        // Build and prepare the projected-parameter query once, unconditionally, so construction fails
        // loudly on a broken harness -- but never on the pre-fix baseline's missing alias. This case
        // must run on BOTH the pre-fix baseline (which renders `select $norm_p0, id ...`) and the
        // candidate, so it must not assert alias presence. Candidate reachability of the
        // projected-parameter alias arm is kept separate at
        // artifacts/pdca/D208/rv1/n1-changed-path-reachability.txt.
        _ = ((DbPreparedQueryCommand<T208ChangedPathRow>)_db.GetPreparedQueryCommand(
            NewQuery(), false, false, CancellationToken.None)).DbCommand.CommandText;
    }

    // Uncached translation/preparation of a SELECT-list SqlFunctions.Parameter<int>: every iteration
    // builds a fresh command and re-renders the SQL (storeInCache: false), exercising the projection
    // visitor and alias resolution rather than the plan cache.
    [Benchmark]
    [BenchmarkCategory("t208-changed-path")]
    public IPreparedQueryCommand<T208ChangedPathRow> Prepare_ProjectedParameter_Uncached()
    {
        IPreparedQueryCommand<T208ChangedPathRow>? r = null;
        for (var i = 0; i < Iterations; i++)
            r = _db.GetPreparedQueryCommand(NewQuery(), false, false, CancellationToken.None);
        return r!;
    }

    private QueryCommand<T208ChangedPathRow> NewQuery()
        => _repo.SimpleEntity
            .Where(it => it.Id == it.Id)
            .Select(it => new T208ChangedPathRow { P = SqlFunctions.Parameter<int>(0), Id = it.Id });
}

public sealed class T208ChangedPathRow
{
    public int P { get; set; }

    public int Id { get; set; }
}
