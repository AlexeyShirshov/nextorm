using System.Collections.Concurrent;
using System.Reflection;

namespace NextORM.Core;

/// <summary>
/// Helpers that expand a <see cref="Range{T}"/> property stored as a column pair into its two scalar
/// bound values and back, used by the INSERT/UPDATE builders.
/// </summary>
internal static class RangeColumnPairs
{
    private static readonly ConcurrentDictionary<Type, (PropertyInfo Lower, PropertyInfo Upper, PropertyInfo IsEmpty)> BoundAccessors = new();

    internal static IPropertyMetadata Lower(IPropertyMetadata property) => Component(property, property.RangeColumns!.LowerColumn);

    internal static IPropertyMetadata Upper(IPropertyMetadata property) => Component(property, property.RangeColumns!.UpperColumn);

    internal static (object? Lower, object? Upper) Extract(IPropertyMetadata property, object? range)
    {
        if (range is null)
            return (null, null);

        var accessors = BoundAccessors.GetOrAdd(range.GetType(), static type => (
            type.GetProperty(nameof(Range<int>.Lower))!,
            type.GetProperty(nameof(Range<int>.Upper))!,
            type.GetProperty(nameof(Range<int>.IsEmpty))!));

        if ((bool)accessors.IsEmpty.GetValue(range)!)
            throw new NotSupportedException($"The empty range cannot be stored in the column pair '{property.RangeColumns!.LowerColumn}'/'{property.RangeColumns.UpperColumn}'.");

        return (accessors.Lower.GetValue(range), accessors.Upper.GetValue(range));
    }

    internal static NotSupportedException NotWritableBySelector(IPropertyMetadata property, string entityForm)
        => new(
            $"Property '{property.PropertyInfo.Name}' is mapped as a Range<T> column pair and cannot be written by a single-column selector. Use {entityForm}, which expands the pair into both bound columns.");

    internal static NotSupportedException NotSingleColumn(IPropertyMetadata property)
        => new(
            $"Property '{property.PropertyInfo.Name}' is mapped as a Range<T> column pair and cannot be addressed as a single column.");

    internal static NotSupportedException NotReturnable(IPropertyMetadata property)
        => new(
            $"Property '{property.PropertyInfo.Name}' is mapped as a Range<T> column pair and cannot be returned through RETURNING/OUTPUT as a single column.");

    private static IPropertyMetadata Component(IPropertyMetadata property, string column)
        => new PropertyMetadata
        {
            ColumnName = column,
            PropertyInfo = property.PropertyInfo,
            IsColumnNameAuto = false,
        };
}
