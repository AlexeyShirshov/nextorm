using System.Linq.Expressions;
using NextORM.Core;

namespace NextORM.SqlServer;

/// <summary>
/// SQL Server-only fluent members of <c>EntityBuilder&lt;TEntity&gt;</c>, removed from the common
/// <c>nextorm</c> package and re-exposed here as extension methods. Add
/// <c>using NextORM.SqlServer;</c> to keep the fluent chain unchanged on a SQL Server provider.
/// The render-time dialect gating (and the thrown exceptions) is unchanged.
/// </summary>
public static class SqlServerEntityBuilderExtensions
{
    /// <summary>
    /// Reshapes this query's source into columns with the native <c>PIVOT</c> operator: for every
    /// distinct <paramref name="forColumn"/> value in <paramref name="values"/> a result column is
    /// produced from <paramref name="aggregate"/> of <paramref name="aggregateColumn"/>. The result is
    /// an untyped source (<see cref="TableAlias"/>): select the grouping columns and the pivoted columns
    /// by name. Each result column is named by its pivot value, so quote non-identifier values in the
    /// projection (<c>Q1 = t.GetNullableDecimal("[1]")</c>).
    /// Requires a dialect that supports it (see <c>ISqlDialect.Pivot</c>). The source may be a plain
    /// table/entity, a table-valued function or a derived query (<c>From(query)</c>); filters and other
    /// modifiers belong to the reshaped result (or, for a derived source, inside the derived query).
    /// </summary>
    /// <param name="builder">The query builder to extend.</param>
    /// <param name="aggregate">The aggregate applied to each cell (<c>SUM</c>/<c>COUNT</c>/<c>AVG</c>/<c>MIN</c>/<c>MAX</c>).</param>
    /// <param name="aggregateColumn">The column expression aggregated into each cell.</param>
    /// <param name="forColumn">The column whose values become the result columns.</param>
    /// <param name="values">The pivot values; each value names its result column.</param>
    public static EntityBuilder<TableAlias> Pivot<TEntity>(
        this EntityBuilder<TEntity> builder,
        PivotAggregate aggregate,
        Expression<Func<TEntity, object?>> aggregateColumn,
        Expression<Func<TEntity, object?>> forColumn,
        params PivotValue[] values)
    {
        ArgumentNullException.ThrowIfNull(aggregateColumn);
        ArgumentNullException.ThrowIfNull(forColumn);
        ArgumentNullException.ThrowIfNull(values);
        if (values.Length == 0)
            throw new ArgumentException("A PIVOT requires at least one value.", nameof(values));
        builder.EnsureNoEagerLoadState(nameof(Pivot));

        var spec = PivotExpression.ForPivot(ResolvePivotInner(builder), typeof(TEntity), aggregate, aggregateColumn, forColumn, values);
        return new EntityBuilder<TableAlias>(builder.DataProvider) { Logger = builder.Logger, SourceFrom = new FromExpression(spec) };
    }

    /// <summary>
    /// Reshapes this query's source with the native <c>UNPIVOT</c> operator: the listed columns are
    /// stacked into two result columns (<paramref name="valueColumnName"/> holding the value and
    /// <paramref name="nameColumnName"/> holding the originating column name). Requires a dialect that
    /// supports it (see <c>ISqlDialect.Pivot</c>). The source may be a plain table/entity or a derived
    /// query (<c>From(query)</c>).
    /// </summary>
    /// <param name="builder">The query builder to extend.</param>
    /// <param name="valueColumnName">The name of the output column that receives the values.</param>
    /// <param name="nameColumnName">The name of the output column that receives the source column name.</param>
    /// <param name="columns">The source columns to unpivot; each column's name becomes its name-column value.</param>
    public static EntityBuilder<TableAlias> Unpivot<TEntity>(
        this EntityBuilder<TEntity> builder,
        string valueColumnName,
        string nameColumnName,
        params UnpivotColumn[] columns)
    {
        ArgumentException.ThrowIfNullOrEmpty(valueColumnName);
        ArgumentException.ThrowIfNullOrEmpty(nameColumnName);
        ArgumentNullException.ThrowIfNull(columns);
        if (columns.Length == 0)
            throw new ArgumentException("An UNPIVOT requires at least one column.", nameof(columns));
        builder.EnsureNoEagerLoadState(nameof(Unpivot));

        var spec = PivotExpression.ForUnpivot(ResolvePivotInner(builder), typeof(TEntity), valueColumnName, nameColumnName, columns);
        return new EntityBuilder<TableAlias>(builder.DataProvider) { Logger = builder.Logger, SourceFrom = new FromExpression(spec) };
    }

    /// <summary>
    /// Resolves the source a <c>PIVOT</c>/<c>UNPIVOT</c> wraps. A plain table/entity mapping, a
    /// table-valued function and a derived query (<c>From(query)</c>) are allowed; the native operators
    /// apply to a table expression, so any filter, join, grouping, ordering, paging or table modifier on
    /// this builder must be applied to the reshaped result (or, for a derived source, moved into the
    /// derived query).
    /// </summary>
    private static FromExpression ResolvePivotInner<TEntity>(EntityBuilder<TEntity> builder)
    {
        if (builder.Joins is { Count: > 0 } || builder.Condition is not null || builder.HavingCondition is not null
            || builder.GroupByExpression is not null || builder.Sorting is { Count: > 0 } || !builder.Paging.IsEmpty || builder.IsDistinct
            || builder.TableSampleClause is not null || builder.TemporalClause is not null || builder.RowLockClause is not null || builder.PreWhereCondition is not null
            || builder.ArrayJoins is { Count: > 0 } || builder.SettingsList is { Count: > 0 } || builder.LimitByClause is not null
            || builder.DistinctOnClause is not null || builder.ExtremeRowClause is not null || builder.Windows is { Count: > 0 } || builder.IsFinal || builder.SampleRatio is not null
            || builder.TableHints is { Count: > 0 } || builder.IndexHints is not null || builder.Ctes is { Count: > 0 })
            throw new NotSupportedException(builder.Query is not null
                ? "PIVOT/UNPIVOT over a derived query accepts no modifiers on the pivot builder; apply filters, joins, grouping, ordering, paging and table modifiers inside the derived query or to the reshaped result."
                : "PIVOT/UNPIVOT can only be applied to a plain table/entity source, a table-valued function or a derived query; apply filters, joins, grouping, ordering, paging and table modifiers to the reshaped result.");

        if (builder.Query is not null)
            return new FromExpression(builder.Query);

        if (builder.SourceFrom is not null)
            return builder.SourceFrom;

        if (!string.IsNullOrEmpty(builder.Table))
            return new FromExpression(builder.Table);

        return builder.DataProvider.GetFrom(typeof(TEntity), null)
            ?? throw new BuildSqlCommandException($"A PIVOT/UNPIVOT source could not be resolved for type {typeof(TEntity)}.");
    }
}
