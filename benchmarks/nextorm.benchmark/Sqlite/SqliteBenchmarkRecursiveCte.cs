using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Benchmark;

/// <summary>
/// Focused, non-acceptance benchmark for the typed recursive CTE (#146-B). Both arms read the same
/// bounded SQLite recursive query (<c>anchor = id 1; step = n + 1 while n &lt; 5</c>, consumer
/// <c>Limit(20)</c>) and differ only in how the consumer command is obtained:
/// <list type="bullet">
/// <item><b>reused/prepared</b> — the consumer is prepared once and the prepared command is executed
///   in the loop (no query re-composition, no plan lookup);</item>
/// <item><b>recomposed cache-hit</b> — a fresh consumer command is composed on every iteration over the
///   same <see cref="Cte{TResult}"/> descriptor, whose reference identity keeps the plan key stable, so
///   the preparation hits the plan cache (composition + lookup + execution).</item>
/// </list>
/// The step predicate (<c>n &lt; 5</c>) and the consumer <c>Limit(20)</c> keep the recursion bounded;
/// <see cref="Setup"/> fails the run if the rendered SQL loses the bound or the <c>UNION ALL</c>, or if
/// the two consumers disagree or leave the bounded window. This benchmark never runs with the acceptance
/// category, so it cannot affect the acceptance perf gate.
/// </summary>
[HideColumns(Column.Job, Column.Runtime, Column.RatioSD)]
[MemoryDiagnoser]
[BenchmarkCategory("recursive-cte")]
[Config(typeof(NextormConfig))]
public class SqliteBenchmarkRecursiveCte
{
    private const int Iterations = 100;

    private readonly IDataContext _db;
    private readonly Cte<int> _nums;
    private readonly IPreparedQueryCommand<int> _prepared;

    public SqliteBenchmarkRecursiveCte()
    {
        var filepath = BenchDb.FilePath;
        var builder = new DataContextBuilder();
        builder.UseSqlite(filepath);
        _db = builder.CreateDataContext();
        ((IConnectionManager)_db).EnsureConnectionOpen();

        _nums = _db.From<SimpleEntity>()
            .Where(it => it.Id == 1)
            .Select(it => it.Id)
            .AsRecursiveCte("bench_recursive_nums", self => _db.From(self).Where(n => n < 5).Select(n => n + 1));

        _prepared = _db.From(_nums).Limit(20).Select(n => n).Prepare();

        // Warm the recomposed consumer's plan cache so the measured arm only pays cache-hit overhead.
        _ = _db.From(_nums).Limit(20).Select(n => n).ToList();
    }

    [GlobalSetup]
    public void Setup()
    {
        var sql = SqlOf(_db, _db.From(_nums).Limit(20).Select(n => n));
        if (!sql.Contains("with recursive", StringComparison.OrdinalIgnoreCase)
            || !sql.Contains("union all", StringComparison.OrdinalIgnoreCase)
            || !sql.Contains("Id < 5", StringComparison.Ordinal))
            throw new InvalidOperationException($"Bounded recursive SQL contract violated: {sql}");

        var reused = _prepared.ToList(_db);
        var recomposed = _db.From(_nums).Limit(20).Select(n => n).ToList();

        if (!reused.SequenceEqual(recomposed))
            throw new InvalidOperationException(
                $"Reused and recomposed recursive consumers disagree: reused=[{string.Join(",", reused)}] recomposed=[{string.Join(",", recomposed)}]");

        // Bounded: the step stops at n < 5, so at most five rows may come back, all inside 1..5.
        if (reused.Count > 5 || reused.Any(n => n < 1 || n > 5))
            throw new InvalidOperationException(
                $"Recursive result left the bounded window: [{string.Join(",", reused)}]");
    }

    // (a) Reused prepared consumer: the command is prepared once; no composition, no plan lookup.
    [Benchmark(Baseline = true)]
    public int Recursive_Reused_Prepared_ToList()
    {
        var sum = 0;
        for (var i = 0; i < Iterations; i++)
        {
            foreach (var n in _prepared.ToList(_db))
                sum += n;
        }

        return sum;
    }

    // (b) Recomposed cache-hit consumer: a fresh consumer command per iteration over the same CTE
    // descriptor, so the preparation is a plan-cache hit (composition + lookup + execution).
    [Benchmark]
    public int Recursive_Recomposed_Cached_ToList()
    {
        var sum = 0;
        for (var i = 0; i < Iterations; i++)
        {
            foreach (var n in _db.From(_nums).Limit(20).Select(n => n).ToList())
                sum += n;
        }

        return sum;
    }

    private static string SqlOf<T>(IDataContext context, QueryCommand<T> command)
        => ((DbPreparedQueryCommand<T>)context.GetPreparedQueryCommand(command, false, false, CancellationToken.None))
            .DbCommand.CommandText.Replace("\r\n", "\n");
}
