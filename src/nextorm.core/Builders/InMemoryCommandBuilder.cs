namespace NextORM.Core;

// public class InMemoryCommandBuilder<TEntity> : CommandBuilder<TEntity>
// {
//     private IEnumerable<TEntity>? _data;
//     private IAsyncEnumerable<TEntity>? _asyncData;

//     public InMemoryCommandBuilder(IDataProvider dataProvider) : base(dataProvider)
//     {
//     }
//     public void WithData(IEnumerable<TEntity>? data)
//     {
//         _data = data;
//     }
//     public void WithAsyncData(IAsyncEnumerable<TEntity>? data)
//     {
//         _asyncData = data;
//     }
//     protected override void OnCommandCreated<TResult>(QueryCommand<TResult> cmd)
//     {
//         if (_data is not null)
//             cmd.AddOrUpdatePayload(()=>new InMemoryDataPayload<TEntity>(_data));

//         if (_asyncData is not null)
//             cmd.AddOrUpdatePayload(()=>new InMemoryAsyncDataPayload<TEntity>(_asyncData));

//         base.OnCommandCreated(cmd);
//     }
// }

public static class InMemoryCommandBuilderExtensions
{
    public static EntityBuilder<TEntity> WithData<TEntity>(this EntityBuilder<TEntity> builder, IEnumerable<TEntity>? data)
    {
        if (builder.DataProvider is InMemoryDataContext inMemoryProvider)
        {
            inMemoryProvider.Data[typeof(TEntity)] = data;
        }

        return builder;
    }
    public static EntityBuilder<TEntity> WithAsyncData<TEntity>(this EntityBuilder<TEntity> builder, IAsyncEnumerable<TEntity>? data)
    {
        if (builder.DataProvider is InMemoryDataContext inMemoryProvider)
        {
            inMemoryProvider.Data[typeof(TEntity)] = data;
        }

        return builder;
    }
}