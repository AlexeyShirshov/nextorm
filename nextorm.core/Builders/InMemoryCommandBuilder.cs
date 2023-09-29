using System.Linq.Expressions;

namespace nextorm.core;

public class InMemoryCommandBuilder<TEntity> : CommandBuilder<TEntity>
{
    private IEnumerable<TEntity>? _data;

    public InMemoryCommandBuilder(IDataProvider dataProvider) : base(dataProvider)
    {
    }
    public void AddRange(IEnumerable<TEntity> data)
    {
        _data = data;
    }
    protected override void OnCommandCreated<TResult>(QueryCommand<TResult> cmd)
    {
        cmd.AddOrUpdatePayload(()=>new InMemoryDataPayload<TEntity>(_data));

        base.OnCommandCreated(cmd);
    }
}
public record InMemoryDataPayload<T>(IEnumerable<T>? Data) : IPayload;