using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Builds the projection (<see cref="SelectExpression"/> list) of a mapped entity type from its
/// metadata. Extracted from the query preparer so the same derivation serves LINQ queries (which fold
/// the plan hash in afterwards) and raw command mapping (which reorders the columns to the reader's
/// ordinals). Keeping one implementation avoids the two paths drifting apart.
/// </summary>
internal static class EntitySelectListBuilder
{
    /// <summary>
    /// Builds one <see cref="SelectExpression"/> per mapped property (two for a
    /// <see cref="Range{T}"/> pair), in declaration order. The caller owns plan-hash computation and
    /// caching; the returned columns carry only the shape metadata.
    /// </summary>
    /// <param name="srcType">The entity type whose properties are projected.</param>
    /// <param name="entityMeta">The resolved metadata of <paramref name="srcType"/>.</param>
    /// <param name="cancellationToken">Stops the loop early when preparation is cancelled.</param>
    /// <returns>The built columns and whether the loop ran to completion; a cancelled build must not be cached by the caller.</returns>
    internal static (SelectExpression[] Columns, bool Completed) Build(Type srcType, IEntityMetadata entityMeta, CancellationToken cancellationToken)
    {
        var p = Expression.Parameter(srcType);
        var props = entityMeta.Properties;
        var columns = new List<SelectExpression>(props.Count);

        for (var idx = 0; idx < props.Count; idx++)
        {
            if (cancellationToken.IsCancellationRequested)
                return (columns.ToArray(), false);

            var prop = props[idx];
            var pi = prop.PropertyInfo;
            var exp = Expression.Lambda(Expression.Property(p, pi), p);

            if (prop.RangeColumns is { } rangeColumns)
            {
                var boundType = Nullable.GetUnderlyingType(pi.PropertyType) ?? pi.PropertyType;
                var nullableBound = typeof(Nullable<>).MakeGenericType(boundType.GetGenericArguments()[0]);

                columns.Add(new SelectExpression(nullableBound)
                {
                    Index = columns.Count,
                    PropertyName = rangeColumns.LowerColumn,
                    Expression = exp,
                    PropertyInfo = pi,
                    RangeColumnRole = RangeColumnRole.Lower,
                    RangeColumns = rangeColumns,
                });
                columns.Add(new SelectExpression(nullableBound)
                {
                    Index = columns.Count,
                    PropertyName = rangeColumns.UpperColumn,
                    Expression = exp,
                    PropertyInfo = pi,
                    RangeColumnRole = RangeColumnRole.Upper,
                    RangeColumns = rangeColumns,
                });
                continue;
            }

            columns.Add(new SelectExpression(pi.PropertyType)
            {
                Index = columns.Count,
                PropertyName = pi.Name,
                Expression = exp,
                PropertyInfo = pi,
                DurationUnit = prop.DurationUnit,
                DurationPrecision = prop.DurationPrecision,
                ProviderType = prop.Converter?.ProviderType,
                Converter = prop.Converter,
            });
        }

        return (columns.ToArray(), true);
    }
}
