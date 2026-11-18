using System.Globalization;
using FluentAssertions;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Integration.Tests;

[Collection("Sqlite")]
public sealed class SqliteShardRoutingCoverageTests : IDisposable
{
    private const int BucketCount = 8;
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "nextorm-sharding-coverage-" + Guid.NewGuid().ToString("N"));

    public SqliteShardRoutingCoverageTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public void CompositeKeyQuery_ShouldRouteAllSupportedKeyTypesAndPredicateShapes()
    {
        var topology = CreateTopology();
        using var context = CreateContext(topology);
        CreateTables(topology, nameof(CanonicalShardRow),
            "Id INTEGER PRIMARY KEY, StringKey TEXT, GuidKey TEXT NOT NULL, " +
            "IntKey INTEGER NOT NULL, LongKey INTEGER NOT NULL, ShortKey INTEGER NOT NULL, " +
            "ByteKey INTEGER NOT NULL, BooleanKey INTEGER NOT NULL, DateTimeKey TEXT NOT NULL, " +
            "DateTimeOffsetKey TEXT NOT NULL, DecimalKey TEXT NOT NULL, Name TEXT NOT NULL");

        var expected = new CanonicalShardRow
        {
            Id = 17,
            StringKey = "tenant-α",
            GuidKey = Guid.Parse("d85b1407-351d-4694-9392-03acc5870eb1"),
            IntKey = 23,
            LongKey = 9_000_000_017L,
            ShortKey = 1234,
            ByteKey = 201,
            BooleanKey = true,
            DateTimeKey = new DateTime(2024, 3, 4, 5, 6, 7, DateTimeKind.Utc),
            DateTimeOffsetKey = new DateTimeOffset(2024, 3, 4, 5, 6, 7, TimeSpan.FromHours(2)),
            DecimalKey = 1234.56m,
            Name = "composite",
        };

        context.CreateInsertBuilder<CanonicalShardRow>().Values(expected).Insert().Should().Be(1);
        object?[] keys =
        [
            expected.StringKey,
            expected.GuidKey,
            expected.IntKey,
            expected.LongKey,
            expected.ShortKey,
            expected.ByteKey,
            expected.BooleanKey,
            expected.DateTimeKey,
            expected.DateTimeOffsetKey,
            expected.DecimalKey,
        ];

        var explicitlyRouted = context.From<CanonicalShardRow>().WithShardKey(keys);
        explicitlyRouted.WithShardKey(keys).Should().BeSameAs(explicitlyRouted);
        explicitlyRouted.Select(row => row.Name).ToList().Should().Equal("composite");
        var router = context.GetShardRouter();
        var bucket = router.GetBucket(expected);
        var otherKeys = (object?[])keys.Clone();
        var candidate = 0;
        while (router.GetBucket(typeof(CanonicalShardRow), otherKeys) == bucket && candidate < 10_000)
            otherKeys[0] = $"tenant-rebind-{candidate++}";

        router.GetBucket(typeof(CanonicalShardRow), otherKeys).Should().NotBe(bucket);
        var rebound = explicitlyRouted.WithShardKey(otherKeys);
        rebound.Should().NotBeSameAs(explicitlyRouted);
        rebound.Select(row => row.Name).ToList().Should().BeEmpty();

        var minimumId = 0;
        var actual = context.From<CanonicalShardRow>()
            .Where(row =>
                expected.StringKey == row.StringKey
                && row.GuidKey == expected.GuidKey
                && expected.GuidKey == row.GuidKey
                && row.IntKey == expected.IntKey
                && row.LongKey == expected.LongKey
                && row.ShortKey == expected.ShortKey
                && row.ByteKey == expected.ByteKey
                && row.BooleanKey == expected.BooleanKey
                && row.DateTimeKey == expected.DateTimeKey
                && row.DateTimeOffsetKey == expected.DateTimeOffsetKey
                && row.DecimalKey == expected.DecimalKey
                && row.Id > minimumId)
            .Select(row => row.Name)
            .ToList();

        actual.Should().Equal("composite");

        var owner = router.GetShard(bucket);
        ReadCount(topology.GetConnectionString(owner), $"{nameof(CanonicalShardRow)}_{bucket}")
            .Should().Be(1L);

        var nullableStringRow = new CanonicalShardRow
        {
            Id = 18,
            StringKey = null,
            GuidKey = expected.GuidKey,
            IntKey = expected.IntKey,
            LongKey = expected.LongKey,
            ShortKey = expected.ShortKey,
            ByteKey = expected.ByteKey,
            BooleanKey = expected.BooleanKey,
            DateTimeKey = expected.DateTimeKey,
            DateTimeOffsetKey = expected.DateTimeOffsetKey,
            DecimalKey = expected.DecimalKey,
            Name = "null-key",
        };
        context.CreateInsertBuilder<CanonicalShardRow>().Values(nullableStringRow).Insert().Should().Be(1);
        var nullKeys = (object?[])keys.Clone();
        nullKeys[0] = null;
        context.From<CanonicalShardRow>()
            .WithShardKey(nullKeys)
            .Where(row => row.Id == nullableStringRow.Id)
            .Select(row => row.Name)
            .ToList()
            .Should().Equal("null-key");

        FluentActions.Invoking(() => context.From<CanonicalShardRow>()
                .Where(row => row.GuidKey == expected.GuidKey || row.GuidKey == Guid.Empty)
                .Select(row => row.Name)
                .ToList())
            .Should().Throw<InvalidOperationException>().WithMessage("*shard*");
    }

    [Fact]
    public async Task BucketFanOut_ShouldRouteArraysListsAndProjectedAsyncStreams()
    {
        var topology = CreateTopology();
        using var context = CreateContext(topology);
        CreateTables(topology, nameof(ShardedRow),
            "Id INTEGER PRIMARY KEY, Tenant TEXT NOT NULL, Name TEXT NOT NULL");

        context.TryGetShardRouter(out var router).Should().BeTrue();
        router.Should().NotBeNull();
        var rows = FindOneRowPerBucket(router!);
        var groups = context.GroupByBucket(rows);
        groups.Select(group => group.Bucket).Should().Equal(Enumerable.Range(0, BucketCount));
        foreach (var row in groups.SelectMany(group => group.Rows))
            context.CreateInsertBuilder<ShardedRow>().Values(row).Insert().Should().Be(1);

        var source = context.From<ShardedRow>().AcrossBuckets(parallelism: 1);
        var syncArrays = source
            .Where(row => row.Id > 0)
            .OrderBy(row => row.Id)
            .Distinct()
            .ToArray();

        syncArrays.Should().HaveCount(BucketCount);
        for (var bucket = 0; bucket < BucketCount; bucket++)
            syncArrays[bucket].Select(row => row.Name).Should().Equal($"bucket-{bucket}");

        var asyncLists = await source.Where(row => row.Id > 0)
            .ToListAsync(TestContext.Current.CancellationToken);
        asyncLists.Should().HaveCount(BucketCount);
        for (var bucket = 0; bucket < BucketCount; bucket++)
            asyncLists[bucket].Select(row => row.Id).Should().Equal(bucket + 1);

        var entityArrays = await source.ToArrayAsync(TestContext.Current.CancellationToken);
        entityArrays.Should().HaveCount(BucketCount);
        for (var bucket = 0; bucket < BucketCount; bucket++)
            entityArrays[bucket].Select(row => row.Name).Should().Equal($"bucket-{bucket}");

        var unprojectedStream = new List<ShardedRow[]>();
        await foreach (var vector in source.AsAsyncEnumerable(TestContext.Current.CancellationToken))
            unprojectedStream.Add(vector);

        unprojectedStream.Should().HaveCount(BucketCount);
        for (var bucket = 0; bucket < BucketCount; bucket++)
            unprojectedStream[bucket].Select(row => row.Name).Should().Equal($"bucket-{bucket}");

        var projected = source.Select(row => row.Name).Where(row => row.Id > 0);
        var projectedLists = projected.ToList();
        projectedLists.Should().HaveCount(BucketCount);
        for (var bucket = 0; bucket < BucketCount; bucket++)
            projectedLists[bucket].Should().Equal($"bucket-{bucket}");

        var projectedArrays = projected.ToArray();
        projectedArrays.Should().HaveCount(BucketCount);
        for (var bucket = 0; bucket < BucketCount; bucket++)
            projectedArrays[bucket].Should().Equal($"bucket-{bucket}");

        var asyncArrays = await projected.ToArrayAsync(TestContext.Current.CancellationToken);
        asyncArrays.Should().HaveCount(BucketCount);
        for (var bucket = 0; bucket < BucketCount; bucket++)
            asyncArrays[bucket].Should().Equal($"bucket-{bucket}");

        var streamed = new List<string[]>();
        await foreach (var vector in projected.AsAsyncEnumerable(TestContext.Current.CancellationToken))
            streamed.Add(vector);

        streamed.Should().HaveCount(BucketCount);
        for (var bucket = 0; bucket < BucketCount; bucket++)
            streamed[bucket].Should().Equal($"bucket-{bucket}");

        projected.Count().Should().Equal(Enumerable.Repeat(1, BucketCount));
        projected.Any().Should().OnlyContain(static hasRow => hasRow);

        source.First().Select(row => row.Name).Should().Equal(
            Enumerable.Range(0, BucketCount).Select(static bucket => $"bucket-{bucket}"));
        source.Single().Select(row => row.Name).Should().Equal(
            Enumerable.Range(0, BucketCount).Select(static bucket => $"bucket-{bucket}"));
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private SqliteDataContext CreateContext(ShardTopology topology)
    {
        var context = new SqliteDataContext(new DataContextBuilder()
            .UseSharding(topology)
            .UseDataRouting(routing => routing.UseDefaultTopology("main", "Data Source=:memory:")));
        context.EnsureConnectionOpen();
        return context;
    }

    private ShardTopology CreateTopology()
    {
        var topology = new ShardTopology(BucketCount, BucketPlacementMode.TableSuffix);
        topology.AddShard("shard-a", ConnectionString("shard-a"));
        topology.AddShard("shard-b", ConnectionString("shard-b"));
        return topology;
    }

    private void CreateTables(ShardTopology topology, string tableName, string columns)
    {
        var router = new ShardRouter(topology);
        foreach (var shardId in topology.ShardIds)
        {
            var statements = Enumerable.Range(0, BucketCount)
                .Where(bucket => router.GetShard(bucket) == shardId)
                .Select(bucket => $"CREATE TABLE {tableName}_{bucket} ({columns})")
                .ToArray();
            if (statements.Length == 0)
                continue;

            using var context = new SqliteDataContext(topology.GetConnectionString(shardId), new DataContextBuilder());
            context.EnsureConnectionOpen();
            using var command = context.CreateCommand(string.Join("; ", statements));
            command.ExecuteNonQuery();
        }
    }

    private string ConnectionString(string shardId)
        => $"Data Source={Path.Combine(_directory, shardId + ".db")}";

    private static ShardedRow[] FindOneRowPerBucket(IShardRouter router)
    {
        var rows = new ShardedRow?[BucketCount];
        for (var index = 0; index < 100_000 && rows.Any(static row => row is null); index++)
        {
            var tenant = $"fanout-tenant-{index}";
            var row = new ShardedRow { Id = index + 1, Tenant = tenant, Name = string.Empty };
            var bucket = router.GetBucket(row);
            if (rows[bucket] is null)
            {
                rows[bucket] = new ShardedRow
                {
                    Id = bucket + 1,
                    Tenant = tenant,
                    Name = $"bucket-{bucket}",
                };
            }
        }

        return rows.Select(static row => row
            ?? throw new InvalidOperationException("Could not find a row for every shard bucket.")).ToArray();
    }

    private static long ReadCount(string connectionString, string tableName)
    {
        using var context = new SqliteDataContext(connectionString, new DataContextBuilder());
        context.EnsureConnectionOpen();
        using var command = context.CreateCommand($"SELECT COUNT(*) FROM {tableName}");
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }
}

[ShardKey(
    nameof(CanonicalShardRow.StringKey),
    nameof(CanonicalShardRow.GuidKey),
    nameof(CanonicalShardRow.IntKey),
    nameof(CanonicalShardRow.LongKey),
    nameof(CanonicalShardRow.ShortKey),
    nameof(CanonicalShardRow.ByteKey),
    nameof(CanonicalShardRow.BooleanKey),
    nameof(CanonicalShardRow.DateTimeKey),
    nameof(CanonicalShardRow.DateTimeOffsetKey),
    nameof(CanonicalShardRow.DecimalKey))]
internal sealed class CanonicalShardRow
{
    public int Id { get; set; }
    public string? StringKey { get; set; }
    public Guid GuidKey { get; set; }
    public int IntKey { get; set; }
    public long LongKey { get; set; }
    public short ShortKey { get; set; }
    public byte ByteKey { get; set; }
    public bool BooleanKey { get; set; }
    public DateTime DateTimeKey { get; set; }
    public DateTimeOffset DateTimeOffsetKey { get; set; }
    public decimal DecimalKey { get; set; }
    public string Name { get; set; } = string.Empty;
}
