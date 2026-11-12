using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// I7/M3 native projection route: a value-agnostic shape derived from the freshly built projection
/// command finds the compiled holder, binds the current closure values and executes it without
/// fingerprinting the raw descriptor, staging the stored preparation, rebuilding the plan key or
/// interrogating the normalized store. These tests run against a real SQLite database, so a stale
/// binding or a wrongly reused plan shows up as a wrong answer rather than as an allocation number.
/// </summary>
[Collection("DataContextCache clear")]
public sealed class Issue319NativeProjectionTests : IDisposable
{
    [Table("native_proj_row")]
    private sealed class NativeProjRow
    {
        public int Id { get; set; }
        public int Value { get; set; }
        public string? Name { get; set; }
    }

    // A type of its own: the filter test declares global metadata for it, and the metadata cache is
    // process-wide, so no other test may share the type.
    [Table("native_proj_filtered_row")]
    private sealed class NativeProjFilteredRow
    {
        public int Id { get; set; }
        public int Value { get; set; }
    }

    private sealed class RowProjection
    {
        public int Id { get; set; }
    }

    private sealed class BucketCount
    {
        public int Bucket { get; set; }
        public int Count { get; set; }
    }

    private readonly SqliteConnection _connection;
    private readonly SqliteDataContext _context;

    public Issue319NativeProjectionTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        using (var setup = _connection.CreateCommand())
        {
            setup.CommandText = "create table native_proj_row (id integer primary key, value integer, name text); " +
                                "insert into native_proj_row (id, value, name) values (1, 10, 'Ab'), (2, 20, 'Cd'), (3, 30, 'Ef'), (4, 40, 'Gh'), (5, 50, 'Ij'); " +
                                "create table native_proj_filtered_row (id integer primary key, value integer); " +
                                "insert into native_proj_filtered_row (id, value) values (1, 10);";
            setup.ExecuteNonQuery();
        }

        _context = new SqliteDataContext(_connection, new DataContextBuilder());
        QueryPlanStore.Clear();
        QueryPlanner.ResetCounters();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    // The same call site, so every invocation hoists a fresh display class: exactly the shape the
    // normalized plan key cannot reuse and the native route exists to serve.
    private Task<List<RowProjection>> ProjectAsync(int id) => _context.From<NativeProjRow>()
        .Where(x => x.Id == id)
        .Select(x => new RowProjection { Id = x.Id })
        .ToListAsync();

    private List<RowProjection> Project(int id) => _context.From<NativeProjRow>()
        .Where(x => x.Id == id)
        .Select(x => new RowProjection { Id = x.Id })
        .ToList();

    // Two call sites that differ only in the projected column: one shape if the member is not part of it.
    private List<int> ProjectIdColumn() => _context.From<NativeProjRow>()
        .Where(x => x.Id == 1)
        .Select(x => x.Id)
        .ToList();

    private List<int> ProjectValueColumn() => _context.From<NativeProjRow>()
        .Where(x => x.Id == 1)
        .Select(x => x.Value)
        .ToList();

    // Two call sites that differ only in the invoked function: one shape if the method is not part of it.
    private List<string?> ProjectHex() => _context.From<NativeProjRow>()
        .Select(x => SqlFunctions.Sqlite.hex(x.Name))
        .ToList();

    private List<string?> ProjectQuote() => _context.From<NativeProjRow>()
        .Select(x => SqlFunctions.Sqlite.quote(x.Name))
        .ToList();

    // One call site reading a *named* table: the name is rendered from the source, not from the graphs.
    private List<long> ReadNamed(string table) => _context.From(table)
        .Select(t => t.GetInt64("id"))
        .ToList();

    private Task<List<BucketCount>> BucketsAsync() => _context.From<NativeProjRow>()
        .GroupBy(x => new { Bucket = x.Id % 2 })
        .Select(x => new BucketCount { Bucket = x.Id % 2, Count = SqlFunctions.Sql.count() })
        .ToListAsync();

    [Fact]
    public async Task Project_FreshClosurePerCall_ShouldHitTheNativeIndexAndBindTheCurrentValue()
    {
        var first = await ProjectAsync(3);
        first.Should().ContainSingle().Which.Id.Should().Be(3);
        QueryPlanStore.NativePublishes.Should().BeGreaterThan(0, "the miss publishes the compiled holder");

        var hits = QueryPlanStore.NativeHits;
        var second = await ProjectAsync(5);
        second.Should().ContainSingle().Which.Id.Should().Be(5, "the bound value is the current one, not the published one");
        QueryPlanStore.NativeHits.Should().BeGreaterThan(hits, "the second call is answered from the index");

        (await ProjectAsync(999)).Should().BeEmpty();
    }

    [Fact]
    public void Project_SynchronousTerminal_ShouldAlsoHit()
    {
        Project(1).Should().ContainSingle().Which.Id.Should().Be(1);

        var hits = QueryPlanStore.NativeHits;
        Project(4).Should().ContainSingle().Which.Id.Should().Be(4);
        QueryPlanStore.NativeHits.Should().BeGreaterThan(hits);
    }

    [Fact]
    public void Project_DifferentColumn_ShouldNotReuseThePlan()
    {
        ProjectIdColumn().Should().ContainSingle().Which.Should().Be(1);
        ProjectValueColumn().Should().ContainSingle().Which.Should().Be(10,
            "the second call must execute its own column, not the first call's statement");
    }

    [Fact]
    public void Project_DifferentNamedSource_ShouldNotReuseThePlan()
    {
        using (var setup = _connection.CreateCommand())
        {
            setup.CommandText = "create table native_proj_src_a (id integer); insert into native_proj_src_a (id) values (111); " +
                                "create table native_proj_src_b (id integer); insert into native_proj_src_b (id) values (222);";
            setup.ExecuteNonQuery();
        }

        ReadNamed("native_proj_src_a").Should().Equal(111L);
        ReadNamed("native_proj_src_b")
            .Should().ContainSingle("the source name is part of the statement")
            .Which.Should().Be(222L, "the second read must not reuse the first one's plan");
    }

    [Fact]
    public void Project_DifferentFunction_ShouldNotReuseThePlan()
    {
        // Aliasing here is silent wrong data: the second call would execute the first one's statement.
        var hex = ProjectHex()[0];
        var quote = ProjectQuote()[0];

        hex.Should().Be("4162", "hex('Ab') is the hexadecimal rendering");
        quote.Should().Be("'Ab'", "quote('Ab') is the SQL literal");
        ProjectHex()[0].Should().Be("4162", "the first shape still answers its own function");
    }

    [Fact]
    public async Task Project_GroupedShape_ShouldHitAndReturnEveryGroup()
    {
        var first = await BucketsAsync();
        first.Should().HaveCount(2);
        first.Sum(row => row.Count).Should().Be(5);
        QueryPlanStore.NativePublishes.Should().BeGreaterThan(0);

        var hits = QueryPlanStore.NativeHits;
        var second = await BucketsAsync();
        QueryPlanStore.NativeHits.Should().BeGreaterThan(hits);
        second.Should().BeEquivalentTo(first);
    }

    [Fact]
    public async Task Project_DifferentPredicate_ShouldNotReuseThePlan()
    {
        await ProjectAsync(2);

        var greater = await _context.From<NativeProjRow>()
            .Where(x => x.Id > 3)
            .Select(x => new RowProjection { Id = x.Id })
            .ToListAsync();

        greater.Select(row => row.Id).Should().Equal(4, 5);
    }

    [Fact]
    public async Task Project_Modifier_ShouldKeepTheLegacyRoute()
    {
        await ProjectAsync(2);
        var publishes = QueryPlanStore.NativePublishes;

        // Paging and ordering change the statement without changing the filter/group/projection graphs
        // the native shape models, so the command must not be keyed by that shape.
        var paged = await _context.From<NativeProjRow>()
            .Where(x => x.Id > 1)
            .OrderBy(x => x.Id)
            .Limit(2)
            .Select(x => new RowProjection { Id = x.Id })
            .ToListAsync();

        paged.Select(row => row.Id).Should().Equal(2, 3);
        QueryPlanStore.NativePublishes.Should().Be(publishes,
            "a paged ordered projection is not a shape this route proves");
    }

    [Fact]
    public async Task Project_FilteredEntity_ShouldKeepTheLegacyRoute()
    {
        // A global/builder filter is injected only during preparation: it changes the statement without
        // changing the raw shape, so a filtered entity must never reuse a native plan. The metadata
        // cache is process-wide, so the declaration starts from a cold one.
        DataContextCache.Clear();
        QueryPlanner.ResetCounters();
        try
        {
            var rows = await _context.From<NativeProjFilteredRow>(builder => builder.HasQueryFilter(entity => entity.Value > 500))
                .Where(x => x.Id == 1)
                .Select(x => new RowProjection { Id = x.Id })
                .ToListAsync();

            rows.Should().BeEmpty("the declared filter must actually apply");
            QueryPlanStore.NativePublishes.Should().Be(0, "a filter makes the shape ineligible");
        }
        finally
        {
            DataContextCache.Clear();
            QueryPlanner.ResetCounters();
        }
    }
}
