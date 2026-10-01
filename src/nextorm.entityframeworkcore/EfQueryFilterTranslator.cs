using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using NextORM.Core;

namespace NextORM.EntityFrameworkCore;

/// <summary>
/// Translates the EF Core 10 query filters declared on an entity type (named/keyed and anonymous) into
/// nextorm's global-filter pipeline. The result is a two-parameter
/// <c>Expression&lt;Func&lt;TEntity, IDataContext, bool&gt;&gt;</c> whose context parameter is resolved
/// against the live owning <see cref="DbContext"/> through <see cref="EfCoreFilterBinding.GetOwner"/>.
/// </summary>
/// <remarks>
/// Translation is structural and fail-fast: it runs while the bridge is created, never executes a live
/// getter, and rejects any shape the bridge cannot represent (closure/static captures,
/// <c>EF.Property</c>/<c>EF.Functions</c>, navigation/subquery filters, invalid signatures, invalid
/// named keys and incompatible context types) instead of silently dropping the filter.
/// </remarks>
internal static class EfQueryFilterTranslator
{
    private static readonly MethodInfo GetOwnerMethod =
        typeof(EfCoreFilterBinding).GetMethod(nameof(EfCoreFilterBinding.GetOwner), BindingFlags.Public | BindingFlags.Static)
        ?? throw new InvalidOperationException($"{nameof(EfCoreFilterBinding)}.{nameof(EfCoreFilterBinding.GetOwner)} was not found.");

    static EfQueryFilterTranslator()
    {
        // Hand core the exact owner-getter identity it must recognise. Core never names the bridge type;
        // it compares the emitted call against this method (assembly/type/method/signature), so a native
        // static helper that happens to take QueryFilterContext.Context is not mistaken for the getter.
        QueryFilterContextAccessor.RegisterOwnerGetter(GetOwnerMethod);
    }

    /// <summary>
    /// Enumerates the filters declared on the root of <paramref name="entityType"/> and translates each
    /// into an <see cref="IQueryFilterMetadata"/>, adapting a root-declared predicate to a mapped derived
    /// entity's CLR type. An entity without declared filters yields an empty list.
    /// </summary>
    /// <param name="entityType">The EF Core entity type being mapped.</param>
    /// <returns>
    /// The translated filters, in declaration order, together with their provenance: the sorted,
    /// distinct CLR type names of the owning <see cref="DbContext"/>s the filters read (empty when no
    /// filter references a context instance).
    /// </returns>
    /// <exception cref="NotSupportedException">A declared filter cannot be represented by the bridge.</exception>
    public static EfFilterTranslation Translate(IEntityType entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);

        var root = entityType;
        while (root.BaseType is not null)
            root = root.BaseType;

        var declared = root.GetDeclaredQueryFilters() as IReadOnlyCollection<IQueryFilter>
            ?? root.GetDeclaredQueryFilters().ToList();
        if (declared.Count == 0)
            return new EfFilterTranslation(Array.Empty<IQueryFilterMetadata>(), string.Empty);

        var entityClrType = entityType.ClrType;
        var result = new List<IQueryFilterMetadata>(declared.Count);
        var namedKeys = new HashSet<string>(StringComparer.Ordinal);
        var contextTypes = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var filter in declared)
        {
            var key = ResolveKey(filter, entityClrType);
            if (key.Length > 0 && !namedKeys.Add(key))
                throw new NotSupportedException(
                    $"The EF Core model declares two query filters with the key '{key}' on '{entityClrType.Name}'; filter keys must be unique per entity type.");

            if (filter.Expression is { } expression)
                ContextProvenanceVisitor.Collect(expression, contextTypes);

            result.Add(TranslateFilter(filter, entityClrType, key));
        }

        return new EfFilterTranslation(result, contextTypes.Count == 0 ? string.Empty : string.Join(',', contextTypes));
    }

    private static string ResolveKey(IQueryFilter filter, Type entityClrType)
    {
        if (filter.IsAnonymous)
            return QueryFilters.AnonymousKey;

        if (string.IsNullOrWhiteSpace(filter.Key))
            throw new NotSupportedException(
                $"The EF Core query filter on '{entityClrType.Name}' is not anonymous but has no key; a named filter must declare a non-empty key.");

        return filter.Key;
    }

    private static EfQueryFilterMetadata TranslateFilter(IQueryFilter filter, Type entityClrType, string key)
    {
        if (filter.Expression is not { } expression)
            throw new NotSupportedException(
                $"The EF Core query filter '{key}' on '{entityClrType.Name}' has no expression.");

        if (expression.Parameters.Count != 1)
            throw new NotSupportedException(
                $"The EF Core query filter '{key}' on '{entityClrType.Name}' must have exactly one entity parameter; it declares {expression.Parameters.Count}.");

        var sourceParameter = expression.Parameters[0];
        if (!sourceParameter.Type.IsAssignableFrom(entityClrType))
            throw new NotSupportedException(
                $"The EF Core query filter '{key}' on '{entityClrType.Name}' is declared over '{sourceParameter.Type.Name}', which '{entityClrType.Name}' is not assignable to.");

        ParameterExpression entityParameter;
        Expression body;
        if (sourceParameter.Type == entityClrType)
        {
            entityParameter = sourceParameter;
            body = expression.Body;
        }
        else
        {
            // A root-declared filter applied to a mapped derived entity: the predicate is written over
            // the root type, so the derived instance is upcast to it.
            entityParameter = Expression.Parameter(entityClrType, sourceParameter.Name);
            body = new EntityParameterAdapter(sourceParameter, entityParameter).Visit(expression.Body);
        }

        var contextParameter = Expression.Parameter(typeof(IDataContext), "context");
        body = new OwnerRewriter(contextParameter, entityParameter).Visit(body)
            ?? throw new NotSupportedException(
                $"The EF Core query filter '{key}' on '{entityClrType.Name}' has a null body.");

        if (body.Type != typeof(bool))
            throw new NotSupportedException(
                $"The EF Core query filter '{key}' on '{entityClrType.Name}' does not return bool ('{body.Type.Name}' instead).");

        var delegateType = typeof(Func<,,>).MakeGenericType(entityClrType, typeof(IDataContext), typeof(bool));
        var lambda = Expression.Lambda(delegateType, body, entityParameter, contextParameter);

        return new EfQueryFilterMetadata(key, lambda);
    }

    /// <summary>Adapts a root-typed filter parameter to a mapped derived CLR type.</summary>
    private sealed class EntityParameterAdapter(ParameterExpression source, ParameterExpression target) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node)
            => node == source ? Expression.Convert(target, source.Type) : base.VisitParameter(node);
    }

    /// <summary>
    /// Rewrites every <see cref="DbContext"/> constant in a filter predicate to
    /// <c>(OriginalContextType)EfCoreFilterBinding.GetOwner(context)</c>, preserving the member chain
    /// on top of it, and rejects shapes the bridge does not support.
    /// </summary>
    private sealed class OwnerRewriter(ParameterExpression contextParameter, ParameterExpression entityParameter) : ExpressionVisitor
    {
        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (node.Value is DbContext owner)
            {
                if (!node.Type.IsAssignableTo(typeof(DbContext)) || !node.Type.IsAssignableFrom(owner.GetType()))
                    throw Unsupported($"a context of type '{owner.GetType().Name}' declared as '{node.Type.Name}'");

                var ownerCall = Expression.Call(GetOwnerMethod, contextParameter);
                return node.Type == typeof(DbContext) ? ownerCall : Expression.Convert(ownerCall, node.Type);
            }

            if (node.Value is not null && !IsSupportedLiteral(node.Value))
                throw Unsupported($"a captured value of type '{node.Value.GetType().Name}'");

            return base.VisitConstant(node);
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression is null)
            {
                // A static scalar member (DateTime.UtcNow, Math.PI, ...) is a value; a user static
                // member is a capture settled outside the filter and is refused.
                if (IsSystemDeclaringType(node.Member.DeclaringType))
                    return base.VisitMember(node);

                throw Unsupported($"a static-member capture '{node.Member.DeclaringType?.Name}.{node.Member.Name}'");
            }

            if (node.Expression is ConstantExpression constant)
            {
                // The context constant is rewritten below; any other captured object is a closure
                // local, whose value is fixed when the model was built and cannot track the live
                // context.
                if (constant.Value is DbContext)
                    return base.VisitMember(node);

                if (constant.Value is not null)
                    throw Unsupported($"a closure/local capture '{node.Member.Name}'");
            }

            if (IsRootedInEntity(node.Expression) && IsUnsupportedMemberType(node.Type))
                throw Unsupported($"a navigation member '{node.Member.Name}'");

            return base.VisitMember(node);
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            var declaringType = node.Method.DeclaringType;

            if (declaringType == typeof(EF))
                throw Unsupported("EF.Property");

            if (declaringType is not null && typeof(DbFunctions).IsAssignableFrom(declaringType))
                throw Unsupported("EF.Functions");

            if (declaringType == typeof(Queryable) || declaringType == typeof(Enumerable))
                throw Unsupported("a nested query, subquery or client-side enumeration");

            if (ReferencesEFFunctions(node.Object) || node.Arguments.Any(ReferencesEFFunctions))
                throw Unsupported("EF.Functions");

            return base.VisitMethodCall(node);
        }

        protected override Expression VisitInvocation(InvocationExpression node)
            => throw Unsupported("an invocation expression");

        protected override Expression VisitExtension(Expression node)
        {
            if (node is QueryRootExpression)
                throw Unsupported("a nested query root");

            return base.VisitExtension(node);
        }

        private static bool ReferencesEFFunctions(Expression? expression)
            => expression is MemberExpression { Member.DeclaringType: var declaring } && declaring == typeof(EF);

        private bool IsRootedInEntity(Expression? expression) => expression switch
        {
            null => false,
            ParameterExpression parameter => parameter == entityParameter,
            MemberExpression member => IsRootedInEntity(member.Expression),
            UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary => IsRootedInEntity(unary.Operand),
            ConditionalExpression conditional => IsRootedInEntity(conditional.IfTrue) && IsRootedInEntity(conditional.IfFalse),
            _ => false,
        };

        private static bool IsSystemDeclaringType(Type? type)
            => type?.Namespace is { } @namespace
                && (@namespace == "System" || @namespace.StartsWith("System.", StringComparison.Ordinal));

        private static bool IsSupportedLiteral(object value) => value switch
        {
            string or char or bool or byte or sbyte or short or ushort or int or uint or long or ulong
                or float or double or decimal or DateTime or DateTimeOffset or Guid or TimeSpan or byte[] => true,
            Enum => true,
            _ => false,
        };

        // A member whose value is not scalar is an entity navigation (or another non-column reference);
        // the bridge has no relationship metadata for filters, so an opaque member is refused.
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

            if (typeof(IEnumerable).IsAssignableFrom(t))
                return true;

            return !IsSystemDeclaringType(t);
        }

        private NotSupportedException Unsupported(string description)
            => new(
                $"The EF Core query filter declares {description}, which the nextorm EF Core bridge cannot translate: " +
                "the filter would be applied incorrectly, so the bridge refuses to register it.");
    }
}

/// <summary>
/// The result of translating one entity's EF Core query filters: the imported filters and the
/// provenance of the contexts they read (the owning <see cref="DbContext"/> CLR type names).
/// </summary>
/// <param name="Filters">The imported filters, in declaration order.</param>
/// <param name="Provenance">Sorted, distinct owning context type names, or an empty string when no filter reads a context.</param>
internal readonly record struct EfFilterTranslation(IReadOnlyList<IQueryFilterMetadata> Filters, string Provenance);

/// <summary>
/// Collects the CLR type names of the <see cref="DbContext"/> instances captured as constants in an EF
/// filter expression, without evaluating anything. Two registrations that read the same context types
/// are the same bridge provenance; a different context type is an independent registration.
/// </summary>
internal static class ContextProvenanceVisitor
{
    public static void Collect(Expression expression, ISet<string> contextTypes)
        => new Visitor(contextTypes).Visit(expression);

    private sealed class Visitor(ISet<string> contextTypes) : ExpressionVisitor
    {
        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (node.Value is DbContext)
                contextTypes.Add(node.Type.FullName ?? node.Type.Name);

            return base.VisitConstant(node);
        }
    }
}

/// <summary>
/// An <see cref="IQueryFilterMetadata"/> imported from an EF Core model: a named or anonymous key and a
/// two-parameter <c>(entity, IDataContext)</c> predicate. The builder-function form is never used.
/// </summary>
internal sealed class EfQueryFilterMetadata(string key, LambdaExpression lambda) : IQueryFilterMetadata
{
    public string Key { get; } = string.IsNullOrEmpty(key) ? QueryFilters.AnonymousKey : key;

    public LambdaExpression? Lambda { get; } = lambda;

    public Delegate? Func => null;
}
