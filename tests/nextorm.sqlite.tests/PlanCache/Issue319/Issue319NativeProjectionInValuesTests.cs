using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// I7/M3 native projection route and value-list membership (<c>in</c>/<c>Contains</c>): the rendered
/// statement follows the collection — its length and its null layout — so
/// <see cref="NativeProjectionRoute"/> can never publish such a shape into its value-agnostic index.
/// These tests pin that the probe does not build and discard a shape it can never store, that the
/// ordinary cached path still answers the current collection (values, length and content replacement,
/// empty list, composed predicate), and that a publishable shape keeps its native fast path.
/// </summary>
[Collection("DataContextCache clear")]
public sealed class Issue319NativeProjectionInValuesTests : IDisposable
{
    [Table("native_proj_row")]
    private sealed class NativeProjRow
    {
        public int Id { get; set; }
        public int Value { get; set; }
    }

    private readonly SqliteConnection _connection;
    private readonly SqliteDataContext _context;

    // Two entries, so a replacement has the same length and a different content.
    private readonly int[] _replaced = { 2, 4 };

    public Issue319NativeProjectionInValuesTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        using (var setup = _connection.CreateCommand())
        {
            setup.CommandText = "create table native_proj_row (id integer primary key, value integer); " +
                                "insert into native_proj_row (id, value) values (1, 10), (2, 20), (3, 30), (4, 40), (5, 50);";
            setup.ExecuteNonQuery();
        }

        _context = new SqliteDataContext(_connection, new DataContextBuilder());
        DataContextCache.Clear();
        QueryPlanner.ResetCounters();
        QueryPlanStore.Clear();
        QueryPlanStore.ResetNativeCounters();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    // Same call site per helper: every invocation hoists a fresh closure, which is exactly the shape the
    // native route exists to serve — for a value-free predicate.
    private List<int> Publishable(int id) => _context.From<NativeProjRow>()
        .Where(x => x.Id == id)
        .Select(x => x.Id)
        .ToList();

    // A captured collection: the expression shape carries the closure access, never the values.
    private List<int> InCaptured(int[] ids) => _context.From<NativeProjRow>()
        .Where(x => SqlFunctions.Sql.@in(x.Id, ids))
        .Select(x => x.Id)
        .ToList();

    private Task<List<int>> InCapturedAsync(int[] ids) => _context.From<NativeProjRow>()
        .Where(x => SqlFunctions.Sql.@in(x.Id, ids))
        .Select(x => x.Id)
        .ToListAsync();

    private List<int> ContainsCaptured(int[] ids) => _context.From<NativeProjRow>()
        .Where(x => ids.Contains(x.Id))
        .Select(x => x.Id)
        .ToList();

    private Task<List<int>> ContainsCapturedAsync(int[] ids) => _context.From<NativeProjRow>()
        .Where(x => ids.Contains(x.Id))
        .Select(x => x.Id)
        .ToListAsync();

    // Inline lists are part of the expression shape, but the rendered statement still follows the list.
    private List<int> InInline() => _context.From<NativeProjRow>()
        .Where(x => SqlFunctions.Sql.@in(x.Id, new[] { 1, 3 }))
        .Select(x => x.Id)
        .ToList();

    private Task<List<int>> InInlineAsync() => _context.From<NativeProjRow>()
        .Where(x => SqlFunctions.Sql.@in(x.Id, new[] { 1, 3 }))
        .Select(x => x.Id)
        .ToListAsync();

    private List<int> ContainsInline() => _context.From<NativeProjRow>()
        .Where(x => new[] { 2, 4 }.Contains(x.Id))
        .Select(x => x.Id)
        .ToList();

    // A value list nested inside a composed predicate keeps the C# semantics of both operands.
    private List<int> ComposedCaptured(int[] ids) => _context.From<NativeProjRow>()
        .Where(x => ids.Contains(x.Id) && x.Value > 20)
        .Select(x => x.Id)
        .ToList();

    // Independent oracle: the same predicate evaluated directly by the provider.
    private List<int> Oracle(params int[] ids)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "select id from native_proj_row where id in (" +
                              string.Join(", ", ids) + ") order by id";
        using var reader = command.ExecuteReader();
        var rows = new List<int>();
        while (reader.Read())
            rows.Add(reader.GetInt32(0));

        return rows;
    }

    [Fact]
    public void ValueListCommand_ShouldNotProbeTheNativeIndexThatCanNeverStoreIt()
    {
        Publishable(2).Should().Equal(2);
        QueryPlanStore.NativePublishes.Should().BeGreaterThan(0,
            "the value-free control shape occupies the index, so an IN probe would hash the graph and miss");

        var misses = QueryPlanStore.NativeMisses;
        var publishes = QueryPlanStore.NativePublishes;

        InInline().OrderBy(x => x).Should().Equal(1, 3);
        InCaptured(new[] { 2, 4 }).OrderBy(x => x).Should().Equal(2, 4);
        ContainsInline().OrderBy(x => x).Should().Equal(2, 4);
        ContainsCaptured(new[] { 1, 5 }).OrderBy(x => x).Should().Equal(1, 5);

        QueryPlanStore.NativeMisses.Should().Be(misses,
            "a value-list statement follows the collection, so Probe.Publish refuses its shape: the probe must not build and discard it");
        QueryPlanStore.NativePublishes.Should().Be(publishes);
    }

    [Fact]
    public async Task ValueListTerminals_ShouldAnswerTheCurrentCollectionAndMatchTheOrdinarySql()
    {
        InInline().OrderBy(x => x).Should().Equal(Oracle(1, 3));
        (await InInlineAsync()).OrderBy(x => x).Should().Equal(Oracle(1, 3));

        InCaptured(new[] { 1, 3 }).OrderBy(x => x).Should().Equal(Oracle(1, 3));
        InCaptured(_replaced).OrderBy(x => x).Should().Equal(Oracle(2, 4),
            "a replaced collection must re-key the plan");
        InCaptured(new[] { 1, 2, 5 }).OrderBy(x => x).Should().Equal(Oracle(1, 2, 5),
            "a longer collection must re-key the plan");
        InCaptured([]).Should().BeEmpty("an empty list is an always-false predicate");

        (await InCapturedAsync(new[] { 3 })).OrderBy(x => x).Should().Equal(Oracle(3));
        (await InCapturedAsync(_replaced)).OrderBy(x => x).Should().Equal(Oracle(2, 4));

        ContainsInline().OrderBy(x => x).Should().Equal(Oracle(2, 4));
        ContainsCaptured(new[] { 2, 5 }).OrderBy(x => x).Should().Equal(Oracle(2, 5));
        (await ContainsCapturedAsync(new[] { 1, 3 })).OrderBy(x => x).Should().Equal(Oracle(1, 3));

        ComposedCaptured(new[] { 1, 3, 4 }).OrderBy(x => x).Should().Equal(Oracle(3, 4),
            "a composed value-list predicate keeps C# semantics");
    }

    [Fact]
    public void PublishableProjection_ShouldKeepItsNativeFastPath()
    {
        Publishable(2).Should().Equal(2);
        QueryPlanStore.NativePublishes.Should().BeGreaterThan(0, "a value-free shape is publishable");

        var hits = QueryPlanStore.NativeHits;
        Publishable(5).Should().Equal(new[] { 5 }, "the bound value is the current one");
        QueryPlanStore.NativeHits.Should().BeGreaterThan(hits, "the value-agnostic shape is still served from the index");
        QueryPlanStore.NativeMisses.Should().Be(0, "neither call is an ineligible probe");
    }

    [Fact]
    public void PreparedValueListCommand_ShouldKeepTheOrdinaryPath()
    {
        var command = _context.From<NativeProjRow>()
            .Where(x => SqlFunctions.Sql.@in(x.Id, new[] { 2, 5 }))
            .Select(x => x.Id);

        command.PrepareCommand(CancellationToken.None);
        command.ToList().OrderBy(x => x).Should().Equal(2, 5);

        QueryPlanStore.NativePublishes.Should().Be(0, "a prepared command never enters the native route");
    }

    // An EMPTY inline scalar list in the projection is keyed by the projection graph and renders no
    // parameters, so the native route must still publish and hit it: the value-list classification is
    // allowlisted to "captured or tuple" there. Empty on purpose — a non-empty inline list renders
    // parameters with no captures, which NativeBinder.TryCreate legitimately refuses to map, so it is
    // not a native shape. The value-list queries run first so a sticky probe mode would surface here.
    private List<bool> InlineProjectionList() => _context.From<NativeProjRow>()
        .Select(x => new int[] { }.Contains(x.Id))
        .ToList();

    [Fact]
    public void PublishableInlineProjectionList_ShouldStillPublishAndHit()
    {
        InCaptured(new[] { 1, 3 }).OrderBy(x => x).Should().Equal(1, 3);
        InInline().OrderBy(x => x).Should().Equal(1, 3);

        var publishes = QueryPlanStore.NativePublishes;
        InlineProjectionList().Should().HaveCount(5).And.OnlyContain(isMatch => !isMatch);
        QueryPlanStore.NativePublishes.Should().BeGreaterThan(publishes,
            "an inline projection list carries no captures, so the shape stays publishable");

        var hits = QueryPlanStore.NativeHits;
        InlineProjectionList().Should().HaveCount(5).And.OnlyContain(isMatch => !isMatch);
        QueryPlanStore.NativeHits.Should().BeGreaterThan(hits, "the second call is served from the index");
    }
}
