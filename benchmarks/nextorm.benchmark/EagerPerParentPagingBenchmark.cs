using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Benchmark;

/// <summary>
/// D211 (#211) dedicated **selective** benchmark for per-parent top-N eager loading: a <c>LoadWith</c>
/// child query whose own <c>Offset</c>/<c>Limit</c> is applied independently to each parent through a
/// partitioned <c>row_number()</c> child source.
/// </summary>
/// <remarks>
/// It is deliberately **not** tagged with the <c>acceptance</c> category, so the seven-case acceptance
/// suite is unchanged; run it explicitly with
/// <c>--filter "*EagerPerParentPagingBenchmark*" --job short</c>. The dataset is seeded once per size in
/// <see cref="Setup"/>, so the measured region is the query, not the setup. Each case loops
/// <see cref="InternalIterations"/> times to exercise the repeated prepared/cached execution, asserts the
/// command count of that same invocation, and returns a checksum so a page that silently returns the wrong
/// rows cannot be measured as green.
/// </remarks>
[GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByCategory)]
[HideColumns(Column.Job, Column.Runtime, Column.Error, Column.StdDev, Column.RatioSD)]
[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
public class EagerPerParentPagingBenchmark
{
    private const int ChildPageOffset = 1;
    private const int ChildPageSize = 2;
    private const int InternalIterations = 10;
    private const int InsertBatchSize = 250;
    private const string Category = "eager-perparent-paging";

    private readonly CountingInterceptor _counter = new();
    private string _path = null!;
    private IDataContext _db = null!;

    /// <summary>The three required parent counts: 1×8, 100×20 and 1000×20 parent/child sizes.</summary>
    [Params(1, 100, 1000)]
    public int ParentCount { get; set; }

    private int ChildrenPerParent => ParentCount == 1 ? 8 : 20;

    [GlobalSetup]
    public void Setup()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nextorm-bench");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, $"perparent-{ParentCount}-{Guid.NewGuid():N}.db");

        using (var conn = new SqliteConnection($"Data Source={_path}"))
        {
            conn.Open();
            using var create = conn.CreateCommand();
            create.CommandText =
                "create table pp_parent (id integer primary key, name text);" +
                "create table pp_child (id integer primary key, parent_id int not null, sort_key int not null);" +
                "create index ix_pp_child_parent on pp_child (parent_id);";
            create.ExecuteNonQuery();
        }

        var builder = new DataContextBuilder();
        builder.UseSqlite(_path);
        builder.AddInterceptor(_counter);
        _db = builder.CreateDataContext();
        ((IConnectionManager)_db).EnsureConnectionOpen();

        Seed();

        // Independent reference page: unpaged load + client-side order → Skip → Take. Both server-paged
        // modes must reproduce it exactly (a benchmark that silently measures the wrong rows is not
        // evidence). These warm-up queries also fill both plan caches, so the measured runs hit the
        // cached path only.
        var reference = LoadReferencePages();
        var split = LoadPagedPages(EagerLoadMode.SplitQuery);
        var single = LoadPagedPages(EagerLoadMode.SingleQuery);
        if (!PagesEqual(split, reference) || !PagesEqual(single, reference))
            throw new InvalidOperationException(
                $"Per-parent page mismatch at {ParentCount}x{ChildrenPerParent}: " +
                $"split={Describe(split)}, single={Describe(single)}, reference={Describe(reference)}");

        // Warm the measured benchmark methods themselves (their caches are already primed above) and
        // prove their command-count assertions hold before the timed region.
        _ = Split_Paged();
        _ = SingleQuery_Paged();
        _ = Split_Unpaged_ReferencePage();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _db.Dispose();
        try
        {
            File.Delete(_path);
        }
        catch (IOException)
        {
            // The temp database is best-effort cleanup; a leftover file must not fail the run.
        }
    }

    /// <summary>Split-query per-parent paging: parent statement + one batched ranked child statement.</summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory(Category)]
    public long Split_Paged()
    {
        long checksum = 0;
        for (var i = 0; i < InternalIterations; i++)
        {
            _counter.Reset();
            var parents = _db.From<PpParent>()
                .LoadWith(p => p.Children, PagedChildQuery, p => p.Id, c => c.ParentId, EagerLoadMode.SplitQuery)
                .OrderBy(p => p.Id)
                .ToList();
            EnsureCommandCount(2);
            checksum += LoadedChecksum(parents);
        }

        return checksum;
    }

    /// <summary>Single-query per-parent paging: one denormalized command with the ranked child source.</summary>
    [Benchmark]
    [BenchmarkCategory(Category)]
    public long SingleQuery_Paged()
    {
        long checksum = 0;
        for (var i = 0; i < InternalIterations; i++)
        {
            _counter.Reset();
            var parents = _db.From<PpParent>()
                .LoadWith(p => p.Children, PagedChildQuery, p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery)
                .OrderBy(p => p.Id)
                .ToList();
            EnsureCommandCount(1);
            checksum += LoadedChecksum(parents);
        }

        return checksum;
    }

    /// <summary>
    /// The existing "unpaged load plus a reference page" calculation: load all children and apply
    /// <c>OrderBy → Skip → Take</c> per parent in memory. Compared against the server-paged cases, it is a
    /// like-for-like page result with a deliberately unlike workload (it transfers and materializes every
    /// child), so its numbers must not be read as a regression of the paged path.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory(Category)]
    public long Split_Unpaged_ReferencePage()
    {
        long checksum = 0;
        for (var i = 0; i < InternalIterations; i++)
        {
            _counter.Reset();
            var parents = _db.From<PpParent>()
                .LoadWith(p => p.Children, UnpagedChildQuery, p => p.Id, c => c.ParentId, EagerLoadMode.SplitQuery)
                .OrderBy(p => p.Id)
                .ToList();
            EnsureCommandCount(2);
            checksum += ReferenceChecksum(parents);
        }

        return checksum;
    }

    private static EntityBuilder<PpChild> PagedChildQuery(IDataContext ctx)
        => ctx.From<PpChild>().OrderBy(x => x.SortKey).Offset(ChildPageOffset).Limit(ChildPageSize);

    private static EntityBuilder<PpChild> UnpagedChildQuery(IDataContext ctx)
        => ctx.From<PpChild>().OrderBy(x => x.SortKey);

    /// <summary>The already-paged children the loader attached (the server-side page).</summary>
    private static long LoadedChecksum(IReadOnlyList<PpParent> parents)
        => parents.Sum(p => p.Children.Sum(c => (long)c.Id));

    /// <summary>The reference page: the same <c>order → Skip → Take</c> slice computed on the loaded set.</summary>
    private static long ReferenceChecksum(IReadOnlyList<PpParent> parents)
        => parents.Sum(p => p.Children
            .OrderBy(c => c.SortKey)
            .Skip(ChildPageOffset)
            .Take(ChildPageSize)
            .Sum(c => (long)c.Id));

    private List<int[]> LoadReferencePages()
    {
        var parents = _db.From<PpParent>()
            .LoadWith(p => p.Children, UnpagedChildQuery, p => p.Id, c => c.ParentId, EagerLoadMode.SplitQuery)
            .OrderBy(p => p.Id)
            .ToList();
        return parents
            .Select(p => p.Children
                .OrderBy(c => c.SortKey)
                .Skip(ChildPageOffset)
                .Take(ChildPageSize)
                .Select(c => c.Id)
                .ToArray())
            .ToList();
    }

    private List<int[]> LoadPagedPages(EagerLoadMode mode)
    {
        var parents = _db.From<PpParent>()
            .LoadWith(p => p.Children, PagedChildQuery, p => p.Id, c => c.ParentId, mode)
            .OrderBy(p => p.Id)
            .ToList();
        return parents.Select(p => p.Children.OrderBy(c => c.SortKey).Select(c => c.Id).ToArray()).ToList();
    }

    private static bool PagesEqual(List<int[]> left, List<int[]> right)
        => left.Count == right.Count && left.Zip(right).All(pair => pair.First.SequenceEqual(pair.Second));

    private static string Describe(List<int[]> pages)
        => string.Join(";", pages.Take(3).Select(p => string.Join(",", p)));

    private void EnsureCommandCount(int expected)
    {
        if (_counter.Executing != expected)
            throw new InvalidOperationException(
                $"Expected {expected} executed commands but observed {_counter.Executing}.");
    }

    private void Seed()
    {
        var parents = new List<PpParent>(ParentCount);
        var children = new List<PpChild>(ParentCount * ChildrenPerParent);
        for (var p = 1; p <= ParentCount; p++)
        {
            parents.Add(new PpParent { Id = p, Name = "p" + p });
            for (var c = 0; c < ChildrenPerParent; c++)
                children.Add(new PpChild { Id = (p * 100) + c, ParentId = p, SortKey = c });
        }

        InsertInBatches(parents);
        InsertInBatches(children);
    }

    private void InsertInBatches<T>(IReadOnlyList<T> rows)
    {
        for (var offset = 0; offset < rows.Count; offset += InsertBatchSize)
        {
            var count = Math.Min(InsertBatchSize, rows.Count - offset);
            var batch = new T[count];
            for (var i = 0; i < count; i++)
                batch[i] = rows[offset + i];

            _db.CreateInsertBuilder<T>().Values(batch).Insert();
        }
    }

    private sealed class CountingInterceptor : IQueryInterceptor
    {
        private int _executing;

        public int Executing => Volatile.Read(ref _executing);

        public void Reset() => Volatile.Write(ref _executing, 0);

        public void CommandExecuting(CommandEventData eventData, DbCommand command)
            => Interlocked.Increment(ref _executing);
    }
}

[SqlTable("pp_parent")]
public sealed class PpParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<PpChild> Children { get; } = new List<PpChild>();
}

[SqlTable("pp_child")]
public sealed class PpChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("sort_key")]
    public int SortKey { get; set; }
}
