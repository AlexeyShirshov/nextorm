using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace NextORM.Core;

/// <summary>
/// Invokes the builder-function form of a global query filter while a plan is built and reduces it to a
/// predicate lambda. The function is called exactly once over a fresh builder; only the predicate it
/// appends with <c>Where</c> is kept. The function is pure (it may add nothing but <c>Where</c>) and its
/// predicate may not snapshot a runtime value: reads of the <see cref="IDataContext"/> are re-rooted
/// onto the live context so they stay bound parameters, while every other captured closure value is
/// rejected.
/// </summary>
internal static class QueryFilterFunc
{
    private static readonly MethodInfo ApplyTypedMethod =
        typeof(QueryFilterFunc).GetMethod(nameof(ApplyTyped), BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>
    /// Invokes the filter function against <paramref name="dataContext"/> and returns its predicate as a
    /// one-parameter lambda over the function's entity type. The caller re-roots the entity parameter
    /// onto the actual source.
    /// </summary>
    /// <param name="filter">The metadata whose <see cref="IQueryFilterMetadata.Func"/> is invoked.</param>
    /// <param name="dataContext">The live executing context passed to the function.</param>
    /// <returns>The predicate produced by the function, with context captures re-rooted.</returns>
    /// <exception cref="NotSupportedException">The function returned no builder or no predicate, changed state other than <c>Where</c>, or captured a runtime value.</exception>
    public static LambdaExpression Apply(IQueryFilterMetadata filter, IDataContext dataContext)
    {
        var func = filter.Func!;
        var entityType = ResolveEntityType(func);
        try
        {
            return (LambdaExpression)ApplyTypedMethod.MakeGenericMethod(entityType).Invoke(null, [func, dataContext, filter.Key])!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw; // Unreachable: Capture().Throw() always throws.
        }
    }

    // The entity type of the function's builder parameter is read from the delegate's own type rather
    // than from the entity the filter is registered under: an attribute inherited from a base type may
    // be declared over the base entity while it is applied to a derived source. A declaration with any
    // other shape (for example a metadata implementation that reports an arbitrary delegate) is rejected.
    private static Type ResolveEntityType(Delegate func)
    {
        var delegateType = func.GetType();
        if (!delegateType.IsGenericType
            || delegateType.GetGenericTypeDefinition() != typeof(Func<,,>)
            || delegateType.GetGenericArguments() is not [var builderType, var contextType, var resultType]
            || contextType != typeof(IDataContext)
            || builderType != resultType
            || !builderType.IsGenericType
            || builderType.GetGenericTypeDefinition() != typeof(EntityBuilder<>))
            throw new NotSupportedException("A query filter function must be a Func<EntityBuilder<TEntity>, IDataContext, EntityBuilder<TEntity>>.");

        return builderType.GetGenericArguments()[0];
    }

    private static LambdaExpression ApplyTyped<TEntity>(Delegate func, IDataContext dataContext, string? key)
    {
        if (func is not Func<EntityBuilder<TEntity>, IDataContext, EntityBuilder<TEntity>> typed)
            throw new NotSupportedException(
                $"The {KeyText(key)} registered for {typeof(TEntity).Name} has an unsupported function signature; declare it as a Func<EntityBuilder<{typeof(TEntity).Name}>, IDataContext, EntityBuilder<{typeof(TEntity).Name}>>.");

        var input = new EntityBuilder<TEntity>(dataContext);
        // Snapshot the pristine builder before the function runs. Comparing the returned builder against
        // this snapshot (rather than against the input instance) detects an in-place mutation the
        // function applies to the input and then returns through a clone: the input no longer holds the
        // pre-call values, so an identity shortcut (or a comparison against the mutated input) would miss
        // it. Only _condition may differ.
        var baseline = CapturePurityState(input);
        var result = typed(input, dataContext)
            ?? throw new NotSupportedException(
                $"The {KeyText(key)} registered for {typeof(TEntity).Name} returned a null builder; the function must return the builder it appended the predicate to.");

        EnsureOnlyWhereChanged(baseline, result, key, typeof(TEntity));

        var predicate = result.Condition
            ?? throw new NotSupportedException(
                $"The {KeyText(key)} registered for {typeof(TEntity).Name} returned a builder without a Where predicate; express the filter with a single Where call (a chain of Where calls is allowed).");

        var body = new FilterFuncCaptureVisitor(dataContext, key, typeof(TEntity)).Visit(predicate.Body);
        return Expression.Lambda(body, predicate.Parameters[0]);
    }

    // The function must express the filter only through Where: a changed source, join, grouping, paging,
    // sorting, eager load or filter scope would silently be dropped if only the predicate were merged, so
    // such a function is rejected with the offending state named instead.
    private static void EnsureOnlyWhereChanged<TEntity>(object?[] baseline, EntityBuilder<TEntity> result, string? key, Type entityType)
    {
        if (result.GetType() != typeof(EntityBuilder<TEntity>))
            throw new NotSupportedException(NotOnlyWhereMessage(key, entityType, "it returned a different builder type"));

        var fields = PurityState<TEntity>.Fields;
        for (var i = 0; i < fields.Length; i++)
        {
            if (!Equals(baseline[i], fields[i].GetValue(result)))
                throw new NotSupportedException(NotOnlyWhereMessage(key, entityType, $"it changed '{FieldDisplayName(fields[i].Name)}'"));
        }
    }

    // Captures the pre-call values of every builder field except _condition, the only field a legitimate
    // filter function changes (through Where). The field set is cached per closed entity type.
    private static object?[] CapturePurityState<TEntity>(EntityBuilder<TEntity> builder)
    {
        var fields = PurityState<TEntity>.Fields;
        var values = new object?[fields.Length];
        for (var i = 0; i < fields.Length; i++)
            values[i] = fields[i].GetValue(builder);

        return values;
    }

    private static class PurityState<TEntity>
    {
        public static readonly FieldInfo[] Fields = GetFields();

        private static FieldInfo[] GetFields()
        {
            var all = typeof(EntityBuilder<TEntity>).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var fields = new List<FieldInfo>(all.Length);
            for (var i = 0; i < all.Length; i++)
            {
                if (all[i].Name != "_condition")
                    fields.Add(all[i]);
            }

            return fields.ToArray();
        }
    }

    // Reports a friendly state name: an auto-property's compiler backing field reads as the property
    // name and a private field without its leading underscore.
    private static string FieldDisplayName(string fieldName)
    {
        if (fieldName.Length > 2 && fieldName[0] == '<' && fieldName.IndexOf('>') is var end && end > 1)
            return fieldName[1..end];

        return fieldName.TrimStart('_');
    }

    private static string NotOnlyWhereMessage(string? key, Type entityType, string reason)
        => $"The {KeyText(key)} registered for {entityType.Name} must only add a Where predicate to the builder, but {reason}. Only Where is merged, so richer builder operations are not supported.";

    private static string KeyText(string? filterKey) => filterKey switch
    {
        null => "query filter",
        "" => "anonymous query filter",
        _ => $"query filter '{filterKey}'",
    };

    // Rewrites a captured IDataContext read inside the produced predicate onto the live context (so its
    // values become bound parameters and never enter the plan key) and rejects every other captured
    // runtime value: the function runs only when the plan is built, so a snapshot taken before the Where
    // call would be reused for every later execution and for a different context. A closure that captured
    // a different IDataContext than the one passed to the function is rejected: re-rooting it onto the
    // live context would silently read the wrong context's values on a shared plan.
    private sealed class FilterFuncCaptureVisitor(IDataContext dataContext, string? key, Type entityType) : ExpressionVisitor
    {
        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression is ConstantExpression constant && constant.Value is not null)
            {
                var captured = node.Member switch
                {
                    FieldInfo field => field.GetValue(constant.Value),
                    PropertyInfo property => property.GetValue(constant.Value),
                    _ => null,
                };

                if (captured is IDataContext context)
                {
                    if (!ReferenceEquals(context, dataContext))
                        throw new NotSupportedException(
                            $"The {KeyText(key)} registered for {entityType.Name} captures a different IDataContext inside its predicate. Read the IDataContext the function receives as its second parameter instead of a context captured in the closure; the captured instance is not the context the query executes on.");

                    var host = new QueryFilterContext(dataContext);
                    return Expression.Property(Expression.Constant(host), nameof(QueryFilterContext.Context));
                }

                throw new NotSupportedException(
                    $"The {KeyText(key)} registered for {entityType.Name} captures the runtime value '{node.Member.Name}' inside its predicate. Read the value through the IDataContext inside the predicate, or use SqlFunctions.Parameter<T>(idx), so it stays a bound parameter; a value evaluated before the Where call would be baked into the cached plan.");
            }

            return base.VisitMember(node);
        }
    }
}
