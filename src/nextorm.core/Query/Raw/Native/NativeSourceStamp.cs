namespace NextORM.Core;

/// <summary>
/// The physical-source identity of a query, as it takes part in a native shape's header.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="FromExpression"/> renders its table, schema and binding into the statement, but none of
/// that lives in the filter/grouping/projection graphs the shape is computed from. Without this stamp a
/// read of <c>From("ctas_a")</c> and a read of <c>From("ctas_b")</c> would be one shape, and the second
/// query would execute the first one's statement — the failure a CTAS/temp-table reader exposes
/// immediately as "relation does not exist".
/// </para>
/// <para>
/// The stamp reads only already-resolved strings and flags: no metadata lookup, no allocation, so it can
/// run on every probe.
/// </para>
/// </remarks>
internal readonly record struct NativeSourceStamp(
    string? Table, string? TableOverride, string? Schema, string? Database,
    string? Server, string? TableExpression, bool AutoMapped, Type? BindingEntity)
{
    /// <summary>Computes the stamp of <paramref name="from"/>.</summary>
    /// <param name="from">The resolved source of the command, or <see langword="null"/>.</param>
    /// <returns>A stable value for one physical source identity.</returns>
    internal static NativeSourceStamp For(FromExpression? from) => from is null ? default : new(
        from.Table, from.TableNameOverride, from.SchemaOverride ?? from.Schema, from.DatabaseOverride,
        from.ServerOverride, from.TableExpressionOverride, from.IsAutoMapped, from.SourceBinding?.EntityType);
}
