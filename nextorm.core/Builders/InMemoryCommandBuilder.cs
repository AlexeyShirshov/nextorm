namespace nextorm.core;

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
    public static CommandBuilder<TEntity> WithData<TEntity>(this CommandBuilder<TEntity> builder, IEnumerable<TEntity>? data)
    {
        if (builder.DataProvider is InMemoryDataProvider inMemoryProvider)
        {
            inMemoryProvider.Data[typeof(TEntity)] = data;
        }

        return builder;
    }
    public static CommandBuilder<TEntity> WithAsyncData<TEntity>(this CommandBuilder<TEntity> builder, IAsyncEnumerable<TEntity>? data)
    {
        if (builder.DataProvider is InMemoryDataProvider inMemoryProvider)
        {
            inMemoryProvider.Data[typeof(TEntity)] = data;
        }

        return builder;
    }
    // private static void OnCommandCreated<TEntity>(CommandBuilder<TEntity> sender, QueryCommand queryCommand)
    // {
    //     if (sender.PayloadManager.TryGetPayload<InMemoryDataPayload<TEntity>>(out var payload))
    //         queryCommand.AddOrUpdatePayload(payload);

    //     if (sender.PayloadManager.TryGetPayload<InMemoryAsyncDataPayload<TEntity>>(out var asyncPayload))
    //         queryCommand.AddOrUpdatePayload(asyncPayload);
    // }
}

// public record InMemoryDataPayload<T>(IEnumerable<T>? Data) : IPayload;
// public record InMemoryAsyncDataPayload<T>(IAsyncEnumerable<T>? Data) : IPayload;