using FluentAssertions;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Core.Tests.Sharding;

public sealed class ShardRouterTests
{
    [Fact]
    public void InstanceAndExplicitValues_ShouldResolveToTheSameScope()
    {
        var topology = CreateTopology();
        var router = new ShardRouter(topology);
        var row = new ShardedRow { Tenant = "tenant-1", Id = 42 };
        var instanceScope = router.Resolve(typeof(ShardedRow), row);
        var explicitScope = router.ResolveShard(typeof(ShardedRow), [row.Tenant, row.Id]);

        instanceScope.Should().Be(explicitScope);
        var bucket = int.Parse(instanceScope.Key!, System.Globalization.CultureInfo.InvariantCulture);
        bucket.Should().BeInRange(0, topology.Ring.BucketCount - 1);
        router.GetShard(bucket).Should().BeOneOf("east", "west");
    }

    [Fact]
    public void ShardedEntityWithoutKeyValues_ShouldFailFast()
    {
        var router = new ShardRouter(CreateTopology());

        FluentActions.Invoking(() => router.ResolveShard(typeof(ShardedRow), Array.Empty<object?>()))
            .Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => router.ResolveShard(typeof(ShardedRow), ["tenant-only"]))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void NonShardedEntity_ShouldResolveToDefault()
    {
        var router = new ShardRouter(CreateTopology());

        router.IsSharded(typeof(UnshardedRow)).Should().BeFalse();
        router.ResolveShard(typeof(UnshardedRow), Array.Empty<object?>()).Should().Be(global::NextORM.Core.MappingVariant.Default);
        router.Resolve(typeof(UnshardedRow)).Should().Be(global::NextORM.Core.MappingVariant.Default);
    }

    [Fact]
    public void AttributeProvider_ShouldCachePropertiesAndRejectMissingProperty()
    {
        var provider = new AttributeShardKeyProvider();
        var first = provider.GetShardKey(typeof(ShardedRow));

        provider.GetShardKey(typeof(ShardedRow)).Should().BeSameAs(first);
        FluentActions.Invoking(() => provider.GetShardKey(typeof(MissingKeyRow)))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void UseSharding_ShouldPreserveUserMetadataProviderAndRejectCustomResolver()
    {
        var topology = CreateTopology();
        var metadata = new MarkerMetadataProvider();
        var builder = new DataContextBuilder().UseEntityMetadataProvider(metadata);

        builder.UseSharding(topology);
        builder.EntityVariantResolver.Should().BeOfType<ShardRouter>();
        builder.EntityMetadataProvider.GetColumnName(
            typeof(UnshardedRow).GetProperty(nameof(UnshardedRow.Id))!, global::NextORM.Core.MappingVariant.Default, null)
            .Should().Be("custom_Id");

        var resolver = new CustomDefaultResolver();
        var conflictingBuilder = new DataContextBuilder().UseVariantResolver(resolver);
        FluentActions.Invoking(() => conflictingBuilder.UseSharding(topology))
            .Should().Throw<InvalidOperationException>();
        conflictingBuilder.EntityVariantResolver.Should().BeSameAs(resolver);
    }

    [Fact]
    public void Context_ShouldExposeTheRouterItRoutesWith()
    {
        var builder = new DataContextBuilder()
            .UseSharding(CreateTopology())
            .UseDataRouting(routing => routing.UseDefaultTopology("main", "Data Source=:memory:"));
        using var context = new SqliteDataContext(builder);

        context.TryGetShardRouter(out var router).Should().BeTrue();
        router.Should().BeSameAs(builder.EntityVariantResolver);
        context.GetShardRouter().Should().BeSameAs(router);
    }

    [Fact]
    public void ContextWithoutSharding_ShouldExposeNoRouter()
    {
        using var context = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());

        context.TryGetShardRouter(out _).Should().BeFalse();
        FluentActions.Invoking(() => context.GetShardRouter())
            .Should().Throw<InvalidOperationException>().WithMessage("*UseSharding*");
    }

    [Fact]
    public void GetBucketFromTheContext_ShouldMatchTheSameRowQuery()
    {
        var builder = new DataContextBuilder()
            .UseSharding(CreateTopology())
            .UseDataRouting(routing => routing.UseDefaultTopology("main", "Data Source=:memory:"));
        using var context = new SqliteDataContext(builder);
        var router = context.GetShardRouter();
        var row = new ShardedRow { Tenant = "tenant-1", Id = 42 };

        var bucket = router.GetBucket(row);
        var scope = router.ResolveShard(typeof(ShardedRow), [row.Tenant, row.Id]);

        bucket.ToString(System.Globalization.CultureInfo.InvariantCulture).Should().Be(scope.Key);
        router.GetBucket(typeof(ShardedRow), [row.Tenant, row.Id]).Should().Be(bucket);
        router.GetScope(bucket).Should().Be(scope);
        router.Resolve(typeof(ShardedRow), row).Should().Be(scope);
        router.GetShard(bucket).Should().BeOneOf("east", "west");
    }

    [Fact]
    public void GetBucket_ForAnUnshardedEntity_ShouldFailFast()
    {
        using var context = new SqliteDataContext(
            new DataContextBuilder()
                .UseSharding(CreateTopology())
                .UseDataRouting(routing => routing.UseDefaultTopology("main", "Data Source=:memory:")));
        var router = context.GetShardRouter();

        FluentActions.Invoking(() => router.GetBucket(new UnshardedRow()))
            .Should().Throw<InvalidOperationException>().WithMessage("*does not declare a shard key*");
        FluentActions.Invoking(() => router.GetScope(router.BucketCount))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    private static ShardTopology CreateTopology()
    {
        var topology = new ShardTopology(4096);
        topology.AddShard("east", "Server=east");
        topology.AddShard("west", "Server=west");
        return topology;
    }

    [ShardKey(nameof(ShardedRow.Tenant), nameof(ShardedRow.Id))]
    private sealed class ShardedRow
    {
        public string Tenant { get; set; } = string.Empty;
        public int Id { get; set; }
    }

    [ShardKey("Missing")]
    private sealed class MissingKeyRow
    {
    }

    private sealed class UnshardedRow
    {
        public int Id { get; set; }
    }

    private sealed class MarkerMetadataProvider : IEntityMetadataProvider
    {
        private readonly DefaultEntityMetadataProvider _inner = new();

        public IEntityMetadata GetMetadata(Type entityType, in global::NextORM.Core.MappingVariant variant)
            => _inner.GetMetadata(entityType, variant);

        public string GetColumnName(System.Reflection.PropertyInfo property, in global::NextORM.Core.MappingVariant variant, INamingConvention? convention)
            => $"custom_{property.Name}";
    }

    private sealed class CustomDefaultResolver : IEntityVariantResolver
    {
        public global::NextORM.Core.MappingVariant Resolve(Type entityType) => global::NextORM.Core.MappingVariant.Default;
        public global::NextORM.Core.MappingVariant Resolve(Type entityType, object entity) => global::NextORM.Core.MappingVariant.Default;
        public global::NextORM.Core.MappingVariant Resolve(Type entityType, IReadOnlyList<object?> keyValues) => global::NextORM.Core.MappingVariant.Default;
    }
}
