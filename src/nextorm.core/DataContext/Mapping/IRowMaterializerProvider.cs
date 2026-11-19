namespace NextORM.Core;

/// <summary>
/// Supplies row materializers that already exist as compiled code, so the query pipeline does not have
/// to build and compile an expression tree for the shape. This is what makes a generated materializer
/// possible under NativeAOT, where <see cref="System.Linq.Expressions.LambdaExpression.Compile()"/>
/// cannot emit code and the expression path falls back to the interpreter.
/// </summary>
/// <remarks>
/// A provider is consulted once per result shape, before the expression tree is built. Returning
/// <see langword="null"/> — for a type it does not cover, or for a column set it cannot reproduce —
/// keeps the built-in expression path, which is the default and the only path when no provider is
/// registered. Implementations must return a <c>Func&lt;IDataRecord, T&gt;</c> whose row semantics
/// match the built-in path: the same typed reader accessors, <c>NULL</c> mapped to <see langword="null"/>
/// for nullable types and to <c>default(T)</c> for a default-on-null column.
/// </remarks>
public interface IRowMaterializerProvider
{
    /// <summary>Creates the materializer for a result shape.</summary>
    /// <param name="resultType">The type each row is materialized into.</param>
    /// <param name="columns">The projected columns, in result-set order.</param>
    /// <returns>
    /// A <c>Func&lt;IDataRecord, T&gt;</c> for <paramref name="resultType"/>, or <see langword="null"/>
    /// to use the built-in expression path.
    /// </returns>
    Delegate? TryCreate(Type resultType, ReadOnlySpan<RowMaterializerColumn> columns);
}
