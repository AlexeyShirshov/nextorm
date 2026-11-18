using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// I7/M2 native <c>Any</c> route: a value-agnostic shape derived from the builder finds the compiled
/// holder, binds the current closure values and executes it without building a command, preparing it
/// or re-rendering the statement. These tests run against a real SQLite database, so a stale binding
/// shows up as a wrong answer rather than as an allocation number.
/// </summary>
[Collection("DataContextCache clear")]
public sealed class Issue319NativeAnyTests : IDisposable
{
    [Table("native_any_row")]
    private sealed class NativeAnyRow
    {
        public int Id { get; set; }
    }

    private readonly SqliteConnection _connection;
    private readonly SqliteDataContext _context;

    public Issue319NativeAnyTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        using (var setup = _connection.CreateCommand())
        {
            setup.CommandText = "create table native_any_row (id integer primary key); " +
                                "insert into native_any_row (id) values (1), (2), (3);";
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
    private Task<bool> AnyAsync(int id) => _context.From<NativeAnyRow>().Where(x => x.Id == id).AnyAsync();

    [Fact]
    public async Task Any_FreshClosurePerCall_ShouldHitTheNativeIndexAndBindTheCurrentValue()
    {
        (await AnyAsync(2)).Should().BeTrue();
        QueryPlanStore.NativePublishes.Should().BeGreaterThan(0, "the miss publishes the compiled holder");

        var hits = QueryPlanStore.NativeHits;
        (await AnyAsync(3)).Should().BeTrue();
        QueryPlanStore.NativeHits.Should().BeGreaterThan(hits, "the second call is answered from the index");
        QueryPlanStore.NativeMisses.Should().BeLessThan(2, "an equivalent shape must not be probed as a miss again");

        (await AnyAsync(999)).Should().BeFalse("the bound value is the current one, not the published one");
    }

    // Same site, so the two calls share one shape and differ only in the captured value.
    private bool Any(int id) => _context.From<NativeAnyRow>().Where(x => x.Id == id).Any();

    [Fact]
    public void Any_ShouldAlsoServeTheSynchronousTerminal()
    {
        Any(1).Should().BeTrue();
        var hits = QueryPlanStore.NativeHits;

        Any(999).Should().BeFalse("the bound value is the current one");

        QueryPlanStore.NativeHits.Should().BeGreaterThan(hits);
    }

    [Fact]
    public async Task Any_ShouldCreateAndKeepTheSharedAnyCommand()
    {
        // The context-shared Any/Count/All command is observable state (`DataContext.AnyCommand`) and the
        // other terminals reuse it. A native hit never builds the wrapper, so the first call on a context
        // must create it and no later hit may replace it or touch its caching policy.
        _context.AnyCommand.Should().BeNull("a fresh context has not built the shared command");

        (await AnyAsync(2)).Should().BeTrue();
        _context.AnyCommand.Should().NotBeNull("the first Any creates the shared command");

        var shared = _context.AnyCommand!.Value;
        (await AnyAsync(3)).Should().BeTrue();

        _context.AnyCommand!.Value.Should().BeSameAs(shared, "a native hit never replaces the shared command");
        shared.Cache.Should().BeTrue("the native route must not touch the sticky cache flag");
    }

    [Fact]
    public async Task Any_DifferentPredicate_ShouldNotReuseThePlan()
    {
        (await AnyAsync(2)).Should().BeTrue();

        var greater = await _context.From<NativeAnyRow>().Where(x => x.Id > 5).AnyAsync();
        var less = await _context.From<NativeAnyRow>().Where(x => x.Id < 0).AnyAsync();

        greater.Should().BeFalse();
        less.Should().BeFalse();
    }

    [Fact]
    public async Task Any_Modifier_ShouldKeepTheLegacyRoute()
    {
        (await AnyAsync(2)).Should().BeTrue();
        var publishes = QueryPlanStore.NativePublishes;

        // A modifier the native shape does not model (ORDER BY changes the statement) must not be
        // keyed by it: the legacy route answers and publishes nothing new for that shape.
        var ordered = await _context.From<NativeAnyRow>()
            .Where(x => x.Id == 2)
            .OrderBy(x => x.Id)
            .AnyAsync();

        ordered.Should().BeTrue();
        QueryPlanStore.NativePublishes.Should().Be(publishes,
            "an ordered Any is not a shape this route proves");
    }
}
