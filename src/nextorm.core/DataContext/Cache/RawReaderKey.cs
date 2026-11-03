using System.Runtime.CompilerServices;

namespace NextORM.Core;

/// <summary>
/// Value-free, detached structural key for the <c>ToDataReader</c> reader-descriptor partition. It
/// captures every dimension that changes the rendered SQL or the parameter shape, but never a value,
/// a closure, a context instance beyond its type, a connection, a command or a reader. Equality is
/// structural: a hash collision is never treated as a match.
/// </summary>
internal readonly struct RawReaderKey : IEquatable<RawReaderKey>
{
    public int ShapeFingerprint { get; init; }

    public RawReaderFlavor Flavor { get; init; }

    public bool SequentialAccess { get; init; }

    public bool SuppressLobLocator { get; init; }

    public Type ContextType { get; init; } = typeof(void);

    public Type DialectType { get; init; } = typeof(void);

    public int ProviderConfigHash { get; init; }

    public int RenderingSettingsHash { get; init; }

    public int ParameterSignatureHash { get; init; }

    public int CollectionShapeDigest { get; init; }

    public bool IsKeyable { get; init; }

    public RawReaderKey()
    {
    }

    public static bool TryCreate(
        DataContext context,
        QueryCommand queryCommand,
        RawReaderFlavor flavor,
        bool sequentialAccess,
        bool suppressLobLocator,
        out RawReaderKey key)
    {
        key = default;

        if (context is null || queryCommand is null)
            return false;

        // Lazy temporary-table sources are not a single statement and must take the uncached route.
        if (queryCommand.HasTemporaryTableSource())
            return false;

        // Captured collections whose shape is not part of the key make the rendered SQL shape-dependent;
        // publishing such a form would risk a stale descriptor.
        if (queryCommand.HasUnkeyedTupleInValues || queryCommand.HasUnkeyedScalarInValues)
            return false;

        var shape = new HashCode();
        shape.Add(queryCommand.ColumnsPlanHash);
        shape.Add(queryCommand.JoinPlanHash);
        shape.Add(queryCommand.SortingPlanHash);
        shape.Add(queryCommand.WherePlanHash);
        shape.Add(queryCommand.GroupingPlanHash);
        shape.Add(queryCommand.ResultPlanHash);
        shape.Add(queryCommand.FromPlanHash);
        shape.Add(queryCommand.UnionPlanHash);
        shape.Add(queryCommand.ReferencedQueriesPlanHash);
        shape.Add(queryCommand.CtesPlanHash);
        shape.Add(queryCommand.HintsPlanHash);
        shape.Add(queryCommand.WindowsPlanHash);
        shape.Add(queryCommand.InValuesShapeHash);
        shape.Add(queryCommand.EntityType?.GetHashCode() ?? 0);
        shape.Add(queryCommand.ResultType?.GetHashCode() ?? 0);

        var rendering = new HashCode();
        rendering.Add(queryCommand.DefaultOnEmpty);
        rendering.Add(queryCommand.SingleScalar);
        rendering.Add(queryCommand.IdentitySlotAliases);
        rendering.Add(queryCommand.Paging.Limit);
        rendering.Add(queryCommand.Paging.Offset);
        rendering.Add((int)queryCommand.FilterScope.GetHashCode());
        rendering.Add(context.Dialect.GetType().GetHashCode());

        var parameters = new HashCode();
        parameters.Add(queryCommand.InValuesShapeHash);
        parameters.Add(queryCommand.ShapeValuesStable);
        parameters.Add(queryCommand.HasTopLevelInValues);

        key = new RawReaderKey
        {
            ShapeFingerprint = shape.ToHashCode(),
            Flavor = flavor,
            SequentialAccess = sequentialAccess,
            SuppressLobLocator = suppressLobLocator,
            ContextType = context.GetType(),
            DialectType = context.Dialect.GetType(),
            ProviderConfigHash = context.Dialect.GetHashCode(),
            RenderingSettingsHash = rendering.ToHashCode(),
            ParameterSignatureHash = parameters.ToHashCode(),
            CollectionShapeDigest = queryCommand.InValuesShapeHash,
            IsKeyable = true,
        };

        return true;
    }

    public bool Equals(RawReaderKey other)
    {
        return ShapeFingerprint == other.ShapeFingerprint
            && Flavor == other.Flavor
            && SequentialAccess == other.SequentialAccess
            && SuppressLobLocator == other.SuppressLobLocator
            && ContextType == other.ContextType
            && DialectType == other.DialectType
            && ProviderConfigHash == other.ProviderConfigHash
            && RenderingSettingsHash == other.RenderingSettingsHash
            && ParameterSignatureHash == other.ParameterSignatureHash
            && CollectionShapeDigest == other.CollectionShapeDigest
            && IsKeyable == other.IsKeyable;
    }

    public override bool Equals(object? obj) => obj is RawReaderKey other && Equals(other);

    public override int GetHashCode()
    {
        var hc = new HashCode();
        hc.Add(ShapeFingerprint);
        hc.Add(Flavor);
        hc.Add(SequentialAccess);
        hc.Add(SuppressLobLocator);
        hc.Add(ContextType);
        hc.Add(DialectType);
        hc.Add(ProviderConfigHash);
        hc.Add(RenderingSettingsHash);
        hc.Add(ParameterSignatureHash);
        hc.Add(CollectionShapeDigest);
        hc.Add(IsKeyable);
        return hc.ToHashCode();
    }
}
