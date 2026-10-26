using System.Linq.Expressions;
using System.Reflection;

namespace NextORM.Core;

public sealed partial class MergeBuilder<TEntity>
{
    private MergeBuilder<TEntity> AddBranch(MergeMatchKind match, MergeActionKind action, LambdaExpression? assignments, LambdaExpression? condition)
    {
        if (_columns.Count == 0 && _source is null && _dynamicColumns is null)
            throw new InvalidOperationException("Call Using(...) before adding a merge branch.");

        if (assignments is not null)
        {
            var set = ParseAssignments(assignments, action, match);
            _branches.Add(new MergeBranch(match, action, [], condition, set));
            return this;
        }

        var list = new List<IPropertyMetadata>();
        foreach (var property in _metadata.Properties)
        {
            if (property.IsIdentity || property.IsComputed)
                continue;

            if (action == MergeActionKind.Update && property.IsKey)
                continue;

            list.Add(property);
        }

        var selected = (IReadOnlyList<IPropertyMetadata>)list;

        // A store-only entity (its only mapped column is the match key) has nothing to select
        // automatically, but the dynamic-columns store still supplies column values; the renderer
        // appends the store's dynamic keys to the branch, so an empty selection is allowed when a
        // store is present. Only an entity with neither a mapped nor a dynamic column is rejected.
        if (action is MergeActionKind.Update or MergeActionKind.Insert && selected.Count == 0 && _dynamicColumns is null)
            throw new NotSupportedException($"Entity {typeof(TEntity)} has no column to {action.ToString().ToLowerInvariant()}.");

        _branches.Add(new MergeBranch(match, action, selected, condition));
        return this;
    }

    /// <summary>
    /// Parses the initializer of an expression-based branch into an immutable assignment set. The body is
    /// a syntactic description of assignments: an anonymous initializer (bound by member name) or a named
    /// <c>MemberInit</c> assignable to the entity (bound by CLR property). Nothing is constructed and no
    /// constructor runs.
    /// </summary>
    /// <param name="assignments">The branch lambda; its parameters carry the roles.</param>
    /// <param name="action">The branch action (<c>UPDATE</c> or <c>INSERT</c>).</param>
    /// <param name="match">The branch match class.</param>
    /// <returns>The parsed assignment set.</returns>
    /// <exception cref="ArgumentException">The body is not a non-empty initializer, a destination is unknown, duplicated or type-incompatible.</exception>
    /// <exception cref="NotSupportedException">A destination may not be written by this action, or the insert form references an unavailable role.</exception>
    private MergeAssignmentSet ParseAssignments(LambdaExpression assignments, MergeActionKind action, MergeMatchKind match)
    {
        var bindings = ReadInitializerBindings(assignments.Body);
        var parameterRoles = BindParameterRoles(assignments.Parameters, action, bindings);

        var parsed = new List<MergeAssignment>(bindings.Count);
        var written = new List<IPropertyMetadata>(bindings.Count);
        for (var i = 0; i < bindings.Count; i++)
        {
            var (name, value) = bindings[i];
            var property = FindMappedByName(name)
                ?? throw new ArgumentException(
                    $"MERGE assignment destination '{name}' is not a mapped CLR property of '{typeof(TEntity)}'.", nameof(assignments));

            ValidateDestination(property, action, match);
            ValidateValueType(property, value, assignments);

            for (var w = 0; w < written.Count; w++)
            {
                if (ReferenceEquals(written[w], property))
                {
                    throw new ArgumentException(
                        $"MERGE assignment destination '{property.PropertyInfo.Name}' is specified more than once.", nameof(assignments));
                }
            }

            written.Add(property);
            parsed.Add(new MergeAssignment(property, value, FindReferencedRoles(value, parameterRoles)));
        }

        return new MergeAssignmentSet(parsed, parameterRoles);
    }

    /// <summary>Reads the ordered <c>(destination name, value expression)</c> pairs of an initializer body.</summary>
    /// <param name="body">The lambda body.</param>
    /// <returns>The initializer bindings in source order.</returns>
    /// <exception cref="ArgumentException">The body is not a supported, non-empty initializer.</exception>
    private List<(string Name, Expression Value)> ReadInitializerBindings(Expression body)
    {
        body = TypeFacts.UnwrapConvert(body);

        var bindings = new List<(string Name, Expression Value)>();
        switch (body)
        {
            case NewExpression { Members: { } members } anonymous:
                for (var i = 0; i < members.Count; i++)
                    bindings.Add((members[i].Name, anonymous.Arguments[i]));
                break;

            case MemberInitExpression initializer when typeof(TEntity).IsAssignableFrom(initializer.NewExpression.Type):
                foreach (var binding in initializer.Bindings)
                {
                    // A nested object/list initializer and a member-list binding are not assignments; only a
                    // single-value member assignment describes "destination = value".
                    if (binding is not MemberAssignment { Member: PropertyInfo property } assignment)
                        throw InvalidInitializer();

                    bindings.Add((property.Name, assignment.Expression));
                }

                break;

            default:
                throw InvalidInitializer();
        }

        if (bindings.Count == 0)
            throw InvalidInitializer();

        return bindings;
    }

    /// <summary>The frozen diagnostic for an unsupported assignment body.</summary>
    /// <returns>The exception to throw.</returns>
    private static ArgumentException InvalidInitializer()
        => new($"MERGE assignments must be a non-empty anonymous initializer or an initializer assignable to '{typeof(TEntity)}'.", "assignments");

    /// <summary>
    /// Binds every lambda parameter to the row it reads. The role comes from the parameter position of the
    /// documented overload, so an unused parameter still binds (and is accepted); the insert form has only
    /// the source row.
    /// </summary>
    /// <param name="parameters">The lambda parameters in declaration order.</param>
    /// <param name="action">The branch action.</param>
    /// <param name="bindings">The initializer bindings the roles are validated against.</param>
    /// <returns>The parameter-to-role bindings.</returns>
    /// <exception cref="NotSupportedException">The insert form references the target or the proposed row.</exception>
    private static MergeAssignmentRoleBinding[] BindParameterRoles(
        IReadOnlyList<ParameterExpression> parameters,
        MergeActionKind action,
        IReadOnlyList<(string Name, Expression Value)> bindings)
    {
        var roles = new MergeAssignmentRoleBinding[parameters.Count];
        for (var i = 0; i < parameters.Count; i++)
        {
            if (action == MergeActionKind.Insert)
            {
                if (i > 0)
                {
                    var role = i == 1 ? MergeAssignmentRole.Target : MergeAssignmentRole.Excluded;
                    if (IsReferenced(bindings, parameters[i]))
                    {
                        throw new NotSupportedException(
                            $"MERGE INSERT assignments cannot reference role '{RoleName(role)}'.");
                    }
                }

                roles[i] = new(parameters[i], MergeAssignmentRole.Source);
                continue;
            }

            roles[i] = new(parameters[i], i switch
            {
                0 => MergeAssignmentRole.Target,
                1 => MergeAssignmentRole.Source,
                _ => MergeAssignmentRole.Excluded,
            });
        }

        return roles;
    }

    /// <summary>Whether any initializer value references <paramref name="parameter"/>.</summary>
    /// <param name="bindings">The initializer bindings.</param>
    /// <param name="parameter">The parameter to look for.</param>
    /// <returns>Whether the parameter is referenced.</returns>
    private static bool IsReferenced(IReadOnlyList<(string Name, Expression Value)> bindings, ParameterExpression parameter)
    {
        var finder = new ParameterReferenceFinder(parameter);
        for (var i = 0; i < bindings.Count; i++)
        {
            finder.Visit(bindings[i].Value);
            if (finder.Found)
                return true;
        }

        return false;
    }

    /// <summary>Collects the distinct roles a right-hand side actually references.</summary>
    /// <param name="value">The right-hand side expression.</param>
    /// <param name="parameterRoles">The parameter-to-role bindings.</param>
    /// <returns>The referenced roles, in role declaration order.</returns>
    private static IReadOnlyList<MergeAssignmentRole> FindReferencedRoles(
        Expression value,
        IReadOnlyList<MergeAssignmentRoleBinding> parameterRoles)
    {
        var finder = new RoleReferenceFinder(parameterRoles);
        finder.Visit(value);

        if (finder.Referenced.Count == 0)
            return [];

        var roles = new List<MergeAssignmentRole>(finder.Referenced.Count);
        foreach (var role in new[] { MergeAssignmentRole.Target, MergeAssignmentRole.Source, MergeAssignmentRole.Excluded })
        {
            if (finder.Referenced.Contains(role))
                roles.Add(role);
        }

        return roles;
    }

    /// <summary>Rejects a destination the action may not write.</summary>
    /// <param name="property">The mapped destination.</param>
    /// <param name="action">The branch action.</param>
    /// <param name="match">The branch match class.</param>
    /// <exception cref="NotSupportedException">The destination is identity, computed or the merge key of an update.</exception>
    private static void ValidateDestination(IPropertyMetadata property, MergeActionKind action, MergeMatchKind match)
    {
        var actionName = action == MergeActionKind.Insert ? "INSERT" : "UPDATE";
        if (property.IsIdentity)
            throw new NotSupportedException($"MERGE {actionName} cannot assign identity property '{property.PropertyInfo.Name}'.");

        if (property.IsComputed)
            throw new NotSupportedException($"MERGE {actionName} cannot assign computed property '{property.PropertyInfo.Name}'.");

        if (action == MergeActionKind.Update && match == MergeMatchKind.Matched && property.IsKey)
            throw new NotSupportedException($"MERGE {actionName} cannot assign merge key property '{property.PropertyInfo.Name}'.");
    }

    /// <summary>Rejects a value expression whose CLR type cannot be stored in the destination.</summary>
    /// <param name="property">The mapped destination.</param>
    /// <param name="value">The right-hand side expression.</param>
    /// <param name="assignments">The branch lambda, for the argument name.</param>
    /// <exception cref="ArgumentException">The value type is incompatible with the destination.</exception>
    private static void ValidateValueType(IPropertyMetadata property, Expression value, LambdaExpression assignments)
    {
        var destination = property.PropertyInfo.PropertyType;
        var valueType = value.Type;
        if (destination.IsAssignableFrom(valueType))
            return;

        var underlying = Nullable.GetUnderlyingType(destination);
        if (underlying is not null && underlying.IsAssignableFrom(valueType))
            return;

        throw new ArgumentException(
            $"MERGE assignment for '{property.PropertyInfo.Name}' has incompatible value type '{valueType}'.", nameof(assignments));
    }

    /// <summary>The SQL spelling of a role for the frozen diagnostics.</summary>
    /// <param name="role">The role.</param>
    /// <returns>The lower-case role name.</returns>
    internal static string RoleName(MergeAssignmentRole role) => role switch
    {
        MergeAssignmentRole.Target => "target",
        MergeAssignmentRole.Source => "source",
        _ => "excluded",
    };

    /// <summary>Resolves a mapped property by its CLR name.</summary>
    /// <param name="name">The CLR property name.</param>
    /// <returns>The mapped property, or <see langword="null"/> when the entity does not map it.</returns>
    private IPropertyMetadata? FindMappedByName(string name)
    {
        foreach (var candidate in _metadata.Properties)
        {
            if (string.Equals(candidate.PropertyInfo.Name, name, StringComparison.Ordinal))
                return candidate;
        }

        return null;
    }

    private IPropertyMetadata? FindMapped(PropertyInfo property)
    {
        foreach (var candidate in _metadata.Properties)
        {
            if (candidate.PropertyInfo == property)
                return candidate;
        }

        return null;
    }

    /// <summary>Finds whether an expression references a specific lambda parameter.</summary>
    private sealed class ParameterReferenceFinder : ExpressionVisitor
    {
        private readonly ParameterExpression _parameter;

        /// <summary>Creates the finder.</summary>
        /// <param name="parameter">The parameter to look for.</param>
        public ParameterReferenceFinder(ParameterExpression parameter) => _parameter = parameter;

        /// <summary>Whether the parameter was referenced.</summary>
        public bool Found { get; private set; }

        /// <inheritdoc/>
        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (ReferenceEquals(node, _parameter))
                Found = true;

            return base.VisitParameter(node);
        }
    }

    /// <summary>Collects the distinct roles referenced by an assignment right-hand side.</summary>
    private sealed class RoleReferenceFinder : ExpressionVisitor
    {
        private readonly IReadOnlyList<MergeAssignmentRoleBinding> _bindings;

        /// <summary>Creates the finder.</summary>
        /// <param name="bindings">The parameter-to-role bindings of the assignment lambda.</param>
        public RoleReferenceFinder(IReadOnlyList<MergeAssignmentRoleBinding> bindings) => _bindings = bindings;

        /// <summary>The distinct roles referenced so far.</summary>
        public HashSet<MergeAssignmentRole> Referenced { get; } = [];

        /// <inheritdoc/>
        protected override Expression VisitParameter(ParameterExpression node)
        {
            for (var i = 0; i < _bindings.Count; i++)
            {
                if (ReferenceEquals(_bindings[i].Parameter, node))
                    Referenced.Add(_bindings[i].Role);
            }

            return base.VisitParameter(node);
        }
    }
}
