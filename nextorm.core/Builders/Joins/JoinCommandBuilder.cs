using System.Linq.Expressions;

namespace nextorm.core;

public class EntityP2<T1, T2> : Entity<Projection<T1, T2>>
{
    public JoinExpression JoinCondition { get; set; }
    public Entity<T1>? BaseBuilder { get; init; }
    public EntityP2(IDataContext dataProvider, JoinExpression join) : base(dataProvider)
    {
        JoinCondition = join;
    }
    public new EntityP3<T1, T2, T3> Join<T3>(Entity<T3> _, Expression<Func<Projection<T1, T2>, T3, bool>> joinCondition)
    {
        if (Condition is not null)
            throw new NotImplementedException();

        var cb = new EntityP3<T1, T2, T3>(DataProvider) { Logger = Logger, Query = Query/*, PayloadManager = PayloadManager*/, BaseBuilder = BaseBuilder };
        cb.Joins.Add(JoinCondition);
        cb.Joins.Add(new JoinExpression(joinCondition));
        return cb;
    }
    protected override void OnCommandCreated<TResult>(QueryCommand<TResult> cmd)
    {
        cmd.Joins.Add(JoinCondition);
        base.OnCommandCreated(cmd);
        BaseBuilder!.RaiseCommandCreated(cmd);
    }
    protected override object CloneImp()
    {
        var r = new EntityP2<T1, T2>(DataProvider, JoinCondition) { Logger = Logger, BaseBuilder = BaseBuilder };

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
    public List<JoinExpression> Joins { get; set; } = new();
    public Entity<T1>? BaseBuilder { get; init; }
    // public CommandBuilder<Projection<TEntity, TJoinEntity>> Join<TJoinEntity>(CommandBuilder<TJoinEntity> joinBuilder, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition)
    // {

    // }
    public EntityP3(IDataContext dataProvider) : base(dataProvider)
    {
    }
    protected override void OnCommandCreated<TResult>(QueryCommand<TResult> cmd)
    {
        cmd.Joins.AddRange(Joins);
        base.OnCommandCreated(cmd);
        BaseBuilder!.RaiseCommandCreated(cmd);
    }
}