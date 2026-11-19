namespace NextORM.Core;

/// <summary>
/// Marks a DTO or record type used as a query result.
/// </summary>
/// <remarks>
/// This attribute is optional. The generator covers every materializable class, struct and anonymous
/// type declared in the consuming assembly, so adding or removing it does not change what is generated.
/// It is kept for source compatibility and as documentation at the projection type itself.
/// <para>
/// The generator emits a provider that is registered automatically, so the query pipeline materializes
/// the type with compiled code instead of an expression tree. This matters under NativeAOT, where a
/// compiled tree is not available and the expression path is interpreted row by row. A shape the
/// generator cannot reproduce faithfully — value converters, non-plain column types, ambiguous
/// constructors, a column set that does not match — keeps the built-in expression path.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class RowMaterializerAttribute : Attribute
{
}
