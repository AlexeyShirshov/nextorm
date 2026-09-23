using System.Globalization;
using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Rewrites the SQL-shaped correlated subqueries of an in-memory command into per-row value
/// expressions. A prepared correlated subquery appears as a lambda over <see cref="IQueryRegistry"/>
/// whose body reads <c>ReferencedQueries[idx]</c>; this visitor replaces that node with a call to a
/// cached <see cref="InMemoryCorrelatedPlan"/> bound to the current outer row, so a correlated
/// scalar/<c>exists</c>/<c>in</c> is evaluated once per row instead of once per query.
/// </summary>
internal sealed class InMemoryCorrelatedSubqueryRewriter : ExpressionVisitor
{
    private readonly InMemoryDataContext _context;
    private readonly QueryCommand _registry;

    /// <summary>Creates a rewriter for the prepared expressions of <paramref name="owner"/>.</summary>
    public InMemoryCorrelatedSubqueryRewriter(InMemoryDataContext context, QueryCommand owner)
    {
        _context = context;
        _registry = owner.RootRegistry as QueryCommand ?? owner;
    }

    /// <summary>
    /// True when the command has a correlated scope or at least one referenced subquery, i.e. when the
    /// rewrite pass can change anything. A plain expression is left untouched.
    /// </summary>
    internal static bool IsNeeded(QueryCommand command)
        => command.OuterReferences is { Count: > 0 } || command.ReferencedQueries is { Count: > 0 };

    /// <summary>Rewrites <paramref name="expression"/>, replacing every correlated subquery with a per-row value.</summary>
    public Expression Rewrite(Expression expression) => Visit(expression)!;

    /// <inheritdoc/>
    public override Expression? Visit(Expression? node)
    {
        if (node is LambdaExpression lambda
            && lambda.Parameters.Count == 1
            && lambda.Parameters[0].Type == typeof(IQueryRegistry))
        {
            return RewriteSubquery(lambda);
        }

        return base.Visit(node);
    }

    /// <summary>
    /// Restores typed equality when a scalar subquery was boxed for SQL rendering
    /// (<c>Convert(x, object) == Convert(y, object)</c>), so the in-memory comparison is a value
    /// comparison instead of reference equality on the boxes.
    /// </summary>
    protected override Expression VisitBinary(BinaryExpression node)
    {
        if (node.NodeType is ExpressionType.Equal or ExpressionType.NotEqual)
        {
            var left = Visit(node.Left)!;
            var right = Visit(node.Right)!;

            if (!ReferenceEquals(left, node.Left) || !ReferenceEquals(right, node.Right))
            {
                left = UnwrapObjectConversion(left);
                right = UnwrapObjectConversion(right);

                if (left.Type != right.Type)
                {
                    var common = CommonType(left.Type, right.Type);
                    left = Expression.Convert(left, common);
                    right = Expression.Convert(right, common);
                }

                return Expression.MakeBinary(node.NodeType, left, right);
            }
        }

        return base.VisitBinary(node);
    }

    /// <summary>
    /// Rebuilds a conversion whose operand changed type after a subquery was replaced by a value
    /// (for example <c>Convert(subquery, object)</c>), instead of letting the base visitor reject the
    /// operand-type change.
    /// </summary>
    protected override Expression VisitUnary(UnaryExpression node)
    {
        if (node.NodeType is ExpressionType.Convert or ExpressionType.ConvertChecked)
        {
            var operand = Visit(node.Operand)!;
            if (ReferenceEquals(operand, node.Operand))
                return node;
            if (operand.Type == node.Type || node.Type.IsAssignableFrom(operand.Type))
                return operand.Type == node.Type ? operand : Expression.Convert(operand, node.Type);

            return Expression.MakeUnary(node.NodeType, operand, node.Type, node.Method);
        }

        return base.VisitUnary(node);
    }

    /// <summary>
    /// Rebuilds a lambda whose body changed type (a correlated subquery became a value), letting the
    /// delegate type be inferred from the new body instead of keeping the SQL-shaped return type.
    /// </summary>
    protected override Expression VisitLambda<T>(Expression<T> node)
    {
        var body = Visit(node.Body)!;
        return ReferenceEquals(body, node.Body) ? node : Expression.Lambda(body, node.Parameters);
    }

    private Expression RewriteSubquery(LambdaExpression lambda)
    {
        var body = lambda.Body;
        CorrelatedTerminal terminal;
        Expression? inValue = null;
        int index;

        if (body is MethodCallExpression call)
        {
            if (call.Method.DeclaringType == typeof(CommonFunctions) && call.Method.Name == nameof(CommonFunctions.exists))
            {
                terminal = CorrelatedTerminal.Exists;
                index = GetReferencedQueryIndex(call.Arguments[0]);
            }
            else if (call.Method.Name == nameof(CommonFunctions.@in) || call.Method.Name == nameof(ClickHouseFunctions.global_in))
            {
                terminal = CorrelatedTerminal.In;
                inValue = call.Arguments[0];
                index = GetReferencedQueryIndex(call.Arguments[1]);
            }
            else
            {
                throw new NotSupportedException($"The subquery operator '{call.Method.Name}' is not supported by the in-memory provider.");
            }
        }
        else
        {
            terminal = CorrelatedTerminal.Scalar;
            index = GetReferencedQueryIndex(body);
        }

        var inner = _registry.ReferencedQueries[index];
        var plan = _context.GetCorrelatedPlan(inner);
        var rowParameter = FindRowParameter();

        if (rowParameter is null)
            return FoldConstant(plan, terminal, inValue, inner.ResultType!);

        var values = BuildOuterValuesExpression(rowParameter);

        return terminal switch
        {
            CorrelatedTerminal.Exists =>
                Expression.Invoke(Expression.Constant((Func<object?[], bool>)plan.Any), values),
            CorrelatedTerminal.In => Expression.Invoke(
                Expression.Constant((Func<object?[], object?, bool>)plan.Contains),
                values,
                Expression.Convert(Visit(inValue!)!, typeof(object))),
            _ => Expression.Convert(
                Expression.Invoke(Expression.Constant((Func<object?[], object?>)plan.Scalar), values),
                inner.ResultType!),
        };
    }

    private Expression FoldConstant(InMemoryCorrelatedPlan plan, CorrelatedTerminal terminal, Expression? inValue, Type resultType)
    {
        var values = Array.Empty<object?>();

        return terminal switch
        {
            CorrelatedTerminal.Exists => Expression.Constant(plan.Any(values)),
            CorrelatedTerminal.In => Expression.Constant(plan.Contains(values, CompileValue(inValue!))),
            _ => ConstantOf(plan.Scalar(values), resultType),
        };
    }

    private static object? CompileValue(Expression expression)
        => Expression.Lambda(Expression.Convert(expression, typeof(object))).Compile().DynamicInvoke();

    private static Expression ConstantOf(object? value, Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type);
        var target = underlying ?? type;

        if (value is null)
            return target.IsValueType && underlying is null ? Expression.Default(type) : Expression.Constant(null, type);

        if (!type.IsInstanceOfType(value))
            value = Convert.ChangeType(value, target, CultureInfo.InvariantCulture);

        return Expression.Constant(value, type);
    }

    private ParameterExpression? FindRowParameter()
    {
        var references = _registry.OuterReferences;
        if (references is null || references.Count == 0)
            return null;

        var parameter = FindParameter(references[0])
            ?? throw new NotSupportedException("A correlated subquery whose outer reference is not rooted in the outer row is not supported by the in-memory provider.");

        for (var i = 1; i < references.Count; i++)
        {
            var other = FindParameter(references[i]);
            if (other is not null && !ReferenceEquals(other, parameter))
                throw new NotSupportedException("Correlated subqueries across multiple outer scopes are not supported by the in-memory provider.");
        }

        return parameter;
    }

    private Expression BuildOuterValuesExpression(ParameterExpression rowParameter)
    {
        var binderParameter = Expression.Parameter(rowParameter.Type, "row");
        var references = _registry.OuterReferences!;
        var items = new Expression[references.Count];

        for (var i = 0; i < references.Count; i++)
        {
            var reference = new ReplaceParameterExpressionVisitor(binderParameter).Visit(references[i])!;
            items[i] = Expression.Convert(reference, typeof(object));
        }

        var binderType = typeof(Func<,>).MakeGenericType(rowParameter.Type, typeof(object[]));
        var body = Expression.NewArrayInit(typeof(object), items);
        var binder = Expression.Lambda(binderType, body, binderParameter).Compile();

        return Expression.Invoke(Expression.Constant(binder), rowParameter);
    }

    private static ParameterExpression? FindParameter(Expression expression) => expression switch
    {
        ParameterExpression parameter => parameter,
        MemberExpression member => FindParameter(member.Expression!),
        UnaryExpression unary => FindParameter(unary.Operand),
        _ => null,
    };

    private static Expression UnwrapObjectConversion(Expression expression)
        => expression is UnaryExpression { NodeType: ExpressionType.Convert } unary && unary.Type == typeof(object)
            ? unary.Operand
            : expression;

    private static Type CommonType(Type left, Type right)
    {
        if (left == right) return left;
        if (left.IsAssignableFrom(right)) return left;
        if (right.IsAssignableFrom(left)) return right;

        var leftRank = NumericRank(left);
        var rightRank = NumericRank(right);
        if (leftRank >= 0 && rightRank >= 0)
            return leftRank >= rightRank ? left : right;

        throw new NotSupportedException($"Cannot compare a correlated subquery of type '{left.Name}' with type '{right.Name}' in the in-memory provider.");
    }

    private static int NumericRank(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;

        return Type.GetTypeCode(type) switch
        {
            TypeCode.Byte or TypeCode.SByte => 1,
            TypeCode.Int16 or TypeCode.UInt16 => 2,
            TypeCode.Int32 or TypeCode.UInt32 => 3,
            TypeCode.Int64 or TypeCode.UInt64 => 4,
            TypeCode.Single => 5,
            TypeCode.Double => 6,
            TypeCode.Decimal => 7,
            _ => -1,
        };
    }

    private static int GetReferencedQueryIndex(Expression expression)
    {
        var current = UnwrapObjectConversion(expression);

        if (current is IndexExpression
            {
                Object: MemberExpression { Member.Name: nameof(IQueryRegistry.ReferencedQueries) },
                Arguments: [ConstantExpression { Value: int index }],
            })
        {
            return index;
        }

        var finder = new ReferencedQueryIndexFinder();
        finder.Visit(current);
        if (finder.Found)
            return finder.Index;

        throw new NotSupportedException("Could not resolve the referenced query of a correlated subquery in the in-memory provider.");
    }

    private sealed class ReferencedQueryIndexFinder : ExpressionVisitor
    {
        public bool Found { get; private set; }
        public int Index { get; private set; }

        protected override Expression VisitIndex(IndexExpression node)
        {
            if (node.Object is MemberExpression { Member.Name: nameof(IQueryRegistry.ReferencedQueries) }
                && node.Arguments is [ConstantExpression { Value: int index }])
            {
                Found = true;
                Index = index;
                return node;
            }

            return Found ? node : base.VisitIndex(node);
        }
    }

    private enum CorrelatedTerminal
    {
        Exists,
        In,
        Scalar,
    }
}
