using System.Reflection;

namespace NextORM.Core;

// A cache key must not retain SelectExpression or its authored expression graph. Snapshot only
// the descriptors used by reader accessors and row construction, in result-set order.
internal sealed class MapperColumnShape : IEquatable<MapperColumnShape>
{
    private static readonly MapperColumnShape Empty = new([]);
    private readonly Column[] _columns;
    private readonly int _hash;

    private MapperColumnShape(Column[] columns)
    {
        _columns = columns;
        var hash = new HashCode();
        foreach (var column in columns)
        {
            hash.Add(column);
        }

        _hash = hash.ToHashCode();
    }

    internal static MapperColumnShape Create(SelectExpression[]? selectList)
    {
        if (selectList is not { Length: > 0 })
        {
            return Empty;
        }

        var columns = new Column[selectList.Length];
        Dictionary<ProjectionEntityItem, int>? groups = null;
        for (var i = 0; i < selectList.Length; i++)
        {
            var column = selectList[i];
            ProjectionGroup? projection = null;
            if (column.ProjectionItem is { } item)
            {
                groups ??= new(ReferenceEqualityComparer.Instance);
                if (!groups.TryGetValue(item, out var firstColumn))
                {
                    groups.Add(item, firstColumn = i);
                }

                projection = new(firstColumn, item.Slot, item.EntityType, item.Member);
            }

            columns[i] = new(
                column.Index, column.PropertyName, column.PropertyType, column.Nullable,
                column.PropertyInfo, column.MaterializerMember, column.ReaderType, column.ProviderType,
                PlanObjectIdentity.For(column.Converter), column.DurationUnit, column.DurationPrecision,
                column.DefaultOnNull, column.IsWideCountNarrowed, column.IsLobStreaming,
                column.IsDynamicColumnsStore, column.PhysicalColumnName, column.RangeColumnRole,
                column.RangeColumns is { } range ? (range.LowerInclusive, range.UpperInclusive) : null,
                projection);
        }

        return new(columns);
    }

    public bool Equals(MapperColumnShape? other)
        => ReferenceEquals(this, other)
            || (other is not null && _hash == other._hash && _columns.AsSpan().SequenceEqual(other._columns));

    public override bool Equals(object? obj) => obj is MapperColumnShape other && Equals(other);

    public override int GetHashCode() => _hash;

    private readonly record struct Column(
        int Index, string? Name, Type Type, bool Nullable, PropertyInfo? Property, PropertyInfo? MaterializerMember,
        Type? ReaderType, Type? ProviderType, object? ConverterIdentity, DurationUnit? DurationUnit,
        int DurationPrecision, bool DefaultOnNull, bool WideCount, bool Streaming, bool DynamicColumns,
        string? PhysicalColumnName, RangeColumnRole RangeRole, (bool LowerInclusive, bool UpperInclusive)? RangeBounds,
        ProjectionGroup? Projection);

    private readonly record struct ProjectionGroup(int FirstColumn, int Slot, Type EntityType, PropertyInfo? Member);
}
