using System.Runtime.CompilerServices;

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
internal static class NativeSourceStamp
{
    /// <summary>Computes the stamp of <paramref name="from"/>.</summary>
    /// <param name="from">The resolved source of the command, or <see langword="null"/>.</param>
    /// <returns>A stable value for one physical source identity.</returns>
    internal static int For(FromExpression? from)
    {
        if (from is null)
            return 0;

        unchecked
        {
            var stamp = Hash(from.Table);
            stamp = stamp * 31 + Hash(from.TableNameOverride);
            stamp = stamp * 31 + Hash(from.SchemaOverride);
            stamp = stamp * 31 + Hash(from.DatabaseOverride);
            stamp = stamp * 31 + Hash(from.ServerOverride);
            stamp = stamp * 31 + Hash(from.TableExpressionOverride);
            stamp = stamp * 31 + (from.IsAutoMapped ? 1 : 0);

            var binding = from.SourceBinding;
            stamp = stamp * 31 + (binding is null ? 0 : RuntimeHelpers.GetHashCode(binding.EntityType));
            return stamp;
        }
    }

    private static int Hash(string? value) => value is { Length: > 0 } ? StringComparer.Ordinal.GetHashCode(value) : 0;
}
