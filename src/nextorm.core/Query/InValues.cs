using System.Collections;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace NextORM.Core;

/// <summary>
/// The evaluated rows of a tuple-valued <c>in</c>/<c>Contains</c> node: the shared tuple arity and the
/// component values of every row in source order. A SQL-null component is preserved as a null cell so the
/// renderer can emit the explicit <c>IS NULL</c>/guarded-equality arms.
/// </summary>
internal readonly struct InValuesTuplePartition
{
    public readonly int Arity;
    public readonly List<object?[]> Rows;

    public InValuesTuplePartition(int arity, List<object?[]> rows)
    {
        Arity = arity;
        Rows = rows;
    }
}

/// <summary>
/// The evaluated (and partitioned) values of one <c>in</c>/<c>Contains</c> node: the non-null values
/// in source order plus whether the source also contained a null. For a tuple-valued node
/// <see cref="Tuple"/> carries the per-row component values instead and <see cref="NonNull"/> is empty.
/// </summary>
internal readonly struct InValuesPartition
{
    private static readonly List<object?> Empty = [];

    public readonly List<object?> NonNull;
    public readonly bool HasNull;
    public readonly InValuesTuplePartition? Tuple;

    public InValuesPartition(List<object?> nonNull, bool hasNull)
    {
        NonNull = nonNull;
        HasNull = hasNull;
        Tuple = null;
    }

    internal InValuesPartition(InValuesTuplePartition tuple)
    {
        NonNull = Empty;
        HasNull = false;
        Tuple = tuple;
    }
}

/// <summary>
/// Shared, build-time handling of value-list membership tests.
/// <para>
/// The SQL text and parameter layout of an <c>in</c> predicate depend on the number of values and on
/// whether the collection contains a null, but a captured collection contributes none of that to the
/// expression-tree plan key (the closure member access is shape independent). <see cref="ComputeShapeHash"/>
/// walks the prepared condition, evaluates every value list once, and folds that shape into
/// <see cref="QueryCommand.InValuesShapeHash"/> so the plan cache key distinguishes different lengths.
/// The evaluated partitions are handed to the renderer so the collection is not read twice during a
/// single build.
/// </para>
/// </summary>
internal static class InValues
{
    /// <summary>
    /// The single pinned rejection message for a provider that has no row-value constructor (SQL
    /// Server). Shared by the preparation-time provider preflight and the render-time defensive guard so
    /// both emit exactly the same text.
    /// </summary>
    internal const string SqlServerTupleInNotSupportedMessage = "SQL Server does not support tuple IN/Contains translation.";

    /// <summary>
    /// The single pinned rejection message for a nullable tuple element
    /// (<see cref="Nullable{T}"/> wrapping a value tuple), which has no null-safe row shape.
    /// </summary>
    internal const string NullableTupleElementNotSupportedMessage = "Tuple IN/Contains does not support nullable tuple elements; use a non-nullable value tuple.";

    /// <summary>
    /// Metadata-only probe: true when <paramref name="expression"/> contains a tuple-valued
    /// <c>IN</c>/<c>Contains</c> node. It never evaluates or enumerates the collection, so it is safe to
    /// call before <see cref="PartitionTuple"/> (for example to reject an unsupported provider before a
    /// null collection would surface an <see cref="ArgumentNullException"/>).
    /// </summary>
    internal static bool ContainsTupleInValues(Expression expression)
    {
        var probe = new TupleInValuesProbe();
        probe.Visit(expression);
        return probe.Found;
    }

    private sealed class TupleInValuesProbe : ExpressionVisitor
    {
        public bool Found { get; private set; }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (!Found
                && TryGetArguments(node, out _, out _, out var elementType, out _)
                && TypeFacts.IsTupleFamily(elementType))
                Found = true;

            return base.VisitMethodCall(node);
        }
    }

    /// <summary>
    /// True when the value an expression folds to is fully determined by the expression shape, i.e.
    /// it cannot change between two executions of a cached plan. Only inline arrays of immutable
    /// values and value-type <c>new</c> expressions over immutable arguments qualify: a captured
    /// member access, a method call (for example <c>Guid.NewGuid()</c>) or a reference-typed constant
    /// (a <see cref="ConstantExpression"/> wrapping a mutable array) may change, so the cached
    /// parameter must be refreshed.
    /// </summary>
    public static bool IsStableValueExpression(Expression expression)
    {
        switch (expression)
        {
            case ConstantExpression constant:
                if (constant.Value is null) return true;
                var type = constant.Value.GetType();
                return type.IsValueType || type == typeof(string);

            case NewArrayExpression array:
                for (var (i, cnt) = (0, array.Expressions.Count); i < cnt; i++)
                {
                    if (!IsStableValueExpression(array.Expressions[i])) return false;
                }
                return true;

            case NewExpression @new when @new.Type.IsValueType:
                for (var (i, cnt) = (0, @new.Arguments.Count); i < cnt; i++)
                {
                    if (!IsStableValueExpression(@new.Arguments[i])) return false;
                }
                return true;

            default:
                return false;
        }
    }
    /// <summary>
    /// Matches a value-list <c>SqlFunctions.Sql.@in(column, collection)</c> or the ClickHouse
    /// <c>global_in</c> (the subquery overloads are excluded) or an <c>Enumerable.Contains</c> /
    /// <c>MemoryExtensions.Contains</c> / instance <c>Contains</c> on a non-string collection.
    /// <paramref name="isGlobal"/> is true for the <c>global_in</c> form.
    /// </summary>
    public static bool TryGetArguments(MethodCallExpression node, out Expression columnExp, out Expression valuesExp, out Type elementType, out bool isGlobal)
    {
        columnExp = null!;
        valuesExp = null!;
        elementType = null!;
        isGlobal = false;

        if ((node.Method.DeclaringType == typeof(CommonFunctions)
                || node.Method.DeclaringType == typeof(ClickHouseFunctions))
            && (node.Method.Name == nameof(CommonFunctions.@in)
                || node.Method.Name == nameof(ClickHouseFunctions.global_in)))
        {
            if (node.Arguments is not [Expression inColumn, Expression inValues]
                || inValues.Type.IsAssignableTo(typeof(QueryCommand)))
                return false;

            columnExp = inColumn;
            valuesExp = inValues;
            elementType = node.Method.GetGenericArguments()[0];
            isGlobal = node.Method.Name == nameof(ClickHouseFunctions.global_in);
            return true;
        }

        return TryGetContainsArguments(node, out columnExp, out valuesExp, out elementType);
    }

    private static bool TryGetContainsArguments(MethodCallExpression node, out Expression columnExp, out Expression valuesExp, out Type elementType)
    {
        columnExp = null!;
        valuesExp = null!;
        elementType = null!;

        if (node.Method.Name != nameof(Enumerable.Contains))
            return false;

        // string.Contains is handled by the string translator; an instance Contains on string with a
        // char argument would otherwise look like a collection membership test.
        if (node.Object?.Type == typeof(string))
            return false;

        if (node.Object is not null)
        {
            if (node.Arguments.Count != 1)
                return false;

            elementType = node.Method.GetParameters()[0].ParameterType;
            if (!TryGetEnumerableElementType(node.Object.Type, out var collectionElementType)
                || collectionElementType != elementType)
                return false;

            columnExp = node.Arguments[0];
            valuesExp = node.Object;
        }
        else
        {
            // Arrays bind to the span overload (MemoryExtensions.Contains) on modern runtimes and to
            // Enumerable.Contains otherwise; both are the same membership test here. A three-argument
            // span overload carries an IEqualityComparer and is accepted only when it is a constant null
            // (the default equality), which is the same membership test.
            var declaringType = node.Method.DeclaringType;
            if (declaringType != typeof(Enumerable) && declaringType != typeof(MemoryExtensions))
                return false;

            Expression source;
            if (node.Arguments.Count == 2)
                source = node.Arguments[0];
            else if (node.Arguments.Count == 3 && node.Arguments[2] is ConstantExpression { Value: null })
                source = node.Arguments[0];
            else
                return false;

            var genericArgs = node.Method.GetGenericArguments();
            if (genericArgs.Length != 1)
                return false;

            elementType = genericArgs[0];
            valuesExp = UnwrapSpanConversion(source);
            columnExp = node.Arguments[1];
        }

        // Only a captured collection can be materialised at translation time.
        if (valuesExp.Has<ParameterExpression>())
            return false;

        // Without a parameter on the value side the whole call is a constant and is folded elsewhere.
        if (!columnExp.Has<ParameterExpression>())
            return false;

        return true;
    }

    public static InValuesPartition EvaluatePartition(Expression valuesExp, IQueryRegistry queryProvider, Type? elementType = null)
    {
        var value = InValuesEvaluator.Evaluate(valuesExp, queryProvider);

        return elementType is not null && TypeFacts.IsTupleFamily(elementType)
            ? PartitionTuple(value, elementType)
            : Partition(value);
    }

    /// <summary>
    /// Evaluates a tuple-valued value list into its rows. Rejects a null collection
    /// (<see cref="ArgumentNullException"/>), a nullable tuple element, an arity outside the flat renderer
    /// scope (1..7, including the 8-element <c>Rest</c> form), tuple-valued nested components and a null
    /// reference-tuple entry, so an unsupported shape never silently degrades into an all-null row or a
    /// single opaque tuple parameter.
    /// </summary>
    public static InValuesPartition PartitionTuple(object? value, Type elementType)
    {
        // A nullable value-tuple element is recognisable as a tuple family (so it routes away from the
        // scalar path), but the renderer has no null-safe row shape for it: reject it explicitly instead
        // of silently treating the whole row as one opaque scalar/null.
        if (Nullable.GetUnderlyingType(elementType) is not null)
            throw new NotSupportedException(NullableTupleElementNotSupportedMessage);

        if (!TypeFacts.IsTupleLike(elementType))
            throw new NotSupportedException(
                $"Tuple IN/Contains supports flat System.Tuple/System.ValueTuple collections of arity 1..7; '{elementType.Name}' is not supported.");

        var componentTypes = elementType.GetGenericArguments();
        for (var i = 0; i < componentTypes.Length; i++)
        {
            if (TypeFacts.IsTupleFamily(componentTypes[i]))
                throw new NotSupportedException("Tuple IN/Contains does not support tuple-valued nested components.");
        }

        if (value is null)
            throw new ArgumentNullException(nameof(value), "The tuple collection of an IN/Contains predicate must not be null.");

        if (value is not IEnumerable enumerable)
            throw new NotSupportedException("A tuple IN/Contains requires an enumerable collection of tuples.");

        var arity = componentTypes.Length;
        var rows = value is ICollection collection ? new List<object?[]>(collection.Count) : [];
        foreach (var item in enumerable)
            rows.Add(ExtractTupleRow(item, arity));

        return new InValuesPartition(new InValuesTuplePartition(arity, rows));
    }

    private static object?[] ExtractTupleRow(object? item, int arity)
    {
        if (item is null)
            throw new NotSupportedException(
                "A null tuple entry in a tuple IN/Contains collection is not supported; use a non-null tuple entry or filter it out.");

        if (item is not ITuple tuple || tuple.Length != arity)
            throw new NotSupportedException("A tuple IN/Contains collection must contain tuples of the same arity as the left-hand side.");

        var row = new object?[arity];
        for (var i = 0; i < arity; i++)
            row[i] = tuple[i];

        return row;
    }

    public static InValuesPartition Partition(object? value)
    {
        List<object?> nonNull;
        var hasNull = false;

        if (value is IEnumerable enumerable)
        {
            // One list instead of the previous value-list + partition pair; pre-sized when the source
            // exposes its count so the common array/list case does not grow the backing array.
            nonNull = value is ICollection collection ? new List<object?>(collection.Count) : [];
            foreach (var item in enumerable)
            {
                if (item is null)
                    hasNull = true;
                else
                    nonNull.Add(item);
            }
        }
        else if (value is not null)
        {
            nonNull = [value];
        }
        else
        {
            nonNull = [];
        }

        return new InValuesPartition(nonNull, hasNull);
    }

    /// <summary>
    /// Evaluates every shape-dependent captured collection of <paramref name="condition"/> and returns
    /// a hash of their shapes, or <c>0</c> when the condition has none. This covers both value-list
    /// (<c>in</c>/<c>Contains</c>) nodes and captured collections indexed by a query expression
    /// (<c>dict[column]</c>); the evaluated value-list partitions are returned and the evaluated
    /// lookups are stored on <paramref name="command"/> for the renderer to reuse.
    /// </summary>
    /// <param name="condition">The prepared condition to scan.</param>
    /// <param name="command">The command whose plan key the shape belongs to.</param>
    /// <param name="partitions">The evaluated value-list partitions, or <c>null</c>.</param>
    /// <param name="hasMatch">Whether the condition contained any value-list or lookup node.</param>
    /// <returns>The shape hash, or <c>0</c> when nothing matched.</returns>
    public static int ComputeShapeHash(Expression condition, QueryCommand command, out Dictionary<Expression, InValuesPartition>? partitions, out bool hasMatch)
    {
        var visitor = new ShapeVisitor(command);
        visitor.Visit(condition);
        partitions = visitor.Partitions;
        hasMatch = visitor.HasMatch;
        return visitor.HasMatch ? visitor.Hash : 0;
    }

    private sealed class ShapeVisitor(QueryCommand command) : ExpressionVisitor
    {
        private XxHash32 _hash = new();
        public Dictionary<Expression, InValuesPartition>? Partitions { get; private set; }
        public bool HasMatch { get; private set; }
        public int Hash => _hash.ToHashCode();

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (TryGetArguments(node, out _, out var valuesExp, out var elementType, out _))
            {
                var partition = EvaluatePartition(valuesExp, command, elementType);
                Partitions ??= new Dictionary<Expression, InValuesPartition>(ReferenceEqualityComparer.Instance);
                Partitions[valuesExp] = partition;

                // A tuple value list is shaped by its arity, row count and per-cell null layout (never by
                // the runtime values), so two collections that produce the same SQL share a plan while a
                // changed null layout re-keys it.
                if (partition.Tuple is { } tuple)
                {
                    _hash.Add(tuple.Arity);
                    _hash.Add(tuple.Rows.Count);
                    for (var r = 0; r < tuple.Rows.Count; r++)
                    {
                        var row = tuple.Rows[r];
                        for (var c = 0; c < row.Length; c++)
                            _hash.Add(row[c] is null);
                    }
                }
                else
                {
                    _hash.Add(partition.NonNull.Count);
                    _hash.Add(partition.HasNull);
                }

                HasMatch = true;
            }

            CheckLookup(node);
            return base.VisitMethodCall(node);
        }

        protected override Expression VisitIndex(IndexExpression node)
        {
            CheckLookup(node);
            return base.VisitIndex(node);
        }

        protected override Expression VisitBinary(BinaryExpression node)
        {
            if (node.NodeType == ExpressionType.ArrayIndex)
                CheckLookup(node);

            return base.VisitBinary(node);
        }

        // A captured collection indexed by a query expression (dict[column]) is rendered as a CASE whose
        // branch count depends on the collection, so its entry count has to travel in the plan key
        // exactly like an in-list length. Only a query-dependent key needs this: a constant key is
        // folded to a parameter and its shape is already in the expression tree.
        private void CheckLookup(Expression node)
        {
            if (!DictionaryLookup.TryGetLookup(node, out var collectionExp, out var keyExp)
                || !keyExp.Has<ParameterExpression>())
                return;

            var entries = DictionaryLookup.Evaluate(collectionExp, command);
            command.LookupPartitions ??= new Dictionary<Expression, List<LookupEntry>>(ReferenceEqualityComparer.Instance);
            command.LookupPartitions[node] = entries;
            _hash.Add(entries.Count);
            HasMatch = true;
        }
    }

    private static bool TryGetEnumerableElementType(Type type, out Type elementType)
    {
        if (type.IsArray)
        {
            elementType = type.GetElementType()!;
            return true;
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            elementType = type.GetGenericArguments()[0];
            return true;
        }

        foreach (var interfaceType in type.GetInterfaces())
        {
            if (interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                elementType = interfaceType.GetGenericArguments()[0];
                return true;
            }
        }

        elementType = null!;
        return false;
    }

    private static Expression UnwrapSpanConversion(Expression expression)
    {
        // MemoryExtensions.Contains takes a ReadOnlySpan<T>, so an array argument is wrapped in a
        // Convert or an op_Implicit call; the underlying collection is what has to be materialised.
        return expression switch
        {
            UnaryExpression { NodeType: ExpressionType.Convert } unary => unary.Operand,
            MethodCallExpression { Method.Name: "op_Implicit", Arguments.Count: 1 } opImplicit => opImplicit.Arguments[0],
            _ => expression
        };
    }
}
