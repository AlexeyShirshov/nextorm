using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using NextORM.Core;

namespace NextORM.SqlServer;

/// <summary>
/// Provider-local, reference-identity rewriter for the typed SQL Server <c>PIVOT</c>/<c>UNPIVOT</c>
/// projection (#232). It maps the two-parameter projection lambda (<c>(m, pv) =&gt; shape</c>) onto the
/// single <see cref="TableAlias"/> parameter of the pivot source:
/// <list type="bullet">
/// <item><description>a direct member of the inner source becomes a
///   <see cref="SqlFunctions.Column{T}"/> reference to its effective column name (the resolved metadata
///   name for an entity source, the derived projection's output name for a derived source);</description></item>
/// <item><description>a marker accessor (<see cref="PivotValues.Value{T}"/>,
///   <see cref="UnpivotValues.Value{T}"/>, <see cref="UnpivotValues.Name{T}"/>) becomes the same typed
///   column reference, so the projected member drives the rename alias and the projection order drives
///   the <c>IN (...)</c> list.</description></item>
/// </list>
/// Only the two captured lambda parameters and their intended accesses are replaced. The existing
/// replacement visitors (<see cref="ReplaceParameterExpressionVisitor"/>) match a parameter by
/// <em>type</em>, which would also rewrite an unrelated same-typed parameter; this rewriter compares the
/// parameter by reference instead.
/// </summary>
internal static class PivotProjectionRewriter
{
    private static readonly MethodInfo s_columnMethod =
        typeof(SqlFunctions).GetMethod(nameof(SqlFunctions.Column), BindingFlags.Public | BindingFlags.Static)!;

    /// <summary>
    /// Extracts the PIVOT values in projection declaration order (a
    /// <see cref="System.Linq.Expressions.NewExpression"/> argument order or a
    /// <see cref="MemberInitExpression"/> binding order) and fail-closed validates the projection:
    /// an empty shape, a projection without values, a duplicate value, a value name that is not a
    /// compile-time or captured constant, and a grouping member that is also the aggregate or
    /// <c>FOR</c> column all throw <see cref="ArgumentException"/>.
    /// </summary>
    /// <param name="projection">The typed projection lambda.</param>
    /// <param name="aggregateColumn">The aggregate column lambda.</param>
    /// <param name="forColumn">The <c>FOR</c> column lambda.</param>
    /// <returns>The declared value names, in declaration order.</returns>
    internal static string[] ExtractValues(
        LambdaExpression projection,
        LambdaExpression aggregateColumn,
        LambdaExpression forColumn)
    {
        if (IsEmptyShape(projection.Body))
            throw new ArgumentException(
                "The PIVOT value projection is empty; declare at least one value with pivot.Value<T>(\"<value>\").",
                nameof(projection));

        var calls = CollectMarkerCalls(projection, typeof(PivotValues), nameof(PivotValues.Value));
        if (calls.Count == 0)
            throw new ArgumentException(
                "A typed PIVOT requires at least one value; declare every result value with pivot.Value<T>(\"<value>\") in the projection.",
                nameof(projection));

        RejectConsumedGroupingMembers(projection, aggregateColumn, forColumn);

        var values = new string[calls.Count];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < calls.Count; i++)
        {
            var value = ReadMarkerName(calls[i], nameof(PivotValues));
            if (!seen.Add(value))
                throw new ArgumentException(
                    $"A duplicate PIVOT value '{value}' was declared; each value may appear only once in the projection.",
                    nameof(projection));

            values[i] = value;
        }

        return values;
    }

    /// <summary>
    /// Extracts the UNPIVOT role identifiers from the projection: the value-column role declared with
    /// <see cref="UnpivotValues.Value{T}"/> and the name-column role declared with
    /// <see cref="UnpivotValues.Name{T}"/>. Exactly one identifier per role is required; a repeated
    /// reference to the same identifier reuses the role, while a missing role or conflicting
    /// identifiers throw <see cref="ArgumentException"/>.
    /// </summary>
    /// <param name="projection">The typed projection lambda.</param>
    /// <returns>The value-column and name-column role identifiers.</returns>
    internal static (string ValueColumn, string NameColumn) ExtractUnpivotRoles(LambdaExpression projection)
    {
        var values = CollectMarkerCalls(projection, typeof(UnpivotValues), nameof(UnpivotValues.Value));
        var names = CollectMarkerCalls(projection, typeof(UnpivotValues), nameof(UnpivotValues.Name));

        return (
            ResolveRole(projection, values, "value-column", nameof(UnpivotValues.Value)),
            ResolveRole(projection, names, "name-column", nameof(UnpivotValues.Name)));
    }

    /// <summary>
    /// Rewrites the projection body over the pivot source: every direct member access on the inner
    /// source parameter becomes a typed <see cref="SqlFunctions.Column{T}"/> reference, every marker
    /// accessor becomes the typed reference to its output column, and the marker parameter (if it is
    /// still referenced) is replaced by a captured marker instance. A bare reference to the inner
    /// source parameter and an unresolvable grouping member fail closed.
    /// </summary>
    /// <param name="projection">The typed projection lambda.</param>
    /// <param name="aliasParameter">The replacement <see cref="TableAlias"/> parameter.</param>
    /// <param name="markerType">The marker type of the projection (<see cref="PivotValues"/> or <see cref="UnpivotValues"/>).</param>
    /// <param name="resolveGroupingColumn">Resolves a grouping member's effective SQL column name, or <c>null</c> when it is not registered.</param>
    /// <param name="formatValueName">Formats an output-column reference (the PIVOT value escaped to its SQL identifier, the UNPIVOT role verbatim).</param>
    /// <returns>The rewritten projection body over <paramref name="aliasParameter"/>.</returns>
    internal static Expression Rewrite(
        LambdaExpression projection,
        ParameterExpression aliasParameter,
        Type markerType,
        Func<MemberInfo, string?> resolveGroupingColumn,
        Func<string, string> formatValueName)
    {
        var entityParameter = projection.Parameters[0];
        var markerParameter = projection.Parameters.Count > 1 ? projection.Parameters[1] : null;
        var markerInstance = markerParameter is null ? null : Activator.CreateInstance(markerType);

        var visitor = new ProjectionVisitor(
            entityParameter, markerParameter, markerInstance, aliasParameter, markerType, resolveGroupingColumn, formatValueName);
        return visitor.Visit(projection.Body);
    }

    /// <summary>
    /// Builds the canonical member path of an expression rooted at a lambda parameter (for example
    /// <c>Margin</c> or <c>Order.Margin</c>), used to compare a projection member with the aggregate and
    /// <c>FOR</c> columns independently of the parameter instance.
    /// </summary>
    /// <param name="expression">The expression to inspect.</param>
    /// <param name="path">The dot-joined member path when it is rooted at a parameter.</param>
    /// <returns><see langword="true"/> when the expression is a member chain rooted at a parameter.</returns>
    internal static bool TryGetMemberPath(Expression expression, out string path)
    {
        var parts = new List<string>();
        var current = TypeFacts.UnwrapConvert(expression);
        while (current is MemberExpression member)
        {
            parts.Add(member.Member.Name);

            if (member.Expression is null)
                break;

            current = TypeFacts.UnwrapConvert(member.Expression);
        }

        if (current is ParameterExpression && parts.Count > 0)
        {
            parts.Reverse();
            path = string.Join('.', parts);
            return true;
        }

        path = string.Empty;
        return false;
    }

    /// <summary>Returns <see langword="true"/> for a shape projection with no members/arguments.</summary>
    private static bool IsEmptyShape(Expression body)
        => TypeFacts.UnwrapConvert(body) switch
        {
            NewExpression { Arguments.Count: 0 } => true,
            MemberInitExpression { Bindings.Count: 0 } => true,
            _ => false
        };

    /// <summary>Collects the marker accessor calls of the projection in declaration order.</summary>
    private static List<MethodCallExpression> CollectMarkerCalls(LambdaExpression projection, Type markerType, string accessorName)
    {
        var scan = new MarkerScan(markerType, accessorName);
        scan.Visit(projection.Body);
        return scan.Calls;
    }

    /// <summary>Resolves the single identifier declared for one UNPIVOT role.</summary>
    private static string ResolveRole(LambdaExpression projection, List<MethodCallExpression> calls, string role, string accessor)
    {
        if (calls.Count == 0)
            throw new ArgumentException(
                $"The UNPIVOT projection must declare exactly one identifier per role; the {role} role is missing (declare it with unpivot.{accessor}<T>(\"<column>\")).",
                nameof(projection));

        var resolved = ReadMarkerName(calls[0], nameof(UnpivotValues));
        for (var i = 1; i < calls.Count; i++)
        {
            var other = ReadMarkerName(calls[i], nameof(UnpivotValues));
            if (!string.Equals(other, resolved, StringComparison.Ordinal))
                throw new ArgumentException(
                    $"The UNPIVOT projection declares conflicting identifiers for the {role} role ('{resolved}' and '{other}'); one identifier per role is required.",
                    nameof(projection));
        }

        return resolved;
    }

    /// <summary>Reads the constant/captured column name of a marker accessor call, or fails closed.</summary>
    private static string ReadMarkerName(MethodCallExpression call, string markerName)
    {
        if (call.Arguments.Count != 1
            || !TryReadConstantString(call.Arguments[0], out var name)
            || string.IsNullOrEmpty(name))
        {
            var argument = call.Arguments.Count == 1 ? call.Arguments[0].ToString() : call.ToString();
            throw new ArgumentException(
                $"The {markerName} accessor requires a compile-time or captured non-empty column name; '{argument}' is row-dependent and cannot be used as a column name.",
                "projection");
        }

        return name;
    }

    /// <summary>Reads a constant or closure-captured string without executing user code.</summary>
    private static bool TryReadConstantString(Expression expression, [NotNullWhen(true)] out string? value)
    {
        switch (TypeFacts.UnwrapConvert(expression))
        {
            case ConstantExpression { Value: string text }:
                value = text;
                return true;
            case MemberExpression { Expression: ConstantExpression closure } member:
                var captured = member.Member switch
                {
                    FieldInfo field => field.GetValue(closure.Value),
                    PropertyInfo property => property.GetValue(closure.Value),
                    _ => null
                };
                value = captured as string;
                return value is not null;
            default:
                value = null;
                return false;
        }
    }

    /// <summary>
    /// Fails when the projection reads a member of the inner source that is the aggregate or the
    /// <c>FOR</c> column: such a column is not part of the PIVOT result.
    /// </summary>
    private static void RejectConsumedGroupingMembers(
        LambdaExpression projection,
        LambdaExpression aggregateColumn,
        LambdaExpression forColumn)
    {
        var forbidden = new HashSet<string>(StringComparer.Ordinal);
        if (TryGetMemberPath(aggregateColumn.Body, out var aggregatePath))
            forbidden.Add(aggregatePath);
        if (TryGetMemberPath(forColumn.Body, out var forPath))
            forbidden.Add(forPath);

        if (forbidden.Count == 0)
            return;

        var grouping = new ElementScan(projection.Parameters[0]);
        grouping.Visit(projection.Body);
        foreach (var member in grouping.Members)
        {
            if (forbidden.Contains(member))
                throw new ArgumentException(
                    $"The grouping member '{member}' is also the PIVOT aggregate or FOR column, so it is not part of the PIVOT result; project another grouping member.",
                    nameof(projection));
        }
    }

    /// <summary>Collects marker accessor calls by declaring type and name, preserving visit order.</summary>
    private sealed class MarkerScan(Type markerType, string accessorName) : ExpressionVisitor
    {
        /// <summary>The collected accessor calls, in declaration order.</summary>
        public List<MethodCallExpression> Calls { get; } = [];

        /// <inheritdoc/>
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType == markerType
                && node.Method.Name == accessorName
                && node.Method.IsGenericMethod)
            {
                Calls.Add(node);
            }

            return base.VisitMethodCall(node);
        }
    }

    /// <summary>Collects the member names of the direct accesses to the given parameter.</summary>
    private sealed class ElementScan(ParameterExpression parameter) : ExpressionVisitor
    {
        /// <summary>The accessed member names.</summary>
        public List<string> Members { get; } = [];

        /// <inheritdoc/>
        protected override Expression VisitMember(MemberExpression node)
        {
            if (ReferenceEquals(node.Expression, parameter))
                Members.Add(node.Member.Name);

            return base.VisitMember(node);
        }
    }

    /// <summary>Rewrites the projection body over the pivot source parameter.</summary>
    private sealed class ProjectionVisitor(
        ParameterExpression entityParameter,
        ParameterExpression? markerParameter,
        object? markerInstance,
        ParameterExpression aliasParameter,
        Type markerType,
        Func<MemberInfo, string?> resolveGroupingColumn,
        Func<string, string> formatValueName) : ExpressionVisitor
    {
        /// <inheritdoc/>
        protected override Expression VisitMember(MemberExpression node)
        {
            if (ReferenceEquals(node.Expression, entityParameter))
            {
                var column = resolveGroupingColumn(node.Member)
                    ?? throw new BuildSqlCommandException(
                        $"The PIVOT/UNPIVOT projection member '{node.Member.Name}' has no registered column name; register the inner source mapping (From<T>()/From<T>(cfg)) before calling Pivot/Unpivot, or project a value instead.");

                return MakeColumn(node.Type, column);
            }

            return base.VisitMember(node);
        }

        /// <inheritdoc/>
        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (ReferenceEquals(node, entityParameter))
                throw new NotSupportedException(
                    "A typed PIVOT/UNPIVOT projection can only read a direct member of the inner source; project the member itself instead of the whole source.");

            if (ReferenceEquals(node, markerParameter))
                return Expression.Constant(markerInstance, markerParameter!.Type);

            return base.VisitParameter(node);
        }

        /// <inheritdoc/>
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType == markerType && node.Method.IsGenericMethod)
            {
                var name = formatValueName(ReadMarkerName(node, markerType.Name));
                return MakeColumn(node.Type, name);
            }

            return base.VisitMethodCall(node);
        }

        private Expression MakeColumn(Type valueType, string columnName)
            => Expression.Call(
                s_columnMethod.MakeGenericMethod(valueType),
                Expression.Convert(aliasParameter, typeof(object)),
                Expression.Constant(columnName, typeof(string)));
    }
}
