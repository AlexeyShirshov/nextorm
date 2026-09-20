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
    public JoinExpression JoinCondition { get; set; }
    public JoinedEntityBuilder(IDataContext dataProvider, JoinExpression join) : base(dataProvider)
    {
        JoinCondition = join;
        _joins = [join];
    }
    public new JoinedEntityBuilder<T1, T2, T3> Join<T3>(EntityBuilder<T3> _, Expression<Func<Projection<T1, T2>, T3, bool>> joinCondition)
        => JoinCore(_, JoinType.Inner, joinCondition);
    public new JoinedEntityBuilder<T1, T2, T3> LeftJoin<T3>(EntityBuilder<T3> _, Expression<Func<Projection<T1, T2>, T3, bool>> joinCondition)
        => JoinCore(_, JoinType.Left, joinCondition);
    public new JoinedEntityBuilder<T1, T2, T3> RightJoin<T3>(EntityBuilder<T3> _, Expression<Func<Projection<T1, T2>, T3, bool>> joinCondition)
        => JoinCore(_, JoinType.Right, joinCondition);
    public new JoinedEntityBuilder<T1, T2, T3> FullJoin<T3>(EntityBuilder<T3> _, Expression<Func<Projection<T1, T2>, T3, bool>> joinCondition)
        => JoinCore(_, JoinType.Full, joinCondition);
    public new JoinedEntityBuilder<T1, T2, T3> CrossJoin<T3>(EntityBuilder<T3> _)
        => JoinCore(_, JoinType.Cross, null);
    public new JoinedEntityBuilder<T1, T2, T3> CrossApply<T3>(EntityBuilder<T3> _)
        => JoinCore(_, JoinType.CrossApply, null);
    public new JoinedEntityBuilder<T1, T2, T3> OuterApply<T3>(EntityBuilder<T3> _)
        => JoinCore(_, JoinType.OuterApply, null);
    private JoinedEntityBuilder<T1, T2, T3> JoinCore<T3>(EntityBuilder<T3> _, JoinType joinType, LambdaExpression? joinCondition)
    {
        if (Condition is not null)
            throw new NotImplementedException();

        var cb = new JoinedEntityBuilder<T1, T2, T3>(DataProvider) { Logger = Logger, Query = Query, Table = Table, SourceFrom = SourceFrom, IsDistinct = IsDistinct, GroupingType = GroupingType, GroupingSets = GroupingSets, GroupByWithTotals = GroupByWithTotals, LimitByClause = LimitByClause, DistinctOnClause = DistinctOnClause, TableSampleClause = TableSampleClause, TemporalClause = TemporalClause, RowLockClause = RowLockClause, IsFinal = IsFinal, SampleRatio = SampleRatio, SampleOffset = SampleOffset, SettingsList = SettingsList, PreWhereCondition = PreWhereCondition, ArrayJoins = ArrayJoins, ArrayJoinKind = ArrayJoinKind, TableHints = TableHints, Ctes = Ctes };
        cb.Joins!.Add(JoinCondition);
        cb.Joins!.Add(new JoinExpression(joinCondition, joinType) { From = _dataProvider.GetFrom(typeof(T3), null)!, EntityType = joinCondition is null ? typeof(T3) : null });
        return cb;
    }
    // protected override void OnCommandCreated<TResult>(QueryCommand<TResult> cmd)
    // {
    //     cmd.Joins.Add(JoinCondition);
    //     base.OnCommandCreated(cmd);
    //     // BaseBuilder!.RaiseCommandCreated(cmd);
    // }
    protected override object CloneImp()
    {
        var r = new JoinedEntityBuilder<T1, T2>(DataProvider, JoinCondition) { Logger = Logger };

        CopyTo(r);

        return r;
    }
    /// <inheritdoc/>
    public new JoinedEntityBuilder<T1, T2> Global()
        => (JoinedEntityBuilder<T1, T2>)base.Global();
    /// <inheritdoc/>
    public new JoinedEntityBuilder<T1, T2> WithStrictness(JoinStrictness strictness)
        => (JoinedEntityBuilder<T1, T2>)base.WithStrictness(strictness);
    /// <inheritdoc/>
    public new JoinedEntityBuilder<T1, T2> ArrayJoin<TArray>(Expression<Func<Projection<T1, T2>, TArray>> array)
        => (JoinedEntityBuilder<T1, T2>)base.ArrayJoin(array);
    /// <inheritdoc/>
    public new JoinedEntityBuilder<T1, T2> LeftArrayJoin<TArray>(Expression<Func<Projection<T1, T2>, TArray>> array)
        => (JoinedEntityBuilder<T1, T2>)base.LeftArrayJoin(array);
    /// <inheritdoc/>
    protected override void OnLastJoinReplaced(JoinExpression join) => JoinCondition = join;
    public new JoinedEntityBuilder<T1, T2> Clone()
    {
        return (JoinedEntityBuilder<T1, T2>)CloneImp();
    }
}
/// <summary>Join builder over a three-table projection.</summary>
public class JoinedEntityBuilder<T1, T2, T3> : EntityBuilder<Projection<T1, T2, T3>>
{
    public EntityBuilder<T1>? BaseBuilder { get; init; }
    // public CommandBuilder<Projection<TEntity, TJoinEntity>> Join<TJoinEntity>(CommandBuilder<TJoinEntity> joinBuilder, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition)
    // {

    // }
    public JoinedEntityBuilder(IDataContext dataProvider) : base(dataProvider)
    {
        _joins = [];
    }
    public new JoinedEntityBuilder<T1, T2, T3, T4> Join<T4>(EntityBuilder<T4> _, Expression<Func<Projection<T1, T2, T3>, T4, bool>> joinCondition)
        => JoinCore(_, JoinType.Inner, joinCondition);
    public new JoinedEntityBuilder<T1, T2, T3, T4> LeftJoin<T4>(EntityBuilder<T4> _, Expression<Func<Projection<T1, T2, T3>, T4, bool>> joinCondition)
        => JoinCore(_, JoinType.Left, joinCondition);
    public new JoinedEntityBuilder<T1, T2, T3, T4> RightJoin<T4>(EntityBuilder<T4> _, Expression<Func<Projection<T1, T2, T3>, T4, bool>> joinCondition)
        => JoinCore(_, JoinType.Right, joinCondition);
    public new JoinedEntityBuilder<T1, T2, T3, T4> FullJoin<T4>(EntityBuilder<T4> _, Expression<Func<Projection<T1, T2, T3>, T4, bool>> joinCondition)
        => JoinCore(_, JoinType.Full, joinCondition);
    public new JoinedEntityBuilder<T1, T2, T3, T4> CrossJoin<T4>(EntityBuilder<T4> _)
        => JoinCore(_, JoinType.Cross, null);
    public new JoinedEntityBuilder<T1, T2, T3, T4> CrossApply<T4>(EntityBuilder<T4> _)
        => JoinCore(_, JoinType.CrossApply, null);
    public new JoinedEntityBuilder<T1, T2, T3, T4> OuterApply<T4>(EntityBuilder<T4> _)
        => JoinCore(_, JoinType.OuterApply, null);
    private JoinedEntityBuilder<T1, T2, T3, T4> JoinCore<T4>(EntityBuilder<T4> _, JoinType joinType, LambdaExpression? joinCondition)
    {
        var cb = new JoinedEntityBuilder<T1, T2, T3, T4>(DataProvider) { Logger = Logger, Query = Query, Table = Table, SourceFrom = SourceFrom, IsDistinct = IsDistinct, GroupingType = GroupingType, GroupingSets = GroupingSets, GroupByWithTotals = GroupByWithTotals, LimitByClause = LimitByClause, DistinctOnClause = DistinctOnClause, TableSampleClause = TableSampleClause, TemporalClause = TemporalClause, RowLockClause = RowLockClause, IsFinal = IsFinal, SampleRatio = SampleRatio, SampleOffset = SampleOffset, SettingsList = SettingsList, PreWhereCondition = PreWhereCondition, ArrayJoins = ArrayJoins, ArrayJoinKind = ArrayJoinKind, TableHints = TableHints, Ctes = Ctes };
        if (Joins is not null) cb.Joins!.AddRange(Joins);
        cb.Joins!.Add(new JoinExpression(joinCondition, joinType) { From = _dataProvider.GetFrom(typeof(T4), null)!, EntityType = joinCondition is null ? typeof(T4) : null });
        return cb;
    }
    protected override object CloneImp()
    {
        var r = new JoinedEntityBuilder<T1, T2, T3>(DataProvider) { Logger = Logger };
        CopyTo(r);
        if (Joins is not null) r.Joins = [.. Joins];
        return r;
    }
    /// <inheritdoc/>
    public new JoinedEntityBuilder<T1, T2, T3> Global()
        => (JoinedEntityBuilder<T1, T2, T3>)base.Global();
    /// <inheritdoc/>
    public new JoinedEntityBuilder<T1, T2, T3> WithStrictness(JoinStrictness strictness)
        => (JoinedEntityBuilder<T1, T2, T3>)base.WithStrictness(strictness);
    /// <inheritdoc/>
    public new JoinedEntityBuilder<T1, T2, T3> ArrayJoin<TArray>(Expression<Func<Projection<T1, T2, T3>, TArray>> array)
        => (JoinedEntityBuilder<T1, T2, T3>)base.ArrayJoin(array);
    /// <inheritdoc/>
    public new JoinedEntityBuilder<T1, T2, T3> LeftArrayJoin<TArray>(Expression<Func<Projection<T1, T2, T3>, TArray>> array)
        => (JoinedEntityBuilder<T1, T2, T3>)base.LeftArrayJoin(array);
    public new JoinedEntityBuilder<T1, T2, T3> Clone()
    {
        return (JoinedEntityBuilder<T1, T2, T3>)CloneImp();
    }
}
/// <summary>Join builder over a four-table projection.</summary>
public class JoinedEntityBuilder<T1, T2, T3, T4> : EntityBuilder<Projection<T1, T2, T3, T4>>
{
    public JoinedEntityBuilder(IDataContext dataProvider) : base(dataProvider)
    {
        _joins = [];
    }
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5> Join<T5>(EntityBuilder<T5> _, Expression<Func<Projection<T1, T2, T3, T4>, T5, bool>> joinCondition)
        => JoinCore(_, JoinType.Inner, joinCondition);
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5> LeftJoin<T5>(EntityBuilder<T5> _, Expression<Func<Projection<T1, T2, T3, T4>, T5, bool>> joinCondition)
        => JoinCore(_, JoinType.Left, joinCondition);
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5> RightJoin<T5>(EntityBuilder<T5> _, Expression<Func<Projection<T1, T2, T3, T4>, T5, bool>> joinCondition)
        => JoinCore(_, JoinType.Right, joinCondition);
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5> FullJoin<T5>(EntityBuilder<T5> _, Expression<Func<Projection<T1, T2, T3, T4>, T5, bool>> joinCondition)
        => JoinCore(_, JoinType.Full, joinCondition);
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5> CrossJoin<T5>(EntityBuilder<T5> _)
        => JoinCore(_, JoinType.Cross, null);
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5> CrossApply<T5>(EntityBuilder<T5> _)
        => JoinCore(_, JoinType.CrossApply, null);
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5> OuterApply<T5>(EntityBuilder<T5> _)
        => JoinCore(_, JoinType.OuterApply, null);
    private JoinedEntityBuilder<T1, T2, T3, T4, T5> JoinCore<T5>(EntityBuilder<T5> _, JoinType joinType, LambdaExpression? joinCondition)
    {
        var cb = new JoinedEntityBuilder<T1, T2, T3, T4, T5>(DataProvider) { Logger = Logger, Query = Query, Table = Table, SourceFrom = SourceFrom, IsDistinct = IsDistinct, GroupingType = GroupingType, GroupingSets = GroupingSets, GroupByWithTotals = GroupByWithTotals, LimitByClause = LimitByClause, DistinctOnClause = DistinctOnClause, TableSampleClause = TableSampleClause, TemporalClause = TemporalClause, RowLockClause = RowLockClause, IsFinal = IsFinal, SampleRatio = SampleRatio, SampleOffset = SampleOffset, SettingsList = SettingsList, PreWhereCondition = PreWhereCondition, ArrayJoins = ArrayJoins, ArrayJoinKind = ArrayJoinKind, TableHints = TableHints, Ctes = Ctes };
        if (Joins is not null) cb.Joins!.AddRange(Joins);
        cb.Joins!.Add(new JoinExpression(joinCondition, joinType) { From = _dataProvider.GetFrom(typeof(T5), null)!, EntityType = joinCondition is null ? typeof(T5) : null });
        return cb;
    }
    protected override object CloneImp()
    {
        var r = new JoinedEntityBuilder<T1, T2, T3, T4>(DataProvider) { Logger = Logger };
        CopyTo(r);
        if (Joins is not null) r.Joins = [.. Joins];
        return r;
    }
    /// <inheritdoc/>
    public new JoinedEntityBuilder<T1, T2, T3, T4> Global()
        => (JoinedEntityBuilder<T1, T2, T3, T4>)base.Global();
    /// <inheritdoc/>
    public new JoinedEntityBuilder<T1, T2, T3, T4> WithStrictness(JoinStrictness strictness)
        => (JoinedEntityBuilder<T1, T2, T3, T4>)base.WithStrictness(strictness);
    /// <inheritdoc/>
    public new JoinedEntityBuilder<T1, T2, T3, T4> ArrayJoin<TArray>(Expression<Func<Projection<T1, T2, T3, T4>, TArray>> array)
        => (JoinedEntityBuilder<T1, T2, T3, T4>)base.ArrayJoin(array);
    /// <inheritdoc/>
    public new JoinedEntityBuilder<T1, T2, T3, T4> LeftArrayJoin<TArray>(Expression<Func<Projection<T1, T2, T3, T4>, TArray>> array)
        => (JoinedEntityBuilder<T1, T2, T3, T4>)base.LeftArrayJoin(array);
    public new JoinedEntityBuilder<T1, T2, T3, T4> Clone()
    {
        return (JoinedEntityBuilder<T1, T2, T3, T4>)CloneImp();
    }
}
/// <summary>Join builder over a five-table projection.</summary>
public class JoinedEntityBuilder<T1, T2, T3, T4, T5> : EntityBuilder<Projection<T1, T2, T3, T4, T5>>
{
    public JoinedEntityBuilder(IDataContext dataProvider) : base(dataProvider)
    {
        _joins = [];
    }
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> Join<T6>(EntityBuilder<T6> _, Expression<Func<Projection<T1, T2, T3, T4, T5>, T6, bool>> joinCondition)
        => JoinCore(_, JoinType.Inner, joinCondition);
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> LeftJoin<T6>(EntityBuilder<T6> _, Expression<Func<Projection<T1, T2, T3, T4, T5>, T6, bool>> joinCondition)
        => JoinCore(_, JoinType.Left, joinCondition);
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> RightJoin<T6>(EntityBuilder<T6> _, Expression<Func<Projection<T1, T2, T3, T4, T5>, T6, bool>> joinCondition)
        => JoinCore(_, JoinType.Right, joinCondition);
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> FullJoin<T6>(EntityBuilder<T6> _, Expression<Func<Projection<T1, T2, T3, T4, T5>, T6, bool>> joinCondition)
        => JoinCore(_, JoinType.Full, joinCondition);
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> CrossJoin<T6>(EntityBuilder<T6> _)
        => JoinCore(_, JoinType.Cross, null);
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> CrossApply<T6>(EntityBuilder<T6> _)
        => JoinCore(_, JoinType.CrossApply, null);
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> OuterApply<T6>(EntityBuilder<T6> _)
        => JoinCore(_, JoinType.OuterApply, null);
    private JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> JoinCore<T6>(EntityBuilder<T6> _, JoinType joinType, LambdaExpression? joinCondition)
    {
        var cb = new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6>(DataProvider) { Logger = Logger, Query = Query, Table = Table, SourceFrom = SourceFrom, IsDistinct = IsDistinct, GroupingType = GroupingType, GroupingSets = GroupingSets, GroupByWithTotals = GroupByWithTotals, LimitByClause = LimitByClause, DistinctOnClause = DistinctOnClause, TableSampleClause = TableSampleClause, TemporalClause = TemporalClause, RowLockClause = RowLockClause, IsFinal = IsFinal, SampleRatio = SampleRatio, SampleOffset = SampleOffset, SettingsList = SettingsList, PreWhereCondition = PreWhereCondition, ArrayJoins = ArrayJoins, ArrayJoinKind = ArrayJoinKind, TableHints = TableHints, Ctes = Ctes };
        if (Joins is not null) cb.Joins!.AddRange(Joins);
        cb.Joins!.Add(new JoinExpression(joinCondition, joinType) { From = _dataProvider.GetFrom(typeof(T6), null)!, EntityType = joinCondition is null ? typeof(T6) : null });
        return cb;
    }
    protected override object CloneImp()
    {
        var r = new JoinedEntityBuilder<T1, T2, T3, T4, T5>(DataProvider) { Logger = Logger };
        CopyTo(r);
        if (Joins is not null) r.Joins = [.. Joins];
        return r;
    }
    /// <inheritdoc/>
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5> Global()
        => (JoinedEntityBuilder<T1, T2, T3, T4, T5>)base.Global();
    /// <inheritdoc/>
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5> WithStrictness(JoinStrictness strictness)
        => (JoinedEntityBuilder<T1, T2, T3, T4, T5>)base.WithStrictness(strictness);
    /// <inheritdoc/>
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5> ArrayJoin<TArray>(Expression<Func<Projection<T1, T2, T3, T4, T5>, TArray>> array)
        => (JoinedEntityBuilder<T1, T2, T3, T4, T5>)base.ArrayJoin(array);
    /// <inheritdoc/>
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5> LeftArrayJoin<TArray>(Expression<Func<Projection<T1, T2, T3, T4, T5>, TArray>> array)
        => (JoinedEntityBuilder<T1, T2, T3, T4, T5>)base.LeftArrayJoin(array);
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5> Clone()
    {
        return (JoinedEntityBuilder<T1, T2, T3, T4, T5>)CloneImp();
    }
}
/// <summary>Join builder over a six-table projection.</summary>
public class JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> : EntityBuilder<Projection<T1, T2, T3, T4, T5, T6>>
{
    public JoinedEntityBuilder(IDataContext dataProvider) : base(dataProvider)
    {
        _joins = [];
    }
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> Join<T7>(EntityBuilder<T7> _, Expression<Func<Projection<T1, T2, T3, T4, T5, T6>, T7, bool>> joinCondition)
        => JoinCore(_, JoinType.Inner, joinCondition);
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> LeftJoin<T7>(EntityBuilder<T7> _, Expression<Func<Projection<T1, T2, T3, T4, T5, T6>, T7, bool>> joinCondition)
        => JoinCore(_, JoinType.Left, joinCondition);
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> RightJoin<T7>(EntityBuilder<T7> _, Expression<Func<Projection<T1, T2, T3, T4, T5, T6>, T7, bool>> joinCondition)
        => JoinCore(_, JoinType.Right, joinCondition);
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> FullJoin<T7>(EntityBuilder<T7> _, Expression<Func<Projection<T1, T2, T3, T4, T5, T6>, T7, bool>> joinCondition)
        => JoinCore(_, JoinType.Full, joinCondition);
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> CrossJoin<T7>(EntityBuilder<T7> _)
        => JoinCore(_, JoinType.Cross, null);
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> CrossApply<T7>(EntityBuilder<T7> _)
        => JoinCore(_, JoinType.CrossApply, null);
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> OuterApply<T7>(EntityBuilder<T7> _)
        => JoinCore(_, JoinType.OuterApply, null);
    private JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> JoinCore<T7>(EntityBuilder<T7> _, JoinType joinType, LambdaExpression? joinCondition)
    {
        var cb = new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7>(DataProvider) { Logger = Logger, Query = Query, Table = Table, SourceFrom = SourceFrom, IsDistinct = IsDistinct, GroupingType = GroupingType, GroupingSets = GroupingSets, GroupByWithTotals = GroupByWithTotals, LimitByClause = LimitByClause, DistinctOnClause = DistinctOnClause, TableSampleClause = TableSampleClause, TemporalClause = TemporalClause, RowLockClause = RowLockClause, IsFinal = IsFinal, SampleRatio = SampleRatio, SampleOffset = SampleOffset, SettingsList = SettingsList, PreWhereCondition = PreWhereCondition, ArrayJoins = ArrayJoins, ArrayJoinKind = ArrayJoinKind, TableHints = TableHints, Ctes = Ctes };
        if (Joins is not null) cb.Joins!.AddRange(Joins);
        cb.Joins!.Add(new JoinExpression(joinCondition, joinType) { From = _dataProvider.GetFrom(typeof(T7), null)!, EntityType = joinCondition is null ? typeof(T7) : null });
        return cb;
    }
    protected override object CloneImp()
    {
        var r = new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6>(DataProvider) { Logger = Logger };
        CopyTo(r);
        if (Joins is not null) r.Joins = [.. Joins];
        return r;
    }
    /// <inheritdoc/>
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> Global()
        => (JoinedEntityBuilder<T1, T2, T3, T4, T5, T6>)base.Global();
    /// <inheritdoc/>
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> WithStrictness(JoinStrictness strictness)
        => (JoinedEntityBuilder<T1, T2, T3, T4, T5, T6>)base.WithStrictness(strictness);
    /// <inheritdoc/>
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> ArrayJoin<TArray>(Expression<Func<Projection<T1, T2, T3, T4, T5, T6>, TArray>> array)
        => (JoinedEntityBuilder<T1, T2, T3, T4, T5, T6>)base.ArrayJoin(array);
    /// <inheritdoc/>
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> LeftArrayJoin<TArray>(Expression<Func<Projection<T1, T2, T3, T4, T5, T6>, TArray>> array)
        => (JoinedEntityBuilder<T1, T2, T3, T4, T5, T6>)base.LeftArrayJoin(array);
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> Clone()
    {
        return (JoinedEntityBuilder<T1, T2, T3, T4, T5, T6>)CloneImp();
    }
}
/// <summary>Join builder over a seven-table projection.</summary>
public class JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> : EntityBuilder<Projection<T1, T2, T3, T4, T5, T6, T7>>
{
    public JoinedEntityBuilder(IDataContext dataProvider) : base(dataProvider)
    {
        _joins = [];
    }
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8> Join<T8>(EntityBuilder<T8> _, Expression<Func<Projection<T1, T2, T3, T4, T5, T6, T7>, T8, bool>> joinCondition)
        => JoinCore(_, JoinType.Inner, joinCondition);
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8> LeftJoin<T8>(EntityBuilder<T8> _, Expression<Func<Projection<T1, T2, T3, T4, T5, T6, T7>, T8, bool>> joinCondition)
        => JoinCore(_, JoinType.Left, joinCondition);
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8> RightJoin<T8>(EntityBuilder<T8> _, Expression<Func<Projection<T1, T2, T3, T4, T5, T6, T7>, T8, bool>> joinCondition)
        => JoinCore(_, JoinType.Right, joinCondition);
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8> FullJoin<T8>(EntityBuilder<T8> _, Expression<Func<Projection<T1, T2, T3, T4, T5, T6, T7>, T8, bool>> joinCondition)
        => JoinCore(_, JoinType.Full, joinCondition);
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8> CrossJoin<T8>(EntityBuilder<T8> _)
        => JoinCore(_, JoinType.Cross, null);
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8> CrossApply<T8>(EntityBuilder<T8> _)
        => JoinCore(_, JoinType.CrossApply, null);
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8> OuterApply<T8>(EntityBuilder<T8> _)
        => JoinCore(_, JoinType.OuterApply, null);
    private JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8> JoinCore<T8>(EntityBuilder<T8> _, JoinType joinType, LambdaExpression? joinCondition)
    {
        var cb = new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8>(DataProvider) { Logger = Logger, Query = Query, Table = Table, SourceFrom = SourceFrom, IsDistinct = IsDistinct, GroupingType = GroupingType, GroupingSets = GroupingSets, GroupByWithTotals = GroupByWithTotals, LimitByClause = LimitByClause, DistinctOnClause = DistinctOnClause, TableSampleClause = TableSampleClause, TemporalClause = TemporalClause, RowLockClause = RowLockClause, IsFinal = IsFinal, SampleRatio = SampleRatio, SampleOffset = SampleOffset, SettingsList = SettingsList, PreWhereCondition = PreWhereCondition, ArrayJoins = ArrayJoins, ArrayJoinKind = ArrayJoinKind, TableHints = TableHints, Ctes = Ctes };
        if (Joins is not null) cb.Joins!.AddRange(Joins);
        cb.Joins!.Add(new JoinExpression(joinCondition, joinType) { From = _dataProvider.GetFrom(typeof(T8), null)!, EntityType = joinCondition is null ? typeof(T8) : null });
        return cb;
    }
    protected override object CloneImp()
    {
        var r = new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7>(DataProvider) { Logger = Logger };
        CopyTo(r);
        if (Joins is not null) r.Joins = [.. Joins];
        return r;
    }
    /// <inheritdoc/>
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> Global()
        => (JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7>)base.Global();
    /// <inheritdoc/>
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> WithStrictness(JoinStrictness strictness)
        => (JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7>)base.WithStrictness(strictness);
    /// <inheritdoc/>
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> ArrayJoin<TArray>(Expression<Func<Projection<T1, T2, T3, T4, T5, T6, T7>, TArray>> array)
        => (JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7>)base.ArrayJoin(array);
    /// <inheritdoc/>
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> LeftArrayJoin<TArray>(Expression<Func<Projection<T1, T2, T3, T4, T5, T6, T7>, TArray>> array)
        => (JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7>)base.LeftArrayJoin(array);
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
    public JoinedEntityBuilder(IDataContext dataProvider) : base(dataProvider)
    {
        _joins = [];
    }
    protected override object CloneImp()
    {
        var r = new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8>(DataProvider) { Logger = Logger };
        CopyTo(r);
        if (Joins is not null) r.Joins = [.. Joins];
        return r;
    }
    /// <inheritdoc/>
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8> Global()
        => (JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8>)base.Global();
    /// <inheritdoc/>
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8> WithStrictness(JoinStrictness strictness)
        => (JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8>)base.WithStrictness(strictness);
    /// <inheritdoc/>
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8> ArrayJoin<TArray>(Expression<Func<Projection<T1, T2, T3, T4, T5, T6, T7, T8>, TArray>> array)
        => (JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8>)base.ArrayJoin(array);
    /// <inheritdoc/>
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8> LeftArrayJoin<TArray>(Expression<Func<Projection<T1, T2, T3, T4, T5, T6, T7, T8>, TArray>> array)
        => (JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8>)base.LeftArrayJoin(array);
    public new JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8> Clone()
    {
        return (JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8>)CloneImp();
    }
}
