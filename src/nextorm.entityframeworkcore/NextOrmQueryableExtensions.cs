using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using NextORM.Core;

namespace NextORM.EntityFrameworkCore;

/// <summary>
/// Translates a bounded EF Core <see cref="IQueryable{T}"/> rooted at a <see cref="DbSet{TEntity}"/>
/// into an equivalent nextorm <see cref="EntityBuilder{T}"/>, so a query written with LINQ-to-Entities
/// can be executed by nextorm on the same connection and transaction.
/// </summary>
/// <remarks>
/// Translation is deliberately bounded: only the operators nextorm can express without a client-side
/// fallback are accepted. Every other operator (projection, eager loading, grouping, joins, subqueries,
/// raw SQL, query-filter overrides and client evaluation) fails with <see cref="NotSupportedException"/>
/// rather than being silently evaluated in memory.
/// </remarks>
public static class NextOrmQueryableExtensions
{
    /// <summary>
    /// Translates the <see cref="DbSet{TEntity}"/> itself into a nextorm <see cref="EntityBuilder{T}"/>
    /// over the <see cref="DbContext"/> that owns it.
    /// </summary>
    /// <typeparam name="T">The entity type at the root of the query.</typeparam>
    /// <param name="source">The EF Core set to read through nextorm.</param>
    /// <returns>A nextorm builder reading every row of <paramref name="source"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The owning <see cref="DbContext"/> cannot be resolved from <paramref name="source"/>.</exception>
    public static EntityBuilder<T> ToNextOrm<T>(this DbSet<T> source) where T : class
    {
        ArgumentNullException.ThrowIfNull(source);

        var dbContext = ResolveDbContext(source);

        return ToNextOrm((IQueryable<T>)source, dbContext);
    }

    /// <summary>
    /// Translates <paramref name="source"/> into a nextorm <see cref="EntityBuilder{T}"/> over
    /// <paramref name="dbContext"/>, replaying the supported operators it carries.
    /// </summary>
    /// <typeparam name="T">The entity type at the root of the query.</typeparam>
    /// <param name="source">An EF Core query whose innermost expression is a <see cref="DbSet{TEntity}"/> of <typeparamref name="T"/>.</param>
    /// <param name="dbContext">The EF Core context the query was composed from; the bridge borrows its connection and transaction.</param>
    /// <returns>A nextorm builder replaying the supported operators of <paramref name="source"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="dbContext"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">
    /// The query is not rooted at a <see cref="DbSet{TEntity}"/> of <typeparamref name="T"/>, or it uses an
    /// operator or an expression nextorm cannot translate without a client-side fallback.
    /// </exception>
    public static EntityBuilder<T> ToNextOrm<T>(this IQueryable<T> source, DbContext dbContext) where T : class
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(dbContext);

        var operators = new List<MethodCallExpression>();
        var current = source.Expression;

        while (current is MethodCallExpression call)
        {
            operators.Add(call);
            current = call.Arguments[0];
        }

        ValidateOperators(operators);

        if (current is not EntityQueryRootExpression root || current.GetType() != typeof(EntityQueryRootExpression))
            throw new NotSupportedException(
                "Only DbSet<T> roots are supported by ToNextOrm; the query's root expression is " +
                $"'{current.GetType().Name}' instead of EntityQueryRootExpression.");

        if (root.EntityType.ClrType != typeof(T))
            throw new NotSupportedException(
                "Only DbSet<T> roots are supported by ToNextOrm; the root entity type is " +
                $"'{root.EntityType.ClrType}' instead of '{typeof(T)}'.");

        var dataContext = dbContext.GetNextOrmContext();
        var builder = dataContext.From<T>();

        for (var i = operators.Count - 1; i >= 0; i--)
            builder = Apply(builder, operators[i]);

        return builder;
    }

    private static DbContext ResolveDbContext<T>(DbSet<T> source) where T : class
    {
        if (source is IInfrastructure<IServiceProvider> infrastructure
            && infrastructure.Instance.GetService(typeof(ICurrentDbContext)) is ICurrentDbContext current)
            return current.Context;

        throw new NotSupportedException(
            "The owning DbContext could not be resolved from the DbSet<T>; pass the DbContext explicitly through ToNextOrm(source, dbContext).");
    }

    private static void ValidateOperators(List<MethodCallExpression> operators)
    {
        var primarySort = false;
        var skipApplied = false;
        var takeApplied = false;
        var pagingStarted = false;
        var distinctApplied = false;

        for (var i = operators.Count - 1; i >= 0; i--)
        {
            var call = operators[i];

            if (call.Method.DeclaringType == typeof(EntityFrameworkQueryableExtensions))
            {
                switch (call.Method.Name)
                {
                    case nameof(EntityFrameworkQueryableExtensions.AsNoTracking):
                    case nameof(EntityFrameworkQueryableExtensions.AsNoTrackingWithIdentityResolution):
                    case nameof(EntityFrameworkQueryableExtensions.TagWith):
                        continue;

                    case nameof(EntityFrameworkQueryableExtensions.AsTracking):
                        throw new NotSupportedException(
                            "The LINQ operator 'AsTracking' is not supported by ToNextOrm: nextorm is " +
                            "read-only, so change tracking cannot be enabled.");

                    default:
                        throw UnknownOperator(call.Method.Name);
                }
            }

            if (call.Method.DeclaringType != typeof(Queryable))
                throw UnknownOperator(call.Method.Name);

            switch (call.Method.Name)
            {
                case nameof(Queryable.Where):
                    if (pagingStarted)
                        throw new NotSupportedException(
                            "The LINQ operator 'Where' is not supported by ToNextOrm after 'Skip' or 'Take': " +
                            "filtering after paging cannot be expressed without a client-side fallback.");
                    GuardLambda(StripQuote(call.Arguments[1]), nameof(Queryable.Where));
                    break;

                case nameof(Queryable.OrderBy):
                case nameof(Queryable.OrderByDescending):
                    if (primarySort)
                        throw new NotSupportedException(
                            $"The LINQ operator '{call.Method.Name}' is not supported by ToNextOrm: a second " +
                            "primary ordering cannot be applied because nextorm ordering keys append and cannot be reset.");
                    if (pagingStarted)
                        throw new NotSupportedException(
                            $"The LINQ operator '{call.Method.Name}' is not supported by ToNextOrm after 'Skip' or 'Take'.");
                    primarySort = true;
                    GuardLambda(StripQuote(call.Arguments[1]), call.Method.Name);
                    break;

                case nameof(Queryable.ThenBy):
                case nameof(Queryable.ThenByDescending):
                    if (!primarySort)
                        throw new NotSupportedException(
                            $"The LINQ operator '{call.Method.Name}' is not supported by ToNextOrm before a primary ordering.");
                    if (pagingStarted)
                        throw new NotSupportedException(
                            $"The LINQ operator '{call.Method.Name}' is not supported by ToNextOrm after 'Skip' or 'Take'.");
                    GuardLambda(StripQuote(call.Arguments[1]), call.Method.Name);
                    break;

                case nameof(Queryable.Skip):
                    if (skipApplied)
                        throw new NotSupportedException(
                            "The LINQ operator 'Skip' is not supported by ToNextOrm more than once.");
                    if (takeApplied)
                        throw new NotSupportedException(
                            "The LINQ operator 'Skip' is not supported by ToNextOrm after 'Take'.");
                    skipApplied = true;
                    pagingStarted = true;
                    break;

                case nameof(Queryable.Take):
                    if (takeApplied)
                        throw new NotSupportedException(
                            "The LINQ operator 'Take' is not supported by ToNextOrm more than once.");
                    takeApplied = true;
                    pagingStarted = true;
                    break;

                case nameof(Queryable.Distinct):
                    if (distinctApplied)
                        throw new NotSupportedException(
                            "The LINQ operator 'Distinct' is not supported by ToNextOrm more than once.");
                    if (pagingStarted)
                        throw new NotSupportedException(
                            "The LINQ operator 'Distinct' is not supported by ToNextOrm after 'Skip' or 'Take'.");
                    distinctApplied = true;
                    break;

                default:
                    throw UnknownOperator(call.Method.Name);
            }
        }
    }

    private static EntityBuilder<T> Apply<T>(EntityBuilder<T> builder, MethodCallExpression call) where T : class
    {
        if (call.Method.DeclaringType == typeof(EntityFrameworkQueryableExtensions))
        {
            switch (call.Method.Name)
            {
                case nameof(EntityFrameworkQueryableExtensions.AsNoTracking):
                case nameof(EntityFrameworkQueryableExtensions.AsNoTrackingWithIdentityResolution):
                case nameof(EntityFrameworkQueryableExtensions.TagWith):
                    return builder;

                default:
                    throw UnknownOperator(call.Method.Name);
            }
        }

        if (call.Method.DeclaringType != typeof(Queryable))
            throw UnknownOperator(call.Method.Name);

        switch (call.Method.Name)
        {
            case nameof(Queryable.Where):
                return builder.Where(BuildPredicate<T>(call));
            case nameof(Queryable.OrderBy):
            case nameof(Queryable.ThenBy):
                return builder.OrderBy(BuildKeySelector<T>(call));
            case nameof(Queryable.OrderByDescending):
            case nameof(Queryable.ThenByDescending):
                return builder.OrderByDescending(BuildKeySelector<T>(call));
            case nameof(Queryable.Skip):
                return builder.Offset(EvaluateCount(call, "Skip"));
            case nameof(Queryable.Take):
                return builder.Limit(EvaluateCount(call, "Take"));
            case nameof(Queryable.Distinct):
                return builder.Distinct();
            default:
                throw UnknownOperator(call.Method.Name);
        }
    }

    private static Expression<Func<T, bool>> BuildPredicate<T>(MethodCallExpression call)
    {
        var lambda = StripQuote(call.Arguments[1]);

        return Expression.Lambda<Func<T, bool>>(lambda.Body, lambda.Parameters);
    }

    private static Expression<Func<T, object?>> BuildKeySelector<T>(MethodCallExpression call)
    {
        var lambda = StripQuote(call.Arguments[1]);
        var body = Expression.Convert(lambda.Body, typeof(object));

        return Expression.Lambda<Func<T, object?>>(body, lambda.Parameters);
    }

    private static int EvaluateCount(MethodCallExpression call, string operatorName)
    {
        var value = call.Arguments[1];

        if (value is ConstantExpression { Value: int constant })
            return constant;

        Func<int> evaluator;

        try
        {
            evaluator = Expression.Lambda<Func<int>>(Expression.Convert(value, typeof(int))).Compile();
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            throw new NotSupportedException(
                $"The LINQ operator '{operatorName}' is not supported by ToNextOrm with a non-constant count.", exception);
        }

        return evaluator();
    }

    private static LambdaExpression StripQuote(Expression expression)
    {
        if (expression is UnaryExpression { NodeType: ExpressionType.Quote, Operand: LambdaExpression quoted })
            return quoted;

        if (expression is LambdaExpression lambda)
            return lambda;

        throw new NotSupportedException(
            $"The LINQ operator argument of type '{expression.NodeType}' is not supported by ToNextOrm: " +
            "a quoted lambda expression is required.");
    }

    private static void GuardLambda(LambdaExpression lambda, string operatorName)
        => LambdaBodyGuard.Validate(lambda, operatorName);

    private static NotSupportedException UnknownOperator(string name)
        => new(
            $"The LINQ operator '{name}' is not supported by ToNextOrm. Supported operators are Where, " +
            "OrderBy, OrderByDescending, ThenBy, ThenByDescending, Skip, Take and Distinct.");

    private sealed class LambdaBodyGuard : ExpressionVisitor
    {
        private readonly ParameterExpression _parameter;
        private readonly string _operatorName;

        private LambdaBodyGuard(ParameterExpression parameter, string operatorName)
        {
            _parameter = parameter;
            _operatorName = operatorName;
        }

        internal static void Validate(LambdaExpression lambda, string operatorName)
            => new LambdaBodyGuard(lambda.Parameters[0], operatorName).Visit(lambda.Body);

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            var declaringType = node.Method.DeclaringType;

            if (declaringType == typeof(Queryable) || declaringType == typeof(Enumerable))
                throw Unsupported("a nested query, subquery or client-side enumeration");

            if (declaringType == typeof(EF))
                throw Unsupported("EF.Property");

            if (declaringType == typeof(DbFunctions) || (declaringType is not null && typeof(DbFunctions).IsAssignableFrom(declaringType)))
                throw Unsupported("EF.Functions");

            if (node.Object is MemberExpression { Member.DeclaringType: var owner } && owner == typeof(EF))
                throw Unsupported("EF.Functions");

            return base.VisitMethodCall(node);
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (IsRootedInParameter(node.Expression) && IsUnsupportedMemberType(node.Type))
                throw Unsupported($"a member access through '{node.Member.Name}'");

            return base.VisitMember(node);
        }

        private bool IsRootedInParameter(Expression? expression)
            => expression switch
            {
                null => false,
                ParameterExpression parameter => parameter == _parameter,
                MemberExpression member => IsRootedInParameter(member.Expression),
                UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary
                    => IsRootedInParameter(unary.Operand),
                ConditionalExpression conditional
                    => IsRootedInParameter(conditional.IfTrue) && IsRootedInParameter(conditional.IfFalse),
                _ => false,
            };

        private static bool IsUnsupportedMemberType(Type type)
        {
            var t = Nullable.GetUnderlyingType(type) ?? type;

            if (t.IsPrimitive || t.IsEnum)
                return false;

            if (t == typeof(string) || t == typeof(decimal) || t == typeof(DateTime)
                || t == typeof(Guid) || t == typeof(TimeSpan) || t == typeof(DateTimeOffset))
                return false;

            if (t == typeof(byte[]))
                return false;

            if (t.IsValueType)
                return false;

            if (t.IsArray)
            {
                var element = t.GetElementType();

                return element is null || (!element.IsPrimitive && !element.IsEnum);
            }

            if (typeof(System.Collections.IEnumerable).IsAssignableFrom(t))
                return true;

            return !IsSystemType(t);
        }

        private static bool IsSystemType(Type type)
        {
            var @namespace = type.Namespace;

            return @namespace == "System"
                || (@namespace is not null && @namespace.StartsWith("System.", StringComparison.Ordinal));
        }

        protected override Expression VisitInvocation(InvocationExpression node)
            => throw Unsupported("an invocation expression");

        protected override Expression VisitExtension(Expression node)
        {
            if (node is QueryRootExpression)
                throw Unsupported("a nested query root");

            return base.VisitExtension(node);
        }

        private NotSupportedException Unsupported(string description)
            => new(
                $"The LINQ operator '{_operatorName}' is not supported by ToNextOrm: its lambda contains " +
                $"{description}, which nextorm cannot translate without a client-side fallback.");
    }
}
