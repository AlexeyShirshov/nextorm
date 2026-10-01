using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Join builder over a two-table projection, produced by a <c>Join</c>/<c>LeftJoin</c>/<c>RightJoin</c>/
/// <c>FullJoin</c>/<c>CrossJoin</c> call.
/// </summary>
/// <remarks>
/// The <c>P</c> in the name denotes projection arity and is not self-explanatory. Recommended name:
/// <c>JoinedEntityBuilder&lt;T1, T2&gt;</c>. See <c>docs/specs/design/API-NAMING-REVIEW.md</c> finding P0-4.
/// </remarks>
public class JoinedEntityBuilder<T1, T2> : EntityBuilder<Projection<T1, T2>>
{
    /// <summary>The join expression that produced this two-table projection.</summary>
    public JoinExpression JoinCondition { get; set; }
    /// <summary>Initializes a builder over the two-table projection seeded with <paramref name="join"/>.</summary>
    /// <param name="dataProvider">The data context used to resolve tables and generate SQL.</param>
    /// <param name="join">The join expression that produced this projection.</param>
    public JoinedEntityBuilder(IDataContext dataProvider, JoinExpression join) : base(dataProvider)
    {
        JoinCondition = join;
        _joins = [join];
    }
    /// <summary>Adds an <c>INNER JOIN</c> over <typeparamref name="T3"/>; rows with no match on the right are excluded.</summary>
    /// <typeparam name="T3">The entity type of the joined table.</typeparam>
    /// <param name="_">The builder identifying the table to join; only its table metadata is used, not its query state.</param>
    /// <param name="joinCondition">A predicate relating the current projection to the joined entity.</param>
    /// <param name="options">Optional per-join configuration, for example <c>j =&gt; j.Global()</c> or <c>j =&gt; j.WithStrictness(JoinStrictness.Any)</c>.</param>
    /// <returns>A builder over the projection extended with <typeparamref name="T3"/>.</returns>
    public new JoinedEntityBuilder<T1, T2, T3> Join<T3>(EntityBuilder<T3> _, Expression<Func<Projection<T1, T2>, T3, bool>> joinCondition, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.Inner, joinCondition, options);
    /// <summary>Adds a <c>LEFT JOIN</c> over <typeparamref name="T3"/>; unmatched left-hand rows are kept with <see langword="null"/> values on the right.</summary>
    /// <typeparam name="T3">The entity type of the joined table.</typeparam>
    /// <param name="_">The builder identifying the table to join; only its table metadata is used, not its query state.</param>
    /// <param name="joinCondition">A predicate relating the current projection to the joined entity.</param>
    /// <param name="options">Optional per-join configuration, for example <c>j =&gt; j.Global()</c> or <c>j =&gt; j.WithStrictness(JoinStrictness.Any)</c>.</param>
    /// <returns>A builder over the projection extended with <typeparamref name="T3"/>.</returns>
    public new JoinedEntityBuilder<T1, T2, T3> LeftJoin<T3>(EntityBuilder<T3> _, Expression<Func<Projection<T1, T2>, T3, bool>> joinCondition, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.Left, joinCondition, options);
    /// <summary>Adds a <c>RIGHT JOIN</c> over <typeparamref name="T3"/>; unmatched right-hand rows are kept with <see langword="null"/> values on the left.</summary>
    /// <typeparam name="T3">The entity type of the joined table.</typeparam>
    /// <param name="_">The builder identifying the table to join; only its table metadata is used, not its query state.</param>
    /// <param name="joinCondition">A predicate relating the current projection to the joined entity.</param>
    /// <param name="options">Optional per-join configuration, for example <c>j =&gt; j.Global()</c> or <c>j =&gt; j.WithStrictness(JoinStrictness.Any)</c>.</param>
    /// <returns>A builder over the projection extended with <typeparamref name="T3"/>.</returns>
    public new JoinedEntityBuilder<T1, T2, T3> RightJoin<T3>(EntityBuilder<T3> _, Expression<Func<Projection<T1, T2>, T3, bool>> joinCondition, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.Right, joinCondition, options);
    /// <summary>Adds a <c>FULL JOIN</c> over <typeparamref name="T3"/>; rows from both sides are kept, with <see langword="null"/> on the missing side.</summary>
    /// <typeparam name="T3">The entity type of the joined table.</typeparam>
    /// <param name="_">The builder identifying the table to join; only its table metadata is used, not its query state.</param>
    /// <param name="joinCondition">A predicate relating the current projection to the joined entity.</param>
    /// <param name="options">Optional per-join configuration, for example <c>j =&gt; j.Global()</c> or <c>j =&gt; j.WithStrictness(JoinStrictness.Any)</c>.</param>
    /// <returns>A builder over the projection extended with <typeparamref name="T3"/>.</returns>
    public new JoinedEntityBuilder<T1, T2, T3> FullJoin<T3>(EntityBuilder<T3> _, Expression<Func<Projection<T1, T2>, T3, bool>> joinCondition, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.Full, joinCondition, options);
    /// <summary>Adds a <c>CROSS JOIN</c> over <typeparamref name="T3"/>, producing the Cartesian product without a join condition.</summary>
    /// <typeparam name="T3">The entity type of the joined table.</typeparam>
    /// <param name="_">The builder identifying the table to join; only its table metadata is used, not its query state.</param>
    /// <param name="options">Optional per-join configuration; a cross join rejects every modifier at render time, while an APPLY folds a join hint into the statement-level hint comment on inline-hint dialects.</param>
    /// <returns>A builder over the projection extended with <typeparamref name="T3"/>.</returns>
    public new JoinedEntityBuilder<T1, T2, T3> CrossJoin<T3>(EntityBuilder<T3> _, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.Cross, null, options);
    /// <summary>Adds a <c>CROSS APPLY</c> over <typeparamref name="T3"/>, evaluating the joined table once per left-hand row; rows with no match are dropped.</summary>
    /// <typeparam name="T3">The entity type of the joined table.</typeparam>
    /// <param name="_">The builder identifying the table to join; only its table metadata is used, not its query state.</param>
    /// <param name="options">Optional per-join configuration; a cross join rejects every modifier at render time, while an APPLY folds a join hint into the statement-level hint comment on inline-hint dialects.</param>
    /// <returns>A builder over the projection extended with <typeparamref name="T3"/>.</returns>
    public new JoinedEntityBuilder<T1, T2, T3> CrossApply<T3>(EntityBuilder<T3> _, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.CrossApply, null, options);
    /// <summary>Adds an <c>OUTER APPLY</c> over <typeparamref name="T3"/>, evaluating the joined table once per left-hand row; unmatched left rows are kept.</summary>
    /// <typeparam name="T3">The entity type of the joined table.</typeparam>
    /// <param name="_">The builder identifying the table to join; only its table metadata is used, not its query state.</param>
    /// <param name="options">Optional per-join configuration; a cross join rejects every modifier at render time, while an APPLY folds a join hint into the statement-level hint comment on inline-hint dialects.</param>
    /// <returns>A builder over the projection extended with <typeparamref name="T3"/>.</returns>
    public new JoinedEntityBuilder<T1, T2, T3> OuterApply<T3>(EntityBuilder<T3> _, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.OuterApply, null, options);
    /// <summary>Adds a ClickHouse <c>LEFT SEMI JOIN</c> over <paramref name="_"/>; only the left-hand columns survive.</summary>
    internal new JoinedEntityBuilder<T1, T2> SemiJoin<T3>(EntityBuilder<T3> _, Expression<Func<Projection<T1, T2>, T3, bool>> joinCondition, Action<JoinOptions>? options = null)
        => (JoinedEntityBuilder<T1, T2>)base.SemiJoin(_, joinCondition, options);
    /// <summary>Adds a ClickHouse <c>LEFT ANTI JOIN</c> over <paramref name="_"/>; only the left-hand columns survive.</summary>
    internal new JoinedEntityBuilder<T1, T2> AntiJoin<T3>(EntityBuilder<T3> _, Expression<Func<Projection<T1, T2>, T3, bool>> joinCondition, Action<JoinOptions>? options = null)
        => (JoinedEntityBuilder<T1, T2>)base.AntiJoin(_, joinCondition, options);
    /// <summary>Adds a ClickHouse <c>PASTE JOIN</c> over <paramref name="_"/>; the projection exposes both sides.</summary>
    internal new JoinedEntityBuilder<T1, T2, T3> PasteJoin<T3>(EntityBuilder<T3> _, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.Paste, null, options);
    /// <summary>
    /// Alias-join seam over an existing join chain; mirrors <see cref="JoinCore{T3}"/> but returns the
    /// caller-created generated builder, serving every <see cref="JoinType"/> through
    /// <paramref name="joinType"/> so a positional join can be followed by a generated alias join.
    /// </summary>
    /// <typeparam name="TNext">The generated builder type that receives the join chain.</typeparam>
    /// <typeparam name="TNextEntity">The projection type <typeparamref name="TNext"/> is built over.</typeparam>
    /// <typeparam name="T3">The entity type being joined.</typeparam>
    /// <param name="create">Creates an empty <typeparamref name="TNext"/> bound to this builder's data context.</param>
    /// <param name="_">The builder identifying the table to join; only its table metadata is used.</param>
    /// <param name="joinCondition">A predicate relating the current projection to the joined entity.</param>
    /// <param name="joinType">The kind of join to add.</param>
    /// <param name="options">Optional per-join configuration.</param>
    /// <returns>A new <typeparamref name="TNext"/> carrying this builder's joins plus the new one.</returns>
    public new TNext JoinAlias<TNext, TNextEntity, T3>(
        Func<IDataContext, TNext> create,
        EntityBuilder<T3> _,
        Expression<Func<Projection<T1, T2>, T3, bool>> joinCondition,
        JoinType joinType = JoinType.Inner,
        Action<JoinOptions>? options = null)
        where TNext : EntityBuilder<TNextEntity>
        => base.JoinAlias<TNext, TNextEntity, T3>(create, _, joinCondition, joinType, options);
    private JoinedEntityBuilder<T1, T2, T3> JoinCore<T3>(EntityBuilder<T3> _, JoinType joinType, LambdaExpression? joinCondition, Action<JoinOptions>? options = null)
    {
        if (Condition is not null)
            throw new NotImplementedException();

        var opts = new JoinOptions();
        options?.Invoke(opts);
        var cb = new JoinedEntityBuilder<T1, T2, T3>(DataProvider) { Logger = Logger, Query = Query, Table = Table, SourceFrom = SourceFrom, IsDistinct = IsDistinct, GroupingType = GroupingType, GroupingSets = GroupingSets, GroupByWithTotals = GroupByWithTotals, LimitByClause = LimitByClause, DistinctOnClause = DistinctOnClause, TableSampleClause = TableSampleClause, TemporalClause = TemporalClause, RowLockClause = RowLockClause, IsFinal = IsFinal, SampleRatio = SampleRatio, SampleOffset = SampleOffset, SettingsList = SettingsList, PreWhereCondition = PreWhereCondition, ArrayJoins = ArrayJoins, ArrayJoinKind = ArrayJoinKind, TableHints = TableHints, IndexHints = IndexHints, IndexHintKind = IndexHintKind, Ctes = Ctes, QuoteIdentifiers = QuoteIdentifiers, NamingConvention = NamingConvention, KeywordCase = KeywordCase };
        if (Joins is not null) cb.Joins!.AddRange(Joins);
        cb.Joins!.Add(new JoinExpression(joinCondition, joinType)
        {
            From = JoinSourceResolver.Resolve(_dataProvider, _),
            EntityType = joinCondition is null ? typeof(T3) : null,
            Strictness = opts.Strictness ?? JoinStrictness.Default,
            IsGlobal = opts.IsGlobal,
            JoinHint = opts.JoinHint,
            TableHints = opts.TableHints
        });
        cb.Ctes = CteMerge.Merge(Ctes, _.Ctes);
        return cb;
    }
    // protected override void OnCommandCreated<TResult>(QueryCommand<TResult> cmd)
    // {
    //     cmd.Joins.Add(JoinCondition);
    //     base.OnCommandCreated(cmd);
    //     // BaseBuilder!.RaiseCommandCreated(cmd);
    // }
    /// <inheritdoc/>
    protected override object CloneImp()
    {
        var r = new JoinedEntityBuilder<T1, T2>(DataProvider, JoinCondition) { Logger = Logger };

        CopyTo(r);

        return r;
    }
    /// <inheritdoc/>
    internal new JoinedEntityBuilder<T1, T2> ArrayJoin<TArray>(Expression<Func<Projection<T1, T2>, TArray>> array)
        => (JoinedEntityBuilder<T1, T2>)base.ArrayJoin(array);
    /// <inheritdoc/>
    internal new JoinedEntityBuilder<T1, T2> LeftArrayJoin<TArray>(Expression<Func<Projection<T1, T2>, TArray>> array)
        => (JoinedEntityBuilder<T1, T2>)base.LeftArrayJoin(array);
    /// <summary>Adds a <c>WHERE</c> condition while keeping the joined builder type, so the chain can end in a multi-table <c>Delete()</c>.</summary>
    /// <param name="condition">The predicate each row must satisfy.</param>
    /// <returns>This builder, for chaining.</returns>
    public new JoinedEntityBuilder<T1, T2> Where(Expression<Func<Projection<T1, T2>, bool>> condition)
        => (JoinedEntityBuilder<T1, T2>)base.Where(condition);
    /// <summary>Creates an independent copy of this builder over the same projection.</summary>
    public new JoinedEntityBuilder<T1, T2> Clone()
    {
        return (JoinedEntityBuilder<T1, T2>)CloneImp();
    }
}
/// <summary>Join builder over a three-table projection.</summary>
public class JoinedEntityBuilder<T1, T2, T3> : EntityBuilder<Projection<T1, T2, T3>>
{
    /// <summary>The builder for the left-hand <typeparamref name="T1"/> table when the join pipeline retained it; otherwise <see langword="null"/>.</summary>
    public EntityBuilder<T1>? BaseBuilder { get; init; }
    // public CommandBuilder<Projection<TEntity, TJoinEntity>> Join<TJoinEntity>(CommandBuilder<TJoinEntity> joinBuilder, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition)
    // {

    // }
    /// <summary>Initializes a builder over the accumulated projection of the previously joined entities.</summary>
    /// <param name="dataProvider">The data context used to resolve tables and generate SQL.</param>
    public JoinedEntityBuilder(IDataContext dataProvider) : base(dataProvider)
    {
        _joins = [];
    }
    /// <summary>Adds an <c>INNER JOIN</c> over <typeparamref name="T4"/>; rows with no match on the right are excluded.</summary>
    /// <typeparam name="T4">The entity type of the joined table.</typeparam>
    /// <param name="_">The builder identifying the table to join; only its table metadata is used, not its query state.</param>
    /// <param name="joinCondition">A predicate relating the current projection to the joined entity.</param>
    /// <param name="options">Optional per-join configuration, for example <c>j =&gt; j.Global()</c> or <c>j =&gt; j.WithStrictness(JoinStrictness.Any)</c>.</param>
    /// <returns>A builder over the projection extended with <typeparamref name="T4"/>.</returns>
    public new JoinedEntityBuilder<T1, T2, T3, T4> Join<T4>(EntityBuilder<T4> _, Expression<Func<Projection<T1, T2, T3>, T4, bool>> joinCondition, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.Inner, joinCondition, options);
    /// <summary>Adds a <c>LEFT JOIN</c> over <typeparamref name="T4"/>; unmatched left-hand rows are kept with <see langword="null"/> values on the right.</summary>
    /// <typeparam name="T4">The entity type of the joined table.</typeparam>
    /// <param name="_">The builder identifying the table to join; only its table metadata is used, not its query state.</param>
    /// <param name="joinCondition">A predicate relating the current projection to the joined entity.</param>
    /// <param name="options">Optional per-join configuration, for example <c>j =&gt; j.Global()</c> or <c>j =&gt; j.WithStrictness(JoinStrictness.Any)</c>.</param>
    /// <returns>A builder over the projection extended with <typeparamref name="T4"/>.</returns>
    public new JoinedEntityBuilder<T1, T2, T3, T4> LeftJoin<T4>(EntityBuilder<T4> _, Expression<Func<Projection<T1, T2, T3>, T4, bool>> joinCondition, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.Left, joinCondition, options);
    /// <summary>Adds a <c>RIGHT JOIN</c> over <typeparamref name="T4"/>; unmatched right-hand rows are kept with <see langword="null"/> values on the left.</summary>
    /// <typeparam name="T4">The entity type of the joined table.</typeparam>
    /// <param name="_">The builder identifying the table to join; only its table metadata is used, not its query state.</param>
    /// <param name="joinCondition">A predicate relating the current projection to the joined entity.</param>
    /// <param name="options">Optional per-join configuration, for example <c>j =&gt; j.Global()</c> or <c>j =&gt; j.WithStrictness(JoinStrictness.Any)</c>.</param>
    /// <returns>A builder over the projection extended with <typeparamref name="T4"/>.</returns>
    public new JoinedEntityBuilder<T1, T2, T3, T4> RightJoin<T4>(EntityBuilder<T4> _, Expression<Func<Projection<T1, T2, T3>, T4, bool>> joinCondition, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.Right, joinCondition, options);
    /// <summary>Adds a <c>FULL JOIN</c> over <typeparamref name="T4"/>; rows from both sides are kept, with <see langword="null"/> on the missing side.</summary>
    /// <typeparam name="T4">The entity type of the joined table.</typeparam>
    /// <param name="_">The builder identifying the table to join; only its table metadata is used, not its query state.</param>
    /// <param name="joinCondition">A predicate relating the current projection to the joined entity.</param>
    /// <param name="options">Optional per-join configuration, for example <c>j =&gt; j.Global()</c> or <c>j =&gt; j.WithStrictness(JoinStrictness.Any)</c>.</param>
    /// <returns>A builder over the projection extended with <typeparamref name="T4"/>.</returns>
    public new JoinedEntityBuilder<T1, T2, T3, T4> FullJoin<T4>(EntityBuilder<T4> _, Expression<Func<Projection<T1, T2, T3>, T4, bool>> joinCondition, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.Full, joinCondition, options);
    /// <summary>Adds a <c>CROSS JOIN</c> over <typeparamref name="T4"/>, producing the Cartesian product without a join condition.</summary>
    /// <typeparam name="T4">The entity type of the joined table.</typeparam>
    /// <param name="_">The builder identifying the table to join; only its table metadata is used, not its query state.</param>
    /// <param name="options">Optional per-join configuration; a cross join rejects every modifier at render time, while an APPLY folds a join hint into the statement-level hint comment on inline-hint dialects.</param>
    /// <returns>A builder over the projection extended with <typeparamref name="T4"/>.</returns>
    public new JoinedEntityBuilder<T1, T2, T3, T4> CrossJoin<T4>(EntityBuilder<T4> _, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.Cross, null, options);
    /// <summary>Adds a <c>CROSS APPLY</c> over <typeparamref name="T4"/>, evaluating the joined table once per left-hand row; rows with no match are dropped.</summary>
    /// <typeparam name="T4">The entity type of the joined table.</typeparam>
    /// <param name="_">The builder identifying the table to join; only its table metadata is used, not its query state.</param>
    /// <param name="options">Optional per-join configuration; a cross join rejects every modifier at render time, while an APPLY folds a join hint into the statement-level hint comment on inline-hint dialects.</param>
    /// <returns>A builder over the projection extended with <typeparamref name="T4"/>.</returns>
    public new JoinedEntityBuilder<T1, T2, T3, T4> CrossApply<T4>(EntityBuilder<T4> _, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.CrossApply, null, options);
    /// <summary>Adds an <c>OUTER APPLY</c> over <typeparamref name="T4"/>, evaluating the joined table once per left-hand row; unmatched left rows are kept.</summary>
    /// <typeparam name="T4">The entity type of the joined table.</typeparam>
    /// <param name="_">The builder identifying the table to join; only its table metadata is used, not its query state.</param>
    /// <param name="options">Optional per-join configuration; a cross join rejects every modifier at render time, while an APPLY folds a join hint into the statement-level hint comment on inline-hint dialects.</param>
    /// <returns>A builder over the projection extended with <typeparamref name="T4"/>.</returns>
    public new JoinedEntityBuilder<T1, T2, T3, T4> OuterApply<T4>(EntityBuilder<T4> _, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.OuterApply, null, options);
    /// <summary>Adds a ClickHouse <c>LEFT SEMI JOIN</c> over <paramref name="_"/>; only the left-hand columns survive.</summary>
    internal new JoinedEntityBuilder<T1, T2, T3> SemiJoin<T4>(EntityBuilder<T4> _, Expression<Func<Projection<T1, T2, T3>, T4, bool>> joinCondition, Action<JoinOptions>? options = null)
        => (JoinedEntityBuilder<T1, T2, T3>)base.SemiJoin(_, joinCondition, options);
    /// <summary>Adds a ClickHouse <c>LEFT ANTI JOIN</c> over <paramref name="_"/>; only the left-hand columns survive.</summary>
    internal new JoinedEntityBuilder<T1, T2, T3> AntiJoin<T4>(EntityBuilder<T4> _, Expression<Func<Projection<T1, T2, T3>, T4, bool>> joinCondition, Action<JoinOptions>? options = null)
        => (JoinedEntityBuilder<T1, T2, T3>)base.AntiJoin(_, joinCondition, options);
    /// <summary>Adds a ClickHouse <c>PASTE JOIN</c> over <paramref name="_"/>; the projection exposes both sides.</summary>
    internal new JoinedEntityBuilder<T1, T2, T3, T4> PasteJoin<T4>(EntityBuilder<T4> _, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.Paste, null, options);
    /// <summary>
    /// Alias-join seam over an existing join chain; mirrors <see cref="JoinCore{T4}"/> but returns the
    /// caller-created generated builder, serving every <see cref="JoinType"/> through
    /// <paramref name="joinType"/>.
    /// </summary>
    /// <typeparam name="TNext">The generated builder type that receives the join chain.</typeparam>
    /// <typeparam name="TNextEntity">The projection type <typeparamref name="TNext"/> is built over.</typeparam>
    /// <typeparam name="T4">The entity type being joined.</typeparam>
    /// <param name="create">Creates an empty <typeparamref name="TNext"/> bound to this builder's data context.</param>
    /// <param name="_">The builder identifying the table to join; only its table metadata is used.</param>
    /// <param name="joinCondition">A predicate relating the current projection to the joined entity.</param>
    /// <param name="joinType">The kind of join to add.</param>
    /// <param name="options">Optional per-join configuration.</param>
    /// <returns>A new <typeparamref name="TNext"/> carrying this builder's joins plus the new one.</returns>
    public new TNext JoinAlias<TNext, TNextEntity, T4>(
        Func<IDataContext, TNext> create,
        EntityBuilder<T4> _,
        Expression<Func<Projection<T1, T2, T3>, T4, bool>> joinCondition,
        JoinType joinType = JoinType.Inner,
        Action<JoinOptions>? options = null)
        where TNext : EntityBuilder<TNextEntity>
        => base.JoinAlias<TNext, TNextEntity, T4>(create, _, joinCondition, joinType, options);
    private JoinedEntityBuilder<T1, T2, T3, T4> JoinCore<T4>(EntityBuilder<T4> _, JoinType joinType, LambdaExpression? joinCondition, Action<JoinOptions>? options = null)
    {
        var opts = new JoinOptions();
        options?.Invoke(opts);
        var cb = new JoinedEntityBuilder<T1, T2, T3, T4>(DataProvider) { Logger = Logger, Query = Query, Table = Table, SourceFrom = SourceFrom, IsDistinct = IsDistinct, GroupingType = GroupingType, GroupingSets = GroupingSets, GroupByWithTotals = GroupByWithTotals, LimitByClause = LimitByClause, DistinctOnClause = DistinctOnClause, TableSampleClause = TableSampleClause, TemporalClause = TemporalClause, RowLockClause = RowLockClause, IsFinal = IsFinal, SampleRatio = SampleRatio, SampleOffset = SampleOffset, SettingsList = SettingsList, PreWhereCondition = PreWhereCondition, ArrayJoins = ArrayJoins, ArrayJoinKind = ArrayJoinKind, TableHints = TableHints, IndexHints = IndexHints, IndexHintKind = IndexHintKind, Ctes = Ctes, QuoteIdentifiers = QuoteIdentifiers, NamingConvention = NamingConvention, KeywordCase = KeywordCase };
        if (Joins is not null) cb.Joins!.AddRange(Joins);
        cb.Joins!.Add(new JoinExpression(joinCondition, joinType)
        {
            From = JoinSourceResolver.Resolve(_dataProvider, _),
            EntityType = joinCondition is null ? typeof(T4) : null,
            Strictness = opts.Strictness ?? JoinStrictness.Default,
            IsGlobal = opts.IsGlobal,
            JoinHint = opts.JoinHint,
            TableHints = opts.TableHints
        });
        cb.Ctes = CteMerge.Merge(Ctes, _.Ctes);
        return cb;
    }
    /// <inheritdoc/>
    protected override object CloneImp()
    {
        var r = new JoinedEntityBuilder<T1, T2, T3>(DataProvider) { Logger = Logger };
        CopyTo(r);
        if (Joins is not null) r.Joins = [.. Joins];
        return r;
    }
    /// <inheritdoc/>
    internal new JoinedEntityBuilder<T1, T2, T3> ArrayJoin<TArray>(Expression<Func<Projection<T1, T2, T3>, TArray>> array)
        => (JoinedEntityBuilder<T1, T2, T3>)base.ArrayJoin(array);
    /// <inheritdoc/>
    internal new JoinedEntityBuilder<T1, T2, T3> LeftArrayJoin<TArray>(Expression<Func<Projection<T1, T2, T3>, TArray>> array)
        => (JoinedEntityBuilder<T1, T2, T3>)base.LeftArrayJoin(array);
    /// <summary>Adds a <c>WHERE</c> condition while keeping the joined builder type, so the chain can end in a multi-table <c>Delete()</c>.</summary>
    /// <param name="condition">The predicate each row must satisfy.</param>
    /// <returns>This builder, for chaining.</returns>
    public new JoinedEntityBuilder<T1, T2, T3> Where(Expression<Func<Projection<T1, T2, T3>, bool>> condition)
        => (JoinedEntityBuilder<T1, T2, T3>)base.Where(condition);
    /// <summary>Creates an independent copy of this builder over the same projection.</summary>
    public new JoinedEntityBuilder<T1, T2, T3> Clone()
    {
        return (JoinedEntityBuilder<T1, T2, T3>)CloneImp();
    }
}
/// <summary>Join builder over a four-table projection.</summary>
public class JoinedEntityBuilder<T1, T2, T3, T4> : EntityBuilder<Projection<T1, T2, T3, T4>>
{
    /// <summary>Initializes a builder over the accumulated projection of the previously joined entities.</summary>
    /// <param name="dataProvider">The data context used to resolve tables and generate SQL.</param>
    public JoinedEntityBuilder(IDataContext dataProvider) : base(dataProvider)
    {
        _joins = [];
    }
    /// <summary>Adds an <c>INNER JOIN</c> over <typeparamref name="T5"/>; rows with no match on the right are excluded.</summary>
    /// <typeparam name="T5">The entity type of the joined table.</typeparam>
    /// <param name="_">The builder identifying the table to join; only its table metadata is used, not its query state.</param>
    /// <param name="joinCondition">A predicate relating the current projection to the joined entity.</param>
    /// <param name="options">Optional per-join configuration, for example <c>j =&gt; j.Global()</c> or <c>j =&gt; j.WithStrictness(JoinStrictness.Any)</c>.</param>
    /// <returns>A builder over the projection extended with <typeparamref name="T5"/>.</returns>
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5> Join<T5>(EntityBuilder<T5> _, Expression<Func<Projection<T1, T2, T3, T4>, T5, bool>> joinCondition, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.Inner, joinCondition, options);
    /// <summary>Adds a <c>LEFT JOIN</c> over <typeparamref name="T5"/>; unmatched left-hand rows are kept with <see langword="null"/> values on the right.</summary>
    /// <typeparam name="T5">The entity type of the joined table.</typeparam>
    /// <param name="_">The builder identifying the table to join; only its table metadata is used, not its query state.</param>
    /// <param name="joinCondition">A predicate relating the current projection to the joined entity.</param>
    /// <param name="options">Optional per-join configuration, for example <c>j =&gt; j.Global()</c> or <c>j =&gt; j.WithStrictness(JoinStrictness.Any)</c>.</param>
    /// <returns>A builder over the projection extended with <typeparamref name="T5"/>.</returns>
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5> LeftJoin<T5>(EntityBuilder<T5> _, Expression<Func<Projection<T1, T2, T3, T4>, T5, bool>> joinCondition, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.Left, joinCondition, options);
    /// <summary>Adds a <c>RIGHT JOIN</c> over <typeparamref name="T5"/>; unmatched right-hand rows are kept with <see langword="null"/> values on the left.</summary>
    /// <typeparam name="T5">The entity type of the joined table.</typeparam>
    /// <param name="_">The builder identifying the table to join; only its table metadata is used, not its query state.</param>
    /// <param name="joinCondition">A predicate relating the current projection to the joined entity.</param>
    /// <param name="options">Optional per-join configuration, for example <c>j =&gt; j.Global()</c> or <c>j =&gt; j.WithStrictness(JoinStrictness.Any)</c>.</param>
    /// <returns>A builder over the projection extended with <typeparamref name="T5"/>.</returns>
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5> RightJoin<T5>(EntityBuilder<T5> _, Expression<Func<Projection<T1, T2, T3, T4>, T5, bool>> joinCondition, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.Right, joinCondition, options);
    /// <summary>Adds a <c>FULL JOIN</c> over <typeparamref name="T5"/>; rows from both sides are kept, with <see langword="null"/> on the missing side.</summary>
    /// <typeparam name="T5">The entity type of the joined table.</typeparam>
    /// <param name="_">The builder identifying the table to join; only its table metadata is used, not its query state.</param>
    /// <param name="joinCondition">A predicate relating the current projection to the joined entity.</param>
    /// <param name="options">Optional per-join configuration, for example <c>j =&gt; j.Global()</c> or <c>j =&gt; j.WithStrictness(JoinStrictness.Any)</c>.</param>
    /// <returns>A builder over the projection extended with <typeparamref name="T5"/>.</returns>
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5> FullJoin<T5>(EntityBuilder<T5> _, Expression<Func<Projection<T1, T2, T3, T4>, T5, bool>> joinCondition, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.Full, joinCondition, options);
    /// <summary>Adds a <c>CROSS JOIN</c> over <typeparamref name="T5"/>, producing the Cartesian product without a join condition.</summary>
    /// <typeparam name="T5">The entity type of the joined table.</typeparam>
    /// <param name="_">The builder identifying the table to join; only its table metadata is used, not its query state.</param>
    /// <param name="options">Optional per-join configuration; a cross join rejects every modifier at render time, while an APPLY folds a join hint into the statement-level hint comment on inline-hint dialects.</param>
    /// <returns>A builder over the projection extended with <typeparamref name="T5"/>.</returns>
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5> CrossJoin<T5>(EntityBuilder<T5> _, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.Cross, null, options);
    /// <summary>Adds a <c>CROSS APPLY</c> over <typeparamref name="T5"/>, evaluating the joined table once per left-hand row; rows with no match are dropped.</summary>
    /// <typeparam name="T5">The entity type of the joined table.</typeparam>
    /// <param name="_">The builder identifying the table to join; only its table metadata is used, not its query state.</param>
    /// <param name="options">Optional per-join configuration; a cross join rejects every modifier at render time, while an APPLY folds a join hint into the statement-level hint comment on inline-hint dialects.</param>
    /// <returns>A builder over the projection extended with <typeparamref name="T5"/>.</returns>
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5> CrossApply<T5>(EntityBuilder<T5> _, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.CrossApply, null, options);
    /// <summary>Adds an <c>OUTER APPLY</c> over <typeparamref name="T5"/>, evaluating the joined table once per left-hand row; unmatched left rows are kept.</summary>
    /// <typeparam name="T5">The entity type of the joined table.</typeparam>
    /// <param name="_">The builder identifying the table to join; only its table metadata is used, not its query state.</param>
    /// <param name="options">Optional per-join configuration; a cross join rejects every modifier at render time, while an APPLY folds a join hint into the statement-level hint comment on inline-hint dialects.</param>
    /// <returns>A builder over the projection extended with <typeparamref name="T5"/>.</returns>
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5> OuterApply<T5>(EntityBuilder<T5> _, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.OuterApply, null, options);
    /// <summary>Adds a ClickHouse <c>LEFT SEMI JOIN</c> over <paramref name="_"/>; only the left-hand columns survive.</summary>
    internal new JoinedEntityBuilder<T1, T2, T3, T4> SemiJoin<T5>(EntityBuilder<T5> _, Expression<Func<Projection<T1, T2, T3, T4>, T5, bool>> joinCondition, Action<JoinOptions>? options = null)
        => (JoinedEntityBuilder<T1, T2, T3, T4>)base.SemiJoin(_, joinCondition, options);
    /// <summary>Adds a ClickHouse <c>LEFT ANTI JOIN</c> over <paramref name="_"/>; only the left-hand columns survive.</summary>
    internal new JoinedEntityBuilder<T1, T2, T3, T4> AntiJoin<T5>(EntityBuilder<T5> _, Expression<Func<Projection<T1, T2, T3, T4>, T5, bool>> joinCondition, Action<JoinOptions>? options = null)
        => (JoinedEntityBuilder<T1, T2, T3, T4>)base.AntiJoin(_, joinCondition, options);
    /// <summary>Adds a ClickHouse <c>PASTE JOIN</c> over <paramref name="_"/>; the projection exposes both sides.</summary>
    internal new JoinedEntityBuilder<T1, T2, T3, T4, T5> PasteJoin<T5>(EntityBuilder<T5> _, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.Paste, null, options);
    /// <summary>
    /// Alias-join seam over an existing join chain; mirrors <see cref="JoinCore{T5}"/> but returns the
    /// caller-created generated builder, serving every <see cref="JoinType"/> through
    /// <paramref name="joinType"/>.
    /// </summary>
    /// <typeparam name="TNext">The generated builder type that receives the join chain.</typeparam>
    /// <typeparam name="TNextEntity">The projection type <typeparamref name="TNext"/> is built over.</typeparam>
    /// <typeparam name="T5">The entity type being joined.</typeparam>
    /// <param name="create">Creates an empty <typeparamref name="TNext"/> bound to this builder's data context.</param>
    /// <param name="_">The builder identifying the table to join; only its table metadata is used.</param>
    /// <param name="joinCondition">A predicate relating the current projection to the joined entity.</param>
    /// <param name="joinType">The kind of join to add.</param>
    /// <param name="options">Optional per-join configuration.</param>
    /// <returns>A new <typeparamref name="TNext"/> carrying this builder's joins plus the new one.</returns>
    public new TNext JoinAlias<TNext, TNextEntity, T5>(
        Func<IDataContext, TNext> create,
        EntityBuilder<T5> _,
        Expression<Func<Projection<T1, T2, T3, T4>, T5, bool>> joinCondition,
        JoinType joinType = JoinType.Inner,
        Action<JoinOptions>? options = null)
        where TNext : EntityBuilder<TNextEntity>
        => base.JoinAlias<TNext, TNextEntity, T5>(create, _, joinCondition, joinType, options);
    private JoinedEntityBuilder<T1, T2, T3, T4, T5> JoinCore<T5>(EntityBuilder<T5> _, JoinType joinType, LambdaExpression? joinCondition, Action<JoinOptions>? options = null)
    {
        var opts = new JoinOptions();
        options?.Invoke(opts);
        var cb = new JoinedEntityBuilder<T1, T2, T3, T4, T5>(DataProvider) { Logger = Logger, Query = Query, Table = Table, SourceFrom = SourceFrom, IsDistinct = IsDistinct, GroupingType = GroupingType, GroupingSets = GroupingSets, GroupByWithTotals = GroupByWithTotals, LimitByClause = LimitByClause, DistinctOnClause = DistinctOnClause, TableSampleClause = TableSampleClause, TemporalClause = TemporalClause, RowLockClause = RowLockClause, IsFinal = IsFinal, SampleRatio = SampleRatio, SampleOffset = SampleOffset, SettingsList = SettingsList, PreWhereCondition = PreWhereCondition, ArrayJoins = ArrayJoins, ArrayJoinKind = ArrayJoinKind, TableHints = TableHints, IndexHints = IndexHints, IndexHintKind = IndexHintKind, Ctes = Ctes, QuoteIdentifiers = QuoteIdentifiers, NamingConvention = NamingConvention, KeywordCase = KeywordCase };
        if (Joins is not null) cb.Joins!.AddRange(Joins);
        cb.Joins!.Add(new JoinExpression(joinCondition, joinType)
        {
            From = JoinSourceResolver.Resolve(_dataProvider, _),
            EntityType = joinCondition is null ? typeof(T5) : null,
            Strictness = opts.Strictness ?? JoinStrictness.Default,
            IsGlobal = opts.IsGlobal,
            JoinHint = opts.JoinHint,
            TableHints = opts.TableHints
        });
        cb.Ctes = CteMerge.Merge(Ctes, _.Ctes);
        return cb;
    }
    /// <inheritdoc/>
    protected override object CloneImp()
    {
        var r = new JoinedEntityBuilder<T1, T2, T3, T4>(DataProvider) { Logger = Logger };
        CopyTo(r);
        if (Joins is not null) r.Joins = [.. Joins];
        return r;
    }
    /// <inheritdoc/>
    internal new JoinedEntityBuilder<T1, T2, T3, T4> ArrayJoin<TArray>(Expression<Func<Projection<T1, T2, T3, T4>, TArray>> array)
        => (JoinedEntityBuilder<T1, T2, T3, T4>)base.ArrayJoin(array);
    /// <inheritdoc/>
    internal new JoinedEntityBuilder<T1, T2, T3, T4> LeftArrayJoin<TArray>(Expression<Func<Projection<T1, T2, T3, T4>, TArray>> array)
        => (JoinedEntityBuilder<T1, T2, T3, T4>)base.LeftArrayJoin(array);
    /// <summary>Adds a <c>WHERE</c> condition while keeping the joined builder type, so the chain can end in a multi-table <c>Delete()</c>.</summary>
    /// <param name="condition">The predicate each row must satisfy.</param>
    /// <returns>This builder, for chaining.</returns>
    public new JoinedEntityBuilder<T1, T2, T3, T4> Where(Expression<Func<Projection<T1, T2, T3, T4>, bool>> condition)
        => (JoinedEntityBuilder<T1, T2, T3, T4>)base.Where(condition);
    /// <summary>Creates an independent copy of this builder over the same projection.</summary>
    public new JoinedEntityBuilder<T1, T2, T3, T4> Clone()
    {
        return (JoinedEntityBuilder<T1, T2, T3, T4>)CloneImp();
    }
}
/// <summary>Join builder over a five-table projection.</summary>
public class JoinedEntityBuilder<T1, T2, T3, T4, T5> : EntityBuilder<Projection<T1, T2, T3, T4, T5>>
{
    /// <summary>Initializes a builder over the accumulated projection of the previously joined entities.</summary>
    /// <param name="dataProvider">The data context used to resolve tables and generate SQL.</param>
    public JoinedEntityBuilder(IDataContext dataProvider) : base(dataProvider)
    {
        _joins = [];
    }
    /// <summary>Adds an <c>INNER JOIN</c> over <typeparamref name="T6"/>; rows with no match on the right are excluded.</summary>
    /// <typeparam name="T6">The entity type of the joined table.</typeparam>
    /// <param name="_">The builder identifying the table to join; only its table metadata is used, not its query state.</param>
    /// <param name="joinCondition">A predicate relating the current projection to the joined entity.</param>
    /// <param name="options">Optional per-join configuration, for example <c>j =&gt; j.Global()</c> or <c>j =&gt; j.WithStrictness(JoinStrictness.Any)</c>.</param>
    /// <returns>A builder over the projection extended with <typeparamref name="T6"/>.</returns>
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> Join<T6>(EntityBuilder<T6> _, Expression<Func<Projection<T1, T2, T3, T4, T5>, T6, bool>> joinCondition, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.Inner, joinCondition, options);
    /// <summary>Adds a <c>LEFT JOIN</c> over <typeparamref name="T6"/>; unmatched left-hand rows are kept with <see langword="null"/> values on the right.</summary>
    /// <typeparam name="T6">The entity type of the joined table.</typeparam>
    /// <param name="_">The builder identifying the table to join; only its table metadata is used, not its query state.</param>
    /// <param name="joinCondition">A predicate relating the current projection to the joined entity.</param>
    /// <param name="options">Optional per-join configuration, for example <c>j =&gt; j.Global()</c> or <c>j =&gt; j.WithStrictness(JoinStrictness.Any)</c>.</param>
    /// <returns>A builder over the projection extended with <typeparamref name="T6"/>.</returns>
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> LeftJoin<T6>(EntityBuilder<T6> _, Expression<Func<Projection<T1, T2, T3, T4, T5>, T6, bool>> joinCondition, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.Left, joinCondition, options);
    /// <summary>Adds a <c>RIGHT JOIN</c> over <typeparamref name="T6"/>; unmatched right-hand rows are kept with <see langword="null"/> values on the left.</summary>
    /// <typeparam name="T6">The entity type of the joined table.</typeparam>
    /// <param name="_">The builder identifying the table to join; only its table metadata is used, not its query state.</param>
    /// <param name="joinCondition">A predicate relating the current projection to the joined entity.</param>
    /// <param name="options">Optional per-join configuration, for example <c>j =&gt; j.Global()</c> or <c>j =&gt; j.WithStrictness(JoinStrictness.Any)</c>.</param>
    /// <returns>A builder over the projection extended with <typeparamref name="T6"/>.</returns>
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> RightJoin<T6>(EntityBuilder<T6> _, Expression<Func<Projection<T1, T2, T3, T4, T5>, T6, bool>> joinCondition, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.Right, joinCondition, options);
    /// <summary>Adds a <c>FULL JOIN</c> over <typeparamref name="T6"/>; rows from both sides are kept, with <see langword="null"/> on the missing side.</summary>
    /// <typeparam name="T6">The entity type of the joined table.</typeparam>
    /// <param name="_">The builder identifying the table to join; only its table metadata is used, not its query state.</param>
    /// <param name="joinCondition">A predicate relating the current projection to the joined entity.</param>
    /// <param name="options">Optional per-join configuration, for example <c>j =&gt; j.Global()</c> or <c>j =&gt; j.WithStrictness(JoinStrictness.Any)</c>.</param>
    /// <returns>A builder over the projection extended with <typeparamref name="T6"/>.</returns>
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> FullJoin<T6>(EntityBuilder<T6> _, Expression<Func<Projection<T1, T2, T3, T4, T5>, T6, bool>> joinCondition, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.Full, joinCondition, options);
    /// <summary>Adds a <c>CROSS JOIN</c> over <typeparamref name="T6"/>, producing the Cartesian product without a join condition.</summary>
    /// <typeparam name="T6">The entity type of the joined table.</typeparam>
    /// <param name="_">The builder identifying the table to join; only its table metadata is used, not its query state.</param>
    /// <param name="options">Optional per-join configuration; a cross join rejects every modifier at render time, while an APPLY folds a join hint into the statement-level hint comment on inline-hint dialects.</param>
    /// <returns>A builder over the projection extended with <typeparamref name="T6"/>.</returns>
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> CrossJoin<T6>(EntityBuilder<T6> _, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.Cross, null, options);
    /// <summary>Adds a <c>CROSS APPLY</c> over <typeparamref name="T6"/>, evaluating the joined table once per left-hand row; rows with no match are dropped.</summary>
    /// <typeparam name="T6">The entity type of the joined table.</typeparam>
    /// <param name="_">The builder identifying the table to join; only its table metadata is used, not its query state.</param>
    /// <param name="options">Optional per-join configuration; a cross join rejects every modifier at render time, while an APPLY folds a join hint into the statement-level hint comment on inline-hint dialects.</param>
    /// <returns>A builder over the projection extended with <typeparamref name="T6"/>.</returns>
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> CrossApply<T6>(EntityBuilder<T6> _, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.CrossApply, null, options);
    /// <summary>Adds an <c>OUTER APPLY</c> over <typeparamref name="T6"/>, evaluating the joined table once per left-hand row; unmatched left rows are kept.</summary>
    /// <typeparam name="T6">The entity type of the joined table.</typeparam>
    /// <param name="_">The builder identifying the table to join; only its table metadata is used, not its query state.</param>
    /// <param name="options">Optional per-join configuration; a cross join rejects every modifier at render time, while an APPLY folds a join hint into the statement-level hint comment on inline-hint dialects.</param>
    /// <returns>A builder over the projection extended with <typeparamref name="T6"/>.</returns>
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> OuterApply<T6>(EntityBuilder<T6> _, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.OuterApply, null, options);
    /// <summary>Adds a ClickHouse <c>LEFT SEMI JOIN</c> over <paramref name="_"/>; only the left-hand columns survive.</summary>
    internal new JoinedEntityBuilder<T1, T2, T3, T4, T5> SemiJoin<T6>(EntityBuilder<T6> _, Expression<Func<Projection<T1, T2, T3, T4, T5>, T6, bool>> joinCondition, Action<JoinOptions>? options = null)
        => (JoinedEntityBuilder<T1, T2, T3, T4, T5>)base.SemiJoin(_, joinCondition, options);
    /// <summary>Adds a ClickHouse <c>LEFT ANTI JOIN</c> over <paramref name="_"/>; only the left-hand columns survive.</summary>
    internal new JoinedEntityBuilder<T1, T2, T3, T4, T5> AntiJoin<T6>(EntityBuilder<T6> _, Expression<Func<Projection<T1, T2, T3, T4, T5>, T6, bool>> joinCondition, Action<JoinOptions>? options = null)
        => (JoinedEntityBuilder<T1, T2, T3, T4, T5>)base.AntiJoin(_, joinCondition, options);
    /// <summary>Adds a ClickHouse <c>PASTE JOIN</c> over <paramref name="_"/>; the projection exposes both sides.</summary>
    internal new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> PasteJoin<T6>(EntityBuilder<T6> _, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.Paste, null, options);
    /// <summary>
    /// Alias-join seam over an existing join chain; mirrors <see cref="JoinCore{T6}"/> but returns the
    /// caller-created generated builder, serving every <see cref="JoinType"/> through
    /// <paramref name="joinType"/>.
    /// </summary>
    /// <typeparam name="TNext">The generated builder type that receives the join chain.</typeparam>
    /// <typeparam name="TNextEntity">The projection type <typeparamref name="TNext"/> is built over.</typeparam>
    /// <typeparam name="T6">The entity type being joined.</typeparam>
    /// <param name="create">Creates an empty <typeparamref name="TNext"/> bound to this builder's data context.</param>
    /// <param name="_">The builder identifying the table to join; only its table metadata is used.</param>
    /// <param name="joinCondition">A predicate relating the current projection to the joined entity.</param>
    /// <param name="joinType">The kind of join to add.</param>
    /// <param name="options">Optional per-join configuration.</param>
    /// <returns>A new <typeparamref name="TNext"/> carrying this builder's joins plus the new one.</returns>
    public new TNext JoinAlias<TNext, TNextEntity, T6>(
        Func<IDataContext, TNext> create,
        EntityBuilder<T6> _,
        Expression<Func<Projection<T1, T2, T3, T4, T5>, T6, bool>> joinCondition,
        JoinType joinType = JoinType.Inner,
        Action<JoinOptions>? options = null)
        where TNext : EntityBuilder<TNextEntity>
        => base.JoinAlias<TNext, TNextEntity, T6>(create, _, joinCondition, joinType, options);
    private JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> JoinCore<T6>(EntityBuilder<T6> _, JoinType joinType, LambdaExpression? joinCondition, Action<JoinOptions>? options = null)
    {
        var opts = new JoinOptions();
        options?.Invoke(opts);
        var cb = new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6>(DataProvider) { Logger = Logger, Query = Query, Table = Table, SourceFrom = SourceFrom, IsDistinct = IsDistinct, GroupingType = GroupingType, GroupingSets = GroupingSets, GroupByWithTotals = GroupByWithTotals, LimitByClause = LimitByClause, DistinctOnClause = DistinctOnClause, TableSampleClause = TableSampleClause, TemporalClause = TemporalClause, RowLockClause = RowLockClause, IsFinal = IsFinal, SampleRatio = SampleRatio, SampleOffset = SampleOffset, SettingsList = SettingsList, PreWhereCondition = PreWhereCondition, ArrayJoins = ArrayJoins, ArrayJoinKind = ArrayJoinKind, TableHints = TableHints, IndexHints = IndexHints, IndexHintKind = IndexHintKind, Ctes = Ctes, QuoteIdentifiers = QuoteIdentifiers, NamingConvention = NamingConvention, KeywordCase = KeywordCase };
        if (Joins is not null) cb.Joins!.AddRange(Joins);
        cb.Joins!.Add(new JoinExpression(joinCondition, joinType)
        {
            From = JoinSourceResolver.Resolve(_dataProvider, _),
            EntityType = joinCondition is null ? typeof(T6) : null,
            Strictness = opts.Strictness ?? JoinStrictness.Default,
            IsGlobal = opts.IsGlobal,
            JoinHint = opts.JoinHint,
            TableHints = opts.TableHints
        });
        cb.Ctes = CteMerge.Merge(Ctes, _.Ctes);
        return cb;
    }
    /// <inheritdoc/>
    protected override object CloneImp()
    {
        var r = new JoinedEntityBuilder<T1, T2, T3, T4, T5>(DataProvider) { Logger = Logger };
        CopyTo(r);
        if (Joins is not null) r.Joins = [.. Joins];
        return r;
    }
    /// <inheritdoc/>
    internal new JoinedEntityBuilder<T1, T2, T3, T4, T5> ArrayJoin<TArray>(Expression<Func<Projection<T1, T2, T3, T4, T5>, TArray>> array)
        => (JoinedEntityBuilder<T1, T2, T3, T4, T5>)base.ArrayJoin(array);
    /// <inheritdoc/>
    internal new JoinedEntityBuilder<T1, T2, T3, T4, T5> LeftArrayJoin<TArray>(Expression<Func<Projection<T1, T2, T3, T4, T5>, TArray>> array)
        => (JoinedEntityBuilder<T1, T2, T3, T4, T5>)base.LeftArrayJoin(array);
    /// <summary>Adds a <c>WHERE</c> condition while keeping the joined builder type, so the chain can end in a multi-table <c>Delete()</c>.</summary>
    /// <param name="condition">The predicate each row must satisfy.</param>
    /// <returns>This builder, for chaining.</returns>
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5> Where(Expression<Func<Projection<T1, T2, T3, T4, T5>, bool>> condition)
        => (JoinedEntityBuilder<T1, T2, T3, T4, T5>)base.Where(condition);
    /// <summary>Creates an independent copy of this builder over the same projection.</summary>
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5> Clone()
    {
        return (JoinedEntityBuilder<T1, T2, T3, T4, T5>)CloneImp();
    }
}
/// <summary>Join builder over a six-table projection.</summary>
public class JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> : EntityBuilder<Projection<T1, T2, T3, T4, T5, T6>>
{
    /// <summary>Initializes a builder over the accumulated projection of the previously joined entities.</summary>
    /// <param name="dataProvider">The data context used to resolve tables and generate SQL.</param>
    public JoinedEntityBuilder(IDataContext dataProvider) : base(dataProvider)
    {
        _joins = [];
    }
    /// <summary>Adds an <c>INNER JOIN</c> over <typeparamref name="T7"/>; rows with no match on the right are excluded.</summary>
    /// <typeparam name="T7">The entity type of the joined table.</typeparam>
    /// <param name="_">The builder identifying the table to join; only its table metadata is used, not its query state.</param>
    /// <param name="joinCondition">A predicate relating the current projection to the joined entity.</param>
    /// <param name="options">Optional per-join configuration, for example <c>j =&gt; j.Global()</c> or <c>j =&gt; j.WithStrictness(JoinStrictness.Any)</c>.</param>
    /// <returns>A builder over the projection extended with <typeparamref name="T7"/>.</returns>
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> Join<T7>(EntityBuilder<T7> _, Expression<Func<Projection<T1, T2, T3, T4, T5, T6>, T7, bool>> joinCondition, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.Inner, joinCondition, options);
    /// <summary>Adds a <c>LEFT JOIN</c> over <typeparamref name="T7"/>; unmatched left-hand rows are kept with <see langword="null"/> values on the right.</summary>
    /// <typeparam name="T7">The entity type of the joined table.</typeparam>
    /// <param name="_">The builder identifying the table to join; only its table metadata is used, not its query state.</param>
    /// <param name="joinCondition">A predicate relating the current projection to the joined entity.</param>
    /// <param name="options">Optional per-join configuration, for example <c>j =&gt; j.Global()</c> or <c>j =&gt; j.WithStrictness(JoinStrictness.Any)</c>.</param>
    /// <returns>A builder over the projection extended with <typeparamref name="T7"/>.</returns>
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> LeftJoin<T7>(EntityBuilder<T7> _, Expression<Func<Projection<T1, T2, T3, T4, T5, T6>, T7, bool>> joinCondition, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.Left, joinCondition, options);
    /// <summary>Adds a <c>RIGHT JOIN</c> over <typeparamref name="T7"/>; unmatched right-hand rows are kept with <see langword="null"/> values on the left.</summary>
    /// <typeparam name="T7">The entity type of the joined table.</typeparam>
    /// <param name="_">The builder identifying the table to join; only its table metadata is used, not its query state.</param>
    /// <param name="joinCondition">A predicate relating the current projection to the joined entity.</param>
    /// <param name="options">Optional per-join configuration, for example <c>j =&gt; j.Global()</c> or <c>j =&gt; j.WithStrictness(JoinStrictness.Any)</c>.</param>
    /// <returns>A builder over the projection extended with <typeparamref name="T7"/>.</returns>
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> RightJoin<T7>(EntityBuilder<T7> _, Expression<Func<Projection<T1, T2, T3, T4, T5, T6>, T7, bool>> joinCondition, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.Right, joinCondition, options);
    /// <summary>Adds a <c>FULL JOIN</c> over <typeparamref name="T7"/>; rows from both sides are kept, with <see langword="null"/> on the missing side.</summary>
    /// <typeparam name="T7">The entity type of the joined table.</typeparam>
    /// <param name="_">The builder identifying the table to join; only its table metadata is used, not its query state.</param>
    /// <param name="joinCondition">A predicate relating the current projection to the joined entity.</param>
    /// <param name="options">Optional per-join configuration, for example <c>j =&gt; j.Global()</c> or <c>j =&gt; j.WithStrictness(JoinStrictness.Any)</c>.</param>
    /// <returns>A builder over the projection extended with <typeparamref name="T7"/>.</returns>
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> FullJoin<T7>(EntityBuilder<T7> _, Expression<Func<Projection<T1, T2, T3, T4, T5, T6>, T7, bool>> joinCondition, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.Full, joinCondition, options);
    /// <summary>Adds a <c>CROSS JOIN</c> over <typeparamref name="T7"/>, producing the Cartesian product without a join condition.</summary>
    /// <typeparam name="T7">The entity type of the joined table.</typeparam>
    /// <param name="_">The builder identifying the table to join; only its table metadata is used, not its query state.</param>
    /// <param name="options">Optional per-join configuration; a cross join rejects every modifier at render time, while an APPLY folds a join hint into the statement-level hint comment on inline-hint dialects.</param>
    /// <returns>A builder over the projection extended with <typeparamref name="T7"/>.</returns>
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> CrossJoin<T7>(EntityBuilder<T7> _, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.Cross, null, options);
    /// <summary>Adds a <c>CROSS APPLY</c> over <typeparamref name="T7"/>, evaluating the joined table once per left-hand row; rows with no match are dropped.</summary>
    /// <typeparam name="T7">The entity type of the joined table.</typeparam>
    /// <param name="_">The builder identifying the table to join; only its table metadata is used, not its query state.</param>
    /// <param name="options">Optional per-join configuration; a cross join rejects every modifier at render time, while an APPLY folds a join hint into the statement-level hint comment on inline-hint dialects.</param>
    /// <returns>A builder over the projection extended with <typeparamref name="T7"/>.</returns>
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> CrossApply<T7>(EntityBuilder<T7> _, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.CrossApply, null, options);
    /// <summary>Adds an <c>OUTER APPLY</c> over <typeparamref name="T7"/>, evaluating the joined table once per left-hand row; unmatched left rows are kept.</summary>
    /// <typeparam name="T7">The entity type of the joined table.</typeparam>
    /// <param name="_">The builder identifying the table to join; only its table metadata is used, not its query state.</param>
    /// <param name="options">Optional per-join configuration; a cross join rejects every modifier at render time, while an APPLY folds a join hint into the statement-level hint comment on inline-hint dialects.</param>
    /// <returns>A builder over the projection extended with <typeparamref name="T7"/>.</returns>
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> OuterApply<T7>(EntityBuilder<T7> _, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.OuterApply, null, options);
    /// <summary>Adds a ClickHouse <c>LEFT SEMI JOIN</c> over <paramref name="_"/>; only the left-hand columns survive.</summary>
    internal new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> SemiJoin<T7>(EntityBuilder<T7> _, Expression<Func<Projection<T1, T2, T3, T4, T5, T6>, T7, bool>> joinCondition, Action<JoinOptions>? options = null)
        => (JoinedEntityBuilder<T1, T2, T3, T4, T5, T6>)base.SemiJoin(_, joinCondition, options);
    /// <summary>Adds a ClickHouse <c>LEFT ANTI JOIN</c> over <paramref name="_"/>; only the left-hand columns survive.</summary>
    internal new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> AntiJoin<T7>(EntityBuilder<T7> _, Expression<Func<Projection<T1, T2, T3, T4, T5, T6>, T7, bool>> joinCondition, Action<JoinOptions>? options = null)
        => (JoinedEntityBuilder<T1, T2, T3, T4, T5, T6>)base.AntiJoin(_, joinCondition, options);
    /// <summary>Adds a ClickHouse <c>PASTE JOIN</c> over <paramref name="_"/>; the projection exposes both sides.</summary>
    internal new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> PasteJoin<T7>(EntityBuilder<T7> _, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.Paste, null, options);
    /// <summary>
    /// Alias-join seam over an existing join chain; mirrors <see cref="JoinCore{T7}"/> but returns the
    /// caller-created generated builder, serving every <see cref="JoinType"/> through
    /// <paramref name="joinType"/>.
    /// </summary>
    /// <typeparam name="TNext">The generated builder type that receives the join chain.</typeparam>
    /// <typeparam name="TNextEntity">The projection type <typeparamref name="TNext"/> is built over.</typeparam>
    /// <typeparam name="T7">The entity type being joined.</typeparam>
    /// <param name="create">Creates an empty <typeparamref name="TNext"/> bound to this builder's data context.</param>
    /// <param name="_">The builder identifying the table to join; only its table metadata is used.</param>
    /// <param name="joinCondition">A predicate relating the current projection to the joined entity.</param>
    /// <param name="joinType">The kind of join to add.</param>
    /// <param name="options">Optional per-join configuration.</param>
    /// <returns>A new <typeparamref name="TNext"/> carrying this builder's joins plus the new one.</returns>
    public new TNext JoinAlias<TNext, TNextEntity, T7>(
        Func<IDataContext, TNext> create,
        EntityBuilder<T7> _,
        Expression<Func<Projection<T1, T2, T3, T4, T5, T6>, T7, bool>> joinCondition,
        JoinType joinType = JoinType.Inner,
        Action<JoinOptions>? options = null)
        where TNext : EntityBuilder<TNextEntity>
        => base.JoinAlias<TNext, TNextEntity, T7>(create, _, joinCondition, joinType, options);
    private JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> JoinCore<T7>(EntityBuilder<T7> _, JoinType joinType, LambdaExpression? joinCondition, Action<JoinOptions>? options = null)
    {
        var opts = new JoinOptions();
        options?.Invoke(opts);
        var cb = new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7>(DataProvider) { Logger = Logger, Query = Query, Table = Table, SourceFrom = SourceFrom, IsDistinct = IsDistinct, GroupingType = GroupingType, GroupingSets = GroupingSets, GroupByWithTotals = GroupByWithTotals, LimitByClause = LimitByClause, DistinctOnClause = DistinctOnClause, TableSampleClause = TableSampleClause, TemporalClause = TemporalClause, RowLockClause = RowLockClause, IsFinal = IsFinal, SampleRatio = SampleRatio, SampleOffset = SampleOffset, SettingsList = SettingsList, PreWhereCondition = PreWhereCondition, ArrayJoins = ArrayJoins, ArrayJoinKind = ArrayJoinKind, TableHints = TableHints, IndexHints = IndexHints, IndexHintKind = IndexHintKind, Ctes = Ctes, QuoteIdentifiers = QuoteIdentifiers, NamingConvention = NamingConvention, KeywordCase = KeywordCase };
        if (Joins is not null) cb.Joins!.AddRange(Joins);
        cb.Joins!.Add(new JoinExpression(joinCondition, joinType)
        {
            From = JoinSourceResolver.Resolve(_dataProvider, _),
            EntityType = joinCondition is null ? typeof(T7) : null,
            Strictness = opts.Strictness ?? JoinStrictness.Default,
            IsGlobal = opts.IsGlobal,
            JoinHint = opts.JoinHint,
            TableHints = opts.TableHints
        });
        cb.Ctes = CteMerge.Merge(Ctes, _.Ctes);
        return cb;
    }
    /// <inheritdoc/>
    protected override object CloneImp()
    {
        var r = new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6>(DataProvider) { Logger = Logger };
        CopyTo(r);
        if (Joins is not null) r.Joins = [.. Joins];
        return r;
    }
    /// <inheritdoc/>
    internal new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> ArrayJoin<TArray>(Expression<Func<Projection<T1, T2, T3, T4, T5, T6>, TArray>> array)
        => (JoinedEntityBuilder<T1, T2, T3, T4, T5, T6>)base.ArrayJoin(array);
    /// <inheritdoc/>
    internal new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> LeftArrayJoin<TArray>(Expression<Func<Projection<T1, T2, T3, T4, T5, T6>, TArray>> array)
        => (JoinedEntityBuilder<T1, T2, T3, T4, T5, T6>)base.LeftArrayJoin(array);
    /// <summary>Adds a <c>WHERE</c> condition while keeping the joined builder type, so the chain can end in a multi-table <c>Delete()</c>.</summary>
    /// <param name="condition">The predicate each row must satisfy.</param>
    /// <returns>This builder, for chaining.</returns>
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> Where(Expression<Func<Projection<T1, T2, T3, T4, T5, T6>, bool>> condition)
        => (JoinedEntityBuilder<T1, T2, T3, T4, T5, T6>)base.Where(condition);
    /// <summary>Creates an independent copy of this builder over the same projection.</summary>
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> Clone()
    {
        return (JoinedEntityBuilder<T1, T2, T3, T4, T5, T6>)CloneImp();
    }
}
/// <summary>Join builder over a seven-table projection.</summary>
public class JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> : EntityBuilder<Projection<T1, T2, T3, T4, T5, T6, T7>>
{
    /// <summary>Initializes a builder over the accumulated projection of the previously joined entities.</summary>
    /// <param name="dataProvider">The data context used to resolve tables and generate SQL.</param>
    public JoinedEntityBuilder(IDataContext dataProvider) : base(dataProvider)
    {
        _joins = [];
    }
    /// <summary>Adds an <c>INNER JOIN</c> over <typeparamref name="T8"/>; rows with no match on the right are excluded.</summary>
    /// <typeparam name="T8">The entity type of the joined table.</typeparam>
    /// <param name="_">The builder identifying the table to join; only its table metadata is used, not its query state.</param>
    /// <param name="joinCondition">A predicate relating the current projection to the joined entity.</param>
    /// <param name="options">Optional per-join configuration, for example <c>j =&gt; j.Global()</c> or <c>j =&gt; j.WithStrictness(JoinStrictness.Any)</c>.</param>
    /// <returns>A builder over the projection extended with <typeparamref name="T8"/>.</returns>
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8> Join<T8>(EntityBuilder<T8> _, Expression<Func<Projection<T1, T2, T3, T4, T5, T6, T7>, T8, bool>> joinCondition, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.Inner, joinCondition, options);
    /// <summary>Adds a <c>LEFT JOIN</c> over <typeparamref name="T8"/>; unmatched left-hand rows are kept with <see langword="null"/> values on the right.</summary>
    /// <typeparam name="T8">The entity type of the joined table.</typeparam>
    /// <param name="_">The builder identifying the table to join; only its table metadata is used, not its query state.</param>
    /// <param name="joinCondition">A predicate relating the current projection to the joined entity.</param>
    /// <param name="options">Optional per-join configuration, for example <c>j =&gt; j.Global()</c> or <c>j =&gt; j.WithStrictness(JoinStrictness.Any)</c>.</param>
    /// <returns>A builder over the projection extended with <typeparamref name="T8"/>.</returns>
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8> LeftJoin<T8>(EntityBuilder<T8> _, Expression<Func<Projection<T1, T2, T3, T4, T5, T6, T7>, T8, bool>> joinCondition, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.Left, joinCondition, options);
    /// <summary>Adds a <c>RIGHT JOIN</c> over <typeparamref name="T8"/>; unmatched right-hand rows are kept with <see langword="null"/> values on the left.</summary>
    /// <typeparam name="T8">The entity type of the joined table.</typeparam>
    /// <param name="_">The builder identifying the table to join; only its table metadata is used, not its query state.</param>
    /// <param name="joinCondition">A predicate relating the current projection to the joined entity.</param>
    /// <param name="options">Optional per-join configuration, for example <c>j =&gt; j.Global()</c> or <c>j =&gt; j.WithStrictness(JoinStrictness.Any)</c>.</param>
    /// <returns>A builder over the projection extended with <typeparamref name="T8"/>.</returns>
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8> RightJoin<T8>(EntityBuilder<T8> _, Expression<Func<Projection<T1, T2, T3, T4, T5, T6, T7>, T8, bool>> joinCondition, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.Right, joinCondition, options);
    /// <summary>Adds a <c>FULL JOIN</c> over <typeparamref name="T8"/>; rows from both sides are kept, with <see langword="null"/> on the missing side.</summary>
    /// <typeparam name="T8">The entity type of the joined table.</typeparam>
    /// <param name="_">The builder identifying the table to join; only its table metadata is used, not its query state.</param>
    /// <param name="joinCondition">A predicate relating the current projection to the joined entity.</param>
    /// <param name="options">Optional per-join configuration, for example <c>j =&gt; j.Global()</c> or <c>j =&gt; j.WithStrictness(JoinStrictness.Any)</c>.</param>
    /// <returns>A builder over the projection extended with <typeparamref name="T8"/>.</returns>
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8> FullJoin<T8>(EntityBuilder<T8> _, Expression<Func<Projection<T1, T2, T3, T4, T5, T6, T7>, T8, bool>> joinCondition, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.Full, joinCondition, options);
    /// <summary>Adds a <c>CROSS JOIN</c> over <typeparamref name="T8"/>, producing the Cartesian product without a join condition.</summary>
    /// <typeparam name="T8">The entity type of the joined table.</typeparam>
    /// <param name="_">The builder identifying the table to join; only its table metadata is used, not its query state.</param>
    /// <param name="options">Optional per-join configuration; a cross join rejects every modifier at render time, while an APPLY folds a join hint into the statement-level hint comment on inline-hint dialects.</param>
    /// <returns>A builder over the projection extended with <typeparamref name="T8"/>.</returns>
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8> CrossJoin<T8>(EntityBuilder<T8> _, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.Cross, null, options);
    /// <summary>Adds a <c>CROSS APPLY</c> over <typeparamref name="T8"/>, evaluating the joined table once per left-hand row; rows with no match are dropped.</summary>
    /// <typeparam name="T8">The entity type of the joined table.</typeparam>
    /// <param name="_">The builder identifying the table to join; only its table metadata is used, not its query state.</param>
    /// <param name="options">Optional per-join configuration; a cross join rejects every modifier at render time, while an APPLY folds a join hint into the statement-level hint comment on inline-hint dialects.</param>
    /// <returns>A builder over the projection extended with <typeparamref name="T8"/>.</returns>
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8> CrossApply<T8>(EntityBuilder<T8> _, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.CrossApply, null, options);
    /// <summary>Adds an <c>OUTER APPLY</c> over <typeparamref name="T8"/>, evaluating the joined table once per left-hand row; unmatched left rows are kept.</summary>
    /// <typeparam name="T8">The entity type of the joined table.</typeparam>
    /// <param name="_">The builder identifying the table to join; only its table metadata is used, not its query state.</param>
    /// <param name="options">Optional per-join configuration; a cross join rejects every modifier at render time, while an APPLY folds a join hint into the statement-level hint comment on inline-hint dialects.</param>
    /// <returns>A builder over the projection extended with <typeparamref name="T8"/>.</returns>
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8> OuterApply<T8>(EntityBuilder<T8> _, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.OuterApply, null, options);
    /// <summary>Adds a ClickHouse <c>LEFT SEMI JOIN</c> over <paramref name="_"/>; only the left-hand columns survive.</summary>
    internal new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> SemiJoin<T8>(EntityBuilder<T8> _, Expression<Func<Projection<T1, T2, T3, T4, T5, T6, T7>, T8, bool>> joinCondition, Action<JoinOptions>? options = null)
        => (JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7>)base.SemiJoin(_, joinCondition, options);
    /// <summary>Adds a ClickHouse <c>LEFT ANTI JOIN</c> over <paramref name="_"/>; only the left-hand columns survive.</summary>
    internal new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> AntiJoin<T8>(EntityBuilder<T8> _, Expression<Func<Projection<T1, T2, T3, T4, T5, T6, T7>, T8, bool>> joinCondition, Action<JoinOptions>? options = null)
        => (JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7>)base.AntiJoin(_, joinCondition, options);
    /// <summary>Adds a ClickHouse <c>PASTE JOIN</c> over <paramref name="_"/>; the projection exposes both sides.</summary>
    internal new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8> PasteJoin<T8>(EntityBuilder<T8> _, Action<JoinOptions>? options = null)
        => JoinCore(_, JoinType.Paste, null, options);
    /// <summary>
    /// Alias-join seam over an existing join chain; mirrors <see cref="JoinCore{T8}"/> but returns the
    /// caller-created generated builder, serving every <see cref="JoinType"/> through
    /// <paramref name="joinType"/>.
    /// </summary>
    /// <typeparam name="TNext">The generated builder type that receives the join chain.</typeparam>
    /// <typeparam name="TNextEntity">The projection type <typeparamref name="TNext"/> is built over.</typeparam>
    /// <typeparam name="T8">The entity type being joined.</typeparam>
    /// <param name="create">Creates an empty <typeparamref name="TNext"/> bound to this builder's data context.</param>
    /// <param name="_">The builder identifying the table to join; only its table metadata is used.</param>
    /// <param name="joinCondition">A predicate relating the current projection to the joined entity.</param>
    /// <param name="joinType">The kind of join to add.</param>
    /// <param name="options">Optional per-join configuration.</param>
    /// <returns>A new <typeparamref name="TNext"/> carrying this builder's joins plus the new one.</returns>
    public new TNext JoinAlias<TNext, TNextEntity, T8>(
        Func<IDataContext, TNext> create,
        EntityBuilder<T8> _,
        Expression<Func<Projection<T1, T2, T3, T4, T5, T6, T7>, T8, bool>> joinCondition,
        JoinType joinType = JoinType.Inner,
        Action<JoinOptions>? options = null)
        where TNext : EntityBuilder<TNextEntity>
        => base.JoinAlias<TNext, TNextEntity, T8>(create, _, joinCondition, joinType, options);
    private JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8> JoinCore<T8>(EntityBuilder<T8> _, JoinType joinType, LambdaExpression? joinCondition, Action<JoinOptions>? options = null)
    {
        var opts = new JoinOptions();
        options?.Invoke(opts);
        var cb = new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8>(DataProvider) { Logger = Logger, Query = Query, Table = Table, SourceFrom = SourceFrom, IsDistinct = IsDistinct, GroupingType = GroupingType, GroupingSets = GroupingSets, GroupByWithTotals = GroupByWithTotals, LimitByClause = LimitByClause, DistinctOnClause = DistinctOnClause, TableSampleClause = TableSampleClause, TemporalClause = TemporalClause, RowLockClause = RowLockClause, IsFinal = IsFinal, SampleRatio = SampleRatio, SampleOffset = SampleOffset, SettingsList = SettingsList, PreWhereCondition = PreWhereCondition, ArrayJoins = ArrayJoins, ArrayJoinKind = ArrayJoinKind, TableHints = TableHints, IndexHints = IndexHints, IndexHintKind = IndexHintKind, Ctes = Ctes, QuoteIdentifiers = QuoteIdentifiers, NamingConvention = NamingConvention, KeywordCase = KeywordCase };
        if (Joins is not null) cb.Joins!.AddRange(Joins);
        cb.Joins!.Add(new JoinExpression(joinCondition, joinType)
        {
            From = JoinSourceResolver.Resolve(_dataProvider, _),
            EntityType = joinCondition is null ? typeof(T8) : null,
            Strictness = opts.Strictness ?? JoinStrictness.Default,
            IsGlobal = opts.IsGlobal,
            JoinHint = opts.JoinHint,
            TableHints = opts.TableHints
        });
        cb.Ctes = CteMerge.Merge(Ctes, _.Ctes);
        return cb;
    }
    /// <inheritdoc/>
    protected override object CloneImp()
    {
        var r = new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7>(DataProvider) { Logger = Logger };
        CopyTo(r);
        if (Joins is not null) r.Joins = [.. Joins];
        return r;
    }
    /// <inheritdoc/>
    internal new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> ArrayJoin<TArray>(Expression<Func<Projection<T1, T2, T3, T4, T5, T6, T7>, TArray>> array)
        => (JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7>)base.ArrayJoin(array);
    /// <inheritdoc/>
    internal new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> LeftArrayJoin<TArray>(Expression<Func<Projection<T1, T2, T3, T4, T5, T6, T7>, TArray>> array)
        => (JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7>)base.LeftArrayJoin(array);
    /// <summary>Adds a <c>WHERE</c> condition while keeping the joined builder type, so the chain can end in a multi-table <c>Delete()</c>.</summary>
    /// <param name="condition">The predicate each row must satisfy.</param>
    /// <returns>This builder, for chaining.</returns>
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> Where(Expression<Func<Projection<T1, T2, T3, T4, T5, T6, T7>, bool>> condition)
        => (JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7>)base.Where(condition);
    /// <summary>Creates an independent copy of this builder over the same projection.</summary>
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> Clone()
    {
        return (JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7>)CloneImp();
    }
}
/// <summary>
/// Terminal join builder for the maximum supported join arity, eight tables (T1..Item8).
/// <para>
/// It deliberately exposes no further <c>Join</c>/<c>LeftJoin</c>/<c>RightJoin</c>/<c>FullJoin</c>/
/// <c>CrossJoin</c> methods and its projection
/// (<see cref="Projection{T1, T2, T3, T4, T5, T6, T7, T8}"/>) is not <see cref="IExtendableProjection"/>,
/// so chaining a ninth join is rejected at compile time.
/// </para>
/// </summary>
/// <summary>Join builder over the maximum supported eight-table projection.</summary>
public class JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8> : EntityBuilder<Projection<T1, T2, T3, T4, T5, T6, T7, T8>>
{
    /// <summary>Initializes a builder over the accumulated projection of the previously joined entities.</summary>
    /// <param name="dataProvider">The data context used to resolve tables and generate SQL.</param>
    public JoinedEntityBuilder(IDataContext dataProvider) : base(dataProvider)
    {
        _joins = [];
    }
    /// <inheritdoc/>
    protected override object CloneImp()
    {
        var r = new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8>(DataProvider) { Logger = Logger };
        CopyTo(r);
        if (Joins is not null) r.Joins = [.. Joins];
        return r;
    }
    /// <inheritdoc/>
    internal new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8> ArrayJoin<TArray>(Expression<Func<Projection<T1, T2, T3, T4, T5, T6, T7, T8>, TArray>> array)
        => (JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8>)base.ArrayJoin(array);
    /// <inheritdoc/>
    internal new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8> LeftArrayJoin<TArray>(Expression<Func<Projection<T1, T2, T3, T4, T5, T6, T7, T8>, TArray>> array)
        => (JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8>)base.LeftArrayJoin(array);
    /// <summary>Adds a <c>WHERE</c> condition while keeping the joined builder type, so the chain can end in a multi-table <c>Delete()</c>.</summary>
    /// <param name="condition">The predicate each row must satisfy.</param>
    /// <returns>This builder, for chaining.</returns>
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8> Where(Expression<Func<Projection<T1, T2, T3, T4, T5, T6, T7, T8>, bool>> condition)
        => (JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8>)base.Where(condition);
    /// <summary>Creates an independent copy of this builder over the same projection.</summary>
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8> Clone()
    {
        return (JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8>)CloneImp();
    }
}
