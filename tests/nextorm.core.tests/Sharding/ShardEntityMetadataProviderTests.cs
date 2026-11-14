using System.Reflection;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests.Sharding;

public sealed class ShardEntityMetadataProviderTests
{
    [Fact]
    public void DefaultScope_ShouldDelegateMetadataUnchanged()
    {
        var inner = new DefaultEntityMetadataProvider();
        var provider = new ShardEntityMetadataProvider(inner, CreateTopology("east"));
        var scope = global::NextORM.Core.MappingScope.Default;
        var expected = inner.GetMetadata(typeof(Orders), scope);

        provider.GetMetadata(typeof(Orders), scope).Should().BeSameAs(expected);
    }

    [Fact]
    public void ScopedPlacement_ShouldApplyConfiguredSchemaAndTable()
    {
        var topology = new ShardTopology();
        topology.AddShard("east", "sales", "orders_east");
        var provider = new ShardEntityMetadataProvider(new DefaultEntityMetadataProvider(), topology);
        var metadata = provider.GetMetadata(typeof(Orders), new global::NextORM.Core.MappingScope("east"));

        metadata.Schema.Should().Be("sales");
        metadata.TableName.Should().Be("orders_east");
    }

    [Fact]
    public void NullPlacementParts_ShouldFallBackToInnerMetadataAndShardSuffix()
    {
        var inner = new DefaultEntityMetadataProvider();
        var topology = CreateTopology("east");
        var provider = new ShardEntityMetadataProvider(inner, topology);
        var defaultScope = global::NextORM.Core.MappingScope.Default;
        var baseMetadata = inner.GetMetadata(typeof(Orders), defaultScope);

        var metadata = provider.GetMetadata(typeof(Orders), new global::NextORM.Core.MappingScope("east"));

        metadata.Schema.Should().Be(baseMetadata.Schema);
        metadata.TableName.Should().Be($"{baseMetadata.TableName}_east");
    }

    [Fact]
    public void ColumnName_ShouldDelegateToWrappedProvider()
    {
        var inner = new ColumnMarkerProvider();
        var provider = new ShardEntityMetadataProvider(inner, CreateTopology("east"));
        var property = typeof(Orders).GetProperty(nameof(Orders.Id))!;
        var scope = new global::NextORM.Core.MappingScope("east");

        provider.GetColumnName(property, scope, null).Should().Be("custom_Id");
        inner.LastScope.Should().Be(scope);
    }

    [Fact]
    public void Topology_ShouldValidateSqlSafeShardIdentifiersAndVersionChanges()
    {
        var topology = new ShardTopology();

        topology.Version.Should().Be(0);
        FluentActions.Invoking(() => topology.AddShard("bad-name"))
            .Should().Throw<ArgumentException>();
        topology.AddShard("valid_1");
        topology.Version.Should().Be(1);
        topology.ShardIds.Should().Equal("valid_1");
        topology.GetPlacement(typeof(Orders), "valid_1").Should().Be(new ShardPlacement(null, null));
        FluentActions.Invoking(() => topology.RemoveShard("valid_1"))
            .Should().Throw<ArgumentException>();
    }

    private static ShardTopology CreateTopology(string shardId)
    {
        var topology = new ShardTopology();
        topology.AddShard(shardId);
        return topology;
    }

    private sealed class Orders
    {
        public int Id { get; set; }
    }

    private sealed class ColumnMarkerProvider : IEntityMetadataProvider
    {
        private readonly DefaultEntityMetadataProvider _inner = new();
        public global::NextORM.Core.MappingScope LastScope { get; private set; }

        public IEntityMetadata GetMetadata(Type entityType, in global::NextORM.Core.MappingScope scope)
            => _inner.GetMetadata(entityType, scope);

        public string GetColumnName(PropertyInfo property, in global::NextORM.Core.MappingScope scope, INamingConvention? convention)
        {
            LastScope = scope;
            return $"custom_{property.Name}";
        }
    }
}
