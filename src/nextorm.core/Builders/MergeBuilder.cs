using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace NextORM.Core;

/// <summary>
/// Fluent builder for a key upsert (a "merge" of a source row set into the target table), started with
/// <see cref="DataContextExtensions.MergeInto{TEntity}"/>. The source is a mapped entity or a batch of
/// entities; the database decides, per key, whether to update the existing row or insert a new one. The
/// statement is rendered through the active dialect as <c>INSERT ... ON CONFLICT ... DO UPDATE</c>
/// (PostgreSQL, SQLite), <c>INSERT ... ON DUPLICATE KEY UPDATE</c> (MySQL, MariaDB) or <c>MERGE</c>
/// (SQL Server).
/// <para>
/// There is deliberately no change tracking: every terminal (<see cref="Merge"/>, <see cref="MergeAsync"/>)
/// issues one explicit command, as with the insert builder. The builder is single-use.
/// </para>
/// </summary>
/// <typeparam name="TEntity">The mapped entity type upserted.</typeparam>
/// <remarks>
/// A key upsert is defined by the <see cref="Using(TEntity)"/> source, the <see cref="OnKeys"/> match
/// key and both branches <see cref="WhenMatchedUpdate"/> and <see cref="WhenNotMatchedInsert"/>; all
/// four are required. <see cref="OnKeys"/> resolves the key from the entity mapping, so it must be
/// declared before the merge; some providers (MySQL, MariaDB) render the update without naming the key
/// because their native <c>ON DUPLICATE KEY UPDATE</c> clause has no key list.
/// </remarks>
public sealed partial class MergeBuilder<TEntity>
{
    private readonly IDataContext _dataContext;
    private readonly IEntityMetadata _metadata;
    private readonly List<ColumnAccumulator> _columns = [];
    private readonly List<MergeBranch> _branches = [];
    private DynamicColumnSet? _dynamicColumns;
    private IReadOnlyList<TEntity>? _sourceEntities;
    private QueryCommand? _source;
    private int _rowCount;
    private IReadOnlyList<IPropertyMetadata>? _keys;
    private bool _whenMatchedUpdate;
    private bool _whenNotMatchedInsert;
    private LambdaExpression? _matchCondition;
    private QueryCommand? _registry;
    private QueryFilterScope _filterScope = QueryFilterScope.None;

    internal MergeBuilder(IDataContext dataContext, IEntityMetadata metadata)
    {
        _dataContext = dataContext;
        _metadata = metadata;
    }

    /// <summary>
    /// Disables <b>all</b> global query filters declared for the target entity type for this statement:
    /// the merge target is no longer constrained or refused by them and the inserted side is not
    /// validated against them. Repeatable: a later call accumulates with the earlier scope.
    /// </summary>
    /// <returns>This builder, for chaining.</returns>
    public MergeBuilder<TEntity> IgnoreFilters()
    {
        _filterScope = _filterScope.Union(QueryFilterScope.AllFilters);
        return this;
    }

    /// <summary>
    /// Disables every global query filter declared for the given entity types for this statement: the
    /// merge target is no longer constrained or refused by them and the inserted side is not validated
    /// against them. An empty or <see langword="null"/> <paramref name="entityTypes"/> disables nothing.
    /// Repeatable: a later call accumulates (union) with the earlier scope.
    /// </summary>
    /// <param name="entityTypes">The entity types whose filters are disabled.</param>
    /// <returns>This builder, for chaining.</returns>
    public MergeBuilder<TEntity> IgnoreFilters(params Type[] entityTypes)
    {
        var scope = QueryFilterScope.FromTypes(entityTypes);
        if (!scope.IsEmpty)
            _filterScope = _filterScope.Union(scope);

        return this;
    }

    /// <summary>
    /// Disables the named global query filters identified by <paramref name="filterKeys"/> on the target
    /// entity type for this statement: the merge target is no longer constrained or refused by them and
    /// the inserted side is not validated against them. An empty or <see langword="null"/>
    /// <paramref name="filterKeys"/> disables nothing. Repeatable: a later call accumulates (union) with
    /// the earlier scope.
    /// </summary>
    /// <param name="filterKeys">The filter keys to disable.</param>
    /// <returns>This builder, for chaining.</returns>
    public MergeBuilder<TEntity> IgnoreFilters(IEnumerable<string> filterKeys)
    {
        var scope = QueryFilterScope.FromKeys(filterKeys);
        if (!scope.IsEmpty)
            _filterScope = _filterScope.Union(scope);

        return this;
    }

    /// <summary>
    /// Disables the named global query filters identified by <paramref name="filterKeys"/> only on the
    /// given <paramref name="entityTypes"/> (the intersection of keys and types); an empty
    /// <paramref name="entityTypes"/> means any entity type. The disabled filters no longer constrain,
    /// refuse or validate this merge's target. The key list is the gate: an empty or
    /// <see langword="null"/> <paramref name="filterKeys"/> disables nothing even when entity types are
    /// supplied. Repeatable: a later call accumulates (union) with the earlier scope.
    /// </summary>
    /// <param name="filterKeys">The filter keys to disable.</param>
    /// <param name="entityTypes">The entity types the disable is scoped to; empty means any entity type.</param>
    /// <returns>This builder, for chaining.</returns>
    public MergeBuilder<TEntity> IgnoreFilters(IEnumerable<string> filterKeys, params Type[] entityTypes)
    {
        var scope = QueryFilterScope.FromKeysAndTypes(filterKeys, entityTypes);
        if (!scope.IsEmpty)
            _filterScope = _filterScope.Union(scope);

        return this;
    }

    /// <summary>
    /// Validates the rows written by the merge's insert branch against the target entity type's active
    /// global query filters. Entity/batch sources are checked in memory; a query source is guarded by a
    /// server-side pre-check. Called by every execution terminal before the statement runs. On the
    /// supported full-<c>MERGE</c> form this covers every branch combination (insert, update-only and
    /// delete-only): the incoming source values are validated even when no row is inserted.
    /// </summary>
    /// <exception cref="QueryFilterException">A merged row violates an active filter.</exception>
    internal void ValidateFilters()
    {
        EnsureFilteredFormSupported();

        if (_filterScope.All)
            return;

        if (_source is QueryCommand<TEntity> typedSource)
        {
            QueryFilterValidator.ValidateSource<TEntity, TEntity>(typedSource, null, _filterScope, _dataContext, "MERGE");
            return;
        }

        if (_sourceEntities is { } entities)
            QueryFilterValidator.ValidateEntities(entities, _filterScope, _dataContext, "MERGE");
    }

    /// <summary>
    /// Asynchronously validates the rows written by the merge's insert branch. A query source is
    /// pre-checked with a genuinely asynchronous existence query; entity/batch sources are checked in
    /// memory and complete immediately.
    /// </summary>
    /// <param name="cancellationToken">Cancels the source pre-check.</param>
    /// <returns>A task that completes when validation has passed.</returns>
    /// <exception cref="QueryFilterException">A merged row violates an active filter.</exception>
    internal Task ValidateFiltersAsync(CancellationToken cancellationToken)
    {
        EnsureFilteredFormSupported();

        if (_filterScope.All)
            return Task.CompletedTask;

        if (_source is QueryCommand<TEntity> typedSource)
            return QueryFilterValidator.ValidateSourceAsync<TEntity, TEntity>(typedSource, null, _filterScope, _dataContext, "MERGE", cancellationToken);

        if (_sourceEntities is { } entities)
            QueryFilterValidator.ValidateEntities(entities, _filterScope, _dataContext, "MERGE");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Fail-closed capability guard for an active target filter. A mutation that cannot express the
    /// filter as an atomic predicate on the write target (the in-memory key upsert, <c>ON CONFLICT</c> and
    /// <c>ON DUPLICATE KEY</c> upserts, or any form without full-<c>MERGE</c> target-predicate support)
    /// must refuse with <see cref="NotSupportedException"/> before source enumeration, validation or any
    /// connection/command. The full-<c>MERGE</c> branch form on SQL Server / PostgreSQL 15+ and the SQL
    /// Server key-upsert (routed through the same filtered <c>MERGE</c>) are allowed. Decided from
    /// metadata only — no database round-trip.
    /// </summary>
    /// <exception cref="NotSupportedException">The active form cannot isolate the write target.</exception>
    internal void EnsureFilteredFormSupported()
    {
        if (_filterScope.All || QueryFilterResolver.GetFilters(typeof(TEntity), _filterScope, _dataContext).Count == 0)
            return;

        // The form is decision-relevant only by actual branches: `.On(...)` without a branch is not a
        // renderable full MERGE, it leaves the builder on the key-upsert path, so `.On(...)` alone must
        // not make the full-MERGE capability check pass.
        var fullMergeForm = _branches.Count > 0;
        if (_dataContext is DataContext database)
        {
            if (fullMergeForm ? database.Dialect.SupportsMergeStatement : database.Dialect.SupportsMerge)
                return;
        }

        throw new NotSupportedException(
            "An active global query filter cannot isolate the write target of this merge atomically: the " +
            "provider's key upsert form would silently bypass it. Call IgnoreFilters() to disable the " +
            "filter, or use the full-MERGE branch form on a provider that supports it (SQL Server, " +
            "PostgreSQL 15+).");
    }

    /// <summary>Uses a single mapped entity as the source row.</summary>
    /// <param name="entity">The entity whose column values form the source row.</param>
    /// <returns>This builder, for chaining.</returns>
    [OverloadResolutionPriority(1)]
    public MergeBuilder<TEntity> Using(TEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return Using([entity]);
    }

    /// <summary>
    /// Uses a batch of mapped entities as the source rows. Identity and computed columns are excluded
    /// automatically.
    /// </summary>
    /// <param name="entities">The entities whose column values form the source rows.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="InvalidOperationException">The source was already specified.</exception>
    [OverloadResolutionPriority(1)]
    public MergeBuilder<TEntity> Using(IEnumerable<TEntity> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        if (_columns.Count > 0 || _source is not null)
            throw new InvalidOperationException("Using can only be specified once per merge.");

        var list = entities as IReadOnlyList<TEntity> ?? entities.ToList();
        if (list.Count == 0)
            throw new ArgumentException("At least one entity is required.", nameof(entities));

        foreach (var property in _metadata.Properties)
        {
            if (property.IsIdentity || property.IsComputed)
                continue;

            var accumulator = new ColumnAccumulator { Property = property };
            for (var i = 0; i < list.Count; i++)
                accumulator.Values.Add(InsertValue.FromConstant(property.PropertyInfo.GetValue(list[i])));

            _columns.Add(accumulator);
        }

        var dynamicColumns = DynamicColumnSet.FromEntities(_metadata, list, "MERGE");

        if (_columns.Count == 0 && dynamicColumns is null)
            throw new BuildSqlCommandException($"Entity {typeof(TEntity)} has no writable column to merge.");

        _sourceEntities = list;
        _dynamicColumns = dynamicColumns;
        _rowCount = list.Count;
        return this;
    }

    /// <summary>
    /// Uses a server-side query as the source rows (<c>MERGE ... USING (&lt;select&gt;) AS source</c>).
    /// The query is an entity query over <typeparamref name="TEntity"/>, so its output columns line up
    /// with the target. Only the full-<c>MERGE</c> branch form is supported for a query source.
    /// </summary>
    /// <param name="source">The entity query supplying the source rows.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="InvalidOperationException">The source was already specified.</exception>
    public MergeBuilder<TEntity> Using(EntityBuilder<TEntity> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return Using(source.ToCommand());
    }

    /// <summary>Uses a server-side query command as the source rows.</summary>
    /// <param name="source">The entity query command supplying the source rows.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="InvalidOperationException">The source was already specified.</exception>
    public MergeBuilder<TEntity> Using(QueryCommand<TEntity> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (_columns.Count > 0 || _source is not null)
            throw new InvalidOperationException("Using can only be specified once per merge.");

        _source = source;
        return this;
    }

    /// <summary>
    /// Matches existing rows on the entity's declared key column(s), resolved from metadata
    /// (<c>[Key]</c>, the fluent <c>.Key()</c> or the <c>Id</c>/<c>&lt;Type&gt;Id</c> convention).
    /// </summary>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="InvalidOperationException">The entity has no key property.</exception>
    /// <exception cref="NotSupportedException">A key column is database-generated.</exception>
    public MergeBuilder<TEntity> OnKeys()
    {
        if (_matchCondition is not null)
            throw new InvalidOperationException("OnKeys can only be specified once; On(...) was already called.");

        var keys = new List<IPropertyMetadata>();
        foreach (var property in _metadata.Properties)
        {
            if (!property.IsKey)
                continue;

            if (property.IsIdentity || property.IsComputed)
                throw new NotSupportedException(
                    $"Key property {property.PropertyInfo.Name} of {typeof(TEntity)} is database-generated and cannot be used as an upsert match key.");

            keys.Add(property);
        }

        if (keys.Count == 0)
            throw new InvalidOperationException(
                $"Entity {typeof(TEntity)} has no key property. Mark one with [Key]/.Key() before calling OnKeys().");

        _keys = keys;
        return this;
    }

    /// <summary>
    /// Matches existing rows on an arbitrary condition over the <c>(target, source)</c> row instead of
    /// the declared key. Only the full-<c>MERGE</c> branch form renders it, on providers that support a
    /// search condition (<see cref="ISqlDialect.SupportsMergeConditionalBranches"/>); the others reject it.
    /// </summary>
    /// <param name="condition">The match condition, for example <c>(t, s) =&gt; t.Id == s.Id &amp;&amp; s.Age &gt; 0</c>.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="InvalidOperationException">A match key was already declared with <see cref="OnKeys"/>.</exception>
    public MergeBuilder<TEntity> On(Expression<Func<TEntity, TEntity, bool>> condition)
    {
        ArgumentNullException.ThrowIfNull(condition);
        if (_keys is { Count: > 0 })
            throw new InvalidOperationException("On can only be specified once; OnKeys was already called.");
        if (_matchCondition is not null)
            throw new InvalidOperationException("On can only be specified once.");

        _matchCondition = condition;
        return this;
    }

    /// <summary>
    /// Rejects a match condition that references a mapped source column absent from the VALUES-derived
    /// source. A VALUES source is built from the writable mapped columns only (identity/computed are
    /// excluded), so any other mapped column has no matching derived column and must not be rendered as
    /// <c>source.&lt;column&gt;</c>. Called only on the VALUES path: a query source projects the whole row,
    /// generated columns included, so the same reference is valid there.
    /// </summary>
    /// <param name="condition">The <c>On(...)</c> or branch condition to inspect.</param>
    /// <exception cref="NotSupportedException">The condition references a column the VALUES source does not declare.</exception>
    private void ValidateValuesSourceCondition(LambdaExpression condition)
    {
        if (FindMissingSourceColumn(condition, _metadata, _columns) is { } missing)
        {
            throw new NotSupportedException(
                $"Property {missing.Name} of {typeof(TEntity)} is not a column of the VALUES-derived merge source; " +
                "a database-generated column is excluded from the source built from Using(entity)/Using(batch) and cannot be referenced there.");
        }
    }

    /// <summary>
    /// Finds the first mapped property referenced through the source lambda (<c>s</c>, the second
    /// parameter) that is not among the derived source columns. A non-mapped member (for example a
    /// dynamic-store key) is ignored.
    /// </summary>
    /// <param name="condition">The condition to inspect.</param>
    /// <param name="metadata">The target entity metadata, resolving a CLR property to its mapped column.</param>
    /// <param name="columns">The VALUES-derived source columns.</param>
    /// <returns>The offending property, or <see langword="null"/> when every source reference is a source column.</returns>
    private static PropertyInfo? FindMissingSourceColumn(LambdaExpression condition, IEntityMetadata metadata, IReadOnlyList<ColumnAccumulator> columns)
    {
        if (condition.Parameters.Count < 2)
            return null;

        var finder = new MissingSourceColumnFinder(condition.Parameters[1], metadata, columns);
        finder.Visit(condition.Body);
        return finder.Found;
    }

    /// <summary>
    /// Collects the first source-rooted mapped property whose column is absent from the derived source.
    /// </summary>
    private sealed class MissingSourceColumnFinder : ExpressionVisitor
    {
        private readonly ParameterExpression _source;
        private readonly IEntityMetadata _metadata;
        private readonly IReadOnlyList<ColumnAccumulator> _columns;

        public MissingSourceColumnFinder(ParameterExpression source, IEntityMetadata metadata, IReadOnlyList<ColumnAccumulator> columns)
        {
            _source = source;
            _metadata = metadata;
            _columns = columns;
        }

        /// <summary>The first offending property, or <see langword="null"/> when none was found.</summary>
        public PropertyInfo? Found { get; private set; }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (Found is null
                && node.Member is PropertyInfo property
                && IsRootedAtSource(node)
                && IsMapped(property)
                && !IsSourceColumn(property))
            {
                Found = property;
            }

            return base.VisitMember(node);
        }

        // A member access is rooted at the source when walking its receiver chain ends at the source
        // parameter (so s.Id and s.Nested.Id are source accesses, while t.Id is not).
        private bool IsRootedAtSource(Expression expression)
        {
            while (expression is MemberExpression member)
                expression = member.Expression!;

            return expression == _source;
        }

        private bool IsMapped(PropertyInfo property)
        {
            foreach (var candidate in _metadata.Properties)
            {
                if (candidate.PropertyInfo == property)
                    return true;
            }

            return false;
        }

        private bool IsSourceColumn(PropertyInfo property)
        {
            for (var i = 0; i < _columns.Count; i++)
            {
                if (_columns[i].Property.PropertyInfo == property)
                    return true;
            }

            return false;
        }
    }

    /// <summary>Updates every non-key writable column from the source when the row already exists.</summary>
    /// <returns>This builder, for chaining.</returns>
    public MergeBuilder<TEntity> WhenMatchedUpdate()
    {
        _whenMatchedUpdate = true;
        return this;
    }

    /// <summary>Inserts a new row when no key matches.</summary>
    /// <returns>This builder, for chaining.</returns>
    public MergeBuilder<TEntity> WhenNotMatchedInsert()
    {
        _whenNotMatchedInsert = true;
        return this;
    }

    /// <summary>
    /// Introduces a full-<c>MERGE</c> <c>WHEN MATCHED</c> branch; finish it with
    /// <c>ThenUpdate()</c>/<c>ThenDelete()</c>. Available only on providers that render a general
    /// <c>MERGE</c> (SQL Server, PostgreSQL 15+); the other dialects reject it with
    /// <see cref="NotSupportedException"/>.
    /// </summary>
    /// <returns>A branch builder whose terminal returns this merge builder.</returns>
    public MergeMatchedBuilder<TEntity> WhenMatched() => new(this);

    /// <summary>
    /// Introduces a conditional full-<c>MERGE</c> <c>WHEN MATCHED</c> branch (<c>WHEN MATCHED AND &lt;condition&gt;</c>);
    /// finish it with <c>ThenUpdate()</c>/<c>ThenDelete()</c>/<c>ThenDoNothing()</c>.
    /// </summary>
    /// <param name="condition">The branch condition over <c>(target, source)</c>, for example <c>(t, s) =&gt; t.Name != s.Name</c>.</param>
    /// <returns>A branch builder whose terminal returns this merge builder.</returns>
    public MergeMatchedBuilder<TEntity> WhenMatched(Expression<Func<TEntity, TEntity, bool>> condition)
    {
        ArgumentNullException.ThrowIfNull(condition);
        return new(this, condition);
    }

    /// <summary>
    /// Introduces a full-<c>MERGE</c> <c>WHEN NOT MATCHED [BY TARGET]</c> branch; finish it with
    /// <c>ThenInsert()</c>. Available only on providers that render a general <c>MERGE</c>.
    /// </summary>
    /// <returns>A branch builder whose terminal returns this merge builder.</returns>
    public MergeNotMatchedBuilder<TEntity> WhenNotMatched() => new(this);

    /// <summary>
    /// Introduces a conditional full-<c>MERGE</c> <c>WHEN NOT MATCHED [BY TARGET]</c> branch
    /// (<c>WHEN NOT MATCHED AND &lt;condition&gt;</c>); finish it with <c>ThenInsert()</c>/<c>ThenDoNothing()</c>.
    /// </summary>
    /// <param name="condition">The branch condition over <c>(target, source)</c>.</param>
    /// <returns>A branch builder whose terminal returns this merge builder.</returns>
    public MergeNotMatchedBuilder<TEntity> WhenNotMatched(Expression<Func<TEntity, TEntity, bool>> condition)
    {
        ArgumentNullException.ThrowIfNull(condition);
        return new(this, condition);
    }

    /// <summary>
    /// Introduces a full-<c>MERGE</c> <c>WHEN NOT MATCHED BY SOURCE</c> branch; finish it with
    /// <c>ThenDelete()</c>. Available only on SQL Server, the sole supported provider that renders this
    /// branch; every other dialect rejects it with <see cref="NotSupportedException"/>.
    /// </summary>
    /// <returns>A branch builder whose terminal returns this merge builder.</returns>
    public MergeNotMatchedBySourceBuilder<TEntity> WhenNotMatchedBySource() => new(this);

    /// <summary>
    /// Introduces a conditional full-<c>MERGE</c> <c>WHEN NOT MATCHED BY SOURCE</c> branch
    /// (<c>WHEN NOT MATCHED BY SOURCE AND &lt;condition&gt;</c>); finish it with <c>ThenDelete()</c>/<c>ThenDoNothing()</c>.
    /// SQL Server only allows the condition to reference the target row.
    /// </summary>
    /// <param name="condition">The branch condition over <c>(target, source)</c>.</param>
    /// <returns>A branch builder whose terminal returns this merge builder.</returns>
    public MergeNotMatchedBySourceBuilder<TEntity> WhenNotMatchedBySource(Expression<Func<TEntity, TEntity, bool>> condition)
    {
        ArgumentNullException.ThrowIfNull(condition);
        return new(this, condition);
    }

    /// <summary>
    /// Switches the builder to a row-returning terminal that materialises the whole merged row through the
    /// provider's <c>OUTPUT</c>/<c>RETURNING</c> form. Both the full-<c>MERGE</c> branch form and the
    /// key-upsert form (<c>ON CONFLICT ... DO UPDATE</c>, <c>MERGE</c>) are supported where the provider
    /// has a row-returning clause; MySQL/MariaDB reject the key-upsert form.
    /// </summary>
    /// <returns>A returning builder whose terminals produce <typeparamref name="TEntity"/>.</returns>
    /// <exception cref="NotSupportedException">A mapped property of <typeparamref name="TEntity"/> cannot be projected.</exception>
    public MergeReturningBuilder<TEntity, TEntity> Returning()
    {
        var parameter = Expression.Parameter(typeof(TEntity), "x");
        var identity = Expression.Lambda<Func<TEntity, TEntity>>(parameter, parameter);
        var (columns, selectList, oneColumn) = ReturningProjection.Parse(identity, _metadata.Properties, FindMapped);
        return new MergeReturningBuilder<TEntity, TEntity>(this, columns, selectList, oneColumn);
    }

    /// <summary>
    /// Switches the builder to a row-returning terminal that materialises a projection of the merged row
    /// through the provider's <c>OUTPUT</c>/<c>RETURNING</c> form. Both the full-<c>MERGE</c> branch form
    /// and the key-upsert form are supported where the provider has a row-returning clause.
    /// </summary>
    /// <typeparam name="TResult">The projected row shape.</typeparam>
    /// <param name="projection">Selects the mapped columns to return.</param>
    /// <returns>A returning builder whose terminals produce <typeparamref name="TResult"/>.</returns>
    /// <exception cref="NotSupportedException">The projection references something other than mapped properties.</exception>
    public MergeReturningBuilder<TEntity, TResult> Returning<TResult>(Expression<Func<TEntity, TResult>> projection)
    {
        ArgumentNullException.ThrowIfNull(projection);
        var (columns, selectList, oneColumn) = ReturningProjection.Parse(projection, _metadata.Properties, FindMapped);
        return new MergeReturningBuilder<TEntity, TResult>(this, columns, selectList, oneColumn);
    }

    /// <summary>Adds an update branch over <paramref name="columns"/> (or every non-key column when <see langword="null"/>).</summary>
    internal MergeBuilder<TEntity> AddMatchedUpdate(LambdaExpression? columns, LambdaExpression? condition = null)
        => AddBranch(MergeMatchKind.Matched, MergeActionKind.Update, columns, condition);

    /// <summary>Adds a delete branch of <paramref name="match"/> kind.</summary>
    internal MergeBuilder<TEntity> AddDelete(MergeMatchKind match, LambdaExpression? condition = null)
    {
        _branches.Add(new MergeBranch(match, MergeActionKind.Delete, [], condition));
        return this;
    }

    /// <summary>Adds a <c>DO NOTHING</c> branch of <paramref name="match"/> kind.</summary>
    internal MergeBuilder<TEntity> AddDoNothing(MergeMatchKind match, LambdaExpression? condition = null)
    {
        _branches.Add(new MergeBranch(match, MergeActionKind.Nothing, [], condition));
        return this;
    }

    /// <summary>Adds an insert branch over <paramref name="columns"/> (or every writable column when <see langword="null"/>).</summary>
    internal MergeBuilder<TEntity> AddNotMatchedInsert(LambdaExpression? columns, LambdaExpression? condition = null)
        => AddBranch(MergeMatchKind.NotMatchedByTarget, MergeActionKind.Insert, columns, condition);

    /// <summary>
    /// Renders the parameterised SQL this builder would execute, without executing it. Useful for
    /// diagnostics and for verifying SQL generation without a database.
    /// </summary>
    /// <returns>The rendered SQL text.</returns>
    public string ToSql()
    {
        if (_dataContext is IMutationExecutor executor)
            return executor.Render(BuildCommand());

        throw new NotSupportedException($"{_dataContext.GetType().Name} cannot render SQL: it is not a database-backed context.");
    }

    /// <summary>Executes the merge and returns the number of affected rows.</summary>
    /// <returns>The number of rows inserted or updated, as reported by the provider.</returns>
    public int Merge()
    {
        ValidateFilters();

        if (_dataContext is InMemoryDataContext inMemory)
            return MergeInMemory(inMemory);

        return RequireExecutor().Execute(BuildCommand());
    }

    /// <summary>Asynchronously executes the merge and returns the number of affected rows.</summary>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the number of rows inserted or updated.</returns>
    public async Task<int> MergeAsync(CancellationToken cancellationToken = default)
    {
        await ValidateFiltersAsync(cancellationToken).ConfigureAwait(false);

        if (_dataContext is InMemoryDataContext inMemory)
            return MergeInMemory(inMemory);

        return await RequireExecutor().Execute(BuildCommand(), cancellationToken).ConfigureAwait(false);
    }

    private MergeCommand BuildCommand(IReadOnlyList<IPropertyMetadata>? returningColumns = null)
    {
        EnsureFilteredFormSupported();

        if (_columns.Count == 0 && _source is null && _dynamicColumns is null)
            throw new InvalidOperationException("No source rows were specified; call Using first.");
        if (_keys is null && _matchCondition is null)
            throw new InvalidOperationException("No match condition was specified; call OnKeys() or On(...).");
        if (_matchCondition is not null && _branches.Count == 0)
            throw new InvalidOperationException("On(...) requires at least one branch; add WhenMatched()/WhenNotMatched().");

        foreach (var column in _columns)
        {
            if (column.Values.Count != _rowCount)
                throw new BuildSqlCommandException($"Column {column.Property.PropertyInfo.Name} has {column.Values.Count} values but the merge writes {_rowCount} row(s).");
        }

        var columns = new InsertColumn[_columns.Count];
        for (var i = 0; i < columns.Length; i++)
            columns[i] = new InsertColumn(_columns[i].Property, _columns[i].Values);

        // The VALUES-derived source declares only the writable mapped columns; a condition that reaches a
        // generated column through the source has no matching derived column. A query source projects the
        // whole row (generated columns included), so its conditions are left to the provider.
        if (_source is null)
        {
            if (_matchCondition is not null)
                ValidateValuesSourceCondition(_matchCondition);

            foreach (var branch in _branches)
            {
                if (branch.Condition is not null)
                    ValidateValuesSourceCondition(branch.Condition);
            }
        }

        if (_branches.Count > 0 || _matchCondition is not null)
        {
            var hasCondition = _matchCondition is not null;
            if (!hasCondition)
            {
                foreach (var branch in _branches)
                {
                    if (branch.Condition is not null)
                    {
                        hasCondition = true;
                        break;
                    }
                }
            }

            var registry = hasCondition || HasActiveFilter() ? _registry ??= new EntityBuilder<TEntity>(_dataContext).ToCommand() : null;
            return new MergeCommand(typeof(TEntity), _metadata.TableName!, _metadata.IsTableNameAuto, columns, _rowCount, _keys ?? [], [], [.. _branches], returningColumns, _source, _matchCondition, registry, _dynamicColumns, _filterScope);
        }

        if (_source is not null)
            throw new NotSupportedException("A query source requires the full-MERGE branch form (WhenMatched()/WhenNotMatched()); the key-upsert form only accepts an entity or batch.");

        if (!_whenMatchedUpdate || !_whenNotMatchedInsert)
            throw new InvalidOperationException("A key upsert requires both WhenMatchedUpdate() and WhenNotMatchedInsert().");

        var updateColumns = new List<IPropertyMetadata>();
        foreach (var column in _columns)
        {
            if (!column.Property.IsKey)
                updateColumns.Add(column.Property);
        }

        if (updateColumns.Count == 0 && _dynamicColumns is null)
            throw new NotSupportedException($"Entity {typeof(TEntity)} has only key columns; a key upsert needs at least one non-key column to update.");

        // A filtered SQL Server key upsert is re-rendered as a general MERGE with the target filter in
        // its ON, so it needs the condition-rendering registry even though it has no user condition.
        var keyRegistry = HasActiveFilter() ? _registry ??= new EntityBuilder<TEntity>(_dataContext).ToCommand() : null;
        return new MergeCommand(typeof(TEntity), _metadata.TableName!, _metadata.IsTableNameAuto, columns, _rowCount, _keys!, updateColumns, returningColumns: returningColumns, dynamicColumns: _dynamicColumns, filterScope: _filterScope, registry: keyRegistry);
    }

    private bool HasActiveFilter()
        => !_filterScope.All && QueryFilterResolver.GetFilters(typeof(TEntity), _filterScope, _dataContext).Count > 0;

    /// <summary>Builds the command whose <c>RETURNING</c>/<c>OUTPUT</c> clause returns <paramref name="returningColumns"/>.</summary>
    internal MergeCommand BuildReturningCommand(IReadOnlyList<IPropertyMetadata> returningColumns) => BuildCommand(returningColumns);

    /// <summary>The context the merge executes on; used by <see cref="MergeReturningBuilder{TEntity, TResult}"/>.</summary>
    internal IDataContext DataContext => _dataContext;

    private IMutationExecutor RequireExecutor()
    {
        if (_dataContext is IMutationExecutor executor)
            return executor;

        throw new NotSupportedException(
            $"{_dataContext.GetType().Name} does not support data modification. Use a database-backed context (SQLite, PostgreSQL, SQL Server, MySQL or MariaDB); the in-memory provider supports only the key-upsert form and ClickHouse has no DML upsert.");
    }

    private sealed class ColumnAccumulator
    {
        public required IPropertyMetadata Property { get; init; }
        public List<InsertValue> Values { get; } = [];
    }
}
