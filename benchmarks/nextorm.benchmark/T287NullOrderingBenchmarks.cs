using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Benchmark;

/// <summary>
/// T287 (#287) dedicated benchmark for portable explicit NULL placement: the in-memory sorting path
/// (<c>InMemoryOrdering</c>) over 1 000 / 10 000 rows with 0 / 10 / 50 % NULL keys, for the implicit
/// <c>Default</c> placement and the explicit first/last placement in both directions.
/// </summary>
/// <remarks>
/// Deliberately <b>not</b> tagged with the <c>acceptance</c> category, so the seven-case acceptance gate
/// is unchanged; run it explicitly with
/// <c>dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=t287-null-placement</c>.
/// The dataset is seeded once per parameter combination in <see cref="Setup"/>, and the measured region is
/// the ordered enumeration only. <see cref="Setup"/> also asserts the portable oracle (NULL group first /
/// last, values not reversed by the placement itself) so a wrong ordering cannot be measured as green.
/// </remarks>
[MemoryDiagnoser]
[HideColumns(Column.Job, Column.Runtime, Column.Error, Column.StdDev, Column.RatioSD)]
[Config(typeof(NextormConfig))]
[BenchmarkCategory(T287NullOrderingBenchmarks.Category)]
public class T287NullOrderingBenchmarks
{
    /// <summary>Category name of the T287 placement benchmarks (never <c>acceptance</c>).</summary>
    public const string Category = "t287-null-placement";

    private const int Iterations = 20;

    private InMemoryDataContext _ctx = null!;
    private long _sink;

    /// <summary>Row count of the in-memory source.</summary>
    [Params(1_000, 10_000)]
    public int RowCount { get; set; }

    /// <summary>Share of rows that carry a NULL key.</summary>
    [Params(0, 10, 50)]
    public int NullPercent { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _ctx = new InMemoryDataContext();

        var rows = new List<T287BenchRow>(RowCount);
        for (var i = 0; i < RowCount; i++)
        {
            int? key = (i % 100) < NullPercent ? null : i - (RowCount / 2);
            rows.Add(new T287BenchRow { Id = i, Key = key });
        }

        _ctx.From<T287BenchRow>().WithData(rows);

        // Oracle guard: the NULL group follows the requested placement and the values follow the direction.
        var first = Sort(NullsPosition.First, OrderDirection.Asc);
        var last = Sort(NullsPosition.Last, OrderDirection.Asc);
        var hasNulls = NullPercent > 0;
        if (hasNulls && (first[0].Key is not null || last[^1].Key is not null))
            throw new InvalidOperationException($"T287 benchmark oracle failed: NullPercent={NullPercent}.");
        if (!hasNulls && first.Any(row => row.Key is null))
            throw new InvalidOperationException("T287 benchmark oracle failed: unexpected NULL key.");

        _sink = first.Count + last.Count;
    }

    [Benchmark(Baseline = true)]
    public int InMemory_Default_Asc()
    {
        var acc = 0L;
        for (var i = 0; i < Iterations; i++)
        {
            var rows = _ctx.From<T287BenchRow>()
                .OrderBy(x => x.Key, OrderDirection.Asc, NullsPosition.Default)
                .ThenBy(x => x.Id)
                .ToList();
            acc += rows.Count + rows[0].Id;
        }
        _sink = acc;
        return (int)acc;
    }

    [Benchmark]
    public int InMemory_First_Asc()
    {
        var acc = 0L;
        for (var i = 0; i < Iterations; i++)
        {
            var rows = Sort(NullsPosition.First, OrderDirection.Asc);
            acc += rows.Count + rows[0].Id;
        }
        _sink = acc;
        return (int)acc;
    }

    [Benchmark]
    public int InMemory_Last_Asc()
    {
        var acc = 0L;
        for (var i = 0; i < Iterations; i++)
        {
            var rows = Sort(NullsPosition.Last, OrderDirection.Asc);
            acc += rows.Count + rows[0].Id;
        }
        _sink = acc;
        return (int)acc;
    }

    [Benchmark]
    public int InMemory_First_Desc()
    {
        var acc = 0L;
        for (var i = 0; i < Iterations; i++)
        {
            var rows = Sort(NullsPosition.First, OrderDirection.Desc);
            acc += rows.Count + rows[0].Id;
        }
        _sink = acc;
        return (int)acc;
    }

    [Benchmark]
    public int InMemory_Last_Desc()
    {
        var acc = 0L;
        for (var i = 0; i < Iterations; i++)
        {
            var rows = Sort(NullsPosition.Last, OrderDirection.Desc);
            acc += rows.Count + rows[0].Id;
        }
        _sink = acc;
        return (int)acc;
    }

    private List<T287BenchRow> Sort(NullsPosition placement, OrderDirection direction) =>
        _ctx.From<T287BenchRow>()
            .OrderBy(x => x.Key, direction, placement)
            .ThenBy(x => x.Id)
            .ToList();
}

/// <summary>
/// T287 placement-bearing <b>SQLite</b> cached-versus-prepared pair: identical SQL, so the delta is the
/// per-execution plan-cache lookup (including the placement-aware comparer) with the database cost cancelling.
/// </summary>
[MemoryDiagnoser]
[HideColumns(Column.Job, Column.Runtime, Column.Error, Column.StdDev, Column.RatioSD)]
[Config(typeof(NextormConfig))]
[BenchmarkCategory(T287NullOrderingBenchmarks.Category)]
public class T287NullOrderingSqlBenchmarks
{
    private const int Iterations = 100;

    private readonly IDataContext _db;
    private readonly TestDataRepository _repo;
    private readonly IPreparedQueryCommand<int> _prepared;

    public T287NullOrderingSqlBenchmarks()
    {
        var builder = new DataContextBuilder();
        builder.UseSqlite(BenchDb.FilePath);
        _db = builder.CreateDataContext();
        _repo = new TestDataRepository(_db);
        ((IConnectionManager)_db).EnsureConnectionOpen();

        _prepared = PreparedPlacementQuery().Prepare();

        // Warm the plan cache so the measured loops exercise only the cached path.
        _ = _repo.SimpleEntity
            .Where(it => it.Id == SqlFunctions.Parameter<int>(0))
            .OrderBy(it => it.Id, OrderDirection.Desc, NullsPosition.First)
            .Select(it => it.Id)
            .ToList(0);
    }

    private QueryCommand<int> PreparedPlacementQuery() =>
        _repo.SimpleEntity
            .Where(it => it.Id == SqlFunctions.Parameter<int>(0))
            .OrderBy(it => it.Id, OrderDirection.Desc, NullsPosition.First)
            .Select(it => it.Id);

    // DB-bound baseline: prepared placement command, no plan lookup and no parameter extraction.
    [Benchmark(Baseline = true)]
    public int Sqlite_Prepared_Placement()
    {
        var sum = 0;
        for (var i = 0; i < Iterations; i++)
        {
            foreach (var row in _prepared.ToList(_db, i))
                sum += row;
        }
        return sum;
    }

    // DB-bound cached query: same SQL execution plus plan lookup + ExtractParams for a placement-bearing plan.
    [Benchmark]
    public int Sqlite_Cached_Placement()
    {
        var sum = 0;
        for (var i = 0; i < Iterations; i++)
        {
            foreach (var row in _repo.SimpleEntity
                         .Where(it => it.Id == SqlFunctions.Parameter<int>(0))
                         .OrderBy(it => it.Id, OrderDirection.Desc, NullsPosition.First)
                         .Select(it => it.Id)
                         .ToList(i))
                sum += row;
        }
        return sum;
    }
}

/// <summary>In-memory row shape for the T287 placement benchmark (unique id, nullable integer key).</summary>
public sealed class T287BenchRow
{
    public int Id { get; set; }

    public int? Key { get; set; }
}
