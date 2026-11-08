using System.Linq.Expressions;
using System.Reflection;

namespace NextORM.Core;

/// <summary>
/// The row an expression-based MERGE assignment may read. The role belongs to the lambda parameter the
/// user wrote, never to the member name, so <c>s.Name</c> and <c>t.Name</c> stay distinct even though both
/// map to the same column.
/// </summary>
internal enum MergeAssignmentRole
{
    /// <summary>The existing target row before the update.</summary>
    Target,

    /// <summary>The original input row, before any insert expression.</summary>
    Source,

    /// <summary>The provider-native proposed insert row (<c>excluded</c>), where the provider has one.</summary>
    Excluded,
}

/// <summary>
/// The role bound to one lambda parameter of an assignment expression. The binding is immutable and keeps
/// the original <see cref="ParameterExpression"/> identity, so a role is never inferred from a name.
/// </summary>
/// <param name="Parameter">The lambda parameter.</param>
/// <param name="Role">The row the parameter reads.</param>
internal readonly record struct MergeAssignmentRoleBinding(ParameterExpression Parameter, MergeAssignmentRole Role);

/// <summary>
/// One immutable column assignment of a MERGE branch: the mapped destination property and the right-hand
/// side expression that produces its value. The destination is bound by the CLR property of the mapped
/// entity, so a remapped database column and an interface-mapped entity both work without materialising
/// anything.
/// </summary>
internal sealed class MergeAssignment
{
    /// <summary>Creates an assignment.</summary>
    /// <param name="property">The mapped destination property.</param>
    /// <param name="value">The right-hand side expression (the initializer binding value).</param>
    /// <param name="referencedRoles">The distinct roles the right-hand side actually references.</param>
    public MergeAssignment(IPropertyMetadata property, Expression value, IReadOnlyList<MergeAssignmentRole> referencedRoles)
    {
        Property = property;
        Value = value;
        ReferencedRoles = referencedRoles;
    }

    /// <summary>The mapped destination property.</summary>
    public IPropertyMetadata Property { get; }

    /// <summary>The right-hand side expression producing the written value.</summary>
    public Expression Value { get; }

    /// <summary>The distinct roles this right-hand side actually references (empty for a constant).</summary>
    public IReadOnlyList<MergeAssignmentRole> ReferencedRoles { get; }
}

/// <summary>
/// The immutable assignment set of one MERGE branch (or of one key-upsert terminator): the ordered
/// assignments, the parameter-to-role bindings in declaration order, and the referenced-role set computed
/// from actual usage. A <see langword="null"/> set means the branch keeps the no-argument behaviour; an
/// empty set is never constructed.
/// </summary>
internal sealed class MergeAssignmentSet
{
    private readonly MergeAssignment[] _assignments;
    private readonly MergeAssignmentRoleBinding[] _bindings;

    /// <summary>Creates an assignment set.</summary>
    /// <param name="assignments">The ordered assignments; must be non-empty.</param>
    /// <param name="bindings">The parameter-to-role bindings, in parameter declaration order.</param>
    /// <exception cref="BuildSqlCommandException">The set is empty.</exception>
    public MergeAssignmentSet(IReadOnlyList<MergeAssignment> assignments, IReadOnlyList<MergeAssignmentRoleBinding> bindings)
    {
        if (assignments.Count == 0)
            throw new BuildSqlCommandException("A MERGE assignment set must contain at least one assignment.");

        _assignments = [.. assignments];
        _bindings = [.. bindings];

        var usesTarget = false;
        var usesSource = false;
        var usesExcluded = false;
        foreach (var assignment in _assignments)
        {
            foreach (var role in assignment.ReferencedRoles)
            {
                switch (role)
                {
                    case MergeAssignmentRole.Target:
                        usesTarget = true;
                        break;
                    case MergeAssignmentRole.Source:
                        usesSource = true;
                        break;
                    default:
                        usesExcluded = true;
                        break;
                }
            }
        }

        UsesTarget = usesTarget;
        UsesSource = usesSource;
        UsesExcluded = usesExcluded;
    }

    /// <summary>The ordered assignments of the set.</summary>
    public IReadOnlyList<MergeAssignment> Assignments => _assignments;

    /// <summary>The parameter-to-role bindings, in parameter declaration order.</summary>
    public IReadOnlyList<MergeAssignmentRoleBinding> Bindings => _bindings;

    /// <summary>Whether any right-hand side reads the target row.</summary>
    public bool UsesTarget { get; }

    /// <summary>Whether any right-hand side reads the original input row.</summary>
    public bool UsesSource { get; }

    /// <summary>Whether any right-hand side reads the provider-native proposed row.</summary>
    public bool UsesExcluded { get; }

    /// <summary>The number of assignments in the set.</summary>
    public int Count => _assignments.Length;

    /// <summary>
    /// Resolves a destination property to its assignment, or <see langword="null"/> when the set does not
    /// write it.
    /// </summary>
    /// <param name="property">The mapped property to look up.</param>
    /// <returns>The assignment writing <paramref name="property"/>, or <see langword="null"/>.</returns>
    public MergeAssignment? Find(IPropertyMetadata property)
    {
        for (var i = 0; i < _assignments.Length; i++)
        {
            if (ReferenceEquals(_assignments[i].Property, property))
                return _assignments[i];
        }

        return null;
    }

    /// <summary>
    /// Returns the role bound to a lambda parameter, or <see langword="null"/> when the parameter does not
    /// belong to this set's lambda.
    /// </summary>
    /// <param name="parameter">The lambda parameter of the assignment expression.</param>
    /// <returns>The parameter's role, or <see langword="null"/>.</returns>
    public MergeAssignmentRole? RoleOf(ParameterExpression parameter)
    {
        for (var i = 0; i < _bindings.Length; i++)
        {
            if (ReferenceEquals(_bindings[i].Parameter, parameter))
                return _bindings[i].Role;
        }

        return null;
    }

    /// <summary>
    /// Determines whether an expression is exactly a direct read of the named property through
    /// <paramref name="role"/> (for example <c>source.Id</c>). The key-upsert conflict-key rule requires
    /// this exact shape, so an expression that merely produces the same value is still rejected.
    /// </summary>
    /// <param name="value">The right-hand side expression.</param>
    /// <param name="role">The required role.</param>
    /// <param name="property">The required destination property.</param>
    /// <returns>Whether the expression is the required direct read.</returns>
    public bool IsDirectRoleRead(Expression value, MergeAssignmentRole role, IPropertyMetadata property)
    {
        var body = TypeFacts.UnwrapConvert(value);
        if (body is not MemberExpression { Expression: ParameterExpression parameter } member
            || member.Member is not PropertyInfo memberProperty
            || memberProperty.Name != property.PropertyInfo.Name
            || RoleOf(parameter) != role)
        {
            return false;
        }

        return true;
    }
}
