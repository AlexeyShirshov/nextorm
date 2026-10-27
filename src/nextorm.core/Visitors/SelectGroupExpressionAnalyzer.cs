using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace NextORM.Core;

/// <summary>
/// The execution shape a <c>SelectGroup</c> projection analysis resolves: keys inferred from the
/// projection, a group-less global aggregate, or a projection that references no row at all.
/// </summary>
internal enum SelectGroupMode
{
    /// <summary>The analysis has not run (the command is not a <c>SelectGroup</c> command).</summary>
    None = 0,
    /// <summary>At least one grouping key was inferred, so the query carries a <c>GROUP BY</c> list.</summary>
    Keyed = 1,
    /// <summary>No keys were inferred and the query contains an aggregate: one row for the whole source.</summary>
    GlobalAggregate = 2,
    /// <summary>No keys and no aggregates: the projection references no row of the source.</summary>
    ConstantProjection = 3,
}

/// <summary>
/// Infers the <c>GROUP BY</c> keys of a <c>SelectGroup</c> projection from its raw source expressions.
/// <para>
/// The analysis works on the lambda the caller wrote, never on the visited select list: a key is a
/// maximal row-dependent, aggregate-free subexpression of a scalar projection output, collected in
/// first-appearance order and deduplicated structurally. An aggregate call is opaque — its arguments,
/// <c>DISTINCT</c> and <c>FILTER</c> never contribute keys. Before the inference runs, window
/// expressions and subqueries are rejected unconditionally (projection, <c>HAVING</c> and expression
/// <c>ORDER BY</c>), and <c>HAVING</c>/<c>ORDER BY</c> are validated against the inferred keys so an
/// uncovered row expression fails at preparation instead of rendering invalid SQL.
/// </para>
/// </summary>
internal static class SelectGroupExpressionAnalyzer
{
    // The built-in aggregate surface of CommonFunctions (and its provider-derived surfaces). The
    // *_distinct spellings are added below; names that WindowSql maps to a window function are never
    // treated as aggregates, because the same CLR name is also a window member (sum_over, ...).
    private static readonly string[] BuiltinAggregateNames =
    [
        "count",
        "count_big",
        "min",
        "max",
        "avg",
        "sum",
        "stdev",
        "stdevp",
        "var",
        "varp",
        "any_agg",
        "string_agg",
        "corr",
        "covar_pop",
        "covar_samp",
    ];

    private static readonly HashSet<string> AggregateNameSet = BuildAggregateNameSet();

    private static HashSet<string> BuildAggregateNameSet()
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in BuiltinAggregateNames)
        {
            names.Add(name);
            names.Add(name + "_distinct");
        }

        return names;
    }

    /// <summary>
    /// Analyzes a <c>SelectGroup</c> projection and returns the inferred keys in first-appearance order
    /// together with the execution mode.
    /// </summary>
    /// <param name="projection">The raw projection lambda.</param>
    /// <param name="having">The raw <c>HAVING</c> lambda, or <c>null</c>.</param>
    /// <param name="sorting">The raw sort keys, or <c>null</c>; ordinal-only keys are skipped.</param>
    /// <param name="cancellationToken">Cancellation token observed at every collection step.</param>
    /// <returns>The inferred keys and the resolved mode.</returns>
    /// <exception cref="QueryPreparationException">
    /// The projection is an identity projection, contains an unsupported window/subquery expression, a
    /// key is not a selectable scalar, or <c>HAVING</c>/<c>ORDER BY</c> references an uncovered row
    /// expression.
    /// </exception>
    internal static (IReadOnlyList<Expression> Keys, SelectGroupMode Mode) Analyze(
        LambdaExpression projection,
        LambdaExpression? having,
        Sorting[]? sorting,
        CancellationToken cancellationToken)
    {
        // 1. Unconditional rejection pass. It runs first so an unsupported shape is reported as such
        // instead of being (mis)classified as a key; it traverses through aggregate calls and their
        // FILTER lambda, and never descends into a nested query scope (it throws on the call node).
        RejectUnsupportedShapes(projection.Body, "projection", cancellationToken);
        if (having is not null)
            RejectUnsupportedShapes(having.Body, "HAVING", cancellationToken);
        if (sorting is not null)
        {
            for (var i = 0; i < sorting.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (sorting[i].SortExpression is LambdaExpression sortLambda)
                    RejectUnsupportedShapes(sortLambda.Body, "ORDER BY", cancellationToken);
            }
        }

        // 2. Scalar outputs of the projection, derived exactly like PrepareColumns derives its columns.
        var body = TypeFacts.UnwrapConvert(projection.Body);
        if (ReferenceEquals(body, projection.Parameters[0]))
        {
            throw new QueryPreparationException(
                "SelectGroup requires a projection with at least one scalar output; an identity projection is not supported.");
        }

        IReadOnlyList<Expression> outputs = projection.Body switch
        {
            NewExpression { Members: not null } ctor when !TypeFacts.IsSingleColumnProjection(ctor.Type) => ctor.Arguments,
            MemberInitExpression init => GetMemberInitExpressions(init),
            _ => [projection.Body],
        };

        // 3. Key collection, in output order, first occurrence wins.
        var keys = new List<Expression>();
        var comparer = new SelectGroupExpressionComparer();
        for (var i = 0; i < outputs.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CollectKey(outputs[i], projection.Parameters[0], keys, comparer, cancellationToken);
        }

        // 4. Coverage validation for HAVING and every expression ORDER BY key.
        if (having is not null)
            ValidateCoverage(having.Body, having.Parameters[0], keys, comparer, "HAVING", cancellationToken);
        if (sorting is not null)
        {
            for (var i = 0; i < sorting.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (sorting[i].SortExpression is LambdaExpression sortLambda)
                    ValidateCoverage(sortLambda.Body, sortLambda.Parameters[0], keys, comparer, "ORDER BY", cancellationToken);
            }
        }

        // 5. Mode.
        if (keys.Count > 0)
            return (keys, SelectGroupMode.Keyed);

        var hasAggregate = ContainsAggregate(projection.Body)
            || (having is not null && ContainsAggregate(having.Body));
        if (!hasAggregate && sorting is not null)
        {
            for (var i = 0; i < sorting.Length && !hasAggregate; i++)
            {
                if (sorting[i].SortExpression is LambdaExpression sortLambda)
                    hasAggregate = ContainsAggregate(sortLambda.Body);
            }
        }

        return ([], hasAggregate ? SelectGroupMode.GlobalAggregate : SelectGroupMode.ConstantProjection);
    }

    /// <summary>
    /// Classifies an expression as an aggregate call: an effective <see cref="SqlFunctionAttribute"/>
    /// whose <see cref="SqlFunctionAttribute.IsAggregate"/> is set, or a built-in
    /// <see cref="CommonFunctions"/> aggregate. Window members are excluded, and every
    /// provider-advanced family that is not part of the built-in set classifies as a scalar.
    /// </summary>
    /// <param name="expression">The expression to classify.</param>
    /// <returns><see langword="true"/> when the expression is an aggregate call.</returns>
    internal static bool IsAggregateCall(Expression expression)
    {
        if (expression is not MethodCallExpression call)
            return false;

        if (ScalarFunctionTranslator.ResolveSqlFunctionAttribute(call.Method)?.IsAggregate == true)
            return true;

        var declaringType = call.Method.DeclaringType;
        if (declaringType is null || !declaringType.IsAssignableTo(typeof(CommonFunctions)))
            return false;

        var name = call.Method.Name;
        if (WindowSql.MapWindowFunctionName(name) is not null)
            return false;

        return AggregateNameSet.Contains(name);
    }

    private static IReadOnlyList<Expression> GetMemberInitExpressions(MemberInitExpression init)
    {
        var expressions = new Expression[init.Bindings.Count];
        for (var i = 0; i < init.Bindings.Count; i++)
        {
            if (init.Bindings[i] is not MemberAssignment assignment)
            {
                throw new QueryPreparationException(
                    "SelectGroup requires a projection with scalar member-init bindings; a list/member binding is not supported.");
            }

            expressions[i] = assignment.Expression;
        }

        return expressions;
    }

    /// <summary>
    /// Collects the maximal row-dependent, aggregate-free subexpressions of <paramref name="expression"/>
    /// as grouping keys. An aggregate call is opaque; a constant or captured-only expression is skipped;
    /// an aggregate-bearing non-aggregate expression is descended into.
    /// </summary>
    private static void CollectKey(
        Expression? expression,
        ParameterExpression parameter,
        List<Expression> keys,
        IEqualityComparer<Expression> comparer,
        CancellationToken cancellationToken)
    {
        if (expression is null)
            return;

        cancellationToken.ThrowIfCancellationRequested();

        if (IsAggregateCall(expression))
            return;

        if (!ReferencesParameter(expression, parameter))
            return;

        if (ContainsAggregate(expression))
        {
            foreach (var child in EnumerateChildren(expression))
                CollectKey(child, parameter, keys, comparer, cancellationToken);

            return;
        }

        AddKey(expression, keys, comparer);
    }

    private static void AddKey(Expression key, List<Expression> keys, IEqualityComparer<Expression> comparer)
    {
        if (!TypeFacts.IsSingleColumnProjection(key.Type))
        {
            throw new QueryPreparationException(
                $"SelectGroup cannot group by the non-scalar expression '{key}'.");
        }

        for (var i = 0; i < keys.Count; i++)
        {
            if (comparer.Equals(keys[i], key))
                return;
        }

        keys.Add(key);
    }

    /// <summary>
    /// Validates a <c>HAVING</c>/<c>ORDER BY</c> expression against the inferred keys. A whole key is
    /// matched before descent, an aggregate call is opaque, a captured/constant expression is
    /// irrelevant, and a bare row member/parameter that is not covered by a key is rejected.
    /// </summary>
    private static void ValidateCoverage(
        Expression expression,
        ParameterExpression parameter,
        List<Expression> keys,
        IEqualityComparer<Expression> comparer,
        string clause,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        for (var i = 0; i < keys.Count; i++)
        {
            if (comparer.Equals(keys[i], expression))
                return;
        }

        if (!ReferencesParameter(expression, parameter))
            return;

        if (IsAggregateCall(expression))
            return;

        if (expression is MemberExpression or ParameterExpression)
        {
            throw new QueryPreparationException(
                $"SelectGroup {clause} references a row expression not covered by inferred keys: {expression}.");
        }

        foreach (var child in EnumerateChildren(expression))
            ValidateCoverage(child, parameter, keys, comparer, clause, cancellationToken);
    }

    private static void RejectUnsupportedShapes(Expression expression, string clause, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        UnsupportedShapeFinder.ThrowIfFound(expression, clause);
    }

    private static bool ContainsAggregate(Expression expression)
    {
        var finder = new AggregateFinder();
        finder.Visit(expression);
        return finder.Found;
    }

    private static bool ReferencesParameter(Expression expression, ParameterExpression parameter)
    {
        var finder = new ParameterFinder(parameter);
        finder.Visit(expression);
        return finder.Found;
    }

    /// <summary>
    /// Enumerates the direct child expressions of a node, for the recursive key collection and
    /// coverage validation. Unknown node kinds have no children (they are then treated as a leaf).
    /// </summary>
    private static IEnumerable<Expression> EnumerateChildren(Expression expression)
    {
        switch (expression)
        {
            case UnaryExpression unary:
                yield return unary.Operand;
                break;
            case BinaryExpression binary:
                yield return binary.Left;
                yield return binary.Right;
                if (binary.Conversion is { } conversion)
                    yield return conversion;
                break;
            case MethodCallExpression call:
                if (call.Object is { } target)
                    yield return target;
                for (var i = 0; i < call.Arguments.Count; i++)
                    yield return call.Arguments[i];
                break;
            case NewExpression ctor:
                for (var i = 0; i < ctor.Arguments.Count; i++)
                    yield return ctor.Arguments[i];
                break;
            case NewArrayExpression newArray:
                for (var i = 0; i < newArray.Expressions.Count; i++)
                    yield return newArray.Expressions[i];
                break;
            case MemberInitExpression init:
                yield return init.NewExpression;
                for (var i = 0; i < init.Bindings.Count; i++)
                {
                    if (init.Bindings[i] is MemberAssignment assignment)
                        yield return assignment.Expression;
                }

                break;
            case ConditionalExpression conditional:
                yield return conditional.Test;
                yield return conditional.IfTrue;
                yield return conditional.IfFalse;
                break;
            case InvocationExpression invocation:
                yield return invocation.Expression;
                for (var i = 0; i < invocation.Arguments.Count; i++)
                    yield return invocation.Arguments[i];
                break;
            case TypeBinaryExpression typeIs:
                yield return typeIs.Expression;
                break;
            case LambdaExpression lambda:
                yield return lambda.Body;
                break;
            case MemberExpression member:
                if (member.Expression is { } memberTarget)
                    yield return memberTarget;
                break;
            case IndexExpression index:
                if (index.Object is { } indexTarget)
                    yield return indexTarget;
                for (var i = 0; i < index.Arguments.Count; i++)
                    yield return index.Arguments[i];
                break;
        }
    }

    /// <summary>Rejects window and subquery expressions anywhere in the visited clause.</summary>
    private sealed class UnsupportedShapeFinder(string clause) : ExpressionVisitor
    {
        public static void ThrowIfFound(Expression expression, string clause)
            => new UnsupportedShapeFinder(clause).Visit(expression);

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (IsWindowCall(node))
            {
                throw new QueryPreparationException(
                    $"SelectGroup {clause} contains an unsupported window expression: {node}.");
            }

            if (IsSubqueryCall(node))
            {
                throw new QueryPreparationException(
                    $"SelectGroup {clause} contains an unsupported subquery expression: {node}.");
            }

            return base.VisitMethodCall(node);
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression is { } receiver && IsEntityBuilderType(receiver.Type))
            {
                throw new QueryPreparationException(
                    $"SelectGroup {clause} contains an unsupported subquery expression: {node}.");
            }

            return base.VisitMember(node);
        }

        private static bool IsWindowCall(MethodCallExpression node)
        {
            var declaringType = node.Method.DeclaringType;
            if (declaringType is { IsGenericType: true }
                && declaringType.GetGenericTypeDefinition() == typeof(WindowFunction<>))
            {
                return true;
            }

            if (declaringType == typeof(SqlServerFunctions)
                && node.Method.Name is nameof(SqlServerFunctions.median)
                    or nameof(SqlServerFunctions.percentile))
            {
                return true;
            }

            // A window member that has not been completed with Over(...) still produces a
            // WindowFunction<T> value; it can never be a scalar key, so fail closed instead of
            // rendering an invalid GROUP BY.
            var returnType = node.Method.ReturnType;
            return returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(WindowFunction<>);
        }

        private static bool IsSubqueryCall(MethodCallExpression node)
        {
            var declaringType = node.Method.DeclaringType;
            var name = node.Method.Name;

            if ((declaringType == typeof(CommonFunctions)
                    || declaringType == typeof(ClickHouseFunctions))
                && name is nameof(CommonFunctions.exists)
                    or nameof(CommonFunctions.any)
                    or nameof(CommonFunctions.all)
                && node.Arguments.Count == 1
                && node.Arguments[0].Type.IsAssignableTo(typeof(QueryCommand)))
            {
                return true;
            }

            if (name is nameof(CommonFunctions.@in) or nameof(ClickHouseFunctions.global_in)
                && node.Arguments.Count > 1
                && node.Arguments[1].Type.IsAssignableTo(typeof(QueryCommand)))
            {
                return true;
            }

            if (node.Object?.Type.IsAssignableTo(typeof(QueryCommand)) == true)
                return true;

            if (node.Object is null
                && node.Arguments.Count > 0
                && node.Arguments[0].Type.IsAssignableTo(typeof(QueryCommand)))
            {
                return true;
            }

            var receiver = node.Object ?? (node.Arguments.Count > 0 ? node.Arguments[0] : null);
            return receiver is not null && IsEntityBuilderType(receiver.Type);
        }

        /// <summary>
        /// True when the type is <see cref="EntityBuilder{TEntity}"/> or derives from it; a joined
        /// builder derives from <c>EntityBuilder&lt;Projection&lt;...&gt;&gt;</c>, so the base chain is
        /// checked as well.
        /// </summary>
        private static bool IsEntityBuilderType(Type type)
        {
            for (var current = type; current is not null; current = current.BaseType)
            {
                if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(EntityBuilder<>))
                    return true;
            }

            return false;
        }
    }

    /// <summary>Depth-first aggregate-call detection with early exit.</summary>
    private sealed class AggregateFinder : ExpressionVisitor
    {
        public bool Found { get; private set; }

        public override Expression? Visit(Expression? node)
        {
            if (Found || node is null)
                return node;

            if (IsAggregateCall(node))
            {
                Found = true;
                return node;
            }

            return base.Visit(node);
        }
    }

    /// <summary>Reference-based parameter occurrence detection with early exit.</summary>
    private sealed class ParameterFinder(ParameterExpression parameter) : ExpressionVisitor
    {
        public bool Found { get; private set; }

        public override Expression? Visit(Expression? node)
        {
            if (Found || node is null)
                return node;

            return base.Visit(node);
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (ReferenceEquals(node, parameter))
                Found = true;

            return node;
        }
    }

    /// <summary>
    /// Structural equality for grouping keys and coverage matching. Deliberately separate from
    /// <see cref="ExpressionPlanEqualityComparer"/>: keys are compared across independent lambdas, so
    /// parameters match by type (alpha-equivalence), literals by value, and captured closure targets by
    /// reference identity — a captured getter is never evaluated.
    /// </summary>
    internal sealed class SelectGroupExpressionComparer : IEqualityComparer<Expression>
    {
        public bool Equals(Expression? x, Expression? y)
        {
            if (ReferenceEquals(x, y))
                return true;

            if (x is null || y is null)
                return false;

            if (x.NodeType != y.NodeType || x.Type != y.Type)
                return false;

            switch (x)
            {
                case ConstantExpression xc when y is ConstantExpression yc:
                    return ConstantsEqual(xc, yc);
                case ParameterExpression:
                    // Parameters match by type only: each clause lambda declares its own instance.
                    return true;
                case MemberExpression xm when y is MemberExpression ym:
                    return xm.Member.Equals(ym.Member) && Equals(xm.Expression, ym.Expression);
                case MethodCallExpression xcall when y is MethodCallExpression ycall:
                    return xcall.Method.Equals(ycall.Method) && Equals(xcall.Object, ycall.Object)
                        && ArgumentsEqual(xcall.Arguments, ycall.Arguments);
                case UnaryExpression xu when y is UnaryExpression yu:
                    return Equals(xu.Method, yu.Method) && Equals(xu.Operand, yu.Operand);
                case BinaryExpression xb when y is BinaryExpression yb:
                    return Equals(xb.Method, yb.Method) && Equals(xb.Conversion, yb.Conversion)
                        && Equals(xb.Left, yb.Left) && Equals(xb.Right, yb.Right);
                case NewExpression xn when y is NewExpression yn:
                    return Equals(xn.Constructor, yn.Constructor) && ArgumentsEqual(xn.Arguments, yn.Arguments);
                case NewArrayExpression xa when y is NewArrayExpression ya:
                    return ArgumentsEqual(xa.Expressions, ya.Expressions);
                case MemberInitExpression xi when y is MemberInitExpression yi:
                    return Equals(xi.NewExpression, yi.NewExpression) && BindingsEqual(xi.Bindings, yi.Bindings);
                case ConditionalExpression xcond when y is ConditionalExpression ycond:
                    return Equals(xcond.Test, ycond.Test) && Equals(xcond.IfTrue, ycond.IfTrue)
                        && Equals(xcond.IfFalse, ycond.IfFalse);
                case InvocationExpression xinv when y is InvocationExpression yinv:
                    return Equals(xinv.Expression, yinv.Expression) && ArgumentsEqual(xinv.Arguments, yinv.Arguments);
                case TypeBinaryExpression xt when y is TypeBinaryExpression yt:
                    return xt.TypeOperand == yt.TypeOperand && Equals(xt.Expression, yt.Expression);
                case LambdaExpression xl when y is LambdaExpression yl:
                    return ParametersEqual(xl.Parameters, yl.Parameters) && Equals(xl.Body, yl.Body);
                case IndexExpression xidx when y is IndexExpression yidx:
                    return Equals(xidx.Indexer, yidx.Indexer) && Equals(xidx.Object, yidx.Object)
                        && ArgumentsEqual(xidx.Arguments, yidx.Arguments);
                default:
                    // Unknown node kinds are never considered equal: an opaque comparison could merge
                    // two different keys and silently drop a real grouping key.
                    return false;
            }
        }

        public int GetHashCode(Expression? obj)
        {
            if (obj is null)
                return 0;

            unchecked
            {
                var hash = new HashCode();
                hash.Add((int)obj.NodeType);
                hash.Add(obj.Type);

                switch (obj)
                {
                    case ConstantExpression constant:
                        if (constant.Value is null)
                            break;
                        if (IsLiteralValue(constant.Value))
                            hash.Add(constant.Value);
                        else
                            hash.Add(RuntimeHelpers.GetHashCode(constant.Value));
                        break;
                    case MemberExpression member:
                        hash.Add(member.Member);
                        break;
                    case MethodCallExpression call:
                        hash.Add(call.Method);
                        break;
                    case NewExpression ctor:
                        hash.Add(ctor.Constructor);
                        break;
                }

                return hash.ToHashCode();
            }
        }

        private static bool ConstantsEqual(ConstantExpression x, ConstantExpression y)
        {
            if (x.Value is null || y.Value is null)
                return x.Value is null && y.Value is null;

            return IsLiteralValue(x.Value)
                ? Equals(x.Value, y.Value)
                : ReferenceEquals(x.Value, y.Value);
        }

        private static bool IsLiteralValue(object value) => value
            is string
            or bool or char
            or byte or sbyte or short or ushort or int or uint or long or ulong
            or float or double or decimal
            or DateTime or DateTimeOffset or TimeSpan or Guid;

        private static bool ParametersEqual(IReadOnlyList<ParameterExpression> x, IReadOnlyList<ParameterExpression> y)
        {
            if (x.Count != y.Count)
                return false;

            for (var i = 0; i < x.Count; i++)
            {
                if (x[i].Type != y[i].Type)
                    return false;
            }

            return true;
        }

        private bool ArgumentsEqual(IReadOnlyList<Expression> x, IReadOnlyList<Expression> y)
        {
            if (x.Count != y.Count)
                return false;

            for (var i = 0; i < x.Count; i++)
            {
                if (!Equals(x[i], y[i]))
                    return false;
            }

            return true;
        }

        private bool BindingsEqual(IReadOnlyList<MemberBinding> x, IReadOnlyList<MemberBinding> y)
        {
            if (x.Count != y.Count)
                return false;

            for (var i = 0; i < x.Count; i++)
            {
                if (x[i] is not MemberAssignment xa || y[i] is not MemberAssignment ya)
                    return false;

                if (!xa.Member.Equals(ya.Member) || !Equals(xa.Expression, ya.Expression))
                    return false;
            }

            return true;
        }
    }
}
