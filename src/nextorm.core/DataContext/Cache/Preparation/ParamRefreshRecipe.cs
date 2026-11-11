using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;

namespace NextORM.Core;

/// <summary>
/// Immutable, guarded recipe that re-binds a cached plan's captured scalar parameters from the fresh
/// closure instances of a re-built command, without re-rendering the statement.
/// </summary>
/// <remarks>
/// The positional binding plan is computed once, when the recipe is built on a cache miss: per
/// parameter it stores the navigation path from the prepared expression root to the captured closure
/// constant, plus a compiled writer that reads the captured member at its natural type and assigns it
/// to the cached <see cref="DbParameter"/> (one boxing conversion at most). A cache hit therefore does
/// no expression visit, allocates no <see cref="Dictionary{TKey,TValue}"/>/collector and no value
/// array, and never retains the miss-time closure, a mutable <see cref="DbCommand"/> or an enumerator.
/// <para>
/// Only the provably-equivalent scalar subset is supported: every refreshed parameter must come from a
/// captured member access whose root is a constant (typically a compiler-generated) closure, the member
/// name must be the parameter name, no value converter/duration/stability/runtime parameter may be
/// involved, and the command must have no joins, grouping, HAVING, PREWHERE, sorting, windows, array
/// joins, CTEs, unions or outer references. A referenced subquery is supported only for the shared
/// terminal wrapper (Any/Count/All), whose captured predicate is rendered inside that subquery; the
/// recipe then navigates the subquery's own roots on every bind. Anything else leaves the cached command
/// without a recipe, and the original <c>ExtractParams</c> path runs unchanged.
/// </para>
/// <para>
/// All compatibility checks (parameter count/name/order against the cached collection and the captured
/// shape) run before any captured member is read, and the shape is validated for every slot before the
/// first write, so a mismatch falls back without evaluating a single closure or corrupting the bind.
/// </para>
/// </remarks>
internal sealed class ParamRefreshRecipe
{
    private readonly string[] _names;
    // Per-path provenance: which expression of the fresh command the navigation path must be walked
    // from. A capture may live in the command's own condition or projection, or — for the shared
    // terminal wrapper (Any/Count/All) — inside a referenced query's condition or projection. Binding
    // from the wrong root would either resolve a foreign constant or throw.
    private readonly RootKind[] _rootKinds;
    // The referenced-query index when the kind addresses one; 0 otherwise.
    private readonly int[] _rootIndices;
    // The miss-time constant (display-class) type of each path, validated before any value is written.
    private readonly Type[] _closureTypes;
    private readonly Func<Expression, Expression>[][] _paths;
    private readonly Action<object?, DbParameter>[] _writers;

    // Cached-path diagnostic counters. Process-wide but updated with Interlocked and read with
    // Volatile, so the increment path is lock-free and allocation-free. Tests reach them through the
    // existing InternalsVisibleTo("nextorm.core.tests") and reset them via ResetCounters().
    private static int _fastBindHits;
    private static int _bindRejected;

    /// <summary>Number of cache hits bound through the fast positional recipe. Diagnostic only.</summary>
    internal static int FastBindHits => Volatile.Read(ref _fastBindHits);

    /// <summary>Number of cache-hit binds the recipe rejected (each early <see langword="false"/>).</summary>
    internal static int BindRejected => Volatile.Read(ref _bindRejected);

    /// <summary>Resets this recipe's cached-path diagnostic counters to zero. Test seam.</summary>
    internal static void ResetCounters()
    {
        Volatile.Write(ref _fastBindHits, 0);
        Volatile.Write(ref _bindRejected, 0);
    }

    private ParamRefreshRecipe(
        string[] names,
        RootKind[] rootKinds,
        int[] rootIndices,
        Type[] closureTypes,
        Func<Expression, Expression>[][] paths,
        Action<object?, DbParameter>[] writers)
    {
        _names = names;
        _rootKinds = rootKinds;
        _rootIndices = rootIndices;
        _closureTypes = closureTypes;
        _paths = paths;
        _writers = writers;
    }

    /// <summary>The expression of the fresh command a captured path is walked from.</summary>
    private enum RootKind
    {
        /// <summary>The command's own prepared condition.</summary>
        Condition,
        /// <summary>The command's own prepared projection.</summary>
        Projection,
        /// <summary>The prepared condition of the referenced query at <c>_rootIndices[i]</c>.</summary>
        ReferencedCondition,
        /// <summary>The prepared projection of the referenced query at <c>_rootIndices[i]</c>.</summary>
        ReferencedProjection,
    }

    /// <summary>The number of parameters the recipe refreshes.</summary>
    internal int Count => _names.Length;

    /// <summary>
    /// Builds a recipe for <paramref name="queryCommand"/>, or returns <see langword="null"/> when the
    /// command/parameter pair is not in the provably-equivalent scalar subset.
    /// </summary>
    /// <param name="queryCommand">The command whose miss-time render produced <paramref name="parameters"/>.</param>
    /// <param name="parameters">The ordered parameters produced by the miss-time render.</param>
    /// <returns>The recipe, or <see langword="null"/> when the original extraction path must be used.</returns>
    internal static ParamRefreshRecipe? TryCreate(QueryCommand queryCommand, List<Parameter> parameters)
    {
        if (parameters.Count == 0)
            return null;

        if (!HasSupportedShape(queryCommand))
            return null;

        for (var i = 0; i < parameters.Count; i++)
        {
            var parameter = parameters[i];
            // Only a captured closure scalar is supported: no fixed (stable) value, no converter or
            // duration normalization, no runtime placeholder, and a structural captured key.
            if (parameter.Stable
                || parameter.HasConversion
                || parameter.CapturedKey is null
                || NormParam.IsName(parameter.Name))
            {
                return null;
            }
        }

        var captures = new Dictionary<string, Capture>(StringComparer.Ordinal);
        if (queryCommand.PreparedCondition is { } ownCondition
            && !TryCollect(ownCondition, RootKind.Condition, -1, captures))
            return null;

        if (queryCommand.ProjectionExpression is { } projection
            && !TryCollect(projection, RootKind.Projection, -1, captures))
            return null;

        // The shared terminal wrapper (Any/Count/All) renders its captured predicate inside the
        // referenced subquery, not on its own condition, so the captures are collected from there as
        // well. The referenced query is swapped in place between executions (ReplaceCommand), so the
        // recipe navigates the CURRENT instance's prepared roots on every bind.
        if (queryCommand.ReferencedQueries is { Count: > 0 } referenced)
        {
            for (var r = 0; r < referenced.Count; r++)
            {
                var body = referenced[r];
                if (body.PreparedCondition is { } referencedCondition
                    && !TryCollect(referencedCondition, RootKind.ReferencedCondition, r, captures))
                    return null;

                if (body.ProjectionExpression is { } referencedProjection
                    && !TryCollect(referencedProjection, RootKind.ReferencedProjection, r, captures))
                    return null;
            }
        }

        // Every parameter must resolve to exactly one unambiguous captured member, and no captured
        // member may be left over that the parameter list does not use (otherwise a capture came from a
        // path this recipe does not model).
        if (captures.Count != parameters.Count)
            return null;

        var names = new string[parameters.Count];
        var rootKinds = new RootKind[parameters.Count];
        var rootIndices = new int[parameters.Count];
        var closureTypes = new Type[parameters.Count];
        var paths = new Func<Expression, Expression>[parameters.Count][];
        var writers = new Action<object?, DbParameter>[parameters.Count];
        for (var i = 0; i < parameters.Count; i++)
        {
            var name = parameters[i].Name;
            if (!captures.TryGetValue(name, out var capture))
                return null;

            var path = BuildPath(capture.SourceRoot, capture.Constant);
            if (path is null)
                return null;

            names[i] = name;
            paths[i] = path;
            // Proven provenance collected at miss time: the same path is walked from the same kind of
            // root on a hit, so a capture collected from a referenced subquery is never navigated from
            // the wrapper's condition (which would misbind or throw).
            rootKinds[i] = capture.RootKind;
            rootIndices[i] = capture.RootIndex;
            closureTypes[i] = capture.ClosureType;
            writers[i] = CompileWriter(capture.Node, capture.ClosureType);
        }

        return new ParamRefreshRecipe(names, rootKinds, rootIndices, closureTypes, paths, writers);
    }

    /// <summary>
    /// Re-binds the cached command's parameters from the fresh closures of <paramref name="queryCommand"/>.
    /// Returns <see langword="false"/> (after evaluating nothing) when any compatibility check fails, so
    /// the caller can run the original extraction path.
    /// </summary>
    /// <param name="queryCommand">The freshly built command matching the cached plan.</param>
    /// <param name="dbCommandParams">The cached command's parameter collection, in placeholder order.</param>
    /// <returns><see langword="true"/> when the parameters were refreshed; otherwise <see langword="false"/>.</returns>
    internal bool TryBind(QueryCommand queryCommand, DbParameterCollection dbCommandParams)
    {
        if (TryBindCore(queryCommand, dbCommandParams))
        {
            Interlocked.Increment(ref _fastBindHits);
            return true;
        }

        Interlocked.Increment(ref _bindRejected);
        return false;
    }

    private bool TryBindCore(QueryCommand queryCommand, DbParameterCollection dbCommandParams)
    {
        // Validate the bound shape against the cached collection before touching any captured value.
        if (dbCommandParams.Count != _names.Length)
            return false;

        for (var i = 0; i < _names.Length; i++)
        {
            if (!string.Equals(dbCommandParams[i].ParameterName, _names[i], StringComparison.Ordinal))
                return false;
        }

        // Pass 1: navigate every path and validate it reaches the expected constant. This reads no
        // captured member, so a malformed recipe cannot leave a partial bind behind.
        for (var i = 0; i < _names.Length; i++)
        {
            if (!TryRoot(queryCommand, i, out var root)
                || !TryResolveClosure(root, _paths[i], _closureTypes[i], out _))
            {
                return false;
            }
        }

        // Pass 2: read each captured member exactly once and assign it to the cached parameter.
        for (var i = 0; i < _names.Length; i++)
        {
            _ = TryRoot(queryCommand, i, out var root);
            _ = TryResolveClosure(root, _paths[i], _closureTypes[i], out var closure);
            _writers[i](closure, dbCommandParams[i]);
        }

        return true;
    }

    // Selects the fresh root a path was built against: the command's own condition/projection, or the
    // condition/projection of the referenced subquery the capture came from. Null means the fresh
    // command cannot supply that root, so the recipe rejects instead of binding a foreign value.
    private bool TryRoot(QueryCommand queryCommand, int index, out Expression root)
    {
        switch (_rootKinds[index])
        {
            case RootKind.Condition:
                if (queryCommand.PreparedCondition is null)
                {
                    root = null!;
                    return false;
                }

                root = queryCommand.PreparedCondition;
                return true;

            case RootKind.Projection:
                if (queryCommand.ProjectionExpression is null)
                {
                    root = null!;
                    return false;
                }

                root = queryCommand.ProjectionExpression;
                return true;

            case RootKind.ReferencedCondition:
            case RootKind.ReferencedProjection:
                if (queryCommand.ReferencedQueries is not { } referenced
                    || _rootIndices[index] >= referenced.Count)
                {
                    root = null!;
                    return false;
                }

                var body = referenced[_rootIndices[index]];
                var expression = _rootKinds[index] == RootKind.ReferencedCondition
                    ? body.PreparedCondition
                    : body.ProjectionExpression;
                if (expression is null)
                {
                    root = null!;
                    return false;
                }

                root = expression;
                return true;

            default:
                root = null!;
                return false;
        }
    }

    private static bool TryResolveClosure(Expression root, Func<Expression, Expression>[] path, Type closureType, out object? closure)
    {
        try
        {
            Expression? node = root;
            for (var s = 0; s < path.Length; s++)
                node = path[s](node);

            if (node is ConstantExpression constant && constant.Value is not null && constant.Type == closureType)
            {
                closure = constant.Value;
                return true;
            }
        }
        catch (InvalidCastException)
        {
            // A fresh root whose node kinds differ from the miss-time shape cannot be walked by this
            // path; reject the recipe and fall back instead of crashing the cache hit.
        }

        closure = null;
        return false;
    }

    private static bool HasSupportedShape(QueryCommand queryCommand)
    {
        // Captures may only come from a scalar WHERE and (optionally) the projection — of the command
        // itself or of a referenced subquery (the shared Any/Count/All wrapper renders the captured
        // predicate there). Every other parameter-bearing clause is out of the supported subset, and a
        // referenced subquery must be a plain scalar body in its own right.
        if (!IsSupportedBody(queryCommand, allowReferenced: true))
            return false;

        if (queryCommand.ReferencedQueries is { Count: > 0 } referenced)
        {
            for (var i = 0; i < referenced.Count; i++)
            {
                if (referenced[i] is null || !IsSupportedBody(referenced[i], allowReferenced: false))
                    return false;
            }
        }

        return true;
    }

    // The body restrictions of the supported scalar subset. A referenced subquery may not itself
    // reference further subqueries: a nested wrapper has no proven single root to bind from.
    private static bool IsSupportedBody(QueryCommand queryCommand, bool allowReferenced)
    {
        if (queryCommand.Joins is { Length: > 0 })
            return false;

        if (queryCommand.GroupingList is { Length: > 0 })
            return false;

        if (queryCommand.PreparedHaving is not null)
            return false;

        if (queryCommand.PreparedPreWhere is not null)
            return false;

        if (queryCommand.Sorting is { Length: > 0 })
            return false;

        if (queryCommand.Windows is { Count: > 0 })
            return false;

        if (queryCommand.ArrayJoinExpressions is { Count: > 0 })
            return false;

        if (queryCommand.Ctes is { Count: > 0 })
            return false;

        if (queryCommand.UnionQuery is not null)
            return false;

        if (!allowReferenced && queryCommand.ReferencedQueries is { Count: > 0 })
            return false;

        if (queryCommand.OuterReferences is { Count: > 0 })
            return false;

        return true;
    }

    private static bool TryCollect(Expression expression, RootKind rootKind, int rootIndex, Dictionary<string, Capture> captures)
    {
        var collector = new Collector(expression, rootKind, rootIndex, captures);
        collector.Visit(expression);
        return !collector.Failed;
    }

    // Finds the navigation path (a chain of child accessors) from the prepared expression root to the
    // captured closure constant node, so a hit can reach the fresh closure instance without a visitor
    // or a name lookup. Returns null for a node kind this recipe does not model, which routes the plan
    // to the original extraction path.
    private static Func<Expression, Expression>[]? BuildPath(Expression root, ConstantExpression target)
    {
        var steps = new List<Func<Expression, Expression>>();
        if (!TryBuildPath(root, target, steps))
            return null;

        return steps.ToArray();
    }

    private static bool TryBuildPath(Expression node, ConstantExpression target, List<Func<Expression, Expression>> steps)
    {
        if (ReferenceEquals(node, target))
            return true;

        foreach (var (getter, child) in GetChildren(node))
        {
            steps.Add(getter);
            if (TryBuildPath(child, target, steps))
                return true;

            steps.RemoveAt(steps.Count - 1);
        }

        return false;
    }

    private static IEnumerable<(Func<Expression, Expression> Getter, Expression Child)> GetChildren(Expression node)
    {
        switch (node)
        {
            case LambdaExpression lambda:
                yield return (e => ((LambdaExpression)e).Body, lambda.Body);
                break;
            case BinaryExpression binary:
                yield return (e => ((BinaryExpression)e).Left, binary.Left);
                yield return (e => ((BinaryExpression)e).Right, binary.Right);
                if (binary.Conversion is not null)
                    yield return (e => ((BinaryExpression)e).Conversion!, binary.Conversion);
                break;
            case MemberExpression member when member.Expression is not null:
                yield return (e => ((MemberExpression)e).Expression!, member.Expression);
                break;
            case UnaryExpression unary:
                yield return (e => ((UnaryExpression)e).Operand, unary.Operand);
                break;
            case MethodCallExpression call:
                if (call.Object is not null)
                    yield return (e => ((MethodCallExpression)e).Object!, call.Object);
                for (var i = 0; i < call.Arguments.Count; i++)
                {
                    var index = i;
                    yield return (e => ((MethodCallExpression)e).Arguments[index], call.Arguments[index]);
                }

                break;
            case ConditionalExpression conditional:
                yield return (e => ((ConditionalExpression)e).Test, conditional.Test);
                yield return (e => ((ConditionalExpression)e).IfTrue, conditional.IfTrue);
                yield return (e => ((ConditionalExpression)e).IfFalse, conditional.IfFalse);
                break;
            case NewExpression @new:
                for (var i = 0; i < @new.Arguments.Count; i++)
                {
                    var index = i;
                    yield return (e => ((NewExpression)e).Arguments[index], @new.Arguments[index]);
                }

                break;
            case TypeBinaryExpression typeBinary:
                yield return (e => ((TypeBinaryExpression)e).Expression, typeBinary.Expression);
                break;
            case IndexExpression index:
                if (index.Object is not null)
                    yield return (e => ((IndexExpression)e).Object!, index.Object);
                for (var i = 0; i < index.Arguments.Count; i++)
                {
                    var argIndex = i;
                    yield return (e => ((IndexExpression)e).Arguments[argIndex], index.Arguments[argIndex]);
                }

                break;
            case InvocationExpression invocation:
                yield return (e => ((InvocationExpression)e).Expression, invocation.Expression);
                for (var i = 0; i < invocation.Arguments.Count; i++)
                {
                    var argIndex = i;
                    yield return (e => ((InvocationExpression)e).Arguments[argIndex], invocation.Arguments[argIndex]);
                }

                break;
            case NewArrayExpression newArray:
                for (var i = 0; i < newArray.Expressions.Count; i++)
                {
                    var index = i;
                    yield return (e => ((NewArrayExpression)e).Expressions[index], newArray.Expressions[index]);
                }

                break;
        }
    }

    // A compiled writer that reads the captured member from the closure instance at its natural type
    // and assigns it to the cached parameter through a single boxing conversion (null normalizes to
    // DBNull, matching the original extraction path). It closes over no query state and does not retain
    // the miss-time closure.
    private static Action<object?, DbParameter> CompileWriter(MemberExpression node, Type closureType)
    {
        var closureParameter = Expression.Parameter(typeof(object), "closure");
        var dbParameter = Expression.Parameter(typeof(DbParameter), "parameter");

        var replace = new ReplaceConstantExpressionVisitor(Expression.Convert(closureParameter, closureType));
        var value = replace.Visit(node)!;

        var boxed = Expression.Convert(value, typeof(object));
        var normalized = Expression.Coalesce(boxed, Expression.Constant(DBNull.Value, typeof(object)));
        var valueProperty = typeof(DbParameter).GetProperty(nameof(DbParameter.Value))!;
        var assign = Expression.Assign(Expression.Property(dbParameter, valueProperty), normalized);
        return Expression.Lambda<Action<object?, DbParameter>>(assign, closureParameter, dbParameter).Compile();
    }

    /// <summary>
    /// Collects the captured closure member accesses of an expression, keyed by member name, and rejects
    /// a name that maps to more than one distinct closure instance or member.
    /// </summary>
    private sealed class Collector : ExpressionVisitor
    {
        private readonly Expression _sourceRoot;
        private readonly RootKind _rootKind;
        private readonly int _rootIndex;
        private readonly Dictionary<string, Capture> _captures;

        internal Collector(Expression sourceRoot, RootKind rootKind, int rootIndex, Dictionary<string, Capture> captures)
        {
            _sourceRoot = sourceRoot;
            _rootKind = rootKind;
            _rootIndex = rootIndex;
            _captures = captures;
        }

        internal bool Failed;

        protected override Expression VisitMember(MemberExpression node)
        {
            // A captured query object is a nested command reference, not a scalar value: it is rendered
            // by the registry (the referenced query), never bound as a parameter, so it must not count
            // as a capture that the parameter list has to explain.
            if (typeof(QueryCommand).IsAssignableFrom(node.Type))
                return node;

            if (node.Expression is not null && TryFindRootConstant(node, out var root))
            {
                var name = node.Member.Name;
                if (_captures.TryGetValue(name, out var existing))
                {
                    // A repeated name is only unambiguous when every occurrence reads the same member
                    // from the same closure instance.
                    if (existing.Member != node.Member
                        || existing.ClosureType != root.Type
                        || existing.RootKind != _rootKind
                        || existing.RootIndex != _rootIndex
                        || !ReferenceEquals(existing.Constant.Value, root.Value))
                    {
                        Failed = true;
                    }
                }
                else
                {
                    _captures[name] = new Capture(root.Type, node.Member, root, node, _sourceRoot, _rootKind, _rootIndex);
                }

                // The whole member subtree is one captured value; do not descend and record inner
                // members again.
                return node;
            }

            return base.VisitMember(node);
        }

        private static bool TryFindRootConstant(Expression expression, out ConstantExpression root)
        {
            var current = expression;
            while (current is MemberExpression member && member.Expression is not null)
                current = member.Expression;

            if (current is ConstantExpression constant)
            {
                root = constant;
                return true;
            }

            root = null!;
            return false;
        }
    }

    private sealed class Capture
    {
        internal Capture(Type closureType, MemberInfo member, ConstantExpression constant, MemberExpression node, Expression sourceRoot, RootKind rootKind, int rootIndex)
        {
            ClosureType = closureType;
            Member = member;
            Constant = constant;
            Node = node;
            SourceRoot = sourceRoot;
            RootKind = rootKind;
            RootIndex = rootIndex;
        }

        internal Type ClosureType { get; }
        internal MemberInfo Member { get; }
        internal ConstantExpression Constant { get; }
        internal MemberExpression Node { get; }
        internal Expression SourceRoot { get; }
        internal RootKind RootKind { get; }
        internal int RootIndex { get; }
    }
}
