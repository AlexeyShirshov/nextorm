using System.Linq.Expressions;

namespace nextorm.core;

public class EntityP2<T1, T2> : Entity<Projection<T1, T2>>
{
    public JoinExpression JoinCondition { get; set; }
    public EntityP2(IDataContext dataProvider, JoinExpression join) : base(dataProvider)
    {
        JoinCondition = join;
        _joins = [join];
    }
    public new EntityP3<T1, T2, T3> Join<T3>(Entity<T3> _, Expression<Func<Projection<T1, T2>, T3, bool>> joinCondition)
        => JoinCore(_, JoinType.Inner, joinCondition);
    public new EntityP3<T1, T2, T3> LeftJoin<T3>(Entity<T3> _, Expression<Func<Projection<T1, T2>, T3, bool>> joinCondition)
        => JoinCore(_, JoinType.Left, joinCondition);
    public new EntityP3<T1, T2, T3> RightJoin<T3>(Entity<T3> _, Expression<Func<Projection<T1, T2>, T3, bool>> joinCondition)
        => JoinCore(_, JoinType.Right, joinCondition);
    public new EntityP3<T1, T2, T3> FullJoin<T3>(Entity<T3> _, Expression<Func<Projection<T1, T2>, T3, bool>> joinCondition)
        => JoinCore(_, JoinType.Full, joinCondition);
    public new EntityP3<T1, T2, T3> CrossJoin<T3>(Entity<T3> _)
        => JoinCore(_, JoinType.Cross, null);
    private EntityP3<T1, T2, T3> JoinCore<T3>(Entity<T3> _, JoinType joinType, LambdaExpression? joinCondition)
    {
        if (Condition is not null)
            throw new NotImplementedException();

        var cb = new EntityP3<T1, T2, T3>(DataProvider) { Logger = Logger, Query = Query, Table = Table, SourceFrom = SourceFrom, IsDistinct = IsDistinct, Ctes = Ctes };
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
        var r = new EntityP2<T1, T2>(DataProvider, JoinCondition) { Logger = Logger };

        CopyTo(r);

        return r;
    }
    public new EntityP2<T1, T2> Clone()
    {
        return (EntityP2<T1, T2>)CloneImp();
    }
}
public class EntityP3<T1, T2, T3> : Entity<Projection<T1, T2, T3>>
{
    public Entity<T1>? BaseBuilder { get; init; }
    // public CommandBuilder<Projection<TEntity, TJoinEntity>> Join<TJoinEntity>(CommandBuilder<TJoinEntity> joinBuilder, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition)
    // {

    // }
    public EntityP3(IDataContext dataProvider) : base(dataProvider)
    {
        _joins = [];
    }
    public new EntityP4<T1, T2, T3, T4> Join<T4>(Entity<T4> _, Expression<Func<Projection<T1, T2, T3>, T4, bool>> joinCondition)
        => JoinCore(_, JoinType.Inner, joinCondition);
    public new EntityP4<T1, T2, T3, T4> LeftJoin<T4>(Entity<T4> _, Expression<Func<Projection<T1, T2, T3>, T4, bool>> joinCondition)
        => JoinCore(_, JoinType.Left, joinCondition);
    public new EntityP4<T1, T2, T3, T4> RightJoin<T4>(Entity<T4> _, Expression<Func<Projection<T1, T2, T3>, T4, bool>> joinCondition)
        => JoinCore(_, JoinType.Right, joinCondition);
    public new EntityP4<T1, T2, T3, T4> FullJoin<T4>(Entity<T4> _, Expression<Func<Projection<T1, T2, T3>, T4, bool>> joinCondition)
        => JoinCore(_, JoinType.Full, joinCondition);
    public new EntityP4<T1, T2, T3, T4> CrossJoin<T4>(Entity<T4> _)
        => JoinCore(_, JoinType.Cross, null);
    private EntityP4<T1, T2, T3, T4> JoinCore<T4>(Entity<T4> _, JoinType joinType, LambdaExpression? joinCondition)
    {
        var cb = new EntityP4<T1, T2, T3, T4>(DataProvider) { Logger = Logger, Query = Query, Table = Table, SourceFrom = SourceFrom, IsDistinct = IsDistinct, Ctes = Ctes };
        if (Joins is not null) cb.Joins!.AddRange(Joins);
        cb.Joins!.Add(new JoinExpression(joinCondition, joinType) { From = _dataProvider.GetFrom(typeof(T4), null)!, EntityType = joinCondition is null ? typeof(T4) : null });
        return cb;
    }
    protected override object CloneImp()
    {
        var r = new EntityP3<T1, T2, T3>(DataProvider) { Logger = Logger };
        CopyTo(r);
        if (Joins is not null) r.Joins = [.. Joins];
        return r;
    }
    public new EntityP3<T1, T2, T3> Clone()
    {
        return (EntityP3<T1, T2, T3>)CloneImp();
    }
}
public class EntityP4<T1, T2, T3, T4> : Entity<Projection<T1, T2, T3, T4>>
{
    public EntityP4(IDataContext dataProvider) : base(dataProvider)
    {
        _joins = [];
    }
    public new EntityP5<T1, T2, T3, T4, T5> Join<T5>(Entity<T5> _, Expression<Func<Projection<T1, T2, T3, T4>, T5, bool>> joinCondition)
        => JoinCore(_, JoinType.Inner, joinCondition);
    public new EntityP5<T1, T2, T3, T4, T5> LeftJoin<T5>(Entity<T5> _, Expression<Func<Projection<T1, T2, T3, T4>, T5, bool>> joinCondition)
        => JoinCore(_, JoinType.Left, joinCondition);
    public new EntityP5<T1, T2, T3, T4, T5> RightJoin<T5>(Entity<T5> _, Expression<Func<Projection<T1, T2, T3, T4>, T5, bool>> joinCondition)
        => JoinCore(_, JoinType.Right, joinCondition);
    public new EntityP5<T1, T2, T3, T4, T5> FullJoin<T5>(Entity<T5> _, Expression<Func<Projection<T1, T2, T3, T4>, T5, bool>> joinCondition)
        => JoinCore(_, JoinType.Full, joinCondition);
    public new EntityP5<T1, T2, T3, T4, T5> CrossJoin<T5>(Entity<T5> _)
        => JoinCore(_, JoinType.Cross, null);
    private EntityP5<T1, T2, T3, T4, T5> JoinCore<T5>(Entity<T5> _, JoinType joinType, LambdaExpression? joinCondition)
    {
        var cb = new EntityP5<T1, T2, T3, T4, T5>(DataProvider) { Logger = Logger, Query = Query, Table = Table, SourceFrom = SourceFrom, IsDistinct = IsDistinct, Ctes = Ctes };
        if (Joins is not null) cb.Joins!.AddRange(Joins);
        cb.Joins!.Add(new JoinExpression(joinCondition, joinType) { From = _dataProvider.GetFrom(typeof(T5), null)!, EntityType = joinCondition is null ? typeof(T5) : null });
        return cb;
    }
    protected override object CloneImp()
    {
        var r = new EntityP4<T1, T2, T3, T4>(DataProvider) { Logger = Logger };
        CopyTo(r);
        if (Joins is not null) r.Joins = [.. Joins];
        return r;
    }
    public new EntityP4<T1, T2, T3, T4> Clone()
    {
        return (EntityP4<T1, T2, T3, T4>)CloneImp();
    }
}
public class EntityP5<T1, T2, T3, T4, T5> : Entity<Projection<T1, T2, T3, T4, T5>>
{
    public EntityP5(IDataContext dataProvider) : base(dataProvider)
    {
        _joins = [];
    }
    public new EntityP6<T1, T2, T3, T4, T5, T6> Join<T6>(Entity<T6> _, Expression<Func<Projection<T1, T2, T3, T4, T5>, T6, bool>> joinCondition)
        => JoinCore(_, JoinType.Inner, joinCondition);
    public new EntityP6<T1, T2, T3, T4, T5, T6> LeftJoin<T6>(Entity<T6> _, Expression<Func<Projection<T1, T2, T3, T4, T5>, T6, bool>> joinCondition)
        => JoinCore(_, JoinType.Left, joinCondition);
    public new EntityP6<T1, T2, T3, T4, T5, T6> RightJoin<T6>(Entity<T6> _, Expression<Func<Projection<T1, T2, T3, T4, T5>, T6, bool>> joinCondition)
        => JoinCore(_, JoinType.Right, joinCondition);
    public new EntityP6<T1, T2, T3, T4, T5, T6> FullJoin<T6>(Entity<T6> _, Expression<Func<Projection<T1, T2, T3, T4, T5>, T6, bool>> joinCondition)
        => JoinCore(_, JoinType.Full, joinCondition);
    public new EntityP6<T1, T2, T3, T4, T5, T6> CrossJoin<T6>(Entity<T6> _)
        => JoinCore(_, JoinType.Cross, null);
    private EntityP6<T1, T2, T3, T4, T5, T6> JoinCore<T6>(Entity<T6> _, JoinType joinType, LambdaExpression? joinCondition)
    {
        var cb = new EntityP6<T1, T2, T3, T4, T5, T6>(DataProvider) { Logger = Logger, Query = Query, Table = Table, SourceFrom = SourceFrom, IsDistinct = IsDistinct, Ctes = Ctes };
        if (Joins is not null) cb.Joins!.AddRange(Joins);
        cb.Joins!.Add(new JoinExpression(joinCondition, joinType) { From = _dataProvider.GetFrom(typeof(T6), null)!, EntityType = joinCondition is null ? typeof(T6) : null });
        return cb;
    }
    protected override object CloneImp()
    {
        var r = new EntityP5<T1, T2, T3, T4, T5>(DataProvider) { Logger = Logger };
        CopyTo(r);
        if (Joins is not null) r.Joins = [.. Joins];
        return r;
    }
    public new EntityP5<T1, T2, T3, T4, T5> Clone()
    {
        return (EntityP5<T1, T2, T3, T4, T5>)CloneImp();
    }
}
public class EntityP6<T1, T2, T3, T4, T5, T6> : Entity<Projection<T1, T2, T3, T4, T5, T6>>
{
    public EntityP6(IDataContext dataProvider) : base(dataProvider)
    {
        _joins = [];
    }
    public new EntityP7<T1, T2, T3, T4, T5, T6, T7> Join<T7>(Entity<T7> _, Expression<Func<Projection<T1, T2, T3, T4, T5, T6>, T7, bool>> joinCondition)
        => JoinCore(_, JoinType.Inner, joinCondition);
    public new EntityP7<T1, T2, T3, T4, T5, T6, T7> LeftJoin<T7>(Entity<T7> _, Expression<Func<Projection<T1, T2, T3, T4, T5, T6>, T7, bool>> joinCondition)
        => JoinCore(_, JoinType.Left, joinCondition);
    public new EntityP7<T1, T2, T3, T4, T5, T6, T7> RightJoin<T7>(Entity<T7> _, Expression<Func<Projection<T1, T2, T3, T4, T5, T6>, T7, bool>> joinCondition)
        => JoinCore(_, JoinType.Right, joinCondition);
    public new EntityP7<T1, T2, T3, T4, T5, T6, T7> FullJoin<T7>(Entity<T7> _, Expression<Func<Projection<T1, T2, T3, T4, T5, T6>, T7, bool>> joinCondition)
        => JoinCore(_, JoinType.Full, joinCondition);
    public new EntityP7<T1, T2, T3, T4, T5, T6, T7> CrossJoin<T7>(Entity<T7> _)
        => JoinCore(_, JoinType.Cross, null);
    private EntityP7<T1, T2, T3, T4, T5, T6, T7> JoinCore<T7>(Entity<T7> _, JoinType joinType, LambdaExpression? joinCondition)
    {
        var cb = new EntityP7<T1, T2, T3, T4, T5, T6, T7>(DataProvider) { Logger = Logger, Query = Query, Table = Table, SourceFrom = SourceFrom, IsDistinct = IsDistinct, Ctes = Ctes };
        if (Joins is not null) cb.Joins!.AddRange(Joins);
        cb.Joins!.Add(new JoinExpression(joinCondition, joinType) { From = _dataProvider.GetFrom(typeof(T7), null)!, EntityType = joinCondition is null ? typeof(T7) : null });
        return cb;
    }
    protected override object CloneImp()
    {
        var r = new EntityP6<T1, T2, T3, T4, T5, T6>(DataProvider) { Logger = Logger };
        CopyTo(r);
        if (Joins is not null) r.Joins = [.. Joins];
        return r;
    }
    public new EntityP6<T1, T2, T3, T4, T5, T6> Clone()
    {
        return (EntityP6<T1, T2, T3, T4, T5, T6>)CloneImp();
    }
}
public class EntityP7<T1, T2, T3, T4, T5, T6, T7> : Entity<Projection<T1, T2, T3, T4, T5, T6, T7>>
{
    public EntityP7(IDataContext dataProvider) : base(dataProvider)
    {
        _joins = [];
    }
    public new EntityP8<T1, T2, T3, T4, T5, T6, T7, T8> Join<T8>(Entity<T8> _, Expression<Func<Projection<T1, T2, T3, T4, T5, T6, T7>, T8, bool>> joinCondition)
        => JoinCore(_, JoinType.Inner, joinCondition);
    public new EntityP8<T1, T2, T3, T4, T5, T6, T7, T8> LeftJoin<T8>(Entity<T8> _, Expression<Func<Projection<T1, T2, T3, T4, T5, T6, T7>, T8, bool>> joinCondition)
        => JoinCore(_, JoinType.Left, joinCondition);
    public new EntityP8<T1, T2, T3, T4, T5, T6, T7, T8> RightJoin<T8>(Entity<T8> _, Expression<Func<Projection<T1, T2, T3, T4, T5, T6, T7>, T8, bool>> joinCondition)
        => JoinCore(_, JoinType.Right, joinCondition);
    public new EntityP8<T1, T2, T3, T4, T5, T6, T7, T8> FullJoin<T8>(Entity<T8> _, Expression<Func<Projection<T1, T2, T3, T4, T5, T6, T7>, T8, bool>> joinCondition)
        => JoinCore(_, JoinType.Full, joinCondition);
    public new EntityP8<T1, T2, T3, T4, T5, T6, T7, T8> CrossJoin<T8>(Entity<T8> _)
        => JoinCore(_, JoinType.Cross, null);
    private EntityP8<T1, T2, T3, T4, T5, T6, T7, T8> JoinCore<T8>(Entity<T8> _, JoinType joinType, LambdaExpression? joinCondition)
    {
        var cb = new EntityP8<T1, T2, T3, T4, T5, T6, T7, T8>(DataProvider) { Logger = Logger, Query = Query, Table = Table, SourceFrom = SourceFrom, IsDistinct = IsDistinct, Ctes = Ctes };
        if (Joins is not null) cb.Joins!.AddRange(Joins);
        cb.Joins!.Add(new JoinExpression(joinCondition, joinType) { From = _dataProvider.GetFrom(typeof(T8), null)!, EntityType = joinCondition is null ? typeof(T8) : null });
        return cb;
    }
    protected override object CloneImp()
    {
        var r = new EntityP7<T1, T2, T3, T4, T5, T6, T7>(DataProvider) { Logger = Logger };
        CopyTo(r);
        if (Joins is not null) r.Joins = [.. Joins];
        return r;
    }
    public new EntityP7<T1, T2, T3, T4, T5, T6, T7> Clone()
    {
        return (EntityP7<T1, T2, T3, T4, T5, T6, T7>)CloneImp();
    }
}
/// <summary>
/// Terminal join builder for the maximum supported join arity, eight tables (T1..T8).
/// <para>
/// It deliberately exposes no further <c>Join</c>/<c>LeftJoin</c>/<c>RightJoin</c>/<c>FullJoin</c>/
/// <c>CrossJoin</c> methods and its projection
/// (<see cref="Projection{T1, T2, T3, T4, T5, T6, T7, T8}"/>) is not <see cref="IExtendableProjection"/>,
/// so chaining a ninth join is rejected at compile time.
/// </para>
/// </summary>
public class EntityP8<T1, T2, T3, T4, T5, T6, T7, T8> : Entity<Projection<T1, T2, T3, T4, T5, T6, T7, T8>>
{
    public EntityP8(IDataContext dataProvider) : base(dataProvider)
    {
        _joins = [];
    }
    protected override object CloneImp()
    {
        var r = new EntityP8<T1, T2, T3, T4, T5, T6, T7, T8>(DataProvider) { Logger = Logger };
        CopyTo(r);
        if (Joins is not null) r.Joins = [.. Joins];
        return r;
    }
    public new EntityP8<T1, T2, T3, T4, T5, T6, T7, T8> Clone()
    {
        return (EntityP8<T1, T2, T3, T4, T5, T6, T7, T8>)CloneImp();
    }
}
