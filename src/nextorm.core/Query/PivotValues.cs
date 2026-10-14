namespace NextORM.Core;

/// <summary>
/// Translation-only marker for the typed <c>PIVOT</c> result projection. An instance of this type is
/// the second parameter of the projection passed to the typed SQL Server
/// <c>Pivot</c> overload: <c>projection: (t, v) =&gt; ... v.Value&lt;decimal?&gt;("[1]") ...</c>.
/// </summary>
/// <remarks>
/// The marker carries no runtime state. It can also be instantiated and captured directly in an
/// ordinary <c>Select</c> over an existing <c>PIVOT</c> source. Only the query translator may bind a
/// <see cref="Value{T}"/> call; executing an accessor outside a query expression throws
/// <see cref="NotSupportedException"/>.
/// </remarks>
public sealed class PivotValues
{
    /// <summary>
    /// Creates the marker. The constructor is public so a captured instance can be used in an
    /// ordinary <c>Select</c> projection; the instance itself has no behaviour.
    /// </summary>
    public PivotValues()
    {
    }

    /// <summary>
    /// References the <c>PIVOT</c> result column produced by <paramref name="columnName"/> (one of
    /// the <c>PivotValue</c> values), with an explicit CLR cell type.
    /// </summary>
    /// <typeparam name="T">The CLR type of the result column value.</typeparam>
    /// <param name="columnName">The pivot value that names the result column.</param>
    /// <returns>The cell value, materialized as <typeparamref name="T"/>.</returns>
    /// <exception cref="NotSupportedException">
    /// Always thrown: the accessor is translation-only and can only be used inside a query expression.
    /// </exception>
    public T Value<T>(string columnName) => throw new NotSupportedException(
        "PivotValues.Value is a translation-only marker and can only be used inside a query expression.");
}

/// <summary>
/// Translation-only marker for the typed <c>UNPIVOT</c> result projection. An instance of this type
/// is the second parameter of the projection passed to the typed SQL Server
/// <c>Unpivot</c> overload: <c>projection: (t, v) =&gt; ... v.Name&lt;string&gt;("qtr") ... v.Value&lt;decimal?&gt;("val") ...</c>.
/// </summary>
/// <remarks>
/// The marker carries no runtime state. It can also be instantiated and captured directly in an
/// ordinary <c>Select</c> over an existing <c>UNPIVOT</c> source. Only the query translator may bind a
/// <see cref="Name{T}"/>/<see cref="Value{T}"/> call; executing an accessor outside a query
/// expression throws <see cref="NotSupportedException"/>.
/// </remarks>
public sealed class UnpivotValues
{
    /// <summary>
    /// Creates the marker. The constructor is public so a captured instance can be used in an
    /// ordinary <c>Select</c> projection; the instance itself has no behaviour.
    /// </summary>
    public UnpivotValues()
    {
    }

    /// <summary>
    /// References the <c>UNPIVOT</c> name column (the output column that receives the originating
    /// source column name) with an explicit CLR type.
    /// </summary>
    /// <typeparam name="T">The CLR type of the name column value.</typeparam>
    /// <param name="nameColumn">The name of the UNPIVOT name column (the <c>FOR</c> identifier).</param>
    /// <returns>The name value, materialized as <typeparamref name="T"/>.</returns>
    /// <exception cref="NotSupportedException">
    /// Always thrown: the accessor is translation-only and can only be used inside a query expression.
    /// </exception>
    public T Name<T>(string nameColumn) => throw new NotSupportedException(
        "UnpivotValues.Name is a translation-only marker and can only be used inside a query expression.");

    /// <summary>
    /// References the <c>UNPIVOT</c> value column (the output column that receives the stacked source
    /// column values) with an explicit CLR type.
    /// </summary>
    /// <typeparam name="T">The CLR type of the value column value.</typeparam>
    /// <param name="valueColumn">The name of the UNPIVOT value column.</param>
    /// <returns>The value, materialized as <typeparamref name="T"/>.</returns>
    /// <exception cref="NotSupportedException">
    /// Always thrown: the accessor is translation-only and can only be used inside a query expression.
    /// </exception>
    public T Value<T>(string valueColumn) => throw new NotSupportedException(
        "UnpivotValues.Value is a translation-only marker and can only be used inside a query expression.");
}
