using FluentAssertions;
using NextORM.Core;

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
        router.ResolveShard(typeof(UnshardedRow), Array.Empty<object?>()).Should().Be(global::NextORM.Core.MappingScope.Default);
        router.Resolve(typeof(UnshardedRow)).Should().Be(global::NextORM.Core.MappingScope.Default);
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
        builder.EntityScopeResolver.Should().BeOfType<ShardRouter>();
        builder.EntityMetadataProvider.GetColumnName(
            typeof(UnshardedRow).GetProperty(nameof(UnshardedRow.Id))!, global::NextORM.Core.MappingScope.Default, null)
            .Should().Be("custom_Id");

        var resolver = new CustomDefaultResolver();
        var conflictingBuilder = new DataContextBuilder().UseScopeResolver(resolver);
        FluentActions.Invoking(() => conflictingBuilder.UseSharding(topology))
            .Should().Throw<InvalidOperationException>();
        conflictingBuilder.EntityScopeResolver.Should().BeSameAs(resolver);
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

        public IEntityMetadata GetMetadata(Type entityType, in global::NextORM.Core.MappingScope scope)
            => _inner.GetMetadata(entityType, scope);

        public string GetColumnName(System.Reflection.PropertyInfo property, in global::NextORM.Core.MappingScope scope, INamingConvention? convention)
            => $"custom_{property.Name}";
    }

    private sealed class CustomDefaultResolver : IEntityScopeResolver
    {
        public global::NextORM.Core.MappingScope Resolve(Type entityType) => global::NextORM.Core.MappingScope.Default;
        public global::NextORM.Core.MappingScope Resolve(Type entityType, object entity) => global::NextORM.Core.MappingScope.Default;
        public global::NextORM.Core.MappingScope Resolve(Type entityType, IReadOnlyList<object?> keyValues) => global::NextORM.Core.MappingScope.Default;
    }
}
