using System.Collections;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace NextORM.Core;

/// <summary>
/// A prepared correlated subquery of an in-memory command: the inner query is executed once per outer
/// row with the outer values bound to the <c>OuterRefMarker</c> positions, and a terminal
/// (<c>exists</c>, <c>in</c> or a scalar <c>First</c>/<c>Single</c>) is applied to the rows it returns.
/// </summary>
/// <remarks>
/// The inner query is cloned and its outer-reference markers are rewritten to
/// <see cref="SqlFunctions.Parameter{T}(int)"/> placeholders, so the ordinary in-memory pipeline
/// (condition factories, aggregates, materialisation) binds the outer values from the
/// <c>object[]</c> passed to <see cref="Enumerate"/>. Plans are cached per command by
/// <see cref="InMemoryDataContext.GetCorrelatedPlan"/>.
/// </remarks>
internal sealed class InMemoryCorrelatedPlan
{
    /// <summary>The inner command this plan executes (kept for diagnostics and default selection).</summary>
    public required QueryCommand Command { get; init; }
    /// <summary>Creates an enumerator over the inner rows for the given outer values.</summary>
    public required Func<object?[], IEnumerator> Enumerate { get; init; }
    /// <summary>Whether the scalar terminal must reject more than one matching row (<c>Single</c>).</summary>
    public required bool Single { get; init; }
    /// <summary>Whether the scalar terminal yields <c>default</c> instead of throwing when no row matches.</summary>
    public required bool DefaultOnEmpty { get; init; }

    /// <summary>True when the inner query returns at least one row.</summary>
    public bool Any(object?[] values)
    {
        var enumerator = Enumerate(values);
        try
        {
            return enumerator.MoveNext();
        }
        finally
        {
            (enumerator as IDisposable)?.Dispose();
        }
    }

    /// <summary>Returns the first (or only) inner value, or the terminal's empty-result behaviour.</summary>
    public object? Scalar(object?[] values)
    {
        var enumerator = Enumerate(values);
        try
        {
            if (!enumerator.MoveNext())
            {
                if (DefaultOnEmpty) return DefaultOf(Command.ResultType!);
                if (Command.ResultType!.IsValueType && Nullable.GetUnderlyingType(Command.ResultType) is null)
                    throw new InvalidOperationException("Sequence contains no elements");
                return null;
            }

            var first = enumerator.Current;
            if (Single && enumerator.MoveNext())
                throw new InvalidOperationException("Sequence contains more than one element");

            return first;
        }
        finally
        {
            (enumerator as IDisposable)?.Dispose();
        }
    }

    /// <summary>True when <paramref name="value"/> occurs in the inner result set.</summary>
    public bool Contains(object?[] values, object? value)
    {
        var enumerator = Enumerate(values);
        try
        {
            while (enumerator.MoveNext())
            {
                if (ValueEquals(enumerator.Current, value))
                    return true;
            }

            return false;
        }
        finally
        {
            (enumerator as IDisposable)?.Dispose();
        }
    }

    private static object? DefaultOf(Type type)
        => type.IsValueType && Nullable.GetUnderlyingType(type) is null ? Activator.CreateInstance(type) : null;

    private static bool ValueEquals(object? a, object? b)
    {
        if (a is null || b is null)
            return a is null && b is null;

        if (a.GetType() == b.GetType())
            return a.Equals(b);

        if (IsNumeric(a) && IsNumeric(b))
            return NumericEquals(a, b);

        return a.Equals(b);
    }

    private static bool NumericEquals(object a, object b)
    {
        // Integral and decimal values are compared exactly through decimal; only a floating-point
        // operand falls back to double (where the usual binary precision applies).
        if (!IsFloating(a) && !IsFloating(b))
            return Convert.ToDecimal(a, CultureInfo.InvariantCulture) == Convert.ToDecimal(b, CultureInfo.InvariantCulture);

        return Convert.ToDouble(a, CultureInfo.InvariantCulture) == Convert.ToDouble(b, CultureInfo.InvariantCulture);
    }

    private static bool IsFloating(object value)
    {
        var type = Type.GetTypeCode(value.GetType());
        return type is TypeCode.Single or TypeCode.Double;
    }

    private static bool IsNumeric(object value)
    {
        var type = Type.GetTypeCode(value.GetType());
        return type is TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.UInt16
            or TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64
            or TypeCode.Single or TypeCode.Double or TypeCode.Decimal;
    }
}

/// <summary>
/// Builds <see cref="InMemoryCorrelatedPlan"/> instances: clones the inner command, binds the outer
/// references as runtime parameters and prepares it through the normal in-memory pipeline so
/// conditions, aggregates and projections are all evaluated with the outer row in scope.
/// </summary>
internal static class InMemoryCorrelatedEvaluator
{
    private static readonly MethodInfo BuildTypedMI = typeof(InMemoryCorrelatedEvaluator)
        .GetMethod(nameof(BuildTyped), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo GetPreparedMI = typeof(InMemoryDataContext)
        .GetMethod(nameof(InMemoryDataContext.GetPreparedQueryCommand))!;
    private static readonly MethodInfo GetEnumerableMI = typeof(InMemoryDataContext)
        .GetMethod(nameof(InMemoryDataContext.GetEnumerable))!;

    /// <summary>Builds (or returns) the plan for <paramref name="cmd"/> on <paramref name="context"/>.</summary>
    public static InMemoryCorrelatedPlan Build(InMemoryDataContext context, QueryCommand cmd)
    {
        var entityType = cmd.EntityType
            ?? throw new NotSupportedException("A correlated subquery without a source entity is not supported by the in-memory provider.");
        return (InMemoryCorrelatedPlan)Invoke(BuildTypedMI.MakeGenericMethod(entityType), null, [context, cmd])!;
    }

    private static InMemoryCorrelatedPlan BuildTyped<TInner>(InMemoryDataContext context, QueryCommand cmd)
    {
        var resultType = cmd.ResultType
            ?? throw new NotSupportedException("A correlated subquery without a result type is not supported by the in-memory provider.");

        if (context.Data[typeof(TInner)] is IAsyncEnumerable<TInner> and not IEnumerable<TInner>)
            throw new NotSupportedException("A correlated subquery over an async source is not supported by the in-memory provider.");

        var clone = cmd.CloneForCorrelatedEvaluation();

        if (clone.PreparedCondition is { } condition)
        {
            if (ContainsParameterPlaceholder(condition))
                throw new NotSupportedException("A correlated subquery that mixes outer references with explicit SqlFunctions.Parameter placeholders is not supported by the in-memory provider.");

            clone.PreparedCondition = new OuterReferenceToParameterVisitor().Visit(condition);
        }

        if (clone.SelectList is { } selectList)
        {
            foreach (var column in selectList)
            {
                if (column.Expression is null) continue;
                if (ContainsOuterReferenceMarker(column.Expression))
                    throw new NotSupportedException("A correlated subquery that references the outer row in its projection is not supported by the in-memory provider.");
                if (ContainsQueryRegistryLambda(column.Expression))
                    throw new NotSupportedException("Nested correlated subqueries (correlation depth greater than one) are not supported by the in-memory provider.");
            }
        }

        if (clone.PreparedHaving is not null)
            throw new NotSupportedException("A correlated subquery with HAVING is not supported by the in-memory provider.");

        if (clone.Sorting is { } sorting)
        {
            foreach (var item in sorting)
            {
                if (item.PreparedExpression is not null && ContainsOuterReferenceMarker(item.PreparedExpression))
                    throw new NotSupportedException("A correlated subquery that references the outer row in ORDER BY is not supported by the in-memory provider.");
            }
        }

        var prepared = Invoke(GetPreparedMI.MakeGenericMethod(resultType), context, [clone, false, false, CancellationToken.None])!;
        var getEnumerable = GetEnumerableMI.MakeGenericMethod(resultType);
        Func<object?[], IEnumerator> enumerate = values =>
            ((IEnumerable)Invoke(getEnumerable, context, [prepared, values])!).GetEnumerator();

        return new InMemoryCorrelatedPlan
        {
            Command = cmd,
            Enumerate = enumerate,
            Single = cmd.SingleScalar,
            DefaultOnEmpty = cmd.DefaultOnEmpty,
        };
    }

    private static object? Invoke(MethodInfo method, object? target, object?[] arguments)
    {
        try
        {
            return method.Invoke(target, arguments);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    private static bool ContainsParameterPlaceholder(Expression expression)
    {
        var detector = new ParameterPlaceholderDetector();
        detector.Visit(expression);
        return detector.Found;
    }

    private static bool ContainsOuterReferenceMarker(Expression expression)
    {
        var detector = new OuterReferenceMarkerDetector();
        detector.Visit(expression);
        return detector.Found;
    }

    private static bool ContainsQueryRegistryLambda(Expression expression)
    {
        var detector = new QueryRegistryLambdaDetector();
        detector.Visit(expression);
        return detector.Found;
    }

    private sealed class ParameterPlaceholderDetector : ExpressionVisitor
    {
        public bool Found { get; private set; }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType == typeof(SqlFunctions) && node.Method.Name == nameof(SqlFunctions.Parameter))
                Found = true;

            return Found ? node : base.VisitMethodCall(node);
        }
    }

    private sealed class OuterReferenceMarkerDetector : ExpressionVisitor
    {
        public bool Found { get; private set; }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (OuterReferenceToParameterVisitor.TryGetMarker(node, out _, out _))
                Found = true;

            return Found ? node : base.VisitMember(node);
        }
    }

    private sealed class QueryRegistryLambdaDetector : ExpressionVisitor
    {
        public bool Found { get; private set; }

        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            if (node.Parameters.Count == 1 && node.Parameters[0].Type == typeof(IQueryRegistry))
                Found = true;

            return Found ? node : base.VisitLambda(node);
        }
    }
}

/// <summary>
/// Rewrites <c>OuterRefMarker&lt;T&gt;(idx).Ref</c> nodes into <see cref="SqlFunctions.Parameter{T}(int)"/>
/// placeholders, so an inner correlated command prepared for the in-memory provider reads the outer
/// values from the runtime parameter array instead of the marker's unset <c>Ref</c>.
/// </summary>
internal sealed class OuterReferenceToParameterVisitor : ExpressionVisitor
{
    private static readonly MethodInfo ParameterMI = typeof(SqlFunctions).GetMethod(nameof(SqlFunctions.Parameter))!;

    /// <inheritdoc/>
    protected override Expression VisitMember(MemberExpression node)
    {
        if (TryGetMarker(node, out var markerType, out var index))
            return Expression.Call(ParameterMI.MakeGenericMethod(markerType), Expression.Constant(index));

        return base.VisitMember(node);
    }

    /// <summary>Recognises the <c>new OuterRefMarker&lt;T&gt;(idx).Ref</c> shape and extracts its type and index.</summary>
    internal static bool TryGetMarker(MemberExpression node, out Type markerType, out int index)
    {
        markerType = null!;
        index = 0;

        if (node.Member.Name != nameof(OuterRefMarker<int>.Ref))
            return false;
        if (node.Expression is not NewExpression { Type.IsGenericType: true } creation)
            return false;
        if (creation.Type.GetGenericTypeDefinition() != typeof(OuterRefMarker<>))
            return false;
        if (creation.Arguments is not [ConstantExpression { Value: int i }])
            return false;

        markerType = creation.Type.GetGenericArguments()[0];
        index = i;
        return true;
    }
}
