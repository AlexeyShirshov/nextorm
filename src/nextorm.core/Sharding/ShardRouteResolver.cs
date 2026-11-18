using System.Globalization;

namespace NextORM.Core;

/// <summary>
/// Routes to the sharding topology every entity type that declares a shard key, and every command whose
/// mapping variant already carries a bucket number (an explicit <c>WithVariant</c> or a bucket fan-out, which
/// select buckets without needing a key on the entity). The topology itself then turns the bucket scope
/// into the owning shard's host.
/// </summary>
internal sealed class ShardRouteResolver(IShardRouter router, string topologyName) : IEntityRouteResolver
{
    private readonly IShardRouter _router = router;
    private readonly string _topologyName = topologyName;

    /// <inheritdoc />
    public bool TryResolve(Type entityType, in MappingVariant variant, out RouteTarget target)
    {
        if ((entityType is not null && _router.IsSharded(entityType)) || IsBucketScope(in variant))
        {
            target = new RouteTarget(_topologyName);
            return true;
        }

        target = default;
        return false;
    }

    private bool IsBucketScope(in MappingVariant variant)
    {
        if (variant == MappingVariant.Default || string.IsNullOrEmpty(variant.Key))
            return false;

        return int.TryParse(variant.Key, NumberStyles.None, CultureInfo.InvariantCulture, out var bucket)
            && (uint)bucket < (uint)_router.BucketCount;
    }
}
