using System.Reflection;

namespace NextORM.Core;

/// <summary>
/// #148-B R2.1: the mapped-child-existence leg of a many-to-many collection correlation. The SQL
/// providers render the relation as a child-key subquery in the correlation condition; the in-memory
/// provider cannot nest a subquery in a correlated subcommand's condition, so it applies this
/// descriptor as a source filter on the junction rows instead (see
/// <c>InMemoryQueryBuilder.CreateEnumerator</c>). The junction multiplicity is therefore preserved:
/// the filter drops only a junction row whose mapped child is absent and never joins the child
/// relation, so no child-join fan-out and no deduplication can occur.
/// </summary>
internal sealed class JunctionChildFilter
{
    /// <summary>Creates the descriptor from the resolved mapped members.</summary>
    /// <param name="childType">The mapped child (principal) entity type.</param>
    /// <param name="junctionForeignKey">The junction member that references the child key.</param>
    /// <param name="childKey">The principal key member of the child.</param>
    internal JunctionChildFilter(Type childType, PropertyInfo junctionForeignKey, PropertyInfo childKey)
    {
        ArgumentNullException.ThrowIfNull(childType);
        ArgumentNullException.ThrowIfNull(junctionForeignKey);
        ArgumentNullException.ThrowIfNull(childKey);

        ChildType = childType;
        JunctionForeignKey = junctionForeignKey;
        ChildKey = childKey;
    }

    /// <summary>The mapped child (principal) entity type whose rows decide existence.</summary>
    internal Type ChildType { get; }

    /// <summary>The junction member that references the child key.</summary>
    internal PropertyInfo JunctionForeignKey { get; }

    /// <summary>The principal key member of the child.</summary>
    internal PropertyInfo ChildKey { get; }
}
