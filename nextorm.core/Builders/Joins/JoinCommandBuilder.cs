using System.Linq.Expressions;

namespace nextorm.core;

public class CommandBuilderP2<T1, T2> : CommandBuilder<Projection<T1, T2>>
{
    public JoinExpression JoinCondition {get;set;}
    public CommandBuilder<T1> BaseBuilder { get; init; }
    public CommandBuilderP2(IDataProvider dataProvider, JoinExpression join) : base(dataProvider)
    {
        JoinCondition = join;
    }
    public new CommandBuilderP3<T1, T2, T3> Join<T3>(CommandBuilder<T3> _, Expression<Func<Projection<T1, T2>, T3, bool>> joinCondition)
    {
        var cb = new CommandBuilderP3<T1, T2, T3>(DataProvider) { Logger = Logger, Condition = Condition, Query = Query, Payload = Payload, BaseBuilder = BaseBuilder };
        cb.Joins.Add(JoinCondition);
        cb.Joins.Add(new JoinExpression(joinCondition));
        return cb;
    }
    protected override void OnCommandCreated<TResult>(QueryCommand<TResult> cmd)
    {
        cmd.Joins.Add(JoinCondition);
        base.OnCommandCreated(cmd);
        BaseBuilder.RaiseCommandCreated(cmd);
    }
}
public class CommandBuilderP3<T1, T2, T3> : CommandBuilder<Projection<T1, T2, T3>>
{
    public List<JoinExpression> Joins {get;set;} = new();
    public CommandBuilder<T1> BaseBuilder { get; init; }
    // public CommandBuilder<Projection<TEntity, TJoinEntity>> Join<TJoinEntity>(CommandBuilder<TJoinEntity> joinBuilder, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition)
    // {

    // }
    public CommandBuilderP3(IDataProvider dataProvider) : base(dataProvider)
    {
    }
    protected override void OnCommandCreated<TResult>(QueryCommand<TResult> cmd)
    {
        cmd.Joins.AddRange(Joins);
        base.OnCommandCreated(cmd);
        BaseBuilder.RaiseCommandCreated(cmd);
    }
}