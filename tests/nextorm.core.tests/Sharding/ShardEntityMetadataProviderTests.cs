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
        var provider = new ShardEntityMetadataProvider(inner, CreateTopology());
        var scope = global::NextORM.Core.MappingVariant.Default;
        var expected = inner.GetMetadata(typeof(Orders), scope);

        provider.GetMetadata(typeof(Orders), scope).Should().BeSameAs(expected);
    }

    [Fact]
    public void DefaultPlacement_ShouldBeSchemaPerBucket()
    {
        var provider = new ShardEntityMetadataProvider(new DefaultEntityMetadataProvider(), CreateTopology());

        var metadata = provider.GetMetadata(typeof(Orders), BucketScope(7));

        metadata.Schema.Should().Be("bucket_7");
        metadata.TableName.Should().Be("Orders");
    }

    [Fact]
    public void TableSuffixPlacement_ShouldDeriveTheTableFromTheBucket()
    {
        var topology = new ShardTopology(4096, BucketPlacementMode.TableSuffix);
        topology.AddShard("east", "Server=east");
        var provider = new ShardEntityMetadataProvider(new DefaultEntityMetadataProvider(), topology);

        var metadata = provider.GetMetadata(typeof(Orders), BucketScope(7));

        metadata.TableName.Should().Be("Orders_7");
        metadata.Schema.Should().BeNull();
    }

    [Fact]
    public void SchemaPerBucketPlacement_ShouldKeepTheMappedSchemaAsTheBase()
    {
        var topology = new ShardTopology(4096, BucketPlacementMode.SchemaPerBucket);
        topology.AddShard("east", "Server=east");
        var provider = new ShardEntityMetadataProvider(new SchemaProvider(), topology);

        var metadata = provider.GetMetadata(typeof(Orders), BucketScope(7));

        metadata.Schema.Should().Be("sales_7");
        metadata.TableName.Should().Be("Orders");
    }

    [Fact]
    public void SchemaPerBucketPlacement_ShouldDeriveTheSchemaFromTheBucket()
    {
        var topology = new ShardTopology(4096, BucketPlacementMode.SchemaPerBucket);
        topology.AddShard("east", "Server=east");
        var provider = new ShardEntityMetadataProvider(new DefaultEntityMetadataProvider(), topology);

        var metadata = provider.GetMetadata(typeof(Orders), BucketScope(7));

        metadata.Schema.Should().Be("bucket_7");
        metadata.TableName.Should().Be("Orders");
    }

    [Fact]
    public void Placement_ShouldNotDependOnTheShardSet()
    {
        // The physical name is a function of the bucket only: adding or removing shards must not rename data.
        var provider = new ShardEntityMetadataProvider(new DefaultEntityMetadataProvider(), CreateTopology());
        var before = provider.GetMetadata(typeof(Orders), BucketScope(7));

        var topology = CreateTopology();
        topology.AddShard("north", "Server=north");
        var after = new ShardEntityMetadataProvider(new DefaultEntityMetadataProvider(), topology)
            .GetMetadata(typeof(Orders), BucketScope(7));

        after.TableName.Should().Be(before.TableName);
        after.Schema.Should().Be(before.Schema);
    }

    [Fact]
    public void ScopeKey_ShouldRejectValuesThatAreNotBuckets()
    {
        var provider = new ShardEntityMetadataProvider(new DefaultEntityMetadataProvider(), CreateTopology());

        FluentActions.Invoking(() => provider.GetMetadata(typeof(Orders), new global::NextORM.Core.MappingVariant("east")))
            .Should().Throw<InvalidOperationException>().WithMessage("*not a bucket number*");
        FluentActions.Invoking(() => provider.GetMetadata(typeof(Orders), BucketScope(4096)))
            .Should().Throw<InvalidOperationException>().WithMessage("*outside*");
    }

    [Fact]
    public void ColumnName_ShouldDelegateToWrappedProvider()
    {
        var inner = new ColumnMarkerProvider();
        var provider = new ShardEntityMetadataProvider(inner, CreateTopology());
        var property = typeof(Orders).GetProperty(nameof(Orders.Id))!;
        var scope = BucketScope(3);

        provider.GetColumnName(property, scope, null).Should().Be("custom_Id");
        inner.LastScope.Should().Be(scope);
    }

    [Fact]
    public void Topology_ShouldStoreServerConnectionsAndRejectInvalidChanges()
    {
        var topology = CreateTopology();

        var versionBefore = topology.Version;
        topology.GetConnectionString("east").Should().Be("Server=east");

        // A shard id never reaches a SQL identifier, so non-identifier characters are allowed.
        topology.AddShard("eu-west-1", "Server=eu;Database=app");
        topology.Version.Should().Be(versionBefore + 1);
        topology.ShardIds.Should().Equal("east", "eu-west-1", "west");
        topology.GetConnectionString("eu-west-1").Should().Be("Server=eu;Database=app");

        FluentActions.Invoking(() => topology.AddShard("east", "Server=other"))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => topology.AddShard("blank", ""))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => topology.AddShard("", "Server=blank"))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => topology.GetConnectionString("missing"))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => topology.RemoveShard("missing"))
            .Should().Throw<ArgumentException>();
    }

    private static global::NextORM.Core.MappingVariant BucketScope(int bucket)
        => new(bucket.ToString(System.Globalization.CultureInfo.InvariantCulture));

    private static ShardTopology CreateTopology()
    {
        var topology = new ShardTopology(4096);
        topology.AddShard("east", "Server=east");
        topology.AddShard("west", "Server=west");
        return topology;
    }

    private sealed class Orders
    {
        public int Id { get; set; }
    }

    /// <summary>Reports the entity as mapped to the <c>sales</c> schema.</summary>
    private sealed class SchemaProvider : IEntityMetadataProvider
    {
        private readonly DefaultEntityMetadataProvider _inner = new();

        public IEntityMetadata GetMetadata(Type entityType, in global::NextORM.Core.MappingVariant variant)
        {
            var metadata = _inner.GetMetadata(entityType, variant);
            return new SchemaMetadata(metadata);
        }

        public string GetColumnName(PropertyInfo property, in global::NextORM.Core.MappingVariant variant, INamingConvention? convention)
            => _inner.GetColumnName(property, variant, convention);

        private sealed class SchemaMetadata(IEntityMetadata inner) : IEntityMetadata
        {
            public IReadOnlyList<IPropertyMetadata> Properties => inner.Properties;
            public string? TableName => inner.TableName;
            public string? Schema => "sales";
            public bool IsTableNameAuto => inner.IsTableNameAuto;
            public IPropertyMetadata? DynamicColumnsStore => inner.DynamicColumnsStore;
            public IReadOnlyList<IQueryFilterMetadata> Filters => inner.Filters;
            public IReadOnlyList<IRelationshipMetadata> Relationships => inner.Relationships;
        }
    }

    private sealed class ColumnMarkerProvider : IEntityMetadataProvider
    {
        private readonly DefaultEntityMetadataProvider _inner = new();
        public global::NextORM.Core.MappingVariant LastScope { get; private set; }

        public IEntityMetadata GetMetadata(Type entityType, in global::NextORM.Core.MappingVariant variant)
            => _inner.GetMetadata(entityType, variant);

        public string GetColumnName(PropertyInfo property, in global::NextORM.Core.MappingVariant variant, INamingConvention? convention)
        {
            LastScope = variant;
            return $"custom_{property.Name}";
        }
    }
}
