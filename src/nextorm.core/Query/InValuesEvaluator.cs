using System.Collections;
using System.Linq.Expressions;

namespace nextorm.core;

/// <summary>
/// Evaluates the captured collection of an <c>in</c>/<c>Contains</c> predicate.
/// <para>
/// The old implementation compiled a fresh <c>Func&lt;object&gt;</c> delegate on every SQL build and
/// every cached <c>ExtractParams</c> pass, which dominated the IN-list build cost. A compiled accessor
/// is now cached per <em>shape</em> in <see cref="DataContextCache.InValuesCache"/> (keyed with the
/// same <see cref="ExpressionKey"/> the other expression caches use).
/// </para>
/// <para>
/// A captured collection is read through a closure instance that differs between query instances, so the
/// cached accessor takes that instance as a parameter: the closure constant is replaced with a parameter
/// and the current instance is passed on each invocation. Structural equality deliberately ignores the
/// closure instance, which makes same-shape collections reuse the accessor while still reading the
/// current values (grown/reassigned collections included).
/// </para>
/// </summary>
internal static class InValuesEvaluator
{
    public static object? Evaluate(Expression valuesExp, IQueryProvider queryProvider)
    {
        // A directly embedded constant (as opposed to a captured field/local or an inline `new[]`)
        // has no closure to parameterise on and could alias a mutable array, so it is evaluated
        // without caching.
        if (valuesExp is ConstantExpression)
            return Expression.Lambda<Func<object>>(Expression.Convert(valuesExp, typeof(object))).Compile()();

        var captures = CaptureCollector.Collect(valuesExp);

        // More than one distinct closure cannot be represented by a single-parameter accessor, and a
        // captured receiver that is itself null would fault on invocation; both fall back to the
        // original per-build compile.
        if (captures.Count > 1 || (captures.Count == 1 && captures[0].Value is null))
            return Expression.Lambda<Func<object>>(Expression.Convert(valuesExp, typeof(object))).Compile()();

        var capture = captures.Count == 1 ? captures[0] : null;
        var key = new ExpressionKey(valuesExp, queryProvider);

        if (!DataContextCache.InValuesCache.TryGetValue(key, out var accessor))
        {
            accessor = BuildAccessor(valuesExp, capture);
            DataContextCache.InValuesCache[key] = accessor;
        }

        return accessor(capture?.Value);
    }

    private static Func<object?, object?> BuildAccessor(Expression valuesExp, ConstantExpression? capture)
    {
        var parameter = Expression.Parameter(typeof(object), "capture");
        var body = valuesExp;

        if (capture is not null)
            body = new ReplaceInstanceConstantVisitor(capture, Expression.Convert(parameter, capture.Type)).Visit(body);

        return Expression.Lambda<Func<object?, object?>>(Expression.Convert(body, typeof(object)), parameter).Compile();
    }

    /// <summary>
    /// Collects the distinct closure constants that a captured collection reads from (the receiver of a
    /// member access), deduplicated by reference so repeated reads of the same closure count once.
    /// </summary>
    private sealed class CaptureCollector : ExpressionVisitor
    {
        private readonly List<ConstantExpression> _captures = [];

        public static IReadOnlyList<ConstantExpression> Collect(Expression expression)
        {
            var collector = new CaptureCollector();
            collector.Visit(expression);
            return collector._captures;
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression is ConstantExpression capture && !Contains(capture))
                _captures.Add(capture);

            return base.VisitMember(node);
        }

        private bool Contains(ConstantExpression capture)
        {
            for (var i = 0; i < _captures.Count; i++)
                if (ReferenceEquals(_captures[i], capture)) return true;

            return false;
        }
    }

    private sealed class ReplaceInstanceConstantVisitor(ConstantExpression target, Expression replacement) : ExpressionVisitor
    {
        protected override Expression VisitConstant(ConstantExpression node)
            => ReferenceEquals(node, target) ? replacement : base.VisitConstant(node);
    }
}
