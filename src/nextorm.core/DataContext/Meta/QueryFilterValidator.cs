using System.Linq.Expressions;
using System.Reflection;

namespace NextORM.Core;

/// <summary>
/// Validates the rows written by an <c>INSERT</c> or <c>MERGE</c> against the target entity type's
/// active global query filters. On a supported <c>MERGE</c> (and <c>UPDATE</c>/<c>DELETE</c>) the filter
/// is also injected into the statement's target predicate; the remaining writes — and the inserted rows
/// of a merge — are checked before the statement executes, and a violation raises a
/// <see cref="QueryFilterException"/>.
/// </summary>
/// <remarks>
/// Two in-memory strategies are used. When the row is a materialised entity the filter is compiled and
/// invoked against it directly. When only column values are known (the <c>Value</c>/<c>Values</c>
/// column forms and client-side mapped writes) the filter is compiled once over an <c>object?[]</c>
/// aligned to the written columns. Validation is <b>fail-closed</b>: a filter that reads a column the
/// statement does not write cannot be checked against a value, so the write is rejected with a
/// <see cref="QueryFilterException"/> rather than letting it through (an <c>INSERT</c> that omits the
/// tenant column must not bypass the tenant filter when a database default could violate it). To write
/// a row whose omitted column is satisfied by the database default, write the column explicitly or
/// disable the filter for the statement with <c>IgnoreFilters</c>.
/// <para>
/// An <c>INSERT ... SELECT</c> or query-sourced <c>MERGE</c> is guarded by a server-side pre-check:
/// the source is queried for a row the target filter rejects, and the statement is not executed when
/// one exists. The pre-check is a separate read, so a concurrent write can still slip a violating row
/// in between the check and the write (a TOCTOU race); validation does not make the write atomic.
/// </para>
/// </remarks>
internal static class QueryFilterValidator
{
    /// <summary>
    /// Builds a reusable per-row check for a streaming write (async bulk insert), so the filter
    /// predicates are compiled once rather than per row. Returns <see langword="null"/> when no filter
    /// applies.
    /// </summary>
    /// <typeparam name="TEntity">The mapped entity type written.</typeparam>
    /// <param name="scope">The scope of filters disabled for the statement.</param>
    /// <param name="dataContext">The executing context, read by context-aware filters.</param>
    /// <param name="operation">The statement name used in the error message.</param>
    /// <returns>A check that throws on a violating row, or <see langword="null"/> when no filter applies.</returns>
    public static Action<TEntity>? CreateEntityCheck<TEntity>(QueryFilterScope scope, IDataContext dataContext, string operation)
    {
        var predicates = BuildEntityPredicates<TEntity>(scope, dataContext, operation);
        if (predicates.Count == 0)
            return null;

        return row =>
        {
            for (var p = 0; p < predicates.Count; p++)
            {
                if (!predicates[p].Predicate(row))
                    throw Violation(predicates[p].Key, typeof(TEntity), operation, rowIndex: null);
            }
        };
    }

    /// <summary>
    /// Validates materialised entity rows against the target filters.
    /// </summary>
    /// <typeparam name="TEntity">The mapped entity type written.</typeparam>
    /// <param name="rows">The rows about to be written.</param>
    /// <param name="scope">The scope of filters disabled for the statement.</param>
    /// <param name="dataContext">The executing context, read by context-aware filters.</param>
    /// <param name="operation">The statement name used in the error message (<c>INSERT</c>/<c>MERGE</c>).</param>
    /// <exception cref="QueryFilterException">A row violates an active filter.</exception>
    public static void ValidateEntities<TEntity>(IReadOnlyList<TEntity> rows, QueryFilterScope scope, IDataContext dataContext, string operation)
    {
        if (rows.Count == 0)
            return;

        var predicates = BuildEntityPredicates<TEntity>(scope, dataContext, operation);
        if (predicates.Count == 0)
            return;

        for (var r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            for (var p = 0; p < predicates.Count; p++)
            {
                if (!predicates[p].Predicate(row))
                    throw Violation(predicates[p].Key, typeof(TEntity), operation, rowIndex: r);
            }
        }
    }

    /// <summary>
    /// Validates a batch whose rows are column-oriented against the target filters. Validation is
    /// fail-closed: a filter that reads a column absent from <paramref name="writtenColumns"/> cannot be
    /// checked against a value the statement does not write, so the write is rejected.
    /// </summary>
    /// <param name="entityType">The mapped entity type written.</param>
    /// <param name="writtenColumns">The columns the statement writes, in row order.</param>
    /// <param name="rows">The projected values, each aligned to <paramref name="writtenColumns"/>; a <see langword="null"/> entry means an unwritten/default value.</param>
    /// <param name="scope">The scope of filters disabled for the statement.</param>
    /// <param name="dataContext">The executing context, read by context-aware filters.</param>
    /// <param name="operation">The statement name used in the error message.</param>
    /// <exception cref="QueryFilterException">A row violates an active filter, or an active filter reads a column the statement does not write.</exception>
    public static void ValidateColumnRows(Type entityType, IReadOnlyList<IPropertyMetadata> writtenColumns, IReadOnlyList<object?[]> rows, QueryFilterScope scope, IDataContext dataContext, string operation)
    {
        // Predicates are built before the row check so an omitted-column filter fails closed even for an
        // empty row set (for example an all-defaults insert).
        var predicates = BuildColumnPredicates(entityType, writtenColumns, scope, dataContext, operation);
        if (predicates.Count == 0 || rows.Count == 0)
            return;

        for (var r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            for (var p = 0; p < predicates.Count; p++)
            {
                if (!predicates[p].Predicate(row))
                    throw Violation(predicates[p].Key, entityType, operation, rowIndex: r);
            }
        }
    }

    /// <summary>
    /// Pre-checks an <c>INSERT ... SELECT</c> source (or a query-sourced <c>MERGE</c>) for rows the
    /// target filter would reject. The source is projected to <typeparamref name="TResult"/>, whose
    /// members are named after the target columns, so the filter is re-rooted onto the projection and
    /// executed as an existence query; a violating row raises before the write.
    /// </summary>
    /// <typeparam name="TEntity">The mapped target entity type.</typeparam>
    /// <typeparam name="TResult">The source projection's row type.</typeparam>
    /// <param name="source">The source command.</param>
    /// <param name="projectedColumns">The target columns the source projection actually writes, or <see langword="null"/> when the source is the whole entity so every column is present.</param>
    /// <param name="scope">The scope of filters disabled for the statement.</param>
    /// <param name="dataContext">The executing context.</param>
    /// <param name="operation">The statement name used in the error message.</param>
    /// <exception cref="QueryFilterException">The source contains a row the target filter rejects, or an active filter reads a column the projection does not write.</exception>
    public static void ValidateSource<TEntity, TResult>(QueryCommand<TResult> source, IReadOnlyList<IPropertyMetadata>? projectedColumns, QueryFilterScope scope, IDataContext dataContext, string operation)
    {
        var violation = BuildSourceViolation<TEntity, TResult>(scope, dataContext, operation, projectedColumns);
        if (violation is null)
            return;

        // IgnoreFilters is essential here: the derived source's row type is the target entity, so the
        // target's own global filters would otherwise be injected into the pre-check SELECT and hide
        // exactly the rows we are looking for.
        if (dataContext.From(source).IgnoreFilters().Where(violation.Value.Predicate).Any())
            throw Violation(violation.Value.Key, typeof(TEntity), operation, rowIndex: null);
    }

    /// <summary>
    /// Asynchronously pre-checks an <c>INSERT ... SELECT</c> source (or a query-sourced <c>MERGE</c>)
    /// for rows the target filter would reject. The async twin of <see cref="ValidateSource{TEntity, TResult}"/>:
    /// the existence query is genuinely asynchronous and honours <paramref name="cancellationToken"/>.
    /// </summary>
    /// <typeparam name="TEntity">The mapped target entity type.</typeparam>
    /// <typeparam name="TResult">The source projection's row type.</typeparam>
    /// <param name="source">The source command.</param>
    /// <param name="projectedColumns">The target columns the source projection actually writes, or <see langword="null"/> when the source is the whole entity so every column is present.</param>
    /// <param name="scope">The scope of filters disabled for the statement.</param>
    /// <param name="dataContext">The executing context.</param>
    /// <param name="operation">The statement name used in the error message.</param>
    /// <param name="cancellationToken">Cancels the pre-check query.</param>
    /// <returns>A task that completes when the source has been checked.</returns>
    /// <exception cref="QueryFilterException">The source contains a row the target filter rejects, or an active filter reads a column the projection does not write.</exception>
    /// <exception cref="OperationCanceledException">The operation was cancelled.</exception>
    public static async Task ValidateSourceAsync<TEntity, TResult>(QueryCommand<TResult> source, IReadOnlyList<IPropertyMetadata>? projectedColumns, QueryFilterScope scope, IDataContext dataContext, string operation, CancellationToken cancellationToken)
    {
        var violation = BuildSourceViolation<TEntity, TResult>(scope, dataContext, operation, projectedColumns);
        if (violation is null)
            return;

        // IgnoreFilters is essential here: the derived source's row type is the target entity, so the
        // target's own global filters would otherwise be injected into the pre-check SELECT and hide
        // exactly the rows we are looking for.
        if (await dataContext.From(source).IgnoreFilters().Where(violation.Value.Predicate).AnyAsync(cancellationToken).ConfigureAwait(false))
            throw Violation(violation.Value.Key, typeof(TEntity), operation, rowIndex: null);
    }

    private static List<(string Key, Func<TEntity, bool> Predicate)> BuildEntityPredicates<TEntity>(QueryFilterScope scope, IDataContext dataContext, string operation)
    {
        var filters = QueryFilterResolver.GetFilters(typeof(TEntity), scope, dataContext);
        var predicates = new List<(string, Func<TEntity, bool>)>(filters.Count);

        foreach (var filter in filters)
        {
            if (filter.Lambda is not { Parameters.Count: >= 1 } lambda)
            {
                if (filter.Func is not null)
                    throw UnsupportedFunc(filter.Key, typeof(TEntity), operation);
                continue;
            }

            var parameter = Expression.Parameter(typeof(TEntity), "e");
            var body = RewriteParameters(lambda, dataContext, lambda.Parameters[0], parameter);

            // The filter may read a column through a base declaration or an interface while the mapped
            // column is declared on the concrete entity (a new-hidden member, an explicit implementation).
            // Resolving the read to its declaration on the written entity type makes the compiled check
            // read exactly the mapped column, so the entity-row path agrees with the column and source
            // paths instead of evaluating a different backing member (a false accept/reject).
            body = new ConcreteMemberRewriteVisitor(parameter, typeof(TEntity)).Visit(body);
            var compiled = Expression.Lambda<Func<TEntity, bool>>(body, parameter).Compile();
            predicates.Add((filter.Key, compiled));
        }

        return predicates;
    }

    private static List<(string Key, Func<object?[], bool> Predicate)> BuildColumnPredicates(Type entityType, IReadOnlyList<IPropertyMetadata> writtenColumns, QueryFilterScope scope, IDataContext dataContext, string operation)
    {
        var filters = QueryFilterResolver.GetFilters(entityType, scope, dataContext);
        var predicates = new List<(string, Func<object?[], bool>)>(filters.Count);
        if (filters.Count == 0)
            return predicates;

        // Key the written columns by the property resolved on the concrete entity type, not by the
        // PropertyInfo instance: a filter may read its property through an interface, a base type or a
        // new-hidden member, and each of those reflection instances differs from the mapped column. The
        // canonical (DeclaringType, MetadataToken) pair is what identifies the member, so the concrete
        // resolution of both sides matches (ReflectedType is deliberately ignored).
        var indexByProperty = new Dictionary<PropertyKey, int>(writtenColumns.Count);
        for (var i = 0; i < writtenColumns.Count; i++)
            indexByProperty[CanonicalPropertyKey(entityType, writtenColumns[i].PropertyInfo)] = i;

        foreach (var filter in filters)
        {
            if (filter.Lambda is not { Parameters.Count: >= 1 } lambda)
            {
                if (filter.Func is not null)
                    throw UnsupportedFunc(filter.Key, entityType, operation);
                continue;
            }

            Expression body = lambda.Body;
            if (lambda.Parameters.Count > 1)
                body = ReplaceParameter(lambda.Parameters[1], ContextProperty(dataContext)).Visit(body);

            var values = Expression.Parameter(typeof(object?[]), "values");
            var visitor = new ColumnSubstitutionVisitor(lambda.Parameters[0], values, indexByProperty, filter.Key, entityType, operation);
            body = visitor.Visit(body);
            if (visitor.Missing)
            {
                // Fail-closed: a filter that reads a column the statement does not write has no value to
                // check, so the write is rejected instead of silently bypassing the filter (a database
                // default can violate it).
                throw Omitted(filter.Key, entityType, operation, visitor.MissingMember);
            }

            if (ReferencesParameter(body, lambda.Parameters[0]))
                throw Unsupported(filter.Key, entityType, operation);

            var compiled = Expression.Lambda<Func<object?[], bool>>(body, values).Compile();
            predicates.Add((filter.Key, compiled));
        }

        return predicates;
    }

    // The identity of a mapped member, independent of the reflection type it was read through: the
    // declaring type plus the metadata token (ReflectedType is not part of the member's identity).
    private readonly record struct PropertyKey(Type DeclaringType, int MetadataToken);

    private static PropertyKey CanonicalPropertyKey(Type entityType, PropertyInfo property)
        => PropertyKeyOf(ResolveOnConcreteType(entityType, property));

    // The canonical identity of an already-resolved member: the declaring type plus the metadata token
    // (ReflectedType is not part of the member's identity).
    private static PropertyKey PropertyKeyOf(PropertyInfo property)
        => new(property.DeclaringType ?? property.ReflectedType!, property.MetadataToken);

    // The same property as seen on the concrete entity type, so an interface-, base- or new-hidden
    // member read by a filter resolves to the mapped column. A public member named like the filter's
    // member (declared on the concrete type, else inherited) is preferred: it covers a base/new-hidden
    // read and the concrete counterpart of an interface member, including an explicit implementation
    // that also exposes the column publicly. The interface map is consulted next, for a member whose
    // implementation is not exposed under the same name; an explicitly implemented interface member
    // whose synthesized declaration is the only match is kept, so a filter reading an unmapped
    // interface member still fails closed rather than validating the wrong slot.
    private static PropertyInfo ResolveOnConcreteType(Type entityType, PropertyInfo property)
    {
        if (entityType.IsInterface)
            return property;

        var byName = entityType.GetProperty(
                property.Name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            ?? entityType.GetProperty(property.Name, BindingFlags.Instance | BindingFlags.Public);
        if (byName is not null)
            return byName;

        if (property.DeclaringType is { IsInterface: true } interfaceType
            && ResolveInterfaceImplementation(entityType, interfaceType, property) is { } implemented)
            return implemented;

        return property;
    }

    private static PropertyInfo? ResolveInterfaceImplementation(Type entityType, Type interfaceType, PropertyInfo interfaceProperty)
    {
        if (!interfaceType.IsAssignableFrom(entityType))
            return null;

        var accessor = interfaceProperty.GetMethod ?? interfaceProperty.SetMethod;
        if (accessor is null)
            return null;

        var map = entityType.GetInterfaceMap(interfaceType);
        var index = Array.IndexOf(map.InterfaceMethods, accessor);
        if (index < 0)
            return null;

        var target = map.TargetMethods[index];
        const BindingFlags instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        foreach (var candidate in entityType.GetProperties(instance))
        {
            if (candidate.GetMethod == target || candidate.SetMethod == target)
                return candidate;
        }

        return null;
    }

    private static (Expression<Func<TResult, bool>> Predicate, string? Key)? BuildSourceViolation<TEntity, TResult>(
        QueryFilterScope scope,
        IDataContext dataContext,
        string operation,
        IReadOnlyList<IPropertyMetadata>? projectedColumns)
    {
        var filters = QueryFilterResolver.GetFilters(typeof(TEntity), scope, dataContext);
        if (filters.Count == 0)
            return null;

        // The columns the projection actually writes, keyed by the canonical identity of the mapped
        // target member. A concrete entity initializer can use the target type as its shape while
        // omitting a column the filter reads, so the result type alone cannot tell whether the value is
        // present; the projected column list can. The identity is canonical so a filter that reads a
        // base-declared or new-hidden member matches the column it maps to.
        HashSet<PropertyKey>? projectedKeys = null;
        if (projectedColumns is not null)
        {
            projectedKeys = new HashSet<PropertyKey>();
            for (var i = 0; i < projectedColumns.Count; i++)
                projectedKeys.Add(CanonicalPropertyKey(typeof(TEntity), projectedColumns[i].PropertyInfo));
        }

        var parameter = Expression.Parameter(typeof(TResult), "v");
        Expression? combined = null;
        string? soleKey = null;
        var contributing = 0;

        foreach (var filter in filters)
        {
            if (filter.Lambda is not { Parameters.Count: >= 1 } lambda)
            {
                if (filter.Func is not null)
                    throw UnsupportedFunc(filter.Key, typeof(TEntity), operation);
                continue;
            }

            Expression body = lambda.Body;
            if (lambda.Parameters.Count > 1)
                body = ReplaceParameter(lambda.Parameters[1], ContextProperty(dataContext)).Visit(body);

            var visitor = new ResultMemberRewriteVisitor(lambda.Parameters[0], parameter, typeof(TEntity), typeof(TResult), projectedKeys);
            body = visitor.Visit(body);
            if (visitor.Missing)
            {
                // Fail-closed: the source does not project a column the target filter reads, so the
                // pre-check cannot see the value that would be written (a database default can violate
                // the filter).
                throw Omitted(filter.Key, typeof(TEntity), operation, visitor.MissingMember);
            }

            if (ReferencesParameter(body, lambda.Parameters[0]))
                throw Unsupported(filter.Key, typeof(TEntity), operation);

            combined = combined is null ? body : Expression.AndAlso(combined, body);
            soleKey = filter.Key;
            contributing++;
        }

        if (combined is null)
            return null;

        // The pre-check ANDs every active filter, so it can only name the failing one when a single
        // filter contributed; with several filters the violating row is not attributable to one key and
        // the message stays generic.
        var key = contributing == 1 ? soleKey : null;
        return (Expression.Lambda<Func<TResult, bool>>(Expression.Not(combined), parameter), key);
    }

    private static Expression RewriteParameters(LambdaExpression filter, IDataContext dataContext, Expression from, Expression to)
    {
        Expression body = filter.Body;
        if (filter.Parameters.Count > 1)
            body = ReplaceParameter(filter.Parameters[1], ContextProperty(dataContext)).Visit(body);

        return ReplaceParameter(from, to).Visit(body);
    }

    // The filter's context parameter is substituted with a member access on a host constant, so the
    // compiled predicate reads the live context at evaluation time (mirroring the query preparer).
    private static Expression ContextProperty(IDataContext dataContext)
    {
        var host = new QueryFilterContext(dataContext);
        return Expression.Property(Expression.Constant(host), nameof(QueryFilterContext.Context));
    }

    private static ReplaceParameterVisitor ReplaceParameter(Expression from, Expression to) => new(from, to);

    private static QueryFilterException Violation(string? filterKey, Type entityType, string operation, int? rowIndex)
    {
        var key = FilterKeyText(filterKey);
        var row = rowIndex is int index ? $" (row {index})" : string.Empty;
        return new QueryFilterException(
            $"{operation} on {entityType.Name} would write a row that violates {key}{row}. Disable the filter for this statement with IgnoreFilters, or change the written values.");
    }

    private static QueryFilterException Omitted(string? filterKey, Type entityType, string operation, string? column)
    {
        var key = FilterKeyText(filterKey);
        var columnText = column is null ? "a column" : $"the '{column}' column";
        return new QueryFilterException(
            $"{operation} on {entityType.Name} does not write {columnText}, which {key} reads, so the filter cannot be validated. Write the column explicitly or disable the filter for this statement with IgnoreFilters.");
    }

    // A filter the in-memory column/projection rewrite cannot evaluate (a field read or an instance
    // member call leaves the entity parameter unbound). The write is rejected fail-closed rather than
    // letting the obscure Compiled-expression exception escape.
    private static QueryFilterException Unsupported(string? filterKey, Type entityType, string operation)
        => new(
            $"{operation} on {entityType.Name} cannot validate {FilterKeyText(filterKey)}: the filter reads a field or calls an instance member, which is not supported for in-memory validation. The write is rejected fail-closed (the filter cannot be evaluated). Rewrite the filter to read mapped properties, or disable the filter for this statement with IgnoreFilters.");

    // The builder-function filter is produced at query-plan build from the live context; the write
    // validator cannot safely evaluate it against a row (it may use bound SqlFunctions.Parameter
    // placeholders or other plan-time state), so a write with such a filter active is rejected
    // fail-closed instead of silently skipping the filter and letting a violating row through. The
    // rejection is a QueryFilterException like every other write-filter failure (the same path runs for
    // SQL providers, so the message must not claim an in-memory-only restriction).
    private static QueryFilterException UnsupportedFunc(string? filterKey, Type entityType, string operation)
        => new(
            $"{operation} on {entityType.Name} cannot validate {FilterKeyText(filterKey)}: it is declared in the builder-function form (FilterFunc), which is evaluated only at query-plan build and cannot be checked against a written row. Disable the filter for this statement with IgnoreFilters, or declare the filter as a predicate (FilterLambda) instead.");

    // A written NULL for a non-nullable value-type column would otherwise unbox into a
    // NullReferenceException; report the rejected write instead.
    private static QueryFilterException NullValue(string? filterKey, Type entityType, string operation, string column)
        => new(
            $"{operation} on {entityType.Name} cannot validate {FilterKeyText(filterKey)}: the written value for the '{column}' column is null, which is not a valid value for its type. The write is rejected fail-closed; write a non-null value or disable the filter for this statement with IgnoreFilters.");

    // The single rendering of the filter key shared by every validation failure. An empty key is the
    // anonymous slot and reads as "the anonymous filter", mirroring the violation message.
    private static string FilterKeyText(string? filterKey) => filterKey switch
    {
        null => "an active filter",
        "" => "the anonymous filter",
        _ => $"the '{filterKey}' filter",
    };

    // Unboxes a written value while guarding the non-nullable value-type case: a compiled
    // Convert(objectNull, typeof(int)) would throw NullReferenceException, which is not a useful
    // diagnostic. Called only for non-nullable value types; reference and nullable types keep the plain
    // Convert.
    private static TValue RequireValue<TValue>(object? value, string? filterKey, Type entityType, string operation, string column)
    {
        if (value is null && typeof(TValue).IsValueType && Nullable.GetUnderlyingType(typeof(TValue)) is null)
            throw NullValue(filterKey, entityType, operation, column);

        return (TValue)value!;
    }

    private static readonly MethodInfo RequireValueMI =
        typeof(QueryFilterValidator).GetMethod(nameof(RequireValue), BindingFlags.NonPublic | BindingFlags.Static)!;

    // Whether the rewritten filter body still reads the filter's entity parameter, which means the
    // rewrite could not express it in terms of the written values / projection (a field or an instance
    // member); compiling that body would throw an obscure unbound-parameter exception.
    private static bool ReferencesParameter(Expression body, ParameterExpression parameter)
    {
        var finder = new ParameterReferenceFinder(parameter);
        finder.Visit(body);
        return finder.Found;
    }

    private sealed class ParameterReferenceFinder(ParameterExpression target) : ExpressionVisitor
    {
        public bool Found { get; private set; }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (ReferenceEquals(node, target))
                Found = true;

            return base.VisitParameter(node);
        }
    }

    private sealed class ReplaceParameterVisitor(Expression from, Expression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node)
            => ReferenceEquals(node, from) ? to : base.VisitParameter(node);
    }

    // Rewrites column reads off the filter's entity parameter to indexed reads of the row values. A
    // read of a column that is not among the written columns sets Missing (with the member name), which
    // makes the caller reject the write fail-closed.
    private sealed class ColumnSubstitutionVisitor(ParameterExpression entityParameter, ParameterExpression values, Dictionary<PropertyKey, int> indexByProperty, string? filterKey, Type entityType, string operation) : ExpressionVisitor
    {
        public bool Missing { get; private set; }

        public string? MissingMember { get; private set; }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (IsEntityRead(node.Expression) && node.Member is PropertyInfo property)
            {
                if (indexByProperty.TryGetValue(CanonicalPropertyKey(entityType, property), out var index))
                {
                    var value = Expression.ArrayIndex(values, Expression.Constant(index));
                    // A non-nullable value-type column read from a written NULL would unbox into a
                    // NullReferenceException; route it through the guard so the write fails with a
                    // QueryFilterException. Reference and nullable types keep the plain conversion.
                    return property.PropertyType.IsValueType && Nullable.GetUnderlyingType(property.PropertyType) is null
                        ? Expression.Call(
                            RequireValueMI.MakeGenericMethod(property.PropertyType),
                            value,
                            Expression.Constant(filterKey, typeof(string)),
                            Expression.Constant(entityType, typeof(Type)),
                            Expression.Constant(operation, typeof(string)),
                            Expression.Constant(property.Name, typeof(string)))
                        : Expression.Convert(value, node.Type);
                }

                Missing = true;
                MissingMember ??= property.Name;
                return node;
            }

            return base.VisitMember(node);
        }

        private bool IsEntityRead(Expression? expression)
            => expression is ParameterExpression parameter && ReferenceEquals(parameter, entityParameter);
    }

    // Rewrites target-entity member reads onto the projection row type. The read is first resolved to
    // its declaration on the target entity type (so a base-declared or new-hidden member maps to the
    // column it actually writes), then matched by canonical identity against the projected columns and
    // rewritten to the projection member of the same name. A member outside the projection sets Missing
    // (with the member name), which makes the caller reject the write fail-closed.
    private sealed class ResultMemberRewriteVisitor(
        ParameterExpression entityParameter,
        ParameterExpression resultParameter,
        Type targetType,
        Type resultType,
        IReadOnlySet<PropertyKey>? projectedKeys) : ExpressionVisitor
    {
        public bool Missing { get; private set; }

        public string? MissingMember { get; private set; }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (IsEntityRead(node.Expression) && node.Member is PropertyInfo property)
            {
                var concrete = ResolveOnConcreteType(targetType, property);
                if (projectedKeys is not null && !projectedKeys.Contains(PropertyKeyOf(concrete)))
                {
                    Missing = true;
                    MissingMember ??= property.Name;
                    return node;
                }

                var mapped = resultType.GetProperty(concrete.Name, BindingFlags.Instance | BindingFlags.Public);
                if (mapped is not null)
                    return Expression.MakeMemberAccess(resultParameter, mapped);

                Missing = true;
                MissingMember ??= property.Name;
                return node;
            }

            return base.VisitMember(node);
        }

        private bool IsEntityRead(Expression? expression)
            => expression is ParameterExpression parameter && ReferenceEquals(parameter, entityParameter);
    }

    // Resolves a member read off the filter's entity parameter to its declaration on the written entity
    // type: a base-declared member stays as declared, an interface member is mapped to its
    // implementation and a new-hidden member resolves to the hiding declaration. Rebuilding the member
    // access makes the compiled entity-row check read the same mapped column as the column and source
    // paths.
    private sealed class ConcreteMemberRewriteVisitor(ParameterExpression entityParameter, Type entityType) : ExpressionVisitor
    {
        protected override Expression VisitMember(MemberExpression node)
        {
            if (IsEntityRead(node.Expression) && node.Member is PropertyInfo property)
            {
                var concrete = ResolveOnConcreteType(entityType, property);
                if (!ReferenceEquals(concrete, property))
                    return Expression.MakeMemberAccess(node.Expression, concrete);
            }

            return base.VisitMember(node);
        }

        private bool IsEntityRead(Expression? expression)
            => expression is ParameterExpression parameter && ReferenceEquals(parameter, entityParameter);
    }
}
