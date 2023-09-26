using System.Linq.Expressions;

namespace nextorm.core;

public class InMemoryCommandBuilder<TEntity> : CommandBuilder<TEntity>
{
    private readonly Expression _accessor;
    private IEnumerable<TEntity> _data;

    public InMemoryCommandBuilder(DataProvider sqlClient, Expression accessor) : base(sqlClient)
    {
        _accessor = accessor;
    }
    public void AddRange(IEnumerable<TEntity> data)
    {
        _data = data;
    }
    protected override void OnCommandCreated<T>(IQueryCommand<T> cmd)
    {
        if (cmd is QueryCommand<T> queryCmd)
            queryCmd.Data = _data.GetEnumerator() as IEnumerator<object>;

        base.OnCommandCreated(cmd);
    }
}