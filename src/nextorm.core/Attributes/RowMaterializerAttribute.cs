namespace NextORM.Core;

/// <summary>
/// Marks a projection type — a DTO or record used as a query result — for a compile-time row
/// materializer. Types annotated with <see cref="SqlTableAttribute"/> are covered without this
/// attribute.
/// </summary>
/// <remarks>
/// The generator emits a provider that is registered automatically, so the query pipeline
/// materializes the marked type with compiled code instead of an expression tree. This matters under
/// NativeAOT, where a compiled tree is not available and the expression path is interpreted row by
/// row. A type is skipped when its shape cannot be reproduced faithfully — including value converters,
/// non-plain column types and ambiguous constructors — and the built-in expression path stays in use.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class RowMaterializerAttribute : Attribute
{
}
