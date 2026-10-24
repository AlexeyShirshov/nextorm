using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ClickHouse.Driver.ADO;
using FluentAssertions;
using FluentAssertions.Execution;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// T286b D7 (F07 Map): live execution of the ClickHouse-only map members against the pinned
/// <c>25.8-alpine</c> (25.8.33.6) server. A ClickHouse Map cannot be projected as a dictionary, so
/// every result is materialised as the declared <see cref="Tuple{TKey,TValue}"/> array of
/// <c>arrayZip(mapKeys(...), mapValues(...))</c>: the tests pin that shape, the vendor semantics of
/// each member (subtraction/addition of keys, sort orders, series filling, delimiter parsing), the
/// duplicate-key behaviour (preserved in the tuple array), and the deterministic refusals that never
/// reach the server.
/// </summary>
public sealed class T286bF07MapFunctionTests : ProviderTestSuite
{
    [SqlTable("t286b_f07_entity")]
    internal interface IT286bMapEntity
    {
        [Key]
        [Column("id")]
        int Id { get; set; }

        [Column("keys")]
        string[] Keys { get; set; }

        [Column("vals")]
        int[] Values { get; set; }

        [Column("data")]
        string Data { get; set; }
    }

    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None))
            .DbCommand.CommandText.Replace("\r\n", "\n");

    private static void Seed()
    {
        using var connection = new ClickHouseConnection(ClickHouseContainer.ConnectionString);
        connection.Open();
        foreach (var statement in new[]
                 {
                     "drop table if exists t286b_f07_entity",
                     "create table t286b_f07_entity (id Int32, keys Array(String), vals Array(Int32), data String) "
                     + "engine = Memory",
                     "insert into t286b_f07_entity values "
                     + "(1, ['a', 'b'], [1, 2], 'name:neymar, age:34 team:santos')"
                 })
        {
            using var command = connection.CreateCommand();
            command.CommandText = statement;
            command.ExecuteNonQuery();
        }
    }

    private static Dictionary<string, int> Left() => new(StringComparer.Ordinal)
    {
        ["a"] = 1,
        ["b"] = 2,
        ["c"] = 3
    };

    private static Dictionary<string, int> Right() => new(StringComparer.Ordinal)
    {
        ["b"] = 1
    };

    [Fact]
    public void MapSubtract_ShouldSubtractMatchingKeysAndKeepTheOthers()
    {
        Seed();
        using var ctx = Provider.CreateContext();
        var left = Left();
        var right = Right();

        var pairs = ctx.From<IT286bMapEntity>()
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.ClickHouse.map_subtract(left, right))
            .First();

        pairs.GetType().Should().Be(typeof(Tuple<string, int>[]));
        pairs.ToDictionary(pair => pair.Item1, pair => pair.Item2)
            .Should().Equal(new Dictionary<string, int> { ["a"] = 1, ["b"] = 1, ["c"] = 3 });
    }

    [Fact]
    public void MapUpdate_ShouldReplaceAndAddKeys()
    {
        Seed();
        using var ctx = Provider.CreateContext();
        var left = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 };
        var right = new Dictionary<string, int> { ["b"] = 9, ["c"] = 3 };

        var pairs = ctx.From<IT286bMapEntity>()
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.ClickHouse.map_update(left, right))
            .First();

        pairs.ToDictionary(pair => pair.Item1, pair => pair.Item2)
            .Should().Equal(new Dictionary<string, int> { ["a"] = 1, ["b"] = 9, ["c"] = 3 });
    }

    [Fact]
    public void SortMembers_ShouldReturnTheDeclaredKeyOrder()
    {
        Seed();
        using var ctx = Provider.CreateContext();
        var source = Left();

        var row = ctx.From<IT286bMapEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Reverse = SqlFunctions.ClickHouse.map_reverse_sort(source),
                Partial = SqlFunctions.ClickHouse.map_partial_sort(2L, source),
                PartialZero = SqlFunctions.ClickHouse.map_partial_sort(0L, source)
            })
            .First();

        using (new AssertionScope())
        {
            row.Reverse.Select(pair => pair.Item1).Should().Equal("c", "b", "a");
            row.Partial.Select(pair => pair.Item1).Should().Equal("a", "b", "c");
            row.PartialZero.Select(pair => pair.Item1).Should().Equal(
                new[] { "a", "b", "c" },
                "the pinned 25.8 server returns the key-sorted map for every accepted limit");
        }
    }

    [Fact]
    public void MapFromArrays_ShouldBuildTheTypedMapAndPreserveDuplicateKeys()
    {
        Seed();
        using var ctx = Provider.CreateContext();
        string[] keys = ["a", "b"];
        int[] values = [1, 2];
        string[] duplicateKeys = ["a", "a"];
        int[] duplicateValues = [1, 2];

        var row = ctx.From<IT286bMapEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                FromArrays = SqlFunctions.ClickHouse.map_from_arrays(keys, values),
                FromColumns = SqlFunctions.ClickHouse.map_from_arrays(x.Keys, x.Values),
                Duplicates = SqlFunctions.ClickHouse.map_from_arrays(duplicateKeys, duplicateValues)
            })
            .First();

        using (new AssertionScope())
        {
            row.FromArrays.ToDictionary(pair => pair.Item1, pair => pair.Item2)
                .Should().Equal(new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 });
            row.FromColumns.ToDictionary(pair => pair.Item1, pair => pair.Item2)
                .Should().Equal(new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 });
            row.Duplicates.Should().Equal(
                new[] { Tuple.Create("a", 1), Tuple.Create("a", 2) },
                "duplicate keys are preserved in the tuple array, not collapsed like a dictionary");
            row.FromArrays.GetType().Should().Be(typeof(Tuple<string, int>[]));
        }
    }

    [Fact]
    public void MapPopulateSeries_ShouldFillAndTruncateTheIntegerSeries()
    {
        Seed();
        using var ctx = Provider.CreateContext();
        var source = new Dictionary<int, int> { [1] = 10, [3] = 30 };

        var row = ctx.From<IT286bMapEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Filled = SqlFunctions.ClickHouse.map_populate_series(source),
                Extended = SqlFunctions.ClickHouse.map_populate_series(source, 4L),
                Truncated = SqlFunctions.ClickHouse.map_populate_series(source, 2L)
            })
            .First();

        using (new AssertionScope())
        {
            row.Filled.ToDictionary(pair => pair.Item1, pair => pair.Item2)
                .Should().Equal(new Dictionary<int, int> { [1] = 10, [2] = 0, [3] = 30 });
            row.Extended.ToDictionary(pair => pair.Item1, pair => pair.Item2)
                .Should().Equal(new Dictionary<int, int> { [1] = 10, [2] = 0, [3] = 30, [4] = 0 });
            row.Truncated.ToDictionary(pair => pair.Item1, pair => pair.Item2)
                .Should().Equal(new Dictionary<int, int> { [1] = 10, [2] = 0 });
        }
    }

    [Fact]
    public void ExtractKeyValuePairs_ShouldHonourTheDefaultAndExplicitDelimiters()
    {
        Seed();
        using var ctx = Provider.CreateContext();
        string mixed = "a=1,b=2";
        string duplicate = "a=1,a=2";

        var row = ctx.From<IT286bMapEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Defaults = SqlFunctions.ClickHouse.extract_key_value_pairs(x.Data),
                Explicit = SqlFunctions.ClickHouse.extract_key_value_pairs(mixed, "=", ","),
                WrongDefaults = SqlFunctions.ClickHouse.extract_key_value_pairs(mixed),
                Duplicates = SqlFunctions.ClickHouse.extract_key_value_pairs(duplicate, "=", ",")
            })
            .First();

        using (new AssertionScope())
        {
            row.Defaults.ToDictionary(pair => pair.Item1, pair => pair.Item2)
                .Should().Equal(new Dictionary<string, string>
                {
                    ["name"] = "neymar", ["age"] = "34", ["team"] = "santos"
                });
            row.Explicit.ToDictionary(pair => pair.Item1, pair => pair.Item2)
                .Should().Equal(new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" });
            row.WrongDefaults.Should().BeEmpty(
                "the default key/value delimiter is ':' and the default pair set is space/,/;");
            row.Duplicates.Should().Equal(
                new[] { Tuple.Create("a", "1"), Tuple.Create("a", "2") },
                "duplicate keys are preserved in the tuple array");
            row.Defaults.GetType().Should().Be(typeof(Tuple<string, string>[]));
        }
    }

    [Fact]
    public void Refusals_ShouldNeverReachTheServer()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        Dictionary<string, int?> nullableValues = [];
        Dictionary<string, int> stringKeyed = [];
        Dictionary<string, int> empty = [];

        using (new AssertionScope())
        {
            FluentActions.Invoking(() => SqlOf(ctx, ctx.From<IT286bMapEntity>()
                    .Select(x => new { V = SqlFunctions.ClickHouse.map_update((IDictionary<string, int>?)null, empty) })))
                .Should().Throw<NotSupportedException>()
                .WithMessage("*map_update*refused for a null operand*");

            FluentActions.Invoking(() => SqlOf(ctx, ctx.From<IT286bMapEntity>()
                    .Select(x => new { V = SqlFunctions.ClickHouse.map_subtract(nullableValues, nullableValues) })))
                .Should().Throw<NotSupportedException>()
                .WithMessage("*map_subtract*refused for the value type*Nullable*");

            FluentActions.Invoking(() => SqlOf(ctx, ctx.From<IT286bMapEntity>()
                    .Select(x => new { V = SqlFunctions.ClickHouse.map_populate_series(stringKeyed) })))
                .Should().Throw<NotSupportedException>()
                .WithMessage("*map_populate_series*refused for the key type*String*integer key*");

            FluentActions.Invoking(() => SqlOf(ctx, ctx.From<IT286bMapEntity>()
                    .Select(x => new { V = SqlFunctions.ClickHouse.extract_key_value_pairs((string?)null) })))
                .Should().Throw<NotSupportedException>()
                .WithMessage("*extract_key_value_pairs*refused for a null operand*");
        }
    }

    [Fact]
    public void Projection_ShouldMaterialiseEveryDeclaredShape()
    {
        Seed();
        using var ctx = Provider.CreateContext();
        var left = Left();
        var right = Right();
        var numbers = new Dictionary<int, int> { [1] = 10, [3] = 30 };

        var row = ctx.From<IT286bMapEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Subtract = SqlFunctions.ClickHouse.map_subtract(left, right),
                Update = SqlFunctions.ClickHouse.map_update(left, right),
                Reverse = SqlFunctions.ClickHouse.map_reverse_sort(left),
                Partial = SqlFunctions.ClickHouse.map_partial_sort(1L, left),
                FromArrays = SqlFunctions.ClickHouse.map_from_arrays(x.Keys, x.Values),
                Populate = SqlFunctions.ClickHouse.map_populate_series(numbers),
                Extract = SqlFunctions.ClickHouse.extract_key_value_pairs(x.Data)
            })
            .First();

        using (new AssertionScope())
        {
            row.Subtract.GetType().Should().Be(typeof(Tuple<string, int>[]));
            row.Update.GetType().Should().Be(typeof(Tuple<string, int>[]));
            row.Reverse.GetType().Should().Be(typeof(Tuple<string, int>[]));
            row.Partial.GetType().Should().Be(typeof(Tuple<string, int>[]));
            row.FromArrays.GetType().Should().Be(typeof(Tuple<string, int>[]));
            row.Populate.GetType().Should().Be(typeof(Tuple<int, int>[]));
            row.Extract.GetType().Should().Be(typeof(Tuple<string, string>[]));
        }
    }
}
