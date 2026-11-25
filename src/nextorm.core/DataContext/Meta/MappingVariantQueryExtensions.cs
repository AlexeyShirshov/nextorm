namespace NextORM.Core;

/// <summary>Starts queries scoped to an explicit runtime mapping variant.</summary>
public static class MappingVariantQueryExtensions
{
    /// <summary>Starts a query over the mapping of <typeparamref name="T"/> for the supplied variant.</summary>
    /// <typeparam name="T">The mapped entity type.</typeparam>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="variant">The mapping variant for this query. <see cref="MappingVariant.Default"/> keeps normal variant resolution.</param>
    /// <returns>A builder for composing the query.</returns>
    public static EntityBuilder<T> From<T>(this IDataContext dataContext, MappingVariant variant)
    {
        var builder = dataContext.From<T>();
        builder.MappingVariant = variant;
        return builder;
    }

    /// <inheritdoc cref="From{T}(IDataContext, MappingVariant)"/>
    public static EntityBuilder<T> CreateQueryBuilder<T>(this IDataContext dataContext, MappingVariant variant)
        => dataContext.From<T>(variant);

    /// <summary>Starts a query over a CTE with a mapping variant for the outer query.</summary>
    /// <typeparam name="TResult">The CTE's projection shape.</typeparam>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="cte">The CTE descriptor. The defining query retains its own mapping variant.</param>
    /// <param name="variant">The mapping variant for the outer query.</param>
    /// <returns>A builder for composing the query over the CTE.</returns>
    public static EntityBuilder<TResult> From<TResult>(this IDataContext dataContext, Cte<TResult> cte, MappingVariant variant)
    {
        var builder = dataContext.From(cte);
        builder.MappingVariant = variant;
        return builder;
    }

    /// <summary>Starts a query over a lazy temporary table with the supplied mapping variant.</summary>
    /// <typeparam name="TResult">The source query's projected row type.</typeparam>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="tempTable">The lazy source to materialise and read.</param>
    /// <param name="variant">The mapping variant for the read. It must agree with the source query's variant.</param>
    /// <returns>A builder for composing the query over the temporary table.</returns>
    public static EntityBuilder<TableAlias> From<TResult>(this IDataContext dataContext, TempTableSource<TResult> tempTable, MappingVariant variant)
    {
        var builder = dataContext.From(tempTable);
        builder.MappingVariant = variant;
        return builder;
    }

    /// <summary>Starts a query over a composable raw SQL source with the supplied mapping variant.</summary>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="sql">The SQL fragment to use as the source.</param>
    /// <param name="parameters">The object supplying named parameters, or <see langword="null"/>.</param>
    /// <param name="variant">The mapping variant for this query. It does not rewrite literal table names in <paramref name="sql"/>.</param>
    /// <returns>A builder for composing the query.</returns>
    public static EntityBuilder<TableAlias> FromSql(this IDataContext dataContext, string sql, object? parameters, MappingVariant variant)
    {
        var builder = dataContext.FromSql(sql, parameters);
        builder.MappingVariant = variant;
        return builder;
    }
}
